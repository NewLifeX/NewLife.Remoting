namespace NewLife.Remoting.Models;

/// <summary>在线信息接口</summary>
public interface IOnlineModel
{
    /// <summary>会话标识。唯一确定该会话</summary>
    String SessionId { get; set; }
}

/// <summary>在线信息接口（扩展）</summary>
public interface IOnlineModel2 : IOnlineModel
{
    /// <summary>填充心跳请求信息到在线记录</summary>
    /// <param name="request">心跳请求</param>
    /// <param name="context">上下文</param>
    void File(IPingRequest request, Object context);
}