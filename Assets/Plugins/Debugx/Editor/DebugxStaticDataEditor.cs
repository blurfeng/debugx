using DebugxLog.Tools;
using UnityEditor;

namespace DebugxLog.Editor
{
    public static class DebugxStaticDataEditor
    {
        #region ProjectSettings
        // Stored per project through DebugxStaticData (these EditorPrefs keys used to be machine-wide, so toggling them in
        // one project changed every project); the old machine-wide value is the default until it is set here.
        // 经 DebugxStaticData 按项目存储（这些 EditorPrefs 键以前是全机共享的，在一个项目里切换会改到所有项目）；在此设置之前以旧的全机值为默认。
        public static bool FaMemberConfigSettingOpen
        {
            get => DebugxStaticData.PlayerPrefsGetBool("DebugxStaticData.FAMemberConfigSettingOpen",
                EditorPrefs.GetBool("DebugxStaticData.FAMemberConfigSettingOpen", true));
            set => DebugxStaticData.PlayerPrefsSetBool("DebugxStaticData.FAMemberConfigSettingOpen", value);
        }

        public static readonly ActionHandler<bool> OnAutoSaveChange = new ActionHandler<bool>();
        // 0 = Not set 1 = Automatic saving 2 = Do not automatically save.
        // 0=未设置 1=自动保存 2=不自动保存。
        private static byte _autoSaveByte;
        public static bool AutoSave
        {
            get
            {
                if (_autoSaveByte == 0)
                {
                    _autoSaveByte = (byte)(DebugxStaticData.PlayerPrefsGetBool("DebugxStaticData.AutoSave",
                        EditorPrefs.GetBool("DebugxStaticData.AutoSave", true)) ? 1 : 2);
                }

                return _autoSaveByte == 1;
            }
            set
            {
                if (value != (_autoSaveByte == 1))
                {
                    DebugxStaticData.PlayerPrefsSetBool("DebugxStaticData.AutoSave", value);
                    _autoSaveByte = (byte)(value ? 1 : 2);

                    OnAutoSaveChange.Invoke(AutoSave);
                }
            }
        }
        #endregion
    }
}