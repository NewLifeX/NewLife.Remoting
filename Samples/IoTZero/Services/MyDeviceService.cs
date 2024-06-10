using System.Reflection;
using IoT.Data;
using NewLife;
using NewLife.Caching;
using NewLife.IoT.Models;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Web;
using TokenModel = NewLife.Remoting.Models.TokenModel;

namespace IoTZero.Services;

/// <summary>设备服务</summary>
public class MyDeviceService
{
    /// <summary>节点引用，令牌无效时使用</summary>
    public Device Current { get; set; }

    private readonly ICache _cache;
    private readonly IPasswordProvider _passwordProvider;
    private readonly DataService _dataService;
    private readonly IoTSetting _setting;
    private readonly ITracer _tracer;

    /// <summary>
    /// 实例化设备服务
    /// </summary>
    /// <param name="passwordProvider"></param>
    /// <param name="dataService"></param>
    /// <param name="cacheProvider"></param>
    /// <param name="setting"></param>
    /// <param name="tracer"></param>
    public MyDeviceService(IPasswordProvider passwordProvider, DataService dataService, ICacheProvider cacheProvider, IoTSetting setting, ITracer tracer)
    {
        _passwordProvider = passwordProvider;
        _dataService = dataService;
        _cache = cacheProvider.InnerCache;
        _setting = setting;
        _tracer = tracer;
    }

    #region 登录
    /// <summary>
    /// 设备登录验证，内部支持动态注册
    /// </summary>
    /// <param name="inf">登录信息</param>
    /// <param name="source">登录来源</param>
    /// <param name="ip">远程IP</param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public LoginResponse Login(LoginInfo inf, String source, String ip)
    {
        var code = inf.Code;
        var secret = inf.Secret;

        var dv = Device.FindByCode(code);
        Current = dv;

        var autoReg = false;
        if (dv == null)
        {
            if (inf.ProductKey.IsNullOrEmpty()) throw new ApiException(ApiCode.NotFound, "找不到设备，且产品证书为空，无法登录");

            dv = AutoRegister(null, inf, ip);
            autoReg = true;
        }
        else
        {
            if (!dv.Enable) throw new ApiException(ApiCode.Forbidden, "禁止登录");

            // 校验唯一编码，防止客户端拷贝配置
            var uuid = inf.UUID;
            if (!uuid.IsNullOrEmpty() && !dv.Uuid.IsNullOrEmpty() && uuid != dv.Uuid)
                WriteHistory(dv, source + "登录校验", false, $"新旧唯一标识不一致！（新）{uuid}!={dv.Uuid}（旧）", ip);

            // 登录密码未设置或者未提交，则执行动态注册
            if (dv == null || !dv.Secret.IsNullOrEmpty()
                && (secret.IsNullOrEmpty() || !_passwordProvider.Verify(dv.Secret, secret)))
            {
                if (inf.ProductKey.IsNullOrEmpty()) throw new ApiException(ApiCode.Unauthorized, "设备验证失败，且产品证书为空，无法登录");

                dv = AutoRegister(dv, inf, ip);
                autoReg = true;
            }
        }

        //if (dv != null && !dv.Enable) throw new ApiException(99, "禁止登录");

        Current = dv ?? throw new ApiException(12, "节点鉴权失败");

        dv.Login(inf, ip);

        // 设置令牌
        var tm = IssueToken(dv.Code, _setting);

        // 在线记录
        var olt = GetOnline(dv, ip) ?? CreateOnline(dv, ip);
        olt.Save(inf, null, tm.AccessToken);

        //SetChildOnline(dv, ip);

        // 登录历史
        WriteHistory(dv, source + "设备鉴权", true, $"[{dv.Name}/{dv.Code}]鉴权成功 " + inf.ToJson(false, false, false), ip);

        var rs = new LoginResponse
        {
            Name = dv.Name,
            Token = tm.AccessToken,
            Time = DateTime.UtcNow.ToLong(),
        };

        // 动态注册的设备不可用时，不要发令牌，只发证书
        if (!dv.Enable) rs.Token = null;

        // 动态注册，下发节点证书
        if (autoReg) rs.Secret = dv.Secret;

        rs.Code = dv.Code;

        return rs;
    }

