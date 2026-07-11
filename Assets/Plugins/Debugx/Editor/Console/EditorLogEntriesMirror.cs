using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Reads script-compile messages from Unity's internal editor console store (<c>UnityEditor.LogEntries</c>) via
    /// reflection. This is the authoritative source of compile warnings/errors for the Debugx Console: it matches the
    /// native console exactly and survives domain reloads (unlike our in-memory buffer), so callers re-read on every
    /// window enable. Note some compile errors ALSO arrive on <c>Application.logMessageReceived(Threaded)</c> (the live
    /// channel); those live copies are NOT dropped — the Console keeps them and reconciles duplicates against this
    /// authoritative set on the main thread (see DebugxConsoleWindow.CompileMirror), matching a live copy to its
    /// authoritative entry via <see cref="LooksLikeCompileMessage"/> + message text. Historical messages (logged before
    /// the window opened, or already present after a reload) are re-sourced here.
    ///
    /// All reflection is resolved once and guarded: if any internal member is missing (the API changed across Unity
    /// versions), the mirror disables itself and returns an empty list rather than throwing. Must be used on the main
    /// thread (it touches editor state).
    ///
    /// 通过反射从 Unity 内部编辑器控制台存储（<c>UnityEditor.LogEntries</c>）读取脚本编译消息。这是 Debugx Console 编译
    /// 警告/错误的权威来源：与原生控制台完全一致、且能跨域重载存活（不同于我们的内存缓冲），故调用方每次窗口启用时重读。
    /// 注意部分编译错误也会经 <c>Application.logMessageReceived(Threaded)</c>（live 通道）到达；这些 live 副本不再被丢弃——
    /// Console 保留它们，并在主线程按此权威集对账去重（见 DebugxConsoleWindow.CompileMirror），用 <see cref="LooksLikeCompileMessage"/>
    /// + 消息文本把 live 副本匹配到其权威条目。历史消息（窗口打开前打印的、或重载后已存在的）在此重新拉取。
    /// 所有反射一次解析并加保护：任一内部成员缺失（跨 Unity 版本 API 变化）时，镜像自禁用、返回空列表而非抛异常。
    /// 必须在主线程使用（会触碰编辑器状态）。
    /// </summary>
    internal static class EditorLogEntriesMirror
    {
        // Bit values of the internal UnityEditor.ConsoleWindow.Mode flags. Stable across 2019–2022. We mirror ONLY
        // script-compile messages: they are the case that matters, they survive domain reloads in LogEntries, and they
        // have a stable, recognisable text format so a live copy (some compile errors DO arrive on the live channel) can
        // be reconciled against this authoritative set. Asset-import messages are left to the live channel.
        // 内部 UnityEditor.ConsoleWindow.Mode 标志的位值。2019–2022 稳定。我们只镜像 脚本编译 消息：它是关键场景、在 LogEntries
        // 里跨域重载存活、且文本格式稳定可识别，便于把 live 副本（部分编译错误确实会经 live 通道到达）与此权威集对账。资源导入消息交给 live 通道。
        private const int ModeScriptCompileError = 1 << 11;
        private const int ModeScriptCompileWarning = 1 << 12;
        private const int CompileMask = ModeScriptCompileError | ModeScriptCompileWarning;

        // Matches Unity's C# compile message format: "...(line,col): error CS0103: ..." / "...: warning CS0414: ...".
        // Used by LooksLikeCompileMessage to spot a live-channel copy of a compile message so it can be reconciled
        // against the authoritative mirror set (absorbed into the single Compile entry) instead of showing twice.
        // 匹配 Unity 的 C# 编译消息格式："...(行,列): error CS0103: ..." / "...: warning CS0414: ..."。
        // 供 LooksLikeCompileMessage 识别 live 通道的编译消息副本，以便与权威镜像集对账（吸收进唯一的 Compile 条目）而非重复显示。
        private static readonly Regex _compileMsgRegex = new Regex(
            @"\(\d+,\d+\):\s*(error|warning)\s+\w+:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>One mirrored script-compile entry. 一条镜像来的脚本编译条目。</summary>
        internal struct Entry
        {
            public LogType Type;
            public string Message;
            public string File;
            public int Line;
            public int Mode;

            /// <summary>Stable identity used to de-dup across repeated scans. 用于多次扫描去重的稳定标识。</summary>
            public string Key => Mode + "|" + File + "|" + Line + "|" + Message;
        }

        private static bool _resolved;
        private static bool _available;

        private static MethodInfo _start;     // int StartGettingEntries()  (returns row count on most versions)
        private static MethodInfo _getCount;  // int GetCount()             (fallback count source)
        private static MethodInfo _end;       // void EndGettingEntries()
        private static MethodInfo _getEntry;  // bool GetEntryInternal(int, LogEntry)
        private static Type _logEntryType;    // UnityEditor.LogEntry
        private static FieldInfo _fMessage;   // string message / condition
        private static FieldInfo _fFile;      // string file
        private static FieldInfo _fLine;      // int line
        private static FieldInfo _fMode;      // int mode

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                Assembly editorAsm = typeof(UnityEditor.Editor).Assembly;
                Type logEntries = editorAsm.GetType("UnityEditor.LogEntries");
                _logEntryType = editorAsm.GetType("UnityEditor.LogEntry");
                if (logEntries == null || _logEntryType == null) return;

                const BindingFlags SFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                const BindingFlags IFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                _start = logEntries.GetMethod("StartGettingEntries", SFlags, null, Type.EmptyTypes, null);
                _end = logEntries.GetMethod("EndGettingEntries", SFlags, null, Type.EmptyTypes, null);
                _getCount = logEntries.GetMethod("GetCount", SFlags, null, Type.EmptyTypes, null);
                _getEntry = logEntries.GetMethod("GetEntryInternal", SFlags, null,
                    new[] { typeof(int), _logEntryType }, null);

                _fMessage = _logEntryType.GetField("message", IFlags) ?? _logEntryType.GetField("condition", IFlags);
                _fFile = _logEntryType.GetField("file", IFlags);
                _fLine = _logEntryType.GetField("line", IFlags);
                _fMode = _logEntryType.GetField("mode", IFlags);

                _available = _start != null && _end != null && _getEntry != null &&
                             _fMessage != null && _fMode != null;
            }
            catch (Exception ex)
            {
                _available = false;
                Debug.LogWarning("[Debugx] Console compile-log mirror disabled (reflection resolve failed): " + ex.Message);
            }
        }

        /// <summary>Whether the reflection into UnityEditor.LogEntries resolved successfully. 反射进 UnityEditor.LogEntries 是否解析成功。</summary>
        internal static bool Available => _available;

        /// <summary>Resolve (once) and report availability. Call on the main thread before relying on <see cref="Available"/>. 解析（一次）并返回可用性。依赖 <see cref="Available"/> 前在主线程调用。</summary>
        internal static bool EnsureAvailable()
        {
            Resolve();
            return _available;
        }

        /// <summary>
        /// Whether a log condition looks like a Unity C# compile message. Used by the Console's reconciliation to spot a
        /// live-channel copy of a compile message and absorb it into the authoritative mirror set. Thread-safe (pure).
        /// 某条日志文本是否像 Unity C# 编译消息。供 Console 对账识别 live 通道的编译消息副本并吸收进权威镜像集。线程安全（纯函数）。
        /// </summary>
        internal static bool LooksLikeCompileMessage(string condition)
            => !string.IsNullOrEmpty(condition) && _compileMsgRegex.IsMatch(condition);

        /// <summary>
        /// Read all current script-compile entries from the editor console. Returns an empty list when the internal API
        /// is unavailable. Balances Start/EndGettingEntries even on error. Main thread only.
        /// 从编辑器控制台读取当前全部 脚本编译 条目。内部 API 不可用时返回空列表。即使出错也保证 Start/EndGettingEntries 配平。仅主线程。
        /// </summary>
        internal static List<Entry> ReadCompileEntries()
        {
            var result = new List<Entry>();
            Resolve();
            if (!_available) return result;

            bool started = false;
            try
            {
                object logEntry = Activator.CreateInstance(_logEntryType, nonPublic: true);

                object startRet = _start.Invoke(null, null);
                started = true;
                int count = startRet is int c ? c
                    : (_getCount != null ? Convert.ToInt32(_getCount.Invoke(null, null)) : 0);

                var args = new object[2];
                for (int i = 0; i < count; i++)
                {
                    args[0] = i;
                    args[1] = logEntry;
                    _getEntry.Invoke(null, args);

                    int mode = Convert.ToInt32(_fMode.GetValue(logEntry));
                    if ((mode & CompileMask) == 0) continue;

                    result.Add(new Entry
                    {
                        Mode = mode,
                        Message = _fMessage.GetValue(logEntry) as string ?? string.Empty,
                        File = _fFile != null ? _fFile.GetValue(logEntry) as string : null,
                        Line = _fLine != null ? Convert.ToInt32(_fLine.GetValue(logEntry)) : 0,
                        Type = (mode & ModeScriptCompileError) != 0 ? LogType.Error : LogType.Warning,
                    });
                }
            }
            catch (Exception ex)
            {
                _available = false;
                Debug.LogWarning("[Debugx] Console compile-log mirror disabled (read failed): " + ex.Message);
            }
            finally
            {
                if (started)
                {
                    try { _end.Invoke(null, null); }
                    catch { /* best-effort unlock. 尽力解锁。 */ }
                }
            }

            return result;
        }
    }
}
