using UnityEditor;
using UnityEngine;

namespace DebugxLog.Editor
{
    public class GUIStylEx
    {
        /// <summary>
        /// 是否是黑色皮肤
        /// </summary>
        public static bool IsDarkSkin => _editorSkinCached == 1;
        private static int _editorSkinCached = -1;
        
        public static GUIStylEx Get
        {
            get
            {
                if (_style == null || _editorSkinCached != (EditorGUIUtility.isProSkin ? 1 : 2))
                {
                    _editorSkinCached = EditorGUIUtility.isProSkin ? 1 : 2;
                    _style = new GUIStylEx();
                }

                return _style;
            }
        }
        private static GUIStylEx _style;

        /// <summary>
        /// 标题风格1级。
        /// </summary>
        public GUIStyle TitleStyle1 { get; private set; }

        /// <summary>
        /// 标题风格2级。
        /// </summary>
        public GUIStyle TitleStyle2 { get; private set; }

        /// <summary>
        /// 标题风格3级。
        /// </summary>
        public GUIStyle TitleStyle3 { get; private set; }

        /// <summary>
        /// 隐藏空间标题。
        /// </summary>
        public GUIStyle AreaStyle1 { get; private set; }

        /// <summary>
        /// 隐藏空间标题。
        /// </summary>
        public GUIStyle LabelStyleFadeAreaHeader { get; private set; }

        private Texture2D _backgroundTex;
        private Texture2D BackgroundTex
        {
            get
            {
                if (!_backgroundTex)
                {
                    bool isDark = EditorGUIUtility.isProSkin;

                    _backgroundTex = new Texture2D(32, 32);
                    Color color = isDark ? new(0.2509f, 0.2509f, 0.2509f) : new(0.8117f, 0.8117f, 0.8117f);
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
                                _backgroundTex.SetPixel(w, h, color);
                        }
                    }
                    _backgroundTex.Apply();
                }

                return _backgroundTex;
            }
        }

        private GUIStylEx()
        {
            Color colorDark = new(0.1f, 0.1f, 0.1f);
            Color colorLightGray = new(0.8784f, 0.8784f, 0.8784f);

            // 一级标题风格。
            TitleStyle1 = new GUIStyle
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
            TitleStyle2 = new GUIStyle
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
            TitleStyle3 = new GUIStyle
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState
                {
                    textColor = IsDarkSkin ? colorLightGray : colorDark
                }
            };

            LabelStyleFadeAreaHeader = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };

            AreaStyle1 = new GUIStyle(GUI.skin.button)
            {
                normal =
                {
                    background = BackgroundTex
                }
            };
        }
    }
}