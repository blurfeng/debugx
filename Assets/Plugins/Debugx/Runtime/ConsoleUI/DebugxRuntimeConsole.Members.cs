using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Console.Runtime
{
    /// <summary>
    /// Runtime Console — the member (Debugx category) filter (B1). Runtime has no GenericMenu, so this builds a custom
    /// popup overlay of per-member toggles (color swatch + signature + count) plus All / None. The member universe is
    /// derived from the entries actually present in the buffer — a runtime overlay filters logs that EXIST, so members
    /// that have not logged yet are simply not listed. Feeds <see cref="LogFilterCriteria.VisibleMemberKeys"/>
    /// (null = all visible; a set = only those keys).
    /// 运行时 Console —— 成员（Debugx 分类）过滤（B1）。运行时无 GenericMenu，故自建每成员开关的弹层（色块 + 签名 + 计数）
    /// 加 All / None。成员全集从缓冲里实际存在的条目派生——运行时覆盖层过滤“已存在”的日志，故尚未打印过的成员不列出。
    /// 填充 <see cref="LogFilterCriteria.VisibleMemberKeys"/>（null=全部可见；集合=仅这些 key）。
    /// </summary>
    public partial class DebugxRuntimeConsole
    {
        private Button _memberButton;
        private VisualElement _memberPopup;
        private VisualElement _memberListContainer;
        private readonly Dictionary<int, Toggle> _memberToggles = new Dictionary<int, Toggle>();

        // Keys the user has explicitly unchecked (hidden). Persisted across popup rebuilds so reopening keeps state.
        // 用户显式取消勾选（隐藏）的 key。跨弹层重建保留，故重开时状态不丢。
        private readonly HashSet<int> _uncheckedMemberKeys = new HashSet<int>();

        // Distinct-member count last applied to the criteria; used to re-apply the include set when a brand-new member
        // appears while a partial filter is active (keeps newly-seen members visible instead of silently hidden).
        // 上次应用到过滤条件时的去重成员数；用于在启用部分过滤时若出现全新成员则重算 include 集（让新成员保持可见而非被静默隐藏）。
        private int _lastMemberUniverseCount = -1;
        private bool _memberPopupOpen;

        private const float MemberPopupWidth = 230f;

        private Button BuildMemberButton()
        {
            _memberButton = new Button(ToggleMemberPopup) { text = "Members" };
            StyleToolbarButton(_memberButton);
            return _memberButton;
        }

        private VisualElement BuildMemberPopup()
        {
            var popup = new VisualElement();
            popup.style.position = Position.Absolute;
            popup.style.left = 8; // fallback; repositioned under the Members button on open (PositionPopupUnderButton). 兜底值；打开时定位到 Members 按钮下方。
            popup.style.top = 48;
            popup.style.width = MemberPopupWidth;
            popup.style.maxHeight = Length.Percent(70);
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

            var all = new Button(() => SetAllMembers(true)) { text = "All" };
            StyleToolbarButton(all);
            var none = new Button(() => SetAllMembers(false)) { text = "None" };
            StyleToolbarButton(none);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Button close = BuildCloseButton(() => SetMemberPopup(false)); // red × at the popup's top-right. 弹层右上角红叉。
            header.Add(all);
            header.Add(none);
            header.Add(spacer);
            header.Add(close);
            popup.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            _memberListContainer = new VisualElement();
            scroll.Add(_memberListContainer);
            popup.Add(scroll);

            _memberPopup = popup;
            return popup;
        }

        private void ToggleMemberPopup() => SetMemberPopup(!_memberPopupOpen);

        private void SetMemberPopup(bool open)
        {
            _memberPopupOpen = open;
            if (open)
            {
                SetSourcePopup(false); // only one popup at a time. 同时只开一个弹层。
                RebuildMemberList();
                PositionPopupUnderButton(_memberPopup, _memberButton, MemberPopupWidth);
            }
            if (_memberPopup != null)
                _memberPopup.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Rebuild the member rows from the distinct member keys currently present in the buffer (with a representative
        // signature + color for each). Called on popup open. 从缓冲中当前存在的去重成员 key 重建成员行（各取一个代表签名 + 颜色）。弹层打开时调用。
        private void RebuildMemberList()
        {
            _memberListContainer.Clear();
            _memberToggles.Clear();

            var keys = new List<int>();
            var meta = new Dictionary<int, (string sig, string color)>();
            LogRingBuffer buf = _store.Buffer;
            int n = buf.Count;
            for (int i = 0; i < n; i++)
            {
                DebugxLogEntry e = buf[i];
                if (e == null) continue;
                if (!meta.ContainsKey(e.MemberKey))
                {
                    meta[e.MemberKey] = (e.MemberSignature, e.ColorHex);
                    keys.Add(e.MemberKey);
                }
            }

            if (keys.Count == 0)
            {
                var empty = new Label("(no members yet)");
                empty.style.color = DebugxRuntimeConsoleStyle.TimestampColor;
                empty.style.paddingLeft = 6;
                empty.style.paddingTop = 4;
                empty.style.paddingBottom = 4;
                _memberListContainer.Add(empty);
                return;
            }

            keys.Sort(CompareMemberKeys);

            foreach (int key in keys)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingLeft = 4;
                row.style.paddingRight = 4;
                row.style.paddingTop = 1;
                row.style.paddingBottom = 1;

                var swatch = new VisualElement();
                swatch.style.width = 12;
                swatch.style.height = 12;
                swatch.style.flexShrink = 0;
                swatch.style.marginRight = 6;
                swatch.style.backgroundColor = MemberColor(meta[key].color);

                // Empty checkbox + a separate name label (same tight layout as the toolbar toggles). 空勾选框 + 独立名字 Label（与工具栏勾选同样的紧贴布局）。
                var toggle = new Toggle();
                toggle.SetValueWithoutNotify(!_uncheckedMemberKeys.Contains(key));
                Label innerLabel = toggle.Q<Label>();
                if (innerLabel != null) { innerLabel.style.minWidth = 0; innerLabel.style.width = StyleKeyword.Auto; }

                int capturedKey = key;
                toggle.RegisterValueChangedCallback(evt => OnMemberToggle(capturedKey, evt.newValue));

                var nameLabel = new Label(MemberLabel(key, meta[key].sig));
                nameLabel.style.flexGrow = 1;
                nameLabel.style.marginLeft = 3;
                nameLabel.style.color = DebugxRuntimeConsoleStyle.TextColor;
                nameLabel.style.fontSize = DebugxRuntimeConsoleStyle.FontSizeSmall;

                var count = new Label(_store.Statistics.CountForMember(key).ToString());
                count.style.color = DebugxRuntimeConsoleStyle.TimestampColor;
                count.style.fontSize = DebugxRuntimeConsoleStyle.FontSizeSmall;
                count.style.marginLeft = 4;

                row.Add(swatch);
                row.Add(toggle);
                row.Add(nameLabel);
                row.Add(count);
                _memberToggles[key] = toggle;
                _memberListContainer.Add(row);
            }
        }

        private void OnMemberToggle(int key, bool isChecked)
        {
            if (isChecked) _uncheckedMemberKeys.Remove(key);
            else _uncheckedMemberKeys.Add(key);
            RecomputeMemberInclude();
            OnCriteriaChanged();
        }

        private void SetAllMembers(bool check)
        {
            _uncheckedMemberKeys.Clear();
            if (!check)
            {
                foreach (KeyValuePair<int, Toggle> kv in _memberToggles)
                    _uncheckedMemberKeys.Add(kv.Key);
            }

            foreach (KeyValuePair<int, Toggle> kv in _memberToggles)
                kv.Value.SetValueWithoutNotify(check);

            RecomputeMemberInclude();
            OnCriteriaChanged();
        }

        // Set criteria.VisibleMemberKeys: null when nothing is unchecked (all visible), else the seen keys minus unchecked.
        // 设置 criteria.VisibleMemberKeys：无取消勾选时为 null（全部可见），否则为 已见 key 减去 取消勾选 的集合。
        private void RecomputeMemberInclude()
        {
            _lastMemberUniverseCount = _store.Statistics.MemberCounts.Count;

            if (_uncheckedMemberKeys.Count == 0)
            {
                _criteria.VisibleMemberKeys = null;
                return;
            }

            HashSet<int> include = _criteria.VisibleMemberKeys ?? new HashSet<int>();
            include.Clear();
            foreach (KeyValuePair<int, int> kv in _store.Statistics.MemberCounts)
                if (!_uncheckedMemberKeys.Contains(kv.Key))
                    include.Add(kv.Key);
            _criteria.VisibleMemberKeys = include;
        }

        // Called from RefreshView: when a partial member filter is active and a brand-new member appears, re-apply the
        // include set so the new member stays visible. Deferred (mark the store dirty; next tick rebuilds) to avoid
        // recursing into RefreshView.
        // 由 RefreshView 调用：启用部分成员过滤时若出现全新成员，重算 include 集使新成员保持可见。延迟处理（标记 store 为脏，
        // 下一帧重建），避免递归进 RefreshView。
        private void ReapplyMemberFilterOnDrift()
        {
            if (_uncheckedMemberKeys.Count == 0) return;
            if (_store.Statistics.MemberCounts.Count == _lastMemberUniverseCount) return;
            RecomputeMemberInclude();
            _store.SetFilterCriteria(_criteria);
        }

        private static int CompareMemberKeys(int a, int b)
        {
            bool ua = a == DebugxLogEntry.UncategorizedKey;
            bool ub = b == DebugxLogEntry.UncategorizedKey;
            if (ua != ub) return ua ? 1 : -1; // Uncategorized sinks to the bottom. 未分类沉底。
            return a.CompareTo(b);
        }

        private static string MemberLabel(int key, string signature)
        {
            if (key == DebugxLogEntry.UncategorizedKey) return "Uncategorized";
            return string.IsNullOrEmpty(signature) ? "Key " + key : signature;
        }

        private static Color MemberColor(string colorHex)
        {
            if (!string.IsNullOrEmpty(colorHex) &&
                ColorUtility.TryParseHtmlString("#" + colorHex, out Color c))
                return c;
            return DebugxRuntimeConsoleStyle.LogColor; // neutral for members without a color. 无色成员用中性色。
        }
    }
}
