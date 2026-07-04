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
    public class DebugxConsoleWindow : EditorWindow
    {
        private const string PrefPrefix = "Debugx.Console.";

        [MenuItem("Window/Debugx/Debugx Console")]
        public static void Open()
        {
            var window = GetWindow<DebugxConsoleWindow>();
            window.titleContent = new GUIContent("Debugx Console");
            window.minSize = new Vector2(480, 320);
        }

        private DebugxLogStore _store;
        private readonly LogFilterCriteria _criteria = new LogFilterCriteria();
        private readonly List<CollapsedRow> _rows = new List<CollapsedRow>();

        private ListView _listView;
        private Label _detailMessage;
        private VisualElement _stackContainer;
        private ScrollView _detailScroll;

        private ToolbarButton _clearButton;
        private ToolbarToggle _collapseToggle, _onlyDebugxToggle, _clearOnPlayToggle, _errorPauseToggle;
        private ToolbarSearchField _searchField;
        private ToolbarButton _langButton;

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
            SavePrefs();
        }

        public void CreateGUI()
        {
            _iconLog = EditorGUIUtility.IconContent("console.infoicon.sml").image;
            _iconWarn = EditorGUIUtility.IconContent("console.warnicon.sml").image;
            _iconError = EditorGUIUtility.IconContent("console.erroricon.sml").image;

            VisualElement root = rootVisualElement;
            root.Add(BuildToolbar());

            var split = new TwoPaneSplitView(1, 140f, TwoPaneSplitViewOrientation.Vertical);
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

            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            _searchField = new ToolbarSearchField();
            _searchField.style.width = 180;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _criteria.Search = new SearchQuery { Text = evt.newValue };
                OnCriteriaChanged();
            });
            toolbar.Add(_searchField);

            _onlyDebugxToggle = MakeToggle(string.Empty, _criteria.OnlyDebugx, evt =>
            {
                _criteria.OnlyDebugx = evt.newValue;
                OnCriteriaChanged();
            });
            toolbar.Add(_onlyDebugxToggle);

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
            img.style.width = 16;
            img.style.height = 16;
            img.style.marginRight = 2;
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
                fixedItemHeight = 20,
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
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 4;
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
                badge.text = row.Count > 999 ? "999+" : row.Count.ToString();
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
                    line.style.color = new Color(0.4f, 0.6f, 1f);
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

        private static string CountText(int n) => n > 999 ? "999+" : n.ToString();

        private void UpdateCountButtonStates()
        {
            SetCountActive(_logButton, _criteria.ShowLog);
            SetCountActive(_warnButton, _criteria.ShowWarning);
            SetCountActive(_errorButton, _criteria.ShowError);
        }

        private static void SetCountActive(VisualElement btn, bool active)
        {
            if (btn != null) btn.style.opacity = active ? 1f : 0.4f;
        }

        private void ApplyLanguage()
        {
            _clearButton.text = L("清空", "Clear");
            _collapseToggle.text = L("折叠", "Collapse");
            _clearOnPlayToggle.text = L("进入Play清空", "Clear on Play");
            _errorPauseToggle.text = L("错误暂停", "Error Pause");
            _onlyDebugxToggle.text = L("仅Debugx", "Debugx Only");
            _langButton.text = _chineseUi ? "EN" : "中";
            _langButton.tooltip = L("切换界面语言 (中/英)", "Switch UI language (EN/CN)");
        }

        private void ToggleLanguage()
        {
            _chineseUi = !_chineseUi;
            SavePrefs();
            ApplyLanguage();
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
