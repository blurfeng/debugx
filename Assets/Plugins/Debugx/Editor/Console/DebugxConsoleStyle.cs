using UnityEngine;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// A language-dependent width pair (English / Chinese) for a toolbar item.
    /// 工具栏条目按语言区分的宽度对（英文 / 中文）。
    /// </summary>
    public struct LangWidth
    {
        public readonly float En;
        public readonly float Cn;

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
        /// <summary>Width of the Clear dropdown-arrow button (kept narrow so the caret tucks against the label). Clear 下拉箭头按钮的宽度（保持窄，让三角紧贴标签）。</summary>
        public static readonly float ClearDropdownWidth = 16f;
        /// <summary>Width (px) of EVERY toolbar separator hairline — Clear's inner caret line and all inter-item dividers. 每条工具栏分隔细线的宽度（像素）——Clear 内部三角竖线，以及所有条目间分隔线。</summary>
        public static readonly float ToolbarDividerWidth = 1f;
        /// <summary>Color of EVERY toolbar separator hairline. Dark (black @0.5) reads as a hairline on both skins. 每条工具栏分隔细线的颜色。深色（黑@0.5）在深/浅皮肤上都呈细线。</summary>
        public static Color ToolbarDividerColor = new Color(0f, 0f, 0f, 0.5f);
        /// <summary>Height of Clear's inner caret divider as a percent of the button height (60 = 60% tall, 20% inset top &amp; bottom). Clear 内部三角分隔线高度占按钮高度的百分比（60 = 占 60% 高，上下各内缩 20%）。</summary>
        public static readonly float ClearDividerHeightPercent = 60f;
        
        /// <summary>Font size of the dropdown caret glyph ("▼") for Clear and Members. Clear 与 Members 下拉三角字形（“▼”）的字号。</summary>
        public static readonly float CaretFontSize = 8f;
        
        public static LangWidth CollapseWidth = new LangWidth(58f, 34f);
        public static LangWidth ErrorPauseWidth = new LangWidth(74f, 56f);
        public static LangWidth EditorWidth = new LangWidth(44f, 44f);
        public static LangWidth MembersWidth = new LangWidth(76f, 48f);
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
        /// <summary>Width of the optional timestamp column. 可选时间戳列的宽度。</summary>
        public static readonly float TimestampWidth = 58f;
        /// <summary>Muted color for the timestamp column. 时间戳列的柔和颜色。</summary>
        public static Color TimestampColor = new Color(0.5f, 0.5f, 0.5f);

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
        /// <summary>Bottom border color of the editor panel. 编辑面板底部边框颜色。</summary>
        public static Color EditorPanelBorderColor = new Color(0f, 0f, 0f, 0.3f);
        /// <summary>Muted color for hint text. 提示文字的柔和颜色。</summary>
        public static Color HintColor = new Color(0.6f, 0.6f, 0.6f);
        /// <summary>Color of the list/detail splitter hairline. Dark gray and static (no hover recolor), like the native Console. 列表/详情分隔线颜色。深灰、静态（悬停不变色），仿原生 Console。</summary>
        public static Color DetailDividerColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        /// <summary>Thickness (px) of the list/detail splitter hairline. Lower = thinner. 列表/详情分隔线的粗细（像素）。越小越细。</summary>
        public static readonly float DetailDividerThickness = 1f;
        /// <summary>Height (px) of the splitter's invisible drag grab-strip. Larger = easier to grab; smaller = closer to the 1px line. 分隔线不可见拖拽抓取条的高度（像素）。越大越好抓，越小越贴近细线。</summary>
        public static readonly float DetailDividerGrabSize = 5f;
    }
}
