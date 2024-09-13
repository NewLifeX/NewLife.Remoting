using System.ComponentModel;

namespace NewLife.Remoting.Models;

/// <summary>更新模式。发布或更新时覆盖目标文件的模式</summary>
public enum UpdateModes
{
    /// <summary>默认</summary>
    Default = 0,

    /// <summary>部分包。覆盖已有文件，保留其它文件</summary>
    [Description("部分包")]
    Partial = 1,

    /// <summary>标准包。清空所有可执行文件，保留配置和数据等其它文件</summary>
    [Description("标准包")]
    Standard = 2,

    /// <summary>完整包。清空所有文件</summary>
    [Description("完整包")]
    Full = 3,
}