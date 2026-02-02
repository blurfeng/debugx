using System;

namespace DebugxLog
{
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
}