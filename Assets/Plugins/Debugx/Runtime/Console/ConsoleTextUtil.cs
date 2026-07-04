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
        private static readonly Regex _richTextRegex = new Regex(
            @"</?(?:color|b|i|size|material|quad|u|s|sub|sup|mark|align|alpha|font)(?:=[^>]*)?>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Returns the message with recognized Unity rich-text tags removed. Null/empty inputs pass through.
        /// 返回移除了已识别 Unity 富文本标签的消息。null/空输入原样返回。
        /// </summary>
        public static string StripRichText(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;
            return _richTextRegex.Replace(message, string.Empty);
        }
    }
}
