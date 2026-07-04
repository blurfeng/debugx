using System;
using System.Collections.Generic;

namespace DebugxLog.Console
{
    /// <summary>
    /// The central, display-agnostic model of the Debugx Console. Owns the collector, ring buffer, statistics, filter
    /// and collapser, and produces the final display rows. Both the Editor Console and the runtime Console drive the
    /// same store: call <see cref="Start"/> once, <see cref="Pump"/> every tick to ingest new logs, then
    /// <see cref="TryRebuildView"/> to refresh <see cref="Rows"/> only when something changed. Zero UnityEditor
    /// dependency — this is the reuse boundary between the two Console front-ends.
    /// Debugx Console 的中枢、与显示无关的模型。拥有采集器、环形缓冲、统计、过滤与折叠，并产出最终显示行。Editor 版与运行时版
    /// 驱动同一个 store：调用一次 <see cref="Start"/>，每帧 <see cref="Pump"/> 摄入新日志，再用 <see cref="TryRebuildView"/>
    /// 仅在有变化时刷新 <see cref="Rows"/>。零 UnityEditor 依赖——这是两个 Console 前端之间的复用边界。
    /// </summary>
    public sealed class DebugxLogStore
    {
        private readonly DebugxLogCollector _collector = new DebugxLogCollector();
        private readonly LogCollapser _collapser = new LogCollapser();
        private readonly List<DebugxLogEntry> _visibleScratch = new List<DebugxLogEntry>();
        private readonly List<CollapsedRow> _rows = new List<CollapsedRow>();

        private LogCollapser.Mode _collapseMode = LogCollapser.Mode.Off;
        private int _lastRebuiltBufferVersion = -1;
        private bool _viewDirty = true;

        /// <summary>The ring buffer of all captured entries. 全部已捕获条目的环形缓冲。</summary>
        public LogRingBuffer Buffer { get; }

        /// <summary>Per-severity / per-member counts over the buffer. 缓冲上的按级别/按成员计数。</summary>
        public LogStatistics Statistics { get; } = new LogStatistics();

        /// <summary>The active filter. Change its criteria via <see cref="SetFilterCriteria"/>. 当前过滤器。用 <see cref="SetFilterCriteria"/> 修改条件。</summary>
        public LogFilter Filter { get; } = new LogFilter();

        /// <summary>The current display rows (filtered then collapsed). 当前显示行（先过滤后折叠）。</summary>
        public IReadOnlyList<CollapsedRow> Rows => _rows;

        /// <summary>The underlying collector (for lifecycle / backpressure inspection). 底层采集器（用于生命周期/背压检查）。</summary>
        public DebugxLogCollector Collector => _collector;

        /// <summary>Whether the collector is subscribed to the log channels. 采集器是否已订阅日志通道。</summary>
        public bool IsRunning => _collector.IsRunning;

        /// <summary>Total entries dropped by backpressure (for a "N dropped" hint). 因背压丢弃的条目总数（用于“已丢弃 N 条”提示）。</summary>
        public long DroppedByBackpressure => _collector.DroppedByBackpressure;

        /// <summary>Buffer capacity. Changing it marks the view dirty. 缓冲容量。修改会标记视图为脏。</summary>
        public int Capacity
        {
            get => Buffer.Capacity;
            set { Buffer.Capacity = value; _viewDirty = true; }
        }

        /// <summary>Collapse mode. Changing it marks the view dirty. 折叠模式。修改会标记视图为脏。</summary>
        public LogCollapser.Mode CollapseMode
        {
            get => _collapseMode;
            set { if (_collapseMode != value) { _collapseMode = value; _viewDirty = true; } }
        }

        public DebugxLogStore(int capacity = LogRingBuffer.DefaultCapacity)
        {
            Buffer = new LogRingBuffer(capacity);
            _collector.EntryProduced += OnEntryProduced;
        }

        /// <summary>Subscribe to the log channels. 订阅日志通道。</summary>
        public void Start() => _collector.Start();

        /// <summary>Unsubscribe from the log channels. 退订日志通道。</summary>
        public void Stop() => _collector.Stop();

        /// <summary>
        /// Ingest captured logs on the main thread. Call once per tick before reading <see cref="Rows"/>.
        /// 在主线程摄入已捕获日志。每帧读取 <see cref="Rows"/> 前调用一次。
        /// </summary>
        public void Pump() => _collector.Pump();

        /// <summary>Clear all buffered entries (does not stop capture). 清空全部缓冲条目（不停止采集）。</summary>
        public void Clear() => Buffer.Clear();

        /// <summary>Set the active filter criteria and mark the view dirty. 设置过滤条件并标记视图为脏。</summary>
        public void SetFilterCriteria(LogFilterCriteria criteria)
        {
            Filter.SetCriteria(criteria);
            _viewDirty = true;
        }

        /// <summary>
        /// Explicitly mark the view dirty (e.g. after mutating the current criteria object in place).
        /// 显式标记视图为脏（例如就地修改了当前条件对象后）。
        /// </summary>
        public void MarkViewDirty() => _viewDirty = true;

        /// <summary>
        /// Rebuild <see cref="Rows"/> and <see cref="Statistics"/> only if the buffer changed or the view was marked
        /// dirty. Returns true if a rebuild happened. Cheap to call every tick.
        /// 仅当缓冲变化或视图被标脏时，重建 <see cref="Rows"/> 与 <see cref="Statistics"/>。发生重建返回 true。每帧调用开销很低。
        /// </summary>
        public bool TryRebuildView()
        {
            if (!_viewDirty && Buffer.Version == _lastRebuiltBufferVersion)
                return false;

            RebuildView();
            _lastRebuiltBufferVersion = Buffer.Version;
            _viewDirty = false;
            return true;
        }

        private void RebuildView()
        {
            _visibleScratch.Clear();
            int n = Buffer.Count;
            for (int i = 0; i < n; i++)
            {
                DebugxLogEntry e = Buffer[i];
                if (Filter.IsVisible(e)) _visibleScratch.Add(e);
            }

            _collapser.Build(_visibleScratch, _collapseMode, _rows);
            Statistics.Recompute(Buffer);
        }

        // Runs on the main thread during Pump.
        // 在 Pump 期间于主线程运行。
        private void OnEntryProduced(DebugxLogEntry entry)
        {
            Buffer.Add(entry);
        }
    }
}
