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

        private static bool _isDarkSkin;

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
                s = _isDarkSkin ? SDark : SLight;
                v = _isDarkSkin ? VDark : VLight;
            }
        }

        public static void OnInitializeOnLoadMethod()
        {
            DebugxProjectSettingsAsset.GetRandomColorForMember += GetRandomColorForMember;
            DebugxProjectSettingsAsset.GetNormalMemberColor += GetNormalMemberColor;
            DebugxProjectSettingsAsset.GetMasterMemberColor += GetMasterMemberColor;
            _isDarkSkin = EditorGUIUtility.isProSkin;
        }

        public static void OnQuit()
        {
            DebugxProjectSettingsAsset.GetRandomColorForMember -= GetRandomColorForMember;
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
            return _isDarkSkin ? Color.white : Color.black;
        }

        private static Color GetMasterMemberColor()
        {
            ColorHSV colorHSV = new(5f / HDimension);

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

            int useLength = SettingsAsset.CustomMemberAssetsLength;
            if (useLength == 0) return Random.Range(0, 361);
            else if (useLength == 1)
            {
                int num = GetColorH360(SettingsAsset.customMemberAssets[0].color);
                num += 180;
                if (num > HDimension) num -= HDimension;
                return num;
            }
            else if (useLength == 2)
            {
                int num1 = GetColorH360(SettingsAsset.customMemberAssets[0].color);
                int num2 = GetColorH360(SettingsAsset.customMemberAssets[1].color);
                bool compare = num1 < num2;
                int left2 = compare ? num1 : num2;
                int right2 = compare ? num2 : num1;
                int line1 = right2 - left2;
                if (line1 > 180)
                {
                    return left2 + Mathf.FloorToInt(line1 * 0.5f);
                }
                else
                {
                    int num = right2 + Mathf.FloorToInt((HDimension - line1) * 0.5f);
                    if (num > HDimension) num -= HDimension;
                    return num;
                }
            }

            // Define the HSV Hue (H) value in the range 0 to dimension, and record all used colors' corresponding H values. Treat each pair of adjacent H values as a line segment, find the longest segment, and use its midpoint as the new H.
            // Saturation (S) and Value (V) will automatically adjust based on whether the editor skin is Dark or Light.
            // 定义HSV的H值为0-dimension，记录所有已经使用的Color对应的H。设定两个H之间为一个线段，找到长度最长的线段，并获取线段的中点作为新的H。
            // S和V会根据编辑器皮肤Dark或者Light自动适应。

            List<int> colorHArray = new List<int>(useLength);
            for (int i = 0; i < useLength; i++)
            {
                int num = GetColorH360(SettingsAsset.customMemberAssets[i].color);
                if (colorHArray.Count == 0 || !colorHArray.Contains(num)) colorHArray.Add(num);
            }

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
        
        /// <summary>
        /// Get the Hue (H) value of a color in HSV and map it to the 0–dimension range.
        /// 获取Color对应的HSV的H值，并映射到0-dimension区间。
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private static int GetColorH360(Color color)
        {
            Color.RGBToHSV(color, out float h, out _, out _);
            return Mathf.FloorToInt(h * HDimension);
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
            int h = 0;
            for (int i = 0; i < length; i++)
            {
                ColorHSV colorHSV = new(h * 1f / HDimension);
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

            bool isDark = EditorGUIUtility.isProSkin;
            bool change = false;
            int length = debugxMemberInfoAsset.Length;

            for (int i = 0; i < length; i++)
            {
                var mInfo = debugxMemberInfoAsset[i];

                Color color = mInfo.color;
                Color.RGBToHSV(color, out float h, out float s, out float v);

                if (Mathf.Approximately(s, (isDark ? SDark : SLight)) && Mathf.Approximately(v, (isDark ? VDark : VLight))) continue;
                change = true;

                var colorHSV = new ColorHSV(h);
                mInfo.color = Color.HSVToRGB(colorHSV.h, colorHSV.s, colorHSV.v);
                debugxMemberInfoAsset[i] = mInfo;
            }

            return change;
        }

        public static Color GetMemberColorByEditorSkin(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            bool isDark = EditorGUIUtility.isProSkin;
            if (Mathf.Approximately(s, (isDark ? SDark : SLight)) && Mathf.Approximately(v, (isDark ? VDark : VLight))) return color;

            var colorHSV = new ColorHSV(h);
            color = Color.HSVToRGB(colorHSV.h, colorHSV.s, colorHSV.v);
            return color;
        }
    }
}