using NewLife.Log;
using NewLife.Remoting.Models;
#if !NET45
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Services;

/// <summary>命令会话基类。管理单个客户端连接的会话状态，处理服务端下发的命令</summary>
/// <remarks>
/// 命令会话是服务端与客户端之间的通信桥梁，每个客户端连接对应一个会话实例。
/// 
/// <para>核心职责：</para>
/// <list type="bullet">
/// <item>维护会话标识（Code），用于识别命令归属的客户端</item>
/// <item>跟踪会话活跃状态，支持会话管理器定期清理不活跃会话</item>
/// <item>处理服务端下发的命令，将命令转发给客户端</item>
/// <item>管理客户端在线/离线状态回调</item>
/// </list>
/// 
/// <para>派生类实现：</para>
/// <list type="bullet">
/// <item><see cref="WsCommandSession"/>：WebSocket 会话实现，通过 WebSocket 长连接与客户端通信</item>
/// </list>
/// 
/// <para>使用场景：</para>
/// 配合 <see cref="ISessionManager"/> 使用，由会话管理器统一管理会话的生命周期。
/// </remarks>
public class CommandSession : DisposeBase, ICommandSession
{
    #region 属性
    /// <summary>会话编码。用于唯一标识客户端，作为命令分发的路由键</summary>
    public String Code { get; set; } = null!;

    /// <summary>是否活动中。用于判断会话是否有效，派生类应重写此属性以反映实际连接状态</summary>
    public virtual Boolean Active { get; } = true;

    /// <summary>日志提供者。用于记录会话相关日志</summary>
    public ILogProvider? Log { get; set; }

    /// <summary>在线状态回调。会话上线或下线时触发，参数为 true 表示上线，false 表示下线</summary>
    public Action<Boolean>? SetOnline { get; set; }

    /// <summary>链路追踪器。用于记录分布式调用链</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    /// <summary>处理服务端下发的命令。派生类应重写此方法实现具体的命令发送逻辑</summary>
    /// <param name="command">命令模型，包含命令名称、参数等信息</param>
    /// <param name="message">原始命令消息的 JSON 字符串，可直接发送给客户端</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public virtual Task HandleAsync(CommandModel command, String? message, CancellationToken cancellationToken) => TaskEx.CompletedTask;
}
