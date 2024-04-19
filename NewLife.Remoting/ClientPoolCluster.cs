using NewLife.Collections;
using NewLife.Log;
using NewLife.Net;

namespace NewLife.Remoting;

/// <summary>客户端连接池负载均衡集群</summary>
public class ClientPoolCluster<T> : ICluster<String, T>
{
    /// <summary>最后使用资源</summary>
    public KeyValuePair<String, T> Current { get; private set; }

    /// <summary>服务器地址列表</summary>
    public Func<IEnumerable<String>>? GetItems { get; set; }

    /// <summary>创建回调</summary>
    public Func<String, T>? OnCreate { get; set; }

    /// <summary>连接池</summary>
    public IPool<T> Pool { get; private set; }

    /// <summary>实例化连接池集群</summary>
    public ClientPoolCluster() => Pool = new MyPool(this);

    /// <summary>打开</summary>
    public virtual Boolean Open() => true;

    /// <summary>关闭</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Close(String reason) => Pool.Clear() > 0;

    /// <summary>从集群中获取资源</summary>
    /// <returns></returns>
    public virtual T Get() => Pool.Get();

    /// <summary>归还</summary>
    /// <param name="value"></param>
    public virtual Boolean Put(T value)
    {
        if (value == null) return false;

        return Pool.Put(value);
    }

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
                WriteLog("集群均衡：{0}", svr);

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

    class MyPool : ObjectPool<T>
    {
        public ClientPoolCluster<T> Host { get; set; }

        public MyPool(ClientPoolCluster<T> cluster)
        {
            // 最小值为0，连接池不再使用栈，只使用队列
            Min = 0;
            Max = 100_000;

            Host = cluster;
        }

        protected override T OnCreate() => Host.CreateClient();

        /// <summary>释放时，返回是否有效。无效对象将会被抛弃</summary>
        /// <param name="value"></param>
        protected override Boolean OnPut(T value) => value != null && (value is not IDisposable2 ds || !ds.Disposed);
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