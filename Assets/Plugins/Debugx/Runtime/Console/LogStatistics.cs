using System.Collections.Generic;
using UnityEngine;

namespace DebugxLog.Console
{
    /// <summary>
    /// Severity bucket used by the three Console count/filter buttons. Unity's Assert and Exception both fold into
    /// <see cref="Error"/> (matching the native Console, which has no separate toggles for them).
    /// Console 三个计数/过滤按钮使用的严重级别桶。Unity 的 Assert 与 Exception 都并入 <see cref="Error"/>
    /// （对齐原生 Console —— 它没有为这两者单设开关）。
    /// </summary>
    public enum LogSeverity
    {
        /// <summary>LogType.Log. </summary>
        Log,
        /// <summary>LogType.Warning. </summary>
        Warning,
        /// <summary>LogType.Error / Assert / Exception. </summary>
        Error,
    }

    /// <summary>
    /// Per-severity and per-member counts over the current buffer contents. Recomputed from the buffer (O(n)) rather
    /// than kept incrementally, so eviction of the oldest entries never causes count drift; the store only recomputes
    /// when the buffer version changes. Feeds the toolbar count badges (Log/Warning/Error) and the member filter.
    /// 对当前缓冲内容的按级别与按成员计数。从缓冲重算（O(n)）而非增量维护，因此淘汰最旧条目不会导致计数漂移；
    /// store 仅在缓冲版本变化时重算。供工具栏计数徽标（Log/Warning/Error）与成员过滤使用。
    /// </summary>
    public sealed class LogStatistics
    {
        private readonly Dictionary<int, int> _memberCounts = new Dictionary<int, int>();

        /// <summary>Count of Log-severity entries. Log 级别条目数。</summary>
        public int LogCount { get; private set; }
        /// <summary>Count of Warning-severity entries. Warning 级别条目数。</summary>
        public int WarningCount { get; private set; }
        /// <summary>Count of Error-severity entries (Error + Assert + Exception). Error 级别条目数（Error + Assert + Exception）。</summary>
        public int ErrorCount { get; private set; }
        /// <summary>Total entry count. 条目总数。</summary>
        public int Total { get; private set; }

        /// <summary>Count per member key (key = <see cref="DebugxLogEntry.MemberKey"/>). 按成员 key 计数。</summary>
        public IReadOnlyDictionary<int, int> MemberCounts => _memberCounts;

        /// <summary>
        /// Map a <see cref="LogType"/> to its Console severity bucket.
        /// 将 <see cref="LogType"/> 映射到 Console 的严重级别桶。
        /// </summary>
        public static LogSeverity SeverityOf(LogType type)
        {
            switch (type)
            {
                case LogType.Warning: return LogSeverity.Warning;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception: return LogSeverity.Error;
                default: return LogSeverity.Log;
            }
        }

        /// <summary>
        /// Recompute all counts from the buffer's current contents.
        /// 从缓冲当前内容重算所有计数。
        /// </summary>
        public void Recompute(LogRingBuffer buffer)
        {
            LogCount = 0;
            WarningCount = 0;
            ErrorCount = 0;
            Total = 0;
            _memberCounts.Clear();

            if (buffer == null) return;

            int n = buffer.Count;
            for (int i = 0; i < n; i++)
            {
                DebugxLogEntry e = buffer[i];
                if (e == null) continue;

                Total++;
                switch (SeverityOf(e.LogType))
                {
                    case LogSeverity.Warning: WarningCount++; break;
                    case LogSeverity.Error: ErrorCount++; break;
                    default: LogCount++; break;
                }

                _memberCounts.TryGetValue(e.MemberKey, out int c);
                _memberCounts[e.MemberKey] = c + 1;
            }
        }

        /// <summary>Count for a given member key (0 if none). 指定成员 key 的计数（无则 0）。</summary>
        public int CountForMember(int memberKey)
        {
            return _memberCounts.TryGetValue(memberKey, out int c) ? c : 0;
        }
    }
}
