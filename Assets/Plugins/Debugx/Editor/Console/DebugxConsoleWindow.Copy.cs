using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Debugx Console — copying selected entries. Supports Ctrl/Cmd+C on the focused list (single or multiple
    /// selection) and a per-row right-click menu (message, or message + stack). Kept in a partial file. The list is
    /// switched to multi-selection in BuildListPane; the primary selection still drives the detail pane.
    /// Debugx Console —— 复制选中条目。支持焦点在列表上时 Ctrl/Cmd+C（单选或多选）与逐行右键菜单（消息，或消息+堆栈）。
    /// 拆到 partial 文件。列表在 BuildListPane 里切为多选；详情面板仍由主选中项驱动。
    /// </summary>
    public partial class DebugxConsoleWindow
    {
        // Ctrl/Cmd+C copies the selected entries WITH their stack traces; adding Shift copies the message only.
        // Works for single or multiple selection.
        // Ctrl/Cmd+C 复制选中条目（含堆栈）；加 Shift 仅复制消息。单选或多选均可。
        private void OnListKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.C && (evt.ctrlKey || evt.commandKey))
            {
                CopyEntries(GetSelectedEntries(), withStack: !evt.shiftKey);
                evt.StopPropagation();
            }
        }

        private List<DebugxLogEntry> GetSelectedEntries()
        {
            var list = new List<DebugxLogEntry>();
            if (_listView == null) return list;
            var indices = new List<int>(_listView.selectedIndices);
            indices.Sort();
            foreach (int i in indices)
                if (i >= 0 && i < _rows.Count) list.Add(_rows[i].Entry);
            return list;
        }

        // Right-click on a row: act on the whole selection if the row is part of it, else just this row.
        // 行右键：若该行属于当前选择则作用于整个选择，否则仅该行。
        private void BuildRowContextMenu(ContextualMenuPopulateEvent evt, VisualElement row)
        {
            int index = row.userData is int i ? i : -1;
            List<DebugxLogEntry> targets = ResolveContextTargets(index);
            if (targets.Count == 0) return;

            evt.menu.AppendAction(L("复制消息+堆栈", "Copy Message + Stack"), _ => CopyEntries(targets, withStack: true));
            evt.menu.AppendAction(L("复制消息", "Copy Message"), _ => CopyEntries(targets, withStack: false));
        }

        private List<DebugxLogEntry> ResolveContextTargets(int rowIndex)
        {
            List<DebugxLogEntry> selected = GetSelectedEntries();
            if (rowIndex >= 0 && rowIndex < _rows.Count)
            {
                DebugxLogEntry rowEntry = _rows[rowIndex].Entry;
                if (selected.Contains(rowEntry)) return selected; // row is within the selection. 该行在选择内。
                return new List<DebugxLogEntry> { rowEntry };      // otherwise just this row. 否则仅该行。
            }
            return selected;
        }

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
                    sb.Append('\n').Append(e.StackTrace).Append('\n'); // trailing blank line separates stacked entries. 结尾空行分隔多条带堆栈的条目。
            }
            EditorGUIUtility.systemCopyBuffer = sb.ToString().TrimEnd('\n');
        }
    }
}
