using System.Collections.Generic;
using UnityEngine;

namespace DebugxLog.Console
{
    /// <summary>
    /// One row of the display view: a representative entry plus how many entries it stands for.
    /// 显示视图的一行：一个代表条目，以及它代表了多少条。
    /// </summary>
    public struct CollapsedRow
    {
        /// <summary>The representative (first-seen) entry. 代表条目（首次出现的那条）。</summary>
        public DebugxLogEntry Entry;
        /// <summary>How many entries this row represents (1 when not collapsed). 本行代表的条目数（未折叠时为 1）。</summary>
        public int Count;

        public CollapsedRow(DebugxLogEntry entry, int count)
        {
            Entry = entry;
            Count = count;
        }
    }

    /// <summary>
    /// Turns an ordered list of (already filtered) entries into display rows, optionally collapsing duplicates globally
    /// (matching the native Console, whose Collapse is global rather than adjacent-only). The collapse count is carried
    /// on the <see cref="CollapsedRow"/>, never written back onto the shared immutable <see cref="DebugxLogEntry"/>.
    /// 将一个有序的（已过滤的）条目列表转换为显示行，可选地全局折叠重复项（对齐原生 Console —— 其 Collapse 是全局而非仅相邻）。
    /// 折叠计数承载在 <see cref="CollapsedRow"/> 上，绝不写回被共享的、不可变的 <see cref="DebugxLogEntry"/>。
    /// </summary>
    public sealed class LogCollapser
    {
        /// <summary>Collapse mode. 折叠模式。</summary>
        public enum Mode
        {
            /// <summary>No collapsing; every entry is its own row. 不折叠；每条各成一行。</summary>
            Off,
            /// <summary>Group by (LogType, message) — native-like. 按 (LogType, 消息) 归并 —— 类原生。</summary>
            ByMessage,
            /// <summary>Group by (LogType, memberKey, message) — Debugx-aware. 按 (LogType, 成员 key, 消息) 归并 —— Debugx 感知。</summary>
            ByMemberAndMessage,
        }

        // Reused across builds to avoid per-refresh allocation of the grouping map.
        // 跨多次构建复用，避免每次刷新分配分组映射表。
        private readonly Dictionary<(LogType, int, string), int> _index =
            new Dictionary<(LogType, int, string), int>();

        /// <summary>
        /// Build display rows from the visible entries into <paramref name="output"/> (cleared first).
        /// 从可见条目构建显示行到 <paramref name="output"/>（先清空）。
        /// </summary>
        public void Build(IReadOnlyList<DebugxLogEntry> visible, Mode mode, List<CollapsedRow> output)
        {
            output.Clear();
            if (visible == null || visible.Count == 0) return;

            if (mode == Mode.Off)
            {
                for (int i = 0; i < visible.Count; i++)
                {
                    DebugxLogEntry e = visible[i];
                    if (e != null) output.Add(new CollapsedRow(e, 1));
                }
                return;
            }

            _index.Clear();
            for (int i = 0; i < visible.Count; i++)
            {
                DebugxLogEntry e = visible[i];
                if (e == null) continue;

                var key = GroupKey(e, mode);

                if (_index.TryGetValue(key, out int rowIndex))
                {
                    CollapsedRow row = output[rowIndex];
                    row.Count++;
                    output[rowIndex] = row;
                }
                else
                {
                    _index[key] = output.Count;
                    output.Add(new CollapsedRow(e, 1));
                }
            }
        }

        private static (LogType, int, string) GroupKey(DebugxLogEntry e, Mode mode)
            => (e.LogType, mode == Mode.ByMemberAndMessage ? e.MemberKey : 0, e.PlainText ?? string.Empty);

        /// <summary>
        /// Identity of the row that shows <paramref name="e"/> under <paramref name="mode"/>: the entry itself when not
        /// collapsing, otherwise its group. Lets a UI keep its selection across rebuilds — a collapsed row's representative
        /// (its oldest entry) changes whenever that entry is evicted from the buffer.
        /// 在 <paramref name="mode"/> 下显示 <paramref name="e"/> 的那一行的标识：不折叠时就是条目本身，否则是它所属的组。
        /// 使 UI 能跨重建保持选中——折叠行的代表条目（组内最旧的条目）被挤出缓冲时就会改变。
        /// </summary>
        public static object RowIdentity(DebugxLogEntry e, Mode mode)
            => mode == Mode.Off ? (object)e.SequenceId : GroupKey(e, mode);
    }
}
