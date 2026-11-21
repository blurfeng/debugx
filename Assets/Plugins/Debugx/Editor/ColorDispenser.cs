using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DebugxLog.Editor
{
    public static class ColorDispenser
    {
        private static DebugxProjectSettingsAsset SettingsAsset => DebugxProjectSettingsAsset.Instance;

        // In the HSV color model, the Hue (H) component is measured on a circular scale, where 0 and 360 degrees represent the same color.
        // HSV中H色调的尺度，注意0和360的颜色是一样的。
        private const int HDimension = 360;

        private const float SDark = 0.15f;
        private const float VDark = 0.95f;
        private const float SLight = 0.75f;
        private const float VLight = 0.17f;

        // Master's hue (degrees). Other members are kept away from it. Master 的色相（度）。其他成员会避开它。
        private const int MasterHue = 5;

        // Below this saturation a color is treated as achromatic (white / gray / black): it has no meaningful hue.
        // 饱和度低于此值视为无彩色（白/灰/黑）：没有有意义的色相。
        private const float AchromaticSaturation = 0.01f;

        /// <summary>
        /// HSV color model. Values range from 0 to 1.
        /// HSV颜色模型。值为0-1。
        /// </summary>
        private struct ColorHSV
        {
            public readonly float h;
            public readonly float s;
            public readonly float v;

            public ColorHSV(float h)
            {
                this.h = h;
                // 实时查询编辑器皮肤，不缓存：Preferences 切换 Dark/Light 不会触发脚本域重载，缓存会过期，
                // 导致按错误皮肤生成 S/V（并使 AdaptColorByEditorSkin 反复误判“已变更”、无法幂等）。
                // Query the editor skin live, do not cache: switching Dark/Light in Preferences doesn't reload the script
                // domain, so a cache goes stale and colors are built for the wrong skin (also breaking AdaptColorByEditorSkin's idempotency).
                bool isDark = EditorGUIUtility.isProSkin;
                s = isDark ? SDark : SLight;
                v = isDark ? VDark : VLight;
            }
        }

        public static void OnInitializeOnLoadMethod()
        {
            DebugxProjectSettingsAsset.GetRandomColorForMember += GetRandomColorForMember;
            DebugxProjectSettingsAsset.GetNormalMemberColor += GetNormalMemberColor;
            DebugxProjectSettingsAsset.GetMasterMemberColor += GetMasterMemberColor;
        }

        /// <summary>
        /// Get a random color for a member.
        /// 为成员获取一个随机颜色。
        /// </summary>
        /// <returns></returns>
        private static Color GetRandomColorForMember()
        {
            ColorHSV colorHSV = new(GetColorH() * 1f / HDimension);

            return Color.HSVToRGB(colorHSV.h, colorHSV.s, colorHSV.v);
        }

        private static Color GetNormalMemberColor()
        {
            return EditorGUIUtility.isProSkin ? Color.white : Color.black;
        }

        private static Color GetMasterMemberColor()
        {
            ColorHSV colorHSV = new(MasterHue * 1f / HDimension);

            return Color.HSVToRGB(colorHSV.h, colorHSV.s, colorHSV.v);
        }

        /// <summary>
        /// Get an HSV color’s Hue (H) value that is the least similar (most distant) to the H values of the existing members’ colors.
        /// 获取一个HSV颜色的H值，此值是和当前存在的Member的Color最不接近的值。
        /// </summary>
        /// <returns></returns>
        private static int GetColorH()
        {
            if (SettingsAsset == null) return 0;

            // Hues in use: the custom members plus the chromatic default members (Master), so a new member never lands
            // next to Master. Achromatic colors (Normal's white / black) have no hue and are skipped.
            // 已用色相：自定义成员加上有彩色的默认成员（Master），使新成员不会紧挨着 Master。无彩色（Normal 的白/黑）没有色相，跳过。
            List<int> colorHArray = new List<int>();
            AddHues(SettingsAsset.defaultMemberAssets, colorHArray);
            AddHues(SettingsAsset.customMemberAssets, colorHArray);
            if (colorHArray.Count == 0) return Random.Range(0, HDimension);

            // Define the HSV Hue (H) value in the range 0 to dimension, and record all used colors' corresponding H values. Treat each pair of adjacent H values as a line segment, find the longest segment, and use its midpoint as the new H.
            // Saturation (S) and Value (V) will automatically adjust based on whether the editor skin is Dark or Light.
            // 定义HSV的H值为0-dimension，记录所有已经使用的Color对应的H。设定两个H之间为一个线段，找到长度最长的线段，并获取线段的中点作为新的H。
            // S和V会根据编辑器皮肤Dark或者Light自动适应。（只有一个色相时，环绕线段长 360，取到的正是对面的色相。）

            colorHArray.Sort();
            int min = colorHArray[0], max = colorHArray[colorHArray.Count - 1];
            int left = -1;
            int line = HDimension - max + min;
            for (int i = 0; i < colorHArray.Count - 1; i++)
            {
                int lineTemp = colorHArray[i + 1] - colorHArray[i];
                if (lineTemp > line)
                {
                    left = colorHArray[i];
                    line = lineTemp;
                }
            }

            int colorH;
            // Other line segments. 其他线段。
            if (left >= 0)
            {
                colorH = left + (int)(line * 0.5f);
            }
            // The line segment connecting the head and tail (i.e., the segment that wraps around).
            // 头尾相接的线段。
            else
            {
                colorH = max + (int)(line * 0.5f);
                if (colorH > HDimension) colorH -= HDimension;
            }

            return colorH;
        }
        
        // Add the 0–dimension hues of the chromatic member colors (deduplicated). 添加有彩色成员颜色的 0-dimension 色相（去重）。
        private static void AddHues(DebugxMemberInfoAsset[] members, List<int> hues)
        {
            if (members == null) return;
            foreach (DebugxMemberInfoAsset m in members)
            {
                Color.RGBToHSV(m.color, out float h, out float s, out _);
                if (s < AchromaticSaturation) continue;
                int hue = Mathf.FloorToInt(h * HDimension);
                if (!hues.Contains(hue)) hues.Add(hue);
            }
        }

        /// <summary>
        /// Automatically reassign colors for custom members.
        /// 为自定义成员自动重分配颜色。
        /// </summary>
        public static void AutomaticallyReassignColors()
        {
            if (SettingsAsset == null) return;

            int length = SettingsAsset.CustomMemberAssetsLength;
            if (length == 0) return;

            int step = HDimension / SettingsAsset.CustomMemberAssetsLength;
            // Start half a step past Master's hue, so no custom member sits on (or right next to) Master's color.
            // 从 Master 色相再偏移半步开始，使自定义成员不会落在 Master 颜色上（或紧挨着它）。
            int h = MasterHue + step / 2;
            for (int i = 0; i < length; i++)
            {
                ColorHSV colorHSV = new(h % HDimension * 1f / HDimension);
                SettingsAsset.customMemberAssets[i].color = Color.HSVToRGB(colorHSV.h, colorHSV.s, colorHSV.v);
                h += step;
            }
        }

        /// <summary>
        /// Reset all members' colors based on the current editor skin.
        /// 重置所有成员颜色，根据当前编辑器皮肤。
        /// </summary>
        public static bool AdaptColorByEditorSkin()
        {
            if (SettingsAsset == null) return false;

            bool change1 = ResetMembersColorByEditorSkin(SettingsAsset.defaultMemberAssets);
            bool change2 = ResetMembersColorByEditorSkin(SettingsAsset.customMemberAssets);

            return change1 || change2;
        }

        private static bool ResetMembersColorByEditorSkin(DebugxMemberInfoAsset[] debugxMemberInfoAsset)
        {
            if (debugxMemberInfoAsset == null || debugxMemberInfoAsset.Length == 0) return false;

            bool change = false;
            for (int i = 0; i < debugxMemberInfoAsset.Length; i++)
            {
                var mInfo = debugxMemberInfoAsset[i];
                Color adapted = GetMemberColorByEditorSkin(mInfo.color);
                if (adapted == mInfo.color) continue;

                change = true;
                mInfo.color = adapted;
                debugxMemberInfoAsset[i] = mInfo;
            }

            return change;
        }

        /// <summary>
        /// The color re-tinted for the current editor skin, keeping its hue. Achromatic colors (white / gray / black, e.g.
        /// Normal) have no hue — RGBToHSV reports 0, i.e. red — so they become plain white (Dark skin) / black (Light skin).
        /// 按当前编辑器皮肤重新调整后的颜色，保留色相。无彩色（白/灰/黑，如 Normal）没有色相——RGBToHSV 会报为 0 即红色——
        /// 故变为纯白（暗色皮肤）/ 纯黑（亮色皮肤）。
        /// </summary>
        public static Color GetMemberColorByEditorSkin(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            bool isDark = EditorGUIUtility.isProSkin;
            if (s < AchromaticSaturation) return isDark ? Color.white : Color.black;
            if (Mathf.Approximately(s, (isDark ? SDark : SLight)) && Mathf.Approximately(v, (isDark ? VDark : VLight))) return color;

            var colorHSV = new ColorHSV(h);
            return Color.HSVToRGB(colorHSV.h, colorHSV.s, colorHSV.v);
        }
    }
}