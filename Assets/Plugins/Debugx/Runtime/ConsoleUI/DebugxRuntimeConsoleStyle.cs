using UnityEngine;

namespace DebugxLog.Console.Runtime
{
    /// <summary>
    /// Visual constants for the runtime Console. Runtime-safe (no UnityEditor / EditorGUIUtility), so this is the
    /// runtime counterpart of the Editor's DebugxConsoleStyle. Colors are chosen to read on top of arbitrary game
    /// content (dark, semi-transparent panel).
    /// 运行时 Console 的视觉常量。运行时安全（不依赖 UnityEditor / EditorGUIUtility），是 Editor 版 DebugxConsoleStyle
    /// 的运行时对应物。配色考虑叠加在任意游戏画面之上（深色、半透明面板）。
    /// </summary>
    internal static class DebugxRuntimeConsoleStyle
    {
        // Panel / chrome. 面板 / 外框。
        public static readonly Color PanelBg = new Color(0.11f, 0.11f, 0.12f, 0.94f);
        public static readonly Color ToolbarBg = new Color(0.17f, 0.17f, 0.18f, 1f);
        public static readonly Color DetailBg = new Color(0.09f, 0.09f, 0.10f, 0.98f);
        public static readonly Color BorderColor = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color TextColor = new Color(0.86f, 0.86f, 0.86f);
        // Muted color for hint text such as the "No results" overlay. Lighter than the timestamp gray so it reads as a
        // centered message over the dark panel. 提示文字（如 “No results” 覆盖提示）的柔和色，比时间戳灰更亮，作为深色面板上的居中提示更清晰。
        public static readonly Color HintColor = new Color(0.6f, 0.6f, 0.62f);
        // Font size of the centered "No results" search-empty overlay (larger than the row text for emphasis).
        // 搜索无结果居中提示的字号（比行文字更大以突出）。
        public const float NoResultsFontSize = 20f;
        public static readonly Color OpenButtonBg = new Color(0.15f, 0.15f, 0.16f, 0.80f);
        // Explicit dark button background — the default runtime theme paints buttons light gray, which hides our light
        // text. 显式深色按钮底——默认运行时主题按钮是浅灰底，会盖掉我们的浅色文字。
        public static readonly Color ButtonBg = new Color(0.27f, 0.27f, 0.29f, 1f);
        // "Hold-to-clear" progress sweep painted over the Clear button while held (translucent red = destructive intent);
        // grows 0→100% width over the hold duration, half-alpha so the "Clear" text stays legible under it.
        // “长按清空”进度扫光，按住 Clear 时覆盖其上（半透明红＝破坏性操作意图）；宽度在按住时长内 0→100% 增长，半透明使下方 “Clear” 文字仍可读。
        public static readonly Color HoldFillColor = new Color(1f, 0.42f, 0.38f, 0.5f);

        // Severity colors — used for the row's left color bar and the three count buttons.
        // 严重级别色——用于行左侧色条与三个计数按钮。
        public static readonly Color LogColor = new Color(0.80f, 0.80f, 0.82f);
        public static readonly Color WarnColor = new Color(1f, 0.80f, 0.25f);
        public static readonly Color ErrorColor = new Color(1f, 0.42f, 0.38f);

        // Optional row columns: timestamp (muted) + net tag (Server/Client). 可选行内列：时间戳（灰）+ 网络标签（Server/Client）。
        public static readonly Color TimestampColor = new Color(0.55f, 0.55f, 0.55f);
        public static readonly Color NetServerColor = new Color(0.45f, 0.75f, 1f);
        public static readonly Color NetClientColor = new Color(0.55f, 0.85f, 0.55f);

