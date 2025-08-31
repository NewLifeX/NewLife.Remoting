namespace NewLife.Remoting.Models;

/// <summary>在线信息接口</summary>
public interface IOnlineModel
{
    /// <summary>会话标识。唯一确定该会话</summary>
    String SessionId { get; set; }
}
