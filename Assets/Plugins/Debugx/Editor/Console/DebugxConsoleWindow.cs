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
    /// model layer and are reused by the future runtime Console. Editor-only concerns (source navigation, Error Pause,
    /// Clear on Play) are wired here.
    /// Editor 端的 Debugx Console：消费共享层 <see cref="DebugxLogStore"/> 的 UIToolkit 日志查看器。这里只有显示层——
    /// 采集/缓冲/过滤/折叠/统计都在共享模型层，未来运行时 Console 复用。Editor 专属能力（源码跳转、错误暂停、进入 Play 清空）在此接线。
    /// </summary>
    public partial class DebugxConsoleWindow : EditorWindow
    {
        private const string PrefPrefix = "Debugx.Console.";

        [MenuItem("Window/Debugx/DebugxConsole", false, 4)]
        public static void Open()
        {
            var window = GetWindow<DebugxConsoleWindow>();
            window.titleContent = new GUIContent("Debugx Console");
            window.minSize = DebugxConsoleStyle.MinWindowSize;
        }

        private DebugxLogStore _store;
        private readonly LogFilterCriteria _criteria = new LogFilterCriteria();
        private readonly List<CollapsedRow> _rows = new List<CollapsedRow>();

        private ListView _listView;
        private Label _detailMessage;
        private VisualElement _stackContainer;
        private ScrollView _detailScroll;
        private ScrollView _listScroll;      // the ListView's internal scroll view, for tail (stick-to-bottom) detection. ListView 内部滚动视图，用于 tail(贴底)检测。
        private bool _stickToBottom = true;  // auto-scroll to newest only while the list is at the bottom. 仅当列表贴底时自动滚到最新。

        private Toolbar _toolbar;
        private VisualElement _searchContainer; // flexible middle region that keeps the right group right-aligned. 弹性中部，使右侧分组保持靠右。
        private ToolbarButton _clearButton;
        private VisualElement _clearDivider; // absolute-positioned line inside Clear's right edge (custom height %). 置于 Clear 右边缘的绝对定位竖线（可自定义高度占比）。
        private ToolbarButton _clearDropdownButton;
        private Label _memberNameLabel, _memberCaretLabel; // Members button split into text + independently-sized caret. Members 按钮拆成文字 + 可独立调大小的三角。
        private ToolbarToggle _collapseToggle, _onlyDebugxToggle, _errorPauseToggle;
        private ToolbarSearchField _searchField;
        private ToolbarButton _langButton;

        // Fixed group widths (per current language) used by the responsive show/hide logic.
        // 供响应式显隐逻辑使用的固定分组宽度（按当前语言）。
        private float _wBase, _wSearch, _wFilters, _wRuntimeGroup;

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

        // Cached console icons.
        private Texture _iconLog, _iconWarn, _iconError;

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
            _iconLog = EditorGUIUtility.IconContent("console.infoicon.sml").image;
            _iconWarn = EditorGUIUtility.IconContent("console.warnicon.sml").image;
            _iconError = EditorGUIUtility.IconContent("console.erroricon.sml").image;

            VisualElement root = rootVisualElement;
            root.Add(BuildToolbar());

            _runtimePanel = BuildRuntimePanel();
            root.Add(_runtimePanel);

            var split = new TwoPaneSplitView(1, DebugxConsoleStyle.DetailPaneHeight, TwoPaneSplitViewOrientation.Vertical);
            split.style.flexGrow = 1;
            split.Add(BuildListPane());
            split.Add(BuildDetailPane());
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

            _clearButton = new ToolbarButton(OnClearClicked);
            // Divider line lives inside the Clear button (right edge) so its % height resolves against a real,
            // sized parent; styled in StyleClearSplit. 分隔线作为 Clear 按钮的子元素置于右边缘，其百分比高度可对
            // 一个有确定尺寸的父级解析；样式见 StyleClearSplit。
            _clearDivider = new VisualElement { pickingMode = PickingMode.Ignore };
            _clearButton.Add(_clearDivider);
            toolbar.Add(_clearButton);

            // Native-style: a dropdown arrow next to Clear with the "Clear on ..." options.
            // 原生风格：Clear 旁的下拉三角，内含“清空时机”选项。
            _clearDropdownButton = new ToolbarButton(ShowClearMenu) { text = "▼" };
            toolbar.Add(_clearDropdownButton);

            _collapseToggle = MakeToggle(string.Empty, CollapseFromPrefs(), evt =>
            {
                _store.CollapseMode = evt.newValue ? LogCollapser.Mode.ByMessage : LogCollapser.Mode.Off;
                SavePrefs();
                ForceRefresh();
            });
            toolbar.Add(_collapseToggle);

            _errorPauseToggle = MakeToggle(string.Empty, _errorPause, evt =>
            {
                _errorPause = evt.newValue;
                SavePrefs();
            });
            toolbar.Add(_errorPauseToggle);

            _runtimeToggle = MakeToggle(string.Empty, false, evt =>
            {
                _runtimeVisible = evt.newValue;
                _runtimePanel.style.display = _runtimeVisible ? DisplayStyle.Flex : DisplayStyle.None;
                if (_runtimeVisible) RefreshRuntimePanel();
            });
            toolbar.Add(_runtimeToggle);

            _searchField = new ToolbarSearchField();
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _criteria.Search = new SearchQuery { Text = evt.newValue };
                OnCriteriaChanged();
            });

            // The search lives in a flex-grow container so that when the search is hidden the container still fills
            // the middle, keeping the right-side group pinned to the right (blank gap instead of snapping left).
            // 搜索栏放入一个 flex-grow 容器：搜索隐藏时容器仍填充中部，使右侧分组保持靠右（中间留白，而非吸附到左边）。
            _searchContainer = new VisualElement();
            _searchContainer.style.flexDirection = FlexDirection.Row;
            _searchContainer.style.flexGrow = 1;
            _searchContainer.style.flexShrink = 1;
            _searchContainer.style.minWidth = 0;
            _searchContainer.style.alignItems = Align.Center;
            _searchContainer.style.overflow = Overflow.Hidden;
            _searchContainer.Add(_searchField);
            toolbar.Add(_searchContainer);

            _onlyDebugxToggle = MakeToggle(string.Empty, _criteria.OnlyDebugx, evt =>
            {
                _criteria.OnlyDebugx = evt.newValue;
                OnCriteriaChanged();
            });
            toolbar.Add(_onlyDebugxToggle);

            _memberButton = new ToolbarButton(ShowMemberMenu);
            // Text + caret as separate children so the caret can be sized independently (a small native-style triangle).
            // 文字与三角拆成两个子元素，让三角可独立设定大小（接近原生的小三角）。
            _memberNameLabel = new Label { pickingMode = PickingMode.Ignore };
            _memberCaretLabel = new Label("▼") { pickingMode = PickingMode.Ignore };
            _memberButton.Add(_memberNameLabel);
            _memberButton.Add(_memberCaretLabel);
            toolbar.Add(_memberButton);

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
            toolbar.Add(_logButton);
            toolbar.Add(_warnButton);
            toolbar.Add(_errorButton);

            // View options dropdown (timestamp column, stack Script-Only/Full). Same text+caret layout as Members.
            // 视图选项下拉（时间戳列、堆栈 仅脚本/完整）。与 Members 相同的 文字+三角 布局。
            _viewButton = new ToolbarButton(ShowViewMenu);
            _viewNameLabel = new Label { pickingMode = PickingMode.Ignore };
            _viewCaretLabel = new Label("▼") { pickingMode = PickingMode.Ignore };
            _viewButton.Add(_viewNameLabel);
            _viewButton.Add(_viewCaretLabel);
            toolbar.Add(_viewButton);

            _langButton = new ToolbarButton(ToggleLanguage);
            toolbar.Add(_langButton);

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

            var img = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
            img.style.width = DebugxConsoleStyle.CountIconSize;
            img.style.height = DebugxConsoleStyle.CountIconSize;
            img.style.marginRight = DebugxConsoleStyle.CountIconMarginRight;
            img.style.flexShrink = 0;

            countLabel = new Label("0");
            countLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            btn.Add(img);
            btn.Add(countLabel);
            btn.RegisterCallback<ClickEvent>(_ => onClick());
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
            _detailScroll = new ScrollView(ScrollViewMode.Vertical);
            _detailScroll.style.flexGrow = 1;

            _detailMessage = new Label { enableRichText = true };
            _detailMessage.style.whiteSpace = WhiteSpace.Normal;
            _detailMessage.style.paddingLeft = 6;
            _detailMessage.style.paddingTop = 6;
            _detailMessage.style.paddingRight = 6;

            _stackContainer = new VisualElement();
            _stackContainer.style.paddingLeft = 6;
            _stackContainer.style.paddingTop = 6;
            _stackContainer.style.paddingBottom = 6;

            _detailScroll.Add(_detailMessage);
            _detailScroll.Add(_stackContainer);
            return _detailScroll;
        }

        private void UpdateDetail(DebugxLogEntry e)
        {
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
                if (!IsStackFrameVisible(frame)) continue; // Script-Only / Full toggle (View menu). 仅脚本/完整 切换（视图菜单）。

                var line = new Label(frame.RawLine) { enableRichText = false };
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.paddingTop = 1;
                line.style.paddingBottom = 1;

                if (frame.HasSource)
                {
                    StackFrameInfo captured = frame;
                    line.style.color = DebugxConsoleStyle.StackLinkColor;
                    line.RegisterCallback<ClickEvent>(_ => OpenSource(captured.FilePath, captured.Line));
                    line.RegisterCallback<MouseEnterEvent>(_ => line.style.unityFontStyleAndWeight = FontStyle.Bold);
                    line.RegisterCallback<MouseLeaveEvent>(_ => line.style.unityFontStyleAndWeight = FontStyle.Normal);
                }

                _stackContainer.Add(line);
            }
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
            _onlyDebugxToggle.text = L("仅Debugx", "Debugx Only");
            _memberNameLabel.text = L("成员", "Members");
            _viewNameLabel.text = L("视图", "View");
            _runtimeToggle.text = L("运行时", "Runtime");
            // Show the CURRENT language, not the target one.
            // 显示当前语言，而非切换目标语言。
            _langButton.text = _chineseUi ? "中" : "EN";
            _langButton.tooltip = L("切换界面语言 (中/英)", "Switch UI language (EN/CN)");

            ApplyToolbarLayout();
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
            float runtime = Wc(DebugxConsoleStyle.RuntimeWidth);
            float debugxOnly = Wc(DebugxConsoleStyle.DebugxOnlyWidth);
            float members = Wc(DebugxConsoleStyle.MembersWidth);
            float view = Wc(DebugxConsoleStyle.ViewWidth);
            float lang = Wc(DebugxConsoleStyle.LangButtonWidth);
            float countMin = Wc(DebugxConsoleStyle.CountWidth);

            // Clear + divider + dropdown arrow styled together to match the native Clear dropdown.
            // Clear + 分隔线 + 下拉三角一起做成与原生 Clear 下拉一致的外观。
            StyleClearSplit(_clearButton, _clearDivider, _clearDropdownButton, clear, clearDropdown);
            StyleItem(_collapseToggle, collapse);
            StyleItem(_errorPauseToggle, errorPause);
            StyleItem(_runtimeToggle, runtime);
            StyleItem(_onlyDebugxToggle, debugxOnly);
            StyleMemberDropdown(_memberButton, _memberNameLabel, _memberCaretLabel, members);
            StyleMemberDropdown(_viewButton, _viewNameLabel, _viewCaretLabel, view);
            StyleItem(_langButton, lang);

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

            _wBase = clear + clearDropdown + collapse + countMin * 3f + view + lang;
            _wSearch = DebugxConsoleStyle.SearchMinWidth + DebugxConsoleStyle.SearchRightSpace;
            _wFilters = debugxOnly + members;
            _wRuntimeGroup = runtime + errorPause;

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
                main.style.borderRightWidth = 0; // no full-height border; the divider child draws a custom-height line. 不用满高边框，改由 divider 子元素画可调高度的线。
                main.style.paddingRight = 0;      // so the divider sits exactly on the right edge (boundary with the caret). 使分隔线正好落在右边缘（与三角的交界）。
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
                divider.style.width = DebugxConsoleStyle.ClearDividerWidth;
                divider.style.backgroundColor = DebugxConsoleStyle.ClearDividerColor;
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
        // search -> (Debugx Only + Members) -> (Runtime + Error Pause).
        // 工具栏过窄时依次隐藏可选分组：搜索栏 -> (仅Debugx + 成员) -> (运行时 + 错误暂停)。
        private void UpdateResponsive()
        {
            if (_toolbar == null) return;
            float avail = _toolbar.contentRect.width - DebugxConsoleStyle.ResponsiveBuffer; // buffer for item margins. 预留条目间距余量。
            if (avail <= 1f || float.IsNaN(avail)) return;

            // Fixed groups (left + right) hide only when they overflow; the flex-grow middle container keeps the right
            // group right-aligned (blank gap in the middle when the search is hidden). Hide order: filters, then runtime.
            // 固定分组（左 + 右）仅在溢出时隐藏；flex-grow 中部容器让右侧分组保持靠右（搜索隐藏时中间留白）。隐藏顺序：过滤组，再运行时组。
            bool showFilters = _wBase + _wFilters + _wRuntimeGroup <= avail;
            bool showRuntime = showFilters || _wBase + _wRuntimeGroup <= avail;

            float fixedVisible = _wBase
                + (showFilters ? _wFilters : 0f)
                + (showRuntime ? _wRuntimeGroup : 0f);

            // Search shows only if the leftover middle can hold it at its minimum width.
            // 仅当中部剩余空间能容纳搜索栏最小宽度时才显示搜索栏。
            bool showSearch = avail - fixedVisible >= _wSearch;

            SetVisible(_searchField, showSearch);
            SetVisible(_onlyDebugxToggle, showFilters);
            SetVisible(_memberButton, showFilters);
            SetVisible(_runtimeToggle, showRuntime);
            SetVisible(_errorPauseToggle, showRuntime);
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
            ResetCompileMirrorDedup(); // so a later scan can re-mirror compile messages still in the console. 便于之后重扫再镜像仍在控制台里的编译消息。
            _selectedIndex = -1;
            _listView?.ClearSelection();
            if (_stackContainer != null) UpdateDetail(null);
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
            if (_runtimeVisible) RefreshRuntimePanel();
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
