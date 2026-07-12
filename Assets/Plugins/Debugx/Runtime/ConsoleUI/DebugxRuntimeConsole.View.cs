using System;
using System.Collections.Generic;
using System.Text;
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
        // Name tag on each list row's root element, so a pointer-down can tell a row click from an empty-area click by
        // walking up from the event target (see OnListPointerDown). 每个列表行根元素的名字标签，使指针按下时可从事件目标
        // 向上回溯来区分行点击与空白区点击（见 OnListPointerDown）。
        private const string RowName = "debugx-row";

        private VisualElement _openButton;
        private VisualElement _panelRoot;
        private ListView _listView;
        private Label _detailMessage;
        private VisualElement _stackContainer;
        private TextField _searchField;
        // Centered "No results for …" overlay shown over the list when an active search matches nothing.
        // 搜索有内容但无匹配时，覆盖在列表上居中显示的 “No results for …” 提示。
        private Label _noResultsLabel;

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
        private bool _showTimestamp = true;  // optional timestamp column (default ON), toggled by the "Time" toolbar toggle. 可选时间戳列（默认开启），由 “Time” 工具栏开关切换。
        private int _selectedIndex = -1;

        // Hold-to-clear state: a quick tap on Clear does nothing (guards against accidental clears); holding for
        // HoldToClearMs fills _clearFill 0→100% across the button, then clears. The repeating tick is paused on release.
        // 长按清空状态：轻点 Clear 无效（防误触）；按住 HoldToClearMs 会让 _clearFill 在按钮上 0→100% 填充后清空。松手即暂停重复计时。
        private const long HoldToClearMs = 1000;
        private Button _clearButton;
        private VisualElement _clearFill;
        private IVisualElementScheduledItem _clearHoldTick;
        private long _clearHoldStartMs = -1;

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
            panel.style.height = Length.Percent(66);
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

            bar.Add(BuildClearButton());

            // Copy the selected entry (message + stack) to the system clipboard. GUIUtility.systemCopyBuffer works at runtime.
            // 复制选中条目（消息 + 堆栈）到系统剪贴板。GUIUtility.systemCopyBuffer 在运行时可用。
            var copy = new Button(CopySelected) { text = "Copy" };
            StyleToolbarButton(copy);
            bar.Add(copy);

            // Capture the toggle to sync its initial checkbox to the persisted/default state (see LoadViewPrefs); without
            // this, BuildCheckToggle starts unchecked regardless of _store.CollapseMode. 捕获 Toggle 以把初始勾选同步到
            // 持久化/默认状态（见 LoadViewPrefs）；否则 BuildCheckToggle 一律起始未勾，无视 _store.CollapseMode。
            bar.Add(BuildCheckToggle("Collapse", out var collapseToggle, evt =>
            {
                _store.CollapseMode = evt.newValue ? LogCollapser.Mode.ByMessage : LogCollapser.Mode.Off;
                ForceRefresh();
            }));
            collapseToggle.SetValueWithoutNotify(_store.CollapseMode != LogCollapser.Mode.Off);

            bar.Add(BuildCheckToggle("Debugx Only", out var debugxOnlyToggle, evt =>
            {
                _criteria.OnlyDebugx = evt.newValue;
                OnCriteriaChanged();
            }));
            debugxOnlyToggle.SetValueWithoutNotify(_criteria.OnlyDebugx);

            // Member (Debugx category) filter — opens a custom popup (BuildMemberPopup, added to the panel). 成员（Debugx 分类）过滤——打开自建弹层（BuildMemberPopup，挂在面板上）。
            bar.Add(BuildMemberButton());

            // Runtime SOURCE switches (B8) — a separate popup; these change actual printing, not just the view. 运行时源头开关（B8）——独立弹层；改的是真实打印，非视图过滤。
            bar.Add(BuildSourceButton());

            // No flex spacer here: with flexWrap the toolbar items simply flow left-to-right and wrap; a growing spacer
            // would shove the right-hand group onto its own line with a large gap. 不放弹性占位：换行布局下让条目自左向右排并换行；撑开占位会把右侧组挤到独立一行、留大空隙。

            // Optional timestamp column (display-only toggle; rebind the visible rows, no filter change).
            // 可选时间戳列（纯显示开关；重绑可见行，不改过滤）。
            // Initialize the checkbox to the field default (ON) without firing the callback, so the toggle state matches
            // the already-shown timestamp column. 用字段默认值（开启）初始化勾选框且不触发回调，使勾选状态与已显示的时间戳列一致。
            bar.Add(BuildCheckToggle("Time", out var timeToggle, evt =>
            {
                _showTimestamp = evt.newValue;
                _listView.RefreshItems();
            }));
            timeToggle.SetValueWithoutNotify(_showTimestamp);

            // Net-tag filter (B7): a compact cycle button All -> Server -> Client. netTag 过滤（B7）：紧凑的循环按钮 全部→Server→Client。
            _netButton = new Button(CycleNetTag) { text = "Net: All" };
            StyleToolbarButton(_netButton);
            UpdateNetButtonText(); // reflect the persisted/default net-tag mode. 反映持久化/默认的网络标签模式。
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
                // Multi-select so Ctrl+click (toggle) / Shift+click (range) + Ctrl/Cmd+C mirror the Editor Console.
                // These modifier gestures are desktop-only; touch still selects a single row by tapping.
                // 多选，使 Ctrl+点击（切换）/ Shift+点击（范围）+ Ctrl/Cmd+C 对齐 Editor 版。组合键仅桌面端；触屏仍是单击选中一行。
                selectionType = SelectionType.Multiple,
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = _rows,
            };
            _listView.style.flexGrow = 1;
            _listView.selectionChanged += OnSelectionChanged;
            _listView.RegisterCallback<KeyDownEvent>(OnListKeyDown); // Ctrl/Cmd+C copy (Shift = message only). Ctrl/Cmd+C 复制（Shift 仅消息）。
            // Deselect on empty-area click. Capture phase on the ListView itself so it always fires (before the ListView's
            // own selection logic) regardless of which internal element is under the pointer. 空白区点击取消选中。注册在
            // ListView 自身的捕获阶段，无论指针下是哪个内部元素都必定触发（早于 ListView 自身的选择逻辑）。
            _listView.RegisterCallback<PointerDownEvent>(OnListPointerDown, TrickleDown.TrickleDown);
            // Hide ListView's built-in "List is empty" placeholder so it never collides with our "No results" overlay.
            // 隐藏 ListView 内置的 “List is empty” 占位，避免与我们的 “No results” 提示重叠。
            _listView.RegisterCallback<GeometryChangedEvent>(_ => HideListEmptyLabel());

            // Wrapper hosting the list + a centered "No results" overlay (see UpdateNoResultsLabel). PickingMode.Ignore lets
            // clicks pass through to the list's own empty-area deselect handling.
            // 容器承载列表 + 居中的 “No results” 提示（见 UpdateNoResultsLabel）。PickingMode.Ignore 让点击穿透到列表自身的空白区取消选中逻辑。
            var wrapper = new VisualElement();
            wrapper.style.flexGrow = 1;
            wrapper.Add(_listView);

            _noResultsLabel = new Label { enableRichText = false, pickingMode = PickingMode.Ignore };
            _noResultsLabel.style.position = Position.Absolute;
            _noResultsLabel.style.left = 0;
            _noResultsLabel.style.right = 0;
            _noResultsLabel.style.top = 0;
            _noResultsLabel.style.bottom = 0;
            _noResultsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _noResultsLabel.style.color = DebugxRuntimeConsoleStyle.HintColor;
            _noResultsLabel.style.fontSize = DebugxRuntimeConsoleStyle.NoResultsFontSize;
            _noResultsLabel.style.display = DisplayStyle.None;
            wrapper.Add(_noResultsLabel);

            return wrapper;
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement { name = RowName }; // tagged so OnListPointerDown can distinguish row vs empty clicks. 打标签，供 OnListPointerDown 区分行/空白点击。
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
            // Rounded pill background behind the collapse count, matching the Editor Console. The row is Center-aligned,
            // so the badge hugs its text vertically and reads as a small pill rather than a full-height bar.
            // 折叠计数后的圆角药丸背景，与 Editor 版一致。行是 Center 对齐，故徽标按文字高度收缩，呈小药丸而非撑满整行的竖条。
            badge.style.backgroundColor = DebugxRuntimeConsoleStyle.BadgeBgColor;
            badge.style.paddingLeft = DebugxRuntimeConsoleStyle.BadgePaddingH;
            badge.style.paddingRight = DebugxRuntimeConsoleStyle.BadgePaddingH;
            badge.style.paddingTop = DebugxRuntimeConsoleStyle.BadgePaddingV;
            badge.style.paddingBottom = DebugxRuntimeConsoleStyle.BadgePaddingV;
            badge.style.borderTopLeftRadius = DebugxRuntimeConsoleStyle.BadgeCornerRadius;
            badge.style.borderTopRightRadius = DebugxRuntimeConsoleStyle.BadgeCornerRadius;
            badge.style.borderBottomLeftRadius = DebugxRuntimeConsoleStyle.BadgeCornerRadius;
            badge.style.borderBottomRightRadius = DebugxRuntimeConsoleStyle.BadgeCornerRadius;

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
            // Gentler wheel step: UI Toolkit's default scrolls this small pane too far per notch. 更平缓的滚轮步长：UI Toolkit 默认每格把这块小面板滚太多。
            scroll.mouseWheelScrollSize = DebugxRuntimeConsoleStyle.DetailWheelScrollSize;

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
                if (frame.HideInCallstack) continue; // [HideInCallstack] forwarder frames are always dropped. [HideInCallstack] 转发帧始终剔除。
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
            // The detail pane's search highlight is built from the current query (ApplySearchHighlight), but the
            // survived-primary path in ReconcileSelection deliberately does NOT re-render the detail (the entry is
            // unchanged). So after a search/filter change, refresh the selected detail here to keep its highlight in
            // sync with the list rows. Only fires on user control changes, never per-tick, so there is no flood cost.
            // 详情面板的搜索高亮基于当前查询（ApplySearchHighlight）构建，但 ReconcileSelection 中“主行存活”分支刻意不重绘详情
            //（条目未变）。故搜索/过滤变化后在此刷新选中项详情，使其高亮与列表行一致。仅在用户操作时触发、绝不每帧，故无洪泛开销。
            RefreshSelectedDetail();
        }

        private void RefreshSelectedDetail()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _rows.Count)
                UpdateDetail(_rows[_selectedIndex].Entry);
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
            // Remember the WHOLE selection by stable SequenceId BEFORE the row list is rebuilt: eviction/filtering shifts
            // positional indices, so raw indices would then point at different entries. primarySeq (the detail-pane row)
            // is captured too so it stays the driver after the rebuild.
            // 重建行列表前按稳定 SequenceId 记住整份选择：淘汰/过滤会让位置索引偏移，原索引之后会指向别的条目。primarySeq
            //（详情面板所指行）一并记住，使其在重建后仍是驱动项。
            long primarySeq = (_selectedIndex >= 0 && _selectedIndex < _rows.Count && _rows[_selectedIndex].Entry != null)
                ? _rows[_selectedIndex].Entry.SequenceId
                : -1;
            var selectedSeqs = new List<long>();
            foreach (int i in _listView.selectedIndices)
                if (i >= 0 && i < _rows.Count && _rows[i].Entry != null)
                    selectedSeqs.Add(_rows[i].Entry.SequenceId);

            _rows.Clear();
            IReadOnlyList<CollapsedRow> src = _store.Rows;
            for (int i = 0; i < src.Count; i++)
                _rows.Add(src[i]);

            _listView.RefreshItems();
            UpdateCounts();
            ReapplyMemberFilterOnDrift(); // keep a new member visible under an active partial filter. 让新成员在启用部分过滤时仍可见。
            ReconcileSelection(selectedSeqs, primarySeq);

            // Tail: auto-scroll to newest only while stuck to the bottom (scrolling up pauses it). 贴底时才自动滚到最新（上滚暂停）。
            EnsureListScrollHook();
            if (_stickToBottom && _rows.Count > 0)
                _listView.ScrollToItem(_rows.Count - 1);

            UpdateNoResultsLabel();
        }

        // Hide ListView's built-in "List is empty" placeholder (class name differs across Unity versions) so it never
        // shows under our own overlay. 隐藏 ListView 内置的 “List is empty” 占位（类名随 Unity 版本不同），避免显示在我们提示之下。
        private void HideListEmptyLabel()
        {
            if (_listView == null) return;
            VisualElement empty = _listView.Q(className: "unity-collection-view__empty-label")
                                  ?? _listView.Q(className: "unity-list-view__empty-label");
            if (empty != null) empty.style.display = DisplayStyle.None;
        }

        // Show a centered "No results for \"…\"" overlay when an active search matches nothing; hide it otherwise. An empty
        // list with no search text (a fresh/cleared console, or a member/type filter) shows nothing, matching the Editor Console.
        // 当搜索有内容却无匹配时，居中显示 “No results for \"…\"” 提示；其他情况隐藏。无搜索文本的空列表（刚清空的控制台，或成员/类型过滤）
        // 不显示提示，与 Editor 版 Console 一致。
        private void UpdateNoResultsLabel()
        {
            if (_noResultsLabel == null) return;
            bool show = _rows.Count == 0 && !_criteria.Search.IsEmpty;
            _noResultsLabel.text = show ? $"No results for \"{_criteria.Search.Text}\"" : string.Empty;
            _noResultsLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Re-align the ListView selection with the entries it held before the rebuild, keyed by SequenceId. Restores the
        // WHOLE multi-selection (highlight + Copy targets) after eviction/filtering shifts the rows, and keeps the detail
        // pane on the primary row (or promotes the first survivor if the primary is gone). SetSelectionWithoutNotify
        // avoids re-firing OnSelectionChanged. Clears everything when all selected entries are gone.
        // 按 SequenceId 把 ListView 选中项重新对齐到重建前所持有的条目：淘汰/过滤导致行偏移后恢复整份多选（高亮 + Copy 目标），
        // 并让详情面板保持在主行（主行消失则提升第一个仍存活项）。SetSelectionWithoutNotify 避免再次触发 OnSelectionChanged。
        // 全部选中条目都消失时清空。
        private void ReconcileSelection(List<long> selectedSeqs, long primarySeq)
        {
            if (selectedSeqs.Count == 0) return; // nothing was selected. 原本无选中。

            var seqSet = new HashSet<long>(selectedSeqs);
            var newIndices = new List<int>();
            int primaryIndex = -1;
            for (int i = 0; i < _rows.Count; i++)
            {
                DebugxLogEntry e = _rows[i].Entry;
                if (e == null || !seqSet.Contains(e.SequenceId)) continue;
                newIndices.Add(i);
                if (e.SequenceId == primarySeq) primaryIndex = i;
            }

            if (newIndices.Count == 0)
            {
                // Every selected entry was evicted or filtered out. 所有选中条目都被淘汰或过滤掉。
                _selectedIndex = -1;
                _listView.ClearSelection();
                UpdateDetail(null);
                return;
            }

            if (primaryIndex >= 0)
            {
                _selectedIndex = primaryIndex; // detail already shows this entry. 详情已是此条目。
            }
            else
            {
                // Primary row gone but others survive: promote the first survivor for the detail pane. 主行消失但仍有存活项：提升第一个供详情显示。
                _selectedIndex = newIndices[0];
                UpdateDetail(_rows[_selectedIndex].Entry);
            }
            _listView.SetSelectionWithoutNotify(newIndices);
        }

        private void EnsureListScrollHook()
        {
            if (_listScroll != null || _listView == null) return;
            _listScroll = _listView.Q<ScrollView>();
            if (_listScroll == null) return;
            _listScroll.verticalScroller.valueChanged += OnListScrolled;
            // RefreshView calls ScrollToItem synchronously in the same frame the rows changed, before the ScrollView has
            // recomputed its content height / scroller.highValue, so it lands one item short of the bottom — and never
            // corrects once the log stream stops (no further RefreshView fires). Re-run ScrollToItem when the content
            // geometry has actually been recomputed (highValue now fresh) so it reaches the true bottom.
            // RefreshView 在改行的同一帧同步调用 ScrollToItem，早于 ScrollView 重算内容高度/scroller.highValue，故会差最新一行——
            // 且日志流一停就不再触发 RefreshView、永久停在差一点处。待内容几何真正重算后（highValue 已刷新）再补一次，落到真正底部。
            _listScroll.contentContainer.RegisterCallback<GeometryChangedEvent>(OnListContentGeometryChanged);
        }

        // Fires after the list content has been laid out (e.g. new rows added). Only tails when stuck to the bottom, so a
        // user who scrolled up to read history is left alone. Scrolling shifts the content by transform, not layout, so it
        // does not re-trigger this — no feedback loop.
        // 列表内容完成布局后触发（如新增行）。仅在贴底时尾随，故上滚查看历史的用户不受打扰。滚动改的是 transform 而非布局，
        // 不会再次触发本回调——无反馈环。
        private void OnListContentGeometryChanged(GeometryChangedEvent evt)
        {
            if (!_stickToBottom || _rows.Count == 0) return;
            _listView.ScrollToItem(_rows.Count - 1);
        }

        // Clear the selection when the click did NOT land on a data row (i.e. the empty area below the rows), so Copy
        // reverts to Copy-All and the detail pane clears — the native Console does the same. Detection walks up from the
        // event target looking for one of our RowName rows: coordinate-free and version-independent (no reliance on which
        // internal ListView element is under an empty click). A row hit leaves the selection to the ListView.
        // 当点击未落在数据行上（即行下方空白区）时清除选中，使 Copy 回到复制全部、详情清空——原生 Console 亦如此。判定从
        // 事件目标向上回溯查找我们的 RowName 行：不依赖坐标、不依赖版本（无需判断空白点击命中的是哪个 ListView 内部元素）。
        // 命中行则把选中交给 ListView。
        private void OnListPointerDown(PointerDownEvent evt)
        {
            var el = evt.target as VisualElement;
            while (el != null && el != _listView)
            {
                // Clicked inside a data row → keep selection. Also keep it when the click lands on the ListView's own
                // scrollbar (a Scroller descendant): dragging/clicking the scrollbar to browse history must NOT wipe the
                // current (multi-)selection or the detail/Copy target — only clicks on the empty area below rows should.
                // 点在数据行内 → 保留选中。点在 ListView 自身的滚动条（Scroller 后代）上也保留：用滚动条拖动/点击浏览历史时
                // 不应清空当前（多）选与详情/Copy 目标；只有点击行下方空白区才清空。
                if (el.name == RowName || el is Scroller) return;
                el = el.hierarchy.parent;
            }
            ClearListSelection();
        }

        private void ClearListSelection()
        {
            _selectedIndex = -1;
            _listView.ClearSelection();
            UpdateDetail(null);
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

        // Clear is a *hold-to-clear* button: a quick tap does nothing (clicking was too easy to mis-trigger); holding for
        // HoldToClearMs sweeps a translucent red fill across the button and only then clears the console.
        // Clear 为*长按清空*按钮：轻点无效（点击太易误触）；按住 HoldToClearMs 会有半透明红色填充扫过按钮，满后才清空 Console。
        private VisualElement BuildClearButton()
        {
            // A wrapper hosts the real Button (kept so it themes/measures like the other toolbar buttons) plus the fill.
            // The fill must be a *sibling*, NOT a child of the Button: Yoga skips a TextElement's text-measure once it has
            // children, collapsing the button to its padding — so the overlay lives on the wrapper instead.
            // wrapper 承载真正的 Button（保持与其它工具栏按钮一致的主题/测量）与填充条。填充条必须是按钮的*兄弟*而非子元素：
            // 一旦 TextElement 有子元素，Yoga 就跳过其文字测量，按钮会塌缩成只剩 padding——故覆盖层挂在 wrapper 上。
            var wrapper = new VisualElement();
            wrapper.style.position = Position.Relative; // anchor for the absolute fill. 绝对定位填充条的锚点。
            wrapper.style.overflow = Overflow.Hidden;   // clip the fill to the button box. 将填充裁剪到按钮框内。
            wrapper.style.marginLeft = 2;               // toolbar spacing normally set by StyleToolbarButton; moved here so
            wrapper.style.marginRight = 2;              // the fill aligns to the button box. 工具栏间距移到 wrapper，使填充条对齐按钮框。

            var clear = new Button { text = "Clear" }; // no click Action — clearing is driven by the hold below. 无点击动作——清空由下方长按驱动。
            StyleToolbarButton(clear);
            clear.style.marginLeft = 0;                // margins live on the wrapper. 外边距改由 wrapper 承担。
            clear.style.marginRight = 0;
            wrapper.Add(clear);

            // Progress fill: an absolutely-positioned overlay stretched to the button's height, its width driven 0→100%
            // while held. pickingMode = Ignore so it never intercepts the pointer. Added after the button → drawn on top;
            // translucent, so "Clear" reads through it. 进度填充：绝对定位覆盖层，撑满按钮高度，按住时宽度 0→100%；pickingMode =
            // Ignore 不拦截指针；在按钮之后添加 → 绘制在其上；半透明，"Clear" 文字可透出。
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.position = Position.Absolute;
            fill.style.left = 0;
            fill.style.top = 0;
            fill.style.bottom = 0;
            fill.style.width = Length.Percent(0f);
            fill.style.backgroundColor = DebugxRuntimeConsoleStyle.HoldFillColor;
            wrapper.Add(fill);

            // Registered in the trickle-down (capture) phase so they run BEFORE the Button's built-in Clickable, which
            // handles pointer events in the target/bubble phase and may stop their propagation. The Clickable also captures
            // the pointer on down, so PointerLeave won't fire mid-hold; release (PointerUp) is the reliable cancel, with
            // Leave/Cancel as best-effort safety nets. 在捕获阶段注册，确保先于按钮内置 Clickable（在目标/冒泡阶段处理指针、可能
            // 中止其传播）执行。Clickable 还会在按下时捕获指针，故按住途中不触发 PointerLeave；松手（PointerUp）为可靠取消，Leave/Cancel 兜底。
            clear.RegisterCallback<PointerDownEvent>(OnClearPointerDown, TrickleDown.TrickleDown);
            clear.RegisterCallback<PointerUpEvent>(_ => CancelClearHold(), TrickleDown.TrickleDown);
            clear.RegisterCallback<PointerLeaveEvent>(_ => CancelClearHold(), TrickleDown.TrickleDown);
            clear.RegisterCallback<PointerCancelEvent>(_ => CancelClearHold(), TrickleDown.TrickleDown);

            _clearButton = clear;
            _clearFill = fill;
            return wrapper;
        }

        private void OnClearPointerDown(PointerDownEvent evt)
        {
            if (evt.button > 0) return; // ignore right/middle mouse; left mouse (0) and touch pass. 忽略右/中键；左键(0)与触屏放行。
            if (_clearButton == null || _clearFill == null) return;
            _clearHoldStartMs = -1; // captured from TimerState.now on the first tick, to measure in panel time. 首帧从 TimerState.now 取起点，用面板时间度量。
            _clearHoldTick?.Pause();
            _clearHoldTick = _clearButton.schedule.Execute(OnClearHoldTick).Every(16);
        }

        private void OnClearHoldTick(TimerState state)
        {
            if (_clearHoldStartMs < 0) _clearHoldStartMs = state.now;
            float ratio = Mathf.Clamp01((state.now - _clearHoldStartMs) / (float)HoldToClearMs);
            _clearFill.style.width = Length.Percent(ratio * 100f);
            if (ratio >= 1f)
            {
                CancelClearHold();
                ClearConsole();
            }
        }

        private void CancelClearHold()
        {
            _clearHoldTick?.Pause();
            _clearHoldTick = null;
            if (_clearFill != null) _clearFill.style.width = Length.Percent(0f);
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

        // Ctrl/Cmd+C on the focused list copies the selected entries WITH their stacks; adding Shift copies the message
        // only. Mirrors the Editor Console. Desktop-only (no keyboard on touch). 焦点在列表上时 Ctrl/Cmd+C 复制选中条目
        //（含堆栈）；加 Shift 仅复制消息。对齐 Editor 版。仅桌面端（触屏无键盘）。
        private void OnListKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.C && (evt.ctrlKey || evt.commandKey))
            {
                CopyEntries(GetSelectedEntries(), withStack: !evt.shiftKey);
                evt.StopPropagation();
            }
        }

        // Copy button: the selected entries (with stacks), or — when nothing is selected — ALL currently visible rows.
        // Copy 按钮：选中条目（含堆栈）；未选中时复制当前全部可见行。
        private void CopySelected()
        {
            List<DebugxLogEntry> targets = GetSelectedEntries();
            if (targets.Count == 0) targets = GetAllVisibleEntries();
            CopyEntries(targets, withStack: true);
        }

        private List<DebugxLogEntry> GetSelectedEntries()
        {
            var list = new List<DebugxLogEntry>();
            if (_listView == null) return list;
            var indices = new List<int>(_listView.selectedIndices);
            indices.Sort(); // copy in visible order regardless of click order. 无论点击顺序，按可见顺序复制。
            foreach (int i in indices)
                if (i >= 0 && i < _rows.Count) list.Add(_rows[i].Entry);
            return list;
        }

        private List<DebugxLogEntry> GetAllVisibleEntries()
        {
            var list = new List<DebugxLogEntry>(_rows.Count);
            for (int i = 0; i < _rows.Count; i++) list.Add(_rows[i].Entry);
            return list;
        }

        // Plain message + raw stack per entry; a trailing blank line separates stacked entries. GUIUtility.systemCopyBuffer
        // works at runtime. 每条为纯文本消息 + 原始堆栈；带堆栈的多条之间用空行分隔。GUIUtility.systemCopyBuffer 运行时可用。
        private static void CopyEntries(List<DebugxLogEntry> entries, bool withStack)
        {
            if (entries == null || entries.Count == 0) return;

            var sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                DebugxLogEntry e = entries[i];
                sb.Append(e.PlainText);
                if (withStack && !string.IsNullOrEmpty(e.StackTrace))
                    sb.Append('\n').Append(e.StackTrace).Append('\n');
            }
            GUIUtility.systemCopyBuffer = sb.ToString().TrimEnd('\n');
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

        // Wrap the active search's matches with a <mark> highlight, delegating to the shared, UI-agnostic
        // SearchHighlighter so the Editor Console and this runtime Console paint matches identically.
        // 给当前搜索命中套上 <mark> 高亮，委托给共享、与 UI 无关的 SearchHighlighter，使 Editor 版与本运行时版 Console 的命中着色一致。
        private string ApplySearchHighlight(string richText) =>
            Apply(richText, _criteria.Search, DebugxRuntimeConsoleStyle.SearchHighlightHex);

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
        private static Button BuildCloseButton(Action onClick)
        {
            var b = new Button(onClick) { text = "X" }; // plain ASCII 'X' as the close glyph — kept simple so runtime default fonts always render it. 用普通 ASCII 'X' 作关闭符，确保运行时默认字体一定能渲染。
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
        
        /// <summary>
        /// Return <paramref name="richText"/> with the active search's matches wrapped in a &lt;mark=#hex&gt; highlight.
        /// The search matches the VISIBLE text, so we can't splice at those offsets directly: walk the rich string once,
        /// skipping &lt;...&gt; tag spans, to build the visible text plus a visible-&gt;rich index map; find the matches in
        /// the visible text; then splice &lt;mark&gt;/&lt;/mark&gt; at the mapped rich positions. &lt;mark&gt; only paints a
        /// background, so member &lt;color&gt; foregrounds survive. No-op when the search is empty or a regex (the search
        /// boxes only ever set plain substring text, so regex highlight is intentionally skipped) — the input is returned
        /// unchanged, so the caller can wrap every row unconditionally.
        /// 返回把当前搜索命中套上 &lt;mark=#hex&gt; 高亮后的 <paramref name="richText"/>。搜索匹配的是可见文本，无法直接按其偏移拼接：
        /// 一次遍历富文本、跳过 &lt;...&gt; 标签段，构建可见文本 + “可见→富文本”下标映射；在可见文本里找命中；再在映射后的富文本位置
        /// 拼入 &lt;mark&gt;/&lt;/mark&gt;。&lt;mark&gt; 只画背景，故成员 &lt;color&gt; 前景色得以保留。搜索为空或为正则时不处理（搜索框只设
        /// 纯子串，正则高亮有意跳过）——原样返回，故调用方可无条件地对每一行套用。
        /// </summary>
        /// <param name="richText">The rich text to highlight. 待高亮的富文本。</param>
        /// <param name="query">The active search query. 当前搜索查询。</param>
        /// <param name="highlightHex">RRGGBBAA hex for the &lt;mark&gt; background (no leading '#'). &lt;mark&gt; 背景的 RRGGBBAA 十六进制（不含 '#'）。</param>
        public static string Apply(string richText, SearchQuery query, string highlightHex)
        {
            if (string.IsNullOrEmpty(richText)) return richText;
            if (query.IsEmpty || query.UseRegex) return richText;

            string needle = query.Text;
            int n = richText.Length;

            // Build the visible text + a map from each visible char (and an end sentinel) to its index in richText.
            // 构建可见文本 + 每个可见字符（含末尾哨兵）到其在 richText 中下标的映射。
            var visible = new StringBuilder(n);
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

            StringComparison cmp = query.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string open = "<mark=#" + highlightHex + ">";

            var outSb = new StringBuilder(n + 16);
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
    }
}
