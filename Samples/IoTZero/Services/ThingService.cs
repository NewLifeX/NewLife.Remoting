using IoT.Data;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using EventModel = NewLife.Remoting.Models.EventModel;

namespace IoTZero.Services;

/// <summary>物模型服务</summary>
public class ThingService
{
    private readonly DataService _dataService;
    private readonly QueueService _queueService;
    private readonly IDeviceService _deviceService;
    private readonly ICacheProvider _cacheProvider;
    private readonly ITokenSetting _setting;
    private readonly ITracer _tracer;
    static Snowflake _snowflake = new();

    /// <summary>
    /// 实例化物模型服务
    /// </summary>
    /// <param name="dataService"></param>
    /// <param name="queueService"></param>
    /// <param name="deviceService"></param>
    /// <param name="cacheProvider"></param>
    /// <param name="setting"></param>
    /// <param name="tracer"></param>
    public ThingService(DataService dataService, QueueService queueService, IDeviceService deviceService, ICacheProvider cacheProvider, ITokenSetting setting, ITracer tracer)
    {
        _dataService = dataService;
        _queueService = queueService;
        _deviceService = deviceService;
        _cacheProvider = cacheProvider;
        _setting = setting;
        _tracer = tracer;
    }

    #region 数据存储
    /// <summary>上报数据</summary>
    /// <param name="device"></param>
    /// <param name="model"></param>
    /// <param name="kind"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public Int32 PostData(Device device, DataModels model, String kind, String ip)
    {
        var rs = 0;
        foreach (var item in model.Items)
        {
            var property = BuildDataPoint(device, item.Name, item.Value, item.Time, ip);
            if (property != null)
            {
                UpdateProperty(property);

                SaveHistory(device, property, item.Time, kind, ip);

                rs++;
            }
        }

        // 自动上线
        if (device != null && _deviceService is MyDeviceService ds) ds.SetDeviceOnline(device, ip, kind);

        //todo 触发指定设备的联动策略

        return rs;
    }

    /// <summary>设备属性上报</summary>
    /// <param name="device">设备</param>
    /// <param name="name">属性名</param>
    /// <param name="value">数值</param>
    /// <param name="timestamp">时间戳</param>
    /// <param name="ip">IP地址</param>
    /// <returns></returns>
    public DeviceProperty BuildDataPoint(Device device, String name, Object value, Int64 timestamp, String ip)
    {
        using var span = _tracer?.NewSpan(nameof(BuildDataPoint), $"{device.Id}-{name}-{value}");

        var entity = GetProperty(device, name);
        if (entity == null)
        {
            var key = $"{device.Id}###{name}";
            entity = DeviceProperty.GetOrAdd(key,
                k => DeviceProperty.FindByDeviceIdAndName(device.Id, name),
                k => new DeviceProperty
                {
                    DeviceId = device.Id,
                    Name = name,
                    NickName = name,
                    Enable = true,

                    CreateTime = DateTime.Now,
                    CreateIP = ip
                });
        }

        // 检查是否锁定
        if (!entity.Enable)
        {
            _tracer?.NewError($"{nameof(BuildDataPoint)}-NotEnable", new { name, entity.Enable });
            return null;
        }

        //todo 检查数据是否越界

        //todo 修正数字精度，小数点位数

        entity.Name = name;
        entity.Value = value?.ToString();

        var now = DateTime.Now;
        entity.TraceId = DefaultSpan.Current?.TraceId;
        entity.UpdateTime = now;
        entity.UpdateIP = ip;

        return entity;
    }

    /// <summary>更新属性</summary>
    /// <param name="property"></param>
    /// <returns></returns>
    public Boolean UpdateProperty(DeviceProperty property)
    {
        if (property == null) return false;

        //todo 如果短时间内数据没有变化（无脏数据），则不需要保存属性
        //var hasDirty = (property as IEntity).Dirtys[nameof(property.Value)];

        // 新属性直接更新，其它异步更新
        if (property.Id == 0)
            property.Insert();
        else
            property.SaveAsync();

        return true;
    }

    /// <summary>保存历史数据，写入属性表、数据表、分段数据表</summary>
    /// <param name="device"></param>
    /// <param name="property"></param>
    /// <param name="timestamp"></param>
    /// <param name="kind"></param>
    /// <param name="ip"></param>
    public void SaveHistory(Device device, DeviceProperty property, Int64 timestamp, String kind, String ip)
    {
        using var span = _tracer?.NewSpan("thing:SaveHistory", new { deviceName = device.Name, property.Name, property.Value, property.Type });
        try
        {
            // 记录数据流水，使用经过处理的属性数值字段
            var id = 0L;
            var data = _dataService.AddData(property.DeviceId, property.Id, timestamp, property.Name, property.Value, kind, ip);
            if (data != null) id = data.Id;

            //todo 存储分段数据

            //todo 推送队列
        }
        catch (Exception ex)
        {
            span?.SetError(ex, property);

            throw;
        }
    }

