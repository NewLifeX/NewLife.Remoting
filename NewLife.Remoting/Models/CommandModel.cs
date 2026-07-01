namespace NewLife.Remoting.Models;

/// <summary>命令模型</summary>
public class CommandModel
{
    /// <summary>序号</summary>
    public Int64 Id { get; set; }

    /// <summary>命令</summary>
    public String Command { get; set; } = null!;

    /// <summary>参数</summary>
    public String? Argument { get; set; }

    /// <summary>开始执行时间。用于提前下发指令后延期执行，暂时不支持取消</summary>
    /// <remarks>
    /// 使用UTC时间传输，客户端转本地时间，避免时区差异。
    /// 有些序列化框架可能不支持带时区信息的序列化，因此约定使用UTC时间传输。
    /// </remarks>
    public DateTime StartTime { get; set; }

    /// <summary>过期时间。未指定时表示不限制</summary>
    /// <remarks>
    /// 使用UTC时间传输，客户端转本地时间，避免时区差异。
    /// 有些序列化框架可能不支持带时区信息的序列化，因此约定使用UTC时间传输。
    /// </remarks>
    public DateTime Expire { get; set; }

    /// <summary>跟踪标识。传输traceParent，用于建立全局调用链，便于查找问题</summary>
    public String? TraceId { get; set; }

    /// <summary>指令状态。用于指令持久化和状态机追踪</summary>
    public CommandStatus Status { get; set; } = CommandStatus.就绪;

    /// <summary>已重试次数</summary>
    public Int32 RetryCount { get; set; }

    /// <summary>最大重试次数。默认 3 次</summary>
    public Int32 MaxRetries { get; set; } = 3;

    /// <summary>重试间隔（秒）。默认 10 秒，指数退避：10s, 20s, 40s</summary>
    public Int32 RetryInterval { get; set; } = 10;

    /// <summary>设备编码。用于响应广播时定位发起方</summary>
    public String? Code { get; set; }

    /// <summary>发起方节点标识。用于集群中精确定位 SendCommand 发起实例</summary>
    public String? SenderNodeId { get; set; }

    /// <summary>响应数据。设备执行完成后上报的结果数据</summary>
    public String? Data { get; set; }
}