    /// <summary>设置设备在线，同时检查在线表</summary>
    /// <param name="dv"></param>
    /// <param name="ip"></param>
    /// <param name="reason"></param>
    public void SetDeviceOnline(Device dv, String ip, String reason)
    {
        // 如果已上线，则不需要埋点
        var tracer = _tracer;
        //if (dv.Online) tracer = null;
        using var span = tracer?.NewSpan(nameof(SetDeviceOnline), new { dv.Name, dv.Code, ip, reason });

        var olt = GetOnline(dv, ip) ?? CreateOnline(dv, ip);

        dv.SetOnline(ip, reason);

        // 避免频繁更新心跳数
        if (olt.UpdateTime.AddSeconds(60) < DateTime.Now)
            olt.Save(null, null, null);
    }

    /// <summary>自动注册</summary>
    /// <param name="device"></param>
    /// <param name="inf"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public Device AutoRegister(Device device, LoginInfo inf, String ip)
    {
        // 全局开关，是否允许自动注册新产品
        if (!_setting.AutoRegister) throw new ApiException(12, "禁止自动注册");

        // 验证产品，即使产品不给自动注册，也会插入一个禁用的设备
        var product = Product.FindByCode(inf.ProductKey);
        if (product == null || !product.Enable)
            throw new ApiException(13, $"无效产品[{inf.ProductKey}]！");
        //if (!product.Secret.IsNullOrEmpty() && !_passwordProvider.Verify(product.Secret, inf.ProductSecret))
        //    throw new ApiException(13, $"非法产品[{product}]！");

        //// 检查白名单
        //if (!product.IsMatchWhiteIP(ip)) throw new ApiException(13, "非法来源，禁止注册");

        var code = inf.Code;
        if (code.IsNullOrEmpty()) code = Rand.NextString(8);

        device ??= new Device
        {
            Code = code,
            CreateIP = ip,
            CreateTime = DateTime.Now,
            Secret = Rand.NextString(8),
        };

        // 如果未打开动态注册，则把节点修改为禁用
        device.Enable = true;

        if (device.Name.IsNullOrEmpty()) device.Name = inf.Name;

        device.ProductId = product.Id;
        //device.Secret = Rand.NextString(16);
        device.UpdateIP = ip;
        device.UpdateTime = DateTime.Now;

        device.Save();

        // 更新产品设备总量避免界面无法及时获取设备数量信息
        device.Product.Fix();

        WriteHistory(device, "动态注册", true, inf.ToJson(false, false, false), ip);

        return device;
    }

    /// <summary>注销</summary>
    /// <param name="device">设备</param>
    /// <param name="reason">注销原因</param>
    /// <param name="source">登录来源</param>
    /// <param name="ip">远程IP</param>
    /// <returns></returns>
    public Device Logout(Device device, String reason, String source, String ip)
    {
        var olt = GetOnline(device, ip);
        if (olt != null)
        {
            var msg = $"{reason} [{device}]]登录于{olt.CreateTime.ToFullString()}，最后活跃于{olt.UpdateTime.ToFullString()}";
            WriteHistory(device, source + "设备下线", true, msg, ip);
            olt.Delete();

            var sid = $"{device.Id}@{ip}";
            _cache.Remove($"DeviceOnline:{sid}");

            // 计算在线时长
            if (olt.CreateTime.Year > 2000)
            {
                device.OnlineTime += (Int32)(DateTime.Now - olt.CreateTime).TotalSeconds;
                device.Logout();
            }

            //DeviceOnlineService.CheckOffline(device, "注销");
        }

        return device;
    }
    #endregion

    #region 心跳
    /// <summary>
    /// 心跳
    /// </summary>
    /// <param name="device"></param>
    /// <param name="inf"></param>
    /// <param name="token"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public DeviceOnline Ping(Device device, PingInfo inf, String token, String ip)
    {
        if (inf != null && !inf.IP.IsNullOrEmpty()) device.IP = inf.IP;

        // 自动上线
        if (device != null && !device.Online) device.SetOnline(ip, "心跳");

        device.UpdateIP = ip;
        device.SaveAsync();

        var olt = GetOnline(device, ip) ?? CreateOnline(device, ip);
        olt.Name = device.Name;
        olt.GroupPath = device.GroupPath;
        olt.ProductId = device.ProductId;
        olt.Save(null, inf, token);

        return olt;
    }

