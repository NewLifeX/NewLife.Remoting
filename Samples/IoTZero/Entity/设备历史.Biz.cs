using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Data;
using XCode;
using XCode.Cache;

namespace IoT.Data;

public partial class DeviceHistory : Entity<DeviceHistory>
{
    #region 对象操作
    static DeviceHistory()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(DeviceId));
        // 按天分表
        //Meta.ShardPolicy = new TimeShardPolicy(nameof(Id), Meta.Factory)
        //{
        //    TablePolicy = "{{0}}_{{1:yyyyMMdd}}",
        //    Step = TimeSpan.FromDays(1),
        //};

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
        Meta.Modules.Add<TraceModule>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        // 在新插入数据或者修改了指定字段时进行修正
        //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
    }
    #endregion

    #region 扩展属性
    /// <summary>设备</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Device Device => Extends.Get(nameof(Device), k => Device.FindById(DeviceId));

    /// <summary>设备</summary>
    [Map(nameof(DeviceId), typeof(Device), "Id")]
    public String DeviceName => Device?.Name;
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static DeviceHistory FindById(Int64 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据设备、编号查找</summary>
    /// <param name="deviceId">设备</param>
    /// <param name="id">编号</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceHistory> FindAllByDeviceIdAndId(Int32 deviceId, Int64 id)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeviceId == deviceId && e.Id == id);

        return FindAll(_.DeviceId == deviceId & _.Id == id);
    }

    /// <summary>根据设备查找</summary>
    /// <param name="deviceId">设备</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceHistory> FindAllByDeviceId(Int32 deviceId)
    {
        if (deviceId <= 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeviceId == deviceId);

        return FindAll(_.DeviceId == deviceId);
    }

    /// <summary>根据设备、操作查找</summary>
    /// <param name="deviceId">设备</param>
    /// <param name="action">操作</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceHistory> FindAllByDeviceIdAndAction(Int32 deviceId, String action)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeviceId == deviceId && e.Action.EqualIgnoreCase(action));

        return FindAll(_.DeviceId == deviceId & _.Action == action);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deviceId">设备</param>
    /// <param name="action">操作</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceHistory> Search(Int32 deviceId, String action, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deviceId >= 0) exp &= _.DeviceId == deviceId;
        if (!action.IsNullOrEmpty()) exp &= _.Action == action;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Action.Contains(key) | _.TraceId.Contains(key) | _.Creator.Contains(key) | _.CreateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Category From DeviceHistory Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<DeviceHistory> _CategoryCache = new FieldCache<DeviceHistory>(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>删除指定日期之前的数据</summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime date) => Delete(_.Id < Meta.Factory.Snow.GetId(date));

    /// <summary>创建日志</summary>
    /// <param name="device"></param>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="remark"></param>
    /// <param name="creator"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public static DeviceHistory Create(Device device, String action, Boolean success, String remark, String creator, String ip)
    {
        device ??= new Device();

        if (creator.IsNullOrEmpty()) creator = Environment.MachineName;
        var history = new DeviceHistory
        {
            DeviceId = device.Id,
            Name = device.Name,
            Action = action,
            Success = success,

            Remark = remark,

            //TraceId = traceId,
            Creator = creator,
            CreateTime = DateTime.Now,
            CreateIP = ip,
        };

        //history.SaveAsync();

        return history;
    }

    public static void WriteHistory(Device device, String action, Boolean success, String remark, String? ip)
    {
        var history = Create(device, action, success, remark, null, ip);
        history.SaveAsync();
    }

    private static readonly Lazy<FieldCache<DeviceHistory>> NameCache = new(() => new FieldCache<DeviceHistory>(__.Action));
    /// <summary>获取所有分类名称</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllAction() => NameCache.Value.FindAllName();
    #endregion
}