using System.Diagnostics;
using Unity.Burst;

namespace DebugxLog
{
    /// <summary>
    /// 用于Burst多线程代码中的Debug工具类。
    /// Debug utility class for Burst multithreaded code.
    /// </summary>
    public class DebugxBurst
    {
        //在DOTS的Burst中的限制问题：
        //一些受到限制的代码，一些功能不支持，在编译时会直接报红。
        //在DOTS的Burst多线程中，不能使用任何引用类型，string只能直接传递，使用String.Format时不能传入string类型。
        //UnityEngine.Debug.unityLogger()不能使用。只能直接使用UnityEngine.Debug.Log等方法，这类方法应该是由引擎开封这做过特殊处理，从而能够直接传参object类型。
        //使用外部的值时，值必须是只读的
        //in ref out 都不支持
        //所以使用[BurstDiscard]特性用于在多线程时直接排除此方法，添加此宏能够使一些被限制的代码在编译时不报错，但必须在Entities.ForEach().WithoutBurst().Run()才能工作;,因为所有[BurstDiscard]特性的方法在多线程中都不工作。

        //在Burst多线程中打印可以直接使用UnityEngine.Debug.Log()，就算调用只传递string的自定义Log方法，运行时不报错但是打包编译时会报错
        //UnityEngine.Debug.Log()应该是Unity经过处理，所以不会打包编译报错

        /// <summary>
        /// 成员打印Log。
        /// 必须在Entities.ForEach().WithoutBurst().Run()时才能工作。
        /// </summary>
        /// <param name="key">DebugxMemberInfo中配置的key</param>
        /// <param name="message">打印内容</param>
        /// <param name="showTime">显示时间</param>
        /// <param name="showNetTag">显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置</param>
        [Conditional("DEBUG_X"), BurstDiscard]
        public static void Log(int key, object message, bool showTime = false, bool showNetTag = true)
        {
            DebugxBase.Log(key, message, showTime, showNetTag);
        }

        /// <summary>
        /// 成员打印LogWarning。
        /// 必须在Entities.ForEach().WithoutBurst().Run()时才能工作。
        /// </summary>
        /// <param name="key">DebugxMemberInfo中配置的key</param>
        /// <param name="message">打印内容</param>
        /// <param name="showTime">显示时间</param>
        /// <param name="showNetTag">显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置</param>
        [Conditional("DEBUG_X"), BurstDiscard]
        public static void LogWarning(int key, object message, bool showTime = false, bool showNetTag = true)
        {
            DebugxBase.LogWarning(key, message, showTime, showNetTag);
        }

        /// <summary>
        /// 成员打印LogError。
        /// 必须在Entities.ForEach().WithoutBurst().Run()时才能工作。
        /// </summary>
        /// <param name="key">DebugxMemberInfo中配置的key</param>
        /// <param name="message">打印内容</param>
        /// <param name="showTime">显示时间</param>
        /// <param name="showNetTag">显示网络标记，Server或者Client。此功能依赖项目，需要项目通过SetServerCheck方法来设置</param>
        [Conditional("DEBUG_X"), BurstDiscard]
        public static void LogError(int key, object message, bool showTime = false, bool showNetTag = true)
        {
            DebugxBase.LogError(key, message, showTime, showNetTag);
        }
    }
}