namespace NewLife.Remoting.Models;

/// <summary>心跳请求</summary>
public interface IPingRequest
{
    ///// <summary>开机时间，单位s</summary>
    //Int32 Uptime { get; set; }

    /// <summary>本地UTC时间。Unix毫秒（UTC）</summary>
    Int64 Time { get; set; }

    ///// <summary>延迟。请求到服务端并返回的延迟时间。单位ms</summary>
    //Int32 Delay { get; set; }
}

/// <summary>扩展的心跳请求。便于ClientBase填充数据</summary>
public interface IPingRequest2 : IPingRequest
{
    /// <summary>内存大小</summary>
    UInt64 Memory { get; set; }

    /// <summary>可用内存大小</summary>
    UInt64 AvailableMemory { get; set; }

    /// <summary>空闲内存大小。在Linux上空闲内存不一定可用，部分作为缓存</summary>
    UInt64 FreeMemory { get; set; }

    /// <summary>磁盘大小。应用所在盘</summary>
    UInt64 TotalSize { get; set; }

    /// <summary>磁盘可用空间。应用所在盘</summary>
    UInt64 AvailableFreeSpace { get; set; }

    /// <summary>CPU占用率</summary>
    Double CpuRate { get; set; }

    /// <summary>温度</summary>
    Double Temperature { get; set; }

    /// <summary>电量</summary>
    Double Battery { get; set; }

    /// <summary>信号强度。WiFi/4G</summary>
    Int32 Signal { get; set; }

    /// <summary>本地IP地址。随着网卡变动，可能改变</summary>
    String? IP { get; set; }

    /// <summary>开机时间，单位s</summary>
    Int32 Uptime { get; set; }

    /// <summary>延迟。请求到服务端并返回的延迟时间。单位ms</summary>
    Int32 Delay { get; set; }
}

/// <summary>心跳请求</summary>
public class PingRequest : IPingRequest, IPingRequest2
{
    #region 属性
    /// <summary>内存大小</summary>
    public UInt64 Memory { get; set; }

    /// <summary>可用内存大小</summary>
    public UInt64 AvailableMemory { get; set; }

    /// <summary>空闲内存大小。在Linux上空闲内存不一定可用，部分作为缓存</summary>
    public UInt64 FreeMemory { get; set; }

    /// <summary>磁盘大小。应用所在盘</summary>
    public UInt64 TotalSize { get; set; }

    /// <summary>磁盘可用空间。应用所在盘</summary>
    public UInt64 AvailableFreeSpace { get; set; }

    /// <summary>CPU占用率</summary>
    public Double CpuRate { get; set; }

    /// <summary>温度</summary>
    public Double Temperature { get; set; }

    /// <summary>电量</summary>
    public Double Battery { get; set; }

    /// <summary>信号强度。WiFi/4G</summary>
    public Int32 Signal { get; set; }

    /// <summary>上行速度。网络发送速度，字节每秒</summary>
    public UInt64 UplinkSpeed { get; set; }

    /// <summary>下行速度。网络接收速度，字节每秒</summary>
    public UInt64 DownlinkSpeed { get; set; }

    /// <summary>本地IP地址。随着网卡变动，可能改变</summary>
    public String? IP { get; set; }

    /// <summary>开机时间，单位s</summary>
    public Int32 Uptime { get; set; }

    /// <summary>本地UTC时间。Unix毫秒（UTC）</summary>
    public Int64 Time { get; set; }

    /// <summary>延迟。请求到服务端并返回的延迟时间。单位ms</summary>
    public Int32 Delay { get; set; }
    #endregion
}
