using System.Collections.Generic;
using System.IO;
using UnityEditor;
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

        private Toolbar _toolbar;
        private VisualElement _searchContainer; // flexible middle region that keeps the right group right-aligned. 弹性中部，使右侧分组保持靠右。
        private ToolbarButton _clearButton;
        private ToolbarToggle _collapseToggle, _onlyDebugxToggle, _clearOnPlayToggle, _errorPauseToggle;
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
        private bool _clearOnPlay;
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

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            DebugxProjectSettingsAsset.OnApplyTo.Bind(OnSettingsApplied);
        }

        private void OnDisable()
        {
            if (_store != null)
            {
                _store.Collector.EntryProduced -= OnEntryForErrorPause;
                _store.Stop();
            }
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            DebugxProjectSettingsAsset.OnApplyTo.Unbind(OnSettingsApplied);
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
            toolbar.Add(_clearButton);

            _collapseToggle = MakeToggle(string.Empty, CollapseFromPrefs(), evt =>
            {
                _store.CollapseMode = evt.newValue ? LogCollapser.Mode.ByMessage : LogCollapser.Mode.Off;
                SavePrefs();
                ForceRefresh();
            });
            toolbar.Add(_collapseToggle);

            _clearOnPlayToggle = MakeToggle(string.Empty, _clearOnPlay, evt =>
            {
                _clearOnPlay = evt.newValue;
                SavePrefs();
            });
            toolbar.Add(_clearOnPlayToggle);

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
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = _rows,
            };
            _listView.style.flexGrow = 1;
            _listView.selectionChanged += OnSelectionChanged;
            _listView.itemsChosen += OnItemsChosen;
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
            row.Add(msg);
            row.Add(badge);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            CollapsedRow row = _rows[index];
            DebugxLogEntry e = row.Entry;

            element.Q<Image>("icon").image = IconFor(e.LogType);

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
            _store.Pump();
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
            _rows.Clear();
            IReadOnlyList<CollapsedRow> src = _store.Rows;
            for (int i = 0; i < src.Count; i++)
                _rows.Add(src[i]);

            _listView.RefreshItems();
            HideListEmptyLabel();
            UpdateCounts();

            // Auto-scroll to bottom while the user is not inspecting a specific entry.
            // 用户未在查看某条时，自动滚到底。
            if (_selectedIndex < 0 && _rows.Count > 0)
                _listView.ScrollToItem(_rows.Count - 1);
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
            _collapseToggle.text = L("折叠", "Collapse");
            _clearOnPlayToggle.text = L("进入Play清空", "Clear on Play");
            _errorPauseToggle.text = L("错误暂停", "Error Pause");
            _onlyDebugxToggle.text = L("仅Debugx", "Debugx Only");
            _memberButton.text = L("成员 ▾", "Members ▾");
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
            float collapse = Wc(DebugxConsoleStyle.CollapseWidth);
            float clearOnPlay = Wc(DebugxConsoleStyle.ClearOnPlayWidth);
            float errorPause = Wc(DebugxConsoleStyle.ErrorPauseWidth);
            float runtime = Wc(DebugxConsoleStyle.RuntimeWidth);
            float debugxOnly = Wc(DebugxConsoleStyle.DebugxOnlyWidth);
            float members = Wc(DebugxConsoleStyle.MembersWidth);
            float lang = Wc(DebugxConsoleStyle.LangButtonWidth);
            float countMin = Wc(DebugxConsoleStyle.CountWidth);

            StyleItem(_clearButton, clear);
            StyleItem(_collapseToggle, collapse);
            StyleItem(_clearOnPlayToggle, clearOnPlay);
            StyleItem(_errorPauseToggle, errorPause);
            StyleItem(_runtimeToggle, runtime);
            StyleItem(_onlyDebugxToggle, debugxOnly);
            StyleItem(_memberButton, members);
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

            _wBase = clear + collapse + countMin * 3f + lang;
            _wSearch = DebugxConsoleStyle.SearchMinWidth + DebugxConsoleStyle.SearchRightSpace;
            _wFilters = debugxOnly + members;
            _wRuntimeGroup = runtime + errorPause + clearOnPlay;

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

        // Progressively hide optional groups when the toolbar is too narrow:
        // search -> (Debugx Only + Members) -> (Runtime + Error Pause + Clear on Play).
        // 工具栏过窄时依次隐藏可选分组：搜索栏 -> (仅Debugx + 成员) -> (运行时 + 错误暂停 + 进入Play清空)。
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
            SetVisible(_clearOnPlayToggle, showRuntime);
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

        private void OnClearClicked()
        {
            _store.Clear();
            _selectedIndex = -1;
            _listView.ClearSelection();
            UpdateDetail(null);
            ForceRefresh();
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
            {
                _store.Clear();
                _selectedIndex = -1;
                UpdateDetail(null);
                ForceRefresh();
            }

            // Runtime source switches are only usable in Play mode; refresh their enabled state.
            // 运行时源头开关仅在 Play 模式可用；刷新其可用状态。
            if (_runtimeVisible) RefreshRuntimePanel();
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
            _clearOnPlay = EditorPrefs.GetBool(PrefPrefix + "ClearOnPlay", false);
            _errorPause = EditorPrefs.GetBool(PrefPrefix + "ErrorPause", false);
            _chineseUi = EditorPrefs.GetBool(PrefPrefix + "LangChinese", false); // default English. 默认英文。
            _store.CollapseMode = CollapseFromPrefs() ? LogCollapser.Mode.ByMessage : LogCollapser.Mode.Off;
        }

        private void SavePrefs()
        {
            EditorPrefs.SetBool(PrefPrefix + "ShowLog", _criteria.ShowLog);
            EditorPrefs.SetBool(PrefPrefix + "ShowWarning", _criteria.ShowWarning);
            EditorPrefs.SetBool(PrefPrefix + "ShowError", _criteria.ShowError);
            EditorPrefs.SetBool(PrefPrefix + "OnlyDebugx", _criteria.OnlyDebugx);
            EditorPrefs.SetBool(PrefPrefix + "ClearOnPlay", _clearOnPlay);
            EditorPrefs.SetBool(PrefPrefix + "ErrorPause", _errorPause);
            EditorPrefs.SetBool(PrefPrefix + "LangChinese", _chineseUi);
            if (_store != null)
                EditorPrefs.SetBool(PrefPrefix + "Collapse", _store.CollapseMode != LogCollapser.Mode.Off);
        }
    }
}
