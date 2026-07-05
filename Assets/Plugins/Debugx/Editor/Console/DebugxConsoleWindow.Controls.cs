using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Debugx Console — member filter (B1) and the runtime source switches + test toggles absorbed from the old
    /// DebugxConsole control panel. Kept in a partial file to keep the main viewer file focused.
    /// Debugx Console —— 成员过滤（B1），以及从旧 DebugxConsole 控制面板吸收来的运行时源头开关 + 测试开关。
    /// 拆到 partial 文件，让主查看器文件保持聚焦。
    /// </summary>
    public partial class DebugxConsoleWindow
    {
        private ToolbarButton _memberButton;
        private ToolbarToggle _editorPanelToggle;
        private VisualElement _editorPanel;
        private bool _editorPanelVisible;

        // ---------- Member filter (B1) ----------

        private void ShowMemberMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(L("全部", "All")), _criteria.VisibleMemberKeys == null, () =>
            {
                _criteria.VisibleMemberKeys = null;
                OnCriteriaChanged();
            });
            menu.AddSeparator(string.Empty);

            DebugxProjectSettings settings = DebugxProjectSettings.Instance;
            if (settings != null && settings.members != null)
            {
                foreach (DebugxMemberInfo m in settings.members)
                {
                    if (m == null) continue;
                    int key = m.key;
                    string sig = string.IsNullOrEmpty(m.signature) ? "Member" : m.signature;
                    menu.AddItem(new GUIContent($"[{key}] {sig}"), IsMemberVisible(key), () => ToggleMember(key));
                }
            }

            // Pseudo-members that are NOT in the configured member list but still appear as entries. They must be listed
            // here (and in BuildAllMemberKeySet) or narrowing the filter would silently and unrecoverably hide them.
            // Admin = LogAdm channel (key 0); Unregistered = a log from an unknown key; Uncategorized = non-Debugx logs.
            // (The runtime Console derives these from the buffer, so this keeps the two front-ends consistent.)
            // 不在配置成员列表里、但仍会作为条目出现的伪成员。必须在此列出（并纳入 BuildAllMemberKeySet），否则收窄过滤会静默且
            // 不可恢复地隐藏它们。Admin = LogAdm 通道（key 0）；Unregistered = 未知 key 的日志；Uncategorized = 非 Debugx 日志。
            //（运行时 Console 从缓冲派生这些项，故此举使两个前端保持一致。）
            if (settings != null && settings.AdminInfo != null)
            {
                DebugxMemberInfo admin = settings.AdminInfo;
                menu.AddItem(new GUIContent($"[{admin.key}] {admin.signature}"),
                    IsMemberVisible(admin.key), () => ToggleMember(admin.key));
            }

            menu.AddItem(new GUIContent(L("未注册", "Unregistered")),
                IsMemberVisible(DebugxRawLog.UnregisteredKey), () => ToggleMember(DebugxRawLog.UnregisteredKey));

            menu.AddItem(new GUIContent(L("未分类", "Uncategorized")),
                IsMemberVisible(DebugxLogEntry.UncategorizedKey), () => ToggleMember(DebugxLogEntry.UncategorizedKey));

            menu.ShowAsContext();
        }

        private bool IsMemberVisible(int key)
            => _criteria.VisibleMemberKeys == null || _criteria.VisibleMemberKeys.Contains(key);

        private void ToggleMember(int key)
        {
            HashSet<int> keys = _criteria.VisibleMemberKeys;
            if (keys == null)
            {
                // Was "all": materialize the full set so unchecking this one narrows the selection.
                // 原为“全部”：先具体化为全集，取消勾选此项即为收窄选择。
                keys = BuildAllMemberKeySet();
                _criteria.VisibleMemberKeys = keys;
            }

            if (!keys.Remove(key)) keys.Add(key);
            OnCriteriaChanged();
        }

        private static HashSet<int> BuildAllMemberKeySet()
        {
            var set = new HashSet<int>();
            DebugxProjectSettings settings = DebugxProjectSettings.Instance;
            if (settings != null && settings.members != null)
            {
                foreach (DebugxMemberInfo m in settings.members)
                    if (m != null) set.Add(m.key);
            }
            // Pseudo-members that also appear as entries but are not in the configured list — see ShowMemberMenu.
            // 同样会作为条目出现、但不在配置列表里的伪成员——见 ShowMemberMenu。
            if (settings != null && settings.AdminInfo != null) set.Add(settings.AdminInfo.key);
            set.Add(DebugxRawLog.UnregisteredKey);
            set.Add(DebugxLogEntry.UncategorizedKey);
            return set;
        }

        // ---------- Clear dropdown (native-style Clear-on options) ----------

        private void ShowClearMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(L("进入 Play 时清空", "Clear on Play")), _clearOnPlay,
                () => { _clearOnPlay = !_clearOnPlay; SavePrefs(); });
            menu.AddItem(new GUIContent(L("重编译时清空", "Clear on Recompile")), _clearOnRecompile,
                () => { _clearOnRecompile = !_clearOnRecompile; SavePrefs(); });
            menu.AddItem(new GUIContent(L("构建时清空", "Clear on Build")), _clearOnBuild,
                () => { _clearOnBuild = !_clearOnBuild; SavePrefs(); });
            menu.ShowAsContext();
        }

        // ---------- Runtime source switches + Test (absorbed from the old DebugxConsole) ----------

        private VisualElement BuildEditorPanel()
        {
            var panel = new VisualElement();
            panel.style.display = DisplayStyle.None; // hidden until the Runtime toggle is turned on. 打开“运行时”开关前隐藏。
            panel.style.paddingLeft = 6;
            panel.style.paddingRight = 6;
            panel.style.paddingTop = 4;
            panel.style.paddingBottom = 4;
            panel.style.borderBottomWidth = 1;
            panel.style.borderBottomColor = DebugxConsoleStyle.EditorPanelBorderColor;
            return panel;
        }

        // Fired when project settings are (re)applied; refresh the member-dependent controls.
        // 项目设置（重新）应用时触发；刷新依赖成员的控件。
        private void OnSettingsApplied()
        {
            if (_editorPanelVisible) RefreshEditorPanel();
            Repaint();
        }

        // Rebuilds the runtime panel from the current Debugx state and play-mode. Called on show / play-mode change /
        // settings apply — so it reflects live values at those moments.
        // 依据当前 Debugx 状态与 Play 模式重建运行时面板。在 显示 / Play 模式变化 / 设置应用 时调用，故这些时刻反映最新值。
        private void RefreshEditorPanel()
        {
            if (_editorPanel == null) return;
            _editorPanel.Clear();

            bool playing = Application.isPlaying;

            // --- View / display options (Debugx-Only filter + the ex-toolbar "View" options; always editable) ---
            // --- 视图 / 显示选项（仅Debugx 过滤 + 原顶部栏「视图」选项；始终可编辑）---
            var viewTitle = new Label(L("视图", "View"));
            viewTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _editorPanel.Add(viewTitle);

            // Debugx Only: filters the list to logs tagged [Debugx]; independent of the runtime switches below.
            // 仅Debugx：把列表过滤为带 [Debugx] 标签的日志；与下方运行时开关无关。
            var onlyDebugxToggle = new Toggle(L("仅Debugx", "Debugx Only")) { value = _criteria.OnlyDebugx };
            onlyDebugxToggle.tooltip = L("仅显示带 [Debugx] 标签的日志。", "Show only logs tagged with [Debugx].");
            onlyDebugxToggle.RegisterValueChangedCallback(evt =>
            {
                _criteria.OnlyDebugx = evt.newValue;
                OnCriteriaChanged();
            });
            _editorPanel.Add(onlyDebugxToggle);

            // Show Timestamp: toggles the per-row time column. 显示时间戳：显隐每行的时间列。
            var showTimestamp = new Toggle(L("显示时间戳", "Show Timestamp")) { value = _showTimestamp };
            showTimestamp.tooltip = L("在每行左侧显示日志时间。", "Show the log time at the start of each row.");
            showTimestamp.RegisterValueChangedCallback(evt =>
            {
                _showTimestamp = evt.newValue;
                SavePrefs();
                _listView?.RefreshItems(); // re-bind rows to show/hide the timestamp column. 重绑行以显隐时间戳列。
            });
            _editorPanel.Add(showTimestamp);

            // Stack: Script Only — the detail stack shows only script frames (hide engine / Debugx-internal frames).
            // 堆栈：仅脚本——详情堆栈仅显示脚本帧（隐藏引擎/Debugx 内部帧）。
            var stackScriptOnly = new Toggle(L("堆栈：仅脚本", "Stack: Script Only")) { value = _stackScriptOnly };
            stackScriptOnly.tooltip = L("详情堆栈仅显示脚本帧，隐藏引擎/Debugx 内部帧。", "Show only script frames in the detail stack; hide engine / Debugx-internal frames.");
            stackScriptOnly.RegisterValueChangedCallback(evt =>
            {
                _stackScriptOnly = evt.newValue;
                SavePrefs();
                RefreshSelectedDetail();
            });
            stackScriptOnly.style.marginBottom = 6;
            _editorPanel.Add(stackScriptOnly);

            // Description moved to the toggle's tooltip (hover) instead of an inline hint label below it.
            // 说明移到开关的 tooltip（悬停显示），不再用其下方的行内提示标签。
            var runtimeConsoleToggle = new Toggle(L("启用游戏内 Console", "Enable in-game Console"))
                { value = DebugxStaticData.RuntimeConsoleEnabled };
            runtimeConsoleToggle.tooltip = L(
                "控制游戏内覆盖层 Console 是否在进入 Play 时自建（下次 Play 生效）。默认关闭——编辑器里通常用不到（本窗口已够用）。此勾选只写编辑器的 PlayerPrefs，不影响构建；实机需在游戏代码中置 DebugxStaticData.RuntimeConsoleEnabled = true。",
                "Whether the in-game overlay Console self-creates on entering Play (applies next Play). Off by default — usually not needed in the Editor (this window covers it). This tick only writes Editor PlayerPrefs and does NOT affect a build; enable it in-build via DebugxStaticData.RuntimeConsoleEnabled = true in game code.");
            runtimeConsoleToggle.RegisterValueChangedCallback(evt => DebugxStaticData.RuntimeConsoleEnabled = evt.newValue);
            runtimeConsoleToggle.style.marginBottom = 6;
            _editorPanel.Add(runtimeConsoleToggle);

            // --- Runtime source switches (only meaningful in Play mode) ---
            var runtimeGroup = new VisualElement();
            runtimeGroup.SetEnabled(playing);

            var title = new Label(L("运行时开关", "Runtime Switches"));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            runtimeGroup.Add(title);

            var enableLog = new Toggle(L("Log 总开关 (EnableLog)", "Master log switch (EnableLog)")) { value = Debugx.enableLog };
            enableLog.RegisterValueChangedCallback(evt => Debugx.enableLog = evt.newValue);
            runtimeGroup.Add(enableLog);

            var enableLogMember = new Toggle(L("成员 Log 总开关 (EnableLogMember)", "Member log switch (EnableLogMember)")) { value = Debugx.enableLogMember };
            enableLogMember.RegisterValueChangedCallback(evt => Debugx.enableLogMember = evt.newValue);
            runtimeGroup.Add(enableLogMember);

            var onlyKey = new IntegerField(L("仅打印此 Key (LogThisKeyMemberOnly)", "Only this key (LogThisKeyMemberOnly)")) { value = Debugx.logThisKeyMemberOnly };
            onlyKey.RegisterValueChangedCallback(evt => Debugx.logThisKeyMemberOnly = evt.newValue);
            runtimeGroup.Add(onlyKey);

            // Per-member runtime send switches removed here — that surface duplicated the toolbar's Members control for
            // the user's purposes; per-member state is still settable in Preferences > Debugx and via game code.
            // 此处的逐成员运行时发送开关已移除——对用户而言与顶部栏 Members 重复；逐成员状态仍可在 Preferences > Debugx 与游戏代码中设置。
            _editorPanel.Add(runtimeGroup);

            if (!playing)
            {
                var hint = new Label(L("以上仅在游戏运行时可设置。", "The above can only be set during Play mode."));
                hint.style.color = DebugxConsoleStyle.HintColor;
                hint.style.whiteSpace = WhiteSpace.Normal;
                _editorPanel.Add(hint);
            }

            // --- Test log toggles (always editable in the Editor) ---
            var testTitle = new Label(L("测试", "Test"));
            testTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            testTitle.style.marginTop = 5;
            _editorPanel.Add(testTitle);

            var awake = new Toggle(L("Awake 测试打印", "Awake test log")) { value = DebugxStaticData.EnableAwakeTestLog };
            awake.RegisterValueChangedCallback(evt => DebugxStaticData.EnableAwakeTestLog = evt.newValue);
            _editorPanel.Add(awake);

            var start = new Toggle(L("Start 测试打印", "Start test log")) { value = DebugxStaticData.EnableStartTestLog };
            start.RegisterValueChangedCallback(evt => DebugxStaticData.EnableStartTestLog = evt.newValue);
            _editorPanel.Add(start);

            var update = new Toggle(L("Update 测试打印", "Update test log")) { value = DebugxStaticData.EnableUpdateTestLog };
            update.RegisterValueChangedCallback(evt => DebugxStaticData.EnableUpdateTestLog = evt.newValue);
            _editorPanel.Add(update);

            // --- UI language (moved here from the toolbar): a single button showing the current language; click toggles. ---
            // ToggleLanguage re-localizes the toolbar and (deferred) rebuilds this panel — see ApplyLanguage.
            // --- 界面语言（从顶部栏移来）：一个显示当前语言的按钮，点击切换。ToggleLanguage 会重新本地化工具栏并（延迟）重建本面板，见 ApplyLanguage。 ---
            var langRow = new VisualElement();
            langRow.style.flexDirection = FlexDirection.Row;
            langRow.style.alignItems = Align.Center;
            langRow.style.marginTop = 8;

            var langLabel = new Label(L("界面语言", "UI Language"));
            langLabel.style.marginRight = 6;
            langRow.Add(langLabel);

            // Shows the CURRENT language (中 / EN), not the switch target. 显示当前语言（中 / EN），而非切换目标。
            var langButton = new Button(ToggleLanguage) { text = _chineseUi ? "中" : "EN" };
            langButton.tooltip = L("切换界面语言 (中/英)", "Switch UI language (EN/CN)");
            langButton.style.width = 40;
            langRow.Add(langButton);

            _editorPanel.Add(langRow);
        }
    }
}
