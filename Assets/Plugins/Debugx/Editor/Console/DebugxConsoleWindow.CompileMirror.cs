using System.Collections.Generic;
using UnityEditor.Compilation;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Debugx Console — compile-log mirroring. Compiler / asset-import messages never come through the live log
    /// channels, so they are read from the internal editor console (see <see cref="EditorLogEntriesMirror"/>) and
    /// injected into the shared store, matching the native console. Kept in a partial file to keep the main viewer
    /// focused. All reflection lives in <see cref="EditorLogEntriesMirror"/>; this half is just the wiring.
    /// Debugx Console —— 编译日志镜像。编译器/资源导入消息从不经 live 日志通道，故从内部编辑器控制台读取
    /// （见 <see cref="EditorLogEntriesMirror"/>）并注入共享 store，与原生控制台对齐。拆到 partial 文件让主查看器保持聚焦。
    /// 反射都在 <see cref="EditorLogEntriesMirror"/> 里；这半边只是接线。
    /// </summary>
    public partial class DebugxConsoleWindow
    {
        // Keys of compile entries already mirrored this domain session, to avoid re-adding on repeated scans. Reset on
        // Clear (so a later scan can re-mirror) and naturally empty after a domain reload (new window instance).
        // 本域会话已镜像过的编译条目的键，避免多次扫描重复添加。Clear 时重置（便于之后重扫再镜像），域重载后自然清空（新窗口实例）。
        private readonly HashSet<string> _mirroredCompileKeys = new HashSet<string>();

        // Set true to request a scan on the next editor-update tick (deferred to the main thread, after CreateGUI).
        // 置 true 表示请求在下一次 editor-update 帧扫描（延后到主线程、CreateGUI 之后）。
        private bool _needsCompileMirror;

        private void EnableCompileMirror()
        {
            // Initial scan picks up whatever is already in the console (survives domain reloads). Compile ERRORS do not
            // trigger a domain reload, so compilationFinished is the trigger that catches them while the window stays alive.
            // 首次扫描拾取控制台里已有的内容（跨域重载存活）。编译“错误”不会触发域重载，故 compilationFinished 是窗口存活期间捕获它们的触发点。
            _needsCompileMirror = true;
            CompilationPipeline.compilationFinished += OnCompilationFinishedForMirror;
        }

        private void DisableCompileMirror()
        {
            CompilationPipeline.compilationFinished -= OnCompilationFinishedForMirror;
        }

        private void OnCompilationFinishedForMirror(object context) => _needsCompileMirror = true;

        // Called from OnEditorUpdate (main thread) BEFORE _store.Pump(), so injected entries are drained the same tick.
        // 在 OnEditorUpdate（主线程）中、_store.Pump() 之前调用，使注入的条目在同一帧被排空。
        private void PumpCompileMirror()
        {
            if (!_needsCompileMirror || _store == null) return;
            _needsCompileMirror = false;

            List<EditorLogEntriesMirror.Entry> entries = EditorLogEntriesMirror.ReadCompileEntries();
            foreach (EditorLogEntriesMirror.Entry e in entries)
            {
                if (!_mirroredCompileKeys.Add(e.Key)) continue; // already mirrored this session. 本会话已镜像。
                string stack = (!string.IsNullOrEmpty(e.File) && e.Line > 0) ? $"(at {e.File}:{e.Line})" : null;
                _store.Collector.InjectExternal(e.Type, e.Message, stack);
            }
        }

        // Called from ClearConsole so a subsequent scan can re-mirror compile messages still present in the console.
        // 由 ClearConsole 调用，使之后的扫描能重新镜像控制台里仍存在的编译消息。
        private void ResetCompileMirrorDedup() => _mirroredCompileKeys.Clear();
    }
}
