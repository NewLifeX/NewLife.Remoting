using NewLife.Log;
using NewLife.Messaging;
#if !NET45
using TaskEx=System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Services;

public class DeviceSession : DisposeBase, IDeviceSession, IEventHandler<String>
{
    #region 属性
    public String Code { get; set; }

    public ITracer Tracer { get; set; }
    #endregion

    public void Start(CancellationTokenSource source)
    {

    }

    public Task HandleAsync(String @event, IEventContext<String> context)
    {
        if (context is not DeviceEventContext ctx || ctx.Code.IsNullOrEmpty() || ctx.Code != Code)
            return TaskEx.CompletedTask;

        return TaskEx.CompletedTask;
    }
}
