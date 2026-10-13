using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace AotSmoke.XCode.Data;

/// <summary>设备在线。用于验证 XCode 实体在 NativeAOT 下的运行期可用性</summary>
[Serializable]
[DataObject]
[Description("设备在线。用于验证 XCode 实体在 NativeAOT 下的运行期可用性")]
[BindIndex("IU_DeviceOnline_SessionId", true, "SessionId")]
[BindTable("DeviceOnline", Description = "设备在线。用于验证 XCode 实体在 NativeAOT 下的运行期可用性", ConnName = "AotSmokeXCode", DbType = DatabaseType.None)]
public partial class DeviceOnline
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _DeviceId;
    /// <summary>设备</summary>
    [DisplayName("设备")]
    [Description("设备")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DeviceId", "设备", "")]
    public Int32 DeviceId { get => _DeviceId; set { if (OnPropertyChanging("DeviceId", value)) { _DeviceId = value; OnPropertyChanged("DeviceId"); } } }

    private String _SessionId;
    /// <summary>会话标识</summary>
    [DisplayName("会话标识")]
    [Description("会话标识")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SessionId", "会话标识", "")]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private DateTime _LoginTime;
    /// <summary>登录时间</summary>
    [DisplayName("登录时间")]
    [Description("登录时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LoginTime", "登录时间", "")]
    public DateTime LoginTime { get => _LoginTime; set { if (OnPropertyChanging("LoginTime", value)) { _LoginTime = value; OnPropertyChanged("LoginTime"); } } }

    private DateTime _LastActive;
    /// <summary>最后活跃</summary>
    [DisplayName("最后活跃")]
    [Description("最后活跃")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastActive", "最后活跃", "")]
    public DateTime LastActive { get => _LastActive; set { if (OnPropertyChanging("LastActive", value)) { _LastActive = value; OnPropertyChanged("LastActive"); } } }

    private Boolean _LongLink;
    /// <summary>长连接</summary>
    [DisplayName("长连接")]
    [Description("长连接")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("LongLink", "长连接", "")]
    public Boolean LongLink { get => _LongLink; set { if (OnPropertyChanging("LongLink", value)) { _LongLink = value; OnPropertyChanged("LongLink"); } } }

    private Int32 _Pings;
    /// <summary>心跳次数</summary>
    [DisplayName("心跳次数")]
    [Description("心跳次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Pings", "心跳次数", "")]
    public Int32 Pings { get => _Pings; set { if (OnPropertyChanging("Pings", value)) { _Pings = value; OnPropertyChanged("Pings"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [Category("扩展")]
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "DeviceId" => _DeviceId,
            "SessionId" => _SessionId,
            "LoginTime" => _LoginTime,
            "LastActive" => _LastActive,
            "LongLink" => _LongLink,
            "Pings" => _Pings,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "DeviceId": _DeviceId = value.ToInt(); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "LoginTime": _LoginTime = value.ToDateTime(); break;
                case "LastActive": _LastActive = value.ToDateTime(); break;
                case "LongLink": _LongLink = value.ToBoolean(); break;
                case "Pings": _Pings = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static DeviceOnline FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据会话标识查找</summary>
    /// <param name="sessionId">会话标识</param>
    /// <returns>实体对象</returns>
    public static DeviceOnline FindBySessionId(String sessionId)
    {
        if (sessionId.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.SessionId.EqualIgnoreCase(sessionId));

        return Find(_.SessionId == sessionId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="sessionId">会话标识</param>
    /// <param name="longLink">长连接</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DeviceOnline> Search(String sessionId, Boolean? longLink, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!sessionId.IsNullOrEmpty()) exp &= _.SessionId == sessionId;
        if (longLink != null) exp &= _.LongLink == longLink;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设备在线字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>设备</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>会话标识</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>登录时间</summary>
        public static readonly Field LoginTime = FindByName("LoginTime");

        /// <summary>最后活跃</summary>
        public static readonly Field LastActive = FindByName("LastActive");

        /// <summary>长连接</summary>
        public static readonly Field LongLink = FindByName("LongLink");

        /// <summary>心跳次数</summary>
        public static readonly Field Pings = FindByName("Pings");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设备在线字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>设备</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>会话标识</summary>
        public const String SessionId = "SessionId";

        /// <summary>登录时间</summary>
        public const String LoginTime = "LoginTime";

        /// <summary>最后活跃</summary>
        public const String LastActive = "LastActive";

        /// <summary>长连接</summary>
        public const String LongLink = "LongLink";

        /// <summary>心跳次数</summary>
        public const String Pings = "Pings";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
