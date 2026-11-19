#if !UNITY_2023_1_OR_NEWER
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace HowFrame
{
    /// <summary>
    /// 2022.3 兼容补丁：Unity 2023.1+ 原生支持 await AsyncOperation，2022 没有。
    /// 让 `await request.SendWebRequest()` 等写法在 2022.3 上也能编译。升级到 2023.1+ 后本文件自动失效。
    /// completed 回调在主线程触发，await 之后的代码仍回到主线程（Unity SynchronizationContext）。
    /// </summary>
    public static class AsyncOperationAwaiter
    {
        public static TaskAwaiter GetAwaiter(this AsyncOperation op)
        {
            if (op.isDone) return Task.CompletedTask.GetAwaiter();
            var tcs = new TaskCompletionSource<bool>();
            op.completed += _ => tcs.TrySetResult(true);
            return ((Task)tcs.Task).GetAwaiter();
        }
    }
}
#endif
