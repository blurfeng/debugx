using System;
using UnityEngine;

namespace DebugxLog
{
    /// <summary>
    /// Debugx settings asset interface.
    /// Debugx设置资源接口。
    /// </summary>
    public interface IDebugxProjectSettingsAsset
    {
        /// <summary>
        /// Copy data.
        /// 复制数据。
        /// </summary>
        /// <param name="settings"></param>
        void ApplyTo(DebugxProjectSettings settings);
    }

    /// <summary>
    /// Runtime member information.
    /// 运行时成员信息。
    /// </summary>
    public class DebugxMemberInfo
    {
        /// <summary>
        /// Whether enabled.
        /// 是否开启。
        /// </summary>
        public bool enableDefault;

        /// <summary>
        /// Debug member key.
        /// 调试成员密钥。
        /// </summary>
        public int key;

        /// <summary>
        /// User signature.
        /// 使用者签名。
        /// </summary>
        public string signature;

        /// <summary>
        /// Whether user signature is printed in the log.
        /// 使用者签名是否打印在Log中。
        /// </summary>
        public bool logSignature;

        /// <summary>
        /// Header information, printed at the top of the log.
        /// 头部信息，在打印Log会打印在头部。
        /// </summary>
        public string header;

        /// <summary>
        /// RGB hexadecimal color code for log printing.
        /// 打印Log颜色的RGB十六进制数。
        /// </summary>
        public string color;

        /// <summary>
        /// Whether there is a signature.
        /// 是否有签名。
        /// </summary>
        public bool haveSignature;

        /// <summary>
        /// Whether there is header information.
        /// 是否有头部信息。
        /// </summary>
        public bool haveHeader;

        /// <summary>
        /// Print signature.
        /// 打印签名。
        /// </summary>
        public bool LogSignature => logSignature && haveSignature;

        /// <summary>
        /// Default constructor.
        /// 默认构造函数。
        /// </summary>
        public DebugxMemberInfo() { }

        /// <summary>
        /// Quick constructor for simple member info.
        /// 快速构造简单成员信息。
        /// </summary>
        /// <param name="key">Debug member key 调试成员密钥</param>
        /// <param name="signature">User signature 使用者签名</param>
        public DebugxMemberInfo(int key, string signature)
        {
            this.key = key;
            enableDefault = true;
            this.signature = signature;
            this.logSignature = true;
            header = string.Empty;
            this.color = String.Empty;
            haveSignature = true;
            haveHeader = false;
        }
    }

    /// <summary>
    /// Debugx settings.
    /// Debugx设置。
    /// </summary>
    public class DebugxProjectSettings
    {
        /// <summary>
        /// Debugx project settings asset file name.
        /// Debugx项目设置Asset文件名称。
        /// </summary>
        public const string FileName = "DebugxProjectSettings";
        private static DebugxProjectSettings _instance;

        /// <summary>
        /// Singleton instance.
        /// 单例。
        /// </summary>
        public static DebugxProjectSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    LoadResources();
                }

