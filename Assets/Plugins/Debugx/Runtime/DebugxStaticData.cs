using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DebugxLog
{
    public static class DebugxStaticData
    {
        public static readonly bool IsChineseSimplified = Application.systemLanguage == SystemLanguage.ChineseSimplified;
        // Project-owned folder outside the plugin, so package upgrades never overwrite the settings asset.
        // 插件目录之外、属于项目自己的目录，插件升级不会覆盖设置资源。
        private const string FolderPath = "/DebugxSettings/Resources";
        public static readonly string ResourcesPathRelative  = "Assets" + FolderPath;
        /// <summary>
        /// Configuration storage folder.
        /// 配置存储文件夹。
        /// </summary>
        public static string ResourcesPathAbsolute
        {
            get
            {
                if (string.IsNullOrEmpty(_resourcesPath))
                {
                    _resourcesPath = Application.dataPath + FolderPath;
                    Debugx.LogAdm($"ResourcesPath: {_resourcesPath}");

                    //确认文件夹是否存在，否则创建（仅首次解析路径时检查，避免每次访问都做文件系统调用）
                    //Ensure the folder exists (only on first resolve, to avoid a filesystem call on every access).
                    if (!Directory.Exists(_resourcesPath))
                        Directory.CreateDirectory(_resourcesPath);
                }

                return _resourcesPath;
            }
        }

        private static string _resourcesPath;

        #region Tools

        // Per-user settings store. In the editor it is EditorPrefs scoped to this project, not PlayerPrefs: the editor's
        // PlayerPrefs are the game's own, so a game calling PlayerPrefs.DeleteAll() (a typical "reset save") wiped every
        // Debugx setting; EditorPrefs is shared by all projects on the machine, hence the project prefix. A value saved
        // by an older version in PlayerPrefs is still read until it is overwritten. Builds keep using PlayerPrefs.
        // Editor values are cached so the settings pages, which write fold states on every GUI event, don't hit the
        // registry each repaint.
        // 个人设置存储。编辑器中为按项目区分的 EditorPrefs，而非 PlayerPrefs：编辑器里的 PlayerPrefs 就是游戏自己的存储，
        // 游戏调用 PlayerPrefs.DeleteAll()（常见的“重置存档”）会清掉 Debugx 的全部设置；EditorPrefs 由本机所有项目共享，故加项目前缀。
        // 旧版本存在 PlayerPrefs 里的值在被覆盖前仍会读取。打包版本仍用 PlayerPrefs。编辑器中的值有内存缓存，避免设置页每个 GUI
        // 事件都写折叠状态时反复读写注册表。
#if UNITY_EDITOR
        private static string _editorKeyPrefix;
        private static readonly Dictionary<string, int> _editorIntCache = new Dictionary<string, int>();

        private static string EditorKey(string key)
        {
            if (_editorKeyPrefix == null) _editorKeyPrefix = "Debugx[" + Application.dataPath + "].";
            return _editorKeyPrefix + key;
        }
#endif

        private static int PrefsGetInt(string key, int defaultValue)
        {
#if UNITY_EDITOR
            if (_editorIntCache.TryGetValue(key, out int cached)) return cached;
            string editorKey = EditorKey(key);
            int value = UnityEditor.EditorPrefs.HasKey(editorKey)
                ? UnityEditor.EditorPrefs.GetInt(editorKey, defaultValue)
                : PlayerPrefs.GetInt(key, defaultValue);
            _editorIntCache[key] = value;
            return value;
#else
            return PlayerPrefs.GetInt(key, defaultValue);
#endif
        }

        private static void PrefsSetInt(string key, int value)
        {
#if UNITY_EDITOR
            if (_editorIntCache.TryGetValue(key, out int cached) && cached == value) return;
            _editorIntCache[key] = value;
            UnityEditor.EditorPrefs.SetInt(EditorKey(key), value);
#else
            PlayerPrefs.SetInt(key, value);
#endif
        }

        private static string PrefsGetString(string key)
        {
#if UNITY_EDITOR
            string editorKey = EditorKey(key);
            if (UnityEditor.EditorPrefs.HasKey(editorKey)) return UnityEditor.EditorPrefs.GetString(editorKey);
#endif
            return PlayerPrefs.GetString(key);
        }

        private static void PrefsSetString(string key, string value)
        {
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetString(EditorKey(key), value);
#else
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save(); // EditorPrefs write through; PlayerPrefs need an explicit save. EditorPrefs 直接落盘；PlayerPrefs 需显式保存。
#endif
        }

        public static bool PlayerPrefsGetBool(string key, bool defaultValue)
        {
            return PrefsGetInt(key, defaultValue ? 1 : 2) == 1;
        }

        public static void PlayerPrefsSetBool(string key, bool value)
        {
            PrefsSetInt(key, value ? 1 : 2);
        }

        public static void PlayerPrefsDeleteKey(string key)
        {
#if UNITY_EDITOR
            _editorIntCache.Remove(key);
            UnityEditor.EditorPrefs.DeleteKey(EditorKey(key));
#endif
            PlayerPrefs.DeleteKey(key);
        }

        #endregion

        public static bool EnableAwakeTestLog
        {
            get => PlayerPrefsGetBool("DebugxStaticData.EnableAwakeTestLog", false);
            set => PlayerPrefsSetBool("DebugxStaticData.EnableAwakeTestLog", value);
        }

        // Start runs once, so a plain PlayerPrefs read (like Awake) is enough — no per-frame cache needed.
        // Start 只跑一次，故与 Awake 一样直接读 PlayerPrefs 即可——无需 Update 那样的逐帧缓存。
        public static bool EnableStartTestLog
        {
            get => PlayerPrefsGetBool("DebugxStaticData.EnableStartTestLog", false);
            set => PlayerPrefsSetBool("DebugxStaticData.EnableStartTestLog", value);
        }

        // 0=未读取 1=true 2=false。缓存避免 DebugxManager.Update 每帧读取 PlayerPrefs；setter 同步缓存，保证运行时切换即时生效。
        // 0=unread 1=true 2=false. Cached to avoid a per-frame PlayerPrefs read in DebugxManager.Update; the setter keeps the cache in sync.
        private static byte _enableUpdateTestLog;
        public static bool EnableUpdateTestLog
        {
            get
            {
                if (_enableUpdateTestLog == 0)
                    _enableUpdateTestLog = (byte)(PlayerPrefsGetBool("DebugxStaticData.EnableUpdateTestLog", false) ? 1 : 2);
                return _enableUpdateTestLog == 1;
            }
            set
            {
                PlayerPrefsSetBool("DebugxStaticData.EnableUpdateTestLog", value);
                _enableUpdateTestLog = (byte)(value ? 1 : 2);
            }
        }

        // Whether the in-game runtime overlay Console (DebugxRuntimeConsole) self-creates at play start. Default OFF.
        // Read once by DebugxRuntimeConsole.Bootstrap, so a change applies on the NEXT entry to Play. In the Editor the
        // docked Debugx Console window already fills the same role, so the overlay is opt-in: tick it in the Editor Console,
        // or set this true from game code. A player build has its own (initially empty) PlayerPrefs store, so it too
        // defaults off there — the Editor tick does NOT carry into a build; enable the overlay in-build from game code.
        // 游戏内运行时覆盖层 Console（DebugxRuntimeConsole）是否在游戏启动时自建。默认关闭。由 DebugxRuntimeConsole.Bootstrap
        // 读取一次，故改动在下次进入 Play 时生效。编辑器已有停靠的 Debugx Console 窗口担同样角色，故覆盖层为按需开启：在 Editor
        // Console 里勾选，或在游戏代码中置 true。实机构建用自身（初始为空）的 PlayerPrefs 存储，故那里也默认关闭——编辑器里的
        // 勾选不会带进构建；实机需在游戏代码中启用覆盖层。
        public static bool RuntimeConsoleEnabled
        {
            get => PlayerPrefsGetBool("DebugxStaticData.RuntimeConsoleEnabled", false);
            set => PlayerPrefsSetBool("DebugxStaticData.RuntimeConsoleEnabled", value);
        }

        // Ring-buffer capacity of the in-game runtime overlay Console (DebugxRuntimeConsole). Read once when the buffer is
        // created (EarlyCapture / OnEnable). The Console's Settings popup exposes a "Capacity" field that live-resizes the
        // active buffer AND writes this value, so the chosen size also persists to the next Play. Editor-only PlayerPrefs
        // like RuntimeConsoleEnabled — a player build keeps its own store and falls back to the default below (the Editor
        // value does NOT carry into a build). §8 rule of thumb: 500–1000 mobile / 2000–5000 desktop.
        // 游戏内运行时覆盖层 Console（DebugxRuntimeConsole）的环形缓冲容量。在缓冲创建时读取一次（EarlyCapture / OnEnable）。
        // Console 的 Settings 弹层有一个 “Capacity” 输入框，会即时改变当前缓冲大小并写入此值，故所选尺寸也会沿用到下次 Play。
        // 与 RuntimeConsoleEnabled 一样是编辑器 PlayerPrefs——实机构建用自身存储并回退到下方默认值（编辑器里的值不会带进构建）。
        // §8 经验值：移动 500–1000 / 桌面 2000–5000。
        public const int RuntimeConsoleBufferCapacityDefault = 4000;
        public static int RuntimeConsoleBufferCapacity
        {
            get => Mathf.Max(1, PrefsGetInt("DebugxStaticData.RuntimeConsoleBufferCapacity", RuntimeConsoleBufferCapacityDefault));
            set => PrefsSetInt("DebugxStaticData.RuntimeConsoleBufferCapacity", Mathf.Max(1, value));
        }

        #region Text
        public const string ToolTipDefaultDebugxMemberAssets = "默认调试成员信息列表";
        public const string ToolTipCustomDebugxMemberAssets = "自定义调试成员信息列表";

        public const string ToolTipEnableLogDefault = "Log总开关，启动时默认状态";
        public const string ToolTipEnableLogMemberDefault = "成员Log总开关，启动时默认状态";
        public const string ToolTipAllowUnregisteredMember = "允许没有注册成员进行打印";
        public const string ToolTipLogThisKeyMemberOnlyDefault = "仅打印此Key的成员Log，0为关闭。启动时默认状态";

        public const string ToolTipLogOutput = "输出Log到本地（启动前设置，运行时设置无效）。编辑器时输出到项目的Logs文件夹下，实机运行时根据平台输出到不同目录下";
        public const string ToolTipEnableLogStackTrace = "输出Log类型的堆栈跟踪";
        public const string ToolTipEnableWarningStackTrace = "输出Warning类型的堆栈跟踪";
        public const string ToolTipEnableErrorStackTrace = "输出Error类型的堆栈跟踪";
        public const string ToolTipRecordAllNonDebugxLogs = "记录所有非Debugx打印的Log";

        #endregion

        #region Default Value
        // The default value of the parameter, used to restore to the default parameter function.
        // The actual usage data in the DLL, namely DebugxProjectSettings, is controlled by DebugxProjectSettingsAsset. Its default value is not significant.
        // 参数的默认值，用于恢复到默认参数功能。
        // dll中的实际使用数据DebugxProjectSettings受到DebugxProjectSettingsAsset支配，其默认值不重要。

        public const bool EnableLogDefaultSet = true;
        public const bool EnableLogMemberDefaultSet = true;
        public const bool AllowUnregisteredMemberSet = true;
        public const int LogThisKeyMemberOnlyDefaultSet = 0;

        public const bool LogOutputSet = false;
        public const bool EnableLogStackTraceSet = false;
        public const bool EnableWarningStackTraceSet = false;
        public const bool EnableErrorStackTraceSet = true;
        public const bool RecordAllNonDebugxLogsSet = false;

        #endregion

        #region Preferences

        /// <summary>
        /// Reset user settings.
        /// 重置用户设置。
        /// </summary>
        public static void ResetPreferences()
        {
            EnableLogDefaultPrefs = DebugxStaticData.EnableLogDefaultSet;
            EnableLogMemberDefaultPrefs = DebugxStaticData.EnableLogMemberDefaultSet;
            AllowUnregisteredMember = DebugxStaticData.AllowUnregisteredMemberSet;
            LogThisKeyMemberOnlyDefaultPrefs = DebugxStaticData.LogThisKeyMemberOnlyDefaultSet;

            LogOutputPrefs = DebugxStaticData.LogOutputSet;
            EnableLogStackTracePrefs = DebugxStaticData.EnableLogStackTraceSet;
            EnableWarningStackTracePrefs = DebugxStaticData.EnableWarningStackTraceSet;
            EnableErrorStackTracePrefs = DebugxStaticData.EnableErrorStackTraceSet;
            RecordAllNonDebugxLogsPrefs = DebugxStaticData.RecordAllNonDebugxLogsSet;

            ResetPreferencesMembers();
        }

        public static void ResetPreferencesMembers()
        {
            MemberEnableDefaultDicPrefs.Clear();
            PlayerPrefsDeleteKey("DebugxStaticData.MemberEnableDefaultDic");
        }

        public static bool EnableLogDefaultPrefs
        {
            get => PlayerPrefsGetBool("DebugxStaticData.EnableLogDefault", DebugxStaticData.EnableLogDefaultSet);
            set => PlayerPrefsSetBool("DebugxStaticData.EnableLogDefault", value);
        }

        public static bool EnableLogMemberDefaultPrefs
        {
            get => PlayerPrefsGetBool("DebugxStaticData.EnableLogMemberDefault", DebugxStaticData.EnableLogMemberDefaultSet);
            set => PlayerPrefsSetBool("DebugxStaticData.EnableLogMemberDefault", value);
        }

        public static bool AllowUnregisteredMember
        {
            get => PlayerPrefsGetBool("DebugxStaticData.AllowUnregisteredMember", DebugxStaticData.AllowUnregisteredMemberSet);
            set => PlayerPrefsSetBool("DebugxStaticData.AllowUnregisteredMember", value);
        }

        public static int LogThisKeyMemberOnlyDefaultPrefs
        {
            get => PrefsGetInt("DebugxStaticData.LogThisKeyMemberOnlyDefault", DebugxStaticData.LogThisKeyMemberOnlyDefaultSet);
            set => PrefsSetInt("DebugxStaticData.LogThisKeyMemberOnlyDefault", value);
        }

        private static Dictionary<int, bool> _memberEnableDefaultDicPrefs;
        public static Dictionary<int, bool> MemberEnableDefaultDicPrefs
        {
            get
            {
                if (_memberEnableDefaultDicPrefs == null)
                {
                    _memberEnableDefaultDicPrefs = new Dictionary<int, bool>();

                    string data = PrefsGetString("DebugxStaticData.MemberEnableDefaultDic");
                    if (!string.IsNullOrEmpty(data))
                    {
                        string[] dataArray = data.Split(';');
                        for (int i = 0; i < dataArray.Length; i++)
                        {
                            string itemRaw = dataArray[i];
                            if (string.IsNullOrEmpty(itemRaw)) continue;

                            string[] item = itemRaw.Split(',');
                            if (item.Length != 2) continue;

                            if (!int.TryParse(item[0], out int key)) continue;
                            if (!bool.TryParse(item[1], out bool value)) continue;

                            // Keep the last value if duplicated data appears in prefs.
                            _memberEnableDefaultDicPrefs[key] = value;
                        }
                    }
                }

                return _memberEnableDefaultDicPrefs;
            }
        }

        public static void SaveMemberEnableDefaultDicPrefs()
        {
            if (_memberEnableDefaultDicPrefs != null)
            {
                StringBuilder sb = new StringBuilder();
                int counter = 0;
                foreach (var item in _memberEnableDefaultDicPrefs)
                {
                    counter++;
                    sb.Append($"{item.Key},{item.Value}");
                    if (counter != _memberEnableDefaultDicPrefs.Count) sb.Append(";");
                }
                PrefsSetString("DebugxStaticData.MemberEnableDefaultDic", sb.ToString());
            }
        }

        public static bool LogOutputPrefs
        {
            get => PlayerPrefsGetBool("DebugxStaticData.LogOutput", DebugxStaticData.LogOutputSet);
            set => PlayerPrefsSetBool("DebugxStaticData.LogOutput", value);
        }

        public static bool EnableLogStackTracePrefs
        {
            get => PlayerPrefsGetBool("DebugxStaticData.EnableLogStackTrace", DebugxStaticData.EnableLogStackTraceSet);
            set => PlayerPrefsSetBool("DebugxStaticData.EnableLogStackTrace", value);
        }

        public static bool EnableWarningStackTracePrefs
        {
            get => PlayerPrefsGetBool("DebugxStaticData.EnableWarningStackTrace", DebugxStaticData.EnableWarningStackTraceSet);
            set => PlayerPrefsSetBool("DebugxStaticData.EnableWarningStackTrace", value);
        }

        public static bool EnableErrorStackTracePrefs
        {
            get => PlayerPrefsGetBool("DebugxStaticData.EnableErrorStackTrace", DebugxStaticData.EnableErrorStackTraceSet);
            set => PlayerPrefsSetBool("DebugxStaticData.EnableErrorStackTrace", value);
        }

        public static bool RecordAllNonDebugxLogsPrefs
        {
            get => PlayerPrefsGetBool("DebugxStaticData.RecordAllNonDebugxLogs", DebugxStaticData.RecordAllNonDebugxLogsSet);
            set => PlayerPrefsSetBool("DebugxStaticData.RecordAllNonDebugxLogs", value);
        }

        public static bool FaMemberEnableSettingOpen
        {
            get => PlayerPrefsGetBool("DebugxStaticData.FAMemberEnableSettingOpen", true);
            set => PlayerPrefsSetBool("DebugxStaticData.FAMemberEnableSettingOpen", value);
        }

        public static bool CanResetPreferences
        {
            get => PlayerPrefsGetBool("DebugxStaticData.CanResetPreferences", false);
            set => PlayerPrefsSetBool("DebugxStaticData.CanResetPreferences", value);
        }

        public static bool CanResetPreferencesMembers
        {
            get => PlayerPrefsGetBool("DebugxStaticData.CanResetPreferencesMembers", false);
            set => PlayerPrefsSetBool("DebugxStaticData.CanResetPreferencesMembers", value);
        }
        #endregion
    }
}