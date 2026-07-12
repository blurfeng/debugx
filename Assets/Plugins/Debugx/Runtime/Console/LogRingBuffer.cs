using System;

namespace DebugxLog.Console
{
    /// <summary>
    /// Append log buffer of <see cref="DebugxLogEntry"/> backed by a ring. The allocated storage grows on demand
    /// (doubling) up to <see cref="Capacity"/>; only once that retention cap is reached is the oldest entry dropped
    /// (FIFO). A very large cap (e.g. int.MaxValue for the Editor Console) therefore costs nothing until entries are
    /// actually added — an idle console does not pre-allocate. Not thread-safe by design: it is written and read only on
    /// the main thread (the collector marshals cross-thread captures to the main thread before adding). Callers poll
    /// <see cref="Version"/> to detect changes cheaply instead of subscribing to a per-add event (avoids event spam
    /// during log floods).
    /// 基于环形存储的追加式 <see cref="DebugxLogEntry"/> 缓冲。已分配存储按需翻倍增长，直到达到 <see cref="Capacity"/> 保留
    /// 上限；唯有到达上限后才丢弃最旧条目（FIFO）。因此设很大的上限（如编辑器 Console 用 int.MaxValue）在真正 Add 前不占内存，
    /// 空闲 Console 不预分配。设计上非线程安全：只在主线程读写（采集器会先把跨线程捕获汇集到主线程再 Add）。调用方通过轮询
    /// <see cref="Version"/> 低成本地检测变化，而非订阅每条日志的事件（避免日志洪泛时的事件风暴）。
    /// </summary>
    public sealed class LogRingBuffer
    {
        /// <summary>
        /// Default (retention) capacity for the Editor Console: effectively unlimited. Storage still grows on demand, so
        /// this costs nothing until logs accumulate; entries are only ever dropped if memory is actually exhausted.
        /// 编辑器 Console 的默认（保留）容量：实际上无上限。存储仍按需增长，故在日志累积前不占内存；只有真正耗尽内存时才会丢条目。
        /// </summary>
        public const int DefaultCapacity = int.MaxValue;

        // Smallest storage allocated on the first Add, then doubled as needed up to _maxCapacity.
        // 首次 Add 分配的最小存储，随后按需翻倍直到 _maxCapacity。
        private const int InitialCapacity = 64;

        private DebugxLogEntry[] _items;
        private int _start;         // index of the oldest entry within _items. _items 中最旧条目的索引。
        private int _count;
        private int _maxCapacity;   // retention cap; _items.Length grows lazily up to this. 保留上限；_items.Length 按需增长到此值。

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
            _items = Array.Empty<DebugxLogEntry>();
            _start = 0;
            _count = 0;
            _maxCapacity = 0;
            Capacity = capacity;
        }

        /// <summary>
        /// Retention capacity (the maximum number of entries kept). Growing it is free — storage grows lazily on Add;
        /// shrinking it below the current count drops the oldest overflow entries and reclaims storage.
        /// 保留容量（最多保留的条目数）。放大是免费的——存储在 Add 时按需增长；缩小到当前条目数以下会丢弃最旧的溢出条目并回收存储。
        /// </summary>
        public int Capacity
        {
            get => _maxCapacity;
            set
            {
                int newCap = value < 1 ? 1 : value;
                if (newCap == _maxCapacity) return;

                // Only reallocate when the new cap is smaller than the currently allocated storage: shrink it (evicting
                // the oldest overflow if we drop below the current count). Growing the cap needs no work here — the
                // storage grows lazily in Add up to the new cap.
                // 仅当新上限小于当前已分配存储时才重新分配：收缩存储（若降到当前条目数以下则淘汰最旧的溢出条目）。放大上限
                // 此处无需操作——存储会在 Add 中按需增长到新上限。
                if (newCap < _items.Length)
                {
                    int keep = _count < newCap ? _count : newCap;
                    int dropped = _count - keep;
                    var shrunk = new DebugxLogEntry[newCap];
                    for (int i = 0; i < keep; i++)
                        shrunk[i] = _items[(_start + dropped + i) % _items.Length];
                    if (dropped > 0) DroppedCount += dropped;
                    _items = shrunk;
                    _start = 0;
                    _count = keep;
                }

                _maxCapacity = newCap;
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
                return _items[(_start + index) % _items.Length];
            }
        }

