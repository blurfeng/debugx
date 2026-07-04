using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Console.Runtime
{
    /// <summary>
    /// Runtime Console — the runtime SOURCE switches (B8). Unlike the display filters (type / member / Debugx-only /
    /// search), these change the DLL's live logging state (<c>Debugx.enableLog</c> / <c>enableLogMember</c> /
    /// <c>logThisKeyMemberOnly</c> / per-member <c>SetMemberEnable</c>) — i.e. whether logs are ACTUALLY produced and
    /// written to file, not merely what the viewer shows. Kept in a separate popup with its own title so users don't
    /// confuse "filtered out of the view" with "not printed at all" (§6-B8). Members are listed from the configured
    /// member set (all of them), not just the ones seen in the buffer. Always usable — the runtime Console is always
    /// in Play, so there is no "edit mode disabled" state like the Editor Console has.
    /// 运行时 Console —— 运行时源头开关（B8）。不同于显示过滤（类型/成员/仅Debugx/搜索），这些改的是 DLL 的实时日志状态
    /// （<c>Debugx.enableLog</c> / <c>enableLogMember</c> / <c>logThisKeyMemberOnly</c> / 逐成员 <c>SetMemberEnable</c>）——
    /// 即日志是否“真的产生并写文件”，而非查看器显示什么。放在带独立标题的单独弹层里，避免用户混淆“被过滤出视图”与“根本没打印”
    /// （§6-B8）。成员来自配置成员全集（全部），而非仅缓冲里出现过的。始终可用——运行时 Console 永远在 Play，故没有 Editor 版
    /// 那种“编辑期置灰”的状态。
    /// </summary>
    public partial class DebugxRuntimeConsole
    {
        private Button _sourceButton;
        private VisualElement _sourcePopup;
        private VisualElement _sourceListContainer;
        private bool _sourcePopupOpen;

        private const float SourcePopupWidth = 260f;

        private Button BuildSourceButton()
        {
            _sourceButton = new Button(ToggleSourcePopup) { text = "Source" };
            StyleToolbarButton(_sourceButton);
            return _sourceButton;
        }

        private VisualElement BuildSourcePopup()
        {
            var popup = new VisualElement();
            popup.style.position = Position.Absolute;
            popup.style.left = 8; // fallback; repositioned under the Source button on open. 兜底值；打开时定位到 Source 按钮下方。
            popup.style.top = 48;
            popup.style.width = SourcePopupWidth;
            popup.style.maxHeight = Length.Percent(75);
            popup.style.backgroundColor = DebugxRuntimeConsoleStyle.ToolbarBg;
            popup.style.flexDirection = FlexDirection.Column;
            popup.style.display = DisplayStyle.None;
            SetBorder(popup, DebugxRuntimeConsoleStyle.BorderColor, 1);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 4;
            header.style.paddingRight = 4;
            header.style.paddingTop = 2;
            header.style.paddingBottom = 2;

            var titleLabel = new Label("Runtime Switches");
            titleLabel.style.color = DebugxRuntimeConsoleStyle.TextColor;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.flexGrow = 1;
            Button close = BuildCloseButton(() => SetSourcePopup(false));
            header.Add(titleLabel);
            header.Add(close);
            popup.Add(header);

            // Make the semantic distinction explicit (§6-B8): these change real printing, not the view filter.
            // 明示语义区分（§6-B8）：这些改的是真实打印，不是视图过滤。
            var note = new Label("Changes real printing, not the view.");
            note.style.color = DebugxRuntimeConsoleStyle.TimestampColor;
            note.style.fontSize = DebugxRuntimeConsoleStyle.FontSizeSmall;
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.paddingLeft = 4;
            note.style.paddingBottom = 2;
            popup.Add(note);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            _sourceListContainer = new VisualElement();
            scroll.Add(_sourceListContainer);
            popup.Add(scroll);

            _sourcePopup = popup;
            return popup;
        }

        private void ToggleSourcePopup() => SetSourcePopup(!_sourcePopupOpen);

        private void SetSourcePopup(bool open)
        {
            _sourcePopupOpen = open;
            if (open)
            {
                SetMemberPopup(false); // only one popup at a time. 同时只开一个弹层。
                RebuildSourceList();
                PositionPopupUnderButton(_sourcePopup, _sourceButton, SourcePopupWidth);
            }
            if (_sourcePopup != null)
                _sourcePopup.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Rebuild from live Debugx state each open so it reflects current values. Reads/writes DLL runtime fields.
        // 每次打开都从 Debugx 实时状态重建，反映当前值。读写 DLL 运行时字段。
        private void RebuildSourceList()
        {
            _sourceListContainer.Clear();

            _sourceListContainer.Add(BuildSourceToggle("Enable Log", Debugx.enableLog, v => Debugx.enableLog = v));
            _sourceListContainer.Add(BuildSourceToggle("Enable Log Member", Debugx.enableLogMember, v => Debugx.enableLogMember = v));

            // logThisKeyMemberOnly: 0 = off; otherwise only that member key prints. 0=关闭；否则仅打印该 key 成员。
            var onlyRow = new VisualElement();
            onlyRow.style.flexDirection = FlexDirection.Row;
            onlyRow.style.alignItems = Align.Center;
            onlyRow.style.marginLeft = 4;
            onlyRow.style.marginTop = 2;
            onlyRow.style.marginBottom = 2;

            var onlyLabel = new Label("Only Key (0=off)");
            onlyLabel.style.color = DebugxRuntimeConsoleStyle.TextColor;
            onlyLabel.style.fontSize = DebugxRuntimeConsoleStyle.FontSizeSmall;
            onlyLabel.style.marginRight = 6;

            var onlyField = new IntegerField { value = Debugx.logThisKeyMemberOnly };
            onlyField.style.flexGrow = 1;
            onlyField.RegisterValueChangedCallback(evt => Debugx.logThisKeyMemberOnly = evt.newValue);

            onlyRow.Add(onlyLabel);
            onlyRow.Add(onlyField);
            _sourceListContainer.Add(onlyRow);

            // Per-member switches — the full configured member set (so members that have not logged yet can be toggled).
            // 逐成员开关——配置成员全集（故尚未打印过的成员也能开关）。
            DebugxProjectSettings settings = DebugxProjectSettings.Instance;
            if (settings != null && settings.members != null && settings.members.Length > 0)
            {
                var membersTitle = new Label("Members");
                membersTitle.style.color = DebugxRuntimeConsoleStyle.TextColor;
                membersTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                membersTitle.style.marginTop = 4;
                membersTitle.style.marginLeft = 4;
                _sourceListContainer.Add(membersTitle);

                foreach (DebugxMemberInfo m in settings.members)
                {
                    if (m == null) continue;
                    int key = m.key;
                    string sig = string.IsNullOrEmpty(m.signature) ? "Member" : m.signature;
                    _sourceListContainer.Add(BuildSourceToggle($"[{key}] {sig}", Debugx.MemberIsEnable(key),
                        v => Debugx.SetMemberEnable(key, v)));
                }
            }
        }

        // A tight [checkbox][label] row reflecting + writing a bool (reuses the toolbar's BuildCheckToggle layout).
        // 一个紧贴的 [勾选框][文字] 行，反映并写入一个 bool（复用工具栏 BuildCheckToggle 布局）。
        private VisualElement BuildSourceToggle(string text, bool value, System.Action<bool> onChange)
        {
            VisualElement group = BuildCheckToggle(text, out Toggle toggle, evt => onChange(evt.newValue));
            toggle.SetValueWithoutNotify(value);
            return group;
        }
    }
}
