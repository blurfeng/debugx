using System;
using UnityEngine;

namespace DebugxLog
{
    /// <summary>
    /// Category of a structured raw log, distinguishing the member / admin / unregistered source.
    /// 结构化原始日志的分类，用于区分 成员 / 管理通道 / 未注册 三种来源。
    /// </summary>
    public enum DebugxLogCategory
    {
        /// <summary>
        /// Normal member log (custom member, or preset Normal/Master).
        /// 普通成员日志（自定义成员，或预设 Normal/Master）。
        /// </summary>
        Member,

        /// <summary>
        /// Admin channel log via LogAdm (key 0); bypasses member switches.
        /// 管理通道日志（LogAdm，key 0）；不受成员开关影响。
        /// </summary>
        Admin,

        /// <summary>
        /// Log from an unregistered member key/signature (printed because allowUnregisteredMember is true).
        /// 来自未注册成员 key/signature 的日志（因 allowUnregisteredMember 为真而打印）。
        /// </summary>
        Unregistered,
    }

    /// <summary>
    /// Structured raw log payload dispatched by <see cref="Debugx.OnRawLog"/> at the single logging funnel.
    /// It carries member metadata + the original message + the final composed string, so a Console can consume
    /// Debugx logs with full member context without re-parsing the formatted text.
    /// Value type (plain struct with readonly fields, .NET 3.5 friendly) to avoid per-log heap allocation.
    /// 由 <see cref="Debugx.OnRawLog"/> 在唯一日志收口点派发的结构化原始日志负载。
    /// 携带成员元数据 + 原始 message + 拼好的最终显示串，使 Console 无需对格式化文本做正则反解即可以完整成员上下文消费 Debugx 日志。
    /// 采用值类型（普通 struct + readonly 字段，兼容 .NET 3.5）以避免每条日志的堆分配。
    /// </summary>
    public struct DebugxRawLog
    {
        /// <summary>
        /// Sentinel member key for unregistered logs (no member info). Distinct from Admin's key 0.
        /// 未注册日志（无成员信息）的哨兵 key。与 Admin 的 key 0 区分。
        /// </summary>
        public const int UnregisteredKey = int.MinValue;

        /// <summary>
        /// Member key. &gt;0 custom; -1 Normal; -2 Master; 0 Admin; <see cref="UnregisteredKey"/> when unregistered.
        /// 成员 key。&gt;0 自定义；-1 Normal；-2 Master；0 Admin；未注册为 <see cref="UnregisteredKey"/>。
        /// </summary>
        public readonly int Key;

        /// <summary>
        /// Member signature; null when unregistered.
        /// 成员签名；未注册时为 null。
        /// </summary>
        public readonly string Signature;

        /// <summary>
        /// Member color, 6-digit RGB hex without '#'; null when the member has no color.
        /// 成员颜色，六位 RGB 十六进制无 '#'；成员无颜色时为 null。
        /// </summary>
        public readonly string ColorHex;

        /// <summary>
        /// Member header; null when the member has no header.
        /// 成员 header；成员无 header 时为 null。
        /// </summary>
        public readonly string Header;

        /// <summary>
        /// Whether the signature prefix "[Sig: xxx]" was shown in <see cref="FinalText"/>.
        /// 签名前缀 "[Sig: xxx]" 是否已出现在 <see cref="FinalText"/> 中。
        /// </summary>
        public readonly bool LogSignatureShown;

        /// <summary>
        /// Source category (Member / Admin / Unregistered).
        /// 来源分类（Member / Admin / Unregistered）。
        /// </summary>
        public readonly DebugxLogCategory Category;

        /// <summary>
        /// Log level. Debugx only produces Log / Warning / Error.
        /// 日志级别。Debugx 只产生 Log / Warning / Error。
        /// </summary>
        public readonly LogType LogType;

        /// <summary>
        /// Original message object passed by the caller (unformatted, without tag/color/prefix).
        /// 调用方传入的原始 message（未格式化，不含标签/颜色/前缀）。
        /// </summary>
        public readonly object RawMessage;

        /// <summary>
        /// Final composed string emitted to unityLogger (with tag / color / net tag / time / signature).
        /// 发给 unityLogger 的最终显示串（含标签 / 颜色 / 网络标签 / 时间 / 签名）。
        /// </summary>
        public readonly string FinalText;

        /// <summary>
        /// Whether a net tag was requested for this log.
        /// 本条日志是否请求了网络标签。
        /// </summary>
        public readonly bool ShowNetTag;

        /// <summary>
        /// Whether a timestamp was requested for this log.
        /// 本条日志是否请求了时间戳。
        /// </summary>
        public readonly bool ShowTime;

        /// <summary>
        /// Whether the net context evaluated as server (only meaningful when ShowNetTag and a server-check is set).
        /// 网络上下文是否判定为服务器（仅当 ShowNetTag 且已设置 server-check 时有意义）。
        /// </summary>
        public readonly bool IsServer;

        /// <summary>
        /// Local time when the log was produced.
        /// 日志产生时的本地时间。
        /// </summary>
        public readonly DateTime Timestamp;

        /// <summary>
        /// Creates a structured raw log payload. Fields map one-to-one to the members documented above.
        /// 创建结构化原始日志负载。各参数与上面文档化的字段一一对应。
        /// </summary>
        public DebugxRawLog(
            int key, string signature, string colorHex, string header, bool logSignatureShown,
            DebugxLogCategory category, LogType logType, object rawMessage, string finalText,
            bool showNetTag, bool showTime, bool isServer, DateTime timestamp)
        {
            Key = key;
            Signature = signature;
            ColorHex = colorHex;
            Header = header;
            LogSignatureShown = logSignatureShown;
            Category = category;
            LogType = logType;
            RawMessage = rawMessage;
            FinalText = finalText;
            ShowNetTag = showNetTag;
            ShowTime = showTime;
            IsServer = isServer;
            Timestamp = timestamp;
        }
    }
}
