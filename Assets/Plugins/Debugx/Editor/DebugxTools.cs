using DebugxLog;
using UnityEditor;

namespace DebugxLog.Editor
{
    public static class DebugxTools
    {
        [MenuItem("Tools/Debugx/Create DebugxProjectSettings Asset")]
        public static void CreateDebugxProjectSettingsAsset()
        {
            if (DebugxProjectSettingsAsset.Instance == null)
            {
                DebugxProjectSettingsAssetEditor.CreateDebugxProjectSettingsAsset();
            }
            else if (EditorUtility.DisplayDialog("Create DebugxProjectSettings Asset", "The debugxProjectSettings asset already exists, whether to create a new and overwrite?\nDebugx项目配置资源已经存在，是否创建一个新的并覆盖？", "Yes", "Cancel"))
            {
                DebugxProjectSettingsAssetEditor.CreateDebugxProjectSettingsAsset();
            }
        }
    }
}