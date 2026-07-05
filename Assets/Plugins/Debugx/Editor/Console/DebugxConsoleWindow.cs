using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// The Editor-side Debugx Console: a UIToolkit log viewer that consumes the shared <see cref="DebugxLogStore"/>.
    /// This is the display layer only — all capture / buffering / filtering / collapsing / stats live in the shared
    /// model layer and are reused by the runtime Console (<c>DebugxRuntimeConsole</c>). Editor-only concerns (source
    /// navigation, Error Pause, Clear on Play) are wired here.
    /// Editor 端的 Debugx Console：消费共享层 <see cref="DebugxLogStore"/> 的 UIToolkit 日志查看器。这里只有显示层——
    /// 采集/缓冲/过滤/折叠/统计都在共享模型层，由运行时 Console（<c>DebugxRuntimeConsole</c>）一同复用。
    /// Editor 专属能力（源码跳转、错误暂停、进入 Play 清空）在此接线。
    /// </summary>
    public partial class DebugxConsoleWindow : EditorWindow
    {
        private const string PrefPrefix = "Debugx.Console.";

        [MenuItem("Window/Debugx/DebugxConsole", false, 4)]
        public static void Open()
        {
            var window = GetWindow<DebugxConsoleWindow>();
            // Reuse Unity's native Console tab icon so the window reads as a console at a glance (IconContent auto-picks the light/dark skin variant).
            // 复用原生 Console 的标签页图标，让窗口一眼可辨为控制台（IconContent 会自动匹配明/暗皮肤变体）。
            var icon = EditorGUIUtility.IconContent("UnityEditor.ConsoleWindow").image;
            window.titleContent = new GUIContent("Debugx Console", icon);
            window.minSize = DebugxConsoleStyle.MinWindowSize;
        }

        private DebugxLogStore _store;
        private readonly LogFilterCriteria _criteria = new LogFilterCriteria();
        private readonly List<CollapsedRow> _rows = new List<CollapsedRow>();

        private ListView _listView;
        // Detail pane: one IMGUIContainer draws message+stacktrace as a single selectable rich-text block (native Console
        // style), uniform across Unity versions. 详情面板：一个 IMGUIContainer 将 消息+堆栈 渲染为单个可选中富文本块（原生 Console 风格），各版本一致。
        private IMGUIContainer _detailImgui;
        private DebugxLogEntry _detailEntry;               // current detail entry; cache key + right-click copy target. 当前详情条目；缓存键 + 右键复制目标。
        private Vector2 _detailScrollPos;                  // cached scroll position for the detail block. 详情块缓存滚动位置。
        private string _detailCombinedText = string.Empty; // cached combined rich text (message + hyperlinked stack). 缓存的合并富文本（消息 + 带超链接堆栈）。
        private readonly GUIContent _detailContent = new GUIContent(string.Empty); // reused for CalcHeight/GetRect (no per-repaint alloc). 复用于 CalcHeight/GetRect（避免每帧分配）。
        private bool _detailScriptOnlyCache;               // ScriptOnly state the cache was built under; rebuild when it flips. 构建缓存时的 仅脚本 状态；翻转时重建。
        private GUIStyle _detailStyle;                     // lazily built "CN Message" (or fallback) clone. 惰性构建的 "CN Message"（或回退）克隆样式。
        private ScrollView _listScroll;      // the ListView's internal scroll view, for tail (stick-to-bottom) detection. ListView 内部滚动视图，用于 tail(贴底)检测。
        private bool _stickToBottom = true;  // auto-scroll to newest only while the list is at the bottom. 仅当列表贴底时自动滚到最新。

        private Toolbar _toolbar;
        private VisualElement _searchContainer; // flexible middle region that keeps the right group right-aligned. 弹性中部，使右侧分组保持靠右。
        private ToolbarButton _clearButton;
        private VisualElement _clearDivider; // absolute-positioned line inside Clear's right edge (custom height %). 置于 Clear 右边缘的绝对定位竖线（可自定义高度占比）。
        private ToolbarButton _clearDropdownButton;
        private Label _memberNameLabel, _memberCaretLabel; // Members button split into text + independently-sized caret. Members 按钮拆成文字 + 可独立调大小的三角。
        private ToolbarToggle _collapseToggle, _errorPauseToggle;
        private ToolbarSearchField _searchField;

        // Fixed group widths (per current language) used by the responsive show/hide logic. _wBase is the always-visible
        // left minimum EXCLUDING the counts — the counts' width is measured live in UpdateResponsive (it grows with the
        // digit count) so they are always reserved accurately and never overflow.
        // 供响应式显隐逻辑使用的固定分组宽度（按当前语言）。_wBase 是不含计数按钮的左侧常驻最小宽度——计数按钮宽度随位数增长，
        // 在 UpdateResponsive 内实时测量预留，从而始终精确预留、永不溢出。
        private float _wBase, _wSearch, _wFilters, _wEditorGroup;

        // Native-style count buttons (icon + count) for the three severities.
        // 三个严重级别的原生风格计数按钮（图标 + 计数）。
        private VisualElement _logButton, _warnButton, _errorButton;
        private Label _logCount, _warnCount, _errorCount;

        private int _selectedIndex = -1;
        private bool _clearOnPlay = true;
        private bool _clearOnRecompile = true;
        private bool _clearOnBuild = true;
        private bool _errorPause;
        private bool _chineseUi; // false = English (default). UI language, independent of system language. 默认英文，独立于系统语言。

        // Cached severity icons (loaded from the plugin Resources; shared by list rows and count buttons).
        // 缓存的严重级别图标（从插件 Resources 加载；列表行与计数按钮共用）。
        private Texture _iconLog, _iconWarn, _iconError;
        // Gray "_g" variants used on a count button when that severity's count is 0. 计数为 0 时计数按钮使用的灰色 "_g" 变体。
        private Texture _iconLogGray, _iconWarnGray, _iconErrorGray;

        private string L(string cn, string en) => _chineseUi ? cn : en;

        private void OnEnable()
        {
            _store = new DebugxLogStore();
            LoadPrefs();
            _store.Collector.EntryProduced += OnEntryForErrorPause;
            _store.Start();
            RestorePersistedLogs(); // repopulate the fresh buffer with logs kept across the domain reload. 用跨域重载留存的日志重新填充新缓冲。

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            DebugxProjectSettingsAsset.OnApplyTo.Bind(OnSettingsApplied);
            EnableCompileMirror();
        }

        private void OnDisable()
        {
            SavePersistedLogs(); // stash logs so they survive the coming domain reload / window close. 转存日志以熬过即将到来的域重载/关窗。
            if (_store != null)
            {
                _store.Collector.EntryProduced -= OnEntryForErrorPause;
                _store.Stop();
            }
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            DebugxProjectSettingsAsset.OnApplyTo.Unbind(OnSettingsApplied);
            DisableCompileMirror();
            SavePrefs();
        }

        public void CreateGUI()
        {
            // Severity icons now come from the plugin's Resources (shared with the runtime Console); fall back to Unity's
            // built-in console icons if a Resources file is missing. Gray "_g" variants are shown when a count is 0.
            // 严重级别图标改用插件 Resources（与运行时 Console 共用）；Resources 缺失时回退到 Unity 内置控制台图标。
            // 计数为 0 时显示灰色 "_g" 变体。
            _iconLog = LoadSeverityIcon("icon_info", "console.infoicon.sml");
            _iconWarn = LoadSeverityIcon("icon_warning", "console.warnicon.sml");
            _iconError = LoadSeverityIcon("icon_error", "console.erroricon.sml");
            _iconLogGray = Resources.Load<Texture2D>("icon_info_g");
            _iconWarnGray = Resources.Load<Texture2D>("icon_warning_g");
            _iconErrorGray = Resources.Load<Texture2D>("icon_error_g");

            VisualElement root = rootVisualElement;
            root.Add(BuildToolbar());

            _editorPanel = BuildEditorPanel();
            root.Add(_editorPanel);

            var split = new TwoPaneSplitView(1, DebugxConsoleStyle.DetailPaneHeight, TwoPaneSplitViewOrientation.Vertical);
            split.style.flexGrow = 1;
            split.Add(BuildListPane());

            // The visible divider is the detail pane's TOP BORDER: it sits exactly on the list/detail boundary, moves with
            // the splitter, and cleanly masks the seam so no list-row sliver bleeds below the line. The split view's own
            // floating dragline is hidden in StyleDetailDivider; its anchor is kept only as an invisible drag grab-strip.
            // 可见分隔线用详情面板的“上边框”绘制：正好落在列表/详情边界、随拖拽移动、干净遮住接缝，列表行残余不会漏到线下方。
            // 拆分视图自带的浮动 dragline 在 StyleDetailDivider 里隐藏；其 anchor 仅作不可见的拖拽抓取条。
            VisualElement detailPane = BuildDetailPane();
            detailPane.style.borderTopWidth = DebugxConsoleStyle.DetailDividerThickness;
            detailPane.style.borderTopColor = DebugxConsoleStyle.DetailDividerColor;
            split.Add(detailPane);

            // Neutralize the split view's floating dragline + size its grab-strip; (re)applied on geometry changes because
            // those internal elements are only created on the split view's first layout pass.
            // 中和拆分视图的浮动 dragline 并设置抓取条大小；这些内部元素首次布局才创建，故在几何变化时（重复）应用。
            split.RegisterCallback<GeometryChangedEvent>(_ => StyleDetailDivider(split));
            root.Add(split);

            ApplyCriteriaToStore();
            ForceRefresh();
        }

        // ---------- Toolbar ----------

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            _toolbar = toolbar;
            // Recompute which optional groups fit whenever the toolbar is resized.
            // 工具栏尺寸变化时重算可选分组的显隐。
            toolbar.RegisterCallback<GeometryChangedEvent>(_ => UpdateResponsive());

            // Two-group layout so the counts are STRUCTURALLY protected, not just reserved by width math:
            //  - leftGroup: flex-grow + flex-shrink + overflow:hidden. It absorbs ALL narrowing; when the window is too
            //    small its right edge (the search side) is clipped rather than the counts.
            //  - countsGroup: flex-shrink 0, pinned to the far right. It is laid out independently of leftGroup, so no
            //    amount of overflow inside leftGroup can ever push the counts off-screen.
            // The responsive show/hide logic still hides optional buttons INSIDE leftGroup for usability.
            // 采用左右两组布局，让计数在结构上被保护，而非仅靠宽度数学预留：
            //  - leftGroup：flex-grow + flex-shrink + overflow:hidden。它吸收全部收缩；窗口过小时被裁的是它的右缘（搜索侧），而非计数。
            //  - countsGroup：flex-shrink 0，固定贴最右。它独立于 leftGroup 布局，故 leftGroup 内部再怎么溢出都无法把计数挤出屏幕。
            // 响应式显隐逻辑仍作用于 leftGroup 内部的可选按钮，用于提升可用性。
            var leftGroup = new VisualElement();
            leftGroup.style.flexDirection = FlexDirection.Row;
            leftGroup.style.flexGrow = 1;
            leftGroup.style.flexShrink = 1;
            leftGroup.style.minWidth = 0;
            leftGroup.style.overflow = Overflow.Hidden;
            // No explicit height: as a direct toolbar child it stretches to the toolbar's height (align-items:stretch),
            // exactly as the buttons did when they were direct children. 不设显式高度：作为工具栏直接子级，它按 align-items:stretch
            // 拉伸到工具栏高度，与按钮此前作为直接子级时一致。

            _clearButton = new ToolbarButton(OnClearClicked);
            // Divider line lives inside the Clear button (right edge) so its % height resolves against a real,
            // sized parent; styled in StyleClearSplit. 分隔线作为 Clear 按钮的子元素置于右边缘，其百分比高度可对
            // 一个有确定尺寸的父级解析；样式见 StyleClearSplit。
            _clearDivider = new VisualElement { pickingMode = PickingMode.Ignore };
            _clearButton.Add(_clearDivider);
            leftGroup.Add(_clearButton);

            // Native-style: a dropdown arrow next to Clear with the "Clear on ..." options.
            // 原生风格：Clear 旁的下拉三角，内含“清空时机”选项。
            _clearDropdownButton = new ToolbarButton(ShowClearMenu) { text = "▼" };
            leftGroup.Add(_clearDropdownButton);

            _collapseToggle = MakeToggle(string.Empty, CollapseFromPrefs(), evt =>
            {
                _store.CollapseMode = evt.newValue ? LogCollapser.Mode.ByMessage : LogCollapser.Mode.Off;
                SavePrefs();
                ForceRefresh();
            });
            leftGroup.Add(_collapseToggle);

            _errorPauseToggle = MakeToggle(string.Empty, _errorPause, evt =>
            {
                _errorPause = evt.newValue;
                SavePrefs();
            });
            leftGroup.Add(_errorPauseToggle);

            // Members dropdown, then the Editor toggle to its right, sit in the LEFT group before the search.
            // (View options and UI language moved into the Editor panel — see RefreshEditorPanel.)
            // 成员下拉，其右侧是 Editor 开关，放在左侧分组、搜索栏之前。（视图选项与界面语言已移入 Editor 面板，见 RefreshEditorPanel。）
            _memberButton = new ToolbarButton(ShowMemberMenu);
            // Text + caret as separate children so the caret can be sized independently (a small native-style triangle).
            // 文字与三角拆成两个子元素，让三角可独立设定大小（接近原生的小三角）。
            _memberNameLabel = new Label { pickingMode = PickingMode.Ignore };
            _memberCaretLabel = new Label("▼") { pickingMode = PickingMode.Ignore };
            _memberButton.Add(_memberNameLabel);
            _memberButton.Add(_memberCaretLabel);
            leftGroup.Add(_memberButton);

            _editorPanelToggle = MakeToggle(string.Empty, false, evt =>
            {
                _editorPanelVisible = evt.newValue;
                _editorPanel.style.display = _editorPanelVisible ? DisplayStyle.Flex : DisplayStyle.None;
                if (_editorPanelVisible) RefreshEditorPanel();
            });
            leftGroup.Add(_editorPanelToggle);

            _searchField = new ToolbarSearchField();
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _criteria.Search = new SearchQuery { Text = evt.newValue };
                OnCriteriaChanged();
            });

            // The search is the LAST child of leftGroup and flex-grows to fill leftGroup's leftover, so it (not the
            // buttons) absorbs the width and is the first thing clipped when the window narrows. leftGroup itself
            // flex-grows within the toolbar, keeping countsGroup pinned to the far right (blank gap when search hides).
            // 搜索栏是 leftGroup 的最后一个子元素，flex-grow 填充 leftGroup 剩余空间，故由它（而非按钮）吸收宽度、窗口变窄时最先被裁。
            // leftGroup 本身在工具栏内 flex-grow，使 countsGroup 始终贴最右（搜索隐藏时中间留白）。
            _searchContainer = new VisualElement();
            _searchContainer.style.flexDirection = FlexDirection.Row;
            _searchContainer.style.flexGrow = 1;
            _searchContainer.style.flexShrink = 1;
            _searchContainer.style.minWidth = 0;
            _searchContainer.style.alignItems = Align.Center;
            _searchContainer.style.overflow = Overflow.Hidden;
            // Divider hairline on the search area's LEFT edge (same color/width as the other toolbar separators).
            // Its width is toggled with the search's visibility in UpdateResponsive, so no stray line remains when the
            // search is hidden. The left padding gives the search box the same breathing room from the divider as
            // SearchRightSpace gives it from the counts on the right.
            // 搜索区左边缘的分隔细线（与其它工具栏分隔线同色同宽）。其宽度在 UpdateResponsive 里随搜索显隐切换，避免搜索隐藏时
            // 残留竖线。左内边距让搜索框与分隔线之间的留白，与右侧 SearchRightSpace 对称。
            _searchContainer.style.borderLeftColor = DebugxConsoleStyle.ToolbarDividerColor;
            _searchContainer.style.paddingLeft = DebugxConsoleStyle.SearchRightSpace;
            _searchContainer.Add(_searchField);
            leftGroup.Add(_searchContainer);
            toolbar.Add(leftGroup);

            // Count buttons live in their own flex-shrink-0 group pinned to the far right. Because this group is laid out
            // independently of leftGroup, the counts can never be clipped no matter how narrow the window gets.
            // 计数按钮放在自己的 flex-shrink 0 分组中、固定贴最右。该分组独立于 leftGroup 布局，故无论窗口多窄，计数都不会被裁切。
            var countsGroup = new VisualElement();
            countsGroup.style.flexDirection = FlexDirection.Row;
            countsGroup.style.flexGrow = 0;
            countsGroup.style.flexShrink = 0; // never shrinks — this is what guarantees the counts stay on-screen. 永不收缩——这正是计数常驻的保证。

            _logButton = MakeCountButton(_iconLog, out _logCount, () =>
            {
                _criteria.ShowLog = !_criteria.ShowLog;
                UpdateCountButtonStates();
                OnCriteriaChanged();
            });
            _warnButton = MakeCountButton(_iconWarn, out _warnCount, () =>
            {
                _criteria.ShowWarning = !_criteria.ShowWarning;
                UpdateCountButtonStates();
                OnCriteriaChanged();
            });
            _errorButton = MakeCountButton(_iconError, out _errorCount, () =>
            {
                _criteria.ShowError = !_criteria.ShowError;
                UpdateCountButtonStates();
                OnCriteriaChanged();
            });
            countsGroup.Add(_logButton);
            countsGroup.Add(_warnButton);
            countsGroup.Add(_errorButton);
            toolbar.Add(countsGroup);

            ApplyLanguage();
            UpdateCountButtonStates();
            return toolbar;
        }

        // Builds a native-Console-style count button: severity icon + a count label, toggling the type filter.
        // 构建原生 Console 风格的计数按钮：严重级别图标 + 计数标签，点击切换该类型过滤。
        private VisualElement MakeCountButton(Texture icon, out Label countLabel, System.Action onClick)
        {
            var btn = new VisualElement();
            btn.AddToClassList("unity-toolbar-button");
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.paddingLeft = 5;
            btn.style.paddingRight = 5;

            var img = new Image { name = "count-icon", image = icon, scaleMode = ScaleMode.ScaleToFit };
            img.style.width = DebugxConsoleStyle.CountIconSize;
            img.style.height = DebugxConsoleStyle.CountIconSize;
            img.style.marginRight = DebugxConsoleStyle.CountIconMarginRight;
            img.style.flexShrink = 0;

            countLabel = new Label("0");
            countLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            btn.Add(img);
            btn.Add(countLabel);
            btn.RegisterCallback<ClickEvent>(_ => onClick());
            // When the count text changes width (e.g. 9 -> 999+), re-evaluate the responsive layout so the wider counts
            // are reserved and stay on-screen even without a window resize. Idempotent, so no layout thrash.
            // 当计数文字宽度变化（如 9 -> 999+）时重跑响应式布局，使更宽的计数被预留、无需缩放窗口也能留在屏幕内。幂等，不会抖动。
            btn.RegisterCallback<GeometryChangedEvent>(_ => UpdateResponsive());
            return btn;
        }

        private static ToolbarToggle MakeToggle(string text, bool value, EventCallback<ChangeEvent<bool>> onChange)
        {
            var toggle = new ToolbarToggle { text = text, value = value };
            toggle.RegisterValueChangedCallback(onChange);
            return toggle;
        }

        // ---------- List pane ----------

        private VisualElement BuildListPane()
        {
            _listView = new ListView
            {
                fixedItemHeight = DebugxConsoleStyle.ListItemHeight,
                selectionType = SelectionType.Multiple, // multi-select so Ctrl+C can copy several rows. 多选，便于 Ctrl+C 复制多行。
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = _rows,
            };
            _listView.style.flexGrow = 1;
            _listView.selectionChanged += OnSelectionChanged;
            _listView.itemsChosen += OnItemsChosen;
            _listView.RegisterCallback<KeyDownEvent>(OnListKeyDown); // Ctrl/Cmd+C copy. Ctrl/Cmd+C 复制。
            // Hide ListView's built-in "List is empty" placeholder on every layout pass.
            // 每次布局时隐藏 ListView 内置的“List is empty”占位。
            _listView.RegisterCallback<GeometryChangedEvent>(_ => HideListEmptyLabel());
            return _listView;
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;

            var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = DebugxConsoleStyle.RowIconSize;
            icon.style.height = DebugxConsoleStyle.RowIconSize;
            icon.style.marginRight = DebugxConsoleStyle.RowIconMarginRight;
            icon.style.flexShrink = 0;

            var time = new Label { name = "time" }; // optional timestamp column, toggled by BindRow. 可选时间戳列，由 BindRow 显隐。
            time.style.width = DebugxConsoleStyle.TimestampWidth;
            time.style.flexShrink = 0;
            time.style.color = DebugxConsoleStyle.TimestampColor;
            time.style.unityTextAlign = TextAnchor.MiddleLeft;
            time.style.display = DisplayStyle.None;

            var msg = new Label { name = "msg", enableRichText = true };
            msg.style.flexGrow = 1;
            msg.style.overflow = Overflow.Hidden;
            msg.style.whiteSpace = WhiteSpace.NoWrap;
            msg.style.unityTextAlign = TextAnchor.MiddleLeft;

            var badge = new Label { name = "badge" };
            badge.style.flexShrink = 0;
            badge.style.minWidth = 22;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;

            row.Add(icon);
            row.Add(time);
            row.Add(msg);
            row.Add(badge);
            row.AddManipulator(new ContextualMenuManipulator(evt => BuildRowContextMenu(evt, row))); // right-click copy. 右键复制。
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            CollapsedRow row = _rows[index];
            DebugxLogEntry e = row.Entry;

            element.userData = index; // consumed by the row's context menu. 供该行右键菜单读取。

            element.Q<Image>("icon").image = IconFor(e.LogType);

            var time = element.Q<Label>("time");
            time.text = _showTimestamp ? TimestampText(e) : string.Empty;
            time.style.display = _showTimestamp ? DisplayStyle.Flex : DisplayStyle.None;

            var msg = element.Q<Label>("msg");
            msg.text = SingleLine(e.RichText);

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
            // One IMGUIContainer renders message + stacktrace as a single selectable rich-text block, matching the native
            // Console: cross-line drag-select + Ctrl/Cmd+C, member colors, and clickable <a href> source links
            // (SelectableLabel routes clicks through EditorGUI.DoTextField -> hyperLinkClicked -> the global file-opener).
            // Uniform on 2021.3 and 2022+, so no version split; right-click copy is handled inside the IMGUI handler.
            // 一个 IMGUIContainer 将 消息+堆栈 渲染为单个可选中富文本块，对齐原生 Console：跨行拖选 + Ctrl/Cmd+C、成员着色、
            // 可点击的 <a href> 源码链接（SelectableLabel 的点击经 EditorGUI.DoTextField -> hyperLinkClicked -> 全局打开文件）。
            // 2021.3 与 2022+ 一致，无需版本分支；右键复制在 IMGUI 处理器内实现。
            _detailImgui = new IMGUIContainer(OnDetailGUI);
            _detailImgui.style.flexGrow = 1;  // fill the pane so the inner scroll view gets real height. 填满面板，使内部滚动视图获得实际高度。
            _detailImgui.style.minHeight = 0; // allow shrinking inside the split pane. 允许在分栏内收缩。
            return _detailImgui;
        }

        // Entry point on selection change and ClearConsole. Stores the entry, rebuilds the combined rich text under the
        // current ScriptOnly filter, resets scroll, and forces one repaint (data changed outside the IMGUI event loop).
        // 选中变化与 ClearConsole 的入口。存条目、按当前 仅脚本 过滤重建合并富文本、重置滚动，并强制一次重绘（数据在 IMGUI 事件循环外变化）。
        private void UpdateDetail(DebugxLogEntry e)
        {
            _detailEntry = e;
            RebuildDetailText();
            _detailScrollPos = Vector2.zero;
            _detailImgui?.MarkDirtyRepaint();
        }

        // Build the cached combined rich text: message (RichText, member colors) + each visible stack frame, with source
        // frames wrapped as <a href="path" line="n">raw</a> so the native hyperlink handler navigates on click. Respects
        // the ScriptOnly (IsStackFrameVisible) filter and records the ScriptOnly state the cache was built under.
        // 构建缓存的合并富文本：消息（RichText，成员色）+ 每个可见堆栈帧，源码帧包成 <a href="path" line="n">raw</a>，
        // 使原生超链接处理器点击可跳转。遵循 仅脚本(IsStackFrameVisible) 过滤，并记录构建时的 仅脚本 状态。
        private void RebuildDetailText()
        {
            _detailScriptOnlyCache = _stackScriptOnly;

            DebugxLogEntry e = _detailEntry;
            if (e == null)
            {
                _detailCombinedText = string.Empty;
                _detailContent.text = string.Empty;
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append(e.RichText);

            List<StackFrameInfo> frames = StackTraceParser.Parse(e.StackTrace);
            foreach (StackFrameInfo frame in frames)
            {
                if (!IsStackFrameVisible(frame)) continue; // Script-Only / Full toggle (View menu). 仅脚本/完整 切换（视图菜单）。
                sb.Append('\n').Append(BuildStackFrameRichLine(frame));
            }
            _detailCombinedText = sb.ToString();
            _detailContent.text = _detailCombinedText;
        }

        // One stacktrace line as rich text. Source frames become <a href="FilePath" line="Line">RawLine</a> (native's exact
        // template) so EditorGUI's global hyperlink handler opens the file at the line; the visible text stays the raw frame.
        // FilePath is passed through verbatim (project-relative or absolute) — the handler resolves both. Only the attribute's
        // own quote is escaped; the visible RawLine is left as-is (Unity renders any unknown angle-bracket token literally).
        // 一行堆栈的富文本。源码帧变为 <a href="FilePath" line="Line">RawLine</a>（原生同款模板），使 EditorGUI 全局超链接处理器按行打开文件；
        // 可见文本仍是原始帧。FilePath 原样传入（工程相对或绝对），处理器都能解析。仅转义属性内的引号；可见的 RawLine 不动（Unity 会把无法识别的尖括号按字面渲染）。
        private static string BuildStackFrameRichLine(StackFrameInfo frame)
        {
            if (!frame.HasSource) return frame.RawLine;
            string href = (frame.FilePath ?? string.Empty).Replace("\"", "&quot;");
            return $"<a href=\"{href}\" line=\"{frame.Line}\">{frame.RawLine}</a>";
        }

        // IMGUI handler for the detail block. IMGUIContainer doesn't scroll, so we host a GUILayout scroll view; the
        // SelectableLabel is sized to full content height via CalcHeight, renders selectable rich text (member colors +
        // clickable <a href> source links), and a right-click Copy All / Copy Stack menu is offered.
        // 详情块的 IMGUI 处理器。IMGUIContainer 不自滚，故内置一个 GUILayout 滚动视图；SelectableLabel 用 CalcHeight 撑到内容全高，
        // 渲染可选中富文本（成员色 + 可点击的 <a href> 源码链接），并提供右键 复制全部 / 复制堆栈 菜单。
        private void OnDetailGUI()
        {
            // Safety: if the ScriptOnly filter changed since the last rebuild, refresh the cache. 安全：仅脚本 过滤若已变则刷新缓存。
            if (_detailScriptOnlyCache != _stackScriptOnly) RebuildDetailText();

            GUIStyle style = DetailStyle;
            string text = _detailCombinedText;

            // Usable content width = container width - vertical scrollbar (~16) - style L/R padding; fall back to the view
            // width before the first layout (width <= 1). 可用内容宽度 = 容器宽 - 竖直滚动条(~16) - 样式左右内边距；首次布局前(宽<=1)回退视图宽。
            float w = _detailImgui != null ? _detailImgui.contentRect.width : 0f;
            if (w <= 1f) w = EditorGUIUtility.currentViewWidth;
            float wrapWidth = Mathf.Max(1f, w - 16f - style.padding.horizontal);
            float h = style.CalcHeight(_detailContent, wrapWidth);

            _detailScrollPos = GUILayout.BeginScrollView(_detailScrollPos);
            Rect labelRect = GUILayoutUtility.GetRect(_detailContent, style, GUILayout.ExpandWidth(true), GUILayout.Height(h));

            // Right-click => Copy All / Copy Stack. Handle before SelectableLabel so the event isn't swallowed. Built in
            // IMGUI because a UIToolkit ContextualMenuManipulator on an IMGUIContainer is unreliable.
            // 右键 => 复制全部 / 复制堆栈。须在 SelectableLabel 之前处理以免被吞。用 IMGUI，因 IMGUIContainer 上的 UIToolkit 右键菜单不可靠。
            if (Event.current.type == EventType.ContextClick && labelRect.Contains(Event.current.mousePosition))
            {
                ShowDetailContextMenu();
                Event.current.Use();
            }

            // SelectableLabel: cross-line drag-select + Ctrl/Cmd+C; richText keeps member colors; <a> clicks route through
            // EditorGUI.DoTextField -> hyperLinkClicked -> the global file-opener (we do not subscribe -> no double-open).
            // SelectableLabel：跨行拖选 + Ctrl/Cmd+C；richText 保留成员色；<a> 点击经 EditorGUI.DoTextField -> hyperLinkClicked -> 全局打开文件（我们不订阅 -> 不会重复打开）。
            EditorGUI.SelectableLabel(labelRect, text, style);

            GUILayout.EndScrollView();
        }

        // The detail text style: native Console's "CN Message" (already richText + wordWrap), cloned before mutating so we
        // don't leak tweaks into the shared skin; falls back to a rich-text label clone if the style name is absent.
        // 详情文本样式：原生 Console 的 "CN Message"（本就 richText + wordWrap），修改前克隆以免污染共享皮肤；样式缺失则回退到富文本 label 克隆。
        private GUIStyle DetailStyle
        {
            get
            {
                if (_detailStyle == null)
                {
                    GUIStyle src = GUI.skin.FindStyle("CN Message");
                    _detailStyle = src != null
                        ? new GUIStyle(src) { richText = true, wordWrap = true }
                        : new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true, padding = new RectOffset(6, 6, 6, 6) };
                }
                return _detailStyle;
            }
        }

        // Right-click Copy All / Copy Stack for the current detail entry (reuses CopyEntries + L). 当前详情条目的右键 复制全部 / 复制堆栈（复用 CopyEntries + L）。
        private void ShowDetailContextMenu()
        {
            DebugxLogEntry e = _detailEntry;
            if (e == null) return;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(L("复制全部", "Copy All")), false,
                () => CopyEntries(new List<DebugxLogEntry> { e }, withStack: true));
            if (!string.IsNullOrEmpty(e.StackTrace))
                menu.AddItem(new GUIContent(L("复制堆栈", "Copy Stack")), false,
                    () => EditorGUIUtility.systemCopyBuffer = e.StackTrace);
            menu.ShowAsContext();
        }

        // The visible divider is the detail pane's top border (set in CreateGUI), so here we only neutralize the split
        // view's own floating dragline: make the anchor + line fully transparent (inline styles also kill the theme's
        // :hover recolor) and shrink the anchor to a small drag grab-strip. This stops a list-row sliver from showing in
        // the (previously themed) anchor band below the seam, while the splitter stays draggable.
        // 可见分隔线改由详情面板的上边框绘制（见 CreateGUI），这里只中和拆分视图自带的浮动 dragline：把 anchor 与 line 全透明
        // （内联样式同时消除主题的 :hover 变色），并把 anchor 收成小的拖拽抓取条。这样接缝下方那条（原本有主题底色的）anchor 带
        // 不再露出列表行残余，同时分隔线仍可拖拽。
        private static void StyleDetailDivider(TwoPaneSplitView split)
        {
            VisualElement anchor = split.Q(className: "unity-two-pane-split-view__dragline-anchor");
            if (anchor != null)
            {
                anchor.style.backgroundColor = Color.clear;
                anchor.style.height = DebugxConsoleStyle.DetailDividerGrabSize; // shrink the invisible grab/hover band. 收窄不可见的抓取/悬停带。
            }

            VisualElement line = split.Q(className: "unity-two-pane-split-view__dragline");
            if (line != null) line.style.backgroundColor = Color.clear; // hidden; the detail pane's top border is the real line. 隐藏；真正的分隔线是详情面板的上边框。
        }

        // ---------- Refresh / update loop ----------

        private void OnEditorUpdate()
        {
            if (_store == null) return;
            PumpCompileMirror();           // may inject compiler/import entries into the collector queue. 可能向采集队列注入编译器/导入条目。
            _store.Pump();                 // drains them (and live-channel entries) into the buffer. 将它们（及 live 通道条目）排空进缓冲。
            if (_listView == null) return; // UI not built yet (OnEnable runs before CreateGUI). UI 尚未构建。
            if (_store.TryRebuildView())
                RefreshView();
        }

        private void OnCriteriaChanged()
        {
            SavePrefs();
            ApplyCriteriaToStore();
            ForceRefresh();
        }

        private void ApplyCriteriaToStore()
        {
            _store.SetFilterCriteria(_criteria);
        }

        private void ForceRefresh()
        {
            _store.MarkViewDirty();
            _store.TryRebuildView();
            RefreshView();
        }

        private void RefreshView()
        {
            // Remember the selected entry by its stable id BEFORE rebuilding rows: eviction shifts positional indices,
            // so a plain _selectedIndex would then point at a different entry (wrong detail / wrong Copy target).
            // 重建行列表前按稳定 id 记住选中条目：淘汰会让位置索引偏移，仅凭 _selectedIndex 之后会指向另一条目（详情/Copy 目标错位）。
            long selectedSeq = (_selectedIndex >= 0 && _selectedIndex < _rows.Count && _rows[_selectedIndex].Entry != null)
                ? _rows[_selectedIndex].Entry.SequenceId
                : -1;

            _rows.Clear();
            IReadOnlyList<CollapsedRow> src = _store.Rows;
            for (int i = 0; i < src.Count; i++)
                _rows.Add(src[i]);

            _listView.RefreshItems();
            HideListEmptyLabel();
            UpdateCounts();
            ReconcileSelection(selectedSeq);

            // Tail: auto-scroll to the newest entry only while the list is stuck to the bottom (native behaviour).
            // Scrolling up pauses the tail; scrolling back to the bottom resumes it (see OnListScrolled).
            // Tail：仅当列表贴底时自动滚到最新条目（对标原生）。上滚暂停 tail，回到底部恢复（见 OnListScrolled）。
            EnsureListScrollHook();
            if (_stickToBottom && _rows.Count > 0)
                _listView.ScrollToItem(_rows.Count - 1);
        }

        // Re-align the ListView selection with the entry it pointed at before the rebuild, keyed by SequenceId, so the
        // highlight, the detail pane and Copy keep referring to the SAME entry after eviction/filtering shifts the rows;
        // clears the selection when that entry is gone. SetSelectionWithoutNotify avoids re-firing OnSelectionChanged.
        // 按 SequenceId 把选中项重新对齐到重建前所指条目，使淘汰/过滤导致行偏移后，高亮、详情与 Copy 仍指向同一条目；
        // 该条目消失时清除选中。SetSelectionWithoutNotify 避免再次触发 OnSelectionChanged。
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
                _selectedIndex = -1;
                _listView.ClearSelection();
                UpdateDetail(null);
            }
        }

        // Lazily hook the ListView's internal scroll view to track whether it is at the bottom. Retried each refresh
        // until the scroll view exists. If it never resolves, _stickToBottom stays true (always tails) — safe fallback.
        // 惰性挂接 ListView 内部滚动视图以跟踪是否贴底。每次刷新重试直到滚动视图存在。若始终解析不到，_stickToBottom 保持 true
        //（始终 tail）——安全降级。
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
            _stickToBottom = value >= s.highValue - 1f; // at (or within 1px of) the bottom. 处于（或距）底部 1px 内。
        }

        private void UpdateCounts()
        {
            LogStatistics s = _store.Statistics;
            _logCount.text = CountText(s.LogCount);
            _warnCount.text = CountText(s.WarningCount);
            _errorCount.text = CountText(s.ErrorCount);

            // Gray "_g" icon when that severity's count is 0; colored icon otherwise (gray missing → keep colored).
            // 某类型计数为 0 时用灰色 "_g" 图标；否则用彩色（灰色缺失则保持彩色）。
            SetCountIcon(_logButton, s.LogCount, _iconLog, _iconLogGray);
            SetCountIcon(_warnButton, s.WarningCount, _iconWarn, _iconWarnGray);
            SetCountIcon(_errorButton, s.ErrorCount, _iconError, _iconErrorGray);
        }

        private static void SetCountIcon(VisualElement btn, int count, Texture colored, Texture gray)
        {
            Image img = btn?.Q<Image>("count-icon");
            if (img != null) img.image = (count == 0 && gray != null) ? gray : colored;
        }

        private static string CountText(int n) =>
            n > DebugxConsoleStyle.CountOverflowThreshold ? DebugxConsoleStyle.CountOverflowThreshold + "+" : n.ToString();

        private void UpdateCountButtonStates()
        {
            SetCountActive(_logButton, _criteria.ShowLog);
            SetCountActive(_warnButton, _criteria.ShowWarning);
            SetCountActive(_errorButton, _criteria.ShowError);
        }

        private static void SetCountActive(VisualElement btn, bool active)
        {
            if (btn != null) btn.style.opacity = active ? 1f : DebugxConsoleStyle.CountInactiveOpacity;
        }

        private void ApplyLanguage()
        {
            _clearButton.text = L("清空", "Clear");
            _clearDropdownButton.tooltip = L("清空时机选项", "Clear-on options");
            _collapseToggle.text = L("折叠", "Collapse");
            _errorPauseToggle.text = L("错误暂停", "Error Pause");
            _memberNameLabel.text = L("成员", "Members");
            _editorPanelToggle.text = L("编辑器", "Editor");

            ApplyToolbarLayout();

            // The View options + language button now live in the Editor panel; re-localize it too when it's open.
            // Deferred so a language-button click (which originates from a control inside the panel) doesn't rebuild the
            // panel mid-event. 视图选项与语言按钮现位于 Editor 面板；面板打开时一并重建以本地化。延迟执行，避免语言按钮的
            // 点击（来自面板内控件）在事件处理途中重建面板。
            if (_editorPanelVisible && _editorPanel != null)
                _editorPanel.schedule.Execute(RefreshEditorPanel);
        }

        private void ToggleLanguage()
        {
            _chineseUi = !_chineseUi;
            SavePrefs();
            ApplyLanguage();
        }

        // Fixed width per item (differs by language) + centered content, so all toolbar items align consistently.
        // 每个条目固定宽度（随语言不同）+ 内容居中，使工具栏所有条目对齐一致。
        private void ApplyToolbarLayout()
        {
            float clear = Wc(DebugxConsoleStyle.ClearWidth);
            float clearDropdown = DebugxConsoleStyle.ClearDropdownWidth;
            float collapse = Wc(DebugxConsoleStyle.CollapseWidth);
            float errorPause = Wc(DebugxConsoleStyle.ErrorPauseWidth);
            float runtime = Wc(DebugxConsoleStyle.EditorWidth);
            float members = Wc(DebugxConsoleStyle.MembersWidth);
            float countMin = Wc(DebugxConsoleStyle.CountWidth);

            // Clear + divider + dropdown arrow styled together to match the native Clear dropdown.
            // Clear + 分隔线 + 下拉三角一起做成与原生 Clear 下拉一致的外观。
            StyleClearSplit(_clearButton, _clearDivider, _clearDropdownButton, clear, clearDropdown);
            StyleItem(_collapseToggle, collapse);
            StyleItem(_errorPauseToggle, errorPause);
            StyleItem(_editorPanelToggle, runtime);
            StyleMemberDropdown(_memberButton, _memberNameLabel, _memberCaretLabel, members);

            // Count buttons: width grows with the number, but never below CountWidth.
            // 计数按钮：宽度随数字增长，但不小于 CountWidth。
            StyleCountItem(_logButton, countMin);
            StyleCountItem(_warnButton, countMin);
            StyleCountItem(_errorButton, countMin);

            // Search: fills the middle (flex-grow), shrinks to SearchMinWidth, then the responsive logic hides it.
            // 搜索栏：伸展填充中部（flex-grow），可收缩至 SearchMinWidth，再窄则由响应式逻辑隐藏。
            _searchField.style.flexGrow = 1;
            _searchField.style.flexShrink = 1;
            _searchField.style.flexBasis = DebugxConsoleStyle.SearchWidth;
            _searchField.style.minWidth = DebugxConsoleStyle.SearchMinWidth;
            _searchField.style.marginRight = DebugxConsoleStyle.SearchRightSpace;

            // Counts are NOT baked into _wBase: their auto width grows with the digit count, so they are reserved live
            // from resolvedStyle in UpdateResponsive (see CountsWidth). Baking a fixed estimate here let big numbers
            // overflow and clip the counts. 计数按钮不并入 _wBase：其自适应宽度随位数增长，改在 UpdateResponsive 里按
            // resolvedStyle 实时预留（见 CountsWidth）。此处若固定估算，大数字会溢出并裁掉计数。
            _wBase = clear + clearDropdown + collapse;
            _wSearch = DebugxConsoleStyle.SearchMinWidth + DebugxConsoleStyle.SearchRightSpace;
            _wFilters = members;
            _wEditorGroup = runtime + errorPause;

            UpdateResponsive();
        }

        private float Wc(LangWidth w) => _chineseUi ? w.Cn : w.En;

        // Fixed-width toolbar item with horizontally + vertically centered text.
        // 固定宽度的工具栏条目，文字水平 + 垂直居中。
        private static void StyleItem(VisualElement el, float width)
        {
            if (el == null) return;
            el.style.width = width;
            el.style.minWidth = width;
            el.style.maxWidth = width;
            el.style.flexShrink = 0;
            el.style.flexGrow = 0;
            el.style.paddingLeft = DebugxConsoleStyle.ItemPadding;
            el.style.paddingRight = DebugxConsoleStyle.ItemPadding;
            el.style.justifyContent = Justify.Center;
            el.style.alignItems = Align.Center;
            el.style.unityTextAlign = TextAnchor.MiddleCenter;
            CenterChildText(el);
            SetLeftDivider(el); // uniform left-side hairline divider (see SetLeftDivider for why left). 统一的左侧细线分隔（为何用左侧见 SetLeftDivider）。
        }

        // Count button: min width = CountWidth, actual width auto (content-driven); content centered.
        // 计数按钮：最小宽度 = CountWidth，实际宽度自适应（随内容）；内容居中。
        private static void StyleCountItem(VisualElement el, float minWidth)
        {
            if (el == null) return;
            el.style.minWidth = minWidth;
            el.style.width = StyleKeyword.Auto;
            el.style.maxWidth = StyleKeyword.None;
            el.style.flexShrink = 0;
            el.style.flexGrow = 0;
            el.style.paddingLeft = DebugxConsoleStyle.ItemPadding;
            el.style.paddingRight = DebugxConsoleStyle.ItemPadding;
            el.style.justifyContent = Justify.Center;
            el.style.alignItems = Align.Center;
            SetLeftDivider(el); // count buttons are right-aligned, so their hairline sits on the LEFT. 计数按钮右对齐，细线画在左侧。
        }

        // Force text children (labels) to center, overriding toolbar USS that left-aligns them.
        // 强制文字子元素（Label）居中，覆盖工具栏 USS 默认的左对齐。
        private static void CenterChildText(VisualElement el)
        {
            el.Query<TextElement>().ForEach(t =>
            {
                t.style.unityTextAlign = TextAnchor.MiddleCenter;
                t.style.flexGrow = 1;
            });
        }

        // Uniform toolbar separators: every item draws its divider as a single LEFT-side hairline of the same width +
        // color, and zeroes its other three borders. The LEFT side matters — Unity's native toolbar gives items a
        // margin-left of -1px so adjacent borders overlap/collapse; a RIGHT border gets covered by the next item and
        // vanishes, while a LEFT border moves WITH the item and stays visible. One border per seam also means two
        // borders can never stack into a thick line. The first item (Clear) and the caret clear their own left border
        // afterward — their seam is drawn by the custom child divider line instead.
        // 统一的工具栏分隔线：每个条目都把分隔线画成左侧一条同宽同色的细线，其余三边置零。用左侧很关键——Unity 原生工具栏给
        // 条目设了 -1px 的 margin-left 让相邻边框重叠折叠；右边框会被后一个条目盖住而消失，左边框则随条目移动、始终可见。
        // 每条接缝只有一条边框，也不会叠成粗线。第一个条目（Clear）与三角随后各自清掉左边框——它们之间改由自绘竖线分隔。
        private static void SetLeftDivider(VisualElement el)
        {
            if (el == null) return;
            el.style.borderTopWidth = 0;
            el.style.borderBottomWidth = 0;
            el.style.borderRightWidth = 0;
            el.style.borderLeftWidth = DebugxConsoleStyle.ToolbarDividerWidth;
            el.style.borderLeftColor = DebugxConsoleStyle.ToolbarDividerColor;
        }

        // Style the Clear button + hairline divider + dropdown caret to read like the native console's Clear
        // dropdown: "Clear" and a "▼" caret each centered in their own zone, separated by a dark vertical
        // hairline. Behaviour is unchanged (main = clear, caret = menu).
        // 将 Clear 按钮 + 细分隔线 + 下拉三角做成原生 Console 的 Clear 下拉外观：“Clear” 与 “▼” 各自在自己
        // 的区域内居中，中间用一条深色竖直细线分隔。行为不变（主体=清空，三角=菜单）。
        private static void StyleClearSplit(ToolbarButton main, VisualElement divider, ToolbarButton arrow, float mainWidth, float arrowWidth)
        {
            if (main != null)
            {
                StyleItem(main, mainWidth);
                main.style.marginRight = 0;
                main.style.borderLeftWidth = 0;  // first toolbar item — no leading divider; Clear|caret is drawn by the divider child. 工具栏第一个条目——前面不画分隔线；Clear|三角 由 divider 子元素绘制。
                main.style.paddingRight = 0;      // so the divider child sits exactly on the right edge (boundary with the caret). 使分隔线子元素正好落在右边缘（与三角的交界）。
            }

            if (divider != null)
            {
                // Absolute child pinned to the right edge; top/bottom percent insets set its height proportion.
                // 绝对定位、贴右边缘；上下百分比内缩决定其高度占比。
                float inset = (100f - DebugxConsoleStyle.ClearDividerHeightPercent) / 2f;
                divider.style.position = Position.Absolute;
                divider.style.right = 0;
                divider.style.top = Length.Percent(inset);
                divider.style.bottom = Length.Percent(inset);
                divider.style.width = DebugxConsoleStyle.ToolbarDividerWidth;
                divider.style.backgroundColor = DebugxConsoleStyle.ToolbarDividerColor;
            }

            if (arrow != null)
            {
                StyleItem(arrow, arrowWidth);
                arrow.style.marginLeft = 0;
                arrow.style.borderLeftWidth = 0;
                arrow.style.fontSize = DebugxConsoleStyle.CaretFontSize;
            }
        }

        // Style the Members dropdown: a name label + an independently-sized caret ("▼"), centered as one group.
        // The caret shares CaretFontSize with the Clear caret so both triangles match in size.
        // 设置 Members 下拉：文字标签 + 可独立设定大小的三角（“▼”），作为一个整体居中。三角与 Clear 的三角
        // 共用 CaretFontSize，使两个三角大小一致。
        private static void StyleMemberDropdown(ToolbarButton button, Label name, Label caret, float width)
        {
            if (button != null)
            {
                button.style.width = width;
                button.style.minWidth = width;
                button.style.maxWidth = width;
                button.style.flexShrink = 0;
                button.style.flexGrow = 0;
                button.style.paddingLeft = DebugxConsoleStyle.ItemPadding;
                button.style.paddingRight = DebugxConsoleStyle.ItemPadding;
                button.style.flexDirection = FlexDirection.Row;
                button.style.alignItems = Align.Center;
                button.style.justifyContent = Justify.Center;
                SetLeftDivider(button); // uniform left-side hairline divider. 统一的左侧细线分隔。
            }

            if (name != null)
            {
                name.style.flexGrow = 0;
                name.style.flexShrink = 0;
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
            }

            if (caret != null)
            {
                caret.style.flexGrow = 0;
                caret.style.flexShrink = 0;
                caret.style.marginLeft = 3;
                caret.style.fontSize = DebugxConsoleStyle.CaretFontSize;
                caret.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
        }

        // Progressively hide optional groups when the toolbar is too narrow:
        // search -> Members -> (Editor + Error Pause).
        // 工具栏过窄时依次隐藏可选分组：搜索栏 -> 成员 -> (编辑器 + 错误暂停)。
        private void UpdateResponsive()
        {
            if (_toolbar == null) return;
            float avail = _toolbar.contentRect.width - DebugxConsoleStyle.ResponsiveBuffer; // buffer for item margins. 预留条目间距余量。
            if (avail <= 1f || float.IsNaN(avail)) return;

            // Reserve the counts' ACTUAL current width (they grow with their digit count and must stay visible), so the
            // groups below hide early enough that the counts never overflow the right edge and get clipped.
            // 预留计数按钮的实际当前宽度（随位数增长，且必须常驻），使下方分组足够早地隐藏，让计数永不溢出右边缘被裁切。
            float baseW = _wBase + CountsWidth();

            // Fixed groups (left + right) hide only when they overflow; the flex-grow middle container keeps the right
            // group right-aligned (blank gap in the middle when the search is hidden). Hide order: filters, then runtime.
            // 固定分组（左 + 右）仅在溢出时隐藏；flex-grow 中部容器让右侧分组保持靠右（搜索隐藏时中间留白）。隐藏顺序：过滤组，再运行时组。
            bool showFilters = baseW + _wFilters + _wEditorGroup <= avail;
            bool showEditorPanel = showFilters || baseW + _wEditorGroup <= avail;

            float fixedVisible = baseW
                + (showFilters ? _wFilters : 0f)
                + (showEditorPanel ? _wEditorGroup : 0f);

            // Search shows only if the leftover middle can hold it at its minimum width.
            // 仅当中部剩余空间能容纳搜索栏最小宽度时才显示搜索栏。
            bool showSearch = avail - fixedVisible >= _wSearch;

            SetVisible(_searchField, showSearch);
            // The search area's left divider only makes sense while the search is visible. 搜索区左侧分隔线仅在搜索可见时显示。
            _searchContainer.style.borderLeftWidth = showSearch ? DebugxConsoleStyle.ToolbarDividerWidth : 0f;
            SetVisible(_memberButton, showFilters);
            SetVisible(_editorPanelToggle, showEditorPanel);
            SetVisible(_errorPauseToggle, showEditorPanel);
        }

        // Sum of the three count buttons' resolved (laid-out) widths — their real footprint including the current digit
        // count. resolvedStyle keeps the full width even while an overflowing button is visually clipped, so this stays
        // accurate and lets UpdateResponsive keep them on-screen. Falls back to CountWidth per button before first layout.
        // 三个计数按钮已解析（布局后）宽度之和——含当前位数的真实占位。即便按钮溢出被裁，resolvedStyle 仍保留完整宽度，故此值
        // 始终准确，让 UpdateResponsive 得以把它们留在屏幕内。首次布局前每个按钮回退到 CountWidth。
        private float CountsWidth()
        {
            float min = Wc(DebugxConsoleStyle.CountWidth);
            return CountWidth(_logButton, min) + CountWidth(_warnButton, min) + CountWidth(_errorButton, min);
        }

        private static float CountWidth(VisualElement btn, float min)
        {
            if (btn == null) return min;
            float w = btn.resolvedStyle.width;
            return w > 1f && !float.IsNaN(w) ? w : min; // before layout resolves, width is 0/NaN — use the minimum. 布局解析前宽度为 0/NaN——用最小值。
        }

        private static void SetVisible(VisualElement el, bool visible)
        {
            if (el != null) el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void HideListEmptyLabel()
        {
            if (_listView == null) return;
            VisualElement empty = _listView.Q(className: "unity-collection-view__empty-label")
                                  ?? _listView.Q(className: "unity-list-view__empty-label");
            if (empty != null) empty.style.display = DisplayStyle.None;
        }

        // ---------- Events ----------

        private void OnClearClicked() => ClearConsole();

        // Clear the buffer + selection + detail, then refresh. Safe to call from Clear / play-mode / recompile / build.
        // 清空缓冲 + 选中 + 详情并刷新。可安全用于 清空 / 进入Play / 重编译 / 构建。
        internal void ClearConsole()
        {
            if (_store == null) return;
            _store.Clear();
            ResetCompileMirrorTracking(); // so a later scan can re-mirror compile messages still in the console. 便于之后重扫再镜像仍在控制台里的编译消息。
            _selectedIndex = -1;
            _listView?.ClearSelection();
            if (_detailImgui != null) UpdateDetail(null);
            _stickToBottom = true; // a just-cleared (empty) list should resume tail-following newest logs. 刚清空的（空）列表应恢复尾随最新日志。
            if (_listView != null) ForceRefresh();
        }

        private void OnSelectionChanged(IEnumerable<object> _)
        {
            _selectedIndex = _listView.selectedIndex;
            if (_selectedIndex >= 0 && _selectedIndex < _rows.Count)
                UpdateDetail(_rows[_selectedIndex].Entry);
            else
                UpdateDetail(null);
        }

        private void OnItemsChosen(IEnumerable<object> _)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _rows.Count) return;
            DebugxLogEntry e = _rows[_selectedIndex].Entry;
            List<StackFrameInfo> frames = StackTraceParser.Parse(e.StackTrace);
            if (StackTraceParser.TryGetNavigationTarget(frames, out StackFrameInfo target))
                OpenSource(target.FilePath, target.Line);
        }

        private void OnEntryForErrorPause(DebugxLogEntry entry)
        {
            if (!_errorPause || !Application.isPlaying) return;
            if (LogStatistics.SeverityOf(entry.LogType) == LogSeverity.Error)
                EditorApplication.isPaused = true;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && _clearOnPlay)
                ClearConsole();

            // Runtime source switches are only usable in Play mode; refresh their enabled state.
            // 运行时源头开关仅在 Play 模式可用；刷新其可用状态。
            if (_editorPanelVisible) RefreshEditorPanel();
        }

        private void OnCompilationStarted(object context)
        {
            if (_clearOnRecompile) ClearConsole();
        }

        // Called by the build pre-process hook (see DebugxConsoleBuildClearer).
        // 由构建预处理钩子调用（见 DebugxConsoleBuildClearer）。
        internal void ClearForBuildIfEnabled()
        {
            if (_clearOnBuild) ClearConsole();
        }

        // ---------- Helpers ----------

        private Texture IconFor(LogType type)
        {
            switch (LogStatistics.SeverityOf(type))
            {
                case LogSeverity.Warning: return _iconWarn;
                case LogSeverity.Error: return _iconError;
                default: return _iconLog;
            }
        }

        // Load a severity icon from the plugin Resources, falling back to a built-in editor console icon when absent.
        // 从插件 Resources 加载严重级别图标，缺失时回退到内置编辑器控制台图标。
        private static Texture LoadSeverityIcon(string resourceName, string builtinName)
        {
            Texture t = Resources.Load<Texture2D>(resourceName);
            return t != null ? t : EditorGUIUtility.IconContent(builtinName).image;
        }

        private static string SingleLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace('\r', ' ').Replace('\n', ' ');
        }

        private static void OpenSource(string filePath, int line)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            if (filePath.StartsWith("Assets/") || filePath.StartsWith("Assets\\"))
            {
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(filePath.Replace('\\', '/'));
                if (obj != null)
                {
                    AssetDatabase.OpenAsset(obj, line);
                    return;
                }
            }

            try
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(Path.GetFullPath(filePath), line);
            }
            catch
            {
                // No editor available for this path; ignore. 该路径无可用编辑器；忽略。
            }
        }

        // ---------- Prefs ----------

        private bool CollapseFromPrefs() => EditorPrefs.GetBool(PrefPrefix + "Collapse", false);

        private void LoadPrefs()
        {
            _criteria.ShowLog = EditorPrefs.GetBool(PrefPrefix + "ShowLog", true);
            _criteria.ShowWarning = EditorPrefs.GetBool(PrefPrefix + "ShowWarning", true);
            _criteria.ShowError = EditorPrefs.GetBool(PrefPrefix + "ShowError", true);
            _criteria.OnlyDebugx = EditorPrefs.GetBool(PrefPrefix + "OnlyDebugx", false);
            _clearOnPlay = EditorPrefs.GetBool(PrefPrefix + "ClearOnPlay", true);
            _clearOnRecompile = EditorPrefs.GetBool(PrefPrefix + "ClearOnRecompile", true);
            _clearOnBuild = EditorPrefs.GetBool(PrefPrefix + "ClearOnBuild", true);
            _errorPause = EditorPrefs.GetBool(PrefPrefix + "ErrorPause", false);
            _chineseUi = EditorPrefs.GetBool(PrefPrefix + "LangChinese", false); // default English. 默认英文。
            // Default to the field initializers (set in the ViewOptions partial) so changing those changes first-run defaults.
            // 默认取字段初始值（在 ViewOptions partial 里设定），改字段即改首次运行默认值。
            _showTimestamp = EditorPrefs.GetBool(PrefPrefix + "ShowTimestamp", _showTimestamp);
            _stackScriptOnly = EditorPrefs.GetBool(PrefPrefix + "StackScriptOnly", _stackScriptOnly);
            _store.CollapseMode = CollapseFromPrefs() ? LogCollapser.Mode.ByMessage : LogCollapser.Mode.Off;
        }

        private void SavePrefs()
        {
            EditorPrefs.SetBool(PrefPrefix + "ShowLog", _criteria.ShowLog);
            EditorPrefs.SetBool(PrefPrefix + "ShowWarning", _criteria.ShowWarning);
            EditorPrefs.SetBool(PrefPrefix + "ShowError", _criteria.ShowError);
            EditorPrefs.SetBool(PrefPrefix + "OnlyDebugx", _criteria.OnlyDebugx);
            EditorPrefs.SetBool(PrefPrefix + "ClearOnPlay", _clearOnPlay);
            EditorPrefs.SetBool(PrefPrefix + "ClearOnRecompile", _clearOnRecompile);
            EditorPrefs.SetBool(PrefPrefix + "ClearOnBuild", _clearOnBuild);
            EditorPrefs.SetBool(PrefPrefix + "ErrorPause", _errorPause);
            EditorPrefs.SetBool(PrefPrefix + "LangChinese", _chineseUi);
            EditorPrefs.SetBool(PrefPrefix + "ShowTimestamp", _showTimestamp);
            EditorPrefs.SetBool(PrefPrefix + "StackScriptOnly", _stackScriptOnly);
            if (_store != null)
                EditorPrefs.SetBool(PrefPrefix + "Collapse", _store.CollapseMode != LogCollapser.Mode.Off);
        }
    }
}
