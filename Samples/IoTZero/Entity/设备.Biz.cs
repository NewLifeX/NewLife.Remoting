using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Common;
using NewLife.Data;
using NewLife.IoT.Models;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using XCode;
using XCode.Cache;

namespace IoT.Data;

public partial class Device : Entity<Device>, IDeviceModel2, ILogProvider
{
    #region 对象操作
    static Device()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        var df = Meta.Factory.AdditionalFields;
        df.Add(nameof(Logins));
        df.Add(nameof(OnlineTime));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();

        var sc = Meta.SingleCache;
        sc.Expire = 20 * 60;
        sc.MaxEntity = 200_000;
        sc.FindSlaveKeyMethod = k => Find(_.Code == k);
        sc.GetSlaveKeyMethod = e => e.Code;
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        if (ProductId <= 0) throw new ApiException(ApiCode.BadRequest, "产品Id错误");

        var product = Product.FindById(ProductId) ?? throw new ApiException(ApiCode.NotFound, "产品Id错误");

        var len = _.IP.Length;
        if (len > 0 && !IP.IsNullOrEmpty() && IP.Length > len) IP = IP[..len];

        len = _.Uuid.Length;
        if (len > 0 && !Uuid.IsNullOrEmpty() && Uuid.Length > len) Uuid = Uuid[..len];

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        // 自动编码
        if (Code.IsNullOrEmpty()) Code = PinYin.GetFirst(Name);
        CheckExist(nameof(Code));

        if (Period <= 0) Period = 60;
        if (PollingTime == 0) PollingTime = 1000;
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        if (Meta.Session.Count > 0) return;

        if (XTrace.Debug) XTrace.WriteLine("开始初始化Device[设备]数据……");

        var entity = new Device
        {
            Name = "测试设备",
            Code = "abc",
            Secret = "abc",
            ProductId = 1,
            GroupId = 1,
            Enable = true,
        };
        entity.Insert();

        if (XTrace.Debug) XTrace.WriteLine("完成初始化Device[设备]数据！");
    }
    #endregion

    #region 扩展属性
    /// <summary>产品</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Product Product => Extends.Get(nameof(Product), k => Product.FindById(ProductId));

    /// <summary>产品</summary>
    [Map(nameof(ProductId), typeof(Product), "Id")]
    public String ProductName => Product?.Name;

    /// <summary>设备属性。借助扩展属性缓存</summary>
    [XmlIgnore, IgnoreDataMember]
    public IList<DeviceProperty> Properties => Extends.Get(nameof(Properties), k => DeviceProperty.FindAllByDeviceId(Id));
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Device FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据编码查找</summary>
    /// <param name="code">编码</param>
    /// <returns>实体对象</returns>
    public static Device FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Code.EqualIgnoreCase(code));

        //return Find(_.Code == code);
        return Meta.SingleCache.GetItemWithSlaveKey(code) as Device;
    }

    /// <summary>根据产品查找</summary>
    /// <param name="productId">产品</param>
    /// <returns>实体列表</returns>
    public static IList<Device> FindAllByProductId(Int32 productId)
    {
        if (productId <= 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProductId == productId);

        return FindAll(_.ProductId == productId);
    }

    /// <summary>根据唯一标识查找</summary>
    /// <param name="uuid">唯一标识</param>
    /// <returns>实体列表</returns>
    public static IList<Device> FindAllByUuid(String uuid)
    {
        if (uuid.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Uuid.EqualIgnoreCase(uuid));

        return FindAll(_.Uuid == uuid);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="productId">产品</param>
    /// <param name="groupId"></param>
    /// <param name="enable"></param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Device> Search(Int32 productId, Int32 groupId, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (productId >= 0) exp &= _.ProductId == productId;
        if (groupId >= 0) exp &= _.GroupId == groupId;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Code.Contains(key) | _.Uuid.Contains(key) | _.Location.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Uuid From Device Where CreateTime>'2020-01-24 00:00:00' Group By Uuid Order By Id Desc limit 20
    static readonly FieldCache<Device> _UuidCache = new FieldCache<Device>(nameof(Uuid))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取唯一标识列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetUuidList() => _UuidCache.FindAllName();

    /// <summary>
    /// 根据设备分组来分组
    /// </summary>
    /// <returns></returns>
    public static IList<Device> SearchGroupByGroup()
    {
        var selects = _.Id.Count();
        selects &= _.Enable.SumCase(1, "Activations");
        selects &= _.Online.SumCase(1, "Onlines");
        selects &= _.GroupId;

        return FindAll(_.GroupId.GroupBy(), null, selects, 0, 0);
    }
    #endregion

    #region 业务操作

    /// <summary>登录并保存信息</summary>
    /// <param name="di"></param>
    /// <param name="ip"></param>
    public void Login(LoginInfo di, String ip)
    {
        var dv = this;

        if (di != null) dv.Fill(di);

        // 如果节点本地IP为空，而来源IP是局域网，则直接取用
        if (dv.IP.IsNullOrEmpty()) dv.IP = ip;

        dv.Online = true;
        dv.Logins++;
        dv.LastLogin = DateTime.Now;
        dv.LastLoginIP = ip;

        if (dv.CreateIP.IsNullOrEmpty()) dv.CreateIP = ip;
        dv.UpdateIP = ip;

        dv.Save();
    }

    /// <summary>设备上线</summary>
    /// <param name="ip"></param>
    /// <param name="reason"></param>
    public void SetOnline(String ip, String reason)
    {
        var dv = this;

        if (!dv.Online && dv.Enable)
        {
            dv.Online = true;
            dv.Update();

            if (!reason.IsNullOrEmpty())
                DeviceHistory.WriteHistory(dv, "上线", true, $"设备上线。{reason}", ip);
        }
    }

    public IOnlineModel CreateOnline(String sessionId)
    {
        var online = DeviceOnline.GetOrAdd(sessionId);
        online.ProductId = ProductId;
        online.DeviceId = Id;
        online.Name = Name;
        online.IP = IP;
        //online.CreateIP = context.UserHost;
        online.Creator = Environment.MachineName;

        return online;
    }

    /// <summary>
    /// 注销
    /// </summary>
    public void Logout()
    {
        Online = false;

        Update();
    }

    /// <summary>填充</summary>
    /// <param name="di"></param>
    public void Fill(LoginInfo di)
    {
        var dv = this;

        if (dv.Name.IsNullOrEmpty()) dv.Name = di.Name;
        if (!di.Version.IsNullOrEmpty()) dv.Version = di.Version;

        if (!di.IP.IsNullOrEmpty()) dv.IP = di.IP;
        if (!di.UUID.IsNullOrEmpty()) dv.Uuid = di.UUID;
    }

    /// <summary>创建设备历史</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    public IExtend CreateHistory(String action, Boolean success, String content) => DeviceHistory.Create(this, action, success, content, null, null);

    /// <summary>写历史日志</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="content"></param>
    public void WriteLog(String action, Boolean success, String content) => DeviceHistory.WriteHistory(this, action, success, content, null);

    #endregion
}
