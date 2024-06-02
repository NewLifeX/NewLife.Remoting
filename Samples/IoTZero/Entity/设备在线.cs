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

namespace IoT.Data;

/// <summary>设备在线</summary>
[Serializable]
[DataObject]
[Description("设备在线")]
[BindIndex("IU_DeviceOnline_SessionId", true, "SessionId")]
[BindIndex("IX_DeviceOnline_ProductId", false, "ProductId")]
[BindIndex("IX_DeviceOnline_UpdateTime", false, "UpdateTime")]
[BindTable("DeviceOnline", Description = "设备在线", ConnName = "IoT", DbType = DatabaseType.None)]
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

    private String _SessionId;
    /// <summary>会话</summary>
    [DisplayName("会话")]
    [Description("会话")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SessionId", "会话", "")]
    public String SessionId { get => _SessionId; set { if (OnPropertyChanging("SessionId", value)) { _SessionId = value; OnPropertyChanged("SessionId"); } } }

    private Int32 _ProductId;
    /// <summary>产品</summary>
    [DisplayName("产品")]
    [Description("产品")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProductId", "产品", "")]
    public Int32 ProductId { get => _ProductId; set { if (OnPropertyChanging("ProductId", value)) { _ProductId = value; OnPropertyChanged("ProductId"); } } }

    private Int32 _DeviceId;
    /// <summary>设备</summary>
    [DisplayName("设备")]
    [Description("设备")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DeviceId", "设备", "")]
    public Int32 DeviceId { get => _DeviceId; set { if (OnPropertyChanging("DeviceId", value)) { _DeviceId = value; OnPropertyChanged("DeviceId"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _IP;
    /// <summary>本地IP</summary>
    [DisplayName("本地IP")]
    [Description("本地IP")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("IP", "本地IP", "")]
    public String IP { get => _IP; set { if (OnPropertyChanging("IP", value)) { _IP = value; OnPropertyChanged("IP"); } } }

    private String _GroupPath;
    /// <summary>分组</summary>
    [DisplayName("分组")]
    [Description("分组")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("GroupPath", "分组", "")]
    public String GroupPath { get => _GroupPath; set { if (OnPropertyChanging("GroupPath", value)) { _GroupPath = value; OnPropertyChanged("GroupPath"); } } }

    private Int32 _Pings;
    /// <summary>心跳</summary>
    [DisplayName("心跳")]
    [Description("心跳")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Pings", "心跳", "")]
    public Int32 Pings { get => _Pings; set { if (OnPropertyChanging("Pings", value)) { _Pings = value; OnPropertyChanged("Pings"); } } }

    private Int32 _Delay;
    /// <summary>延迟。网络延迟，单位ms</summary>
    [DisplayName("延迟")]
    [Description("延迟。网络延迟，单位ms")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Delay", "延迟。网络延迟，单位ms", "")]
    public Int32 Delay { get => _Delay; set { if (OnPropertyChanging("Delay", value)) { _Delay = value; OnPropertyChanged("Delay"); } } }

    private Int32 _Offset;
    /// <summary>偏移。客户端时间减服务端时间，单位s</summary>
    [DisplayName("偏移")]
    [Description("偏移。客户端时间减服务端时间，单位s")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Offset", "偏移。客户端时间减服务端时间，单位s", "")]
    public Int32 Offset { get => _Offset; set { if (OnPropertyChanging("Offset", value)) { _Offset = value; OnPropertyChanged("Offset"); } } }

    private DateTime _LocalTime;
    /// <summary>本地时间</summary>
    [DisplayName("本地时间")]
    [Description("本地时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LocalTime", "本地时间", "")]
    public DateTime LocalTime { get => _LocalTime; set { if (OnPropertyChanging("LocalTime", value)) { _LocalTime = value; OnPropertyChanged("LocalTime"); } } }

    private String _Token;
    /// <summary>令牌</summary>
    [DisplayName("令牌")]
    [Description("令牌")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Token", "令牌", "")]
    public String Token { get => _Token; set { if (OnPropertyChanging("Token", value)) { _Token = value; OnPropertyChanged("Token"); } } }

    private String _Creator;
    /// <summary>创建者。服务端设备</summary>
    [DisplayName("创建者")]
    [Description("创建者。服务端设备")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Creator", "创建者。服务端设备", "")]
    public String Creator { get => _Creator; set { if (OnPropertyChanging("Creator", value)) { _Creator = value; OnPropertyChanged("Creator"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
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
            "SessionId" => _SessionId,
            "ProductId" => _ProductId,
            "DeviceId" => _DeviceId,
            "Name" => _Name,
            "IP" => _IP,
            "GroupPath" => _GroupPath,
            "Pings" => _Pings,
            "Delay" => _Delay,
            "Offset" => _Offset,
            "LocalTime" => _LocalTime,
            "Token" => _Token,
            "Creator" => _Creator,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateTime" => _UpdateTime,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "SessionId": _SessionId = Convert.ToString(value); break;
                case "ProductId": _ProductId = value.ToInt(); break;
                case "DeviceId": _DeviceId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "IP": _IP = Convert.ToString(value); break;
                case "GroupPath": _GroupPath = Convert.ToString(value); break;
                case "Pings": _Pings = value.ToInt(); break;
                case "Delay": _Delay = value.ToInt(); break;
                case "Offset": _Offset = value.ToInt(); break;
                case "LocalTime": _LocalTime = value.ToDateTime(); break;
                case "Token": _Token = Convert.ToString(value); break;
                case "Creator": _Creator = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 字段名
    /// <summary>取得设备在线字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>会话</summary>
        public static readonly Field SessionId = FindByName("SessionId");

        /// <summary>产品</summary>
        public static readonly Field ProductId = FindByName("ProductId");

        /// <summary>设备</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>本地IP</summary>
        public static readonly Field IP = FindByName("IP");

        /// <summary>分组</summary>
        public static readonly Field GroupPath = FindByName("GroupPath");

        /// <summary>心跳</summary>
        public static readonly Field Pings = FindByName("Pings");

        /// <summary>延迟。网络延迟，单位ms</summary>
        public static readonly Field Delay = FindByName("Delay");

        /// <summary>偏移。客户端时间减服务端时间，单位s</summary>
        public static readonly Field Offset = FindByName("Offset");

        /// <summary>本地时间</summary>
        public static readonly Field LocalTime = FindByName("LocalTime");

        /// <summary>令牌</summary>
        public static readonly Field Token = FindByName("Token");

        /// <summary>创建者。服务端设备</summary>
        public static readonly Field Creator = FindByName("Creator");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设备在线字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>会话</summary>
        public const String SessionId = "SessionId";

        /// <summary>产品</summary>
        public const String ProductId = "ProductId";

        /// <summary>设备</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>本地IP</summary>
        public const String IP = "IP";

        /// <summary>分组</summary>
        public const String GroupPath = "GroupPath";

        /// <summary>心跳</summary>
        public const String Pings = "Pings";

        /// <summary>延迟。网络延迟，单位ms</summary>
        public const String Delay = "Delay";

        /// <summary>偏移。客户端时间减服务端时间，单位s</summary>
        public const String Offset = "Offset";

        /// <summary>本地时间</summary>
        public const String LocalTime = "LocalTime";

        /// <summary>令牌</summary>
        public const String Token = "Token";

        /// <summary>创建者。服务端设备</summary>
        public const String Creator = "Creator";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
