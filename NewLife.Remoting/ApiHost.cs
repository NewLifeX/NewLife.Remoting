using System.Collections.Concurrent;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;

namespace NewLife.Remoting;

/// <summary>Api主机</summary>
public abstract class ApiHost : DisposeBase, IApiHost, IExtend, ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>编码器</summary>
    public IEncoder Encoder { get; set; } = null!;

    /// <summary>调用超时时间。请求发出后，等待响应的最大时间，默认15_000ms</summary>
    public Int32 Timeout { get; set; } = 15_000;

    /// <summary>慢追踪。远程调用或处理时间超过该值时，输出慢调用日志，默认5000ms</summary>
    public Int32 SlowTrace { get; set; } = 5_000;

    private ConcurrentDictionary<String, Object?>? _items;
    /// <summary>数据项</summary>
    public IDictionary<String, Object?> Items => _items ??= new();

    /// <summary>获取/设置 用户会话数据</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public virtual Object? this[String key] { get => _items != null && _items.TryGetValue(key, out var obj) ? obj : null; set => Items[key] = value; }

    /// <summary>启动时间</summary>
    public DateTime StartTime { get; set; } = DateTime.Now;
    #endregion

    #region 方法
    /// <summary>获取消息编解码器。重载以指定不同的封包协议</summary>
    /// <returns></returns>
    public virtual IMessageCodec GetMessageCodec() => new SrmpCodec();
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>编码器日志</summary>
    public ILog EncoderLog { get; set; } = Logger.Null;

    /// <summary>显示调用和处理错误。默认false</summary>
    public Boolean ShowError { get; set; }

    /// <summary>性能跟踪器</summary>
    public ITracer? Tracer { get; set; } = DefaultTracer.Instance;

    /// <summary>写日志。名称前缀不参与本地化，先按日志语言翻译并格式化消息，再拼接前缀</summary>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object?[] args) => Log?.InfoPrefixed("[" + Name + "]", format, args);

    /// <summary>已重载。返回具有本类特征的字符串</summary>
    /// <returns>String</returns>
    public override String ToString() => Name;
    #endregion
}