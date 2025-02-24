﻿using NewLife.Remoting.Models;
#if !NET45
using TaskEx=System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Services;

/// <summary>设备会话</summary>
public class DeviceSession : DisposeBase, IDeviceSession
{
    #region 属性
    /// <summary>设备编码。用于识别指令归属</summary>
    public String Code { get; set; } = null!;

    /// <summary>是否活动中</summary>
    public virtual Boolean Active { get; } = true;
    #endregion

    /// <summary>处理事件</summary>
    public virtual Task HandleAsync(CommandModel command, String message) => TaskEx.CompletedTask;
}