                return _instance;
            }
        }

        /// <summary>
        /// Administrator member.
        /// 管理者成员。
        /// </summary>
        public DebugxMemberInfo AdminInfo
        {
            get
            {
                if (_adminInfo == null)
                {
                    _adminInfo = new DebugxMemberInfo(0, "Admin");
                }
                return _adminInfo;
            }
        }
        private DebugxMemberInfo _adminInfo;

        /// <summary>
        /// List of member information.
        /// 成员信息列表。
        /// </summary>
        public DebugxMemberInfo[] members;

        #region Static Data
        /// <summary>
        /// Debugx printed content tag.
        /// No special symbols with regex meanings are allowed.
        /// When modifying, also update the regex in LogOutput.
        /// Debugx打印的内容标识。
        /// 不允许带有任何正则表达式的特殊含义符号。
        /// 修改时需要同事修改LogOutput的正则表达式。
        /// </summary>
        public const string DebugxTag = "[Debugx]";

        /// <summary>
        /// Default member normal signature.
        /// 默认成员，普通成员签名。
        /// </summary>
        public const string NormalInfoSignature = "Normal";

        /// <summary>
        /// Default member normal key.
        /// 默认成员，普通成员密钥。
        /// </summary>
        public const int NormalInfoKey = -1;

        /// <summary>
        /// Default member normal color.
        /// 默认成员，普通成员颜色。
        /// </summary>
        public static Color NormalInfoColor => Color.white;

        /// <summary>
        /// Default member master signature.
        /// 默认成员，高级成员签名。
        /// </summary>
        public const string MasterInfoSignature = "Master";

        /// <summary>
        /// Default member master key.
        /// 默认成员，高级成员密钥。
        /// </summary>
        public const int MasterInfoKey = -2;

        /// <summary>
        /// Default member master color.
        /// 默认成员，高级成员颜色。
        /// </summary>
        public static Color MasterInfoColor => new Color(1f, 0.627f, 0.627f, 1f);
        #endregion

        /// <summary>
        /// Master log switch.
        /// Log总开关。
        /// </summary>
        public bool enableLogDefault = true;

        /// <summary>
        /// Member log master switch.
        /// 成员Log总开关。
        /// </summary>
        public bool enableLogMemberDefault = true;

        /// <summary>
        /// Allow printing without registered members.
        /// 允许没有注册成员进行打印。
        /// </summary>
        public bool allowUnregisteredMember = true;

        /// <summary>
        /// Only print logs for this Key member, 0 to disable.
        /// 仅打印此Key的成员Log，0为关闭。
        /// </summary>
        public int logThisKeyMemberOnlyDefault = 0;

        /// <summary>
        /// Whether the key is valid.
        /// Key是否合法。
        /// </summary>
        /// <param name="key">The key to validate 要验证的密钥</param>
        /// <returns>True if valid, false otherwise 是否有效</returns>
        public static bool KeyValid(int key)
        {
            return key > 0;
        }

        /// <summary>
        /// Load configuration resources.
        /// 加载配置资源。
        /// </summary>
        private static void LoadResources()
        {
            // Resources.Load is not available in some lifecycle stages,
            // for example, calling it in [InitializeOnLoadMethod] during editor startup causes stack overflow errors.
            // Resources.Load在某些生命周期时不可用，比如[InitializeOnLoadMethod]特性方法在启动Editor时调用会导致Resources.Load报错堆栈溢出
            try
            {
                IDebugxProjectSettingsAsset asset = Resources.Load<ScriptableObject>(FileName) as IDebugxProjectSettingsAsset;
                if (asset != null) ApplyBy(asset);
                else DebugxBase.LogAdmWarning("Failed to load the DebugxProjectSettings configuration resource file. 加载DebugxProjectSettings配置资源文件失败。");
            }
            catch
            {
                DebugxBase.LogAdmWarning("Failed to load the DebugxProjectSettings configuration resource file. 加载DebugxProjectSettings配置资源文件失败。");
            }
        }

        /// <summary>
        /// Read data from asset and save to DebugxProjectSettings.
        /// 从Asset读取数据保存到DebugxProjectSettings。
        /// </summary>
        /// <param name="asset">The settings asset  设置资源</param>
        public static void ApplyBy(IDebugxProjectSettingsAsset asset)
        {
            if (asset == null) return;

            _instance = new DebugxProjectSettings();
            asset.ApplyTo(_instance);
        }

        #region Log Output

        /// <summary>
        /// Output logs to local file.
        /// 输出Log到本地文件。
        /// </summary>
        public bool logOutput = true;

        /// <summary>
        /// Enable stack trace for Log type.
        /// 输出Log类型的堆栈跟踪。
        /// </summary>
        public bool enableLogStackTrace = false;

        /// <summary>
        /// Enable stack trace for Warning type.
        /// 输出Warning类型的堆栈跟踪。
        /// </summary>
        public bool enableWarningStackTrace = false;

        /// <summary>
        /// Enable stack trace for Error type.
        /// 输出Error类型的堆栈跟踪。
        /// </summary>
        public bool enableErrorStackTrace = true;

        /// <summary>
        /// Record all logs not printed by Debugx.
        /// 记录所有非Debugx打印的Log。
        /// </summary>
        public bool recordAllNonDebugxLogs = false;

        /// <summary>
        /// Draw logs to screen.
        /// 绘制Log到屏幕。
        /// </summary>
        public bool drawLogToScreen = false;

        /// <summary>
        /// Restrict the number of drawn logs.
        /// 限制绘制Log数量。
        /// </summary>
        public bool restrictDrawLogCount = false;

        /// <summary>
        /// Maximum number of drawn logs.
        /// 绘制Log最大数量。
        /// </summary>
        public int maxDrawLogs = 100;

        #endregion
    }
}