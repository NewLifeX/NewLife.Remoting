using NewLife.Log;
using NewLife.Remoting.Models;
#if !NET45
using TaskEx=System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Services;

/// <summary>设备会话</summary>
public class CommandSession : DisposeBase, ICommandSession
{
    #region 属性
    /// <summary>会话编码。用于识别指令归属</summary>
    public String Code { get; set; } = null!;

    /// <summary>是否活动中</summary>
    public virtual Boolean Active { get; } = true;

    /// <summary>写日志</summary>
    public ILogProvider? Log { get; set; }

    /// <summary>设置 在线/离线</summary>
    public Action<Boolean>? SetOnline { get; set; }
    #endregion

    /// <summary>处理事件</summary>
    public virtual Task HandleAsync(CommandModel command, String? message, CancellationToken cancellationToken) => TaskEx.CompletedTask;
}
