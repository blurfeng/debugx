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
            // None hides every key an entry can carry — including members that haven't logged yet — not just the rows
            // listed in the popup. None 隐藏条目可能携带的全部 key（包括尚未打印过的成员），而不仅是弹层里列出的行。
            if (!check) _uncheckedMemberKeys.UnionWith(MemberKeyUniverse());

            foreach (KeyValuePair<int, Toggle> kv in _memberToggles)
                kv.Value.SetValueWithoutNotify(check);

            RecomputeMemberInclude();
            OnCriteriaChanged();
        }

        // The fixed set of keys an entry can carry (shared with the Editor Console), plus any key already in the buffer
        // (a member removed from the settings during Play still has entries).
        // 条目可能携带的固定 key 集合（与 Editor 版 Console 共用），再加上缓冲中已有的 key（Play 期间从设置里移除的成员仍有条目）。
        private HashSet<int> MemberKeyUniverse()
        {
            HashSet<int> keys = LogFilterCriteria.CreateAllMemberKeySet();
            foreach (KeyValuePair<int, int> kv in _store.Statistics.MemberCounts) keys.Add(kv.Key);
            return keys;
        }

        // Set criteria.VisibleMemberKeys: null when nothing is unchecked (all visible), else the universe minus unchecked,
        // so members that log later stay visible under a partial filter.
        // 设置 criteria.VisibleMemberKeys：无取消勾选时为 null（全部可见），否则为 全集 减去 取消勾选，使部分过滤下之后才打印的成员仍可见。
        private void RecomputeMemberInclude()
        {
            if (_uncheckedMemberKeys.Count == 0)
            {
                _criteria.VisibleMemberKeys = null;
                return;
            }

            HashSet<int> include = MemberKeyUniverse();
            include.ExceptWith(_uncheckedMemberKeys);
            _criteria.VisibleMemberKeys = include;
        }

        private static int CompareMemberKeys(int a, int b)
        {
            int ra = MemberSortRank(a), rb = MemberSortRank(b);
            if (ra != rb) return ra.CompareTo(rb);
            return a.CompareTo(b);
        }

        // Real members sort first (by key), then Unregistered, then Uncategorized last. Both sentinels (int.MinValue and
        // int.MinValue+1) would otherwise sort to the very top by raw value.
        // 真实成员在前（按 key），其后未注册，最后未分类。否则两个哨兵（int.MinValue 与 int.MinValue+1）会按原值排到最前。
        private static int MemberSortRank(int key)
        {
            if (key == DebugxLogEntry.UncategorizedKey) return 2;
            if (key == DebugxRawLog.UnregisteredKey) return 1;
            return 0;
        }

        private static string MemberLabel(int key, string signature)
        {
            if (key == DebugxLogEntry.UncategorizedKey) return "Uncategorized";
            if (key == DebugxRawLog.UnregisteredKey) return "Unregistered";
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
