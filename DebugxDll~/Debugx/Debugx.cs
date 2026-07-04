#region AuthorInfo

////////////////////////////////////////////////////////////////////////////////////////////////////
// Author: Blur Feng
// Time: 20230109
// Version: 2.4.0.0
// Description:
// The debug log is managed according to its members.use macro "DEBUG_X" open the functional.
// 此插件用于以成员的方式管理调试日志。使用宏"DEBUG_X"来开启功能。
// ** 为了 Unity 使用 Debugx 类的 Log 方法时堆栈能在 Debugx.Log 为止阻断，必须构建封装的 DebugxLog.dll **
////////////////////////////////////////////////////////////////////////////////////////////////////
// 版本号使用规范：大版本前后不兼容.新功能.小功能或功能更新.bug修复
////////////////////////////////////////////////////////////////////////////////////////////////////
// **** Update log ****
////////////////////
// Version: 1.0.0.0 20220829
// 1.创建插件。成员数据的配置功能，打印Log功能。
////////////////////
// Version: 1.1.0.0 20220830
// 1.新增类LogOutput，用于到处Log数据到本地txt文件。
// 2.新增AdminInfo成员用于管理者打印，此成员不受开关影响。
// 3.默认成员配置文件中增加 Blur 成员。
// 4.修复在Window中移除一个成员时，移除的对应FadeArea不正确的问题。
// 5.创建新成员时，设置默认signature且设置logSignature=true。
// 6.通过 Tools/Debugx/CreateDebugxManager 创建Manager时，配置当前debugxMemberConfig为DebugxEditorLibrary.DebugxMemberConfigDefault。
// 7.增加logThisKeyMemberOnly参数，用于设置仅打印某个Key的成员Log。LogAdm不受影响，LogMasterOnly为设置logThisKeyMemberOnly为MasterKey的快速开关。
// 8.DebugxManager的Inspector界面更新。
// 9.新增ActionHandler类，用于创建Action事件。
// 10.新增DebugxTools类，提供一些可用的工具方法。
// 11.修复一些Bug。
////////////////////
// Version: 1.1.1.0 20220903
// 1.新增功能，在Editor编辑器启动时，初始化调试成员配置到Debux，保证在编辑器非游玩时也能使用Debux.Log()。
// 2.DebugxMemberWindow改名为DebugxSettingWindow，调整窗口内容，优化代码。
// 3.Debugx.dll中修改Dictionary为List。为了DOTS等某些情况下，不支持Dictionary的情况。
// 4.LogOutput类，新增绘制Log到屏幕功能，在DebugxManager上设置是否绘制。
////////////////////
// Version: 2.0.0.0 20220911
// 1.DebugxMemberConfig类改名为DebugxProjectSettings，增加更多成员字段；创建对应配置用类DebugxProjectSettingsAsset，用于生成.asset文件在编辑器中配置。
// 2.设置界面从EditorWindow改为SettingsProvider，在Editor->ProjectSetting->Debugx中设置。设置内容调整。
// 3.新增界面 PreferencesDebugx 在 Editor->Preferences->Debugx 目录下。可以让不同用户配置本地化的内容，比如一些成员在自己设备的项目中仅想看到自己打印的Log。
// 4.DebugxManager成员打印开关相关功能转移到新窗口DebugxConsole；DebugxManager在游戏运行时自动创建，不需要再默认创建并保存到场景中。
// 5.新增ColorDispenser类，用于在Member创建时分配一个颜色。
// 6.增加配置辅助功能，重置配置，快速设置全部成员开关，颜色重置和自动分配等。
// 7.编辑器配置界面，适应Dark和Light编辑器皮肤。
// 8.文件夹整理，类重命名，代码整理优化。
////////////////////
// Version: 2.0.1.0 20220920
// 1.GUI界面更新，颜色调整。
// 2.移除DebugxEditorConfig类。
// FixBug
// 1.修复在安卓平台时Application.consoleLogPath获取为空导致无法输出Log文件的问题。
// 2.用户手册名称更新，去除中文。防止一些因中文路径导致打包失败。
// 3.替换掉 new() 的语法，防止低版本的C#报错。
// 4.修复DebugxProjectSettings自动创建流程相关的Bug。
// 5.修复ProjectSettings界面中数组越界Bug。
////////////////////
// Version: 2.0.2.0 20221031
// 1.未注册成员进行打印功能，新增allowUnregisteredMember字段，用于配置是否允许没有注册者打印内容。
// FixBug
// 1.修复某些情况下DebugxProjectSettings初始化时无法通过Resources.Load加载，导致的各类问题。
// 2.DebugxProjectSettingsAsset配置资源加载和创建流程更新，尝试修复配置被重置为空的问题。
////////////////////
// Version: 2.1.0.0 20230103
// 1.菜单栏新增CreateDebugxProjectSettingsAsset方法用于创建配置资源文件。
// 2.DebugxProjectSettingsProvider项目设置界面优化。
// 3.Log方法扩展，可以输入Signature签名来代替Key作为成员参数。
// 4.DebugxBurst更新，移除会导致DOTS项目编译报错的方法。
// FixBug
// 1.在没有DebugxProjectSettings.asset文件时，如果编辑器启动或代码重编译，会导致Resources.Load方法报错堆栈溢出的问题修复。
//   复现流程为在Editor启动方法中或代码编译时，新创建了DebugxProjectSettings.asset资源并保存后，直接调用Resources.Load方法加载此资源。
// 2.修复每次代码重编译时DebugxProjectSettingsAsset资源都被重新创建的默认资源覆盖的bug。
////////////////////
// Version: 2.1.1.0 20250528
// 1.注释添加英文，README更新。界面根据系统语言切换中英文。
// 2.默认关闭测试打印和输出Log文件。修复两处单词拼写错误。
// 3.代码整理。
////////////////////
// Version: 2.2.0.0 20260131
// 1.Debugx 改名为 DebugxBase，在 Unity 中根据当前成员自动生成子类 Debugx 和每个成员的快速调用方法。
// 2.ActionHandler 和 DebugxTools 类转移到 Unity 中，因为和 dll 无关。
// 3.整体代码整理，规范化。命名空间整理。
////////////////////
// Version: 2.2.1.0 20260202
// 1.整理项目代码，拆分类到不同脚本。
////////////////////
// Version: 2.3.3.0
// 1.修复应用配置后运行时成员开关失效、Preferences 批量开关不生效等编辑器交互问题。
// 2.成员遍历补充空元素判空；SetMemberEnable 未初始化时补充告警。
// 3.日志文件输出改为按需刷盘；正则由 DebugxTag 常量动态构造，避免不同步。
// 4.代码整理，版本号同步。
////////////////////
// Version: 2.4.0.0 20260704
// 1.新增结构化日志事件 Debugx.OnRawLog，在唯一日志收口点 LogCreator 携带成员 key/签名/颜色/header/网络标签/LogType/原始 message/最终显示串，供 Debugx Console 等工具以完整成员元数据消费日志。无订阅者时不构造负载，开销近似为零。
// 2.新增 Debugx.IsDebugxTagged 方法，将 [Debugx] 标签判定收敛一处，供外部（如 Console 双通道去重）复用，与 DebugxTag 常量保持同步。
// 3.LogCreator 调整为锁内拼串、锁外派发事件并在 unityLogger.Log 之前触发，保证「事件→同线程紧邻的 logMessageReceived 回调」顺序；网络标签只求值一次。
// 4.退役旧的屏幕 IMGUI 日志覆盖层（LogOutput.DrawGUI），由运行时 UIToolkit 版 Debugx Console 取代；LogOutput 移除整段 Draw Logs（含 message 路径里的 HandleDrawLogs 调用）。
// 5.移除项目设置 drawLogToScreen / restrictDrawLogCount / maxDrawLogs 三字段（Unity 侧同步移除镜像/设置UI/Prefs/序列化键）。
////////////////////////////////////////////////////////////////////////////////////////////////////

