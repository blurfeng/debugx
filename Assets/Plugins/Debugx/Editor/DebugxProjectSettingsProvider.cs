using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Editor
{
    static class DebugxProjectSettingsProvider
    {
        private static SettingsProvider _settingsProvider;
        private static DebugxProjectSettingsAsset SettingsAsset => DebugxProjectSettingsAsset.Instance;
        private static readonly List<FadeArea> _memberInfosFadeAreaPool = new();
        private static FadeArea _faMemberConfigSetting;
        private static bool _isInitGUI;
        private static bool _assetIsDirty;
        
        private const float ButtonWidth1 = 100f;
        private const float ButtonWidth2 = 150f;
        private const float ButtonWidth3 = 200f;

        [SettingsProvider]
        public static SettingsProvider DebugxProjectSettingsProviderCreate()
        {
            if (_settingsProvider == null)
            {
                _isInitGUI = false;

                _settingsProvider = new SettingsProvider("Project/Debugx", SettingsScope.Project)
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

            if (!_isInitGUI)
            {
                // Some initialization involving GUI classes must be called within OnGUI.
                // 一些初始化内容调用到GUI类，必须在OnGUI内调用。
                _isInitGUI = true;
                ResetWindowData();
            }

            string defineSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            // 按 ';' 精确匹配宏符号，避免把 DEBUG_XR 等含子串的其它宏误判为已配置 DEBUG_X。
            // Match the define token exactly (split on ';') so symbols that merely contain the substring — e.g. DEBUG_XR —
            // aren't mistaken for DEBUG_X being configured.
            if (System.Array.IndexOf(defineSymbols.Split(';'), "DEBUG_X") < 0)
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
            EditorGUI.BeginDisabledGroup(!_assetIsDirty);
            if (GUILayoutEx.ButtonGreen("Save Asset", GUILayout.Width(ButtonWidth1))) 
                Apply();
            EditorGUI.EndDisabledGroup();
            
            GUILayout.FlexibleSpace(); // Push the Save Asset button to the left side. // 将 Save Asset 按钮推到左侧。

            if (GUILayoutEx.ButtonRed("Reset to Default", GUILayout.Width(ButtonWidth3)))
            {
                if (ConfirmDialog(
                        "Reset To Default",
                        "确认要重置到默认设置吗？\n重置并不会清空Member成员配置，仅将成员配置的部分字段重置。",
                        "Are you sure you want to reset to the default settings?\nResetting will not clear the Member configurations; it will only reset certain fields within the member configurations."))
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
            BeginChangeCheck();

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
            EditorGUI.EndDisabledGroup();

            EndChangeCheck();

            EditorGUILayout.Space(16f);
        }

        private static void DrawMemberConfigSetting()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Member Settings", GUIStyleEx.TitleStyle2);

            _faMemberConfigSetting.Begin();
            _faMemberConfigSetting.Header("Members");

            DebugxStaticDataEditor.FaMemberConfigSettingOpen = _faMemberConfigSetting.BeginFade();
            if (DebugxStaticDataEditor.FaMemberConfigSettingOpen)
            {
                EditorGUILayout.BeginHorizontal();
                EndChangeCheck(); // 将生成代码按钮操作排除在外。 // Exclude the generate code button operation.
                if (GUILayout.Button(new GUIContent("Generate DebugxLogger Code",
                        DebugxStaticData.IsChineseSimplified 
                            ? "根据当前成员配置生成Debugx类代码，会覆盖原有代码文件。保存时也会自动生成代码，只有当成员和代码不匹配时才需要手动生成代码。"
                            : "Generate DebugxLogger class code based on the current member configuration, which will overwrite the existing code file.\nCode generation also occurs upon saving; manual generation is only necessary when members and code do not match."
                        ), EditorStyles.miniButtonLeft, GUILayout.Width(ButtonWidth3)))
                {
                    // 保存成功时也会重新生成代码。没成功保存时主动调用生成代码。
                    // Code will be regenerated when saved successfully. If saving fails, code generation is called proactively.
                    if (!Apply())
                    {
                        DebugxLoggerCodeGenerator.GenerateDebugxLoggerClass();
                        AssetDatabase.Refresh();
                    }
                }
                BeginChangeCheck();
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button(new GUIContent(
                        "Reset Members Part Config",
                        DebugxStaticData.IsChineseSimplified
                            ? "重置成员设置，仅会重置部分成员的配置到默认值。（重置内容：EnableDefault,LogSignature,fadeAreaOpen）"
                            : "Reset member settings; only some member configurations will be reset to default values.\n(Reset contents: EnableDefault, LogSignature, fadeAreaOpen)"),
                        GUILayout.Width(ButtonWidth3)))
                {
                    if (ConfirmDialog(
                            "Reset Members Part Config",
                            "确认要重置所有成员的部分设置吗？",
                            "Are you sure you want to reset partial settings for all members?"))
                    {
                        Undo.RecordObject(SettingsAsset, "ResetMembersPartConfig");
                        ResetProjectSettingsMembers();
                    }
                }

                if (GUILayout.Button(new GUIContent(
                        "Adapt Color By Editor Skin",
                        DebugxStaticData.IsChineseSimplified
                            ? "颜色根据编辑器皮肤自动适应。在Dark暗皮肤时Log颜色会变亮，在Light亮皮肤时Log颜色会变暗。"
                            : "Colors automatically adapt based on the editor skin. Log colors become brighter in Dark mode and darker in Light mode."),
                        GUILayout.Width(ButtonWidth3)))
                {
                    if (ConfirmDialog(
                            "Adapt Color By Editor Skin",
                            "确认要执行颜色根据编辑器皮肤自动适应吗？",
                            "Are you sure you want to apply color adaptation based on the editor skin?"))
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
                                : "Reset the default members; this will reset all data of the default members.",
                            GUILayout.Width(ButtonWidth3)))
                    {
                        if (ConfirmDialog(
                                "Reset Default Members",
                                "确认要重置所有默认成员吗？",
                                "Are you sure you want to reset all default members?"))
                        {
                            Undo.RecordObject(SettingsAsset, "ResetDefaultMembers");
                            SettingsAsset.ResetMembers(true, false);
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    for (int i = 0; i < SettingsAsset.defaultMemberAssets.Length; i++)
                    {
                        DebugxMemberInfoAsset mInfo = SettingsAsset.defaultMemberAssets[i];

                        if (i >= _memberInfosFadeAreaPool.Count)
                        {
                            // Undo operation may cause memberInfosFadeAreaPool count to be incorrect.
                            // Undo回退可能导致memberInfosFadeAreaPool数量不正确。
                            ResetWindowData();
                            break;
                        }

                        var faTemp = _memberInfosFadeAreaPool[i];
                        faTemp.Begin();
                        faTemp.Header(string.IsNullOrEmpty(mInfo.signature)
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
                
                if (GUILayoutEx.ButtonGreen("Add Member", GUILayout.Width(ButtonWidth3)))
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
                
                GUILayout.FlexibleSpace();
                
                if (GUILayoutEx.ButtonYellow(
                        "Automatically Reassign Colors",
                        DebugxStaticData.IsChineseSimplified
                            ? "自动重分配所有自定义成员的颜色，颜色将根据成员数量平均分配。"
                            : "Automatically reassign colors for all custom members; colors will be evenly distributed based on the number of members.",
                        GUILayout.Width(ButtonWidth3)))
                {
                    if (ConfirmDialog(
                            "Automatically Reassign Colors",
                            "确认要重分配所有自定义成员的颜色吗？",
                            "Are you sure you want to reassign colors for all custom members?"))
                    {
                        Undo.RecordObject(SettingsAsset, "AutomaticallyReassignColors");
                        ColorDispenser.AutomaticallyReassignColors();
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (SettingsAsset && SettingsAsset.customMemberAssets != null)
                {
                    int removeIndex = -1;

                    for (int i = 0; i < SettingsAsset.customMemberAssets.Length; i++)
                    {
                        int index = i + SettingsAsset.DefaultMemberAssetsLength;
                        if (index >= _memberInfosFadeAreaPool.Count)
                        {
                            // Undoing the operation may result in incorrect memberInfosFadeAreaPool quantity.
                            // Undo回退可能导致memberInfosFadeAreaPool数量不正确。
                            ResetWindowData();
                            break;
                        }

                        var faTemp = _memberInfosFadeAreaPool[index];
                        DebugxMemberInfoAsset mInfo = SettingsAsset.customMemberAssets[i];

                        faTemp.Begin();
                        GUILayout.BeginHorizontal();
                        faTemp.Header(string.IsNullOrEmpty(mInfo.signature) ? $"Member {mInfo.key}" : mInfo.signature, 320);
                        GUILayout.FlexibleSpace(); // Push the button to the right side. // 将按钮推到右侧。
                        if (GUILayoutEx.ButtonRed("Delete Member", GUILayout.Width(ButtonWidth1)))
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

            _faMemberConfigSetting.End();
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
                    GUILayout.Width(ButtonWidth1)))
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

            _faMemberConfigSetting = new FadeArea(_settingsProvider, DebugxStaticDataEditor.FaMemberConfigSettingOpen);

            _memberInfosFadeAreaPool.Clear();

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
            _memberInfosFadeAreaPool.Add(new FadeArea(_settingsProvider, DebugxMemberInfoAssetEditor.GetFadeAreaOpenCached(info.key)));
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
            // 池布局为 [默认成员..., 自定义成员...]，自定义索引需偏移默认成员数量；用 DefaultMemberAssetsLength 而非硬编码 2，与绘制循环保持一致。
            // Pool layout is [defaults..., customs...]; offset by the default count. Use DefaultMemberAssetsLength (not a hardcoded 2) to match the draw loop.
            _memberInfosFadeAreaPool.RemoveAt(index + SettingsAsset.DefaultMemberAssetsLength);
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

            ResetProjectSettingsMembers();

            // EditorConfig.canResetProjectSettings = false;
        }

        private static void ResetProjectSettingsMembers()
        {
            if (SettingsAsset.defaultMemberAssets != null)
            {
                for (int i = 0; i < SettingsAsset.defaultMemberAssets.Length; i++)
                {
                    SettingsAsset.defaultMemberAssets[i].ResetToDefaultPart();
                }
            }

            if (SettingsAsset.customMemberAssets != null)
            {
                for (int i = 0; i < SettingsAsset.customMemberAssets.Length; i++)
                {
                    SettingsAsset.customMemberAssets[i].ResetToDefaultPart();
                }
            }
        }
        
        private static void BeginChangeCheck()
        {
            EditorGUI.BeginChangeCheck();
        }
        
        private static void EndChangeCheck()
        {
            if (EditorGUI.EndChangeCheck())
            {
                SaveCheck();
            }
        }

        private static void SaveCheck()
        {
            _assetIsDirty = true;

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

        private static bool Apply()
        {
            // 仅折叠/展开（_fadeAreaHeaderIsDirty）只改变 UI 折叠态（存于 PlayerPrefs），不改 asset 数据，
            // 因此不应触发落盘 + ApplyTo + 重生成代码。只有 asset 数据真正变更（_assetIsDirty）时才做重活。
            // Fold/expand only changes UI state (stored in PlayerPrefs), not asset data, so it must not trigger
            // a disk write + ApplyTo + codegen. Only do the heavy work when asset data actually changed.
            bool assetChanged = _assetIsDirty;
            _assetIsDirty = false;

            if (!assetChanged) return false;

            if (SettingsAsset != null)
            {
                EditorUtility.SetDirty(SettingsAsset);
                AssetDatabase.SaveAssetIfDirty(SettingsAsset);
                SettingsAsset.ApplyTo(DebugxProjectSettings.Instance);

                // Generate DebugxLogger class with member-specific Log methods.
                // 生成包含成员专用 Log 方法的 Debugx 类。
                DebugxLoggerCodeGenerator.GenerateDebugxLoggerClass();
            }

            return true;
        }

        /// <summary>
        /// Unified bilingual confirmation dialog (Ok/Cancel).
        /// 统一的双语确认对话框（Ok/Cancel）。
        /// </summary>
        private static bool ConfirmDialog(string title, string cnMessage, string enMessage)
        {
            return EditorUtility.DisplayDialog(
                title, DebugxStaticData.IsChineseSimplified ? cnMessage : enMessage, "Ok", "Cancel");
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
                if (string.Equals(m.signature, withoutSignature)) continue;
                if (string.Equals(m.signature, signature))
                {
                    GetSignatureUnique(ref signature, withoutSignature);
                    return;
                }
            }

            for (int i = 0; i < SettingsAsset.DefaultMemberAssetsLength; i++)
            {
                var m = SettingsAsset.defaultMemberAssets[i];
                if (string.Equals(m.signature, withoutSignature)) continue;
                if (string.Equals(m.signature, signature))
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

