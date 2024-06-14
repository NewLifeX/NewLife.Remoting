﻿namespace NewLife.Remoting.Models;

/// <summary>设备接口</summary>
public interface IDevice
{
    /// <summary>编码</summary>
    String Code { get; set; }

    /// <summary>名称</summary>
    String Name { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get;set; }
}
