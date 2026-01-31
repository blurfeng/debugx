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
}