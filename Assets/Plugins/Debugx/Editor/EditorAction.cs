using UnityEditor;

namespace DebugxLog.Editor
{
    public static class EditorAction
    {
        [InitializeOnLoadMethod]
        static void InitializeOnLoadMethod()
        {
            EditorApplication.delayCall += () =>
            {
                // Color callbacks first: on a first run the settings asset is created right below, and its default members
                // need the skin-aware colors (otherwise Normal falls back to white, unreadable on the Light skin).
                // 先注册颜色回调：首次运行时下面会创建设置资源，其默认成员需要按皮肤取色（否则 Normal 回退为白色，亮色皮肤下看不清）。
                ColorDispenser.OnInitializeOnLoadMethod();
                DebugxProjectSettingsAssetEditor.OnInitializeOnLoadMethod();
            };
        }
    }
}