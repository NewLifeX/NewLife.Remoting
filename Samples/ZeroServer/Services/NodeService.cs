using NewLife;
using NewLife.Caching;
using NewLife.Remoting;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using Zero.Data.Nodes;
using ZeroServer.Models;

namespace ZeroServer.Services;

/// <summary>设备服务</summary>
/// <param name="passwordProvider"></param>
/// <param name="cacheProvider"></param>
/// <param name="setting"></param>
public class NodeService : DefaultDeviceService<Node, NodeOnline>
{
    private readonly ITokenSetting _setting;

    public NodeService(ISessionManager sessionManager, IPasswordProvider passwordProvider, ICacheProvider cacheProvider, ITokenSetting setting, IServiceProvider serviceProvider) : base(sessionManager, passwordProvider, cacheProvider, serviceProvider)
    {
        Name = "Node";
        _setting = setting;
    }

    #region 登录注销
    public override Boolean Authorize(DeviceContext context, ILoginRequest request)
    {
        var dv = context.Device as Node;
        var inf = request as LoginInfo;

        // 校验唯一编码，防止客户端拷贝配置
        var uuid = inf.UUID;
        if (!uuid.IsNullOrEmpty() && !dv.Uuid.IsNullOrEmpty() && uuid != dv.Uuid)
            WriteHistory(context, "登录校验", false, $"新旧唯一标识不一致！（新）{uuid}!={dv.Uuid}（旧）");

        return base.Authorize(context, request);
    }

    protected override void OnRegister(DeviceContext context, ILoginRequest request)
    {
        // 全局开关，是否允许自动注册新产品
        if (!_setting.AutoRegister) throw new ApiException(ApiCode.Forbidden, "禁止自动注册");

        var inf = request as LoginInfo;
        var node = context.Device as Node;
        if (node.Name.IsNullOrEmpty()) node.Name = inf.Name;

        node.ProductCode = inf.ProductCode;
        //node.Secret = Rand.NextString(16);
        //node.UpdateIP = context.UserHost;
        //node.UpdateTime = DateTime.Now;

        base.OnRegister(context, request);
    }

    public override void OnLogin(DeviceContext context, ILoginRequest request)
    {
        var node = context.Device as Node;
        var inf = request as LoginInfo;
        node.Login(inf, context.UserHost);

        base.OnLogin(context, request);
    }
    #endregion

    #region 心跳保活
    /// <summary>设备心跳。更新在线记录信息</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns></returns>
    public override IOnlineModel OnPing(DeviceContext context, IPingRequest request)
    {
        var node = context.Device as Node;
        var inf = request as PingInfo;
        if (inf != null && !inf.IP.IsNullOrEmpty()) node.IP = inf.IP;

        node.UpdateIP = context.UserHost;
        node.FixArea();

        // 每10分钟更新一次节点信息，确保活跃
        if (node.LastActive.AddMinutes(10) < DateTime.Now) node.LastActive = DateTime.Now;
        node.SaveAsync();

        return base.OnPing(context, request);
    }

    /// <summary>结算在线时长。累加本次会话在线时长到节点</summary>
    /// <param name="online">在线实体</param>
    /// <param name="device">设备信息</param>
    protected override void OnSettleOnline(IOnlineModel online, IDeviceModel device)
    {
        if (online is NodeOnline olt && device is Node node)
        {
            var sec = (Int32)(olt.UpdateTime - olt.LoginTime).TotalSeconds;
            if (sec > 0)
            {
                node.OnlineTime += sec;
                node.Update();
            }
        }
    }
    #endregion

    #region 辅助
    /// <summary>查找设备</summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public override IDeviceModel QueryDevice(String code) => Node.FindByCode(code);

    /// <summary>查找在线。直接查库，绕过所有缓存</summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    public override IOnlineModel QueryOnline(String sessionId) => NodeOnline.FindBySessionIdWithCache(sessionId, false);

    /// <summary>写设备历史</summary>
    /// <param name="context">上下文</param>
    /// <param name="action">动作</param>
    /// <param name="success">成功</param>
    /// <param name="remark">备注内容</param>
    public override void WriteHistory(DeviceContext context, String action, Boolean success, String remark)
    {
        var ip = context.UserHost;
        var history = NodeHistory.Create(context.Device as Node, action, success, remark, Environment.MachineName, ip);

        history.SaveAsync();
    }
    #endregion
}