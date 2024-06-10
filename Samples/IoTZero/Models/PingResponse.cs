using NewLife.Remoting.Models;

namespace NewLife.IoT.Models;

/// <summary>心跳响应</summary>
public class PingResponse : IPingResponse
{
    /// <summary>本地时间。ms毫秒</summary>
    public Int64 Time { get; set; }

    /// <summary>服务器时间</summary>
    public Int64 ServerTime { get; set; }

    /// <summary>心跳周期。单位秒</summary>
    public Int32 Period { get; set; }

    /// <summary>令牌。现有令牌即将过期时，颁发新的令牌</summary>
    public String Token { get; set; }

    /// <summary>下发命令</summary>
    public CommandModel[]? Commands { get; set; }
}