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

        // Severity colors — used for the row's left color bar and the three count buttons.
        // 严重级别色——用于行左侧色条与三个计数按钮。
        public static readonly Color LogColor = new Color(0.80f, 0.80f, 0.82f);
        public static readonly Color WarnColor = new Color(1f, 0.80f, 0.25f);
        public static readonly Color ErrorColor = new Color(1f, 0.42f, 0.38f);

        // Optional row columns: timestamp (muted) + net tag (Server/Client). 可选行内列：时间戳（灰）+ 网络标签（Server/Client）。
        public static readonly Color TimestampColor = new Color(0.55f, 0.55f, 0.55f);
        public static readonly Color NetServerColor = new Color(0.45f, 0.75f, 1f);
        public static readonly Color NetClientColor = new Color(0.55f, 0.85f, 0.55f);

        // Sizing. 尺寸。
        public const float ListItemHeight = 22f;
        public const float DetailPaneHeight = 130f;
        public const float SeverityBarWidth = 4f;
        public const float TimestampWidth = 56f;
        public const int CountOverflowThreshold = 999;
        public const float InactiveOpacity = 0.4f;
        public const int FontSizeSmall = 11;

        // Ring-buffer capacity for the runtime Console (mobile-friendly default; §8 suggests 500–1000 mobile / 2000–5000 desktop).
        // 运行时 Console 的环形缓冲容量（对移动端友好的默认值；§8 建议移动 500–1000 / 桌面 2000–5000）。
        public const int RuntimeBufferCapacity = 1000;
    }
}
