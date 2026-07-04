using System;
using UnityEngine;
using DebugxLog.Tools;

namespace DebugxLog
{
    /// <summary>
    /// Debugx调试配置
    /// 推荐使用编辑器工具编辑，也可以直接编辑.asset文件
    /// </summary>
    public class DebugxProjectSettingsAsset : ScriptableObject, IDebugxProjectSettingsAsset
    {
        private static DebugxProjectSettingsAsset _instance;
        public static DebugxProjectSettingsAsset Instance
        {
            get
            {
                if (_instance == null)
                {
                    try
                    {
                        _instance = Resources.Load<DebugxProjectSettingsAsset>(DebugxProjectSettings.FileName);
                    }
                    catch
                    {
                        // ignored
                    }

                    //通过CheckDebugxProjectSettingsAsset方法来确认并自动创建配置资源
                    //在启动项目和打开ProjectSettings或Preferences时会确认
                }

                return _instance;
            }
            set => _instance = value;
        }

        #region Action

        //用于为创建的成员分配一个颜色
        public static Func<Color> GetRandomColorForMember;

        public static Func<Color> GetNormalMemberColor;

        public static Func<Color> GetMasterMemberColor;

        #endregion

        public static readonly ActionHandler OnApplyTo = new ActionHandler();

        [Tooltip(DebugxStaticData.ToolTipEnableLogDefault)]
        public bool enableLogDefault = true;

        [Tooltip(DebugxStaticData.ToolTipEnableLogMemberDefault)]
        public bool enableLogMemberDefault = true;

        [Tooltip(DebugxStaticData.ToolTipAllowUnregisteredMember)]
        public bool allowUnregisteredMember = true;

        [Tooltip(DebugxStaticData.ToolTipLogThisKeyMemberOnlyDefault)]
        public int logThisKeyMemberOnlyDefault;

        [Tooltip(DebugxStaticData.ToolTipDefaultDebugxMemberAssets)]
        public DebugxMemberInfoAsset[] defaultMemberAssets;
        public int DefaultMemberAssetsLength => defaultMemberAssets == null ? 0 : defaultMemberAssets.Length;

        [Tooltip(DebugxStaticData.ToolTipCustomDebugxMemberAssets)]
        public DebugxMemberInfoAsset[] customMemberAssets;
        public int CustomMemberAssetsLength => customMemberAssets == null ? 0 : customMemberAssets.Length;
        


        public void ResetMembers(bool resetDefault = true, bool resetCustom = true)
        {
            if (resetDefault)
            {
                defaultMemberAssets = new DebugxMemberInfoAsset[2];

                //普通Log成员信息
                DebugxMemberInfoAsset normalMember = new DebugxMemberInfoAsset()
                {
                    signature = DebugxProjectSettings.NormalInfoSignature,
                    logSignature = true,
                    key = DebugxProjectSettings.NormalInfoKey,
                    color = GetNormalMemberColor != null ? GetNormalMemberColor.Invoke() : Color.white,
                    enableDefault = true,
                };
                defaultMemberAssets[0] = normalMember;

                //高级Log成员信息
                DebugxMemberInfoAsset masterMember = new DebugxMemberInfoAsset()
                {
                    signature = DebugxProjectSettings.MasterInfoSignature,
                    logSignature = true,
                    key = DebugxProjectSettings.MasterInfoKey,
                    color = GetMasterMemberColor != null ? GetMasterMemberColor.Invoke() : new Color(1f, 0.627f, 0.627f, 1f),
                    enableDefault = true,
                };
                defaultMemberAssets[1] = masterMember;
            }

            if (resetCustom)
            {
                customMemberAssets = new[]
                {
                    new DebugxMemberInfoAsset()
                    {
                        signature = "Blur",
                        logSignature = true,
                        key = 1,
                        color = new Color(0.7843f, 0.941f, 1f, 1f),
                        enableDefault = true, 
                    }
                };
            }
        }

        #region Log Output

