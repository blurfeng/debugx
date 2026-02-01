using UnityEditor;

namespace DebugxLog.Editor
{
    [CanEditMultipleObjects, CustomEditor(typeof(DebugxProjectSettingsAsset))]
    public class DebugxProjectSettingsAssetIns : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Do not modify the .asset file directly, as this may cause unnecessary issues.
            // 禁止直接修改.asset，这会导致不必要的问题。
            EditorGUILayout.HelpBox(
                DebugxStaticData.IsChineseSimplified ? 
                    "禁止直接修改配置资源文件，请通过 Editor->ProjectSettings->Debugx 界面修改。" 
                    : "Do not modify the configuration asset directly. Please use the Editor -> Project Settings -> Debugx interface to make changes.", 
                MessageType.Info);
            EditorGUI.BeginDisabledGroup(true);
            base.OnInspectorGUI();
            EditorGUI.EndDisabledGroup();
        }
    }
}