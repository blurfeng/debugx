using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Reads compiler / asset-import messages from Unity's internal editor console store
    /// (<c>UnityEditor.LogEntries</c>) via reflection. Those messages are injected straight into the console and never
    /// come through <c>Application.logMessageReceived(Threaded)</c>, so the Console's live capture channel misses them;
    /// mirroring them here is how the Debugx Console reaches parity with the native console for compile warnings/errors.
    /// They also survive domain reloads (unlike our in-memory buffer), so callers re-read on every window enable.
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
        // Bit values of the internal UnityEditor.ConsoleWindow.Mode flags. Stable across 2019–2022. Only the
        // compile/import bits below matter — those are exactly the entries the live log channel does not deliver.
        // 内部 UnityEditor.ConsoleWindow.Mode 标志的位值。2019–2022 稳定。仅关注下列 编译/导入 位——正是 live 通道不会交付的条目。
        private const int ModeError = 1 << 0;
        private const int ModeAssetImportError = 1 << 6;
        private const int ModeAssetImportWarning = 1 << 7;
        private const int ModeScriptCompileError = 1 << 11;
        private const int ModeScriptCompileWarning = 1 << 12;

        private const int CompileImportMask =
            ModeAssetImportError | ModeAssetImportWarning | ModeScriptCompileError | ModeScriptCompileWarning;
        private const int ErrorMask = ModeError | ModeAssetImportError | ModeScriptCompileError;

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

        /// <summary>
        /// Read all current compiler / asset-import entries from the editor console. Returns an empty list when the
        /// internal API is unavailable. Balances Start/EndGettingEntries even on error. Main thread only.
        /// 从编辑器控制台读取当前全部 编译器/资源导入 条目。内部 API 不可用时返回空列表。即使出错也保证 Start/EndGettingEntries 配平。仅主线程。
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
                    if ((mode & CompileImportMask) == 0) continue;

                    result.Add(new Entry
                    {
                        Mode = mode,
                        Message = _fMessage.GetValue(logEntry) as string ?? string.Empty,
                        File = _fFile != null ? _fFile.GetValue(logEntry) as string : null,
                        Line = _fLine != null ? Convert.ToInt32(_fLine.GetValue(logEntry)) : 0,
                        Type = (mode & ErrorMask) != 0 ? LogType.Error : LogType.Warning,
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
