using NewLife;
using NewLife.Caching;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;
using XCode.Membership;
using Zero.Data.Nodes;
using ZeroServer.Models;

namespace ZeroServer.Services;

/// <summary>设备服务</summary>
/// <param name="passwordProvider"></param>
/// <param name="cacheProvider"></param>
/// <param name="setting"></param>
public class NodeService(ISessionManager sessionManager, IPasswordProvider passwordProvider, ICacheProvider cacheProvider, ITokenSetting setting) : IDeviceService
{
    private readonly ICache _cache = cacheProvider.InnerCache;

    #region 登录注销
    /// <summary>
    /// 设备登录验证，内部支持动态注册
    /// </summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录信息</param>
    /// <param name="source">登录来源</param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public ILoginResponse Login(DeviceContext context, ILoginRequest request, String source)
    {
        if (request is not LoginInfo inf) throw new ArgumentOutOfRangeException(nameof(request));

        var code = inf.Code;
        var secret = inf.Secret;

        var node = Node.FindByCode(code!, true);
        if (node != null && !node.Enable) throw new ApiException(ApiCode.Forbidden, "禁止登录");

        var ip = context.UserHost;
        var autoReg = false;
        if (node == null)
        {
            node = AutoRegister(null, inf, ip);
            autoReg = true;
        }
        else
        {
            if (!node.Enable) throw new ApiException(ApiCode.Forbidden, "禁止登录");

            // 校验唯一编码，防止客户端拷贝配置
            var uuid = inf.UUID;
            if (!uuid.IsNullOrEmpty() && !node.Uuid.IsNullOrEmpty() && uuid != node.Uuid)
                WriteHistory(context, source + "登录校验", false, $"新旧唯一标识不一致！（新）{uuid}!={node.Uuid}（旧）");

            // 登录密码未设置或者未提交，则执行动态注册
            if (node == null || !node.Secret.IsNullOrEmpty()
                && (secret.IsNullOrEmpty() || !passwordProvider.Verify(node.Secret, secret)))
            {
                node = AutoRegister(node, inf, ip);
                autoReg = true;
            }
        }

        if (node == null) throw new ApiException(ApiCode.Unauthorized, "登录失败");

        node.Login(inf, ip);
        context.Device = node;

        // 在线记录
        var olt = GetOnline(node, ip) ?? CreateOnline(node, ip);
        olt.Save(inf, null, null, ip);
        context.Online = olt;

        // 登录历史
        WriteHistory(context, source + "登录", true, $"[{node.Name}/{node.Code}]登录成功 " + inf.ToJson(false, false, false));

        var rs = new LoginResponse
        {
            Code = node.Code,
            Name = node.Name
        };

        // 动态注册，下发节点证书
        if (autoReg) rs.Secret = node.Secret;

        return rs;
    }

    /// <summary>自动注册</summary>
    /// <param name="node"></param>
    /// <param name="inf"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public Node AutoRegister(Node node, LoginInfo inf, String ip)
    {
        // 全局开关，是否允许自动注册新产品
        if (!setting.AutoRegister) throw new ApiException(ApiCode.Forbidden, "禁止自动注册");

        var code = inf.Code;
        if (code.IsNullOrEmpty()) code = inf.UUID.GetBytes().Crc().ToString("X8");
        if (code.IsNullOrEmpty()) code = Rand.NextString(8);

        node ??= Node.FindByCode(code, false);
        node ??= new Node
        {
            Code = code,
            CreateIP = ip,
            CreateTime = DateTime.Now,
            Secret = Rand.NextString(8),
        };

        // 如果未打开动态注册，则把节点修改为禁用
        node.Enable = true;

        if (node.Name.IsNullOrEmpty()) node.Name = inf.Name;

        node.ProductCode = inf.ProductCode;
        node.Secret = Rand.NextString(16);
        node.UpdateIP = ip;
        node.UpdateTime = DateTime.Now;

        node.Save();

        this.WriteHistory(node, "动态注册", true, inf.ToJson(false, false, false), null, ip);

        return node;
    }

    /// <summary>注销</summary>
    /// <param name="context">上下文</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">登录来源</param>
    /// <returns></returns>
    public IOnlineModel Logout(DeviceContext context, String reason, String source)
    {
        var node = context.Device as Node;
        var ip = context.UserHost;
        var online = GetOnline(node, ip);
        if (online != null)
        {
            context.Online = online;

            var msg = $"{reason} [{node}]]登录于{online.CreateTime.ToFullString()}，最后活跃于{online.UpdateTime.ToFullString()}";
            WriteHistory(context, source + "设备下线", true, msg);
            online.Delete();

            var sid = $"{node.Id}@{ip}";
            _cache.Remove($"NodeOnline:{sid}");

            // 计算在线时长
            if (online.CreateTime.Year > 2000)
            {
                node.OnlineTime += (Int32)(DateTime.Now - online.CreateTime).TotalSeconds;
                node.Update();
            }
        }

        return online;
    }
    #endregion

