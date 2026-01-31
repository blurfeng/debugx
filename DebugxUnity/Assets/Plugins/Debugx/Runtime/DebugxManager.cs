using UnityEngine;
using DebugxLog.Tools;
using System.Diagnostics;

namespace DebugxLog
{
    /// <summary>
    /// Debugx Debugging Function Manager. It is usually added to the GameObject used for startup.
    /// Debugx调试功能管理器。一般添加到启动用GameObject上。
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
                if (_instance == null)
                {
                    _instance = FindObjectOfType<DebugxManager>();

                    if (_instance == null)
                    {
                        _gameObject = new GameObject { name = typeof(DebugxManager).Name };
                        _instance = _gameObject.AddComponent<DebugxManager>();
                    }
                }
                return _instance;
            }
        }

#if DEBUG_X
        // It is automatically created when the game starts.
        // 游戏启动时自动创建。
        [RuntimeInitializeOnLoadMethod]
        private static void CheckInstance()
        {
            if (_instance == null && Application.isPlaying)
            {
                DebugxManager.Instance.Create();
            }
        }
        
        public virtual void Create() { }
#endif

        private void Awake()
        {
            DontDestroyOnLoad(this);
            Debugx.OnAwake();

#if UNITY_EDITOR
            // The editor rewrites the Log output path to the "Logs" folder of the project.
            // 编辑器时重写Log输出路径到项目Logs文件夹下。
            string directoryPathCover = Application.dataPath;
            directoryPathCover = directoryPathCover.Replace("Assets", "Logs");
            LogOutput.DirectoryPath = directoryPathCover;
#elif UNITY_STANDALONE_WIN
            LogOutput.DirectoryPath = Application.dataPath;
#elif UNITY_ANDROID
            LogOutput.DirectoryPath = $"{Application.persistentDataPath}/Log";
#elif UNITY_IPHONE
            LogOutput.DirectoryPath = Application.persistentDataPath;
#endif

            LogOutput.RecordStart();

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
                Debugx.Log(1, "Key 1 Print Test");
                Debugx.Log(2, "Key 2 Print Test");
                Debugx.Log(999, "Key 999 Print Test");
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
                Debugx.Log(1, "MemberKey 1 Update");
            }
#endif
        }

        private void OnDestroy()
        {
            Debugx.OnDestroy();
            LogOutput.RecordOver();
        }

        private void OnGUI()
        {
            LogOutput.DrawGUI();
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