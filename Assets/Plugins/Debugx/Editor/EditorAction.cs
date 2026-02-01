using UnityEditor;

namespace DebugxLog.Editor
{
    public static class EditorAction
    {
        [InitializeOnLoadMethod]
        static void InitializeOnLoadMethod()
        {
            DebugxEditorLibrary.OnInitializeOnLoadMethod();
            DebugxProjectSettingsAssetEditor.OnInitializeOnLoadMethod();
            ColorDispenser.OnInitializeOnLoadMethod();

            EditorApplication.wantsToQuit += Quit;
        }

        static bool Quit()
        {
            ColorDispenser.OnQuit();

            EditorApplication.wantsToQuit -= Quit;
            return true;
        }
    }
}