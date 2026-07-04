using System.Collections.Generic;

namespace DebugxLog.Console
{
    /// <summary>
    /// A search query: the text plus its matching options. Plain data; the compiled regex lives in <see cref="LogFilter"/>.
    /// 一次搜索查询：文本加匹配选项。纯数据；编译后的正则位于 <see cref="LogFilter"/>。
    /// </summary>
    public struct SearchQuery
    {
        /// <summary>Search text. 搜索文本。</summary>
        public string Text;
        /// <summary>Treat <see cref="Text"/> as a regular expression. 将 <see cref="Text"/> 视为正则表达式。</summary>
        public bool UseRegex;
        /// <summary>Case-sensitive matching. 区分大小写。</summary>
        public bool CaseSensitive;
        /// <summary>Also match against the stack trace, not just the message. 除消息外也匹配堆栈。</summary>
        public bool SearchStackTrace;

        /// <summary>Whether the query has no text (matches everything). 查询是否无文本（匹配全部）。</summary>
        public bool IsEmpty => string.IsNullOrEmpty(Text);
    }

    /// <summary>
    /// The full set of Console filter conditions. A plain, UI-agnostic POCO: the Editor Console and the runtime Console
    /// both just fill it from their controls, then hand it to a <see cref="LogFilter"/>. All conditions combine with AND.
    /// Console 全部过滤条件的集合。一个与 UI 无关的纯 POCO：Editor 版与运行时版 Console 都只是用各自控件填充它，
    /// 再交给 <see cref="LogFilter"/>。所有条件按 AND 组合。
    /// </summary>
    public sealed class LogFilterCriteria
    {
        /// <summary>Show Log-severity entries. 显示 Log 级别条目。</summary>
        public bool ShowLog = true;
        /// <summary>Show Warning-severity entries. 显示 Warning 级别条目。</summary>
        public bool ShowWarning = true;
        /// <summary>Show Error-severity entries (Error/Assert/Exception). 显示 Error 级别条目（Error/Assert/Exception）。</summary>
        public bool ShowError = true;

        /// <summary>Only show Debugx logs (hide non-Debugx "Uncategorized"). 仅显示 Debugx 日志（隐藏非 Debugx 的“未分类”）。</summary>
        public bool OnlyDebugx = false;

        /// <summary>Show the Admin (LogAdm) channel. 显示 Admin（LogAdm）通道。</summary>
        public bool ShowAdmin = true;

        /// <summary>
        /// Visible member keys. Null or empty means "all members visible"; otherwise only entries whose
        /// <see cref="DebugxLogEntry.MemberKey"/> is in this set are shown (include
        /// <see cref="DebugxLogEntry.UncategorizedKey"/> to keep non-Debugx logs visible).
        /// 可见成员 key。Null 或空表示“全部成员可见”；否则仅显示 <see cref="DebugxLogEntry.MemberKey"/> 在此集合中的条目
        /// （加入 <see cref="DebugxLogEntry.UncategorizedKey"/> 可保留非 Debugx 日志可见）。
        /// </summary>
        public HashSet<int> VisibleMemberKeys;

        /// <summary>The search query. 搜索查询。</summary>
        public SearchQuery Search;
    }
}
