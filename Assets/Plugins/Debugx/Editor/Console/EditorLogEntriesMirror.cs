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
    /// channel); the live channel drops those (via <see cref="LooksLikeCompileMessage"/>) so they are not duplicated,
    /// and historical messages (logged before the window opened, or already present after a reload) are re-sourced here.
    ///
    /// All reflection is resolved once and guarded: if any internal member is missing (the API changed across Unity
    /// versions), the mirror disables itself and returns an empty list rather than throwing. Must be used on the main
    /// thread (it touches editor state).
    ///
    /// 通过反射从 Unity 内部编辑器控制台存储（<c>UnityEditor.LogEntries</c>）读取编译器/资源导入消息。这些消息被直接注入
    /// 控制台、从不经由 <c>Application.logMessageReceived(Threaded)</c>，故 Console 的 live 采集通道漏掉它们；在此镜像它们，
    /// 使 Debugx Console 在编译警告/错误上与原生控制台对齐。它们还能跨域重载存活（不同于我们的内存缓冲），故调用方每次窗口
    /// 启用时重读。所有反射一次解析并加保护：任一内部成员缺失（跨 Unity 版本 API 变化）时，镜像自禁用、返回空列表而非抛异常。
    /// 必须在主线程使用（会触碰编辑器状态）。
    /// </summary>
    internal static class EditorLogEntriesMirror
    {
        // Bit values of the internal UnityEditor.ConsoleWindow.Mode flags. Stable across 2019–2022. We mirror ONLY
        // script-compile messages: they are the case that matters, they survive domain reloads in LogEntries, and they
        // have a stable, recognisable text format so the live channel can suppress its duplicate copy (some compile
        // errors DO arrive on the live channel). Asset-import messages are left to the live channel.
        // 内部 UnityEditor.ConsoleWindow.Mode 标志的位值。2019–2022 稳定。我们只镜像 脚本编译 消息：它是关键场景、在 LogEntries
        // 里跨域重载存活、且文本格式稳定可识别，便于 live 通道抑制其重复副本（部分编译错误确实会经 live 通道到达）。资源导入消息交给 live 通道。
        private const int ModeScriptCompileError = 1 << 11;
        private const int ModeScriptCompileWarning = 1 << 12;
        private const int CompileMask = ModeScriptCompileError | ModeScriptCompileWarning;

        // Matches Unity's C# compile message format: "...(line,col): error CS0103: ..." / "...: warning CS0414: ...".
        // Used by LooksLikeCompileMessage so the live channel can drop its duplicate of a mirrored compile message.
        // 匹配 Unity 的 C# 编译消息格式："...(行,列): error CS0103: ..." / "...: warning CS0414: ..."。
        // 供 LooksLikeCompileMessage 使用，让 live 通道丢弃与镜像重复的编译消息。
        private static readonly Regex _compileMsgRegex = new Regex(
            @"\(\d+,\d+\):\s*(error|warning)\s+\w+:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>One mirrored compiler / asset-import entry. 一条镜像来的编译器/资源导入条目。</summary>
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
        /// Whether a log condition looks like a Unity C# compile message. The live channel uses this to drop its
        /// duplicate of a compile message that this mirror provides authoritatively. Thread-safe (pure).
        /// 某条日志文本是否像 Unity C# 编译消息。live 通道用它丢弃与本镜像重复的编译消息。线程安全（纯函数）。
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
