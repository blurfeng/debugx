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

        private VisualElement _logButton, _warnButton, _errorButton;
        private Label _logCount, _warnCount, _errorCount;
        private Button _netButton;

        // Severity icon set + branding icon, loaded once from Resources. Null when a file is missing → graceful fallback
        // to color-bar rows / colored count text. 严重级别图标集 + 品牌图标，从 Resources 加载一次。文件缺失时为 null →
        // 优雅回退到色条行 / 彩色计数文字。
        private Texture2D _iconLog, _iconWarn, _iconError, _iconArticle;
        private bool _iconsLoaded;
        private bool _useIconRows;

        private ScrollView _listScroll;
        private bool _stickToBottom = true; // auto-scroll to newest only while the list is at the bottom. 仅当列表贴底时自动滚到最新。
        private bool _showTimestamp;         // optional timestamp column, toggled by the "Time" toolbar toggle. 可选时间戳列，由 “Time” 工具栏开关切换。
        private int _selectedIndex = -1;

        // ---------- Build ----------

        private void BuildUI(VisualElement root)
        {
            EnsureIconsLoaded();

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
            // Branding icon (icon_article) + "Debugx" text. Built as an icon/label row inside the Button rather than using
            // Button.text, so the glyph sits beside the label. 品牌图标（icon_article）+ “Debugx” 文字。以按钮内的图标/文字
            // 行构建，而非用 Button.text，让图标排在文字旁边。
            var btn = new Button(() => SetVisible(true));
            btn.style.position = Position.Absolute;
            btn.style.left = 8;
            btn.style.top = 8;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.paddingTop = 3;
            btn.style.paddingBottom = 3;
            btn.style.backgroundColor = DebugxRuntimeConsoleStyle.OpenButtonBg;
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;

            if (_iconArticle != null)
            {
                var img = new Image { image = _iconArticle, scaleMode = ScaleMode.ScaleToFit };
                img.style.width = DebugxRuntimeConsoleStyle.OpenButtonIconSize;
                img.style.height = DebugxRuntimeConsoleStyle.OpenButtonIconSize;
                img.style.marginRight = 4;
                img.style.flexShrink = 0;
                btn.Add(img);
            }

            var label = new Label("Debugx");
            label.style.color = DebugxRuntimeConsoleStyle.TextColor;
            label.style.fontSize = DebugxRuntimeConsoleStyle.FontSizeSmall;
            btn.Add(label);
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

            // Filter / source popups overlay the panel (hidden until their toolbar buttons open them).
            // 过滤 / 源头弹层覆盖在面板上（隐藏，直到各自工具栏按钮打开）。
            panel.Add(BuildMemberPopup());
            panel.Add(BuildSourcePopup());

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

            // Runtime SOURCE switches (B8) — a separate popup; these change actual printing, not just the view. 运行时源头开关（B8）——独立弹层；改的是真实打印，非视图过滤。
            bar.Add(BuildSourceButton());

            // No flex spacer here: with flexWrap the toolbar items simply flow left-to-right and wrap; a growing spacer
            // would shove the right-hand group onto its own line with a large gap. 不放弹性占位：换行布局下让条目自左向右排并换行；撑开占位会把右侧组挤到独立一行、留大空隙。

            // Optional timestamp column (display-only toggle; rebind the visible rows, no filter change).
            // 可选时间戳列（纯显示开关；重绑可见行，不改过滤）。
            bar.Add(BuildCheckToggle("Time", out _, evt =>
            {
                _showTimestamp = evt.newValue;
                _listView.RefreshItems();
            }));

            // Net-tag filter (B7): a compact cycle button All -> Server -> Client. netTag 过滤（B7）：紧凑的循环按钮 全部→Server→Client。
            _netButton = new Button(CycleNetTag) { text = "Net: All" };
            StyleToolbarButton(_netButton);
            bar.Add(_netButton);

            _logButton = BuildCountButton(_iconLog, DebugxRuntimeConsoleStyle.LogColor, out _logCount, () =>
            {
                _criteria.ShowLog = !_criteria.ShowLog;
                UpdateCountButtonStates();
                OnCriteriaChanged();
            });
            _warnButton = BuildCountButton(_iconWarn, DebugxRuntimeConsoleStyle.WarnColor, out _warnCount, () =>
            {
                _criteria.ShowWarning = !_criteria.ShowWarning;
                UpdateCountButtonStates();
                OnCriteriaChanged();
            });
            _errorButton = BuildCountButton(_iconError, DebugxRuntimeConsoleStyle.ErrorColor, out _errorCount, () =>
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

            // Severity indicator: an info/warning/error icon when the icon set loaded, else a thin color bar fallback
            // (runtime fonts may lack symbol glyphs, and the icons ship as Resources). Both are built; BindRow shows one.
            // 严重级别指示：图标集已加载时用 info/warning/error 图标，否则回退为细色条（运行时字体可能缺符号字形，图标随
            // Resources 分发）。两者都构建，由 BindRow 择一显示。
            var sev = new VisualElement { name = "sevbar" };
            sev.style.width = DebugxRuntimeConsoleStyle.SeverityBarWidth;
            sev.style.flexShrink = 0;
            sev.style.marginRight = 6;
            sev.style.alignSelf = Align.Stretch;
            sev.style.display = _useIconRows ? DisplayStyle.None : DisplayStyle.Flex;

            var sevIcon = new Image { name = "sevicon", scaleMode = ScaleMode.ScaleToFit };
            sevIcon.style.width = DebugxRuntimeConsoleStyle.RowIconSize;
            sevIcon.style.height = DebugxRuntimeConsoleStyle.RowIconSize;
            sevIcon.style.marginRight = DebugxRuntimeConsoleStyle.RowIconMarginRight;
            sevIcon.style.flexShrink = 0;
            sevIcon.style.display = _useIconRows ? DisplayStyle.Flex : DisplayStyle.None;

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
            row.Add(sevIcon);
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

            if (_useIconRows)
                element.Q<Image>("sevicon").image = SeverityIcon(e.LogType);
            else
                element.Q<VisualElement>("sevbar").style.backgroundColor = SeverityColor(e.LogType);

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

            element.Q<Label>("msg").text = ApplySearchHighlight(SingleLine(e.RichText));

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

            _detailMessage.text = ApplySearchHighlight(e.RichText);

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
            // Remember the selected entry by its stable id BEFORE the row list is rebuilt: eviction shifts positional
            // indices, so a plain _selectedIndex would then point at a different entry. 重建行列表前按稳定 id 记住选中条目：
            // 淘汰会让位置索引偏移，仅凭 _selectedIndex 之后会指向另一条目。
            long selectedSeq = (_selectedIndex >= 0 && _selectedIndex < _rows.Count && _rows[_selectedIndex].Entry != null)
                ? _rows[_selectedIndex].Entry.SequenceId
                : -1;

            _rows.Clear();
            IReadOnlyList<CollapsedRow> src = _store.Rows;
            for (int i = 0; i < src.Count; i++)
                _rows.Add(src[i]);

            _listView.RefreshItems();
            UpdateCounts();
            ReapplyMemberFilterOnDrift(); // keep a new member visible under an active partial filter. 让新成员在启用部分过滤时仍可见。
            ReconcileSelection(selectedSeq);

            // Tail: auto-scroll to newest only while stuck to the bottom (scrolling up pauses it). 贴底时才自动滚到最新（上滚暂停）。
            EnsureListScrollHook();
            if (_stickToBottom && _rows.Count > 0)
                _listView.ScrollToItem(_rows.Count - 1);
        }

        // Re-align the ListView selection with the entry it pointed at before the rebuild, keyed by SequenceId. Keeps the
        // highlight, the detail pane and Copy all referring to the SAME entry after eviction/filtering shifts the rows;
        // clears the selection when that entry is gone. SetSelectionWithoutNotify avoids re-firing OnSelectionChanged
        // (the detail already shows this entry, so no rebuild is needed).
        // 按 SequenceId 把 ListView 选中项重新对齐到重建前所指的条目，使淘汰/过滤导致行偏移后，高亮、详情面板与 Copy 仍指向
        // 同一条目；该条目已消失时清除选中。SetSelectionWithoutNotify 避免再次触发 OnSelectionChanged（详情已是此条目，无需重建）。
        private void ReconcileSelection(long selectedSeq)
        {
            if (selectedSeq < 0) return; // nothing was selected. 原本无选中。

            int newIndex = -1;
            for (int i = 0; i < _rows.Count; i++)
            {
                DebugxLogEntry e = _rows[i].Entry;
                if (e != null && e.SequenceId == selectedSeq) { newIndex = i; break; }
            }

            if (newIndex == _selectedIndex) return; // still aligned. 仍对齐。

            if (newIndex >= 0)
            {
                _selectedIndex = newIndex;
                _listView.SetSelectionWithoutNotify(new[] { newIndex });
            }
            else
            {
                // The selected entry was evicted or filtered out. 选中条目已被淘汰或过滤掉。
                _selectedIndex = -1;
                _listView.ClearSelection();
                UpdateDetail(null);
            }
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
            _logCount.text = CountText(s.LogCount);
            _warnCount.text = CountText(s.WarningCount);
            _errorCount.text = CountText(s.ErrorCount);
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
            _stickToBottom = true; // a just-cleared (empty) list should resume tail-following newest logs. 刚清空的（空）列表应恢复尾随最新日志。
            ForceRefresh();
        }

        // Copy to the system clipboard: the selected entry, or — when nothing is selected — ALL currently visible rows
        // (A6.1 Copy / Copy All in one button). Each entry is plain message + raw stack. GUIUtility works at runtime.
        // 复制到系统剪贴板：选中条目；未选中时复制当前全部可见行（A6.1 复制 / 复制全部合到一个按钮）。每条为纯文本消息 + 原始堆栈。
        private void CopySelected()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _rows.Count)
            {
                GUIUtility.systemCopyBuffer = FormatEntryForCopy(_rows[_selectedIndex].Entry);
                return;
            }

            if (_rows.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _rows.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(FormatEntryForCopy(_rows[i].Entry));
            }
            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        private static string FormatEntryForCopy(DebugxLogEntry e)
        {
            string text = e.PlainText ?? string.Empty;
            if (!string.IsNullOrEmpty(e.StackTrace))
                text += "\n" + e.StackTrace;
            return text;
        }

        private void CycleNetTag()
        {
            switch (_criteria.NetTagMode)
            {
                case NetTagFilterMode.All: _criteria.NetTagMode = NetTagFilterMode.Server; break;
                case NetTagFilterMode.Server: _criteria.NetTagMode = NetTagFilterMode.Client; break;
                default: _criteria.NetTagMode = NetTagFilterMode.All; break;
            }
            UpdateNetButtonText();
            OnCriteriaChanged();
        }

        private void UpdateNetButtonText()
        {
            if (_netButton == null) return;
            switch (_criteria.NetTagMode)
            {
                case NetTagFilterMode.Server: _netButton.text = "Net: S"; break;
                case NetTagFilterMode.Client: _netButton.text = "Net: C"; break;
                default: _netButton.text = "Net: All"; break;
            }
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

        // Load the icon set once from Resources. _useIconRows gates the row/count-button icon path — off when any severity
        // icon is missing, so the Console degrades to color bars/text instead of blank gaps.
        // 从 Resources 加载图标集一次。_useIconRows 控制行/计数按钮的图标路径——任一严重级别图标缺失时关闭，使 Console 降级为
        // 色条/文字而非留空。
        private void EnsureIconsLoaded()
        {
            if (_iconsLoaded) return;
            _iconsLoaded = true;
            _iconLog = Resources.Load<Texture2D>(DebugxRuntimeConsoleStyle.IconInfoResource);
            _iconWarn = Resources.Load<Texture2D>(DebugxRuntimeConsoleStyle.IconWarningResource);
            _iconError = Resources.Load<Texture2D>(DebugxRuntimeConsoleStyle.IconErrorResource);
            _iconArticle = Resources.Load<Texture2D>(DebugxRuntimeConsoleStyle.IconArticleResource);
            _useIconRows = _iconLog != null && _iconWarn != null && _iconError != null;
        }

        private Texture2D SeverityIcon(LogType type)
        {
            switch (LogStatistics.SeverityOf(type))
            {
                case LogSeverity.Warning: return _iconWarn;
                case LogSeverity.Error: return _iconError;
                default: return _iconLog;
            }
        }

        private static string SingleLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace('\r', ' ').Replace('\n', ' ');
        }

        // Wrap the active search's matches inside a rich-text string with a <mark> highlight, without disturbing the
        // existing tags. The search matches the VISIBLE text, so we can't splice at those offsets directly: walk the rich
        // string once, skipping <...> tag spans, to build the visible text plus a visible->rich index map; find the
        // matches in the visible text; then splice <mark>/</mark> at the mapped rich positions. <mark> only paints a
        // background, so member <color> foregrounds survive. No-op when the search is empty or a regex (the runtime search
        // box only ever sets plain substring text, so regex highlight is intentionally skipped).
        // 给富文本里的当前搜索命中套上 <mark> 高亮，且不破坏已有标签。搜索匹配的是可见文本，无法直接按其偏移拼接：一次遍历
        // 富文本、跳过 <...> 标签段，构建可见文本 + “可见→富文本”下标映射；在可见文本里找命中；再在映射后的富文本位置拼入
        // <mark>/</mark>。<mark> 只画背景，故成员 <color> 前景色得以保留。搜索为空或为正则时不处理（运行时搜索框只设纯子串，
        // 正则高亮有意跳过）。
        private string ApplySearchHighlight(string richText)
        {
            if (string.IsNullOrEmpty(richText)) return richText;
            SearchQuery q = _criteria.Search;
            if (q.IsEmpty || q.UseRegex) return richText;

            string needle = q.Text;
            int n = richText.Length;

            // Build the visible text + a map from each visible char (and an end sentinel) to its index in richText.
            // 构建可见文本 + 每个可见字符（含末尾哨兵）到其在 richText 中下标的映射。
            var visible = new System.Text.StringBuilder(n);
            var richPos = new List<int>(n + 1);
            int i = 0;
            while (i < n)
            {
                char c = richText[i];
                if (c == '<')
                {
                    int close = richText.IndexOf('>', i + 1);
                    if (close < 0) break; // malformed trailing '<': stop scanning; the tail holds no matchable text. 残缺尾部 '<'：停止扫描；尾部无可匹配文本。
                    i = close + 1;        // skip the whole <...> tag. 跳过整个 <...> 标签。
                    continue;
                }
                richPos.Add(i);
                visible.Append(c);
                i++;
            }
            richPos.Add(n); // a match ending at the last visible char closes here. 命中止于最后可见字符时在此闭合。

            string hay = visible.ToString();
            if (hay.Length == 0 || needle.Length > hay.Length) return richText;

            StringComparison cmp = q.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string open = "<mark=#" + DebugxRuntimeConsoleStyle.SearchHighlightHex + ">";

            var outSb = new System.Text.StringBuilder(n + 16);
            int cursor = 0; // how far into richText we've copied. 已复制到 richText 的位置。
            int from = 0;   // search start in the visible text. 可见文本里的搜索起点。
            bool any = false;
            while (true)
            {
                int idx = hay.IndexOf(needle, from, cmp);
                if (idx < 0) break;
                int ro = richPos[idx];
                int rc = richPos[idx + needle.Length];
                if (ro >= cursor)
                {
                    outSb.Append(richText, cursor, ro - cursor);
                    outSb.Append(open);
                    outSb.Append(richText, ro, rc - ro);
                    outSb.Append("</mark>");
                    cursor = rc;
                    any = true;
                }
                from = idx + needle.Length;
            }
            if (!any) return richText;
            outSb.Append(richText, cursor, n - cursor);
            return outSb.ToString();
        }

        private static string CountText(int n) =>
            n > DebugxRuntimeConsoleStyle.CountOverflowThreshold
                ? DebugxRuntimeConsoleStyle.CountOverflowThreshold + "+"
                : n.ToString();

        // A native-Console-style count button: severity icon + a count label, toggling that type's filter. Falls back to
        // a color-tinted number when the icon is missing. Mirrors the Editor Console's MakeCountButton.
        // 原生 Console 风格的计数按钮：严重级别图标 + 计数标签，点击切换该类型过滤。图标缺失时回退为彩色数字。对齐 Editor
        // 版的 MakeCountButton。
        private VisualElement BuildCountButton(Texture2D icon, Color fallbackColor, out Label countLabel, Action onClick)
        {
            var btn = new VisualElement();
            StyleToolbarButton(btn);
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.minWidth = 34;

            if (icon != null)
            {
                var img = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                img.style.width = DebugxRuntimeConsoleStyle.CountIconSize;
                img.style.height = DebugxRuntimeConsoleStyle.CountIconSize;
                img.style.marginRight = DebugxRuntimeConsoleStyle.CountIconMarginRight;
                img.style.flexShrink = 0;
                btn.Add(img);
            }

            countLabel = new Label("0");
            countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            countLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            // No glyph → color the number by severity so the type stays readable. 无图标 → 用严重级别色标注数字，保留类型可读性。
            if (icon == null) countLabel.style.color = fallbackColor;
            btn.Add(countLabel);

            btn.RegisterCallback<ClickEvent>(_ => onClick());
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

        // Place a popup's top-left just under its trigger button, clamped to the panel's right edge. Shared by the
        // member-filter and runtime-source popups. worldBound and style.left share the panel coordinate space (UIToolkit
        // points, already scaled), so the delta needs no DPI conversion.
        // 把弹层左上角放到其触发按钮正下方，并对面板右边缘钳制。成员过滤与运行时源头两个弹层共用。worldBound 与 style.left
        // 同属面板坐标系（UIToolkit 点，已缩放），差值无需 DPI 换算。
        private void PositionPopupUnderButton(VisualElement popup, VisualElement button, float width)
        {
            if (popup == null || button == null || _panelRoot == null) return;

            Rect btn = button.worldBound;
            Rect panel = _panelRoot.worldBound;
            if (float.IsNaN(btn.x) || float.IsNaN(panel.x) || panel.width <= 0f) return; // layout not resolved yet. 布局尚未解析。

            float left = btn.x - panel.x;
            float top = btn.yMax - panel.y;

            float maxLeft = panel.width - width - 4f;
            if (left > maxLeft) left = maxLeft;
            if (left < 4f) left = 4f;

            popup.style.left = left;
            popup.style.top = top;
        }

        private static void StyleToolbarButton(VisualElement b)
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

        // A red "×" close icon (used by the panel and the popups). Shares StyleToolbarButton for matching margins/bg/font,
        // then forces a SQUARE: horizontal padding is dropped and, once laid out, the width is set to the resolved height
        // (so it matches the other buttons' natural height while being perfectly square).
        // 一个红色 “×” 关闭图标（面板与弹层共用）。复用 StyleToolbarButton 以匹配外边距/底色/字体，再强制成正方形：去掉左右内边距，
        // 并在布局完成后把宽度设为解析出的高度（既匹配其他按钮的自然高度，又是正方形）。
        private static Button BuildCloseButton(System.Action onClick)
        {
            var b = new Button(onClick) { text = "X" }; // U+00D7 multiplication sign — present in default fonts. 乘号，默认字体都有。
            StyleToolbarButton(b);
            b.style.color = new Color(1f, 0.42f, 0.38f); // red, overriding the toolbar text color. 红色，覆盖工具栏文字色。
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.paddingLeft = 0;
            b.style.paddingRight = 0;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float h = b.resolvedStyle.height;
                if (h > 0f && Mathf.Abs(b.resolvedStyle.width - h) > 0.5f)
                {
                    b.style.width = h;
                    b.style.minWidth = h;
                    b.style.maxWidth = h;
                }
            });
            return b;
        }
    }
}
