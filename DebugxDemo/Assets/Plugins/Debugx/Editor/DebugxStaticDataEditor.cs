using DebugxLog.Tools;
using UnityEditor;

namespace DebugxLog.Editor
{
    public static class DebugxStaticDataEditor
    {
        #region ProjectSettings
        public static bool FaMemberConfigSettingOpen
        {
            get => EditorPrefs.GetBool("DebugxStaticData.FAMemberConfigSettingOpen", true);
            set => EditorPrefs.SetBool("DebugxStaticData.FAMemberConfigSettingOpen", value);
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
                    _autoSaveByte = (byte)(EditorPrefs.GetBool("DebugxStaticData.AutoSave", true) ? 1 : 2);
                }

                return _autoSaveByte == 1;
            }
            set
            {
                if (value != (_autoSaveByte == 1))
                {
                    EditorPrefs.SetBool("DebugxStaticData.AutoSave", value);
                    _autoSaveByte = (byte)(value ? 1 : 2);

                    OnAutoSaveChange.Invoke(AutoSave);
                }
            }
        }
        #endregion
    }
}