#endregion

using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using System.Collections.Generic;

namespace DebugxLog
{
    /// <summary>
    /// Debugx Core Utility Class.
    /// Debugx核心工具类。
    /// Unity 中会继承此类自动创建 Debugx 类，根据当前成员配置生成快速调用方法，比如 LogJack()。
    /// </summary>
    public static class Debugx
    {
        // Debugx, debug extension utility class.
        // Debugx，调试扩展工具类。

        // Add the macro "DEBUG_X" in U3D project to enable the feature methods.
        // 在U3D项目中添加宏“DEBUG_X”开启功能方法。

        private static DebugxProjectSettings Settings => DebugxProjectSettings.Instance;
        private static DebugxMemberInfo AdminInfo => Settings != null ? Settings.AdminInfo : _adminInfoDefault;
        private static readonly DebugxMemberInfo _adminInfoDefault = new DebugxMemberInfo(0, "Admin");

        private static Func<bool> _serverCheckDelegate;
        private static readonly StringBuilder _logSb = new StringBuilder();
        // Protect shared StringBuilder in logging path.
        // 保护日志路径中的共享 StringBuilder，避免并发污染。
        private static readonly object _logSbLocker = new object();

        private static readonly Dictionary<int, bool> _memberEnables = new Dictionary<int, bool>();

