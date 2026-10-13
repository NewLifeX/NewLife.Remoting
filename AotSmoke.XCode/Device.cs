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

/// <summary>设备。用于验证 XCode 实体在 NativeAOT 下的运行期可用性</summary>
[Serializable]
[DataObject]
[Description("设备。用于验证 XCode 实体在 NativeAOT 下的运行期可用性")]
[BindIndex("IU_Device_Code", true, "Code")]
[BindTable("Device", Description = "设备。用于验证 XCode 实体在 NativeAOT 下的运行期可用性", ConnName = "AotSmokeXCode", DbType = DatabaseType.None)]
public partial class Device
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

    private String _Code;
    /// <summary>编码。设备唯一证书</summary>
    [DisplayName("编码")]
    [Description("编码。设备唯一证书")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Code", "编码。设备唯一证书", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Secret;
    /// <summary>密钥</summary>
    [DisplayName("密钥")]
    [Description("密钥")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Secret", "密钥", "")]
    public String Secret { get => _Secret; set { if (OnPropertyChanging("Secret", value)) { _Secret = value; OnPropertyChanged("Secret"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _Online;
    /// <summary>在线</summary>
    [DisplayName("在线")]
    [Description("在线")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Online", "在线", "")]
    public Boolean Online { get => _Online; set { if (OnPropertyChanging("Online", value)) { _Online = value; OnPropertyChanged("Online"); } } }

    private String _Uuid;
    /// <summary>唯一标识</summary>
    [DisplayName("唯一标识")]
    [Description("唯一标识")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Uuid", "唯一标识", "")]
    public String Uuid { get => _Uuid; set { if (OnPropertyChanging("Uuid", value)) { _Uuid = value; OnPropertyChanged("Uuid"); } } }

    private Int32 _Period;
    /// <summary>心跳周期。默认60秒</summary>
    [DisplayName("心跳周期")]
    [Description("心跳周期。默认60秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Period", "心跳周期。默认60秒", "")]
    public Int32 Period { get => _Period; set { if (OnPropertyChanging("Period", value)) { _Period = value; OnPropertyChanged("Period"); } } }

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
            "Name" => _Name,
            "Code" => _Code,
            "Secret" => _Secret,
            "Enable" => _Enable,
            "Online" => _Online,
            "Uuid" => _Uuid,
            "Period" => _Period,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "Secret": _Secret = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Online": _Online = value.ToBoolean(); break;
                case "Uuid": _Uuid = Convert.ToString(value); break;
                case "Period": _Period = value.ToInt(); break;
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
    public static Device FindById(Int32 id)
    {
        if (id < 0) return null;

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

        return Find(_.Code == code);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="code">编码。设备唯一证书</param>
    /// <param name="online">在线</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Device> Search(String code, Boolean? online, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (online != null) exp &= _.Online == online;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设备字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>编码。设备唯一证书</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>密钥</summary>
        public static readonly Field Secret = FindByName("Secret");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>在线</summary>
        public static readonly Field Online = FindByName("Online");

        /// <summary>唯一标识</summary>
        public static readonly Field Uuid = FindByName("Uuid");

        /// <summary>心跳周期。默认60秒</summary>
        public static readonly Field Period = FindByName("Period");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设备字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>编码。设备唯一证书</summary>
        public const String Code = "Code";

        /// <summary>密钥</summary>
        public const String Secret = "Secret";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>在线</summary>
        public const String Online = "Online";

        /// <summary>唯一标识</summary>
        public const String Uuid = "Uuid";

        /// <summary>心跳周期。默认60秒</summary>
        public const String Period = "Period";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
