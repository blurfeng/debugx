using UnityEngine;

namespace DebugxLog.Console.Runtime
{
    /// <summary>
    /// Debugx runtime Console — view-option persistence. The runtime Console lives on a play-only GameObject rebuilt from
    /// scratch every launch, so (unlike the Editor Console, which persists via EditorPrefs) its toolbar state would
    /// otherwise reset each run. This partial mirrors the Editor Console's LoadPrefs/SavePrefs using PlayerPrefs
    /// (available in builds), keeping the same scope: severity filters, Debugx-Only, the timestamp column, collapse, plus
    /// the runtime-only net-tag filter. Search text and the member filter are deliberately NOT persisted, matching the
    /// Editor Console. Loaded in OnEnable (before the toolbar is built) and saved on teardown (OnDisable) and when the app
    /// is paused (OnApplicationPause, so a backgrounded-then-killed mobile app still keeps changes).
    /// Debugx 运行时 Console —— 视图选项持久化。运行时 Console 挂在仅 Play 的 GameObject 上、每次启动从零重建，故（不同于用
    /// EditorPrefs 持久化的 Editor 版）其工具栏状态本会每次运行重置。本 partial 用 PlayerPrefs（构建可用）对齐 Editor 版的
    /// LoadPrefs/SavePrefs，范围一致：严重级别过滤、仅 Debugx、时间戳列、折叠，以及运行时特有的网络标签过滤。搜索文本与成员
    /// 过滤刻意不持久化，与 Editor 版一致。于 OnEnable 加载（早于工具栏构建），于拆卸（OnDisable）及应用暂停（OnApplicationPause，
    /// 使移动端“后台被杀”也能保留改动）时保存。
    /// </summary>
    public partial class DebugxRuntimeConsole
    {
        // PlayerPrefs key prefix; distinctive so it never collides with the game's own prefs. PlayerPrefs 键前缀；足够独特，避免与游戏自身偏好冲突。
        private const string PrefPrefix = "Debugx.RuntimeConsole.";

        // PlayerPrefs has no bool accessor; store as int 0/1. PlayerPrefs 无 bool 存取，用 int 0/1 存。
        private static bool PrefGetBool(string key, bool defaultValue) => PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        private static void PrefSetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);

        // Load persisted toolbar state into the criteria / fields BEFORE the toolbar is built, so each control can sync its
        // initial value. Each default falls back to the current field/criteria initializer, so changing those changes the
        // first-run default (same convention as the Editor Console's LoadPrefs).
        // 在工具栏构建前把持久化的工具栏状态加载进 criteria/字段，使各控件能同步初始值。每个默认兜底取当前字段/criteria 初值，
        // 改初值即改首启默认（与 Editor 版 LoadPrefs 同一约定）。
        private void LoadViewPrefs()
        {
            _criteria.ShowLog = PrefGetBool(PrefPrefix + "ShowLog", _criteria.ShowLog);
            _criteria.ShowWarning = PrefGetBool(PrefPrefix + "ShowWarning", _criteria.ShowWarning);
            _criteria.ShowError = PrefGetBool(PrefPrefix + "ShowError", _criteria.ShowError);
            _criteria.OnlyDebugx = PrefGetBool(PrefPrefix + "OnlyDebugx", _criteria.OnlyDebugx);
            _showTimestamp = PrefGetBool(PrefPrefix + "ShowTimestamp", _showTimestamp);
            _criteria.NetTagMode = (NetTagFilterMode)PlayerPrefs.GetInt(PrefPrefix + "NetTagMode", (int)_criteria.NetTagMode);

            if (_store != null)
                _store.CollapseMode = PrefGetBool(PrefPrefix + "Collapse", _store.CollapseMode != LogCollapser.Mode.Off)
                    ? LogCollapser.Mode.ByMessage
                    : LogCollapser.Mode.Off;
        }

        // Persist the current toolbar state, then flush to disk. Cheap and click-driven, so flushing here is fine.
        // 保存当前工具栏状态并落盘。开销小且由点击触发，故此处落盘无碍。
        private void SaveViewPrefs()
        {
            PrefSetBool(PrefPrefix + "ShowLog", _criteria.ShowLog);
            PrefSetBool(PrefPrefix + "ShowWarning", _criteria.ShowWarning);
            PrefSetBool(PrefPrefix + "ShowError", _criteria.ShowError);
            PrefSetBool(PrefPrefix + "OnlyDebugx", _criteria.OnlyDebugx);
            PrefSetBool(PrefPrefix + "ShowTimestamp", _showTimestamp);
            PlayerPrefs.SetInt(PrefPrefix + "NetTagMode", (int)_criteria.NetTagMode);
            if (_store != null)
                PrefSetBool(PrefPrefix + "Collapse", _store.CollapseMode != LogCollapser.Mode.Off);
            PlayerPrefs.Save();
        }
    }
}
