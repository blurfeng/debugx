using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SettingsProvider = UnityEditor.SettingsProvider;

namespace DebugxLog.Editor
{
    public static class GUILayoutEx
    {
        #region Button

        public static bool ButtonColor(string text, Color color, bool? showColor = null, string tooltip = "",
            params GUILayoutOption[] options)
        {
            if (showColor != null && showColor.Value)
                GUIUtilityEx.PushTintBg(color);
            bool press = GUILayout.Button(new GUIContent(text, tooltip), options);
            if (showColor != null && showColor.Value)
                GUIUtilityEx.PopTintBg();

            return press;
        }

        public static bool ButtonColor(Rect rect, string text, Color color, bool? showColor = null, string tooltip = "")
        {
            if (showColor != null && showColor.Value)
                GUIUtilityEx.PushTintBg(color);
            bool press = GUI.Button(rect, new GUIContent(text, tooltip));
            if (showColor != null && showColor.Value)
                GUIUtilityEx.PopTintBg();

            return press;
        }

        public static bool ButtonGreen(string text, string tooltip = "", params GUILayoutOption[] options)
        {
            GUIUtilityEx.PushTintBg(Color.green);
            bool press = GUILayout.Button(new GUIContent(text, tooltip), options);
            GUIUtilityEx.PopTintBg();

            return press;
        }

        public static bool ButtonGreen(string text, params GUILayoutOption[] options)
        {
            return ButtonGreen(text, "", options);
        }

        public static bool ButtonYellow(string text, string tooltip = "", params GUILayoutOption[] options)
        {
            GUIUtilityEx.PushTintBg(Color.yellow);
            bool press = GUILayout.Button(new GUIContent(text, tooltip), options);
            GUIUtilityEx.PopTintBg();

            return press;
        }

        public static bool ButtonYellow(string text, params GUILayoutOption[] options)
        {
            return ButtonYellow(text, "", options);
        }
        
        public static bool ButtonRed(GUIContent content, GUIStyle style = null, params GUILayoutOption[] options)
        {
            GUIUtilityEx.PushTintBg(Color.red);
            if (style == null)
                style = GUI.skin.button;
            bool press = GUILayout.Button(content, style, options);
            GUIUtilityEx.PopTintBg();

            return press;
        }

        public static bool ButtonRed(Rect rect, GUIContent content, GUIStyle style = null)
        {
            GUIUtilityEx.PushTintBg(Color.red);
            if (style == null)
                style = GUI.skin.button;
            bool press = GUI.Button(rect, content, style);
            GUIUtilityEx.PopTintBg();

            return press;
        }

        public static bool ButtonRed(Rect rect, string text, GUIStyle style = null)
        {
            return ButtonRed(rect, new GUIContent(text, ""), style);
        }

        public static bool ButtonRed(string text, string tooltip = "", params GUILayoutOption[] options)
        {
            return ButtonRed(new GUIContent(text, tooltip), null, options);
        }

        public static bool ButtonRed(string text, params GUILayoutOption[] options)
        {
            return ButtonRed(text, "", options);
        }

        public static bool ButtonRed(string text, GUIStyle style = null, params GUILayoutOption[] options)
        {
            return ButtonRed(new GUIContent(text, ""), style, options);
        }

        public static bool ButtonCyan(string text, string tooltip = "", params GUILayoutOption[] options)
        {
            GUIUtilityEx.PushTintBg(Color.cyan);
            bool press = GUILayout.Button(new GUIContent(text, tooltip), options);
            GUIUtilityEx.PopTintBg();

            return press;
        }

        public static bool ButtonCyan(string text, params GUILayoutOption[] options)
        {
            return ButtonCyan(text, "", options);
        }

        public static bool ButtonGray(string text, string tooltip = "", params GUILayoutOption[] options)
        {
            GUIUtilityEx.PushTintBg(Color.gray);
            bool press = GUILayout.Button(new GUIContent(text, tooltip), options);
            GUIUtilityEx.PopTintBg();

            return press;
        }

        public static bool ButtonGray(string text, params GUILayoutOption[] options)
        {
            return ButtonGray(text, "", options);
        }

        #endregion

        public static bool Toggle(string label, string tooltip, bool value, params GUILayoutOption[] options)
        {
            return EditorGUILayout.Toggle(new GUIContent(label, tooltip), value, options);
        }

        public static bool ToggleUndo(string label, string tooltip, bool value, Object undoObject, string undoName,
            params GUILayoutOption[] options)
        {
            bool change = Toggle(label, tooltip, value, options);
            if (change != value)
            {
                Undo.RecordObject(undoObject, undoName);
            }

            return change;
        }