        /// <summary>
        /// Master switch for logging.
        /// Log总开关。
        /// </summary>
        public static bool enableLog = true;

        /// <summary>
        /// Master switch for member logs.
        /// 成员Log总开关。
        /// </summary>
        public static bool enableLogMember = true;

        /// <summary>
        /// Allow unregistered members to print logs.
        /// 允许没有注册成员进行打印。
        /// </summary>
        public static bool allowUnregisteredMember = true;

        /// <summary>
        /// Only print logs for this key.
        /// 0 means off; when set to another key, only logs for this key will be printed if the corresponding member info exists.
        /// This value can only be set after turning off logMasterOnly.
        /// 仅打印此Key的Log。
        /// 0为关闭，设置其他Key时，只有此Key对应的成员信息确实存在，才会只打印此Key的成员Log。
        /// 必须关闭logMasterOnly后才能设置此值
        /// </summary>
        public static int logThisKeyMemberOnly;

        /// <summary>
        /// Structured raw log event. Fired once at the single logging funnel (LogCreator), on the same thread and
        /// immediately before UnityEngine.Debug.unityLogger.Log, carrying member metadata + the original message +
        /// the final composed string. Intended for tooling such as the Debugx Console to consume logs with full
        /// member metadata (instead of re-parsing the formatted text). Only meaningful while DEBUG_X is defined
        /// (the whole logging path compiles out otherwise). When there is no subscriber the payload is not even
        /// constructed, so the cost is near zero.
        /// 结构化原始日志事件。在唯一日志收口点（LogCreator）中、于同一线程且紧邻 UnityEngine.Debug.unityLogger.Log
        /// 之前触发一次，携带成员元数据 + 原始 message + 拼好的最终显示串。供 Debugx Console 等工具以完整成员元数据消费日志
        /// （而非对格式化文本做正则反解）。仅在定义了 DEBUG_X 时有意义（否则整条日志路径被编译掉）。
        /// 无订阅者时连负载都不会构造，开销近似为零。
        /// </summary>
        public static event Action<DebugxRawLog> OnRawLog;

        /// <summary>
        /// Whether a log message string was produced by Debugx (i.e. it contains the DebugxTag).
        /// Lets consumers de-duplicate: a message captured from Application.logMessageReceived that is Debugx-tagged
        /// has already been delivered via OnRawLog and should not be counted a second time. Centralizing the check
        /// here keeps it in sync with DebugxTag (mirrors the tag regex in LogOutput).
        /// 判断一条日志字符串是否由 Debugx 产生（即包含 DebugxTag）。
        /// 供消费方去重：从 Application.logMessageReceived 捕获到、且带 Debugx 标签的消息，已经通过 OnRawLog 投递过，
        /// 不应重复计入。判定收敛在此处，与 DebugxTag 保持同步（对应 LogOutput 中的标签正则）。
        /// </summary>
        /// <param name="message">The log message string to test. 待检测的日志字符串。</param>
        /// <returns>True if the message carries the DebugxTag. 若消息带有 DebugxTag 则为 true。</returns>
        public static bool IsDebugxTagged(string message)
        {
            return !string.IsNullOrEmpty(message) && message.Contains(DebugxProjectSettings.DebugxTag);
        }

        /// <summary>
        /// OnAwake lifecycle method.
        /// Awake 生命周期方法。
        /// </summary>
        public static void OnAwake()
        {
            ResetToDefault();

            if (Settings != null && Settings.members != null)
            {
                foreach (var info in Settings.members)
                {
                    if (info == null) continue;

                    // 配置数据异常出现重复 key 时，后值覆盖前值，避免抛出重复 key 异常。
                    _memberEnables[info.key] = info.enableDefault;
                }
            }
        }

