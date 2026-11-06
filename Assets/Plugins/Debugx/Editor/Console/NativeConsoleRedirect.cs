using System;
using System.Reflection;
using UnityEditor;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Optional "replace the native Console": when enabled, any open instance of Unity's native Console window (opened by
    /// clicking the status-bar log, Ctrl+Shift+C, Window > General > Console, or restored with a layout) is closed and
    /// the Debugx Console is opened/focused instead. Also, while enabled, clearing the Debugx Console clears Unity's
    /// native log store too, which is what resets the bottom status-bar message. Runs from editor load, so the redirect
    /// works even while the Debugx Console is closed.
    /// 可选的“替代原生 Console”：开启后，原生 Console 窗口的任何实例（点击状态栏日志、Ctrl+Shift+C、
    /// Window > General > Console 打开，或随布局恢复）都会被关闭，并改为打开/聚焦 Debugx Console。开启期间，
    /// 清空 Debugx Console 也会清空 Unity 原生日志存储——这正是重置底部状态栏文字的方式。随编辑器加载运行，
    /// 故 Debugx Console 关着时重定向也生效。
    /// </summary>
    [InitializeOnLoad]
    internal static class NativeConsoleRedirect
    {
        private const string PrefKey = "Debugx.Console.RedirectNativeConsole";
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        // Internal types/members; any may be null if a future Unity renames them (that part then silently does nothing).
        // 内部类型/成员；若未来 Unity 改名则可能为 null（对应功能静默失效）。
        private static readonly Type NativeConsoleType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
        // ConsoleWindow keeps its single live instance here (set in OnEnable, cleared in OnDisable): a cheap per-frame
        // check that catches the window however it was opened — focus alone misses the status-bar click.
        // ConsoleWindow 的唯一活动实例存于此（OnEnable 设置、OnDisable 清空）：每帧廉价检查，无论以何种方式打开都能捕获——仅看焦点会漏掉状态栏点击。
        private static readonly FieldInfo InstanceField = NativeConsoleType?.GetField("ms_ConsoleWindow", StaticFlags);
        private static readonly MethodInfo LogEntriesClear = typeof(EditorWindow).Assembly
            .GetType("UnityEditor.LogEntries")?.GetMethod("Clear", StaticFlags, null, Type.EmptyTypes, null);

        private static bool _enabled;

        static NativeConsoleRedirect()
        {
            _enabled = EditorPrefs.GetBool(PrefKey, false);
            EditorApplication.update += OnUpdate;
        }

        /// <summary>Per-user switch (EditorPrefs), off by default. 各用户开关（EditorPrefs），默认关闭。</summary>
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                EditorPrefs.SetBool(PrefKey, value);
            }
        }

        /// <summary>
        /// When enabled, clears Unity's native log store (same as the native Console's Clear), which also clears the
        /// status-bar message. Compile errors stay, as with the native Clear.
        /// 开启时清空 Unity 原生日志存储（等同原生 Console 的 Clear），从而同时清掉状态栏文字。编译错误会保留，与原生 Clear 一致。
        /// </summary>
        public static void ClearNativeIfEnabled()
        {
            if (_enabled) LogEntriesClear?.Invoke(null, null);
        }

        private static void OnUpdate()
        {
            if (!_enabled || NativeConsoleType == null) return;
            EditorWindow native = InstanceField != null
                ? InstanceField.GetValue(null) as EditorWindow
                : EditorWindow.focusedWindow; // fallback if the field is renamed. 字段改名时的回退。
            if (native == null || native.GetType() != NativeConsoleType) return;
            native.Close();
            DebugxConsoleWindow.Open();
        }
    }
}
