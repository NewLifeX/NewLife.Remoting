using IoT.Data;
using NewLife.IoT.ThingModels;
using NewLife.Log;

namespace IoTZero.Services;

/// <summary>数据服务</summary>
/// <param name="tracer"></param>
public class DataService(ITracer tracer)
{
    #region 方法
    /// <summary>
    /// 插入设备原始数据，异步批量操作
    /// </summary>
    /// <param name="deviceId">设备</param>
    /// <param name="sensorId">传感器</param>
    /// <param name="time"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <param name="kind"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public DeviceData AddData(Int32 deviceId, Int32 sensorId, Int64 time, String name, String value, String kind, String ip)
    {
        if (value.IsNullOrEmpty()) return null;

        using var span = tracer?.NewSpan("thing:AddData", new { deviceId, time, name, value });

        /*
         * 使用采集时间来生成雪花Id，数据存储序列即业务时间顺序。
         * 在历史数据查询和统计分析时，一马平川，再也不必考虑边界溢出问题。
         * 数据延迟上传可能会导致插入历史数据，从而影响蚂蚁实时计算，可通过补偿定时批计算修正。
         * 实际应用中，更多通过消息队列来驱动实时计算。
         */

        // 取客户端采集时间，较大时间差时取本地时间
        var t = time.ToDateTime().ToLocalTime();
        if (t.Year < 2000 || t.AddDays(1) < DateTime.Now) t = DateTime.Now;

        var snow = DeviceData.Meta.Factory.Snow;

        var traceId = DefaultSpan.Current?.TraceId;

        var entity = new DeviceData
        {
            Id = snow.NewId(t, sensorId),
            DeviceId = deviceId,
            Name = name,
            Value = value,
            Kind = kind,

            Timestamp = time,
            TraceId = traceId,
            Creator = Environment.MachineName,
            CreateTime = DateTime.Now,
            CreateIP = ip,
        };

        var rs = entity.SaveAsync() ? 1 : 0;

        return entity;
    }

    /// <summary>添加事件</summary>
    /// <param name="deviceId"></param>
    /// <param name="model"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public void AddEvent(Int32 deviceId, EventModel model, String ip) => throw new NotImplementedException();
    #endregion
}