        public static bool ToggleLeft(string label, string tooltip, bool value, params GUILayoutOption[] options)
        {
            return EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), value, options);
        }

        public static bool ToggleLeftUndo(string label, string tooltip, bool value, Object undoObject, string undoName,
            params GUILayoutOption[] options)
        {
            bool change = ToggleLeft(label, tooltip, value, options);
            if (change != value)
            {
                Undo.RecordObject(undoObject, undoName);
            }

            return change;
        }

        public static int IntField(string label, string tooltip, int value, params GUILayoutOption[] options)
        {
            return EditorGUILayout.IntField(new GUIContent(label, tooltip), value, options);
        }

        public static int IntFieldUndo(string label, string tooltip, int value, Object undoObject, string undoName,
            params GUILayoutOption[] options)
        {
            int change = IntField(label, tooltip, value, options);
            if (change != value)
            {
                Undo.RecordObject(undoObject, undoName);
            }

            return change;
        }

        public static string TextField(string label, string tooltip, string value, params GUILayoutOption[] options)
        {
            return EditorGUILayout.TextField(new GUIContent(label, tooltip), value, options);
        }

        public static string TextFieldUndo(string label, string tooltip, string value, Object undoObject,
            string undoName, params GUILayoutOption[] options)
        {
            string change = TextField(label, tooltip, value, options);
            if (change != value)
            {
                Undo.RecordObject(undoObject, undoName);
            }

            return change;
        }

        public static Color ColorField(string label, string tooltip, Color value, params GUILayoutOption[] options)
        {
            return EditorGUILayout.ColorField(new GUIContent(label, tooltip), value, options);
        }

        public static Color ColorFieldUndo(string label, string tooltip, Color value, Object undoObject,
            string undoName, params GUILayoutOption[] options)
        {
            Color change = ColorField(label, tooltip, value, options);
            if (change != value)
            {
                Undo.RecordObject(undoObject, undoName);
            }

            return change;
        }
    }

    /// <summary>
    /// GUI extension tool. It includes the GUI drawing class taken from the AstarPathfindingProject plugin.
    /// GUI扩展工具。有从AstarPathfindingProject插件拿过来的GUI绘制类
    /// </summary>
    public static class GUIUtilityEx
    {
        private static readonly Stack<Color> _colorsBg = new Stack<Color>();
        private static readonly Stack<Color> _colors = new Stack<Color>();

        public static void PushTintBg(Color tint)
        {
            _colorsBg.Push(GUI.color);
            GUI.backgroundColor = tint;
        }

        public static void PopTintBg()
        {
            GUI.backgroundColor = _colorsBg.Pop();
        }

        public static void PushTint(Color tint)
        {
            _colors.Push(GUI.color);
            GUI.color = tint;
        }

        public static void PopTint()
        {
            GUI.color = _colors.Pop();
        }
    }

    /// <summary>
    /// Switchable hidden area GUI.
    /// 可开关隐藏区域GUI。
    /// Sequence of invocation:
    /// 调用顺序:
    /// - Begin
    /// - Header
    /// - if(BeginFade)
    /// - { customize content 自定义内容 }
    /// - End
    /// </summary>
    public class FadeArea
    {
        private Rect _lastRect;
        private float _value;
        private float _lastUpdate;
        private readonly GUIStyle _labelStyle;
        private readonly GUIStyle _areaStyle;
        private bool _visible;
        private readonly EditorWindow _editorWindow;
        private readonly SettingsProvider _settingsProvider;
        private readonly bool _immediately;
        private bool _changedCached;

        private readonly float _beginSpace;

        // Exclude the "Header" from the "GUI.changed" list. // 将点击Header排除出GUI.changed。
        private readonly bool _changedExcludeHeaderClick;

        /// <summary>
        /// Is this area open.
        /// This is not the same as if any contents are visible, use <see cref="BeginFade"/> for that.
        /// </summary>
        private bool _open;

        /// <summary>Animate dropdowns when they open and close</summary>
        private const bool FancyEffects = true;

        private const float AnimationSpeed = 100f;

        public FadeArea(EditorWindow editor, GUIStyle areaStyle, GUIStyle labelStyle, bool open, float beginSpace = 1f,
            bool immediately = false, bool changedExcludeHeaderClick = true)
        {
            this._editorWindow = editor;

            this._areaStyle = areaStyle;
            this._labelStyle = labelStyle;
            _visible = this._open = open;
            _value = open ? 1 : 0;
            this._beginSpace = beginSpace;
            this._immediately = immediately;
            this._changedExcludeHeaderClick = changedExcludeHeaderClick;
        }

        public FadeArea(EditorWindow editor, bool open = false, float beginSpace = 1f, bool immediately = false,
            bool changedExcludeHeaderClick = true)
        {
            this._editorWindow = editor;

            this._areaStyle = GUIStyleEx.AreaStyle1;
            this._labelStyle = GUIStyleEx.LabelStyleFadeAreaHeader;
            _visible = this._open = open;
            _value = open ? 1 : 0;
            this._beginSpace = beginSpace;
            this._immediately = immediately;
            this._changedExcludeHeaderClick = changedExcludeHeaderClick;
        }

        public FadeArea(SettingsProvider settingsProvider, GUIStyle areaStyle, GUIStyle labelStyle, bool open,
            float beginSpace = 1f, bool immediately = false, bool changedExcludeHeaderClick = true)
        {
            this._settingsProvider = settingsProvider;

            this._areaStyle = areaStyle;
            this._labelStyle = labelStyle;
            _visible = this._open = open;
            _value = open ? 1 : 0;
            this._beginSpace = beginSpace;
            this._immediately = immediately;
            this._changedExcludeHeaderClick = changedExcludeHeaderClick;
        }

        public FadeArea(SettingsProvider settingsProvider, bool open = false, float beginSpace = 1f,
            bool immediately = false, bool changedExcludeHeaderClick = true)
        {
            this._settingsProvider = settingsProvider;

            this._areaStyle = GUIStyleEx.AreaStyle1;
            this._labelStyle = GUIStyleEx.LabelStyleFadeAreaHeader;
            _visible = this._open = open;
            _value = open ? 1 : 0;
            this._beginSpace = beginSpace;
            this._immediately = immediately;
            this._changedExcludeHeaderClick = changedExcludeHeaderClick;
        }

        void Tick()
        {
            if (Event.current.type == EventType.Repaint)
            {
                float deltaTime = Time.realtimeSinceStartup - _lastUpdate;

                // Right at the start of a transition the deltaTime will
                // not be reliable, so use a very small value instead
                // until the next repaint
                if (_value == 0f || Mathf.Approximately(_value, 1f)) deltaTime = 0.001f;
                deltaTime = Mathf.Clamp(deltaTime, 0.00001F, 0.1F);

                // Larger regions fade slightly slower
                deltaTime /= Mathf.Sqrt(Mathf.Max(_lastRect.height, 100));

                _lastUpdate = Time.realtimeSinceStartup;


                float targetValue = _open ? 1F : 0F;
                if (!Mathf.Approximately(targetValue, _value))
                {
                    _value += deltaTime * AnimationSpeed * Mathf.Sign(targetValue - _value);
                    _value = Mathf.Clamp01(_value);

                    _settingsProvider?.Repaint();
                    _editorWindow?.Repaint();

                    // if (!FancyEffects)
                    // {
                    //     _value = targetValue;
                    // }
                }
                else
                {
                    _value = targetValue;
                }
            }
        }

        public void Begin()
        {
            _lastRect = _areaStyle != null
                ? EditorGUILayout.BeginVertical(_areaStyle)
                : EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(_beginSpace);
        }

        public bool Header(string label, string tooltip = "")
        {
            return Header(label, ref _open, tooltip);
        }

        public bool Header(string label, string tooltip, int width)
        {
            return Header(label, ref _open, tooltip, width);
        }

        public bool Header(string label, int width)
        {
            return Header(label, ref _open, "", width);
        }

        /// <summary>
        /// 页眉。
        /// </summary>
        /// <param name="label"></param>
        /// <param name="open"></param>
        /// <param name="tooltip"></param>
        /// <param name="width"></param>
        /// <returns>Header是否被点击产生开关变化</returns>
        public bool Header(string label, ref bool open, string tooltip = "", int width = -1)
        {
            if (_changedExcludeHeaderClick)
                _changedCached = GUI.changed;

            bool press;
            if (width > 0)
            {
                press = GUILayout.Button(new GUIContent(label, tooltip), _labelStyle, GUILayout.Width(width));
            }
            else
            {
                press = GUILayout.Button(new GUIContent(label, tooltip), _labelStyle);
            }

            if (press)
            {
                open = !open;
                _settingsProvider?.Repaint();
                _editorWindow?.Repaint();
            }

            this._open = open;
            if (_immediately) _value = open ? 1f : 0f;

            if (_changedExcludeHeaderClick && !_changedCached) GUI.changed = _changedCached; //开关FadeArea排除Changed判断

            return press;
        }

        /// <summary>Hermite spline interpolation</summary>
        static float Hermite(float start, float end, float value)
        {
            return Mathf.Lerp(start, end, value * value * (3.0f - 2.0f * value));
        }

        public bool BeginFade()
        {
            var hermite = Hermite(0, 1, _value);

            _visible = EditorGUILayout.BeginFadeGroup(hermite);
            GUIUtilityEx.PushTintBg(new Color(1, 1, 1, hermite));
            Tick();

            // Another vertical group is necessary to work around
            // a kink of the BeginFadeGroup implementation which
            // causes the padding to change when value!=0 && value!=1
            EditorGUILayout.BeginVertical();

            return _visible;
        }

        public void End()
        {
            EditorGUILayout.EndVertical();

            if (_visible)
            {
                // Some space that cannot be placed in the GUIStyle unfortunately
                GUILayout.Space(4);
            }

            EditorGUILayout.EndFadeGroup();
            EditorGUILayout.EndVertical();
            GUIUtilityEx.PopTintBg();
        }
    }
}