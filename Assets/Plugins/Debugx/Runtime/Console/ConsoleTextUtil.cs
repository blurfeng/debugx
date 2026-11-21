using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DebugxLog.Console
{
    /// <summary>
    /// Small text helpers for the Console shared layer. Thread-safe (compiled static Regex, no shared mutable state).
    /// Console 共享层的小文本工具。线程安全（编译期静态 Regex，无共享可变状态）。
    /// </summary>
    public static class ConsoleTextUtil
    {
        // Strips the common Unity rich-text tags so a message can be matched/collapsed as plain text.
        // Targets the tag set Unity's Console understands; leaves other '<...>' (e.g. generics) untouched.
        // 剥去常见的 Unity 富文本标签，使消息可作为纯文本用于匹配/折叠。
        // 仅针对 Unity Console 识别的标签集合；不动其它 '<...>'（如泛型）。
        // CultureInvariant 使 IgnoreCase 折叠不随运行区域变化：否则土耳其/阿塞拜疆区域下含字母 i 的大写标签
        // （如 <I>、<SIZE>、<ALIGN>）会剥不掉，导致同一消息在不同区域的 PlainText 不一致（影响折叠与搜索）。
        // CultureInvariant keeps IgnoreCase folding stable across locales: without it, uppercase tags containing the
        // letter i (e.g. <I>, <SIZE>, <ALIGN>) fail to strip under Turkish/Azerbaijani cultures, so the same message
        // yields different PlainText per locale (breaking collapsing and search).
        private const string RichTagPattern =
            @"</?(?:color|b|i|size|material|quad|u|s|sub|sup|mark|align|alpha|font)(?:=[^>]*)?>";
        private const RegexOptions TagOptions =
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private static readonly Regex _richTextRegex = new Regex(RichTagPattern, TagOptions);

        // The same tag set anchored at a given index (\G), so callers walking rich text skip exactly the tags
        // StripRichText removes and treat any other '<' (List<int>, hp <= 0, <null>) as visible text.
        // 同一标签集合，锚定在指定下标（\G），使遍历富文本的调用方恰好跳过 StripRichText 会移除的标签，其他 '<'
        // （List<int>、hp <= 0、<null>）都当作可见文本。
        private static readonly Regex _richTagAtRegex = new Regex(@"\G" + RichTagPattern, TagOptions);

        /// <summary>
        /// Returns the message with recognized Unity rich-text tags removed. Null/empty inputs pass through.
        /// 返回移除了已识别 Unity 富文本标签的消息。null/空输入原样返回。
        /// </summary>
        public static string StripRichText(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;
            return _richTextRegex.Replace(message, string.Empty);
        }

        /// <summary>
        /// Length of the recognized rich-text tag starting at <paramref name="index"/>, or 0 when the character there is
        /// visible text (including a literal '&lt;').
        /// 从 <paramref name="index"/> 开始的已识别富文本标签的长度；该处是可见文本（包括字面量 '&lt;'）时返回 0。
        /// </summary>
        public static int RichTagLengthAt(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length || text[index] != '<') return 0;
            Match m = _richTagAtRegex.Match(text, index);
            return m.Success ? m.Length : 0;
        }

        /// <summary>
        /// Row timestamp text, culture-invariant ("HH:mm:ss"), shared by the Editor and runtime Consoles.
        /// 行时间戳文本（与区域无关的 "HH:mm:ss"），Editor 与运行时 Console 共用。
        /// </summary>
        public static string FormatTime(DateTime time) => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        /// <summary>
        /// Clipboard text for copied entries: each plain message, optionally followed by its stack with Debugx's own
        /// [HideInCallstack] forwarder frames removed (matching the visible stack); a blank line separates stacked entries.
        /// 复制条目的剪贴板文本：每条为纯文本消息，可选附上堆栈（剔除 Debugx 自身的 [HideInCallstack] 转发帧，与可见堆栈一致）；
        /// 带堆栈的多条之间以空行分隔。
        /// </summary>
        public static string FormatEntriesForCopy(IReadOnlyList<DebugxLogEntry> entries, bool withStack)
        {
            if (entries == null || entries.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                DebugxLogEntry e = entries[i];
                sb.Append(e.PlainText);
                if (!withStack) continue;

                string stack = StackTraceParser.StripHiddenFrames(e.StackTrace);
                if (!string.IsNullOrEmpty(stack))
                    sb.Append('\n').Append(stack).Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }
    }
}
