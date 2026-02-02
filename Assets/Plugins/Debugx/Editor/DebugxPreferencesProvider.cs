using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Editor
{
    static class DebugxPreferencesProvider
    {
        private static SettingsProvider _settingsProvider;
        private static DebugxProjectSettings Settings => DebugxProjectSettings.Instance;
        private static DebugxProjectSettingsAsset SettingsAsset => DebugxProjectSettingsAsset.Instance;
        private static Dictionary<int, bool> MemberEnableDefaultDic => DebugxStaticData.MemberEnableDefaultDicPrefs;
        private static FadeArea _faMemberEnableSetting;
        private static bool _isInitGUI;
        private static ReorderableList _membersList;

        [SettingsProvider]
        public static SettingsProvider DebugxPreferencesProviderCreate()
        {
            if (_settingsProvider == null)
            {
                _isInitGUI = false;

                _settingsProvider = new SettingsProvider("Preferences/Debugx", SettingsScope.User)
                {
                    label = "Debugx",
                    activateHandler = Enable,
                    guiHandler = Draw,
                    deactivateHandler = Disable,
                };
            }

            return _settingsProvider;
        }

        private static void Enable(string searchContext, VisualElement rootElement)
        {
            DebugxProjectSettingsAssetEditor.CheckDebugxProjectSettingsAsset();
        }

        private static void Disable()
        {
        }

        private static void Draw(string searchContext)
        {
            if (SettingsAsset == null) return;

            if (!_isInitGUI)
            {
                _isInitGUI = true;

                _faMemberEnableSetting = new FadeArea(_settingsProvider, DebugxStaticData.FaMemberEnableSettingOpen);
                _membersList = new ReorderableList(Settings.members, typeof(DebugxMemberInfo), false, true, false, false)
                {
                    drawHeaderCallback = DrawMembersHeader,
                    drawElementCallback = DrawMembersElement,
                    elementHeight = 20f,
                };
            }

            string defineSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (!defineSymbols.Contains("DEBUG_X"))
            {
                EditorGUILayout.HelpBox(
                    DebugxStaticData.IsChineseSimplified
                        ? "当前项目的Standalone平台未配置宏\"DEBUG_X\",Debugx不会进行工作。"
                        : "The current project’s Standalone platform does not have the macro \"DEBUG_X\" configured, so Debugx will not function.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                DebugxStaticData.IsChineseSimplified
                    ? "此处为用户设置，在 UNITY_EDITOR 编辑器时，一些参数会优先使用 Preferences 用户设置。用户设置不会影响项目的其他人。"
                    : "This section is for user settings. When in the UNITY_EDITOR, some parameters will prioritize using the Preferences user settings. User settings do not affect other people in the project.",
                MessageType.Info);

            EditorGUI.BeginDisabledGroup(!DebugxStaticData.CanResetPreferences);
            if (GUILayout.Button("Reset to Default"))
            {
                if (EditorUtility.DisplayDialog(
                        "Reset to Default",
                        DebugxStaticData.IsChineseSimplified
                            ? "确认要重置到默认设置吗？"
                            : "Are you sure you want to reset to the default settings?",
                        "Ok", "Cancel"))
                {
                    ResetPreferences();
                    Apply();
                }
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Toggle", GUIStyleEx.TitleStyle2);

            EditorGUI.BeginChangeCheck();

            DebugxStaticData.EnableLogDefaultPrefs = GUILayoutEx.Toggle("EnableLog Default",
                DebugxStaticData.ToolTipEnableLogDefault, DebugxStaticData.EnableLogDefaultPrefs);
            DebugxStaticData.EnableLogMemberDefaultPrefs = GUILayoutEx.Toggle("EnableLogMember Default",
                DebugxStaticData.ToolTipEnableLogMemberDefault, DebugxStaticData.EnableLogMemberDefaultPrefs);
            DebugxStaticData.AllowUnregisteredMember = GUILayoutEx.Toggle("AllowUnregisteredMember",
                DebugxStaticData.ToolTipAllowUnregisteredMember, DebugxStaticData.AllowUnregisteredMember);
            DebugxStaticData.LogThisKeyMemberOnlyDefaultPrefs = GUILayoutEx.IntField("LogThisKeyMemberOnly Default",
                DebugxStaticData.ToolTipLogThisKeyMemberOnlyDefault,
                DebugxStaticData.LogThisKeyMemberOnlyDefaultPrefs);

            EditorGUILayout.Space();

            _faMemberEnableSetting.Begin();
            _faMemberEnableSetting.Header("Members Enable");
            DebugxStaticData.FaMemberEnableSettingOpen = _faMemberEnableSetting.BeginFade();
            if (DebugxStaticData.FaMemberEnableSettingOpen)
            {
                if (Settings.members != null && Settings.members.Length > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Enable All")) SetMemberEnableDefaultDicAll(true);
                    if (GUILayout.Button("Disable All")) SetMemberEnableDefaultDicAll(false);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();

                    _membersList.DoLayoutList();
                }
                else
                    EditorGUILayout.LabelField(DebugxStaticData.IsChineseSimplified
                        ? "没有配置任何成员。"
                        : "No members have been configured.");
            }

            _faMemberEnableSetting.End();


            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log Output", GUIStyleEx.TitleStyle2);
            DebugxStaticData.LogOutputPrefs = GUILayoutEx.Toggle("EnableLogOutput", DebugxStaticData.ToolTipLogOutput,
                DebugxStaticData.LogOutputPrefs);
            EditorGUI.BeginDisabledGroup(!DebugxStaticData.LogOutputPrefs);
            DebugxStaticData.EnableLogStackTracePrefs = GUILayoutEx.Toggle("EnableLogStackTrace",
                DebugxStaticData.ToolTipEnableLogStackTrace, DebugxStaticData.EnableLogStackTracePrefs);
            DebugxStaticData.EnableWarningStackTracePrefs = GUILayoutEx.Toggle("EnableWarningStackTrace",
                DebugxStaticData.ToolTipEnableWarningStackTrace, DebugxStaticData.EnableWarningStackTracePrefs);
            DebugxStaticData.EnableErrorStackTracePrefs = GUILayoutEx.Toggle("EnableErrorStackTrace",
                DebugxStaticData.ToolTipEnableErrorStackTrace, DebugxStaticData.EnableErrorStackTracePrefs);
            DebugxStaticData.RecordAllNonDebugxLogsPrefs = GUILayoutEx.Toggle("RecordAllNonDebugxLogs",
                DebugxStaticData.ToolTipRecordAllNonDebugxLogs, DebugxStaticData.RecordAllNonDebugxLogsPrefs);
            DebugxStaticData.DrawLogToScreenPrefs = GUILayoutEx.Toggle("DrawLogToScreen",
                DebugxStaticData.ToolTipDrawLogToScreen, DebugxStaticData.DrawLogToScreenPrefs);
            EditorGUI.BeginDisabledGroup(!DebugxStaticData.DrawLogToScreenPrefs);
            DebugxStaticData.RestrictDrawLogCountPrefs = GUILayoutEx.Toggle("RestrictDrawLogCount",
                DebugxStaticData.ToolTipRestrictDrawLogCount, DebugxStaticData.RestrictDrawLogCountPrefs);
            DebugxStaticData.MaxDrawLogsPrefs = GUILayoutEx.IntField("MaxDrawLogs", DebugxStaticData.ToolTipMaxDrawLogs,
                DebugxStaticData.MaxDrawLogsPrefs);
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();

            // Still call the save method of DebugxProjectSettingsAsset, which will prioritize using Prefs when in UNITY_EDITOR.
            // 还是调用DebugxProjectSettingsAsset的保存，里面会判断在UNITY_EDITOR时优先使用Prefs。
            if (EditorGUI.EndChangeCheck())
            {
                DebugxStaticData.CanResetPreferences = true;
                Apply();
            }

            EditorGUILayout.Space(16f);
        }

        /// <summary>
        /// Reset user settings.
        /// 重置用户设置。
        /// </summary>
        private static void ResetPreferences()
        {
            if (!DebugxStaticData.CanResetPreferences) return;
            DebugxStaticData.ResetPreferences();
            DebugxStaticData.CanResetPreferences = false;
        }

        private static void DrawMembersHeader(Rect rect)
        {
            var buttonRect = rect;
            buttonRect.x += rect.width - 140f;
            buttonRect.width = 140f;
            buttonRect.y += 1f;
            buttonRect.height -= 2f;

            var titleRect = rect;
            titleRect.width -= buttonRect.width;
            GUI.Label(titleRect, new GUIContent(
                "Members Enable",
                DebugxStaticData.IsChineseSimplified
                    ? "此处仅能配置成员的默认开关，详细成员配置在 Project Settings 中设置。在重置到默认状态时，成员开关将恢复到成员配置中的 Enable Default 的值。"
                    : "Only the default switches for members can be configured here; detailed member configurations are set in Project Settings. When resetting to default, member switches will revert to the \"Enable Default\" values in the member configurations."));

            EditorGUI.BeginDisabledGroup(!DebugxStaticData.CanResetPreferencesMembers);
            if (GUI.Button(buttonRect, new GUIContent("Reset",
                    DebugxStaticData.IsChineseSimplified
                        ? "重置到和目前成员配置中的 Enable Default 值一致。"
                        : "Reset to match the current member configurations' Enable Default values.")))
            {
                // Remove focus. 移除焦点。
                GUI.FocusControl("");
                DebugxStaticData.ResetPreferencesMembers();
                DebugxStaticData.CanResetPreferencesMembers = false;
                Apply();
            }

            EditorGUI.EndDisabledGroup();
        }

        private static void DrawMembersElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index >= Settings.members.Length) return;

            const float idWidth = 90f;
            const float enableWidth = 30f;
            const float interval = 5f;

            var info = Settings.members[index];
            bool enable = MemberEnableDefaultDic.ContainsKey(info.key)
                ? MemberEnableDefaultDic[info.key]
                : Debugx.MemberIsEnable(info.key);
            EditorGUI.BeginChangeCheck();

            Rect idRect = rect;
            idRect.width = idWidth;
            GUI.Label(idRect, $"[{info.key}]");

            Rect signatureRect = rect;
            signatureRect.x += idWidth + interval;
            signatureRect.width = rect.width - idWidth - interval - enableWidth - interval;
            GUI.Label(signatureRect, $"{(string.IsNullOrEmpty(info.signature) ? "Member" : info.signature)}");

            Rect enableRect = rect;
            enableRect.x = signatureRect.x + signatureRect.width + interval;
            enableRect.width = enableWidth;

            bool newEnable = EditorGUI.Toggle(enableRect, enable);
            if (newEnable != enable)
            {
                SetMemberEnableDefaultDic(info.key, newEnable);

                DebugxStaticData.SaveMemberEnableDefaultDicPrefs();
                DebugxStaticData.CanResetPreferencesMembers = true;
                Apply();
            }
        }

        private static void SetMemberEnableDefaultDic(int key, bool enable)
        {
            MemberEnableDefaultDic[key] = enable;
        }

        private static void SetMemberEnableDefaultDicAll(bool enable)
        {
            for (int i = 0; i < Settings.members.Length; i++)
            {
                SetMemberEnableDefaultDic(Settings.members[i].key, enable);
            }

            DebugxStaticData.SaveMemberEnableDefaultDicPrefs();
            DebugxStaticData.CanResetPreferencesMembers = true;
        }

        private static void Apply()
        {
            // In ApplyTo, it checks if running in the Editor to use user preferences instead of DebugxProjectSettingsAsset configuration.
            // ApplyTo中会判断如果在Editor就使用用户偏好设置，而不是使用DebugxProjectSettingsAsset配置。
            if (SettingsAsset != null)
                SettingsAsset.ApplyTo(DebugxProjectSettings.Instance);
        }
    }
}