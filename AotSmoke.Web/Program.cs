using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Serialization;

namespace AotSmoke.Web;

/// <summary>NewLife.Remoting.Extensions 最小 API 宿主的 NativeAOT 冒烟</summary>
/// <remarks>
/// <para>进程内启动最小 API 宿主（<c>AddRemotingCore</c> + <c>MapRemoting</c>），再用 HTTP/WebSocket 客户端逐场景验证，
/// JIT 与 NativeAOT 双跑，逐场景输出 PASS/FAIL，非 0 退出码即失败。</para>
/// <para>设备服务使用内存实现，不依赖 XCode：XCode 的运行期 AOT 可用性见《功能清单》EXT-6。</para>
/// <para>复现方法见 Doc/AOT兼容性分析.md</para>
/// </remarks>
internal static class Program
{
    #region 字段
    private static readonly List<String> _failedNames = [];
    private static Int32 _total;
    private static Int32 _failed;
    private static Int32 _port;
    private static String _token = "";
    private static MemoryDeviceService _device = null!;
    #endregion

    #region 入口
    private static async Task<Int32> Main()
    {
        Console.WriteLine($"=== NewLife.Remoting.Extensions 最小 API AOT 冒烟 === IsDynamicCodeSupported={RuntimeFeature.IsDynamicCodeSupported}");

        _port = FindFreePort();
        _device = new MemoryDeviceService();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseUrls($"http://127.0.0.1:{_port}");
        builder.Logging.ClearProviders();

        builder.Services.AddRemotingCore(new SmokeTokenSetting { TokenSecret = "HS256:AotSmokeWeb_secret_1234567890" });
        builder.Services.AddSingleton<IDeviceService>(_device);
        builder.Services.AddSingleton<ITracer, DefaultTracer>();

        var app = builder.Build();
        app.UseRemoting();
        app.MapRemoting();

        await app.StartAsync().ConfigureAwait(false);
        Console.WriteLine($"最小 API 宿主已启动：http://127.0.0.1:{_port}");

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
        var sessionManager = app.Services.GetRequiredService<ISessionManager>();

        try
        {
            await Run("01 登录颁发令牌", () => Login(client));
            await Run("02 心跳装载设备", () => Ping(client));
            await Run("03 GET 心跳无请求体", () => PingGet(client));
            await Run("04 事件上报计数", () => PostEvents(client));
            await Run("05 匿名指令下发与回执", () => SendCommand(client));
            await Run("06 命令回执入库", () => CommandReply(client));
            await Run("07 升级返回绝对地址", () => Upgrade(client));
            await Run("08 指令下发缺少编码报错", () => SendCommandWithoutCode(client));
            await Run("09 无令牌返回401", () => Unauthorized(client));
            await Run("10 WebSocket 长连接接收指令", () => NotifyWebSocket(client, sessionManager));
            await Run("11 注销后令牌立即失效", () => Logout(client));
        }
        finally
        {
            await app.StopAsync().ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
        }

        Console.WriteLine(_failed == 0
            ? $"=== 全部通过 {_total}/{_total} ==="
            : $"=== 失败 {_failed}/{_total}：{String.Join(", ", _failedNames)} ===");

        return _failed;
    }

