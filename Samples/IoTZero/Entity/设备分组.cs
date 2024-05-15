﻿using System;
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

/// <summary>设备分组。物联网平台支持建立设备分组，分组中可包含不同产品下的设备。通过设备组来进行跨产品管理设备。</summary>
[Serializable]
[DataObject]
[Description("设备分组。物联网平台支持建立设备分组，分组中可包含不同产品下的设备。通过设备组来进行跨产品管理设备。")]
[BindIndex("IU_DeviceGroup_ParentId_Name", true, "ParentId,Name")]
[BindIndex("IX_DeviceGroup_Name", false, "Name")]
[BindTable("DeviceGroup", Description = "设备分组。物联网平台支持建立设备分组，分组中可包含不同产品下的设备。通过设备组来进行跨产品管理设备。", ConnName = "IoT", DbType = DatabaseType.None)]
public partial class DeviceGroup
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _ParentId;
    /// <summary>父级</summary>
    [DisplayName("父级")]
    [Description("父级")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ParentId", "父级", "")]
    public Int32 ParentId { get => _ParentId; set { if (OnPropertyChanging("ParentId", value)) { _ParentId = value; OnPropertyChanged("ParentId"); } } }

    private Int32 _Sort;
    /// <summary>排序</summary>
    [DisplayName("排序")]
    [Description("排序")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sort", "排序", "")]
    public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

    private Int32 _Devices;
    /// <summary>设备总数</summary>
    [DisplayName("设备总数")]
    [Description("设备总数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Devices", "设备总数", "")]
    public Int32 Devices { get => _Devices; set { if (OnPropertyChanging("Devices", value)) { _Devices = value; OnPropertyChanged("Devices"); } } }

    private Int32 _Activations;
    /// <summary>激活设备</summary>
    [DisplayName("激活设备")]
    [Description("激活设备")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Activations", "激活设备", "")]
    public Int32 Activations { get => _Activations; set { if (OnPropertyChanging("Activations", value)) { _Activations = value; OnPropertyChanged("Activations"); } } }

    private Int32 _Onlines;
    /// <summary>当前在线</summary>
    [DisplayName("当前在线")]
    [Description("当前在线")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Onlines", "当前在线", "")]
    public Int32 Onlines { get => _Onlines; set { if (OnPropertyChanging("Onlines", value)) { _Onlines = value; OnPropertyChanged("Onlines"); } } }

    private Int32 _CreateUserId;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserId", "创建者", "")]
    public Int32 CreateUserId { get => _CreateUserId; set { if (OnPropertyChanging("CreateUserId", value)) { _CreateUserId = value; OnPropertyChanged("CreateUserId"); } } }

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

    private Int32 _UpdateUserId;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserId", "更新者", "")]
    public Int32 UpdateUserId { get => _UpdateUserId; set { if (OnPropertyChanging("UpdateUserId", value)) { _UpdateUserId = value; OnPropertyChanged("UpdateUserId"); } } }

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

    private String _Remark;
    /// <summary>描述</summary>
    [Category("扩展")]
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "描述", "")]
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
            "Name" => _Name,
            "ParentId" => _ParentId,
            "Sort" => _Sort,
            "Devices" => _Devices,
            "Activations" => _Activations,
            "Onlines" => _Onlines,
            "CreateUserId" => _CreateUserId,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUserId" => _UpdateUserId,
            "UpdateTime" => _UpdateTime,
            "UpdateIP" => _UpdateIP,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "ParentId": _ParentId = value.ToInt(); break;
                case "Sort": _Sort = value.ToInt(); break;
                case "Devices": _Devices = value.ToInt(); break;
                case "Activations": _Activations = value.ToInt(); break;
                case "Onlines": _Onlines = value.ToInt(); break;
                case "CreateUserId": _CreateUserId = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUserId": _UpdateUserId = value.ToInt(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 字段名
    /// <summary>取得设备分组字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>父级</summary>
        public static readonly Field ParentId = FindByName("ParentId");

        /// <summary>排序</summary>
        public static readonly Field Sort = FindByName("Sort");

        /// <summary>设备总数</summary>
        public static readonly Field Devices = FindByName("Devices");

        /// <summary>激活设备</summary>
        public static readonly Field Activations = FindByName("Activations");

        /// <summary>当前在线</summary>
        public static readonly Field Onlines = FindByName("Onlines");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserId = FindByName("CreateUserId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUserId = FindByName("UpdateUserId");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>描述</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设备分组字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>父级</summary>
        public const String ParentId = "ParentId";

        /// <summary>排序</summary>
        public const String Sort = "Sort";

        /// <summary>设备总数</summary>
        public const String Devices = "Devices";

        /// <summary>激活设备</summary>
        public const String Activations = "Activations";

        /// <summary>当前在线</summary>
        public const String Onlines = "Onlines";

        /// <summary>创建者</summary>
        public const String CreateUserId = "CreateUserId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新者</summary>
        public const String UpdateUserId = "UpdateUserId";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";

        /// <summary>描述</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
