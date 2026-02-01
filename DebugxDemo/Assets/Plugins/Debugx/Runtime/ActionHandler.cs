using System;

namespace DebugxLog.Tools
{
    /// <summary>
    /// Event handler.
    /// 事件处理器。
    /// </summary>
    public class ActionHandler
    {
        // Cannot use <in T> to apply to Action with all parameter counts, so just write several versions.
        // For Actions with different numbers of parameters, copy this class and modify accordingly.
        // 不能<in T>来应用于所有数量参数的Action，只能多写几个了。
        // 有其他数量参数的Action就复制这个类改一下。

        private event Action MainAction;

        /// <summary>
        /// Bind delegate.
        /// 绑定委托。
        /// </summary>
        /// <param name="action"></param>
        /// <param name="preventDuplicate"></param>
        public void Bind(Action action, bool preventDuplicate = true)
        {
            if (preventDuplicate && DebugxTools.ContainsDelegate(MainAction, action)) return;

            MainAction += action;
        }

        /// <summary>
        /// Unbind delegate.
        /// 解绑委托。
        /// </summary>
        /// <param name="action"></param>
        public void Unbind(Action action)
        {
            MainAction -= action;
        }

        /// <summary>
        /// Invoke.
        /// 调用。
        /// </summary>
        public void Invoke()
        {
            if (MainAction == null) return;
            MainAction.Invoke();
        }

        //private void Clear()
        //{
        //    MainAction = null;
        //}
    }

    /// <summary>
    /// Event handler.
    /// Provides methods for binding, unbinding, and invoking.
    /// Does not provide a Clear method because each user should manage their own delegate bindings and unbindings.
    /// This handler is designed to prevent risky operations like setting action = null directly.
    /// 事件处理器。
    /// 提供了绑定，解绑，调用等方法。
    /// 没有提供Clear，因为每个人都应当管理自己需要绑定和解绑的委托。此处理器就是为了屏蔽action=null等较危险的操作权限。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ActionHandler<T>
    {
        // 不能<in T>来应用于所有数量参数的Action，只能多写几个了。
        // 有其他数量参数的Action就复制这个类改一下。

        private event Action<T> MainAction;

        /// <summary>
        /// 绑定委托。
        /// 绑定委托。
        /// </summary>
        /// <param name="action"></param>
        /// <param name="preventDuplicate"></param>
        public void Bind(Action<T> action, bool preventDuplicate = true)
        {
            if (preventDuplicate && DebugxTools.ContainsDelegate(MainAction, action)) return;

            MainAction += action;
        }

        /// <summary>
        /// Unbind delegate.
        /// 解绑委托。
        /// </summary>
        /// <param name="action"></param>
        public void Unbind(Action<T> action)
        {
            MainAction -= action;
        }

        /// <summary>
        /// Invoke.
        /// 调用。
        /// </summary>
        /// <param name="obj"></param>
        public void Invoke(T obj)
        {
            if (MainAction == null) return;
            MainAction.Invoke(obj);
        }

        //private void Clear()
        //{
        //    MainAction = null;
        //}
    }
}