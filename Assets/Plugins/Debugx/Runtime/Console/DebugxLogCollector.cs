using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DebugxLog.Console
{
    /// <summary>
    /// The single data entry point of the Debugx Console. Captures logs from two channels and produces unified
    /// <see cref="DebugxLogEntry"/> objects, de-duplicating Debugx logs that arrive on both channels:
    ///  - Channel A: <see cref="Debugx.OnRawLog"/> — Debugx logs with full member metadata, fired synchronously on the
    ///    producing thread immediately before unityLogger.Log.
    ///  - Channel B: <see cref="Application.logMessageReceivedThreaded"/> — ALL Unity logs (engine / third-party / raw
    ///    Debug.Log, and also every Debugx log after unityLogger.Log), on the producing thread, carrying the stack trace.
    /// A single Debugx log triggers A then B on the same thread with nothing in between, so pairing is done with a
    /// per-thread FIFO: A enqueues its metadata; the immediately-following tagged B dequeues it and attaches B's stack
    /// trace. Non-tagged B becomes an "Uncategorized" entry. Capture callbacks may run on background threads, so they
    /// only do the light pairing and push finished entries into a thread-safe queue; <see cref="Pump"/> drains that
    /// queue on the main thread and raises <see cref="EntryProduced"/>.
    /// Debugx Console 的唯一数据入口。从两条通道采集日志并产出统一的 <see cref="DebugxLogEntry"/>，对同时经两条通道到达的
    /// Debugx 日志去重：
    ///  - 通道 A：<see cref="Debugx.OnRawLog"/> —— 带完整成员元数据的 Debugx 日志，在产生线程上、紧邻 unityLogger.Log 之前同步触发。
    ///  - 通道 B：<see cref="Application.logMessageReceivedThreaded"/> —— 全部 Unity 日志（引擎/第三方/裸 Debug.Log，以及每条
    ///    Debugx 日志在 unityLogger.Log 之后），在产生线程上回调，携带堆栈。
    /// 一条 Debugx 日志会在同一线程上先触发 A 再触发 B、中间无其它日志，故用线程内 FIFO 配对：A 入队其元数据；紧随其后的带标签 B
    /// 出队并附上 B 的堆栈。未带标签的 B 归为"未分类"。采集回调可能在后台线程，故只做轻量配对并把成品条目压入线程安全队列；
    /// <see cref="Pump"/> 在主线程排空该队列并触发 <see cref="EntryProduced"/>。
    /// </summary>
    public sealed class DebugxLogCollector
    {
        /// <summary>
        /// Raised on the main thread (during <see cref="Pump"/>) for each produced entry.
        /// 每产出一条条目时在主线程（<see cref="Pump"/> 内）触发。
        /// </summary>
        public event Action<DebugxLogEntry> EntryProduced;

        /// <summary>
        /// Optional filter for non-Debugx ("Uncategorized") live-channel logs. When set and it returns true for a given
        /// (condition, type), that log is dropped from the live channel. Used by the Editor Console to suppress compile
        /// messages that its LogEntries mirror provides authoritatively (some compile errors DO arrive on the live
        /// channel, so without this they would be duplicated). Never affects Debugx logs. May run on background threads,
        /// so it must be a pure, thread-safe function.
        /// 非 Debugx（“未分类”）live 通道日志的可选过滤器。设置且对某 (condition, type) 返回 true 时，该日志从 live 通道丢弃。
        /// 供 Editor Console 抑制其 LogEntries 镜像已权威提供的编译消息（部分编译错误确实会经 live 通道到达，无此则会重复）。
        /// 绝不影响 Debugx 日志。可能在后台线程运行，故必须是纯函数、线程安全。
        /// </summary>
        public Func<string, LogType, bool> LiveUncategorizedFilter { get; set; }

        // Per-thread pending Debugx metadata (channel A waiting for its channel B). ThreadLocal so it is per-instance
        // AND per-thread, which lets multiple collectors (e.g. an Editor Console and a runtime Console) coexist.
        // 每线程待配对的 Debugx 元数据（通道 A 等待其通道 B）。用 ThreadLocal 做到既按实例又按线程隔离，
        // 从而允许多个采集器（如 Editor 版与运行时版 Console）并存。
        private readonly ThreadLocal<Queue<DebugxRawLog>> _pending =
            new ThreadLocal<Queue<DebugxRawLog>>(() => new Queue<DebugxRawLog>());

        // Cross-thread hand-off of finished entries to the main thread.
        // 成品条目到主线程的跨线程交接队列。
        private readonly ConcurrentQueue<DebugxLogEntry> _incoming = new ConcurrentQueue<DebugxLogEntry>();

        private long _sequence;
        private int _incomingCount;   // approximate size of _incoming, for backpressure. 近似队列长度，用于背压。
        private long _droppedByBackpressure;
        private int _cachedFrame;     // last main-thread Time.frameCount, used for best-effort frame stamping.
        private bool _started;

        /// <summary>
        /// Max number of unconsumed entries buffered between pumps. Beyond this, new entries are dropped (counted in
        /// <see cref="DroppedByBackpressure"/>) so a background log flood cannot grow the queue without bound.
        /// 两次 pump 之间未消费条目的上限。超过后丢弃新条目（计入 <see cref="DroppedByBackpressure"/>），
        /// 避免后台日志洪泛导致队列无界增长。
        /// </summary>
        public int MaxPendingEntries { get; set; } = 20000;

        // Cap on the per-thread pending metadata queue. Normally 0 or 1; a larger cap only guards a pathological
        // de-sync (e.g. unityLogger disabled so channel B never arrives), dropping the oldest stale metadata.
        // 每线程待配对元数据队列的上限。正常为 0 或 1；较大的上限仅用于防范异常失步（如 unityLogger 被禁用导致通道 B 不到），
        // 会丢弃最旧的陈旧元数据。
        private const int PendingCap = 16;

        /// <summary>Total entries dropped due to backpressure. 因背压丢弃的条目总数。</summary>
        public long DroppedByBackpressure => Interlocked.Read(ref _droppedByBackpressure);

        /// <summary>Whether the collector is currently subscribed to the log channels. 采集器当前是否已订阅日志通道。</summary>
        public bool IsRunning => _started;

        /// <summary>
        /// Subscribe to both channels. Idempotent.
        /// 订阅两条通道。可重复调用（幂等）。
        /// </summary>
        public void Start()
        {
            if (_started) return;
            _started = true;
            Debugx.OnRawLog += OnRawLog;
            Application.logMessageReceivedThreaded += OnUnityLog;
        }

        /// <summary>
        /// Unsubscribe from both channels. Idempotent.
        /// 退订两条通道。可重复调用（幂等）。
        /// </summary>
        public void Stop()
        {
            if (!_started) return;
            _started = false;
            Debugx.OnRawLog -= OnRawLog;
            Application.logMessageReceivedThreaded -= OnUnityLog;
        }

        /// <summary>
        /// Drain produced entries on the main thread and raise <see cref="EntryProduced"/> for each. Call once per tick
        /// (Editor: EditorApplication.update; runtime: MonoBehaviour.Update).
        /// 在主线程排空成品条目并逐条触发 <see cref="EntryProduced"/>。每帧调用一次（Editor：EditorApplication.update；运行时：MonoBehaviour.Update）。
        /// </summary>
        public void Pump()
        {
            _cachedFrame = Time.frameCount; // main-thread read; reused for best-effort frame stamping of new captures.

            // Bounded drain so a producer flood during Pump cannot spin here forever.
            // 有界排空，避免 Pump 期间的生产者洪泛在此死循环。
            int budget = MaxPendingEntries + 1;
            while (budget-- > 0 && _incoming.TryDequeue(out DebugxLogEntry entry))
            {
                Interlocked.Decrement(ref _incomingCount);
                var handler = EntryProduced;
                if (handler != null)
                {
                    try { handler.Invoke(entry); }
                    catch (Exception ex) { Debug.LogError("[Debugx] Console EntryProduced handler threw: " + ex); }
                }
            }
        }

        /// <summary>
        /// Inject an externally-sourced, non-Debugx entry (e.g. compiler / asset-import messages mirrored from the
        /// editor console, which never arrive via the log channels) into the same pipeline, so it is buffered,
        /// filtered, collapsed and displayed like any other entry. Delivered on the next <see cref="Pump"/>. Intended
        /// for main-thread editor use but thread-safe via the same backpressure-bounded queue.
        /// 注入一条外部来源、非 Debugx 的条目（如从编辑器控制台镜像来的编译器/资源导入消息，它们从不经日志通道到达）进入
        /// 同一管线，使其与其它条目一样被缓冲/过滤/折叠/显示。于下次 <see cref="Pump"/> 交付。面向主线程的编辑器用途，
        /// 但经同一条背压受限队列而线程安全。
        /// </summary>
        public void InjectExternal(LogType type, string message, string stackTrace,
            LogEntryCategory category = LogEntryCategory.Uncategorized, int memberKey = DebugxLogEntry.UncategorizedKey)
        {
            string msg = message ?? string.Empty;
            string plain = ConsoleTextUtil.StripRichText(msg);
            var entry = new DebugxLogEntry(
                type, msg, plain, stackTrace ?? string.Empty,
                memberKey, null, null, null, false,
                NetTag.None, false, category,
                DateTime.Now, _cachedFrame, NextSequence());
            EnqueueIncoming(entry);
        }

        // Channel A: Debugx structured event. Just stash the metadata for the immediately-following channel B.
        // 通道 A：Debugx 结构化事件。仅暂存元数据，交给紧随其后的通道 B。
        private void OnRawLog(DebugxRawLog raw)
        {
            try
            {
                Queue<DebugxRawLog> q = _pending.Value;
                q.Enqueue(raw);
                // Guard against a pathological de-sync where channel B never arrives for some A.
                // 防范某些 A 的通道 B 始终不到达的异常失步。
                while (q.Count > PendingCap) q.Dequeue();
            }
            catch (Exception ex) { Debug.LogError("[Debugx] Console OnRawLog capture threw: " + ex); }
        }

        // Channel B: every Unity log, with stack trace, on the producing thread.
        // 通道 B：全部 Unity 日志，带堆栈，在产生线程上回调。
        private void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            try
            {
                Queue<DebugxRawLog> q = _pending.Value;
                DebugxLogEntry entry;

                if (q.Count > 0 && Debugx.IsDebugxTagged(condition))
                {
                    // Paired: Debugx log. Metadata from channel A, stack trace from channel B.
                    // 已配对：Debugx 日志。元数据取自通道 A，堆栈取自通道 B。
                    entry = BuildDebugxEntry(q.Dequeue(), stackTrace);
                }
                else
                {
                    // Drop live-channel logs an external mirror provides authoritatively (e.g. compile messages the
                    // editor mirrors from LogEntries), so they are not duplicated. Only for non-Debugx logs.
                    // 丢弃由外部镜像权威提供的 live 通道日志（如编辑器从 LogEntries 镜像的编译消息），避免重复。仅限非 Debugx 日志。
                    Func<string, LogType, bool> filter = LiveUncategorizedFilter;
                    if (filter != null && filter(condition, type))
                        return;

                    // Non-Debugx (or a false-positive "[Debugx]" text with no pending metadata) -> Uncategorized.
                    // 非 Debugx（或无待配对元数据、文本恰含 "[Debugx]" 的误判）-> 未分类。
                    entry = BuildUncategorizedEntry(condition, stackTrace, type);
                }

                EnqueueIncoming(entry);
            }
            catch (Exception ex) { Debug.LogError("[Debugx] Console OnUnityLog capture threw: " + ex); }
        }

        private DebugxLogEntry BuildDebugxEntry(DebugxRawLog raw, string stackTrace)
        {
            LogEntryCategory category;
            switch (raw.Category)
            {
                case DebugxLogCategory.Admin: category = LogEntryCategory.Admin; break;
                case DebugxLogCategory.Unregistered: category = LogEntryCategory.Unregistered; break;
                default: category = LogEntryCategory.Member; break;
            }

            NetTag netTag = !raw.NetTagShown ? NetTag.None : (raw.IsServer ? NetTag.Server : NetTag.Client);
            string plain = raw.RawMessage != null ? raw.RawMessage.ToString() : string.Empty;

            return new DebugxLogEntry(
                raw.LogType, raw.FinalText, plain, stackTrace,
                raw.Key, raw.Signature, raw.ColorHex, raw.Header, raw.LogSignatureShown,
                netTag, true, category,
                raw.Timestamp, _cachedFrame, NextSequence());
        }

        private DebugxLogEntry BuildUncategorizedEntry(string condition, string stackTrace, LogType type)
        {
            string plain = ConsoleTextUtil.StripRichText(condition);
            return new DebugxLogEntry(
                type, condition, plain, stackTrace,
                DebugxLogEntry.UncategorizedKey, null, null, null, false,
                NetTag.None, false, LogEntryCategory.Uncategorized,
                DateTime.Now, _cachedFrame, NextSequence());
        }

        private void EnqueueIncoming(DebugxLogEntry entry)
        {
            if (_incomingCount >= MaxPendingEntries)
            {
                Interlocked.Increment(ref _droppedByBackpressure);
                return;
            }
            _incoming.Enqueue(entry);
            Interlocked.Increment(ref _incomingCount);
        }

        private long NextSequence()
        {
            return Interlocked.Increment(ref _sequence);
        }
    }
}
