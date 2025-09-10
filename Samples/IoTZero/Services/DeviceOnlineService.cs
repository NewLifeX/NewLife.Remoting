using IoT.Data;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Threading;

namespace IoTZero.Services;

/// <summary>节点在线服务</summary>
public class DeviceOnlineService : IHostedService
{
    #region 属性
    private TimerX _timer;
    private readonly IDeviceService _deviceService;
    private readonly ITokenSetting _setting;
    private readonly ITracer _tracer;
    #endregion

    #region 构造
    /// <summary>
    /// 实例化节点在线服务
    /// </summary>
    /// <param name="deviceService"></param>
    /// <param name="setting"></param>
    /// <param name="tracer"></param>
    public DeviceOnlineService(IDeviceService deviceService, ITokenSetting setting, ITracer tracer)
    {
        _deviceService = deviceService;
        _setting = setting;
        _tracer = tracer;
    }
    #endregion

    #region 方法
    /// <summary>
    /// 开始服务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new TimerX(CheckOnline, null, 5_000, 30_000) { Async = true };

        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    private void CheckOnline(Object state)
    {
        // 节点超时
        if (_setting.SessionTimeout > 0)
        {
            using var span = _tracer?.NewSpan(nameof(CheckOnline));

            var rs = DeviceOnline.ClearExpire(TimeSpan.FromSeconds(_setting.SessionTimeout));
            if (rs != null)
            {
                foreach (var olt in rs)
                {
                    var device = olt?.Device;
                    var msg = $"[{device}]登录于{olt.CreateTime.ToFullString()}，最后活跃于{olt.UpdateTime.ToFullString()}";
                    _deviceService.WriteHistory(device, "超时下线", true, msg, null, olt.CreateIP);

                    if (_deviceService is MyDeviceService ds)
                        ds.RemoveOnline(new DeviceContext { Device = device, UserHost = olt.CreateIP });

                    if (device != null)
                    {
                        // 计算在线时长
                        if (olt.CreateTime.Year > 2000 && olt.UpdateTime.Year > 2000)
                            device.OnlineTime += (Int32)(olt.UpdateTime - olt.CreateTime).TotalSeconds;

                        device.Logout();

                        CheckOffline(device, "超时下线");
                    }
                }
            }
        }
    }

    /// <summary>
    /// 检查离线
    /// </summary>
    /// <param name="node"></param>
    /// <param name="reason"></param>
    public static void CheckOffline(Device node, String reason)
    {
        //todo 下线告警
    }
    #endregion
}