        [Tooltip("普通Log配置")]
        public bool logOutput = DebugxStaticData.LogOutputSet;

        [Tooltip("输出Log类型的堆栈跟踪")]
        public bool enableLogStackTrace = DebugxStaticData.EnableLogStackTraceSet;

        [Tooltip("输出Warning类型的堆栈跟踪")]
        public bool enableWarningStackTrace = DebugxStaticData.EnableWarningStackTraceSet;

        [Tooltip("输出Error类型的堆栈跟踪")]
        public bool enableErrorStackTrace = DebugxStaticData.EnableErrorStackTraceSet;

        [Tooltip("记录所有非Debugx打印的Log")]
        public bool recordAllNonDebugxLogs = DebugxStaticData.RecordAllNonDebugxLogsSet;

        #endregion

        //保存配置数据资源到dll中的DebugxProjectSettings，这是实际使用的配置数据
        public void ApplyTo(DebugxProjectSettings settings)
        {
            if (settings == null) return;

            if (Application.isEditor)
            {
                settings.enableLogDefault = DebugxStaticData.EnableLogDefaultPrefs;
                settings.enableLogMemberDefault = DebugxStaticData.EnableLogMemberDefaultPrefs;
                settings.allowUnregisteredMember = DebugxStaticData.AllowUnregisteredMember;
                settings.logThisKeyMemberOnlyDefault = DebugxStaticData.LogThisKeyMemberOnlyDefaultPrefs;
            }
            else
            {
                settings.enableLogDefault = enableLogDefault;
                settings.enableLogMemberDefault = enableLogMemberDefault;
                settings.allowUnregisteredMember = allowUnregisteredMember;
                settings.logThisKeyMemberOnlyDefault = logThisKeyMemberOnlyDefault;
            }

            settings.members = new DebugxMemberInfo[DefaultMemberAssetsLength + (customMemberAssets != null ? customMemberAssets.Length : 0)];

            //添加默认成员信息
            if (defaultMemberAssets != null && defaultMemberAssets.Length > 0)
            {
                for (int i = 0; i < defaultMemberAssets.Length; i++)
                {
                    settings.members[i] = defaultMemberAssets[i].CreateDebugxMemberInfo();
                }
            }

            //添加自定义成员信息
            if (customMemberAssets != null && customMemberAssets.Length > 0)
            {
                for (int i = 0; i < customMemberAssets.Length; i++)
                {
                    settings.members[i + DefaultMemberAssetsLength] = customMemberAssets[i].CreateDebugxMemberInfo();
                }
            }

            if (Application.isEditor)
            {
                //Log输出设置
                settings.logOutput = DebugxStaticData.LogOutputPrefs;
                settings.enableLogStackTrace = DebugxStaticData.EnableLogStackTracePrefs;
                settings.enableWarningStackTrace = DebugxStaticData.EnableWarningStackTracePrefs;
                settings.enableErrorStackTrace = DebugxStaticData.EnableErrorStackTracePrefs;
                settings.recordAllNonDebugxLogs = DebugxStaticData.RecordAllNonDebugxLogsPrefs;
            }
            else
            {
                //Log输出设置
                settings.logOutput = logOutput;
                settings.enableLogStackTrace = enableLogStackTrace;
                settings.enableWarningStackTrace = enableWarningStackTrace;
                settings.enableErrorStackTrace = enableErrorStackTrace;
                settings.recordAllNonDebugxLogs = recordAllNonDebugxLogs;
            }

            // 用 OnAwake 而非 ResetToDefault：后者只清空 _memberEnables 不重建，会导致应用配置后运行时成员开关静默失效、
            // 且 SetMemberEnable 因字典为空而无法再设置。OnAwake 会依据新的 members 重建开关字典。
            // Use OnAwake instead of ResetToDefault: the latter only clears _memberEnables without rebuilding, which makes
            // runtime member switches silently break after applying settings. OnAwake rebuilds the switch dictionary from members.
            Debugx.OnAwake();

            OnApplyTo.Invoke();
        }
    }
}