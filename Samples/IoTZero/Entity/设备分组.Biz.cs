using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace IoT.Data;

public partial class DeviceGroup : Entity<DeviceGroup>
{
    #region 对象操作
    static DeviceGroup()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(ParentId));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        if (Name.IsNullOrEmpty()) throw new ApiException(500, "名称不能为空");
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        if (Meta.Session.Count > 0) return;

        if (XTrace.Debug) XTrace.WriteLine("开始初始化DeviceGroup[设备分组]数据……");

        var entity = new DeviceGroup
        {
            Name = "默认分组",
            ParentId = 0,
            Devices = 0,
            Activations = 0,
            Onlines = 0,
        };
        entity.Insert();

        if (XTrace.Debug) XTrace.WriteLine("完成初始化DeviceGroup[设备分组]数据！");
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static DeviceGroup FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据父级、名称查找</summary>
    /// <param name="parentId">父级</param>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static DeviceGroup FindByParentIdAndName(Int32 parentId, String name)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ParentId == parentId && e.Name.EqualIgnoreCase(name));

        return Find(_.ParentId == parentId & _.Name == name);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceGroup> FindAllByName(String name)
    {
        if (name.IsNullOrEmpty()) return new List<DeviceGroup>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Name.EqualIgnoreCase(name));

        return FindAll(_.Name == name);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="name">名称</param>
    /// <param name="parentId">父级</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceGroup> Search(String name, Int32 parentId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!name.IsNullOrEmpty()) exp &= _.Name == name;
        if (parentId >= 0) exp &= _.ParentId == parentId;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Category From DeviceGroup Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<DeviceGroup> _CategoryCache = new FieldCache<DeviceGroup>(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作

    public static Int32 Refresh()
    {
        var count = 0;
        var groups = FindAll();
        var list = Device.SearchGroupByGroup();
        foreach (var item in list)
        {
            var gb = groups.FirstOrDefault(e => e.Id == item.GroupId);
            if (gb != null)
            {
                gb.Devices = item.Id;
                gb.Activations = item["Activations"].ToInt();
                gb.Onlines = item["Onlines"].ToInt();
                count += gb.Update();
            }
        }

        return count;
    }
    #endregion
}