    /// <summary></summary>
    /// <param name="device"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    protected virtual DeviceOnline GetOnline(Device device, String ip)
    {
        var sid = $"{device.Id}@{ip}";
        var olt = _cache.Get<DeviceOnline>($"DeviceOnline:{sid}");
        if (olt != null)
        {
            _cache.SetExpire($"DeviceOnline:{sid}", TimeSpan.FromSeconds(600));
            return olt;
        }

        return DeviceOnline.FindBySessionId(sid);
    }

    /// <summary>检查在线</summary>
    /// <param name="device"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    protected virtual DeviceOnline CreateOnline(Device device, String ip)
    {
        var sid = $"{device.Id}@{ip}";
        var olt = DeviceOnline.GetOrAdd(sid);
        olt.ProductId = device.ProductId;
        olt.DeviceId = device.Id;
        olt.Name = device.Name;
        olt.IP = device.IP;
        olt.CreateIP = ip;

        olt.Creator = Environment.MachineName;

        _cache.Set($"DeviceOnline:{sid}", olt, 600);

        return olt;
    }

    /// <summary>删除在线</summary>
    /// <param name="deviceId"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public Int32 RemoveOnline(Int32 deviceId, String ip)
    {
        var sid = $"{deviceId}@{ip}";

        return _cache.Remove($"DeviceOnline:{sid}");
    }
    #endregion

    #region 辅助
    /// <summary>
    /// 颁发令牌
    /// </summary>
    /// <param name="name"></param>
    /// <param name="set"></param>
    /// <returns></returns>
    public TokenModel IssueToken(String name, IoTSetting set)
    {
        // 颁发令牌
        var ss = set.TokenSecret.Split(':');
        var jwt = new JwtBuilder
        {
            Issuer = Assembly.GetEntryAssembly().GetName().Name,
            Subject = name,
            Id = Rand.NextString(8),
            Expire = DateTime.Now.AddSeconds(set.TokenExpire),

            Algorithm = ss[0],
            Secret = ss[1],
        };

        return new TokenModel
        {
            AccessToken = jwt.Encode(null),
            TokenType = jwt.Type ?? "JWT",
            ExpireIn = set.TokenExpire,
            RefreshToken = jwt.Encode(null),
        };
    }

    /// <summary>
    /// 解码令牌，并验证有效性
    /// </summary>
    /// <param name="token"></param>
    /// <param name="tokenSecret"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public Device DecodeToken(String token, String tokenSecret)
    {
        //if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));
        if (token.IsNullOrEmpty()) throw new ApiException(ApiCode.Unauthorized, "节点未登录");

        // 解码令牌
        var ss = tokenSecret.Split(':');
        var jwt = new JwtBuilder
        {
            Algorithm = ss[0],
            Secret = ss[1],
        };

        var rs = jwt.TryDecode(token, out var message);
        var node = Device.FindByCode(jwt.Subject);
        Current = node;
        if (!rs) throw new ApiException(ApiCode.Forbidden, $"非法访问 {message}");

        return node;
    }

    /// <summary>
    /// 验证并颁发令牌
    /// </summary>
    /// <param name="deviceCode"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public TokenModel ValidAndIssueToken(String deviceCode, String token)
    {
        if (token.IsNullOrEmpty()) return null;

        // 令牌有效期检查，10分钟内过期者，重新颁发令牌
        var ss = _setting.TokenSecret.Split(':');
        var jwt = new JwtBuilder
        {
            Algorithm = ss[0],
            Secret = ss[1],
        };
        var rs = jwt.TryDecode(token, out var message);
        if (!rs || jwt == null) return null;

        if (DateTime.Now.AddMinutes(10) > jwt.Expire) return IssueToken(deviceCode, _setting);

        return null;
    }

    /// <summary>
    /// 写设备历史
    /// </summary>
    /// <param name="device"></param>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="remark"></param>
    /// <param name="ip"></param>
    public void WriteHistory(Device device, String action, Boolean success, String remark, String ip)
    {
        var traceId = DefaultSpan.Current?.TraceId;
        var hi = DeviceHistory.Create(device ?? Current, action, success, remark, Environment.MachineName, ip, traceId);
    }
    #endregion
}