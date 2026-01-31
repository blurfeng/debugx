using UnityEditor;
using UnityEngine;

namespace DebugxLog.Editor
{
    public class GUIStyleEx
    {
        public static bool IsDarkSkin => EditorGUIUtility.isProSkin;
        
        private static GUIStyleEx Instance
        {
            get
            {
                // 根据当前皮肤类型缓存样式实例。修改皮肤后会重新创建实例。
                if (_style == null || _editorSkinCached != (EditorGUIUtility.isProSkin ? 1 : 2))
                {
                    _editorSkinCached = EditorGUIUtility.isProSkin ? 1 : 2;
                    _style = new GUIStyleEx();
                }

                return _style;
            }
        }
        private static GUIStyleEx _style;
        private static int _editorSkinCached = -1;

        /// <summary>
        /// 标题风格1级。
        /// </summary>
        public static GUIStyle TitleStyle1 => Instance._titleStyle1;
        private readonly GUIStyle _titleStyle1;

        /// <summary>
        /// 标题风格2级。
        /// </summary>
        public static GUIStyle TitleStyle2 => Instance._titleStyle2;
        private readonly GUIStyle _titleStyle2;

        /// <summary>
        /// 标题风格3级。
        /// </summary>
        public static GUIStyle TitleStyle3 => Instance._titleStyle3;
        private readonly GUIStyle _titleStyle3;

        /// <summary>
        /// 隐藏空间标题。
        /// </summary>
        public static GUIStyle AreaStyle1 => Instance._areaStyle1;
        private readonly GUIStyle _areaStyle1;

        /// <summary>
        /// 隐藏空间标题。
        /// </summary>
        public static GUIStyle LabelStyleFadeAreaHeader => Instance._labelStyleFadeAreaHeader;
        private readonly GUIStyle _labelStyleFadeAreaHeader;
        
        /// <summary>
        /// 背景纹理。
        /// </summary>
        public Texture2D BackgroundTex => Instance._backgroundTex;
        private readonly Texture2D _backgroundTex;

        #region Colors

        public static Color ColorGg = IsDarkSkin ? new(0.2509f, 0.2509f, 0.2509f) : new(0.8117f, 0.8117f, 0.8117f);

        #endregion

        private GUIStyleEx()
        {
            Color colorDark = new(0.1f, 0.1f, 0.1f);
            Color colorLightGray = new(0.8784f, 0.8784f, 0.8784f);
            
            bool isDark = EditorGUIUtility.isProSkin;

            _backgroundTex = new Texture2D(32, 32);
            Color colorBg = isDark ? new(0.2509f, 0.2509f, 0.2509f) : new(0.8117f, 0.8117f, 0.8117f);
            Color colorLine = isDark ? new(0.1372f, 0.1372f, 0.1372f) : new(0.6627f, 0.6627f, 0.6627f);
            Color colorSideRound = isDark ? new(0.1764f, 0.1764f, 0.1764f) : new(0.7137f, 0.7137f, 0.7137f);
            Color colorNone = new(1f, 1f, 1f, 0f);
            int width = _backgroundTex.width;
            int height = _backgroundTex.height;
            for (int w = 0; w < width; w++)
            {
                for (int h = 0; h < height; h++)
                {
                    if (w == 0 && (h <= 0 || h >= height - 1)
                        || w == width - 1 && (h <= 0 || h >= height - 1))
                    {
                        _backgroundTex.SetPixel(w, h, colorNone);
                    }
                    else if ((w == 0 || w == 1 || w == width - 1 || w == width - 2) && (h == 0 || h == 1 || h == height - 2 || h == height - 1))
                    {
                        _backgroundTex.SetPixel(w, h, colorSideRound);
                    }
                    else if (w == 0 || h == 0 || w == width - 1 || h == height - 1)
                    {
                        _backgroundTex.SetPixel(w, h, colorLine);
                    }
                    else
                        _backgroundTex.SetPixel(w, h, colorBg);
                }
            }
            _backgroundTex.Apply();

            // 一级标题风格。
            _titleStyle1 = new GUIStyle
            {
                fontSize = 16,
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState
                {
                    textColor = IsDarkSkin ? colorLightGray : colorDark
                }
            };

            // 二级标题风格。
            _titleStyle2 = new GUIStyle
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState
                {
                    textColor = IsDarkSkin ? colorLightGray : colorDark
                }
            };

            // 三级标题风格。
            _titleStyle3 = new GUIStyle
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState
                {
                    textColor = IsDarkSkin ? colorLightGray : colorDark
                }
            };

            _labelStyleFadeAreaHeader = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };

            _areaStyle1 = new GUIStyle(GUI.skin.button)
            {
                normal =
                {
                    background = _backgroundTex
                }
            };
        }
    }
}