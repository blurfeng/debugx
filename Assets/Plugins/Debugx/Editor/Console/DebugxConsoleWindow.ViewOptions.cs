using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Debugx Console — view options (the timestamp column and the stack Script-Only / Full toggle), now rendered as
    /// inline toggles in the Editor panel (see RefreshEditorPanel). This partial keeps their backing state and the
    /// helpers that consume it, so the main viewer stays focused. State persists via the shared LoadPrefs/SavePrefs.
    /// Debugx Console —— 视图选项（时间戳列、堆栈 仅脚本/完整 切换），现以内联勾选呈现在 Editor 面板中（见 RefreshEditorPanel）。
    /// 本 partial 保留其状态字段与消费它们的辅助方法，让主查看器保持聚焦。状态经共享的 LoadPrefs/SavePrefs 持久化。
    /// </summary>
    public partial class DebugxConsoleWindow
    {
        private bool _showTimestamp = true;
        private bool _stackScriptOnly; // default off: show ALL frames; when on, show only frames that have source (hides engine frames with no source). 默认关闭：显示全部帧；开启后仅显示带源码的帧（隐藏无源码的引擎帧）。

        // The View options are now rendered as inline toggles inside the Editor panel (see RefreshEditorPanel);
        // this file keeps their backing state + the helpers that consume it.
        // 视图选项现以内联勾选形式呈现在 Editor 面板中（见 RefreshEditorPanel）；本文件保留其状态字段与消费它们的辅助方法。

        // InvariantCulture 固定 ':' 分隔符：否则部分系统区域的 TimeSeparator 会把时间戳显示成 12.34.56。
        // InvariantCulture pins the ':' separator; otherwise some locales' TimeSeparator renders it as 12.34.56.
        private static string TimestampText(DebugxLogEntry e) => e.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        // Whether a stack frame is shown under the current Script-Only / Full mode.
        // 当前“仅脚本/完整”模式下是否显示某堆栈帧。
        private bool IsStackFrameVisible(StackFrameInfo frame)
        {
            if (!_stackScriptOnly) return true;
            return frame.HasSource;
        }

        // Re-render the detail pane for the current primary selection (after a view option changes).
        // 视图选项变化后，按当前主选中项重绘详情面板。
        private void RefreshSelectedDetail()
        {
            if (_detailImgui == null) return;
            if (_selectedIndex >= 0 && _selectedIndex < _rows.Count)
                UpdateDetail(_rows[_selectedIndex].Entry);
            else
                UpdateDetail(null);
        }
    }
}
