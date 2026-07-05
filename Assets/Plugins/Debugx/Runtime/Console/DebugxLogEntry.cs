using System;
using UnityEngine;

namespace DebugxLog.Console
{
    /// <summary>
    /// Source category of a captured log entry, as seen by the Console.
    /// Extends the DLL's <see cref="DebugxLogCategory"/> with <see cref="Uncategorized"/> for non-Debugx logs.
    /// Console 视角下一条日志的来源分类。在 DLL 的 <see cref="DebugxLogCategory"/> 基础上，为非 Debugx 日志增加 <see cref="Uncategorized"/>。
    /// </summary>
    public enum LogEntryCategory
    {
        /// <summary>Normal member (custom, or preset Normal/Master). 普通成员（自定义，或预设 Normal/Master）。</summary>
        Member,
        /// <summary>Admin channel (LogAdm, key 0). 管理通道（LogAdm，key 0）。</summary>
        Admin,
        /// <summary>Unregistered member. 未注册成员。</summary>
        Unregistered,
        /// <summary>Non-Debugx log (engine / third-party / raw Debug.Log). 非 Debugx 日志（引擎 / 第三方 / 裸 Debug.Log）。</summary>
        Uncategorized,
        /// <summary>Compiler / asset-import message mirrored from the editor console. Re-sourced from LogEntries each reload, so it is deliberately NOT persisted. 从编辑器控制台镜像的编译器/资源导入消息。每次重载由 LogEntries 重新拉取，故刻意不持久化。</summary>
        Compile,
    }

    /// <summary>
    /// Network tag carried by a log entry.
    /// 日志条目携带的网络标签。
    /// </summary>
    public enum NetTag
    {
        /// <summary>No net tag. 无网络标签。</summary>
        None,
        /// <summary>Server context. 服务器上下文。</summary>
        Server,
        /// <summary>Client context. 客户端上下文。</summary>
        Client,
    }

    /// <summary>
    /// The single atomic data unit of the Debugx Console: one captured log line with full member context.
    /// Produced by the collector from either a Debugx structured event or a non-Debugx Unity log, and consumed by
    /// buffering / collapsing / filtering / searching / statistics / display. Lives in the shared model layer and does
    /// NOT depend on UnityEditor, so both the Editor Console and the runtime Console reuse it unchanged.
    /// Reference type, fully immutable; the collapse count is carried on <see cref="CollapsedRow"/>, never written
    /// back onto the shared entry.
    /// Debugx Console 的唯一原子数据单元：一条捕获到的、带完整成员上下文的日志。由采集器从 Debugx 结构化事件或非 Debugx 的
    /// Unity 日志产出，供 缓冲 / 折叠 / 过滤 / 搜索 / 统计 / 显示 消费。位于共享模型层，不依赖 UnityEditor，
    /// 故 Editor 版与运行时版 Console 原样复用。引用类型，完全不可变；折叠计数由 <see cref="CollapsedRow"/> 承载，绝不写回共享条目。
    /// </summary>
    public sealed class DebugxLogEntry
    {
        /// <summary>Log level. 日志级别。</summary>
        public LogType LogType { get; }

        /// <summary>
        /// Rich text to render (may contain Unity rich-text tags such as &lt;color&gt;). For Debugx logs this is the
        /// final composed string; for non-Debugx logs this is the original condition string.
        /// 用于渲染的富文本（可能含 Unity 富文本标签，如 &lt;color&gt;）。Debugx 日志为最终拼好的显示串；非 Debugx 日志为原始 condition。
        /// </summary>
        public string RichText { get; }

        /// <summary>
        /// Plain text used for search matching and collapse keys. For Debugx logs this is the caller's original message
        /// (which never carries the tag / color / prefix); for non-Debugx logs it is the condition with rich-text tags
        /// stripped. It does NOT carry the Debugx tag — not because it is stripped, but because these source strings
        /// never contain it (the tag only ever appears in <see cref="RichText"/>).
        /// 用于搜索匹配与折叠归并键的纯文本。Debugx 日志为调用方的原始 message（本就不含 标签/颜色/前缀）；非 Debugx 日志为
        /// 剥去富文本标签后的 condition。它不含 Debugx 标签——不是因为被剥离，而是因为这些源串本就不含它（标签只出现在
        /// <see cref="RichText"/> 中）。
        /// </summary>
        public string PlainText { get; }

        /// <summary>Original stack trace string (unparsed). 原始堆栈字符串（未解析）。</summary>
        public string StackTrace { get; }

        /// <summary>
        /// Member key. &gt;0 custom; -1 Normal; -2 Master; 0 Admin; <see cref="DebugxRawLog.UnregisteredKey"/> for
        /// unregistered; <see cref="UncategorizedKey"/> for non-Debugx logs.
        /// 成员 key。&gt;0 自定义；-1 Normal；-2 Master；0 Admin；未注册为 <see cref="DebugxRawLog.UnregisteredKey"/>；
        /// 非 Debugx 日志为 <see cref="UncategorizedKey"/>。
        /// </summary>
        public int MemberKey { get; }

        /// <summary>Member signature; null/empty for non-Debugx. 成员签名；非 Debugx 时为空。</summary>
        public string MemberSignature { get; }

        /// <summary>Member color, 6-digit RGB hex without '#'; null/empty when none. 成员颜色，六位 RGB 十六进制无 '#'；无色时为空。</summary>
        public string ColorHex { get; }

        /// <summary>Member header; null/empty when none. 成员 header；无时为空。</summary>
        public string Header { get; }

        /// <summary>Whether the signature prefix was shown in <see cref="RichText"/>. 签名前缀是否已出现在 <see cref="RichText"/> 中。</summary>
        public bool LogSignatureShown { get; }

        /// <summary>Network tag. 网络标签。</summary>
        public NetTag NetTag { get; }

        /// <summary>Whether this entry came from Debugx (drives the "Debugx only" filter). 是否来自 Debugx（驱动“仅 Debugx”过滤）。</summary>
        public bool IsDebugx { get; }

        /// <summary>Source category. 来源分类。</summary>
        public LogEntryCategory Category { get; }

        /// <summary>Local time when produced. 产生时的本地时间。</summary>
        public DateTime Timestamp { get; }

        /// <summary>Frame number when produced (best-effort; may be 0 for background-thread logs). 产生时的帧号（尽力而为；后台线程日志可能为 0）。</summary>
        public int FrameCount { get; }

        /// <summary>Global monotonic sequence id, for stable ordering. 全局单调递增序号，用于稳定排序。</summary>
        public long SequenceId { get; }

        /// <summary>
        /// Sentinel member key for non-Debugx ("Uncategorized") logs.
        /// 非 Debugx（“未分类”）日志的哨兵成员 key。
        /// </summary>
        public const int UncategorizedKey = int.MinValue + 1;

        public DebugxLogEntry(
            LogType logType, string richText, string plainText, string stackTrace,
            int memberKey, string memberSignature, string colorHex, string header, bool logSignatureShown,
            NetTag netTag, bool isDebugx, LogEntryCategory category,
            DateTime timestamp, int frameCount, long sequenceId)
        {
            LogType = logType;
            RichText = richText;
            PlainText = plainText;
            StackTrace = stackTrace;
            MemberKey = memberKey;
            MemberSignature = memberSignature;
            ColorHex = colorHex;
            Header = header;
            LogSignatureShown = logSignatureShown;
            NetTag = netTag;
            IsDebugx = isDebugx;
            Category = category;
            Timestamp = timestamp;
            FrameCount = frameCount;
            SequenceId = sequenceId;
        }
    }
}