        // Search-match highlight: a translucent blue background painted behind matched substrings via a rich-text
        // <mark> tag. Stored as an RRGGBBAA hex string because <mark=#...> takes a hex color, not a UnityEngine.Color.
        // Kept in sync with DebugxConsoleStyle.SearchHighlightHex so both Consoles highlight matches the same blue.
        // 搜索命中高亮：半透明蓝色底，通过富文本 <mark> 标签绘制在命中子串后。以 RRGGBBAA 十六进制字符串存储，
        // 因为 <mark=#...> 接收的是十六进制颜色而非 UnityEngine.Color。与 DebugxConsoleStyle.SearchHighlightHex 保持一致，使两个 Console 的命中高亮为同一种蓝。
        public const string SearchHighlightHex = "73BFFF80";

        // Mouse-wheel scroll step for the detail/stack ScrollView. UI Toolkit's default wheel step scrolls the detail
        // pane too far per notch; a smaller value gives gentler, near line-by-line scrolling. Tune to taste in-editor.
        // 详情/堆栈 ScrollView 的滚轮步长。UI Toolkit 默认每格滚动过多，调小可获得更平缓、接近逐行的滚动。进编辑器按手感微调。
        public const float DetailWheelScrollSize = 12f;

        // Sizing. 尺寸。
        public const float ListItemHeight = 22f;
        // Fixed height of the bottom detail/stack pane. Roomy enough to read a message plus a few stack frames without
        // squeezing the list (the panel is enlarged to match — see BuildPanel).
        // 底部详情/堆栈面板的固定高度。足够读一条消息加几帧堆栈而不挤压列表（面板整体也相应放大——见 BuildPanel）。
        public const float DetailPaneHeight = 200f;
        public const float SeverityBarWidth = 4f;
        public const float TimestampWidth = 56f;
        public const int CountOverflowThreshold = 999;

        // Collapse count badge: a rounded pill behind the duplicate count, matching the Editor Console. Slightly lighter
        // than the panel so the count reads over dark game content.
        // 折叠计数徽标：数字后的圆角药丸，与 Editor 版一致。比面板略浅，使计数叠在深色游戏画面上仍清晰。
        public static readonly Color BadgeBgColor = new Color(0.32f, 0.32f, 0.35f, 0.95f);
        public const float BadgeCornerRadius = 8f;
        public const float BadgePaddingH = 6f;
        public const float BadgePaddingV = 1f;
        public const float InactiveOpacity = 0.4f;
        public const int FontSizeSmall = 11;

        // Severity icons + branding icon, loaded from any Resources folder at runtime (files live under
        // Plugins/Debugx/Resources). When absent the Console falls back to the color bar / colored count text.
        // 严重级别图标 + 品牌图标，运行时从任意 Resources 目录加载（文件位于 Plugins/Debugx/Resources）。缺失时 Console
        // 回退到色条 / 彩色计数文字。
        public const string IconInfoResource = "icon_info";
        public const string IconWarningResource = "icon_warning";
        public const string IconErrorResource = "icon_error";
        public const string IconArticleResource = "icon_article";

        public const float RowIconSize = 14f;
        public const float RowIconMarginRight = 5f;
        public const float CountIconSize = 14f;
        public const float CountIconMarginRight = 3f;
        public const float OpenButtonIconSize = 15f;

        // Number of simultaneous fingers whose tap toggles the console on touch devices (a mobile alternative to the
        // backquote key). 触屏端同时按下、点击即可开合 Console 的手指数（反引号键的移动端替代）。
        public const int SummonTouchCount = 3;

        // Ring-buffer capacity moved to a persisted, user-editable setting: DebugxStaticData.RuntimeConsoleBufferCapacity
        // (default 4000; live-editable via the Console's Settings popup). §8 rule of thumb: 500–1000 mobile / 2000–5000 desktop.
        // 环形缓冲容量已迁移为可持久化、用户可编辑的设置：DebugxStaticData.RuntimeConsoleBufferCapacity
        // （默认 4000；可在 Console 的 Settings 弹层里即时修改）。§8 经验值：移动 500–1000 / 桌面 2000–5000。
    }
}
