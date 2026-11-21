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
                DebugxProjectSettingsAssetEditor.OnInitializeOnLoadMethod();
                ColorDispenser.OnInitializeOnLoadMethod();
            };
        }
    }
}