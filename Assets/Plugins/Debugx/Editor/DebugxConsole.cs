using UnityEditor;
using UnityEngine;

namespace DebugxLog.Editor
{
    public class DebugxConsole : EditorWindow
    {
        private static DebugxProjectSettings Settings => DebugxProjectSettings.Instance;

        private static EditorWindow _window;
        private static Vector2 _scrollViewPos;

        [MenuItem("Window/Debugx/DebugxConsole", false, 4)]
        public static void ShowWindow()
        {
            _window = EditorWindow.GetWindow(typeof(DebugxConsole));
            _window.titleContent.text = "DebugxConsole";
            _window.minSize = new Vector2(400f, 300f);
        }

        private void OnEnable()
        {
            DebugxProjectSettingsAsset.OnApplyTo.Bind(OnApplyTo);
        }

        private void OnDisable()
        {
            DebugxProjectSettingsAsset.OnApplyTo.Unbind(OnApplyTo);
        }

        private void OnGUI()
        {
            _scrollViewPos = EditorGUILayout.BeginScrollView(_scrollViewPos);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Playing Settings", GUIStyleEx.TitleStyle2);
            if (!Application.isPlaying) EditorGUILayout.HelpBox(
                DebugxStaticData.IsChineseSimplified ? 
                "在游戏运行时才能设置这部分内容。" : "This part can only be set during game runtime.", 
                MessageType.Info);
            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            EditorGUILayout.LabelField("Toggle", GUIStyleEx.TitleStyle3);
            Debugx.enableLog = GUILayoutEx.Toggle("EnableLog", 
                DebugxStaticData.IsChineseSimplified ? "Log总开关" : "Master switch for logging", Debugx.enableLog);
            Debugx.enableLogMember = GUILayoutEx.Toggle("EnableLogMember", 
                DebugxStaticData.IsChineseSimplified ? "成员Log总开关" : "Master logging switch for members", Debugx.enableLogMember);
            Debugx.logThisKeyMemberOnly = GUILayoutEx.IntField("LogThisKeyMemberOnly", 
                DebugxStaticData.IsChineseSimplified ? 
                    "仅打印此Key的成员Log。0为关闭，设置时Key必须是存在于配置的成员。必须关闭LogMasterOnly后才能设置此值。" 
                    : "Only print member logs for this specific Key. Set to 0 to disable. When setting, the Key must exist in the configured members. This value can only be set after disabling LogMasterOnly.",
                Debugx.logThisKeyMemberOnly);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Members", GUIStyleEx.TitleStyle3);
            if (Settings.members != null && Settings.members.Length > 0)
            {
                for (int i = 0; i < Settings.members.Length; i++)
                {
                    var info = Settings.members[i];
                    EditorGUILayout.BeginHorizontal();
                    bool enable = Debugx.MemberIsEnable(info.key);
                    EditorGUI.BeginChangeCheck();
                    enable = GUILayoutEx.Toggle($" [{info.key}] {(string.IsNullOrEmpty(info.signature) ? "Member" : info.signature)}", "", enable);
                    if (EditorGUI.EndChangeCheck()) Debugx.SetMemberEnable(info.key, enable);
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.LabelField(DebugxStaticData.IsChineseSimplified ? "没有配置任何成员。" : "No members have been configured.");
            }

            EditorGUI.EndDisabledGroup();

#if UNITY_EDITOR
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Test", GUIStyleEx.TitleStyle2);
            DebugxStaticData.EnableAwakeTestLog = GUILayoutEx.Toggle("EnableAwakeTestLog", 
                DebugxStaticData.IsChineseSimplified ? "打开Awake中测试用的Log打印。" : "Enable the test log printing in Awake.", DebugxStaticData.EnableAwakeTestLog);
            DebugxStaticData.EnableUpdateTestLog = GUILayoutEx.Toggle("EnableUpdateTestLog", 
                DebugxStaticData.IsChineseSimplified ? "打开Update中测试用的Log打印" : "Enable the test log printing in Update.", DebugxStaticData.EnableUpdateTestLog);
#endif


            EditorGUILayout.EndScrollView();
        }

        private void OnApplyTo()
        {
            this.Repaint();
        }
    }
}