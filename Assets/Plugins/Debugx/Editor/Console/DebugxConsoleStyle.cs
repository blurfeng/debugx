using UnityEngine;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// A language-dependent width pair (English / Chinese) for a toolbar item.
    /// 工具栏条目按语言区分的宽度对（英文 / 中文）。
    /// </summary>
    public struct LangWidth
    {
        public float En;
        public float Cn;

        public LangWidth(float en, float cn)
        {
            En = en;
            Cn = cn;
        }
    }

    /// <summary>
    /// Centralised, tweakable UI style constants for the Debugx Console window (widths, sizes, colors, thresholds).
    /// Edit values here to adjust the look without touching window logic. These are intentionally mutable static
    /// fields so everything is easy to change in one place.
    /// Debugx Console 窗口集中、可调的 UI 样式常量（宽度、尺寸、颜色、阈值）。在此改值即可调整外观，无需改窗口逻辑。
    /// 刻意采用可变静态字段，便于集中一处修改。
    /// </summary>
    public static class DebugxConsoleStyle
    {
        // ---- Window ----
        /// <summary>Minimum window size. 窗口最小尺寸。</summary>
        public static Vector2 MinWindowSize = new Vector2(480f, 320f);
        /// <summary>Fixed height of the bottom detail pane. 底部详情面板固定高度。</summary>
        public static readonly float DetailPaneHeight = 140f;

        // ---- Toolbar item widths (English / Chinese) ----
        public static LangWidth ClearWidth = new LangWidth(40f, 34f);
        public static LangWidth CollapseWidth = new LangWidth(58f, 34f);
        public static LangWidth ClearOnPlayWidth = new LangWidth(82f, 78f);
        public static LangWidth ErrorPauseWidth = new LangWidth(74f, 56f);
        public static LangWidth RuntimeWidth = new LangWidth(56f, 44f);
        public static LangWidth DebugxOnlyWidth = new LangWidth(80f, 64f);
        public static LangWidth MembersWidth = new LangWidth(70f, 42f);
        public static LangWidth LangButtonWidth = new LangWidth(24f, 24f);
        public static LangWidth CountWidth = new LangWidth(34f, 34f);
        /// <summary>Search field preferred (flex-basis) width. 搜索栏首选（flex-basis）宽度。</summary>
        public static readonly float SearchWidth = 160f;
        /// <summary>Minimum search width; below this the responsive logic hides it. 搜索栏最小宽度；再窄则由响应式逻辑隐藏。</summary>
        public static readonly float SearchMinWidth = 80f;
        /// <summary>Space to the right of the search field. 搜索栏右侧的间距。</summary>
        public static readonly float SearchRightSpace = 6f;

        // ---- Toolbar item layout ----
        /// <summary>Left/right padding applied to every toolbar item. 每个工具栏条目的左右内边距。</summary>
        public static readonly float ItemPadding = 2f;
        /// <summary>Slack subtracted from the toolbar width before deciding what fits. 判断可容纳前从工具栏宽度扣除的余量。</summary>
        public static readonly float ResponsiveBuffer = 12f;

        // ---- List row ----
        public static readonly float ListItemHeight = 20f;
        public static readonly float RowIconSize = 16f;
        public static readonly float RowIconMarginRight = 4f;

        // ---- Count buttons ----
        public static readonly float CountIconSize = 16f;
        public static readonly float CountIconMarginRight = 2f;
        /// <summary>Opacity for a count button whose type is filtered out. 类型被过滤关闭时计数按钮的不透明度。</summary>
        public static readonly float CountInactiveOpacity = 0.4f;
        /// <summary>Counts above this show as "N+". 超过此值的计数显示为 “N+”。</summary>
        public static readonly int CountOverflowThreshold = 999;

        // ---- Colors ----
        /// <summary>Color of clickable (navigable) stack-frame lines. 可点击（可跳转）堆栈帧行的颜色。</summary>
        public static Color StackLinkColor = new Color(0.4f, 0.6f, 1f);
        /// <summary>Bottom border color of the runtime panel. 运行时面板底部边框颜色。</summary>
        public static Color RuntimePanelBorderColor = new Color(0f, 0f, 0f, 0.3f);
        /// <summary>Muted color for hint text. 提示文字的柔和颜色。</summary>
        public static Color HintColor = new Color(0.6f, 0.6f, 0.6f);
    }
}
