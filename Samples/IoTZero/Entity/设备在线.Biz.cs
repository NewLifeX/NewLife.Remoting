using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Data;
using NewLife.IoT.Models;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using XCode;

namespace IoT.Data;

public partial class DeviceOnline : Entity<DeviceOnline>, IOnlineModel2
{
    #region 对象操作
    static DeviceOnline()
    {
        var df = Meta.Factory.AdditionalFields;
        df.Add(__.Pings);

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();

        var sc = Meta.SingleCache;
        sc.FindSlaveKeyMethod = k => Find(_.SessionId == k);
        sc.GetSlaveKeyMethod = e => e.SessionId;
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
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;

        // 检查唯一索引
        // CheckExist(isNew, nameof(SessionId));
    }
    #endregion

    #region 扩展属性
    /// <summary>产品</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Product Product => Extends.Get(nameof(Product), k => Product.FindById(ProductId));

    /// <summary>产品</summary>
    [Map(nameof(ProductId), typeof(Product), "Id")]
    public String ProductName => Product?.Name;
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
    public static DeviceOnline FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据会话查找</summary>
    /// <param name="sessionId">会话</param>
    /// <returns>实体对象</returns>
    public static DeviceOnline FindBySessionId(String sessionId, Boolean cache = true)
    {
        if (sessionId.IsNullOrEmpty()) return null;

        if (!cache) return Find(_.SessionId == sessionId);

        return Meta.SingleCache.GetItemWithSlaveKey(sessionId) as DeviceOnline;
    }

    /// <summary>根据产品查找</summary>
    /// <param name="productId">产品</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceOnline> FindAllByProductId(Int32 productId)
    {
        if (productId <= 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProductId == productId);

        return FindAll(_.ProductId == productId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="sessionId">会话</param>
    /// <param name="productId">产品</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceOnline> Search(String sessionId, Int32 productId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!sessionId.IsNullOrEmpty()) exp &= _.SessionId == sessionId;
        if (productId >= 0) exp &= _.ProductId == productId;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.SessionId.Contains(key) | _.Name.Contains(key) | _.IP.Contains(key) | _.GroupPath.Contains(key) | _.Token.Contains(key) | _.Creator.Contains(key) | _.CreateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Category From DeviceOnline Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<DeviceOnline> _CategoryCache = new FieldCache<DeviceOnline>(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>根据编码查询或添加</summary>
    /// <param name="sessionid"></param>
    /// <returns></returns>
    public static DeviceOnline GetOrAdd(String sessionid) => GetOrAdd(sessionid, FindBySessionId, k => new DeviceOnline { SessionId = k });

    /// <summary>删除过期，指定过期时间</summary>
    /// <param name="expire">超时时间，秒</param>
    /// <returns></returns>
    public static IList<DeviceOnline> ClearExpire(TimeSpan expire)
    {
        if (Meta.Count == 0) return null;

        // 10分钟不活跃将会被删除
        var exp = _.UpdateTime < DateTime.Now.Subtract(expire);
        var list = FindAll(exp, null, null, 0, 0);
        list.Delete();

        return list;
    }

    ///// <summary>更新并保存在线状态</summary>
    ///// <param name="di"></param>
    ///// <param name="pi"></param>
    ///// <param name="token"></param>
    //public void Save(LoginInfo di, PingInfo pi, String token)
    //{
    //    var olt = this;

    //    // di不等于空，登录时调用；
    //    // pi不为空，客户端发ping消息是调用；
    //    // 两个都是空，收到mqtt协议ping报文时调用
    //    if (di != null)
    //    {
    //        olt.Fill(di);
    //        olt.LocalTime = di.Time.ToDateTime().ToLocalTime();
    //    }
    //    else if (pi != null)
    //    {
    //        olt.Fill(pi);
    //    }

    //    olt.Token = token;
    //    olt.Pings++;

    //    // 5秒内直接保存
    //    if (olt.CreateTime.AddSeconds(5) > DateTime.Now)
    //        olt.Save();
    //    else
    //        olt.SaveAsync();
    //}

    ///// <summary>填充节点信息</summary>
    ///// <param name="di"></param>
    //public void Fill(LoginInfo di)
    //{
    //    var online = this;

    //    online.LocalTime = di.Time.ToDateTime().ToLocalTime();
    //    online.IP = di.IP;
    //}

    /// <summary>填充在线节点信息</summary>
    /// <param name="inf"></param>
    private void Fill(PingInfo inf)
    {
        var olt = this;

        if (inf.Delay > 0) olt.Delay = inf.Delay;

        var dt = inf.Time.ToDateTime().ToLocalTime();
        if (dt.Year > 2000)
        {
            olt.LocalTime = dt;
            olt.Offset = (Int32)Math.Round((dt - DateTime.Now).TotalSeconds);
        }

        if (!inf.IP.IsNullOrEmpty()) olt.IP = inf.IP;
        olt.Remark = inf.ToJson();
    }

    public Int32 Save(IPingRequest request, Object context)
    {
        if (context is DeviceContext ctx)
        {
            Token = ctx.Token;

            if (ctx.Device is Device device)
            {
                Name = device.Name;
                GroupPath = device.GroupPath;
                ProductId = device.ProductId;
            }

            if (request is PingInfo ping) Fill(ping);
        }

        Pings++;

        //SaveAsync();
        return Update();
    }
    #endregion
}
