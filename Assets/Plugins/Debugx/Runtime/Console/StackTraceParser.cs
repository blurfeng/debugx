using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

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

        /// <summary>
        /// Whether this frame's method carries [HideInCallstack] (or is a Debugx-internal forwarder). Such frames are
        /// dropped from stack display and skipped as navigation targets, matching Unity's own callstack stripping —
        /// which does NOT apply to the raw stack traces the Console receives via Application.logMessageReceived.
        /// 本帧的方法是否带 [HideInCallstack]（或属于 Debugx 内部转发帧）。此类帧在堆栈显示中被剔除、也不作为跳转目标，
        /// 对齐 Unity 自身的调用栈裁剪——而该裁剪并不作用于 Console 经 Application.logMessageReceived 收到的原始堆栈。
        /// </summary>
        public readonly bool HideInCallstack;

        public StackFrameInfo(string symbol, string filePath, int line, string rawLine, bool hideInCallstack)
        {
            Symbol = symbol;
            FilePath = filePath;
            Line = line;
            RawLine = rawLine;
            HideInCallstack = hideInCallstack;
        }

        /// <summary>Whether this frame points at an openable source location. 本帧是否指向可打开的源码位置。</summary>
        public bool HasSource => !string.IsNullOrEmpty(FilePath) && Line > 0;

        /// <summary>Whether the source lives under the project's Assets folder. 源码是否位于项目 Assets 目录下。</summary>
        public bool IsInAssets =>
            !string.IsNullOrEmpty(FilePath) &&
            (FilePath.StartsWith("Assets/") || FilePath.StartsWith("Assets\\"));
    }

    /// <summary>
    /// Parses a Unity stack-trace string into structured frames. Does not open files and does NOT depend on UnityEditor,
    /// so it is shared by the Editor Console (for source navigation) and the runtime Console (for stack text display).
    /// Handles the two common Unity formats:
    ///   "Namespace.Type:Method ()"                         (no source)
    ///   "Namespace.Type:Method () (at Assets/Foo/Bar.cs:42)" (with source)
    /// Each frame is also tagged with <see cref="StackFrameInfo.HideInCallstack"/>: the raw traces delivered by
    /// Application.logMessageReceived are NOT run through Unity's [HideInCallstack] callstack stripping (that only
    /// happens inside Unity's own Console window), so this parser reproduces it — via reflection on the frame's method,
    /// with a name fallback for Debugx's own generated forwarder — so plumbing frames don't leak into display or steal
    /// the double-click navigation target.
    /// 将 Unity 堆栈字符串解析为结构化帧。不打开文件、不依赖 UnityEditor，故由 Editor 版 Console（源码跳转）与运行时版
    /// Console（堆栈文本展示）共用。处理两种常见 Unity 格式（见上）。每帧另附
    /// <see cref="StackFrameInfo.HideInCallstack"/> 标记：Application.logMessageReceived 投递的原始堆栈并未经过 Unity 的
    /// [HideInCallstack] 调用栈裁剪（该裁剪只在 Unity 自身 Console 窗口内发生），故本解析器自行复刻它——通过对帧方法反射，
    /// 并对 Debugx 自身生成的转发类做名称兜底——使转发帧不泄漏到显示中、也不抢走双击跳转目标。
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
                    frames.Add(new StackFrameInfo(symbol, filePath, line, rawLine, ComputeHideInCallstack(symbol)));
                }
                else
                {
                    string symbol = rawLine.Trim();
                    frames.Add(new StackFrameInfo(symbol, null, -1, rawLine, ComputeHideInCallstack(symbol)));
                }
            }

            return frames;
        }

        /// <summary>
        /// Picks the best frame to jump to on double-click: the first non-hidden frame that has source, preferring frames
        /// under Assets/. [HideInCallstack] frames (e.g. Debugx's generated forwarder, which has source under Assets/ and
        /// would otherwise be chosen first) are skipped, so navigation lands on the caller's own code — the whole point
        /// of the DLL workflow. Returns false when no navigable frame exists.
        /// 选出双击跳转的最佳帧：第一个非隐藏、带源码的帧，优先 Assets/ 下的帧。跳过 [HideInCallstack] 帧（如 Debugx 生成的
        /// 转发类，它在 Assets/ 下有源码、否则会被最先选中），使跳转落在调用方自己的代码上——这正是 DLL 工作流的目标。
        /// 无可跳转帧时返回 false。
        /// </summary>
        public static bool TryGetNavigationTarget(List<StackFrameInfo> frames, out StackFrameInfo target)
        {
            target = default;
            if (frames == null) return false;

            bool found = false;
            foreach (StackFrameInfo f in frames)
            {
                if (f.HideInCallstack) continue; // never navigate into a hidden forwarder frame. 绝不跳进被隐藏的转发帧。
                if (!f.HasSource) continue;
                if (f.IsInAssets) { target = f; return true; } // Assets frame wins immediately.
                if (!found) { target = f; found = true; }      // otherwise remember the first source frame.
            }

            return found;
        }

        /// <summary>
        /// Returns the stack-trace text with [HideInCallstack] frames removed, matching what the detail pane displays,
        /// so copied text agrees with the visible stack. Returns the input unchanged when it is empty or contains no
        /// hidden frames (preserving the original whitespace / CRLF).
        /// 返回剔除 [HideInCallstack] 帧后的堆栈文本，与详情面板显示一致，使复制内容与可见堆栈相符。输入为空或不含隐藏帧时
        /// 原样返回（保留原始空白/换行）。
        /// </summary>
        public static string StripHiddenFrames(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return stackTrace;

            List<StackFrameInfo> frames = Parse(stackTrace);

            bool anyHidden = false;
            foreach (StackFrameInfo f in frames)
                if (f.HideInCallstack) { anyHidden = true; break; }
            if (!anyHidden) return stackTrace; // nothing to strip; keep original text verbatim. 无可剔除项；原样保留。

            var sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (StackFrameInfo f in frames)
            {
                if (f.HideInCallstack) continue;
                if (!first) sb.Append('\n');
                sb.Append(f.RawLine);
                first = false;
            }
            return sb.ToString();
        }

        // Cache keyed on "Type|Method" -> whether that method carries [HideInCallstack]. Filled lazily so each unique
        // method is resolved by reflection only once; guarded by a lock because Parse can run on the main thread from
        // several Console instances. Symbols are pure text (Type + Method + a simplified param list), so the parameter
        // list is intentionally ignored: a logging forwarder's overloads all share the attribute, and dropping params
        // lets every overload reuse one lookup.
        // 缓存键为 "Type|Method"，值为该方法是否带 [HideInCallstack]。惰性填充，使每个唯一方法只反射解析一次；用锁保护，因为
        // Parse 可能在主线程被多个 Console 实例调用。符号是纯文本（类型+方法+简化参数列表），故刻意忽略参数列表：日志转发方法的
        // 各重载都带同样的特性，忽略参数可让所有重载复用一次查询。
        private static readonly Dictionary<string, bool> _hideCache = new Dictionary<string, bool>();
        private static readonly object _hideCacheLock = new object();

        // Debugx's own generated forwarder type. Hidden even when reflection cannot resolve it (e.g. aggressive AOT
        // stripping), so the plugin's own plumbing never leaks into the stack regardless of platform.
        // Debugx 自身生成的转发类。即使反射无法解析（如激进的 AOT 裁剪）也隐藏它，使插件自身的转发帧无论平台都不泄漏进堆栈。
        private const string DebugxLoggerTypeName = "DebugxLog.DebugxLogger";

        // Decide whether the frame described by <paramref name="symbol"/> should be hidden. Result is cached per method.
        // 判定 <paramref name="symbol"/> 所描述的帧是否应被隐藏。结果按方法缓存。
        private static bool ComputeHideInCallstack(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return false;
            if (!TryExtractTypeAndMethod(symbol, out string typeName, out string methodName)) return false;

            string key = typeName + "|" + methodName;
            lock (_hideCacheLock)
            {
                if (_hideCache.TryGetValue(key, out bool cached)) return cached;
            }

            bool hide = ResolveHideInCallstack(typeName, methodName);

            lock (_hideCacheLock)
            {
                _hideCache[key] = hide;
            }
            return hide;
        }

        // Reflection lookup + Debugx name fallback. Any exotic type/assembly that throws falls through to the fallback.
        // 反射查询 + Debugx 名称兜底。任何抛异常的异常类型/程序集都回退到兜底判断。
        private static bool ResolveHideInCallstack(string typeName, string methodName)
        {
            bool debugxForwarder = typeName == DebugxLoggerTypeName;

            try
            {
                Type type = FindType(typeName);
                if (type != null)
                {
                    MethodInfo[] methods = type.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Static | BindingFlags.Instance);
                    foreach (MethodInfo m in methods)
                    {
                        if (m.Name != methodName) continue;
                        // Any overload carrying the attribute hides the whole method name (params are not compared).
                        // 任一带该特性的重载即隐藏整个方法名（不比较参数）。
                        if (Attribute.IsDefined(m, typeof(HideInCallstackAttribute), false)) return true;
                    }
                }
            }
            catch
            {
                // Reflection can throw on unloadable/dynamic assemblies or malformed names; use the name fallback.
                // 反射在不可加载/动态程序集或畸形名称上可能抛异常；改用名称兜底。
            }

            return debugxForwarder;
        }

        // Split a Unity stack symbol "Namespace.Type:Method (params)" into its type name and method name. The method
        // name ends at the first space / '(' / '<' / '[' so generic and parameterized forms are handled.
        // 将 Unity 堆栈符号 "Namespace.Type:Method (params)" 拆为类型名与方法名。方法名到首个空格/ '(' / '<' / '[' 为止，
        // 从而兼容泛型与带参形式。
        private static bool TryExtractTypeAndMethod(string symbol, out string typeName, out string methodName)
        {
            typeName = null;
            methodName = null;

            int colon = symbol.IndexOf(':');
            if (colon <= 0 || colon >= symbol.Length - 1) return false;

            typeName = symbol.Substring(0, colon).Trim();

            string rest = symbol.Substring(colon + 1);
            int end = rest.IndexOfAny(new[] { ' ', '(', '<', '[' });
            methodName = (end < 0 ? rest : rest.Substring(0, end)).Trim();

            return typeName.Length > 0 && methodName.Length > 0;
        }

        // Resolve a "Namespace.Type" name to a Type by scanning loaded assemblies (the frame's assembly is unknown from
        // the string alone). Not itself cached — the caller caches the final bool per method, so this runs once per name.
        // 通过扫描已加载程序集将 "Namespace.Type" 名解析为 Type（仅凭字符串无法得知帧所属程序集）。本方法不自缓存——调用方按方法
        // 缓存最终 bool，故每个名称只执行一次。
        private static Type FindType(string typeName)
        {
            Type t = Type.GetType(typeName, false);
            if (t != null) return t;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly asm in assemblies)
            {
                try
                {
                    t = asm.GetType(typeName, false);
                    if (t != null) return t;
                }
                catch { /* dynamic / unloadable assembly; skip it. 动态/不可加载程序集；跳过。 */ }
            }
            return null;
        }
    }
}
