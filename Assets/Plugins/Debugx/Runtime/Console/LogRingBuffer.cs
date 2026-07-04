using System;

namespace DebugxLog.Console
{
    /// <summary>
    /// Fixed-capacity ring buffer of <see cref="DebugxLogEntry"/>. When full, the oldest entry is dropped (FIFO).
    /// This is the proper replacement for the old screen-draw buffer (which kept only 100 items of Message+LogType).
    /// Not thread-safe by design: it is written and read only on the main thread (the collector marshals cross-thread
    /// captures to the main thread before adding). Callers poll <see cref="Version"/> to detect changes cheaply
    /// instead of subscribing to a per-add event (avoids event spam during log floods).
    /// 定容的 <see cref="DebugxLogEntry"/> 环形缓冲。满时丢弃最旧条目（FIFO）。它是旧屏幕绘制缓冲（仅存 100 条
    /// Message+LogType）的正式替代品。设计上非线程安全：只在主线程读写（采集器会先把跨线程捕获汇集到主线程再 Add）。
    /// 调用方通过轮询 <see cref="Version"/> 低成本地检测变化，而非订阅每条日志的事件（避免日志洪泛时的事件风暴）。
    /// </summary>
    public sealed class LogRingBuffer
    {
        /// <summary>Default capacity for the Editor Console. 编辑器 Console 的默认容量。</summary>
        public const int DefaultCapacity = 5000;

        private DebugxLogEntry[] _items;
        private int _start;   // index of the oldest entry. 最旧条目的索引。
        private int _count;
        private int _capacity;

        /// <summary>
        /// Monotonic change counter. Incremented on every Add / Clear / capacity change. Display layers compare it
        /// against their last-seen value to decide whether to refresh.
        /// 单调变化计数。每次 Add / Clear / 容量变更都会自增。显示层用它与上次记录的值比较，决定是否刷新。
        /// </summary>
        public int Version { get; private set; }

        /// <summary>Total number of entries dropped due to capacity (oldest evicted). 因容量丢弃（淘汰最旧）的条目总数。</summary>
        public long DroppedCount { get; private set; }

        /// <summary>Current number of entries held. 当前持有的条目数。</summary>
        public int Count => _count;

        public LogRingBuffer(int capacity)
        {
            _capacity = 0;
            _items = Array.Empty<DebugxLogEntry>();
            Capacity = capacity;
        }

        /// <summary>
        /// Buffer capacity. Shrinking drops the oldest overflow entries; growing preserves all current entries.
        /// 缓冲容量。缩小会丢弃最旧的溢出条目；扩大会保留全部现有条目。
        /// </summary>
        public int Capacity
        {
            get => _capacity;
            set
            {
                int newCap = value < 1 ? 1 : value;
                if (newCap == _capacity) return;

                var newItems = new DebugxLogEntry[newCap];
                int keep = _count < newCap ? _count : newCap;
                int dropped = _count - keep; // when shrinking below current count, evict this many oldest

                for (int i = 0; i < keep; i++)
                    newItems[i] = _items[(_start + dropped + i) % _capacity];

                if (dropped > 0) DroppedCount += dropped;

                _items = newItems;
                _start = 0;
                _count = keep;
                _capacity = newCap;
                Version++;
            }
        }

        /// <summary>
        /// Entry at the given position, 0 = oldest, Count-1 = newest.
        /// 指定位置的条目，0 = 最旧，Count-1 = 最新。
        /// </summary>
        public DebugxLogEntry this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _items[(_start + index) % _capacity];
            }
        }

        /// <summary>
        /// Append an entry. If the buffer is full, the oldest entry is evicted first.
        /// 追加一条。若缓冲已满，先淘汰最旧的一条。
        /// </summary>
        public void Add(DebugxLogEntry entry)
        {
            if (entry == null) return;

            if (_count < _capacity)
            {
                _items[(_start + _count) % _capacity] = entry;
                _count++;
            }
            else
            {
                // Full: overwrite the oldest slot and advance the start pointer.
                // 已满：覆盖最旧槽位并前移起始指针。
                _items[_start] = entry;
                _start = (_start + 1) % _capacity;
                DroppedCount++;
            }

            Version++;
        }

        /// <summary>
        /// Remove all entries (does not reset <see cref="DroppedCount"/>).
        /// 清空所有条目（不重置 <see cref="DroppedCount"/>）。
        /// </summary>
        public void Clear()
        {
            if (_count > 0)
                Array.Clear(_items, 0, _capacity);
            _start = 0;
            _count = 0;
            Version++;
        }
    }
}
