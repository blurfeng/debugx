using DebugxLog.Tools;
using System;
using UnityEngine;

namespace DebugxLog
{
    /// <summary>
    /// Debugx provides debugging member information. It is used for configuring data.
    /// Debugx调试成员信息。用于配置的数据。
    /// </summary>
    [Serializable]
    public struct DebugxMemberInfoAsset
    {
        [Tooltip("是否开启")]
        public bool enableDefault;

        [Tooltip("使用者签名")]
        public string signature;

        [Tooltip("使用者签名是否打印在Log中")]
        public bool logSignature;

        [Tooltip("此成员信息密钥,不要重复")]
        public int key;

        [Tooltip("头部信息，在打印Log会打印在头部")]
        public string header;

        [Tooltip("打印Log颜色")]
        public Color color;

        public DebugxMemberInfoAsset(int key)
        {
            enableDefault = true;
            signature = $"Menber {key}";
            logSignature = true;
            this.key = key;
            header = String.Empty;
            color = DebugxProjectSettingsAsset.GetRandomColorForMember != null ? DebugxProjectSettingsAsset.GetRandomColorForMember.Invoke() : Color.white;
        }

        /// <summary>
        /// 将部分数据重置到默认
        /// </summary>
        public void ResetToDefaultPart()
        {
            enableDefault = true;
            logSignature = true;
        }

        public DebugxMemberInfo CreateDebugxMemberInfo()
        {
            DebugxMemberInfo info = new DebugxMemberInfo()
            {
                key = key,
                enableDefault = enableDefault,
                signature = signature,
                logSignature = logSignature,
                header = header,
                color = ColorUtility.ToHtmlStringRGB(color),

                haveSignature = !string.IsNullOrEmpty(signature),
                haveHeader = !string.IsNullOrEmpty(header),
            };

            //本地用户设置覆盖
            if (Application.isEditor && DebugxStaticData.MemberEnableDefaultDicPrefs.ContainsKey(key))
            {
                info.enableDefault = DebugxStaticData.MemberEnableDefaultDicPrefs[key];
            }

            return info;
        }
    }

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
                        _instance = Resources.Load<DebugxProjectSettingsAsset>(DebugxProjectSettings.fileName);
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

        [Tooltip(DebugxStaticData.ToolTipCustomDebugxMemberAssets)]
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
                    signature = DebugxProjectSettings.normalInfoSignature,
                    logSignature = true,
                    key = DebugxProjectSettings.normalInfoKey,
                    color = GetNormalMemberColor != null ? GetNormalMemberColor.Invoke() : Color.white,
                    enableDefault = true,
                };
                defaultMemberAssets[0] = normalMember;

                //高级Log成员信息
                DebugxMemberInfoAsset masterMember = new DebugxMemberInfoAsset()
                {
                    signature = DebugxProjectSettings.masterInfoSignature,
                    logSignature = true,
                    key = DebugxProjectSettings.masterInfoKey,
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

        [Tooltip("绘制Log到屏幕")]
        public bool drawLogToScreen = DebugxStaticData.DrawLogToScreenSet;

        [Tooltip("限制绘制Log数量")]
        public bool restrictDrawLogCount = DebugxStaticData.RestrictDrawLogCountSet;

        [Tooltip("绘制Log最大数量")]
        public int maxDrawLogs = DebugxStaticData.MaxDrawLogsSet;

        #endregion

        //保存配置数据资源到dll中的DebugxProjectSettings，这是实际使用的配置数据
        public void ApplyTo(DebugxProjectSettings settings)
        {
            if (settings == null) return;


            if (Application.isEditor)
            {
                settings.enableLogDefault = DebugxStaticData.EnableLogDefaultPrefs;
                settings.enableLogMemberDefault = DebugxStaticData.EnableLogMemberDefaultPrefs;
                settings.logThisKeyMemberOnlyDefault = DebugxStaticData.LogThisKeyMemberOnlyDefaultPrefs;
            }
            else
            {
                settings.enableLogDefault = enableLogDefault;
                settings.enableLogMemberDefault = enableLogMemberDefault;
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
                settings.drawLogToScreen = DebugxStaticData.DrawLogToScreenPrefs;
                settings.restrictDrawLogCount = DebugxStaticData.RestrictDrawLogCountPrefs;
                settings.maxDrawLogs = DebugxStaticData.MaxDrawLogsPrefs;
            }
            else
            {
                //Log输出设置
                settings.logOutput = logOutput;
                settings.enableLogStackTrace = enableLogStackTrace;
                settings.enableWarningStackTrace = enableWarningStackTrace;
                settings.enableErrorStackTrace = enableErrorStackTrace;
                settings.recordAllNonDebugxLogs = recordAllNonDebugxLogs;
                settings.drawLogToScreen = drawLogToScreen;
                settings.restrictDrawLogCount = restrictDrawLogCount;
                settings.maxDrawLogs = maxDrawLogs;
            }

            Debugx.ResetToDefault();

            OnApplyTo.Invoke();
        }
    }
}