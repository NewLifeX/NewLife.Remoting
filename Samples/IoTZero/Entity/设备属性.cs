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

/// <summary>设备属性。设备的功能模型之一，一般用于描述设备运行时的状态，如环境监测设备所读取的当前环境温度等。一个设备有多个属性，名值表</summary>
[Serializable]
[DataObject]
[Description("设备属性。设备的功能模型之一，一般用于描述设备运行时的状态，如环境监测设备所读取的当前环境温度等。一个设备有多个属性，名值表")]
[BindIndex("IU_DeviceProperty_DeviceId_Name", true, "DeviceId,Name")]
[BindIndex("IX_DeviceProperty_UpdateTime", false, "UpdateTime")]
[BindTable("DeviceProperty", Description = "设备属性。设备的功能模型之一，一般用于描述设备运行时的状态，如环境监测设备所读取的当前环境温度等。一个设备有多个属性，名值表", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class DeviceProperty
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

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _NickName;
    /// <summary>昵称</summary>
    [DisplayName("昵称")]
    [Description("昵称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("NickName", "昵称", "")]
    public String NickName { get => _NickName; set { if (OnPropertyChanging("NickName", value)) { _NickName = value; OnPropertyChanged("NickName"); } } }

    private String _Type;
    /// <summary>类型</summary>
    [DisplayName("类型")]
    [Description("类型")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Type", "类型", "")]
    public String Type { get => _Type; set { if (OnPropertyChanging("Type", value)) { _Type = value; OnPropertyChanged("Type"); } } }

    private String _Value;
    /// <summary>数值。设备上报数值</summary>
    [DisplayName("数值")]
    [Description("数值。设备上报数值")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Value", "数值。设备上报数值", "")]
    public String Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private String _Unit;
    /// <summary>单位</summary>
    [DisplayName("单位")]
    [Description("单位")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Unit", "单位", "")]
    public String Unit { get => _Unit; set { if (OnPropertyChanging("Unit", value)) { _Unit = value; OnPropertyChanged("Unit"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _TraceId;
    /// <summary>追踪。用于记录调用链追踪标识，在APM查找调用链</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪。用于记录调用链追踪标识，在APM查找调用链")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪。用于记录调用链追踪标识，在APM查找调用链", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

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

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [Category("扩展")]
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String _UpdateIP;
    /// <summary>更新地址</summary>
    [Category("扩展")]
    [DisplayName("更新地址")]
    [Description("更新地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "更新地址", "")]
    public String UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }
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
            "NickName" => _NickName,
            "Type" => _Type,
            "Value" => _Value,
            "Unit" => _Unit,
            "Enable" => _Enable,
            "TraceId" => _TraceId,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateTime" => _UpdateTime,
            "UpdateIP" => _UpdateIP,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "DeviceId": _DeviceId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "NickName": _NickName = Convert.ToString(value); break;
                case "Type": _Type = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "Unit": _Unit = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 字段名
    /// <summary>取得设备属性字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>设备</summary>
        public static readonly Field DeviceId = FindByName("DeviceId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>昵称</summary>
        public static readonly Field NickName = FindByName("NickName");

        /// <summary>类型</summary>
        public static readonly Field Type = FindByName("Type");

        /// <summary>数值。设备上报数值</summary>
        public static readonly Field Value = FindByName("Value");

        /// <summary>单位</summary>
        public static readonly Field Unit = FindByName("Unit");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>追踪。用于记录调用链追踪标识，在APM查找调用链</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设备属性字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>设备</summary>
        public const String DeviceId = "DeviceId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>昵称</summary>
        public const String NickName = "NickName";

        /// <summary>类型</summary>
        public const String Type = "Type";

        /// <summary>数值。设备上报数值</summary>
        public const String Value = "Value";

        /// <summary>单位</summary>
        public const String Unit = "Unit";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>追踪。用于记录调用链追踪标识，在APM查找调用链</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";
    }
    #endregion
}