    #region 心跳保活
    /// <summary>心跳</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">心跳请求</param>
    /// <returns></returns>
    public IOnlineModel Ping(DeviceContext context, IPingRequest request)
    {
        var node = context.Device as Node;
        var inf = request as PingInfo;
        if (inf != null && !inf.IP.IsNullOrEmpty()) node.IP = inf.IP;

        var ip = context.UserHost;
        node.UpdateIP = ip;
        node.FixArea();

        // 每10分钟更新一次节点信息，确保活跃
        if (node.LastActive.AddMinutes(10) < DateTime.Now) node.LastActive = DateTime.Now;
        node.SaveAsync();

        var online = GetOnline(node, ip) ?? CreateOnline(node, ip);
        online.Name = node.Name;
        online.Category = node.Category;
        online.Version = node.Version;
        online.CompileTime = node.CompileTime;
        online.OSKind = node.OSKind;
        online.Save(null, inf, context.Token, ip);

        context.Online = online;

        return online;
    }

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="context">上下文</param>
    /// <param name="online"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public IOnlineModel SetOnline(DeviceContext context, Boolean online)
    {
        if (context.Device is Node node)
        {
            // 上线打标记
            var olt = GetOnline(node, context.UserHost);
            if (olt != null)
            {
                olt.WebSocket = online;
                olt.Update();
            }

            return olt;
        }

        return null;
    }

    /// <summary></summary>
    /// <param name="node"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public virtual NodeOnline GetOnline(Node node, String ip)
    {
        var sid = $"{node.Id}@{ip}";
        var online = _cache.Get<NodeOnline>($"NodeOnline:{sid}");
        if (online != null)
        {
            _cache.SetExpire($"NodeOnline:{sid}", TimeSpan.FromSeconds(600));
            return online;
        }

        return NodeOnline.FindBySessionID(sid);
    }

    /// <summary>检查在线</summary>
    /// <param name="node"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public virtual NodeOnline CreateOnline(Node node, String ip)
    {
        var sid = $"{node.Id}@{ip}";
        var online = NodeOnline.GetOrAdd(sid);
        online.NodeId = node.Id;
        online.Name = node.Name;
        online.IP = node.IP;
        online.CreateIP = ip;

        online.Creator = Environment.MachineName;

        _cache.Set($"NodeOnline:{sid}", online, 600);

        return online;
    }

    /// <summary>删除在线</summary>
    /// <param name="deviceId"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public Int32 RemoveOnline(Int32 deviceId, String ip)
    {
        var sid = $"{deviceId}@{ip}";

        return _cache.Remove($"NodeOnline:{sid}");
    }
    #endregion

    #region 下行通知
    /// <summary>发送命令</summary>
    /// <param name="device">设备</param>
    /// <param name="command"></param>
    /// <returns></returns>
    public Task<Int32> SendCommand(IDeviceModel device, CommandModel command, CancellationToken cancellationToken) => sessionManager.PublishAsync(device.Code, command, null, cancellationToken);
    #endregion

    #region 升级更新
    /// <summary>升级检查</summary>
    /// <param name="channel">更新通道</param>
    /// <returns></returns>
    public IUpgradeInfo Upgrade(DeviceContext context, String channel) => null;
    #endregion

    #region 事件上报
    /// <summary>命令响应</summary>
    /// <param name="context">上下文</param>
    /// <param name="model"></param>
    /// <returns></returns>
    public Int32 CommandReply(DeviceContext context, CommandReplyModel model) => 0;

    /// <summary>上报事件</summary>
    /// <param name="context">上下文</param>
    /// <param name="events"></param>
    /// <returns></returns>
    public Int32 PostEvents(DeviceContext context, EventModel[] events) => 0;
    #endregion

    #region 辅助
    /// <summary>查找设备</summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public IDeviceModel QueryDevice(String code) => Node.FindByCode(code);

    /// <summary>写设备历史</summary>
    /// <param name="context">上下文</param>
    /// <param name="action">动作</param>
    /// <param name="success">成功</param>
    /// <param name="remark">备注内容</param>
    public void WriteHistory(DeviceContext context, String action, Boolean success, String remark)
    {
        var ip = context.UserHost;
        var history = NodeHistory.Create(context.Device as Node, action, success, remark, Environment.MachineName, ip);

        if (history.CityID == 0 && !ip.IsNullOrEmpty())
        {
            var rs = Area.SearchIP(ip);
            if (rs.Count > 0) history.ProvinceID = rs[0].ID;
            if (rs.Count > 1) history.CityID = rs[^1].ID;
        }

        history.SaveAsync();
    }
    #endregion
}