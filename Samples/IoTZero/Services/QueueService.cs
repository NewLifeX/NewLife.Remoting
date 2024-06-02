using NewLife.Caching;
using NewLife.Caching.Queues;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Serialization;

namespace IoTZero.Services;

/// <summary>队列服务</summary>
public class QueueService
{
    #region 属性
    private readonly ICacheProvider _cacheProvider;
    private readonly ITracer _tracer;
    #endregion

    #region 构造
    /// <summary>
    /// 实例化队列服务
    /// </summary>
    public QueueService(ICacheProvider cacheProvider, ITracer tracer)
    {
        _cacheProvider = cacheProvider;
        _tracer = tracer;
    }
    #endregion

    #region 命令队列
    /// <summary>
    /// 获取指定设备的命令队列
    /// </summary>
    /// <param name="deviceCode"></param>
    /// <returns></returns>
    public IProducerConsumer<String> GetQueue(String deviceCode)
    {
        var q = _cacheProvider.GetQueue<String>($"cmd:{deviceCode}");
        if (q is QueueBase qb) qb.TraceName = "ServiceQueue";

        return q;
    }

    /// <summary>
    /// 向指定设备发送命令
    /// </summary>
    /// <param name="deviceCode"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    public Int32 Publish(String deviceCode, ServiceModel model)
    {
        using var span = _tracer?.NewSpan(nameof(Publish), $"{deviceCode} {model.ToJson()}");

        var q = GetQueue(deviceCode);
        return q.Add(model.ToJson());
    }

    /// <summary>
    /// 获取指定设备的服务响应队列
    /// </summary>
    /// <param name="serviceLogId"></param>
    /// <returns></returns>
    public IProducerConsumer<String> GetReplyQueue(Int64 serviceLogId) => throw new NotImplementedException();

    /// <summary>
    /// 发送消息到服务响应队列
    /// </summary>
    /// <param name="model"></param>
    public void PublishReply(ServiceReplyModel model) => throw new NotImplementedException();
    #endregion
}