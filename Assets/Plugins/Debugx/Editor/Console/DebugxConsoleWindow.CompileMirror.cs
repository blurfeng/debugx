using System.Collections.Generic;
using UnityEditor.Compilation;
using UnityEngine;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Debugx Console — compile-log mirroring. Script-compile messages are not reliably delivered by the live log
    /// channels (they are injected straight into the editor console, and only SOME compile errors also surface on the
    /// live channel), so they are read authoritatively from the internal editor console (see
    /// <see cref="EditorLogEntriesMirror"/>) and injected into the shared store, matching the native console. Kept in a
    /// partial file to keep the main viewer focused. All reflection lives in <see cref="EditorLogEntriesMirror"/>; this
    /// half is just the wiring.
    /// Debugx Console —— 编译日志镜像。脚本编译消息不由 live 日志通道可靠投递（它们被直接注入编辑器控制台，且只有“部分”
    /// 编译错误也会出现在 live 通道），故从内部编辑器控制台权威读取（见 <see cref="EditorLogEntriesMirror"/>）并注入共享
    /// store，与原生控制台对齐。拆到 partial 文件让主查看器保持聚焦。反射都在 <see cref="EditorLogEntriesMirror"/> 里；
    /// 这半边只是接线。
    /// </summary>
    public partial class DebugxConsoleWindow
    {
        // Snapshot of the compile-entry keys mirrored on the LAST scan. Each scan compares the current key set against
        // this via SetEquals: unchanged -> skip; changed -> drop the whole mirrored batch and re-inject the current one
        // (so it never accumulates — it always holds just the latest batch's keys). Reset on Clear (so a later scan can
        // re-mirror) and naturally empty after a domain reload (new window instance).
        // 上一次扫描镜像的编译条目键集的快照。每次扫描用 SetEquals 与当前键集比较：无变化 -> 跳过；有变化 -> 移除整批已镜像条目
        // 并重新注入当前批（故绝不累积——始终只保存最新一批的键）。Clear 时重置（便于之后重扫再镜像），域重载后自然清空（新窗口实例）。
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

            // Make this mirror the single source of compile messages: drop the live channel's duplicate copy (some
            // compile errors arrive there too). Only when the mirror actually resolved — otherwise let the live channel
            // deliver them (degraded, but not lost).
            // 让本镜像成为编译消息的唯一来源：丢弃 live 通道的重复副本（部分编译错误也会经它到达）。仅当镜像确实解析成功时——
            // 否则让 live 通道照常交付（降级，但不丢失）。
            if (_store != null && EditorLogEntriesMirror.EnsureAvailable())
                _store.Collector.LiveUncategorizedFilter = (condition, type) =>
                    EditorLogEntriesMirror.Available && // if the mirror later degrades, let the live channel deliver. 若镜像后续失效，交回 live 通道交付。
                    (type == LogType.Error || type == LogType.Warning) &&
                    EditorLogEntriesMirror.LooksLikeCompileMessage(condition);
        }

        private void DisableCompileMirror()
        {
            CompilationPipeline.compilationFinished -= OnCompilationFinishedForMirror;
            if (_store != null)
                _store.Collector.LiveUncategorizedFilter = null;
        }

        private void OnCompilationFinishedForMirror(object context) => _needsCompileMirror = true;

        // Called from OnEditorUpdate (main thread) BEFORE _store.Pump(), so injected entries are drained the same tick.
        // 在 OnEditorUpdate（主线程）中、_store.Pump() 之前调用，使注入的条目在同一帧被排空。
        private void PumpCompileMirror()
        {
            if (!_needsCompileMirror || _store == null) return;
            _needsCompileMirror = false;

            List<EditorLogEntriesMirror.Entry> entries = EditorLogEntriesMirror.ReadCompileEntries();

            // Compute the current authoritative compile-key set. The editor console REPLACES its compile messages on
            // every recompile (it only ever shows the latest batch), so we mirror that "replace" semantics rather than
            // just appending: compile ERRORS don't trigger a domain reload, so without this, errors from earlier failed
            // compiles linger in our buffer while the native console shows only the current batch.
            // 计算当前权威的编译键集。编辑器控制台在每次重编译时“替换”其编译消息（只显示最新批），故我们对齐这种“替换”语义
            // 而非只追加：编译“错误”不触发域重载，若不这样做，更早失败编译的错误会滞留在我们的缓冲里，而原生控制台只显示当前批。
            var currentKeys = new HashSet<string>();
            foreach (EditorLogEntriesMirror.Entry e in entries)
                currentKeys.Add(e.Key);

            // Unchanged since the last scan → leave the mirrored entries exactly where they are (no churn / no reorder).
            // 自上次扫描无变化 → 保持已镜像条目原样（不刷新、不重排）。
            if (currentKeys.SetEquals(_mirroredCompileKeys)) return;

            // The compile set changed: drop every previously-mirrored compile entry, then re-mirror the current batch.
            // RemoveWhere mutates the buffer immediately; InjectExternal queues into the collector, drained by the
            // _store.Pump() that runs right after this in OnEditorUpdate — so the buffer ends the tick holding only the
            // current batch. 编译集变化：移除此前镜像的全部编译条目，再重新镜像当前批。RemoveWhere 立即改动缓冲；InjectExternal
            // 入采集队列，由紧随其后（OnEditorUpdate 里）的 _store.Pump() 排空——故本帧结束时缓冲只含当前批。
            _store.Buffer.RemoveWhere(e => e.Category == LogEntryCategory.Compile);
            _mirroredCompileKeys.Clear();

            foreach (EditorLogEntriesMirror.Entry e in entries)
            {
                _mirroredCompileKeys.Add(e.Key);
                string stack = (!string.IsNullOrEmpty(e.File) && e.Line > 0) ? $"(at {e.File}:{e.Line})" : null;
                // Category=Compile marks it for persistence exclusion (re-sourced from LogEntries each reload).
                // Category=Compile 标记它以便持久化时排除（每次重载由 LogEntries 重新拉取）。
                _store.Collector.InjectExternal(e.Type, e.Message, stack, LogEntryCategory.Compile);
            }
        }

        // Called from ClearConsole so a subsequent scan can re-mirror compile messages still present in the console.
        // 由 ClearConsole 调用，使之后的扫描能重新镜像控制台里仍存在的编译消息。
        private void ResetCompileMirrorTracking() => _mirroredCompileKeys.Clear();
    }
}
