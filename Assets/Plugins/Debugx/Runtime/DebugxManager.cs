using UnityEngine;
using DebugxLog.Tools;
using System.Diagnostics;
using System.IO;

namespace DebugxLog
{
    /// <summary>
    /// Debugx Debugging Function Manager. Created automatically at runtime when DEBUG_X is defined; there is no need to
    /// add it to a scene.
    /// Debugx调试功能管理器。定义了 DEBUG_X 时在运行时自动创建，无需添加到场景中。
    /// </summary>
    public class DebugxManager : MonoBehaviour
    {
        // Add the macro "DEBUG_X" to the U3D project to enable the function.
        // 在U3D项目中添加宏“DEBUG_X”开启功能。

        private static DebugxManager _instance;
        private static GameObject _gameObject;
        public static DebugxManager Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = FindObjectOfType<DebugxManager>();

                    // 仅在运行时创建管理器，避免编辑期（非 Play）访问 Instance 时在场景里留下常驻 GameObject。
                    // Only create the manager at runtime, so accessing Instance in edit mode doesn't leave a stray GameObject in the scene.
                    if (!_instance && Application.isPlaying)
                    {
                        _gameObject = new GameObject { name = typeof(DebugxManager).Name };
                        _instance = _gameObject.AddComponent<DebugxManager>();
                    }
                }
                return _instance;
            }
        }

        // Set once logging has started (member switches initialized, file recording started); cleared in OnDestroy.
        // 日志已启动（成员开关已初始化、文件录制已开始）时置位；在 OnDestroy 中清除。
        private static bool _loggingStarted;

#if DEBUG_X
        // Start logging before the first scene loads, so the first scene's Awake/OnEnable logs reach the log file and its
        // SetMemberEnable calls are not reset later. The manager itself is still created after the scene loads (below),
        // so a manager (or subclass) placed in the first scene is found and its Create() runs.
        // 在首个场景加载之前启动日志，使首个场景 Awake/OnEnable 中的日志能写进日志文件，其中的 SetMemberEnable 也不会在之后被重置。
        // 管理器本身仍在场景加载后创建（见下），故放在首个场景中的管理器（或其子类）会被找到并执行其 Create()。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartLoggingEarly()
        {
            StartLogging();
        }

        // It is automatically created when the game starts.
        // 游戏启动时自动创建。
        [RuntimeInitializeOnLoadMethod]
        private static void CheckInstance()
        {
            if (!_instance && Application.isPlaying)
            {
                Instance.Create();
            }
        }

        public virtual void Create() { }
#endif

        private void Awake()
        {
            // Only one manager: a second one (e.g. in a scene that is loaded again) would otherwise reset the runtime
            // switches, and when destroyed it would stop the file recording for everyone. Only this component is removed,
            // not the GameObject it sits on.
            // 只保留一个管理器：否则第二个（如再次加载的场景里的那个）会重置运行时开关，销毁时还会停掉全局的文件录制。
            // 只移除这个组件，不销毁它所在的 GameObject。
            if (_instance && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(this);
            StartLogging();

            Debugx.LogAdm("DebugxManager --- Awake");

            // 在 菜单>Edit>Preferences>Debugx 界面的 LogOutput>EnableLogOutput 开关是否输出本地log。
            if (DebugxProjectSettings.Instance.logOutput)
                Debugx.LogAdm($"DebugxManager --- Log output to {LogOutput.DirectoryPath}");

#if UNITY_EDITOR
            // test case.Open the console via Menu > Window > Debugx > DebugxConsole, and toggle the setting at Test > EnableAwakeTestLog to enable or disable it.
            // 测试用。请在 菜单>Window>Debugx>DebugxConsole 打开控制台，并设置 Test>EnableAwakeTestLog 进行开关。
            if (DebugxStaticData.EnableAwakeTestLog)
            {
                // Try to print. It can be annotated. The member information with the corresponding key will be printed.
                // 试打印，可注释。有对应key的成员信息时才会被打印。
                Debugx.Log(1, "Key 1 Print Test (Awake)");
                Debugx.Log(2, "Key 2 Print Test (Awake)");
                Debugx.Log(999, "Key 999 Print Test (Awake)");
            }
#endif
        }

        // Initialize the member switches and start the log file, once per session.
        // 初始化成员开关并开始写日志文件，每次运行只做一次。
        private static void StartLogging()
        {
            if (_loggingStarted) return;
            _loggingStarted = true;

            Debugx.OnAwake();

#if UNITY_EDITOR
            // The editor rewrites the Log output path to the "Logs" folder of the project.
            // 编辑器时重写Log输出路径到项目Logs文件夹下。
            // 取工程根目录（dataPath 的父目录）再拼 Logs，避免 string.Replace 把路径中更靠前的其它 "Assets" 一并替换。
            // Take the project root (parent of dataPath) then append "Logs", so string.Replace can't also rewrite
            // another "Assets" occurring earlier in the path.
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            LogOutput.DirectoryPath = Path.Combine(projectRoot, "Logs");
#elif UNITY_STANDALONE_WIN
            LogOutput.DirectoryPath = Application.dataPath;
#elif UNITY_ANDROID
            LogOutput.DirectoryPath = $"{Application.persistentDataPath}/Log";
#else
            // iOS/OSX/Linux/WebGL 等其余平台：persistentDataPath，避免 DirectoryPath 未设置。
            // iOS/OSX/Linux/WebGL and any other platform: persistentDataPath, so DirectoryPath is never left unset.
            LogOutput.DirectoryPath = Application.persistentDataPath;
#endif

            LogOutput.RecordStart();
        }

        private void Start()
        {
#if UNITY_EDITOR
            // test case. Toggle at Menu > Window > Debugx > DebugxConsole, Test > EnableStartTestLog.
            // Unlike the Awake test log, Start fires one frame later — after the runtime Console has subscribed to the
            // log channels — so these lines also show up in the in-game runtime Console, not just the Editor one.
            // 测试用。在 菜单>Window>Debugx>DebugxConsole 的 Test>EnableStartTestLog 开关。与 Awake 测试打印不同，Start 晚一帧
            // 触发——此时运行时 Console 已订阅日志通道——故这些日志在游戏内运行时 Console 里也能看到，而不只是 Editor 版。
            if (DebugxStaticData.EnableStartTestLog)
            {
                // Try to print. It can be annotated. The member information with the corresponding key will be printed.
                // 试打印，可注释。有对应key的成员信息时才会被打印。
                Debugx.Log(1, "Key 1 Print Test (Start)");
                Debugx.Log(2, "Key 2 Print Test (Start)");
                Debugx.Log(999, "Key 999 Print Test (Start)");
            }
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR
            // test case. // 测试用。
            if (DebugxStaticData.EnableUpdateTestLog)
            {
                // Test print, with comments. It will only be printed when there is corresponding key for the member information.
                // 测试打印，可注释。有对应key的成员信息时才会被打印。
                Debugx.Log(1, "MemberKey 1 Update (Update)");
            }
#endif
        }

        private void OnDestroy()
        {
            if (_instance != this) return; // a duplicate removed in Awake. 在 Awake 中被移除的重复实例。

            Debugx.OnDestroy();
            LogOutput.RecordOver();
            _loggingStarted = false;
        }

        /// <summary>
        /// Set the member switch. This method can be used to set it during operation.
        /// 设置成员开关。运行时可通过此方法设置。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="enable"></param>
        [Conditional("DEBUG_X")]
        public void SetMemberEnable(int key, bool enable)
        {
            Debugx.SetMemberEnable(key, enable);
        }
    }
}