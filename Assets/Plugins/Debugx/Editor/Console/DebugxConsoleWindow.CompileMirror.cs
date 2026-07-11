using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Debugx Console — compile-log mirroring (reconciliation model). Script-compile messages are the one case that
    /// diverges from the live log channels: they are injected straight into the internal editor console
    /// (<c>UnityEditor.LogEntries</c>) and only SOME of them also surface on <c>Application.logMessageReceived(Threaded)</c>.
    /// To match the native console WITHOUT ever losing one, this half:
    ///  - Never drops the live-channel copies (the collector's live-drop filter was removed), so a compile-looking
    ///    Error/Warning is shown immediately as an Uncategorized entry and can never be lost to a mirror timing gap.
    ///  - Reconciles against LogEntries (the authoritative compile set): each pass removes the prior mirror entries AND
    ///    the live Uncategorized entries whose text matches the authoritative set, then re-injects the authoritative set
    ///    once each as <see cref="LogEntryCategory.Compile"/>. This "absorbs" a live copy into the single,
    ///    replace-on-recompile Compile representation (so no duplicate, and old compile errors don't linger after a fix —
    ///    matching the native console).
    ///  - A compile-looking live entry that is NOT in LogEntries at all (a false positive, or one still being flushed) is
    ///    left as a normal Uncategorized log — shown, never dropped; absorbed later if/when it becomes authoritative.
    /// Reconciliation runs every editor-update tick inside a short window after each trigger (enable / compilationFinished
    /// / Clear), which closes the LogEntries async-flush race that previously made compile errors "sometimes" go missing.
    /// All reflection lives in <see cref="EditorLogEntriesMirror"/>; this half is the wiring + buffer reconciliation.
    /// Debugx Console —— 编译日志镜像（对账模型）。脚本编译消息是唯一与 live 日志通道有出入的场景：它们被直接注入内部编辑器控制台
    /// （<c>UnityEditor.LogEntries</c>），且只有“部分”也会出现在 <c>Application.logMessageReceived(Threaded)</c> 上。
    /// 为在“绝不丢失”的前提下对齐原生控制台，这半边：
    ///  - 不再丢弃 live 通道副本（采集器的 live 丢弃过滤器已移除），故编译类 Error/Warning 会即时以“未分类”条目显示，绝不会因镜像时序而丢失。
    ///  - 以 LogEntries（权威编译集合）为准对账：每次移除“此前镜像的编译条目”与“文本匹配权威集的 live 未分类条目”，再把权威集合按
    ///    <see cref="LogEntryCategory.Compile"/> 各注入一次。由此把 live 副本“吸收”进唯一的、可随重编译替换的 Compile 表示
    ///    （不重复，修复后旧编译错误也不滞留——与原生一致）。
    ///  - 根本不在 LogEntries 里的“像编译消息”的 live 条目（误判，或正在刷入中）保留为普通“未分类”日志——显示、不丢；之后若成为权威再被吸收。
    /// 对账在每次触发（启用 / compilationFinished / Clear）后的一小段窗口内逐帧执行，堵住此前令编译错误“偶尔”缺失的 LogEntries 异步刷入竞态。
    /// 反射都在 <see cref="EditorLogEntriesMirror"/> 里；这半边是接线 + 缓冲对账。
    /// </summary>
    public partial class DebugxConsoleWindow
    {
        // Keys of the authoritative compile entries currently mirrored as Compile entries. Compared each pass so a pass
        // that would produce the same set (and has nothing to absorb) leaves the buffer untouched — no churn / no reorder.
        // Reset on Clear; naturally empty after a domain reload (new window instance).
        // 当前以 Compile 条目镜像的权威编译条目键集。每次对账比较：若产出的集合不变且无待吸收项，则不动缓冲——不刷新、不重排。
        // Clear 时重置；域重载后自然清空（新窗口实例）。
        private readonly HashSet<string> _mirroredCompileKeys = new HashSet<string>();

        // Reconcile on every editor-update tick until this EditorApplication.timeSinceStartup. Opened/extended on
        // enable / compilationFinished / Clear so a short window of per-tick reconciliation absorbs both late-flushed
        // LogEntries and just-arrived live copies. Outside the window PumpCompileMirror is a single cheap comparison.
        // 逐帧对账直到此 EditorApplication.timeSinceStartup。启用 / compilationFinished / Clear 时打开/延长，用一小段逐帧对账窗口
        // 吸收“LogEntries 晚刷入”与“刚到达的 live 副本”。窗口外 PumpCompileMirror 只是一次廉价比较。
        private double _compileRescanUntil;

        // Length of the post-trigger reconciliation window (seconds). Generous enough to cover LogEntries' async flush
        // after a compile finishes; only active right after a trigger, so the cost is negligible.
        // 触发后对账窗口时长（秒）。足以覆盖编译完成后 LogEntries 的异步刷入；仅在触发后短暂生效，开销可忽略。
        private const double CompileRescanWindowSeconds = 1.0;

        private void EnableCompileMirror()
        {
            // compilationFinished catches compile ERRORS (which do NOT trigger a domain reload, so the window stays alive
            // to reconcile them). The initial rescan picks up whatever compile messages already exist (survives domain
            // reloads, or when the window is opened after errors were already produced).
            // compilationFinished 捕获编译“错误”（其不触发域重载，窗口存活期间正好对账它们）。首次重扫拾取控制台里已有的编译消息
            //（跨域重载存活，或窗口在错误已产生之后才打开）。
            CompilationPipeline.compilationFinished += OnCompilationFinishedForMirror;
            if (EditorLogEntriesMirror.EnsureAvailable())
                RequestCompileRescan();
        }

        private void DisableCompileMirror()
        {
            CompilationPipeline.compilationFinished -= OnCompilationFinishedForMirror;
        }

        private void OnCompilationFinishedForMirror(object context) => RequestCompileRescan();

        // Open (or extend) the per-tick reconciliation window. 打开（或延长）逐帧对账窗口。
        private void RequestCompileRescan()
            => _compileRescanUntil = EditorApplication.timeSinceStartup + CompileRescanWindowSeconds;

        // Called from OnEditorUpdate (main thread) BEFORE _store.Pump(), so entries injected by a reconcile are drained
        // the same tick. Reconciles only inside the active window; outside it this is a single comparison.
        // 在 OnEditorUpdate（主线程）中、_store.Pump() 之前调用，使对账注入的条目在同一帧被排空。仅在活动窗口内对账；窗口外只是一次比较。
        private void PumpCompileMirror()
        {
            if (_store == null) return;
            if (EditorApplication.timeSinceStartup > _compileRescanUntil) return;
            ReconcileCompileEntries();
        }

        // Make the buffer's compile representation match LogEntries exactly, absorbing any live-channel copies into a
        // single replace-on-recompile Compile entry per authoritative message. See the class summary for the rationale.
        // 让缓冲的编译表示与 LogEntries 完全一致：把 live 通道副本吸收成“每条权威消息一条、可随重编译替换的 Compile 条目”。理由见类摘要。
        private void ReconcileCompileEntries()
        {
            List<EditorLogEntriesMirror.Entry> entries = EditorLogEntriesMirror.ReadCompileEntries();

            // Authoritative compile set: keys (change detection) + normalized message texts (matching live copies).
            // 权威编译集：键（变更检测）+ 归一化消息文本（匹配 live 副本）。
            var authKeys = new HashSet<string>();
            var authTexts = new HashSet<string>();
            foreach (EditorLogEntriesMirror.Entry e in entries)
            {
                authKeys.Add(e.Key);
                authTexts.Add(NormalizeCompileText(e.Message));
            }

            // Any live (Uncategorized) compile-looking entry that matches the authoritative set is waiting to be absorbed.
            // 任何“文本匹配权威集”的 live（未分类）编译类条目，都在等待被吸收。
            bool hasAbsorbable = false;
            LogRingBuffer buf = _store.Buffer;
            for (int i = 0; i < buf.Count; i++)
            {
                DebugxLogEntry e = buf[i];
                if (IsAbsorbableLiveCompile(e, authTexts)) { hasAbsorbable = true; break; }
            }

            // Steady state: authoritative set unchanged AND nothing to absorb → leave the buffer untouched (no churn).
            // 稳定态：权威集不变且无待吸收项 → 不动缓冲（不抖动）。
            if (!hasAbsorbable && authKeys.SetEquals(_mirroredCompileKeys)) return;

            // Rebuild: drop the prior mirror entries + the live copies we are absorbing, then re-inject the authoritative
            // set once each as Compile. RemoveWhere mutates now; InjectExternal queues into the collector, drained by the
            // _store.Pump() that runs right after this in OnEditorUpdate — so the buffer ends the tick holding exactly one
            // Compile entry per authoritative message, while non-authoritative compile-looking live entries stay as logs.
            // 重建：移除“此前镜像的编译条目 + 正被吸收的 live 副本”，再把权威集各注入一次为 Compile。RemoveWhere 立即改动缓冲；
            // InjectExternal 入采集队列，由紧随其后（OnEditorUpdate 里）的 _store.Pump() 排空——故本帧结束时缓冲“每条权威消息恰好一条
            // Compile 条目”，而“非权威的像编译消息的 live 条目”仍保留为普通日志。
            _store.Buffer.RemoveWhere(e =>
                e.Category == LogEntryCategory.Compile || IsAbsorbableLiveCompile(e, authTexts));

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

        // A live (Uncategorized) entry whose text looks like a compile message AND matches the authoritative set — i.e. a
        // live copy of a real compile message, to be absorbed into the Compile representation. A compile-looking live
        // entry NOT in the authoritative set is deliberately excluded (kept as a normal log).
        // 一条 live（未分类）条目，文本像编译消息且匹配权威集——即真实编译消息的 live 副本，应被吸收进 Compile 表示。
        // “像编译消息但不在权威集”的 live 条目被有意排除（保留为普通日志）。
        private static bool IsAbsorbableLiveCompile(DebugxLogEntry e, HashSet<string> authTexts)
            => e != null
               && e.Category == LogEntryCategory.Uncategorized
               && EditorLogEntriesMirror.LooksLikeCompileMessage(e.PlainText)
               && authTexts.Contains(NormalizeCompileText(e.PlainText));

        // Normalized text key for matching the same compile message across the two sources (live condition vs LogEntries
        // message). They are the same underlying diagnostic string; trimming absorbs trailing whitespace/newline differences.
        // 归一化文本键，用于跨两个来源匹配同一条编译消息（live 的 condition 与 LogEntries 的 message）。二者是同一段诊断串；
        // Trim 吸收结尾空白/换行差异。
        private static string NormalizeCompileText(string s) => s == null ? string.Empty : s.Trim();

        // Called from ClearConsole. Compile errors don't clear themselves (they still exist in LogEntries until fixed), so
        // after a manual/play/build Clear we must re-mirror them. Clearing the key snapshot alone isn't enough: a plain
        // Clear fires no compilation event, so reconciliation would stay dormant. Open a rescan window so the next ticks
        // re-read and re-inject the still-present compile messages.
        // 由 ClearConsole 调用。编译错误不会自行消失（修复前仍存在于 LogEntries），故 手动/进Play/构建 Clear 之后必须重新镜像它们。
        // 仅清空键快照并不够：单纯的 Clear 不触发任何编译事件，对账会一直休眠。故打开一个重扫窗口，让随后几帧重新读取并重新注入仍存在的编译消息。
        private void ResetCompileMirrorTracking()
        {
            _mirroredCompileKeys.Clear();
            RequestCompileRescan();
        }
    }
}