    private static async Task Run(String name, Func<Task> action)
    {
        _total++;
        try
        {
            await action().ConfigureAwait(false);
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            _failedNames.Add(name);
            Console.WriteLine($"FAIL {name} :: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>取一个空闲 TCP 端口</summary>
    private static Int32 FindFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));

        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
    #endregion

    #region 场景
    private static async Task Login(HttpClient client)
    {
        var text = await PostAsync(client, "Login", new Dictionary<String, Object?>
        {
            ["Code"] = "dev-aot",
            ["Secret"] = "abc",
        }).ConfigureAwait(false);

        var rs = Decode<LoginResponse>(text) ?? throw new Exception("登录响应为空");
        if (rs.Token.IsNullOrEmpty()) throw new Exception("未颁发访问令牌");
        if (rs.Name != "AOT设备") throw new Exception($"设备名称错误：{rs.Name}");

        _token = rs.Token!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    private static async Task Ping(HttpClient client)
    {
        var text = await PostAsync(client, "Ping", new Dictionary<String, Object?> { ["State"] = "aot" }).ConfigureAwait(false);

        var rs = Decode<PingResponse>(text) ?? throw new Exception("心跳响应为空");
        if (rs.Period != 60) throw new Exception($"心跳周期错误：{rs.Period}");
        if (rs.ServerTime <= 0) throw new Exception("缺少服务器时间");
    }

    private static async Task PingGet(HttpClient client)
    {
        using var msg = await client.GetAsync("Ping").ConfigureAwait(false);
        var text = await msg.Content.ReadAsStringAsync().ConfigureAwait(false);

        var rs = Decode<PingResponse>(text) ?? throw new Exception("GET 心跳响应为空");
        if (rs.Period != 60) throw new Exception($"心跳周期错误：{rs.Period}");
    }

    private static async Task PostEvents(HttpClient client)
    {
        var before = _device.Events.Count;

        var count = Decode<Int32>(await PostAsync(client, "PostEvents", new Object[]
        {
            new Dictionary<String, Object?> { ["Type"] = "info", ["Name"] = "LightOpen", ["Remark"] = "开灯" },
            new Dictionary<String, Object?> { ["Type"] = "alert", ["Name"] = "HighTemp", ["Remark"] = "高温" },
        }).ConfigureAwait(false));

        if (count != 2) throw new Exception($"上报数量错误：{count}");
        if (_device.Events.Count - before != 2) throw new Exception("服务端未收到事件");
    }

    private static async Task SendCommand(HttpClient client)
    {
        var auth = client.DefaultRequestHeaders.Authorization;
        client.DefaultRequestHeaders.Authorization = null;
        try
        {
            var text = await PostAsync(client, "SendCommand", new Dictionary<String, Object?>
            {
                ["Code"] = "dev-aot",
                ["Command"] = "Reboot",
            }).ConfigureAwait(false);

            var rs = Decode<CommandReplyModel>(text) ?? throw new Exception("指令下发响应为空");
            if (rs.Id != 1) throw new Exception($"回执编号错误：{rs.Id}");
            if (rs.Data != "Reboot") throw new Exception($"回执数据错误：{rs.Data}");
        }
        finally
        {
            client.DefaultRequestHeaders.Authorization = auth;
        }
    }

    private static async Task CommandReply(HttpClient client)
    {
        var count = Decode<Int32>(await PostAsync(client, "CommandReply", new Dictionary<String, Object?>
        {
            ["Id"] = 77,
            ["Status"] = "已完成",
            ["Remark"] = "ok",
        }).ConfigureAwait(false));

        if (count != 1) throw new Exception($"回执处理数量错误：{count}");
        if (!_device.Replies.Any(e => e.Id == 77)) throw new Exception("服务端未记录回执");
    }

    private static async Task Upgrade(HttpClient client)
    {
        var text = await PostAsync(client, "Upgrade").ConfigureAwait(false);

        var rs = Decode<UpgradeInfo>(text) ?? throw new Exception("升级响应为空");
        if (rs.Version != "9.9.9") throw new Exception($"版本号错误：{rs.Version}");

        var expect = $"http://127.0.0.1:{_port}/";
        if (rs.Source.IsNullOrEmpty() || !rs.Source.StartsWith(expect)) throw new Exception($"更新地址未转绝对路径：{rs.Source}");
    }

    private static async Task SendCommandWithoutCode(HttpClient client)
    {
        var auth = client.DefaultRequestHeaders.Authorization;
        client.DefaultRequestHeaders.Authorization = null;
        try
        {
            var json = JsonHelper.ToJson(new Dictionary<String, Object?> { ["Command"] = "Reboot" }, JsonHelper.Default.Options);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var msg = await client.PostAsync("SendCommand", content).ConfigureAwait(false);
            var text = await msg.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!text.Contains("必须指定编码")) throw new Exception($"未返回参数校验错误：{text}");
        }
        finally
        {
            client.DefaultRequestHeaders.Authorization = auth;
        }
    }

    private static async Task Unauthorized(HttpClient client)
    {
        var auth = client.DefaultRequestHeaders.Authorization;
        client.DefaultRequestHeaders.Authorization = null;
        try
        {
            var text = await PostAsync(client, "Ping").ConfigureAwait(false);

            if (!text.Contains("\"code\":401")) throw new Exception($"未返回401业务码：{text}");
        }
        finally
        {
            client.DefaultRequestHeaders.Authorization = auth;
        }
    }

    private static async Task NotifyWebSocket(HttpClient client, ISessionManager sessionManager)
    {
        using var ws = new ClientWebSocket();
        using var source = new CancellationTokenSource(15_000);

        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{_port}/Notify?Token={_token}"), source.Token).ConfigureAwait(false);

        // 等待会话注册到会话管理器
        for (var i = 0; i < 50 && sessionManager.Get("dev-aot") == null; i++)
        {
            await Task.Delay(100, source.Token).ConfigureAwait(false);
        }
        if (sessionManager.Get("dev-aot") == null) throw new Exception("设备会话未注册");

        // 服务端下发命令，客户端应从 WebSocket 收到命令文本
        var command = new CommandModel { Id = 9, Command = "Restart", Argument = "svc" };
        await sessionManager.PublishAsync("dev-aot", command, null, 0, source.Token).ConfigureAwait(false);

        var buf = new Byte[4096];
        var data = await ws.ReceiveAsync(new ArraySegment<Byte>(buf), source.Token).ConfigureAwait(false);
        var text = buf.ToStr(null, 0, data.Count);

        if (!text.Contains("Restart")) throw new Exception($"未收到下发命令：{text}");

        // 服务端会话销毁不等待关闭握手，客户端直接中断连接
        ws.Abort();
    }

    private static async Task Logout(HttpClient client)
    {
        var text = await PostAsync(client, "Logout?reason=aot").ConfigureAwait(false);

        var rs = Decode<LogoutResponse>(text) ?? throw new Exception("注销响应为空");
        if (rs.Name != "AOT设备") throw new Exception($"注销设备名称错误：{rs.Name}");

        // 注销即吊销令牌，原令牌不可继续使用
        var after = await PostAsync(client, "Ping").ConfigureAwait(false);
        if (!after.Contains("\"code\":401")) throw new Exception($"注销后令牌仍可用：{after}");
    }
    #endregion

    #region 辅助
    /// <summary>发送 Json 请求</summary>
    private static async Task<String> PostAsync(HttpClient client, String action, Object? args = null)
    {
        var json = JsonHelper.ToJson(args ?? new Dictionary<String, Object?>(), JsonHelper.Default.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var msg = await client.PostAsync(action, content).ConfigureAwait(false);

        return await msg.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    /// <summary>按设备端同款逻辑拆解 code/data 信封</summary>
    private static T? Decode<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] T>(String text)
        => ApiHelper.ProcessResponse<T>(text, null, "data");
    #endregion

    /// <summary>冒烟用令牌设置</summary>
    private class SmokeTokenSetting : ITokenSetting
    {
        public String TokenSecret { get; set; } = null!;

        public Int32 TokenExpire { get; set; } = 3600;

        public Int32 SessionTimeout { get; set; } = 600;

        public Boolean AutoRegister { get; set; } = true;
    }
}
