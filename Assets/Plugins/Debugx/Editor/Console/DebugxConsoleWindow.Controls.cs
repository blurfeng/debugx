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
        private ToolbarToggle _runtimeToggle;
        private VisualElement _runtimePanel;
        private bool _runtimeVisible;

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

        private VisualElement BuildRuntimePanel()
        {
            var panel = new VisualElement();
            panel.style.display = DisplayStyle.None; // hidden until the Runtime toggle is turned on. 打开“运行时”开关前隐藏。
            panel.style.paddingLeft = 6;
            panel.style.paddingRight = 6;
            panel.style.paddingTop = 4;
            panel.style.paddingBottom = 4;
            panel.style.borderBottomWidth = 1;
            panel.style.borderBottomColor = DebugxConsoleStyle.RuntimePanelBorderColor;
            return panel;
        }

        // Fired when project settings are (re)applied; refresh the member-dependent controls.
        // 项目设置（重新）应用时触发；刷新依赖成员的控件。
        private void OnSettingsApplied()
        {
            if (_runtimeVisible) RefreshRuntimePanel();
            Repaint();
        }

        // Rebuilds the runtime panel from the current Debugx state and play-mode. Called on show / play-mode change /
        // settings apply — so it reflects live values at those moments.
        // 依据当前 Debugx 状态与 Play 模式重建运行时面板。在 显示 / Play 模式变化 / 设置应用 时调用，故这些时刻反映最新值。
        private void RefreshRuntimePanel()
        {
            if (_runtimePanel == null) return;
            _runtimePanel.Clear();

            bool playing = Application.isPlaying;

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

            DebugxProjectSettings settings = DebugxProjectSettings.Instance;
            if (settings != null && settings.members != null && settings.members.Length > 0)
            {
                var membersTitle = new Label(L("成员开关", "Members"));
                membersTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                membersTitle.style.marginTop = 3;
                runtimeGroup.Add(membersTitle);

                foreach (DebugxMemberInfo m in settings.members)
                {
                    if (m == null) continue;
                    int key = m.key;
                    string sig = string.IsNullOrEmpty(m.signature) ? "Member" : m.signature;
                    var t = new Toggle($"[{key}] {sig}") { value = Debugx.MemberIsEnable(key) };
                    t.RegisterValueChangedCallback(evt => Debugx.SetMemberEnable(key, evt.newValue));
                    runtimeGroup.Add(t);
                }
            }

            _runtimePanel.Add(runtimeGroup);

            if (!playing)
            {
                var hint = new Label(L("以上仅在游戏运行时可设置。", "The above can only be set during Play mode."));
                hint.style.color = DebugxConsoleStyle.HintColor;
                hint.style.whiteSpace = WhiteSpace.Normal;
                _runtimePanel.Add(hint);
            }

            // --- Test log toggles (always editable in the Editor) ---
            var testTitle = new Label(L("测试", "Test"));
            testTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            testTitle.style.marginTop = 5;
            _runtimePanel.Add(testTitle);

            var awake = new Toggle(L("Awake 测试打印", "Awake test log")) { value = DebugxStaticData.EnableAwakeTestLog };
            awake.RegisterValueChangedCallback(evt => DebugxStaticData.EnableAwakeTestLog = evt.newValue);
            _runtimePanel.Add(awake);

            var update = new Toggle(L("Update 测试打印", "Update test log")) { value = DebugxStaticData.EnableUpdateTestLog };
            update.RegisterValueChangedCallback(evt => DebugxStaticData.EnableUpdateTestLog = evt.newValue);
            _runtimePanel.Add(update);
        }
    }
}
