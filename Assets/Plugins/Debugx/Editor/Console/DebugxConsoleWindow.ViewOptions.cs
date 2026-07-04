using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Debugx Console — view options housed in a "View ▼" toolbar dropdown: the timestamp column and the stack
    /// Script-Only / Full toggle. Kept in a partial file so the main viewer stays focused. State persists via the
    /// shared LoadPrefs/SavePrefs.
    /// Debugx Console —— 收进「视图 ▼」工具栏下拉里的视图选项：时间戳列、堆栈 仅脚本/完整 切换。拆到 partial 文件让主
    /// 查看器保持聚焦。状态经共享的 LoadPrefs/SavePrefs 持久化。
    /// </summary>
    public partial class DebugxConsoleWindow
    {
        private ToolbarButton _viewButton;
        private Label _viewNameLabel, _viewCaretLabel;

        private bool _showTimestamp = true;
        private bool _stackScriptOnly; // default: show only script frames (hide engine / Debugx-internal). 默认仅显示脚本帧（隐藏引擎/Debugx 内部帧）。

        private void ShowViewMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(L("显示时间戳", "Show Timestamp")), _showTimestamp, () =>
            {
                _showTimestamp = !_showTimestamp;
                SavePrefs();
                _listView?.RefreshItems(); // re-bind rows to show/hide the timestamp column. 重绑行以显隐时间戳列。
            });
            menu.AddItem(new GUIContent(L("堆栈：仅脚本", "Stack: Script Only")), _stackScriptOnly, () =>
            {
                _stackScriptOnly = !_stackScriptOnly;
                SavePrefs();
                RefreshSelectedDetail();
            });
            menu.ShowAsContext();
        }

        private static string TimestampText(DebugxLogEntry e) => e.Timestamp.ToString("HH:mm:ss");

        // Whether a stack frame is shown under the current Script-Only / Full mode.
        // 当前“仅脚本/完整”模式下是否显示某堆栈帧。
        private bool IsStackFrameVisible(StackFrameInfo frame)
        {
            if (!_stackScriptOnly) return true;
            return frame.HasSource && !StackTraceParser.IsDebugxInternalFrame(frame);
        }

        // Re-render the detail pane for the current primary selection (after a view option changes).
        // 视图选项变化后，按当前主选中项重绘详情面板。
        private void RefreshSelectedDetail()
        {
            if (_stackContainer == null) return;
            if (_selectedIndex >= 0 && _selectedIndex < _rows.Count)
                UpdateDetail(_rows[_selectedIndex].Entry);
            else
                UpdateDetail(null);
        }
    }
}
