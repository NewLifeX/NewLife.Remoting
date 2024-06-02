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

/// <summary>设备数据。设备采集原始数据，按天分表存储</summary>
[Serializable]
[DataObject]
[Description("设备数据。设备采集原始数据，按天分表存储")]
[BindIndex("IX_DeviceData_DeviceId_Id", false, "DeviceId,Id")]
[BindIndex("IX_DeviceData_DeviceId_Name_Id", false, "DeviceId,Name,Id")]
[BindIndex("IX_DeviceData_DeviceId_Kind_Id", false, "DeviceId,Kind,Id")]
[BindTable("DeviceData", Description = "设备数据。设备采集原始数据，按天分表存储", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class DeviceData
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _DeviceId;
    /// <summary>设备</summary>
    [DisplayName("设备")]
    [Description("设备")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DeviceId", "设备", "")]
    public Int32 DeviceId { get => _DeviceId; set { if (OnPropertyChanging("DeviceId", value)) { _DeviceId = value; OnPropertyChanged("DeviceId"); } } }

    private String _Name;
    /// <summary>名称。MQTT的Topic，或者属性名</summary>
    [DisplayName("名称")]
    [Description("名称。MQTT的Topic，或者属性名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。MQTT的Topic，或者属性名", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Kind;
    /// <summary>类型。数据来源，如PostProperty/PostData/MqttPostData</summary>
    [DisplayName("类型")]
    [Description("类型。数据来源，如PostProperty/PostData/MqttPostData")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Kind", "类型。数据来源，如PostProperty/PostData/MqttPostData", "")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _Value;
    /// <summary>数值</summary>
    [DisplayName("数值")]
    [Description("数值")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Value", "数值", "")]
    public String Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private Int64 _Timestamp;
    /// <summary>时间戳。设备生成数据时的UTC毫秒</summary>
    [DisplayName("时间戳")]
    [Description("时间戳。设备生成数据时的UTC毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Timestamp", "时间戳。设备生成数据时的UTC毫秒", "")]
    public Int64 Timestamp { get => _Timestamp; set { if (OnPropertyChanging("Timestamp", value)) { _Timestamp = value; OnPropertyChanged("Timestamp"); } } }

    private String _TraceId;
    /// <summary>追踪标识。用于记录调用链追踪标识，在APM查找调用链</summary>
    [Category("扩展")]
    [DisplayName("追踪标识")]
    [Description("追踪标识。用于记录调用链追踪标识，在APM查找调用链")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪标识。用于记录调用链追踪标识，在APM查找调用链", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private String _Creator;
    /// <summary>创建者。服务端设备</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者。服务端设备")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Creator", "创建者。服务端设备", "")]
    public String Creator { get => _Creator; set { if (OnPropertyChanging("Creator", value)) { _Creator = value; OnPropertyChanged("Creator"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }
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
            "Name" => _Name,
            "Kind" => _Kind,
            "Value" => _Value,
            "Timestamp" => _Timestamp,
            "TraceId" => _TraceId,
            "Creator" => _Creator,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "DeviceId": _DeviceId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "Timestamp": _Timestamp = value.ToLong(); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "Creator": _Creator = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 字段名
    /// <summary>取得设备数据字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>设备</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>名称。MQTT的Topic，或者属性名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>类型。数据来源，如PostProperty/PostData/MqttPostData</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>数值</summary>
        public static readonly Field Value = FindByName("Value");

        /// <summary>时间戳。设备生成数据时的UTC毫秒</summary>
        public static readonly Field Timestamp = FindByName("Timestamp");

        /// <summary>追踪标识。用于记录调用链追踪标识，在APM查找调用链</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建者。服务端设备</summary>
        public static readonly Field Creator = FindByName("Creator");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设备数据字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>设备</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>名称。MQTT的Topic，或者属性名</summary>
        public const String Name = "Name";

        /// <summary>类型。数据来源，如PostProperty/PostData/MqttPostData</summary>
        public const String Kind = "Kind";

        /// <summary>数值</summary>
        public const String Value = "Value";

        /// <summary>时间戳。设备生成数据时的UTC毫秒</summary>
        public const String Timestamp = "Timestamp";

        /// <summary>追踪标识。用于记录调用链追踪标识，在APM查找调用链</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建者。服务端设备</summary>
        public const String Creator = "Creator";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";
    }
    #endregion
}