        /// <summary>
        /// OnDestroy lifecycle method.
        /// OnDestroy 生命周期方法。
        /// </summary>
        public static void OnDestroy()
        {
            ResetToDefault();
        }

        /// <summary>
        /// Reset data to defaults in Settings.
        /// 重置数据到Settings中Default。
        /// </summary>
        public static void ResetToDefault()
        {
            if (Settings != null)
            {
                enableLog = Settings.enableLogDefault;
                enableLogMember = Settings.enableLogMemberDefault;
                allowUnregisteredMember = Settings.allowUnregisteredMember;
                logThisKeyMemberOnly = Settings.logThisKeyMemberOnlyDefault;
            }

            _memberEnables.Clear();
        }

        /// <summary>
        /// Set member switch during game runtime.
        /// 在游戏运行时设置成员开关。
        /// </summary>
        /// <param name="key">The key of the member to set. 成员的Key。</param>
        /// <param name="enable">Whether to enable or disable the member log. 是否启用该成员的日志。</param>
        [Conditional("DEBUG_X")]
        public static void SetMemberEnable(int key, bool enable)
        {
            // _memberEnables 仅在 OnAwake（游戏运行时）填充。未初始化时给出告警而非静默失败。
            // _memberEnables is populated only in OnAwake (at runtime). Warn instead of failing silently when uninitialized.
            if (_memberEnables.Count == 0)
            {
                LogAdmWarning("Debugx.SetMemberEnable: 运行时成员开关尚未初始化，设置被忽略。请在游戏运行时（OnAwake 之后）调用。 Member switches are not initialized yet; call at runtime after OnAwake.");
                return;
            }

            if (!_memberEnables.ContainsKey(key))
            {
                Debugx.LogAdmWarning($"Debugx.SetMemberEnable: cant find memberInfo by key:{key}. 无法找到Key为{key}的成员信息。");
                return;
            }

            _memberEnables[key] = enable;
        }

