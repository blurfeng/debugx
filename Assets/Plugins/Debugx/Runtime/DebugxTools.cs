using System;

namespace DebugxLog.Tools
{
    internal static class DebugxTools
    {
        /// <summary>
        /// 确认委托是否已经包含。
        /// Confirm whether the delegate is already included.
        /// </summary>
        /// <param name="main"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool ContainsDelegate(Delegate main, Delegate other)
        {
            if (main == null || other == null) return false;

            var list = main.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                var item = list[i];
                if (item.Equals(other)) return true;
            }

            return false;
        }
    }
}