    /// <summary>获取设备属性对象，长时间缓存，便于加速属性保存</summary>
    /// <param name="device"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    private DeviceProperty GetProperty(Device device, String name)
    {
        var key = $"DeviceProperty:{device.Id}:{name}";
        if (_cacheProvider.InnerCache.TryGetValue<DeviceProperty>(key, out var property)) return property;

        using var span = _tracer?.NewSpan(nameof(GetProperty), $"{device.Id}-{name}");

        //var entity = device.Properties.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        var entity = DeviceProperty.FindByDeviceIdAndName(device.Id, name);
        if (entity != null)
            _cacheProvider.InnerCache.Set(key, entity, 600);

        return entity;
    }
    #endregion

    #region 属性功能
    /// <summary>获取设备属性</summary>
    /// <param name="device">设备</param>
    /// <param name="names">属性名集合</param>
    /// <returns></returns>
    public PropertyModel[] GetProperty(Device device, String[] names)
    {
        var list = new List<PropertyModel>();
        foreach (var item in device.Properties)
        {
            // 转换得到的属性是只读，不会返回到设备端，可以人为取消只读，此时返回设备端。
            if (item.Enable && (names == null || names.Length == 0 || item.Name.EqualIgnoreCase(names)))
            {
                list.Add(new PropertyModel { Name = item.Name, Value = item.Value });

                item.SaveAsync();
            }
        }

        return list.ToArray();
    }

    /// <summary>查询设备属性。应用端调用</summary>
    /// <param name="device">设备编码</param>
    /// <param name="names">属性名集合</param>
    /// <returns></returns>
    public PropertyModel[] QueryProperty(Device device, String[] names)
    {
        var list = new List<PropertyModel>();
        foreach (var item in device.Properties)
        {
            // 如果未指定属性名，则返回全部
            if (item.Enable && (names == null || names.Length == 0 || item.Name.EqualIgnoreCase(names)))
                list.Add(new PropertyModel { Name = item.Name, Value = item.Value });
        }

        return list.ToArray();
    }
    #endregion

    #region 事件
    /// <summary>设备事件上报</summary>
    /// <param name="device"></param>
    /// <param name="events"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public Int32 PostEvent(Device device, EventModel[] events, String ip) => throw new NotImplementedException();

    /// <summary>设备事件上报</summary>
    /// <param name="device"></param>
    /// <param name="event"></param>
    /// <param name="ip"></param>
    public void PostEvent(Device device, EventModel @event, String ip) => throw new NotImplementedException();
    #endregion

    #region 服务调用
    /// <summary>调用服务</summary>
    /// <param name="device"></param>
    /// <param name="command"></param>
    /// <param name="argument"></param>
    /// <param name="expire"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public ServiceModel InvokeService(Device device, String command, String argument, DateTime expire)
    {
        var traceId = DefaultSpan.Current?.TraceId;

        var log = new ServiceModel
        {
            Id = Rand.Next(),
            Name = command,
            InputData = argument,
            Expire = expire,
            TraceId = traceId,
        };

        return log;
    }

    ///// <summary>服务响应</summary>
    ///// <param name="device"></param>
    ///// <param name="model"></param>
    ///// <returns></returns>
    ///// <exception cref="InvalidOperationException"></exception>
    //public DeviceServiceLog ServiceReply(Device device, ServiceReplyModel model) => throw new NotImplementedException();

    /// <summary>异步调用服务，并等待响应</summary>
    /// <param name="device"></param>
    /// <param name="command"></param>
    /// <param name="argument"></param>
    /// <param name="expire"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public async Task<ServiceReplyModel> InvokeServiceAsync(Device device, String command, String argument, DateTime expire, Int32 timeout)
    {
        var model = InvokeService(device, command, argument, expire);

        _queueService.Publish(device.Code, model);

        var reply = new ServiceReplyModel { Id = model.Id };

        // 挂起等待。借助redis队列，等待响应
        if (timeout > 1000)
        {
            await Task.Delay(1000);

            throw new NotImplementedException();
        }

        return reply;
    }
    #endregion
}