        /// <summary>
        /// Confirm whether the member is enabled.
        /// 确认成员是否打开。
        /// </summary>
        /// <param name="key">The key of the member. 成员的Key。</param>
        /// <returns>True if enabled, otherwise false. 是否启用。</returns>
        public static bool MemberIsEnable(int key)
        {
            if (_memberEnables != null && _memberEnables.Count > 0)
            {
                if (!_memberEnables.TryGetValue(key, out var enable)) return false;
                return enable;
            }

            if (Settings != null && Settings.members != null && Settings.members.Length > 0)
            {
                foreach (var info in Settings.members)
                {
                    if (info == null) continue;
                    if (info.key == key)
                    {
                        return info.enableDefault;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Sets the method to determine whether the current context is a server.
        /// 设置确认是否是服务器的方法。
        /// This should be called by the project. Only after this is set,
        /// can the Logx series methods print network tags when showNetTag is true.
        /// 由项目调用设置，那么Logx系列方法的showNetTag参数设置为true后，才能打印网络标记。
        /// </summary>
        /// <param name="serverCheckFunc">The function used to check if the current context is a server.
        /// 用于判断当前是否服务器的方法。</param>
        [Conditional("DEBUG_X")]
        public static void SetServerCheck(Func<bool> serverCheckFunc)
        {
            if (serverCheckFunc == null) return;

            _serverCheckDelegate = serverCheckFunc;
        }

        /// <summary>
        /// Checks if a member key exists.
        /// 确认成员Key是否包含。
        /// This method can be used both during runtime and outside of runtime.
        /// 在程序运行时和非运行时都可用。
        /// </summary>
        /// <param name="key">The member key to check. 成员的Key。</param>
        /// <returns>Returns true if the member key exists; otherwise, false.
        /// 如果存在该成员Key则返回true，否则返回false。</returns>
        public static bool ContainsMemberKey(int key)
        {
            if (GetMemberInfo(key, out _))
            {
                return true;
            }

            return false;
        }

        private static bool GetMemberInfo(int key, out DebugxMemberInfo memberInfo)
        {
            memberInfo = null;
            if (Settings == null || Settings.members == null || Settings.members.Length == 0) return false;

            foreach (var t in Settings.members)
            {
                if (t == null) continue;
                if (t.key == key)
                {
                    memberInfo = t;
                    return true;
                }
            }

            return false;
        }

        private static bool GetMemberInfo(string signature, out DebugxMemberInfo memberInfo)
        {
            memberInfo = null;

            if (Settings == null)
            {
                LogAdmWarning(
                    $"Debugx.GetMemberInfo: The initial configuration is not performed.Settings is null. 未成功初始化配置，Settings为空。");
                return false;
            }

            if (Settings.members == null)
            {
                LogAdmWarning(
                    $"Debugx.GetMemberInfo: The initial configuration is not performed.Settings.members is null. 未初始化配置，Settings.members为空。");
                return false;
            }

            if (Settings.members.Length == 0)
            {
                LogAdmWarning(
                    $"Debugx.GetMemberInfo: There are no members available.Settings.members.Length is 0. 没有任何可用的成员，Settings.members.Length为0。");
                return false;
            }

            foreach (var t in Settings.members)
            {
                if (t == null) continue;
                if (t.signature == signature)
                {
                    memberInfo = t;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Member log printing.
        /// 成员打印Log。
        /// </summary>
        /// <remarks>
        /// Note: message is typed as object. Turning a member OFF (or the master switches off) still lets the caller's
        /// string interpolation / boxing execute before this method decides to drop the log. Disabling a member is NOT
        /// zero-cost while DEBUG_X is defined; for hot paths, guard with MemberIsEnable or compile out via DEBUG_X.
        /// 注意：message 为 object 类型。关闭某成员（或总开关关闭）时，调用方的字符串插值/装箱仍会在本方法丢弃日志前完成。
        /// 在定义了 DEBUG_X 的情况下，关闭成员并非零成本；高频路径请先用 MemberIsEnable 判断，或用 DEBUG_X 宏整体编译掉。
        /// </remarks>
        /// <param name="key">Member key, configured in DebugxMemberInfo. 成员密钥，DebugxMemberInfo中配置的key。</param>
        /// <param name="message">Content to log. 打印内容。</param>
        /// <param name="showTime">Whether to show timestamp. 显示时间。</param>
        /// <param name="showNetTag">
        /// Whether to show network tag (Server or Client).
        /// This feature depends on the project and requires setting via the SetServerCheck method.
        /// 显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置。
        /// </param>
        [Conditional("DEBUG_X")]
        public static void Log(int key, object message, bool showTime = false, bool showNetTag = true)
        {
            Log(LogType.Log, key, message, showTime, showNetTag);
        }

        /// <summary>
        /// Member log printing.
        /// 成员打印Log。
        /// </summary>
        /// <param name="signature">Member signature, configured in DebugxMemberInfo. 成员签名，DebugxMemberInfo中配置的Signature。</param>
        /// <param name="message">Content to log. 打印内容。</param>
        /// <param name="showTime">Whether to show timestamp. 显示时间。</param>
        /// <param name="showNetTag">
        /// Whether to show network tag (Server or Client).
        /// This feature depends on the project and requires setting via the SetServerCheck method.
        /// 显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置。
        /// </param>
        [Conditional("DEBUG_X")]
        public static void Log(string signature, object message, bool showTime = false, bool showNetTag = true)
        {
            Log(LogType.Log, signature, message, showTime, showNetTag);
        }

        /// <summary>
        /// Member LogWarning printing.
        /// 成员打印LogWarning。
        /// </summary>
        /// <param name="key">Member key, configured in DebugxMemberInfo. 成员密钥，DebugxMemberInfo中配置的key。</param>
        /// <param name="message">Content to log. 打印内容。</param>
        /// <param name="showTime">Whether to show timestamp. 显示时间。</param>
        /// <param name="showNetTag">
        /// Whether to show network tag (Server or Client).
        /// This feature depends on the project and requires setting via the SetServerCheck method.
        /// 显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置。
        /// </param>
        [Conditional("DEBUG_X")]
        public static void LogWarning(int key, object message, bool showTime = false, bool showNetTag = true)
        {
            Log(LogType.Warning, key, message, showTime, showNetTag);
        }

        /// <summary>
        /// Member LogWarning printing.
        /// 成员打印LogWarning。
        /// </summary>
        /// <param name="signature">Member signature, configured in DebugxMemberInfo. 成员签名，DebugxMemberInfo中配置的Signature。</param>
        /// <param name="message">Content to log. 打印内容。</param>
        /// <param name="showTime">Whether to show timestamp. 显示时间。</param>
        /// <param name="showNetTag">
        /// Whether to show network tag (Server or Client).
        /// This feature depends on the project and requires setting via the SetServerCheck method.
        /// 显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置。
        /// </param>
        [Conditional("DEBUG_X")]
        public static void LogWarning(string signature, object message, bool showTime = false, bool showNetTag = true)
        {
            Log(LogType.Warning, signature, message, showTime, showNetTag);
        }

        /// <summary>
        /// Member LogError printing.
        /// 成员打印LogError。
        /// </summary>
        /// <param name="key">Member key, configured in DebugxMemberInfo. 成员密钥，DebugxMemberInfo中配置的key。</param>
        /// <param name="message">Content to log. 打印内容。</param>
        /// <param name="showTime">Whether to show timestamp. 显示时间。</param>
        /// <param name="showNetTag">
        /// Whether to show network tag (Server or Client).
        /// This feature depends on the project and requires setting via the SetServerCheck method.
        /// 显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置。
        /// </param>
        [Conditional("DEBUG_X")]
        public static void LogError(int key, object message, bool showTime = false, bool showNetTag = true)
        {
            Log(LogType.Error, key, message, showTime, showNetTag);
        }

        /// <summary>
        /// Member LogError printing.
        /// 成员打印LogError。
        /// </summary>
        /// <param name="signature">Member signature, configured in DebugxMemberInfo. 成员签名，DebugxMemberInfo中配置的Signature。</param>
        /// <param name="message">Content to log. 打印内容。</param>
        /// <param name="showTime">Whether to show timestamp. 显示时间。</param>
        /// <param name="showNetTag">
        /// Whether to show network tag (Server or Client).
        /// This feature depends on the project and requires setting via the SetServerCheck method.
        /// 显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置。
        /// </param>
        [Conditional("DEBUG_X")]
        public static void LogError(string signature, object message, bool showTime = false, bool showNetTag = true)
        {
            Log(LogType.Error, signature, message, showTime, showNetTag);
        }

        private static void Log(LogType type, int key, object message, bool showTime = false, bool showNetTag = true)
        {
            if (!enableLog || !enableLogMember) return;
            LogCreator(type, key, message, showTime, showNetTag);
        }

        private static void Log(LogType type, string signature, object message, bool showTime = false,
            bool showNetTag = true)
        {
            if (!enableLog || !enableLogMember) return;
            LogCreator(type, signature, message, showTime, showNetTag);
        }

        /// <summary>
        /// Checks whether displaying only members with a specific key is enabled.
        /// Returns true to allow filtering by key; false to disallow and prevent logging.
        /// 确认是否开启了仅显示某个Key的成员。
        /// true=允许通过 false=返回，不允许打印。
        /// </summary>
        private static bool CheckLogThisKeyMemberOnly(int key)
        {
            // 顺序敏感：先比对当前 key，命中即短路，避免开启过滤后每条日志都对 members 做一次线性扫描。
            // Order matters: test the current key first so the common path short-circuits before the O(n) ContainsMemberKey scan.
            return logThisKeyMemberOnly == 0 || logThisKeyMemberOnly == key || !ContainsMemberKey(logThisKeyMemberOnly);
        }

        /// <summary>
        /// Extended logging method.
        /// 扩展日志打印方法。
        /// </summary>
        /// <param name="type">Log type. 日志类型。</param>
        /// <param name="key">Member key configured in DebugxMemberInfo. DebugxMemberInfo 中配置的成员 Key。</param>
        /// <param name="message">Content to log. 打印内容。</param>
        /// <param name="showTime">Whether to show the timestamp. 是否显示时间戳。</param>
        /// <param name="showNetTag">
        /// Whether to show the network tag (Server or Client). 
        /// This feature depends on the project and requires setting via the SetServerCheck method.
        /// 是否显示网络标记（Server 或 Client）。
        /// 此功能依赖项目侧实现，需要通过 SetServerCheck 方法设置。
        /// </param>
        private static void LogCreator(LogType type, int key, object message, bool showTime = false,
            bool showNetTag = true)
        {
            if (GetMemberInfo(key, out DebugxMemberInfo memberInfo))
            {
                // This member is not enabled.
                // 此成员未开启。
                if (!MemberIsEnable(key)) return;
            }
            else
            {
                LogAdmWarning($"Debugx.LogCreator: cant find memberInfo by key:{key}. 无法找到Key为{key}的成员信息。");
                if (!allowUnregisteredMember) return;
            }

            // Set to print logs only for a specific member key.
            // 设置为仅打印某个 Key 成员的日志。
            if (!CheckLogThisKeyMemberOnly(key)) return;

            LogCreator(type, memberInfo, message, showTime, showNetTag);
        }

        private static void LogCreator(LogType type, string signature, object message, bool showTime = false,
            bool showNetTag = true)
        {
            int key = 0;
            if (GetMemberInfo(signature, out DebugxMemberInfo memberInfo))
            {
                key = memberInfo.key;
                // This member is not enabled.
                // 此成员未开启。
                if (!MemberIsEnable(key)) return;
            }
            else
            {
                LogAdmWarning(
                    $"Debugx.LogCreator: cant find memberInfo by signature:{signature}. 无法找到Signature为{signature}的成员信息。");
                if (!allowUnregisteredMember) return;
            }

            // Logging is restricted to a specific member key only.
            // 日志仅允许指定的 Key 成员打印。
            if (!CheckLogThisKeyMemberOnly(key)) return;

            LogCreator(type, memberInfo, message, showTime, showNetTag);
        }

        private static void LogCreator(LogType type, DebugxMemberInfo info, object message, bool showTime = false,
            bool showNetTag = true)
        {
            // Evaluate the net tag once, shared by string building and the structured event, so the project's
            // server-check delegate is invoked only a single time per log.
            // 网络标签只求值一次，供拼串与结构化事件共用，使项目的 server-check 委托每条日志只被调用一次。
            bool hasNetTag = showNetTag && _serverCheckDelegate != null;
            bool isServer = hasNetTag && _serverCheckDelegate.Invoke();

            string finalText;
            lock (_logSbLocker)
            {
                try
                {
                    _logSb.Append(DebugxProjectSettings.DebugxTag);

                    if (hasNetTag)
                        _logSb.Append(isServer ? "Server: " : "Client: ");

                    if (showTime)
                    {
                        _logSb.Append($" [{DateTime.Now:HH:mm:ss}] ");
                    }

                    if (info != null)
                    {
                        if (info.LogSignature)
                            _logSb.Append($"[Sig: {info.signature}]");

                        if (!string.IsNullOrEmpty(info.color))
                            _logSb.Append(info.haveHeader
                                ? $" <color=#{info.color}>{info.header} : {message}</color>"
                                : $" <color=#{info.color}>{message}</color>");
                        else
                            _logSb.Append(info.haveHeader ? $" {info.header} : {message}" : $" {message}");
                    }
                    else
                    {
                        _logSb.Append($" UnregisteredMember : {message}");
                    }

                    finalText = _logSb.ToString();
                }
                finally
                {
                    _logSb.Length = 0;
                }
            }

            // Dispatch the structured event first (outside the lock, so a subscriber that logs again cannot re-enter
            // and corrupt _logSb), then emit the Unity log. This preserves the "event -> the immediately-following
            // same-thread logMessageReceived callback" ordering that backs the Console's per-thread FIFO de-dup pairing.
            // 先派发结构化事件（在锁外，避免订阅者回调再打日志导致 _logSb 被重入破坏），再发 Unity 日志。
            // 由此保证「事件 → 同线程紧邻的 logMessageReceived 回调」顺序，支撑 Console 的线程内 FIFO 去重配对。
            if (OnRawLog != null)
                DispatchRawLog(type, info, message, finalText, showTime, hasNetTag, isServer);

            UnityEngine.Debug.unityLogger.Log(type, finalText);
        }

        /// <summary>
        /// Build and dispatch the structured raw log to <see cref="OnRawLog"/> subscribers. The payload struct is not
        /// even constructed when there is no subscriber. Subscriber exceptions are isolated so one bad subscriber
        /// cannot break the logging pipeline or the other subscribers.
        /// 构造并向 <see cref="OnRawLog"/> 订阅者派发结构化原始日志。无订阅者时连负载结构都不构造。
        /// 订阅者异常被隔离，避免单个坏订阅者破坏日志管线或其它订阅者。
        /// </summary>
        private static void DispatchRawLog(LogType type, DebugxMemberInfo info, object message, string finalText,
            bool showTime, bool netTagShown, bool isServer)
        {
            // Local snapshot to avoid the race between the null check and the invocation if unsubscribed in between.
            // 本地快照，规避判空与调用之间被取消订阅置空的竞态。
            Action<DebugxRawLog> handler = OnRawLog;
            if (handler == null) return;

            int key;
            string signature, colorHex, header;
            bool logSignatureShown;
            DebugxLogCategory category;

            if (info == null)
            {
                key = DebugxRawLog.UnregisteredKey;
                signature = null;
                colorHex = null;
                header = null;
                logSignatureShown = false;
                category = DebugxLogCategory.Unregistered;
            }
            else
            {
                key = info.key;
                signature = info.signature;
                colorHex = string.IsNullOrEmpty(info.color) ? null : info.color;
                header = info.haveHeader ? info.header : null;
                logSignatureShown = info.LogSignature;
                // Admin channel (LogAdm) always uses AdminInfo whose key is 0; custom/preset members use their own key.
                // 管理通道（LogAdm）始终使用 key 为 0 的 AdminInfo；自定义/预设成员使用各自的 key。
                category = info.key == 0 ? DebugxLogCategory.Admin : DebugxLogCategory.Member;
            }

            DebugxRawLog rawLog = new DebugxRawLog(
                key, signature, colorHex, header, logSignatureShown, category,
                type, message, finalText, netTagShown, showTime, isServer, DateTime.Now);

            try
            {
                handler.Invoke(rawLog);
            }
            catch (Exception ex)
            {
                // Must not route through Debugx logging here (would recurse); report via the raw Unity error log.
                // 此处不能再走 Debugx 日志（会递归）；直接用 Unity 原生错误日志上报订阅者异常。
                UnityEngine.Debug.LogError("[Debugx] OnRawLog subscriber threw: " + ex);
            }
        }

        #region LogAdm

        /// <summary>
        /// Administrative log printing.
        /// For plugin developers only; generally not intended for use by others.
        /// 管理打印Log。
        /// 插件开发者使用，所有人理论上都不可使用此方法。
        /// </summary>
        /// <param name="message">The content to log. 打印内容。</param>
        /// <param name="showTime">Whether to display the timestamp. 显示时间。</param>
        /// <param name="showNetTag">
        /// Whether to display the network tag (Server or Client). 
        /// This feature depends on the project and requires setting via the SetServerCheck method.
        /// 显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置。
        /// </param>
        [Conditional("DEBUG_X")]
        public static void LogAdm(object message, bool showTime = false, bool showNetTag = true)
        {
            LogAdm(LogType.Log, message, showTime, showNetTag);
        }

        /// <summary>
        /// Administrative log warning printing.
        /// For plugin developers only; generally not intended for use by others.
        /// 管理打印LogWarning。
        /// 插件开发者使用，所有人理论上都不可使用此方法。
        /// </summary>
        /// <param name="message">The content to log as a warning. 打印内容。</param>
        /// <param name="showTime">Whether to display the timestamp. 显示时间。</param>
        /// <param name="showNetTag">
        /// Whether to display the network tag (Server or Client). 
        /// This feature depends on the project and requires setting via the SetServerCheck method.
        /// 显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置。
        /// </param>
        [Conditional("DEBUG_X")]
        public static void LogAdmWarning(object message, bool showTime = false, bool showNetTag = true)
        {
            LogAdm(LogType.Warning, message, showTime, showNetTag);
        }

        /// <summary>
        /// Administrative log error printing.
        /// For plugin developers only; generally not intended for use by others.
        /// 管理打印LogError。
        /// 插件开发者使用，所有人理论上都不可使用此方法。
        /// </summary>
        /// <param name="message">The content to log as an error. 打印内容。</param>
        /// <param name="showTime">Whether to display the timestamp. 显示时间。</param>
        /// <param name="showNetTag">
        /// Whether to display the network tag (Server or Client). 
        /// This feature depends on the project and requires setting via the SetServerCheck method.
        /// 显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置。
        /// </param>
        [Conditional("DEBUG_X")]
        public static void LogAdmError(object message, bool showTime = false, bool showNetTag = true)
        {
            LogAdm(LogType.Error, message, showTime, showNetTag);
        }

        private static void LogAdm(LogType type, object message, bool showTime = false, bool showNetTag = true)
        {
            LogCreator(type, AdminInfo, message, showTime, showNetTag);
        }
        #endregion
    }
}