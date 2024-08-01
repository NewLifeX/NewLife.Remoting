namespace NewLife.Remoting.Clients;

/// <summary>基于服务器时间差的时间提供者</summary>
public class ServerTimeProvider : TimeProvider
{
    /// <summary>客户端</summary>
    public ClientBase Client { get; set; } = null!;

    /// <summary>获取UTC时间</summary>
    /// <returns></returns>
    public override DateTimeOffset GetUtcNow() => Client != null ? DateTime.UtcNow.Add(Client.Span) : base.GetUtcNow();
}
