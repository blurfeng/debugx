﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Editor
{
    static class DebugxProjectSettingsProvider
    {
        private static SettingsProvider settingsProvider;
        private static DebugxProjectSettingsAsset SettingsAsset => DebugxProjectSettingsAsset.Instance;
        private static readonly List<FadeArea> memberInfosFadeAreaPool = new();
        private static FadeArea faMemberConfigSetting;
        private static bool isInitGUI;
        private static bool assetIsDirty;

        private static bool FadeAreaHeaderIsDirty
        {
            set
            {
                // Dirty flag can only be set to true.
                // 标脏只能设为true。
                if (value)
                {
                    fadeAreaHeaderIsDirty = true;
                }
            }
        }

        private static bool fadeAreaHeaderIsDirty;

        [SettingsProvider]
        public static SettingsProvider DebugxProjectSettingsProviderCreate()
        {
            if (settingsProvider == null)
            {
                isInitGUI = false;

                settingsProvider = new SettingsProvider("Project/Debugx", SettingsScope.Project)
                {
                    label = "Debugx",
                    activateHandler = Enable,
                    guiHandler = Draw,
                    deactivateHandler = Disable,
                };
            }

            return settingsProvider;
        }

        private static void Enable(string searchContext, VisualElement rootElement)
        {
            DebugxProjectSettingsAssetEditor.CheckDebugxProjectSettingsAsset();

            DebugxStaticDataEditor.OnAutoSaveChange.Bind(OnAutoSaveChange);
        }

        private static void Disable()
        {
            Apply();

            DebugxStaticDataEditor.OnAutoSaveChange.Unbind(OnAutoSaveChange);
        }

        private static void Draw(string searchContext)
        {
            if (SettingsAsset == null) return;

            if (!isInitGUI)
            {
                // Some initialization involving GUI classes must be called within OnGUI.
                // 一些初始化内容调用到GUI类，必须在OnGUI内调用。
                isInitGUI = true;
                ResetWindowData();
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
                    ? "此处为项目设置，项目设置会影响所有人的项目和打包后的包体软件。\n如果你仅想对自己的项目做一些本地化的设置，请在 Preferences/Debugx 用户设置中配置。"
                    : "This section is for project settings, which affect everyone’s project and the packaged build software.\nIf you only want to make some local settings for your own project, please configure them in Preferences/Debugx user settings.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();

            DebugxStaticDataEditor.AutoSave = GUILayoutEx.Toggle(
                "Auto Save Asset",
                DebugxStaticData.IsChineseSimplified
                    ? "自动保存配置资源，自动保存时在修改内容时会有卡顿。"
                    : "Automatically save configuration assets. There may be a lag during automatic saving when content is being modified.",
                DebugxStaticDataEditor.AutoSave);
            EditorGUI.BeginDisabledGroup(!assetIsDirty);
            if (GUILayoutEx.ButtonGreen("Save Asset")) Apply();
            EditorGUI.EndDisabledGroup();

            if (GUILayoutEx.ButtonRed("Reset to Default"))
            {
                if (EditorUtility.DisplayDialog(
                        "Reset To Default",
                        DebugxStaticData.IsChineseSimplified
                            ? "确认要重置到默认设置吗？\n重置并不会清空Member成员配置，仅将成员配置的部分字段重置。"
                            : "Are you sure you want to reset to the default settings?\nResetting will not clear the Member configurations; it will only reset certain fields within the member configurations.",
                        "Ok", "Cancel"))
                {
                    Undo.RecordObject(SettingsAsset, "ResetToDefault");
                    ResetProjectSettings();
                    SaveCheck();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Config Settings", GUIStyleEx.TitleStyle3);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("", SettingsAsset, typeof(DebugxProjectSettingsAsset), false);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            // Confirm whether any parameters have been modified.
            // 确认是否修改任何参数。
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Toggle", GUIStyleEx.TitleStyle2);

            SettingsAsset.enableLogDefault = ToggleUndo(
                "EnableLog Default", DebugxStaticData.ToolTipEnableLogDefault, SettingsAsset.enableLogDefault);
            SettingsAsset.enableLogMemberDefault = ToggleUndo(
                "EnableLogMember Default", DebugxStaticData.ToolTipEnableLogMemberDefault, SettingsAsset.enableLogMemberDefault);
            SettingsAsset.allowUnregisteredMember = ToggleUndo(
                "AllowUnregisteredMember", DebugxStaticData.ToolTipAllowUnregisteredMember, SettingsAsset.allowUnregisteredMember);
            SettingsAsset.logThisKeyMemberOnlyDefault = IntFieldUndo(
                "LogThisKeyMemberOnly Default", DebugxStaticData.ToolTipLogThisKeyMemberOnlyDefault, SettingsAsset.logThisKeyMemberOnlyDefault);

            // Member configuration modification.
            // 成员配置修改。
            DrawMemberConfigSetting();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log Output", GUIStyleEx.TitleStyle2);
            SettingsAsset.logOutput =
                ToggleUndo("EnableLogOutput", DebugxStaticData.ToolTipLogOutput, SettingsAsset.logOutput);
            EditorGUI.BeginDisabledGroup(!SettingsAsset.logOutput);
            SettingsAsset.enableLogStackTrace = ToggleUndo("EnableLogStackTrace",
                DebugxStaticData.ToolTipEnableLogStackTrace, SettingsAsset.enableLogStackTrace);
            SettingsAsset.enableWarningStackTrace = ToggleUndo("EnableWarningStackTrace",
                DebugxStaticData.ToolTipEnableWarningStackTrace, SettingsAsset.enableWarningStackTrace);
            SettingsAsset.enableErrorStackTrace = ToggleUndo("EnableErrorStackTrace",
                DebugxStaticData.ToolTipEnableErrorStackTrace, SettingsAsset.enableErrorStackTrace);
            SettingsAsset.recordAllNonDebugxLogs = ToggleUndo("RecordAllNonDebugxLogs",
                DebugxStaticData.ToolTipRecordAllNonDebugxLogs, SettingsAsset.recordAllNonDebugxLogs);
            SettingsAsset.drawLogToScreen = ToggleUndo("DrawLogToScreen", DebugxStaticData.ToolTipDrawLogToScreen,
                SettingsAsset.drawLogToScreen);
            EditorGUI.BeginDisabledGroup(!SettingsAsset.drawLogToScreen);
            SettingsAsset.restrictDrawLogCount = ToggleUndo("RestrictDrawLogCount",
                DebugxStaticData.ToolTipRestrictDrawLogCount, SettingsAsset.restrictDrawLogCount);
            SettingsAsset.maxDrawLogs = IntFieldUndo("MaxDrawLogs", DebugxStaticData.ToolTipMaxDrawLogs,
                SettingsAsset.maxDrawLogs);
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                SaveCheck();
            }

            EditorGUILayout.Space(16f);
        }

        private static void DrawMemberConfigSetting()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Member Settings", GUIStyleEx.TitleStyle2);

            faMemberConfigSetting.Begin();
            FadeAreaHeaderIsDirty = faMemberConfigSetting.Header("Members");

            DebugxStaticDataEditor.FaMemberConfigSettingOpen = faMemberConfigSetting.BeginFade();
            if (DebugxStaticDataEditor.FaMemberConfigSettingOpen)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent(
                        "Reset Members Part Config",
                        DebugxStaticData.IsChineseSimplified
                            ? "重置成员设置，仅会重置部分成员的配置到默认值。（重置内容：EnableDefault,LogSignature,fadeAreaOpen）"
                            : "Reset member settings; only some member configurations will be reset to default values.\n(Reset contents: EnableDefault, LogSignature, fadeAreaOpen)")))
                {
                    if (EditorUtility.DisplayDialog(
                            "Reset Members Part Config",
                            DebugxStaticData.IsChineseSimplified
                                ? "确认要重置所有成员的部分设置吗？"
                                : "Are you sure you want to reset partial settings for all members?",
                            "Ok", "Cancel"))
                    {
                        Undo.RecordObject(SettingsAsset, "ResetMembersPartConfig");
                        ResetProjectSettingsMembers();
                    }
                }

                if (GUILayout.Button(new GUIContent(
                        "Adapt Color By Editor Skin",
                        DebugxStaticData.IsChineseSimplified
                            ? "颜色根据编辑器皮肤自动适应。在Dark暗皮肤时Log颜色会变亮，在Light亮皮肤时Log颜色会变暗。"
                            : "Colors automatically adapt based on the editor skin. Log colors become brighter in Dark mode and darker in Light mode.")))
                {
                    if (EditorUtility.DisplayDialog(
                            "Adapt Color By Editor Skin",
                            DebugxStaticData.IsChineseSimplified
                                ? "确认要执行颜色根据编辑器皮肤自动适应吗？"
                                : "Are you sure you want to apply color adaptation based on the editor skin?",
                            "Ok", "Cancel"))
                    {
                        Undo.RecordObject(SettingsAsset, "AdaptColorByEditorSkin");

                        ColorDispenser.AdaptColorByEditorSkin();
                    }
                }

                EditorGUILayout.EndHorizontal();

                // Default members.
                // 默认成员。
                if (SettingsAsset.defaultMemberAssets != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Default", GUIStyleEx.TitleStyle3);
                    if (GUILayoutEx.ButtonYellow(
                            "Reset Default Members",
                            DebugxStaticData.IsChineseSimplified
                                ? "重置默认成员，这会重置默认成员的所有数据。"
                                : "Reset the default members; this will reset all data of the default members."))
                    {
                        if (EditorUtility.DisplayDialog(
                                "Reset Default Members",
                                DebugxStaticData.IsChineseSimplified
                                    ? "确认要重置所有默认成员吗？"
                                    : "Are you sure you want to reset all default members?",
                                "Ok", "Cancel"))
                        {
                            Undo.RecordObject(SettingsAsset, "ResetDefaultMembers");
                            SettingsAsset.ResetMembers(true, false);
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    for (int i = 0; i < SettingsAsset.defaultMemberAssets.Length; i++)
                    {
                        DebugxMemberInfoAsset mInfo = SettingsAsset.defaultMemberAssets[i];

                        if (i >= memberInfosFadeAreaPool.Count)
                        {
                            // Undo operation may cause memberInfosFadeAreaPool count to be incorrect.
                            // Undo回退可能导致memberInfosFadeAreaPool数量不正确。
                            ResetWindowData();
                            break;
                        }

                        var faTemp = memberInfosFadeAreaPool[i];
                        faTemp.Begin();
                        FadeAreaHeaderIsDirty = faTemp.Header(string.IsNullOrEmpty(mInfo.signature)
                            ? $"Member {mInfo.key}"
                            : mInfo.signature);
                        bool fadeAreaOpenCached = DebugxMemberInfoAssetEditor.SetFadeAreaOpenCached(SettingsAsset.defaultMemberAssets[i].key,faTemp.BeginFade());
                        if (fadeAreaOpenCached)
                            DrawMemberInfo(ref SettingsAsset.defaultMemberAssets[i], true, true);
                        faTemp.End();
                    }
                }

                // Custom members.
                // 自定义成员。
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Custom Members", GUIStyleEx.TitleStyle3);
                if (GUILayoutEx.ButtonYellow(
                        "Automatically Reassign Colors",
                        DebugxStaticData.IsChineseSimplified
                            ? "自动重分配所有自定义成员的颜色，颜色将根据成员数量平均分配。"
                            : "Automatically reassign colors for all custom members; colors will be evenly distributed based on the number of members."))
                {
                    if (EditorUtility.DisplayDialog(
                            "Automatically Reassign Colors",
                            DebugxStaticData.IsChineseSimplified
                                ? "确认要重分配所有自定义成员的颜色吗？"
                                : "Are you sure you want to reassign colors for all custom members?",
                            "Ok", "Cancel"))
                    {
                        Undo.RecordObject(SettingsAsset, "AutomaticallyReassignColors");
                        ColorDispenser.AutomaticallyReassignColors();
                    }
                }

                if (GUILayout.Button("Add Member"))
                {
                    Undo.RecordObject(SettingsAsset, "AddMember");

                    // Create new member. // 创建新成员。
                    AutoGetMemberKey(out int newKey);
                    DebugxMemberInfoAsset mInfo = new DebugxMemberInfoAsset(newKey);

                    // Add to the end of the array. // 添加到数组末尾。
                    List<DebugxMemberInfoAsset> memberInfos = SettingsAsset.customMemberAssets != null
                        ? new List<DebugxMemberInfoAsset>(SettingsAsset.customMemberAssets)
                        : new List<DebugxMemberInfoAsset>();
                    memberInfos.Add(mInfo);
                    SettingsAsset.customMemberAssets = memberInfos.ToArray();

                    OnAddMemberInfo(mInfo);
                }

                EditorGUILayout.EndHorizontal();

                if (SettingsAsset && SettingsAsset.customMemberAssets != null)
                {
                    int removeIndex = -1;

                    for (int i = 0; i < SettingsAsset.customMemberAssets.Length; i++)
                    {
                        int index = i + SettingsAsset.DefaultMemberAssetsLength;
                        if (index >= memberInfosFadeAreaPool.Count)
                        {
                            // Undoing the operation may result in incorrect memberInfosFadeAreaPool quantity.
                            // Undo回退可能导致memberInfosFadeAreaPool数量不正确。
                            ResetWindowData();
                            break;
                        }

                        var faTemp = memberInfosFadeAreaPool[index];
                        DebugxMemberInfoAsset mInfo = SettingsAsset.customMemberAssets[i];

                        faTemp.Begin();
                        GUILayout.BeginHorizontal();
                        FadeAreaHeaderIsDirty =
                            faTemp.Header(
                                string.IsNullOrEmpty(mInfo.signature) ? $"Member {mInfo.key}" : mInfo.signature, 320);
                        if (GUILayout.Button("Delete Member"))
                        {
                            removeIndex = i;
                        }

                        GUILayout.EndHorizontal();

                        bool mInfoFadeAreaOpenCached = DebugxMemberInfoAssetEditor.SetFadeAreaOpenCached(mInfo.key, faTemp.BeginFade());
                        if (mInfoFadeAreaOpenCached)
                        {
                            DrawMemberInfo(ref mInfo);
                        }

                        // refresh data. // 更新数据。
                        if (SettingsAsset.customMemberAssets != null && i < SettingsAsset.customMemberAssets.Length)
                        {
                            //The switch status of FadeArea can be changed directly without the need for confirmation and saving.
                            // FadeArea的开关状态直接改变不需要保存确认。
                            DebugxMemberInfoAssetEditor.SetFadeAreaOpenCached(
                                SettingsAsset.customMemberAssets[i].key,
                                mInfoFadeAreaOpenCached);
                        }
                        
                        SettingsAsset.customMemberAssets[i] = mInfo;

                        faTemp.End();
                    }

                    // Remove. // 移除。
                    if (removeIndex >= 0)
                    {
                        Undo.RecordObject(SettingsAsset, "DeleteMember");
                        OnRemoveMemberInfo(removeIndex, SettingsAsset.customMemberAssets[removeIndex]);
                        List<DebugxMemberInfoAsset> mInfos = new(SettingsAsset.customMemberAssets);
                        mInfos.RemoveAt(removeIndex);
                        SettingsAsset.customMemberAssets = mInfos.ToArray();
                    }
                }
            }

            faMemberConfigSetting.End();
        }

        private static void DrawMemberInfo(ref DebugxMemberInfoAsset mInfo, bool lockSignature = false, bool lockKey = false)
        {
            mInfo.enableDefault = ToggleUndo(
                "Enable Default",
                DebugxStaticData.IsChineseSimplified
                    ? "是否开启，在运行时也可通过DebugxConsole动态改变开关。"
                    : "Whether to enable; the switch can also be dynamically changed via DebugxConsole at runtime.",
                mInfo.enableDefault);

            //signature. //签名。
            EditorGUI.BeginDisabledGroup(lockSignature);
            string signatureNew = EditorGUILayout.DelayedTextField(
                new GUIContent("Signature", DebugxStaticData.IsChineseSimplified ? "成员签名" : "Member signature"),
                mInfo.signature);
            if (signatureNew != mInfo.signature)
            {
                //Confirm if it is a duplicate. // 确认是否重复。
                CheckMemberSignatureRepetition(ref signatureNew, mInfo.signature);
                mInfo.signature = signatureNew;

                Undo.RecordObject(SettingsAsset, "DebugxSettingsProvider Text Set");
            }

            EditorGUI.EndDisabledGroup();
            mInfo.logSignature = ToggleUndo("LogSignature",
                DebugxStaticData.IsChineseSimplified ? "是否打印签名" : "Whether to print the signature", mInfo.logSignature);

            // Print key. // 打印密钥。
            EditorGUI.BeginDisabledGroup(lockKey);
            int changeKey = EditorGUILayout.DelayedIntField(new GUIContent(
                    "Key",
                    DebugxStaticData.IsChineseSimplified
                        ? "成员信息密钥，在效用Debugx.Logx()方法时使用。"
                        : "Member info key, used when calling the Debugx.Logx() method."),
                mInfo.key);
            if (changeKey != mInfo.key)
            {
                if (changeKey <= 0) changeKey = 1;

                bool setKey = true;

                // Confirm whether the key is duplicated. If duplicated, it will automatically start using the smallest available key.
                // 确认Key是否重复，重复时自动从最小可用Key开始使用。
                if (!DebugxProjectSettings.KeyValid(changeKey) || CheckMemberKeyRepetition(changeKey, mInfo.key))
                {
                    if (!AutoGetMemberKey(out changeKey, mInfo.key))
                    {
                        setKey = false;
                    }
                }

                if (setKey)
                {
                    mInfo.key = changeKey;
                    Undo.RecordObject(SettingsAsset, "Member Key Change");
                }
            }

            EditorGUI.EndDisabledGroup();

            mInfo.header = TextFieldUndo("Header", "头部信息，在答应log时打印在头部", mInfo.header);

            EditorGUILayout.BeginHorizontal();
            mInfo.color = ColorFieldUndo("Color", "Log颜色", mInfo.color);
            if (GUILayout.Button(new GUIContent(
                        "Adapt Color",
                        DebugxStaticData.IsChineseSimplified
                            ? "颜色根据编辑器皮肤自动适应。在Dark暗皮肤时Log颜色会变亮，在Light亮皮肤时Log颜色会变暗。"
                            : "Colors automatically adapt based on the editor skin. In Dark mode, log colors become brighter; in Light mode, log colors become darker."),
                    GUILayout.Width(100)))
            {
                Undo.RecordObject(SettingsAsset, "AdaptColor Single");
                mInfo.color = ColorDispenser.GetMemberColorByEditorSkin(mInfo.color);
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Reset the window data.
        /// 重置窗口数据。
        /// </summary>
        private static void ResetWindowData()
        {
            // This method makes use of GUI.skin.button. The GUI class can only be called within the OnGUI function and cannot be called in OnEnable.
            // 此方法内调用到了GUI.skin.button，GUI类必须在OnGUI才能调用，不能在OnEnable。

            faMemberConfigSetting = new FadeArea(settingsProvider, DebugxStaticDataEditor.FaMemberConfigSettingOpen);

            memberInfosFadeAreaPool.Clear();

            if (SettingsAsset.defaultMemberAssets != null)
            {
                for (int i = 0; i < SettingsAsset.defaultMemberAssets.Length; i++)
                {
                    OnAddMemberInfo(SettingsAsset.defaultMemberAssets[i]);
                }
            }

            if (SettingsAsset.customMemberAssets != null)
            {
                for (int i = 0; i < SettingsAsset.customMemberAssets.Length; i++)
                {
                    OnAddMemberInfo(SettingsAsset.customMemberAssets[i]);
                }
            }
        }

        /// <summary>
        /// When adding a member information.
        /// 当添加一个成员信息时。
        /// </summary>
        /// <param name="info"></param>
        private static void OnAddMemberInfo(DebugxMemberInfoAsset info)
        {
            memberInfosFadeAreaPool.Add(new FadeArea(settingsProvider, DebugxMemberInfoAssetEditor.GetFadeAreaOpenCached(info.key)));
        }

        /// <summary>
        /// When a member information is removed.
        /// 当移除一个成员信息时。
        /// </summary>
        /// <param name="index"></param>
        /// <param name="info"></param>
        private static void OnRemoveMemberInfo(int index, DebugxMemberInfoAsset info)
        {
            DebugxMemberInfoAssetEditor.DeleteFadeAreaOpenCached(info.key);
            memberInfosFadeAreaPool.RemoveAt(index + 2); //前面两个时Normal和Master用的
        }

        private static void ResetProjectSettings()
        {
            // if (!EditorConfig.canResetProjectSettings) return;

            SettingsAsset.enableLogDefault = DebugxStaticData.EnableLogDefaultSet;
            SettingsAsset.enableLogMemberDefault = DebugxStaticData.EnableLogMemberDefaultSet;
            SettingsAsset.allowUnregisteredMember = DebugxStaticData.AllowUnregisteredMemberSet;
            SettingsAsset.logThisKeyMemberOnlyDefault = DebugxStaticData.LogThisKeyMemberOnlyDefaultSet;

            SettingsAsset.logOutput = DebugxStaticData.LogOutputSet;
            SettingsAsset.enableLogStackTrace = DebugxStaticData.EnableLogStackTraceSet;
            SettingsAsset.enableWarningStackTrace = DebugxStaticData.EnableWarningStackTraceSet;
            SettingsAsset.enableErrorStackTrace = DebugxStaticData.EnableErrorStackTraceSet;
            SettingsAsset.recordAllNonDebugxLogs = DebugxStaticData.RecordAllNonDebugxLogsSet;
            SettingsAsset.drawLogToScreen = DebugxStaticData.DrawLogToScreenSet;
            SettingsAsset.restrictDrawLogCount = DebugxStaticData.RestrictDrawLogCountSet;
            SettingsAsset.maxDrawLogs = DebugxStaticData.MaxDrawLogsSet;

            ResetProjectSettingsMembers();

            // EditorConfig.canResetProjectSettings = false;
        }

        private static void ResetProjectSettingsMembers()
        {
            for (int i = 0; i < SettingsAsset.defaultMemberAssets.Length; i++)
            {
                SettingsAsset.defaultMemberAssets[i].ResetToDefaultPart();
            }

            for (int i = 0; i < SettingsAsset.customMemberAssets.Length; i++)
            {
                SettingsAsset.customMemberAssets[i].ResetToDefaultPart();
            }
        }

        private static void SaveCheck()
        {
            assetIsDirty = true;

            if (DebugxStaticDataEditor.AutoSave)
            {
                Apply();
            }
        }

        private static void OnAutoSaveChange(bool enable)
        {
            // When switching to automatic saving mode, a storage operation will be performed automatically.
            // 变为自动保存时，自动进行一次存储。
            if (enable)
            {
                Apply();
            }
        }

        private static void Apply()
        {
            if (!DebugxStaticDataEditor.AutoSave && !assetIsDirty && !fadeAreaHeaderIsDirty) return;
            assetIsDirty = false;
            fadeAreaHeaderIsDirty = false;

            if (SettingsAsset != null)
            {
                EditorUtility.SetDirty(SettingsAsset);
                AssetDatabase.SaveAssetIfDirty(SettingsAsset);
                SettingsAsset.ApplyTo(DebugxProjectSettings.Instance);
                
                // Generate Debugx class with member-specific Log methods.
                // 生成包含成员专用 Log 方法的 Debugx 类。
                DebugxCodeGenerator.GenerateDebugxClass();
            }
        }

        #region MemberInfo

        private static readonly Regex _regexEndingDigit = new Regex(@"\d+$");

        // Check if the key for member information is duplicated. If it is duplicated, return true.
        // 确认成员信息的Key是否重复，重复时返回true。
        private static bool CheckMemberKeyRepetition(int key, int withoutKey = 0)
        {
            if (SettingsAsset.CustomMemberAssetsLength == 0) return false;

            for (int i = 0; i < SettingsAsset.CustomMemberAssetsLength; i++)
            {
                var m = SettingsAsset.customMemberAssets[i];
                if (m.key == withoutKey) continue;
                if (m.key == key)
                    return true;
            }

            return false;
        }

        private static void CheckMemberSignatureRepetition(ref string signature, string withoutSignature = "")
        {
            if (string.IsNullOrEmpty(signature)) return;

            for (int i = 0; i < SettingsAsset.CustomMemberAssetsLength; i++)
            {
                var m = SettingsAsset.customMemberAssets[i];
                if (m.signature.Equals(withoutSignature)) continue;
                if (m.signature.Equals(signature))
                {
                    GetSignatureUnique(ref signature, withoutSignature);
                    return;
                }
            }

            for (int i = 0; i < SettingsAsset.DefaultMemberAssetsLength; i++)
            {
                var m = SettingsAsset.defaultMemberAssets[i];
                if (m.signature.Equals(withoutSignature)) continue;
                if (m.signature.Equals(signature))
                {
                    GetSignatureUnique(ref signature, withoutSignature);
                    return;
                }
            }
        }

        private static void GetSignatureUnique(ref string signature, string withoutSignature = "")
        {
            string signatureOri = signature;
            string signatureBase = signature;
            int num = 1;
            Match match = _regexEndingDigit.Match(signatureBase);
            if (match.Length != 0)
            {
                int.TryParse(match.Value, out num);
                signatureBase = _regexEndingDigit.Replace(signatureBase, "");
            }

            string signatureNew = $"{signatureBase}{num}";
            while (signatureOri.Equals(signatureNew))
            {
                num++;
                signatureNew = $"{signatureBase}{num}";
                if (num == int.MaxValue)
                {
                    signatureNew = "";
                    break;
                }
            }

            signature = signatureNew;
            CheckMemberSignatureRepetition(ref signature, withoutSignature);
        }

        /// <summary>
        /// Obtain a unique Key.
        /// 获取一个不重复的Key。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="withoutKey"></param>
        /// <returns></returns>
        private static bool AutoGetMemberKey(out int key, int withoutKey = 0)
        {
            key = 1;
            while (CheckMemberKeyRepetition(key, withoutKey))
            {
                if (key >= int.MaxValue)
                {
                    return false;
                }

                key++;
            }

            return true;
        }

        #endregion

        #region GUILayout for Undo

        private static bool ToggleUndo(string label, string tooltip, bool value)
        {
            return GUILayoutEx.ToggleUndo(label, tooltip, value, SettingsAsset, "DebugxSettingsProvider Toggle Set");
        }

        private static int IntFieldUndo(string label, string tooltip, int value)
        {
            return GUILayoutEx.IntFieldUndo(label, tooltip, value, SettingsAsset, "DebugxSettingsProvider Int Set");
        }

        private static string TextFieldUndo(string label, string tooltip, string value)
        {
            return GUILayoutEx.TextFieldUndo(label, tooltip, value, SettingsAsset, "DebugxSettingsProvider Text Set");
        }

        private static Color ColorFieldUndo(string label, string tooltip, Color value)
        {
            return GUILayoutEx.ColorFieldUndo(label, tooltip, value, SettingsAsset, "DebugxSettingsProvider Color Set");
        }

        #endregion
    }
}