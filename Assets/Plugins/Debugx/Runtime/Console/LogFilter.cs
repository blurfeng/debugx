using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DebugxLog.Console
{
    /// <summary>
    /// Applies a <see cref="LogFilterCriteria"/> to entries. Holds the criteria plus a compiled search regex (recompiled
    /// only when the query changes), and exposes <see cref="IsVisible"/> for the display to test each buffer entry.
    /// Pure logic, no UnityEditor dependency — shared by the Editor and runtime Consoles.
    /// 将 <see cref="LogFilterCriteria"/> 应用于条目。持有条件与一份编译后的搜索正则（仅在查询变化时重编译），
    /// 并暴露 <see cref="IsVisible"/> 供显示层逐条测试缓冲条目。纯逻辑、无 UnityEditor 依赖——Editor 版与运行时版共用。
    /// </summary>
    public sealed class LogFilter
    {
        private LogFilterCriteria _criteria = new LogFilterCriteria();

        // Cached search state so we don't recompile/reallocate per entry.
        // 缓存的搜索状态，避免逐条重编译/分配。
        private string _searchText;
        private bool _searchUseRegex;
        private bool _searchCaseSensitive;
        private bool _searchStack;
        private Regex _searchRegex;
        private bool _searchEmpty = true;

        /// <summary>The active criteria. 当前生效的条件。</summary>
        public LogFilterCriteria Criteria => _criteria;

        /// <summary>True when the search text is a regex that failed to compile (matching falls back to substring). 搜索文本为无法编译的正则时为 true（匹配回退为子串）。</summary>
        public bool SearchRegexError { get; private set; }

        /// <summary>
        /// Set the active criteria and refresh the cached search state.
        /// 设置生效条件并刷新缓存的搜索状态。
        /// </summary>
        public void SetCriteria(LogFilterCriteria criteria)
        {
            _criteria = criteria ?? new LogFilterCriteria();
            RebuildSearch(_criteria.Search);
        }

        private void RebuildSearch(SearchQuery q)
        {
            _searchText = q.Text;
            _searchUseRegex = q.UseRegex;
            _searchCaseSensitive = q.CaseSensitive;
            _searchStack = q.SearchStackTrace;
            _searchEmpty = q.IsEmpty;
            _searchRegex = null;
            SearchRegexError = false;

            if (!_searchEmpty && _searchUseRegex)
            {
                try
                {
                    var options = _searchCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    _searchRegex = new Regex(_searchText, options);
                }
                catch (ArgumentException)
                {
                    // Invalid regex: remember the error and fall back to substring matching.
                    // 非法正则：记录错误并回退为子串匹配。
                    _searchRegex = null;
                    SearchRegexError = true;
                }
            }
        }

        /// <summary>
        /// Whether an entry passes all filter conditions.
        /// 一条条目是否通过全部过滤条件。
        /// </summary>
        public bool IsVisible(DebugxLogEntry e)
        {
            if (e == null) return false;

            switch (LogStatistics.SeverityOf(e.LogType))
            {
                case LogSeverity.Warning: if (!_criteria.ShowWarning) return false; break;
                case LogSeverity.Error: if (!_criteria.ShowError) return false; break;
                default: if (!_criteria.ShowLog) return false; break;
            }

            if (_criteria.OnlyDebugx && !e.IsDebugx) return false;

            // Member-key filter. Compile messages are editor tooling output, not a Debugx member, so they are exempt
            // (governed by the severity toggles / search instead) — otherwise unchecking "Uncategorized", whose sentinel
            // key they share, would also hide compiler errors. Admin (key 0) and Unregistered ARE first-class filter keys.
            // 成员 key 过滤。编译消息是编辑器工具输出、并非 Debugx 成员，故豁免（改由 严重级别/搜索 控制）——否则取消勾选“未分类”
            //（与其共用哨兵 key）会连带隐藏编译错误。Admin（key 0）与 未注册 则是一等过滤 key。
            HashSet<int> keys = _criteria.VisibleMemberKeys;
            if (keys != null && e.Category != LogEntryCategory.Compile && !keys.Contains(e.MemberKey)) return false;

            switch (_criteria.NetTagMode)
            {
                case NetTagFilterMode.Server: if (e.NetTag != NetTag.Server) return false; break;
                case NetTagFilterMode.Client: if (e.NetTag != NetTag.Client) return false; break;
            }

            if (!_searchEmpty && !MatchesSearch(e)) return false;

            return true;
        }

        private bool MatchesSearch(DebugxLogEntry e)
        {
            if (MatchesText(e.PlainText)) return true;
            if (!string.IsNullOrEmpty(e.MemberSignature) && MatchesText(e.MemberSignature)) return true;
            if (_searchStack && MatchesText(e.StackTrace)) return true;
            return false;
        }

        private bool MatchesText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            if (_searchRegex != null)
                return _searchRegex.IsMatch(text);

            StringComparison cmp = _searchCaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            return text.IndexOf(_searchText, cmp) >= 0;
        }
    }
}
