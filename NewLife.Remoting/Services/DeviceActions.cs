using System.Globalization;

namespace NewLife.Remoting.Services;

/// <summary>设备历史动作名</summary>
/// <remarks>
/// 历史记录是持久化数据，<c>action</c> 必须使用与写入进程语言无关的规范键（英文原文），由展示层按当前界面语言翻译。
/// 写入时不要本地化，也不要拼接可变的服务名或登录来源（Http/WebSocket/Mqtt），
/// 否则同一动作会因部署环境不同而在数据库中产生多种文本，展示层无法按标签码映射。
/// <para>动作名使用英文事件短语（完成态或名词）。同一事件的成功与失败共用动作名，结果由 Success 字段区分，
/// 不要把成功/失败写进动作名。</para>
/// <para>旧版本写入的来源+中文标签（如 <c>Http设备下线</c>、<c>Node鉴权</c>）应归一化为本类常量后再展示或筛选。</para>
/// </remarks>
public static class DeviceActions
{
    #region 登录注销
    /// <summary>设备鉴权。成功与失败共用该动作名，结果由 Success 字段区分</summary>
    public const String DeviceAuthentication = "Device authentication";

    /// <summary>标识不一致。客户端上报的唯一标识与登记信息不符，用于发现配置拷贝或设备重置</summary>
    public const String IdentityMismatch = "Identity mismatch";

    /// <summary>动态注册。设备首次登录或密钥失效后，由服务端自动登记并下发新密钥</summary>
    public const String DynamicRegistration = "Dynamic registration";

    /// <summary>设备登录</summary>
    public const String DeviceLogin = "Device login";

    /// <summary>设备上线。离线后恢复在线，如心跳或数据上报触发</summary>
    public const String DeviceOnline = "Device online";

    /// <summary>设备下线。包含主动注销与长连接断开</summary>
    public const String DeviceOffline = "Device offline";

    /// <summary>下线超时。在线会话超时后由应用的清理逻辑写入</summary>
    public const String OfflineTimeout = "Offline timeout";
    #endregion

    #region 命令与事件
    /// <summary>命令下发。平台向设备发送命令</summary>
    public const String CommandSent = "Command sent";

    /// <summary>命令回执。设备回传命令执行结果</summary>
    public const String CommandReply = "Command reply";

    /// <summary>设备事件。设备上报的事件没有名称时使用</summary>
    public const String DeviceEvent = "Device event";
    #endregion

    #region 方法
    /// <summary>格式化设备下线备注</summary>
    /// <remarks>固定英文与 yyyy-MM-dd HH:mm:ss 时间格式，避免写入进程的区域设置影响审计文本</remarks>
    /// <param name="reason">下线原因，为空时使用 Offline</param>
    /// <param name="device">设备名称或编码</param>
    /// <param name="sessionId">会话标识</param>
    /// <param name="loginTime">登录时间</param>
    /// <param name="lastActive">最后活跃时间</param>
    /// <returns>规范化备注，如 ApplicationExit [DESKTOP/2EE79C9F] login at 2026-11-14 14:02:12, last active at 2026-11-14 15:52:35</returns>
    public static String FormatOfflineRemark(String? reason, String? device, String? sessionId, DateTime loginTime, DateTime lastActive)
    {
        if (reason.IsNullOrEmpty()) reason = "Offline";

        var target = device.IsNullOrEmpty() ? "-" : device;
        if (!sessionId.IsNullOrEmpty()) target += "/" + sessionId;

        var text = $"{reason} [{target}]";
        if (loginTime.Year > 2000) text += $" login at {loginTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}";
        if (lastActive.Year > 2000) text += $", last active at {lastActive.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}";

        return text;
    }
    #endregion
}