        /// <summary>
        /// Append an entry. Grows the storage on demand up to <see cref="Capacity"/>; once at the cap, the oldest entry
        /// is evicted first (FIFO).
        /// 追加一条。按需将存储增长到 <see cref="Capacity"/>；到达上限后先淘汰最旧的一条（FIFO）。
        /// </summary>
        public void Add(DebugxLogEntry entry)
        {
            if (entry == null) return;

            // Storage full but still below the retention cap → grow (double), clamped to the cap.
            // 存储已满但未达保留上限 → 增长（翻倍），并夹到上限。
            if (_count == _items.Length && _items.Length < _maxCapacity)
            {
                long doubled = _items.Length == 0 ? InitialCapacity : (long)_items.Length * 2;
                Reallocate((int)Math.Min(doubled, _maxCapacity));
            }

            if (_count < _items.Length)
            {
                _items[(_start + _count) % _items.Length] = entry;
                _count++;
            }
            else
            {
                // At the retention cap: overwrite the oldest slot and advance the start pointer.
                // 达到保留上限：覆盖最旧槽位并前移起始指针。
                _items[_start] = entry;
                _start = (_start + 1) % _items.Length;
                DroppedCount++;
            }

            Version++;
        }

        // Re-linearize the current entries into a freshly-sized array (oldest at index 0).
        // 将当前条目重新线性化到一个新尺寸数组（最旧位于索引 0）。
        private void Reallocate(int newLength)
        {
            var newItems = new DebugxLogEntry[newLength];
            for (int i = 0; i < _count; i++)
                newItems[i] = _items[(_start + i) % _items.Length];
            _items = newItems;
            _start = 0;
        }

        /// <summary>
        /// Remove every entry matching <paramref name="predicate"/>, compacting the survivors in place while preserving
        /// their order. Returns the number removed and bumps <see cref="Version"/> only when at least one was removed.
        /// Unlike a capacity eviction this is an explicit removal, so it does NOT touch <see cref="DroppedCount"/>.
        /// Main thread only. Used e.g. to drop stale compile-log entries before re-mirroring the current batch.
        /// 移除所有满足 <paramref name="predicate"/> 的条目，其余就地压紧且保持顺序。返回移除数量；仅当至少移除一条时自增
        /// <see cref="Version"/>。区别于容量淘汰，这是显式移除，故不影响 <see cref="DroppedCount"/>。仅主线程。
        /// 例如用于在重新镜像当前批之前，移除滞留的旧编译日志条目。
        /// </summary>
        public int RemoveWhere(Predicate<DebugxLogEntry> predicate)
        {
            if (predicate == null || _count == 0) return 0;

            // Stable in-place compaction over the ring: read every slot oldest->newest, copy kept entries down into the
            // next write position (write <= read always, so we never clobber an unread entry).
            // 环上的稳定就地压紧：自旧向新读每个槽，把保留的条目下移到下一个写位（write <= read 恒成立，绝不覆盖未读条目）。
            int write = 0;
            for (int read = 0; read < _count; read++)
            {
                DebugxLogEntry e = _items[(_start + read) % _items.Length];
                if (predicate(e)) continue; // remove. 移除。
                if (write != read)
                    _items[(_start + write) % _items.Length] = e;
                write++;
            }

            int removed = _count - write;
            if (removed == 0) return 0;

            // Null the freed tail slots so we don't pin references. 清空腾出的尾部槽，避免滞留引用。
            for (int i = write; i < _count; i++)
                _items[(_start + i) % _items.Length] = null;

            _count = write;
            Version++;
            return removed;
        }

        /// <summary>
        /// Remove all entries (does not reset <see cref="DroppedCount"/>).
        /// 清空所有条目（不重置 <see cref="DroppedCount"/>）。
        /// </summary>
        public void Clear()
        {
            if (_count > 0)
                Array.Clear(_items, 0, _items.Length);
            _start = 0;
            _count = 0;
            Version++;
        }
    }
}
