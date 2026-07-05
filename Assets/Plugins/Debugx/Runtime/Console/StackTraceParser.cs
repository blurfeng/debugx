using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DebugxLog.Console
{
    /// <summary>
    /// One parsed stack frame: method symbol plus an optional source file and line.
    /// 一个解析后的堆栈帧：方法符号，加上可选的源文件与行号。
    /// </summary>
    public struct StackFrameInfo
    {
        /// <summary>Method signature text (the part before "(at ...)"). 方法签名文本（"(at ...)" 之前的部分）。</summary>
        public readonly string Symbol;
        /// <summary>Source file path (e.g. Assets/Foo/Bar.cs); null when the frame has no file. 源文件路径；无文件时为 null。</summary>
        public readonly string FilePath;
        /// <summary>1-based line number; -1 when unknown. 从 1 起的行号；未知时为 -1。</summary>
        public readonly int Line;
        /// <summary>The original raw stack line. 原始堆栈行文本。</summary>
        public readonly string RawLine;

        public StackFrameInfo(string symbol, string filePath, int line, string rawLine)
        {
            Symbol = symbol;
            FilePath = filePath;
            Line = line;
            RawLine = rawLine;
        }

        /// <summary>Whether this frame points at an openable source location. 本帧是否指向可打开的源码位置。</summary>
        public bool HasSource => !string.IsNullOrEmpty(FilePath) && Line > 0;

        /// <summary>Whether the source lives under the project's Assets folder. 源码是否位于项目 Assets 目录下。</summary>
        public bool IsInAssets =>
            !string.IsNullOrEmpty(FilePath) &&
            (FilePath.StartsWith("Assets/") || FilePath.StartsWith("Assets\\"));
    }

    /// <summary>
    /// Parses a Unity stack-trace string into structured frames. Pure string parsing — it never opens files and does
    /// NOT depend on UnityEditor, so it is shared by the Editor Console (for source navigation) and the runtime Console
    /// (for stack text display). Handles the two common Unity formats:
    ///   "Namespace.Type:Method ()"                         (no source)
    ///   "Namespace.Type:Method () (at Assets/Foo/Bar.cs:42)" (with source)
    /// 将 Unity 堆栈字符串解析为结构化帧。纯字符串解析——不打开文件、不依赖 UnityEditor，故由 Editor 版 Console（源码跳转）
    /// 与运行时版 Console（堆栈文本展示）共用。处理两种常见 Unity 格式（见上）。
    /// </summary>
    public static class StackTraceParser
    {
        // Matches the "(at <path>:<line>)" suffix Unity appends to frames that have source info.
        // 匹配 Unity 为带源码信息的帧追加的 "(at <path>:<line>)" 后缀。
        private static readonly Regex _atRegex = new Regex(
            @"\(at (.+):(\d+)\)\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Parse a stack-trace string into frames (one per non-empty line). Returns an empty list for null/empty input.
        /// 将堆栈字符串解析为帧（每个非空行一帧）。null/空输入返回空列表。
        /// </summary>
        public static List<StackFrameInfo> Parse(string stackTrace)
        {
            var frames = new List<StackFrameInfo>();
            if (string.IsNullOrEmpty(stackTrace)) return frames;

            string[] lines = stackTrace.Split('\n');
            foreach (string rawLineWithCr in lines)
            {
                string rawLine = rawLineWithCr.TrimEnd('\r');
                if (string.IsNullOrEmpty(rawLine)) continue;
                if (string.IsNullOrEmpty(rawLine.Trim())) continue;

                Match m = _atRegex.Match(rawLine);
                if (m.Success)
                {
                    string filePath = m.Groups[1].Value.Trim();
                    int line = int.TryParse(m.Groups[2].Value, out int parsed) ? parsed : -1;
                    string symbol = rawLine.Substring(0, m.Index).TrimEnd();
                    frames.Add(new StackFrameInfo(symbol, filePath, line, rawLine));
                }
                else
                {
                    frames.Add(new StackFrameInfo(rawLine.Trim(), null, -1, rawLine));
                }
            }

            return frames;
        }

        /// <summary>
        /// Picks the best frame to jump to on double-click: the first frame that has source, preferring frames under
        /// Assets/. The parser no longer special-cases Debugx-internal frames — it relies on Unity's own
        /// [HideInCallstack] / "*Logger" callstack stripping to drop the logging plumbing frames before they reach here.
        /// Returns false when no navigable frame exists.
        /// 选出双击跳转的最佳帧：第一个带源码的帧，优先 Assets/ 下的帧。解析器不再特殊处理 Debugx 内部帧——由 Unity 自身对
        /// [HideInCallstack] / "*Logger" 结尾类的调用栈裁剪负责在此之前剔除日志转发帧。无可跳转帧时返回 false。
        /// </summary>
        public static bool TryGetNavigationTarget(List<StackFrameInfo> frames, out StackFrameInfo target)
        {
            target = default;
            if (frames == null) return false;

            bool found = false;
            foreach (StackFrameInfo f in frames)
            {
                if (!f.HasSource) continue;
                if (f.IsInAssets) { target = f; return true; } // Assets frame wins immediately.
                if (!found) { target = f; found = true; }      // otherwise remember the first source frame.
            }

            return found;
        }
    }
}
