using NewLife.Log;
using NewLife.Messaging;
using NewLife.Remoting.Models;
#if !NET45
using TaskEx=System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Services;

/// <summary>设备会话</summary>
public class DeviceSession : DisposeBase, IDeviceSession
{
    #region 属性
    /// <summary>编码</summary>
    public String Code { get; set; } = null!;

    /// <summary>最后一次通信时间，主要表示会话活跃时间，包括收发</summary>
    public DateTime LastTime { get; }

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    /// <summary>开始数据交互</summary>
    public void Start(CancellationTokenSource source)
    {
    }

    /// <summary>处理事件</summary>
    public virtual Task HandleAsync(CommandModel command)
    {
        //if (context is not DeviceEventContext ctx || ctx.Code.IsNullOrEmpty() || ctx.Code != Code)
        //    return TaskEx.CompletedTask;

        return TaskEx.CompletedTask;
    }
}
