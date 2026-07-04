using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Console.Runtime
{
    /// <summary>
    /// Runtime Console — the UIToolkit view. Builds the panel (toolbar + search + list/detail split) with plain runtime
    /// controls only (the Editor's Toolbar* controls live in UnityEditor.UIElements and are unavailable here), binds the
    /// shared <see cref="DebugxLogStore"/> rows, and refreshes on demand. Kept in a partial file so the MonoBehaviour
    /// lifecycle half stays focused.
    /// 运行时 Console —— UIToolkit 视图。仅用纯运行时控件构建面板（工具栏 + 搜索 + 列表/详情双栏）——Editor 的 Toolbar*
    /// 控件位于 UnityEditor.UIElements，此处不可用；绑定共享 <see cref="DebugxLogStore"/> 的行并按需刷新。拆到 partial
    /// 文件让 MonoBehaviour 生命周期那半保持聚焦。
    /// </summary>
    public partial class DebugxRuntimeConsole
    {
        private VisualElement _openButton;
        private VisualElement _panelRoot;
        private ListView _listView;
        private Label _detailMessage;
        private VisualElement _stackContainer;
        private TextField _searchField;

        private Button _logButton, _warnButton, _errorButton;

        private ScrollView _listScroll;
        private bool _stickToBottom = true; // auto-scroll to newest only while the list is at the bottom. 仅当列表贴底时自动滚到最新。
        private bool _showTimestamp;         // optional timestamp column, toggled by the "Time" toolbar toggle. 可选时间戳列，由 “Time” 工具栏开关切换。
        private int _selectedIndex = -1;

        // ---------- Build ----------

        private void BuildUI(VisualElement root)
        {
            // Let clicks pass through the (empty) root to the game; only interactive children capture input, so the game
            // still receives touches/clicks while the console is hidden. Children remain pickable despite the parent.
            // 让点击穿透（空的）根节点到游戏；仅交互子元素捕获输入，故 Console 隐藏时游戏仍接收触摸/点击。父级 Ignore 不影响子元素可拾取。
            root.pickingMode = PickingMode.Ignore;

            _openButton = BuildOpenButton();
            root.Add(_openButton);

            _panelRoot = BuildPanel();
            root.Add(_panelRoot);
        }

        private VisualElement BuildOpenButton()
        {
            var btn = new Button(() => SetVisible(true)) { text = "Debugx" };
            btn.style.position = Position.Absolute;
            btn.style.left = 8;
            btn.style.top = 8;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.paddingTop = 3;
            btn.style.paddingBottom = 3;
            btn.style.backgroundColor = DebugxRuntimeConsoleStyle.OpenButtonBg;
            btn.style.color = DebugxRuntimeConsoleStyle.TextColor;
            btn.style.fontSize = DebugxRuntimeConsoleStyle.FontSizeSmall;
            return btn;
        }

        private VisualElement BuildPanel()
        {
            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = 8;
            panel.style.top = 8;
            panel.style.width = Length.Percent(66);
            panel.style.height = Length.Percent(58);
            panel.style.minWidth = 320;
            panel.style.backgroundColor = DebugxRuntimeConsoleStyle.PanelBg;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.overflow = Overflow.Hidden; // clip any stray child overflow so nothing spills past the panel/window. 裁剪子元素溢出，杜绝越出面板/窗口。
            SetBorder(panel, DebugxRuntimeConsoleStyle.BorderColor, 1);

            panel.Add(BuildToolbar());
            panel.Add(BuildSearchRow());

            // Manual vertical layout instead of TwoPaneSplitView: the split view's drag handle uses a resize cursor,
            // which at runtime spams "Runtime cursors other than the default cursor need to be defined using a texture".
            // List fills the remaining space; the detail/stack pane is a fixed-height bottom strip.
            // 用手写竖向布局代替 TwoPaneSplitView：分栏的拖拽手柄用 resize 光标，运行时会刷屏 “Runtime cursors ... 需要用贴图定义”。
            // 列表填充剩余空间；详情/堆栈为固定高度的底栏。
            VisualElement listPane = BuildListPane();
            listPane.style.flexGrow = 1;

            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.flexShrink = 0;
            divider.style.backgroundColor = DebugxRuntimeConsoleStyle.BorderColor;

            VisualElement detailPane = BuildDetailPane();
            detailPane.style.flexGrow = 0;
            detailPane.style.flexShrink = 0;
            detailPane.style.height = DebugxRuntimeConsoleStyle.DetailPaneHeight;

            panel.Add(listPane);
            panel.Add(divider);
            panel.Add(detailPane);

            // Member filter popup overlays the panel (added last = on top; hidden until the Members button opens it).
            // 成员过滤弹层覆盖在面板上（最后添加 = 置顶；隐藏，直到 Members 按钮打开它）。
            panel.Add(BuildMemberPopup());

            // Close (×) pinned to the panel's top-right corner, above everything.
            // 关闭（×）钉在面板右上角，置于所有内容之上。
            Button close = BuildCloseButton(() => SetVisible(false));
            close.style.position = Position.Absolute;
            close.style.top = 2;
            close.style.right = 2;
            panel.Add(close);

            return panel;
        }

        private VisualElement BuildToolbar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap; // wrap to a new line instead of overflowing the panel at narrow widths. 窄宽时换行而非溢出面板。
            bar.style.alignItems = Align.Center;
            bar.style.backgroundColor = DebugxRuntimeConsoleStyle.ToolbarBg;
            bar.style.paddingLeft = 4;
            bar.style.paddingRight = 4;
            bar.style.paddingTop = 2;
            bar.style.paddingBottom = 2;

            var title = new Label("Debugx Console");
            title.style.color = DebugxRuntimeConsoleStyle.TextColor;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginRight = 8;
            bar.Add(title);

            var clear = new Button(ClearConsole) { text = "Clear" };
            StyleToolbarButton(clear);
            bar.Add(clear);

            // Copy the selected entry (message + stack) to the system clipboard. GUIUtility.systemCopyBuffer works at runtime.
            // 复制选中条目（消息 + 堆栈）到系统剪贴板。GUIUtility.systemCopyBuffer 在运行时可用。
            var copy = new Button(CopySelected) { text = "Copy" };
            StyleToolbarButton(copy);
            bar.Add(copy);

            bar.Add(BuildCheckToggle("Collapse", out _, evt =>
            {
                _store.CollapseMode = evt.newValue ? LogCollapser.Mode.ByMessage : LogCollapser.Mode.Off;
                ForceRefresh();
            }));

            bar.Add(BuildCheckToggle("Debugx Only", out _, evt =>
            {
                _criteria.OnlyDebugx = evt.newValue;
                OnCriteriaChanged();
            }));

            // Member (Debugx category) filter — opens a custom popup (BuildMemberPopup, added to the panel). 成员（Debugx 分类）过滤——打开自建弹层（BuildMemberPopup，挂在面板上）。
            bar.Add(BuildMemberButton());

            // No flex spacer here: with flexWrap the toolbar items simply flow left-to-right and wrap; a growing spacer
            // would shove the right-hand group onto its own line with a large gap. 不放弹性占位：换行布局下让条目自左向右排并换行；撑开占位会把右侧组挤到独立一行、留大空隙。

            // Optional timestamp column (display-only toggle; rebind the visible rows, no filter change).
            // 可选时间戳列（纯显示开关；重绑可见行，不改过滤）。
            bar.Add(BuildCheckToggle("Time", out _, evt =>
            {
                _showTimestamp = evt.newValue;
                _listView.RefreshItems();
            }));

            _logButton = BuildCountButton(DebugxRuntimeConsoleStyle.LogColor, () =>
            {
                _criteria.ShowLog = !_criteria.ShowLog;
                UpdateCountButtonStates();
                OnCriteriaChanged();
            });
            _warnButton = BuildCountButton(DebugxRuntimeConsoleStyle.WarnColor, () =>
            {
                _criteria.ShowWarning = !_criteria.ShowWarning;
                UpdateCountButtonStates();
                OnCriteriaChanged();
            });
            _errorButton = BuildCountButton(DebugxRuntimeConsoleStyle.ErrorColor, () =>
            {
                _criteria.ShowError = !_criteria.ShowError;
                UpdateCountButtonStates();
                OnCriteriaChanged();
            });
            bar.Add(_logButton);
            bar.Add(_warnButton);
            bar.Add(_errorButton);

            // The panel's Close (×) is an absolute top-right overlay (see BuildPanel), not a toolbar item — reserve the
            // corner so wrapped toolbar rows don't slide under it. 面板 Close（×）是右上角绝对定位覆盖层（见 BuildPanel），非工具栏条目——预留右上角，避免换行的工具栏挤到它下面。
            bar.style.paddingRight = 24;

            return bar;
        }

        private VisualElement BuildSearchRow()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.paddingLeft = 4;
            container.style.paddingRight = 4;
            container.style.paddingTop = 2;
            container.style.paddingBottom = 2;
            container.style.overflow = Overflow.Hidden; // never let the field push past the panel width. 不让输入框撑破面板宽度。

            var label = new Label("Search");
            label.style.color = DebugxRuntimeConsoleStyle.TextColor;
            label.style.fontSize = DebugxRuntimeConsoleStyle.FontSizeSmall;
            label.style.flexShrink = 0;
            label.style.marginRight = 6;
            container.Add(label);

            _searchField = new TextField();
            _searchField.style.flexGrow = 1;
            _searchField.style.flexShrink = 1;
            _searchField.style.minWidth = 40; // shrink instead of overflowing on narrow panels. 窄面板时收缩而非溢出。
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _criteria.Search = new SearchQuery { Text = evt.newValue };
                OnCriteriaChanged();
            });
            container.Add(_searchField);
            return container;
        }

        // ---------- List pane ----------

        private VisualElement BuildListPane()
        {
            _listView = new ListView
            {
                fixedItemHeight = DebugxRuntimeConsoleStyle.ListItemHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = _rows,
            };
            _listView.style.flexGrow = 1;
            _listView.selectionChanged += OnSelectionChanged;
            return _listView;
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;

            // A thin severity color bar instead of an icon glyph (runtime fonts may lack symbol glyphs).
            // 用细的严重级别色条代替图标字形（运行时字体可能缺少符号字形）。
            var sev = new VisualElement { name = "sev" };
            sev.style.width = DebugxRuntimeConsoleStyle.SeverityBarWidth;
            sev.style.flexShrink = 0;
            sev.style.marginRight = 6;
            sev.style.alignSelf = Align.Stretch;

            // Optional timestamp column, shown/hidden per BindRow by the "Time" toggle. 可选时间戳列，由 BindRow 按 “Time” 开关显隐。
            var time = new Label { name = "time" };
            time.style.width = DebugxRuntimeConsoleStyle.TimestampWidth;
            time.style.flexShrink = 0;
            time.style.color = DebugxRuntimeConsoleStyle.TimestampColor;
            time.style.fontSize = DebugxRuntimeConsoleStyle.FontSizeSmall;
            time.style.unityTextAlign = TextAnchor.MiddleLeft;
            time.style.display = DisplayStyle.None;

            // Net tag badge (S/C), shown only when the entry carries a Server/Client tag. 网络标签徽标（S/C），仅当条目带 Server/Client 标签时显示。
            var net = new Label { name = "net" };
            net.style.flexShrink = 0;
            net.style.minWidth = 16;
            net.style.marginRight = 4;
            net.style.unityTextAlign = TextAnchor.MiddleCenter;
            net.style.unityFontStyleAndWeight = FontStyle.Bold;
            net.style.display = DisplayStyle.None;

            var msg = new Label { name = "msg", enableRichText = true };
            msg.style.flexGrow = 1;
            msg.style.overflow = Overflow.Hidden;
            msg.style.whiteSpace = WhiteSpace.NoWrap;
            msg.style.color = DebugxRuntimeConsoleStyle.TextColor;
            msg.style.unityTextAlign = TextAnchor.MiddleLeft;

            var badge = new Label { name = "badge" };
            badge.style.flexShrink = 0;
            badge.style.minWidth = 22;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.color = DebugxRuntimeConsoleStyle.TextColor;

            row.Add(sev);
            row.Add(time);
            row.Add(net);
            row.Add(msg);
            row.Add(badge);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            CollapsedRow row = _rows[index];
            DebugxLogEntry e = row.Entry;

            element.Q<VisualElement>("sev").style.backgroundColor = SeverityColor(e.LogType);

            var time = element.Q<Label>("time");
            if (_showTimestamp)
            {
                time.text = e.Timestamp.ToString("HH:mm:ss");
                time.style.display = DisplayStyle.Flex;
            }
            else
            {
                time.style.display = DisplayStyle.None;
            }

            var net = element.Q<Label>("net");
            if (e.NetTag != NetTag.None)
            {
                net.text = e.NetTag == NetTag.Server ? "S" : "C";
                net.style.color = e.NetTag == NetTag.Server
                    ? DebugxRuntimeConsoleStyle.NetServerColor
                    : DebugxRuntimeConsoleStyle.NetClientColor;
                net.style.display = DisplayStyle.Flex;
            }
            else
            {
                net.style.display = DisplayStyle.None;
            }

            element.Q<Label>("msg").text = SingleLine(e.RichText);

            var badge = element.Q<Label>("badge");
            if (row.Count > 1)
            {
                badge.text = CountText(row.Count);
                badge.style.display = DisplayStyle.Flex;
            }
            else
            {
                badge.style.display = DisplayStyle.None;
            }
        }

        // ---------- Detail pane ----------

        private VisualElement BuildDetailPane()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.backgroundColor = DebugxRuntimeConsoleStyle.DetailBg;

            _detailMessage = new Label { enableRichText = true };
            _detailMessage.style.whiteSpace = WhiteSpace.Normal;
            _detailMessage.style.color = DebugxRuntimeConsoleStyle.TextColor;
            _detailMessage.style.paddingLeft = 6;
            _detailMessage.style.paddingTop = 6;
            _detailMessage.style.paddingRight = 6;

            _stackContainer = new VisualElement();
            _stackContainer.style.paddingLeft = 6;
            _stackContainer.style.paddingTop = 6;
            _stackContainer.style.paddingBottom = 6;

            scroll.Add(_detailMessage);
            scroll.Add(_stackContainer);
            return scroll;
        }

        // Runtime degradation: stack frames are text only (no source navigation — that needs UnityEditor).
        // 运行时降级：堆栈帧仅文本（无源码跳转——那需要 UnityEditor）。
        private void UpdateDetail(DebugxLogEntry e)
        {
            if (_stackContainer == null) return;
            _stackContainer.Clear();
            if (e == null)
            {
                _detailMessage.text = string.Empty;
                return;
            }

            _detailMessage.text = e.RichText;

            List<StackFrameInfo> frames = StackTraceParser.Parse(e.StackTrace);
            foreach (StackFrameInfo frame in frames)
            {
                var line = new Label(frame.RawLine) { enableRichText = false };
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.color = DebugxRuntimeConsoleStyle.TextColor;
                line.style.paddingTop = 1;
                line.style.paddingBottom = 1;
                _stackContainer.Add(line);
            }
        }

        // ---------- Refresh ----------

        private void OnCriteriaChanged()
        {
            ApplyCriteria();
            ForceRefresh();
        }

        private void ApplyCriteria() => _store.SetFilterCriteria(_criteria);

        private void ForceRefresh()
        {
            _store.MarkViewDirty();
            _store.TryRebuildView();
            RefreshView();
        }

        private void RefreshView()
        {
            _rows.Clear();
            IReadOnlyList<CollapsedRow> src = _store.Rows;
            for (int i = 0; i < src.Count; i++)
                _rows.Add(src[i]);

            _listView.RefreshItems();
            UpdateCounts();
            ReapplyMemberFilterOnDrift(); // keep a new member visible under an active partial filter. 让新成员在启用部分过滤时仍可见。

            // Tail: auto-scroll to newest only while stuck to the bottom (scrolling up pauses it). 贴底时才自动滚到最新（上滚暂停）。
            EnsureListScrollHook();
            if (_stickToBottom && _rows.Count > 0)
                _listView.ScrollToItem(_rows.Count - 1);
        }

        private void EnsureListScrollHook()
        {
            if (_listScroll != null || _listView == null) return;
            _listScroll = _listView.Q<ScrollView>();
            if (_listScroll == null) return;
            _listScroll.verticalScroller.valueChanged += OnListScrolled;
        }

        private void OnListScrolled(float value)
        {
            if (_listScroll == null) return;
            Scroller s = _listScroll.verticalScroller;
            _stickToBottom = value >= s.highValue - 1f;
        }

        private void UpdateCounts()
        {
            LogStatistics s = _store.Statistics;
            _logButton.text = CountText(s.LogCount);
            _warnButton.text = CountText(s.WarningCount);
            _errorButton.text = CountText(s.ErrorCount);
        }

        private void UpdateCountButtonStates()
        {
            SetActiveOpacity(_logButton, _criteria.ShowLog);
            SetActiveOpacity(_warnButton, _criteria.ShowWarning);
            SetActiveOpacity(_errorButton, _criteria.ShowError);
        }

        // ---------- Events ----------

        private void OnSelectionChanged(IEnumerable<object> _)
        {
            _selectedIndex = _listView.selectedIndex;
            if (_selectedIndex >= 0 && _selectedIndex < _rows.Count)
                UpdateDetail(_rows[_selectedIndex].Entry);
            else
                UpdateDetail(null);
        }

        private void ClearConsole()
        {
            if (_store == null) return;
            _store.Clear();
            _selectedIndex = -1;
            _listView?.ClearSelection();
            UpdateDetail(null);
            ForceRefresh();
        }

        // Copy the selected entry's plain message + raw stack to the system clipboard (no-op when nothing is selected).
        // 复制选中条目的纯文本消息 + 原始堆栈到系统剪贴板（未选中时不做事）。
        private void CopySelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count) return;
            DebugxLogEntry e = _rows[_selectedIndex].Entry;
            string text = e.PlainText ?? string.Empty;
            if (!string.IsNullOrEmpty(e.StackTrace))
                text += "\n" + e.StackTrace;
            GUIUtility.systemCopyBuffer = text;
        }

        // ---------- Helpers ----------

        private static Color SeverityColor(LogType type)
        {
            switch (LogStatistics.SeverityOf(type))
            {
                case LogSeverity.Warning: return DebugxRuntimeConsoleStyle.WarnColor;
                case LogSeverity.Error: return DebugxRuntimeConsoleStyle.ErrorColor;
                default: return DebugxRuntimeConsoleStyle.LogColor;
            }
        }

        private static string SingleLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace('\r', ' ').Replace('\n', ' ');
        }

        private static string CountText(int n) =>
            n > DebugxRuntimeConsoleStyle.CountOverflowThreshold
                ? DebugxRuntimeConsoleStyle.CountOverflowThreshold + "+"
                : n.ToString();

        private Button BuildCountButton(Color color, Action onClick)
        {
            var btn = new Button(onClick) { text = "0" };
            StyleToolbarButton(btn);
            btn.style.color = color;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.minWidth = 34;
            return btn;
        }

        private static void SetActiveOpacity(VisualElement el, bool active)
        {
            if (el != null) el.style.opacity = active ? 1f : DebugxRuntimeConsoleStyle.InactiveOpacity;
        }

        private static void SetBorder(VisualElement el, Color color, float w)
        {
            el.style.borderLeftWidth = w;
            el.style.borderRightWidth = w;
            el.style.borderTopWidth = w;
            el.style.borderBottomWidth = w;
            el.style.borderLeftColor = color;
            el.style.borderRightColor = color;
            el.style.borderTopColor = color;
            el.style.borderBottomColor = color;
        }

        private static void StyleToolbarButton(Button b)
        {
            b.style.marginLeft = 2;
            b.style.marginRight = 2;
            b.style.paddingLeft = 6;
            b.style.paddingRight = 6;
            b.style.color = DebugxRuntimeConsoleStyle.TextColor;
            b.style.backgroundColor = DebugxRuntimeConsoleStyle.ButtonBg; // dark fill so light text is legible. 深色底，使浅色文字清晰。
            b.style.fontSize = DebugxRuntimeConsoleStyle.FontSizeSmall;
        }

        // A tight [checkbox][label] pair. The built-in Toggle label reserves a wide field min-width that pushes the box
        // far from the text, so we use an empty Toggle (checkbox only) + our own Label right beside it.
        // 一个紧贴的 [勾选框][文字] 组。内置 Toggle 的 label 会预留很宽的字段最小宽度，把勾选框推得离文字很远，故改用
        // 空 Toggle（仅勾选框）+ 紧挨着的自绘 Label。
        private static VisualElement BuildCheckToggle(string text, out Toggle toggle, EventCallback<ChangeEvent<bool>> onChange)
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.marginLeft = 4;
            group.style.marginRight = 4;

            toggle = new Toggle();
            // Defensive: if the theme still reserves width for an (empty) label element, collapse it. 防御：若主题仍为空 label 预留宽度，收起它。
            Label inner = toggle.Q<Label>();
            if (inner != null) { inner.style.minWidth = 0; inner.style.width = StyleKeyword.Auto; }
            toggle.RegisterValueChangedCallback(onChange);

            var label = new Label(text);
            label.style.color = DebugxRuntimeConsoleStyle.TextColor;
            label.style.fontSize = DebugxRuntimeConsoleStyle.FontSizeSmall;
            label.style.marginLeft = 3;

            group.Add(toggle);
            group.Add(label);
            return group;
        }

        // A red "×" close icon (used by the panel and the member popup), pinned by the caller to the top-right.
        // 一个红色 “×” 关闭图标（面板与成员弹层共用），由调用方钉到右上角。
        private static Button BuildCloseButton(System.Action onClick)
        {
            var b = new Button(onClick) { text = "×" }; // U+00D7 multiplication sign — present in default fonts. 乘号，默认字体都有。
            b.style.color = new Color(1f, 0.42f, 0.38f);
            b.style.backgroundColor = DebugxRuntimeConsoleStyle.ButtonBg;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.fontSize = 15;
            b.style.paddingLeft = 6;
            b.style.paddingRight = 6;
            b.style.paddingTop = 0;
            b.style.paddingBottom = 0;
            b.style.marginLeft = 2;
            b.style.marginRight = 2;
            return b;
        }
    }
}
