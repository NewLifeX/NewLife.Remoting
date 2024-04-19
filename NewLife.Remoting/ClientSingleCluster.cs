using NewLife.Log;
using NewLife.Net;

namespace NewLife.Remoting;

/// <summary>客户端单连接故障转移集群</summary>
public class ClientSingleCluster<T> : ICluster<String, T>
{
    /// <summary>最后使用资源</summary>
    public KeyValuePair<String, T> Current { get; private set; }

    /// <summary>服务器地址列表</summary>
    public Func<IEnumerable<String>>? GetItems { get; set; }

    /// <summary>创建回调</summary>
    public Func<String, T>? OnCreate { get; set; }

    /// <summary>打开</summary>
    public virtual Boolean Open() => true;

    /// <summary>关闭</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Close(String reason)
    {
        if (_Client == null) return false;

        if (_Client is ISocketClient client) return client.Close(reason);

        _Client.TryDispose();

        return true;
    }

    private T? _Client;
    /// <summary>从集群中获取资源</summary>
    /// <returns></returns>
    public virtual T Get()
    {
        var tc = _Client;
        if (tc != null && (tc is not DisposeBase ds || !ds.Disposed)) return tc;
        lock (this)
        {
            tc = _Client;
            if (tc != null && (tc is not DisposeBase ds2 || !ds2.Disposed)) return tc;

            // 释放旧对象
            tc.TryDispose();

            return _Client = CreateClient();
        }
    }

    /// <summary>归还</summary>
    /// <param name="value"></param>
    public virtual Boolean Put(T value) => true;

    /// <summary>Round-Robin 负载均衡</summary>
    private Int32 _index = -1;

    /// <summary>为连接池创建连接</summary>
    /// <returns></returns>
    protected virtual T CreateClient()
    {
        if (GetItems == null) throw new ArgumentNullException(nameof(GetItems));
        if (OnCreate == null) throw new ArgumentNullException(nameof(OnCreate));

        // 遍历所有服务，找到可用服务端
        var svrs = GetItems().ToArray();
        if (svrs == null || svrs.Length == 0) throw new InvalidOperationException("没有设置服务端地址Servers");

        var idx = Interlocked.Increment(ref _index);
        Exception? last = null;
        for (var i = 0; i < svrs.Length; i++)
        {
            // Round-Robin 负载均衡
            var k = (idx + i) % svrs.Length;
            var svr = svrs[k];
            try
            {
                WriteLog("集群转移：{0}", svr);

                var client = OnCreate(svr);
                //client.Open();

                // 设置当前资源
                Current = new KeyValuePair<String, T>(svr, client);

                return client;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw last ?? new NullReferenceException();
    }

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}