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

        // Search-match highlight: a translucent amber background painted behind matched substrings via a rich-text
        // <mark> tag. Stored as an RRGGBBAA hex string because <mark=#...> takes a hex color, not a UnityEngine.Color.
        // 搜索命中高亮：半透明琥珀色底，通过富文本 <mark> 标签绘制在命中子串后。以 RRGGBBAA 十六进制字符串存储，
        // 因为 <mark=#...> 接收的是十六进制颜色而非 UnityEngine.Color。
        public const string SearchHighlightHex = "FFD54F66";

        // Sizing. 尺寸。
        public const float ListItemHeight = 22f;
        public const float DetailPaneHeight = 130f;
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

        // Ring-buffer capacity for the runtime Console (mobile-friendly default; §8 suggests 500–1000 mobile / 2000–5000 desktop).
        // 运行时 Console 的环形缓冲容量（对移动端友好的默认值；§8 建议移动 500–1000 / 桌面 2000–5000）。
        public const int RuntimeBufferCapacity = 1000;
    }
}
