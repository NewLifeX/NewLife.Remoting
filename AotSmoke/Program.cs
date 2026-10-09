using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using NewLife;
using NewLife.Data;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Serialization;

namespace AotSmoke;

/// <summary>NewLife.Remoting NativeAOT 冒烟。JIT 与 NativeAOT 双跑，逐场景输出 PASS/FAIL，非 0 退出码即失败</summary>
/// <remarks>
/// <para>参数一律使用 <see cref="Dictionary{TKey,TValue}"/> 或带 <c>[JsonSerializable]</c> 标记的 DTO 承载：
/// NativeAOT 下匿名类型与未标记类型的公开成员会被裁剪，运行时类型 JSON 序列化将得到空对象。</para>
/// <para>受限环境下 NewLife.Core 的 <c>MachineInfo</c> 首次探测可能向 stderr 打印系统错误后按空值降级，
/// 属环境行为，不影响场景结果。</para>
/// <para>复现方法见 Doc/AOT兼容性分析.md</para>
/// </remarks>
internal static class Program
{
    #region 字段
    private static readonly List<String> _failedNames = [];
    private static Int32 _total;
    private static Int32 _failed;
    #endregion

    #region 入口
    private static async Task<Int32> Main()
    {
        Console.WriteLine($"=== NewLife.Remoting AOT 冒烟 === IsDynamicCodeSupported={RuntimeFeature.IsDynamicCodeSupported}");

        await Run("01 泛型注册与服务发现", GenericRegister);
        await Run("02 TCP 同步调用往返", TcpSyncInvoke);
        await Run("03 TCP 异步调用与复杂参数", TcpAsyncInvoke);
        await Run("04 二进制 Packet 参数与返回", PacketChannel);
        await Run("05 值类型参数默认值与缺参", ParameterDefaults);
        await Run("06 异步动作 Task/Task<T>", AsyncActions);
        await Run("07 流式 IAsyncEnumerable<string>", StreamingReference);
        await Run("08 流式 IAsyncEnumerable<int>", StreamingValue);
        await Run("09 HTTP 入口往返", HttpRoundTrip);
        await Run("10 控制器依赖注入构造", ContainerInjection);
        await Run("11 控制器每请求实例化", PerRequestInstance);
        await Run("12 通配路由与 Api 重命名", WildcardRouting);
        await Run("13 源生成命中与回退双场景", GeneratorHitAndMiss);
        await Run("14 内置 DTO 运行时类型往返", DtoRoundTrip);
        await Run("15 AOT 参数承载形态", ArgumentForms);
        await Run("16 客户端基类登录/心跳/事件/注销往返", ClientBaseRoundTrip);
        await Run("17 单方法注册的静态委托与裁剪保留", SingleMethodRegister);

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
            await action();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            _failedNames.Add(name);
            Console.WriteLine($"FAIL {name} :: {ex.GetType().Name}: {ex.Message}");
        }
    }
    #endregion

    #region 场景
    private static Task GenericRegister()
    {
        using var server = CreateTcpServer();
        var act = server.Manager.Find("Smoke/Sum") ?? throw new Exception("未找到动作 Smoke/Sum");
        if (act.FastInvoker == null) throw new Exception("动作调用委托为空，源生成器未命中");

        return Task.CompletedTask;
    }

    private static async Task TcpSyncInvoke()
    {
        using var server = CreateTcpServer();
        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var rs = client.Invoke<Int32>("Smoke/Sum", Args(("a", 3), ("b", 4)));
        if (rs != 7) throw new Exception($"结果错误 {rs} != 7");
    }

    private static async Task TcpAsyncInvoke()
    {
        using var server = CreateTcpServer();
        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var rs = await client.InvokeAsync<String>("Smoke/Greet", Args(("name", "AOT")));
        if (rs != "Hello, AOT!") throw new Exception($"结果错误 {rs}");
    }

    private static async Task PacketChannel()
    {
        using var server = CreateTcpServer();
        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var rs = client.Invoke<IPacket>("Smoke/Reverse", (ArrayPacket)"abc".GetBytes());
        var str = rs?.ToStr();
        if (str != "cba") throw new Exception($"结果错误 {str}");
    }

    private static async Task ParameterDefaults()
    {
        using var server = CreateTcpServer();
        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        // 只传 a，b 取默认值 10，tag 取默认值 "x"
        var rs = await client.InvokeAsync<String>("Smoke/Bump", Args(("a", 5)));
        if (rs != "15:x") throw new Exception($"结果错误 {rs}");
    }

    private static async Task AsyncActions()
    {
        using var server = CreateTcpServer();
        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var rs = await client.InvokeAsync<Int32>("Smoke/AddAsync", Args(("a", 10), ("b", 20)));
        if (rs != 30) throw new Exception($"Task<T> 结果错误 {rs}");

        var ts = await client.InvokeAsync<Object>("Smoke/FireAsync");
        _ = ts;
    }

    private static async Task StreamingReference()
    {
        using var server = CreateTcpServer();
        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var list = new List<String>();
        await foreach (var item in client.InvokeStreamAsync<String>("Smoke/Logs", Args(("count", 3))))
            list.Add(item);

        if (list.Count != 3 || list[0] != "line0") throw new Exception($"结果错误 [{String.Join(",", list)}]");
    }

    private static async Task StreamingValue()
    {
        using var server = CreateTcpServer();
        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var list = new List<Int32>();
        await foreach (var item in client.InvokeStreamAsync<Int32>("Smoke/Range", Args(("count", 3))))
            list.Add(item);

        if (list.Count != 3 || list[2] != 2) throw new Exception($"结果错误 [{String.Join(",", list)}]");
    }

    private static async Task HttpRoundTrip()
    {
        // HTTP 专用模式下 Port=0，启动后由底层 HTTP 服务器回写实际端口
        using var server = new ApiServer(new NetUri(NetType.Http, "*", 0))
        {
            ShowError = true,
        };
        server.Register<SmokeController>();
        server.Start();

        IApiClient client = new ApiHttpClient($"http://127.0.0.1:{server.Port}");
        using var _ = client as IDisposable;

        // 无参动作：验证 HTTP 响应封套在裁剪后仍完整
        var all = await client.InvokeAsync<String[]>("Api/All");
        if (all == null || all.Length == 0) throw new Exception("Api/All 为空，HTTP 响应封套可能被裁剪");

        var rs = await client.InvokeAsync<Int32>("Smoke/Sum", Args(("a", 100), ("b", 200)));
        if (rs != 300) throw new Exception($"结果错误 {rs}");
    }

    private static async Task ContainerInjection()
    {
        var ioc = new ObjectContainer();
        ioc.AddSingleton<IClock>(new Clock());
        ioc.AddTransient<IClockService, ClockService>();

        using var server = CreateTcpServer(ioc.BuildServiceProvider());
        server.Register<ClockController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
        var rs = await client.InvokeAsync<String>("Clock/Now");
        if (rs == null || rs.Length == 0) throw new Exception("注入构造的控制器返回为空");
    }

    private static async Task PerRequestInstance()
    {
        using var server = CreateTcpServer();
        server.Register<StatefulController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
        var rs1 = await client.InvokeAsync<Int32>("Stateful/Increment");
        var rs2 = await client.InvokeAsync<Int32>("Stateful/Increment");

        // 每次请求新建控制器实例，计数各自从 1 开始
        if (rs1 != 1 || rs2 != 1) throw new Exception($"结果错误 {rs1},{rs2}");
    }

    private static async Task WildcardRouting()
    {
        using var server = CreateTcpServer();
        server.Register<WildcardController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var exact = await client.InvokeAsync<String>("Wildcard/Exact");
        if (exact != "ExactMatch") throw new Exception($"具名路由结果错误 {exact}");

        var any = await client.InvokeAsync<String>("Wildcard/AnyOther");
        if (any != "Wildcard") throw new Exception($"通配路由结果错误 {any}");
    }

    private static async Task GeneratorHitAndMiss()
    {
        // 公开控制器：源生成器命中
        if (!ApiActionTable.TryGetInvoker(typeof(SmokeController), "Sum", out var invoker) || invoker == null)
            throw new Exception("公开控制器未命中源生成器注册");

        // 私有嵌套控制器在生成代码中不可见，不命中，运行期回退反射路径
        if (ApiActionTable.TryGetInvoker(typeof(HiddenController), "Hidden", out _))
            throw new Exception("私有嵌套控制器不应命中");

        // 值类型流式泵：闭集静态扎根
        if (!ApiActionTable.TryGetStreamPump(typeof(Int32), out _))
            throw new Exception("Int32 流式泵未注册");

        using var server = CreateTcpServer();
        server.Register<HiddenController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
        var rs = await client.InvokeAsync<Int32>("Hidden/Hidden");
        if (rs != 1) throw new Exception($"回退反射调用结果错误 {rs}");
    }

    private static Task DtoRoundTrip()
    {
        var enc = new JsonEncoder { JsonHost = JsonHelper.Default };

        var req = new LoginRequest { Code = "dev01", Secret = "s3cret", ClientId = "c1" };
        var json = JsonHelper.ToJson(req);
        var raw = JsonHelper.Default.Parse(json) ?? throw new Exception("解析为空");

        var back = enc.Convert(raw, typeof(LoginRequest)) as LoginRequest ?? throw new Exception("转换失败");
        if (back.Code != "dev01" || back.Secret != "s3cret") throw new Exception($"往返结果错误 {back.Code}/{back.Secret}");

        return Task.CompletedTask;
    }

    private static async Task ArgumentForms()
    {
        using var server = CreateTcpServer();
        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        // 形态一：字典。走反射无关的字典序列化路径
        var rs1 = await client.InvokeAsync<Int32>("Smoke/Sum", Args(("a", 3), ("b", 4)));
        if (rs1 != 7) throw new Exception($"字典形态结果错误 {rs1}");

        // 形态二：带 JsonSerializable 标记的 DTO。由 NewLife.Core 源生成器落成员访问器表，
        // 并由本包源生成器的 Preserve 保留其无参构造与公开成员
        var rs2 = await client.InvokeAsync<Int32>("Smoke/SumByArgs", new SumArgs { a = 5, b = 6 });
        if (rs2 != 11) throw new Exception($"标记 DTO 形态结果错误 {rs2}");
    }

    // ClientBase 全链路：登录（DTO + 接口返回类型）→ 心跳 → 事件上报（数组参数）→ 注销（字典参数）
    private static async Task ClientBaseRoundTrip()
    {
        using var server = CreateTcpServer();
        server.Register<DeviceController>();
        server.Start();

        using var client = new SmokeNodeClient($"tcp://127.0.0.1:{server.Port}")
        {
            Code = "dev01",
            Secret = "s3cret",
        };

        var login = await client.Login("AOT");
        if (login == null || login.Token != "tok-dev01") throw new Exception($"登录响应异常 {login?.Token}");

        var ping = await client.Ping();
        if (ping == null) throw new Exception("心跳响应为空");

        var count = await client.PostEvents(new EventModel { Type = "info", Name = "boot", Remark = "ok" });
        if (count != 1) throw new Exception($"事件上报返回 {count}");

        var logout = await client.Logout("AOT");
        if (logout == null) throw new Exception("注销响应为空");
    }

    // 实例入口 Register(instance, "Method") 只注册单个方法（宽面入口，AOT 下方法可能被裁剪）。
    // 生成器对该调用点可见，为其生成强类型委托并静态引用目标方法，使其在裁剪与 AOT 下仍可调用
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "调用点对源生成器可见：生成委托并静态引用目标方法，裁剪与 AOT 下均可调用")]
    private static async Task SingleMethodRegister()
    {
        using var server = CreateTcpServer();
        server.Register(new SingleController(), "Alpha");
        server.Start();

        if (!ApiActionTable.TryGetInvoker(typeof(SingleController), "Alpha", out var invoker) || invoker == null)
            throw new Exception("单方法注册未命中源生成器");

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");
        var rs = await client.InvokeAsync<String>("Single/Alpha");
        if (rs != "Alpha!") throw new Exception($"结果错误 {rs}");
    }
    #endregion

    #region 辅助
    private static ApiServer CreateTcpServer(IServiceProvider? serviceProvider = null)
    {
        var server = new ApiServer(PickPort())
        {
            ShowError = true,
            ReuseAddress = true,
        };
        if (serviceProvider != null) server.ServiceProvider = serviceProvider;

        server.Register<SmokeController>();
        server.Start();

        return server;
    }

    private static Dictionary<String, Object?> Args(params (String Key, Object? Value)[] items)
    {
        var dic = new Dictionary<String, Object?>();
        foreach (var item in items)
            dic[item.Key] = item.Value;

        return dic;
    }

    // 取一个空闲端口，避免固定端口在 CI 上冲突
    private static Int32 PickPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));

        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    /// <summary>私有嵌套控制器。生成代码不可见，运行期回退反射调用</summary>
    private class HiddenController
    {
        /// <summary>固定返回 1</summary>
        public Int32 Hidden() => 1;
    }
    #endregion
}

#region 模型
/// <summary>求和参数。属性名与动作参数名一致，NativeAOT 下由源生成器保留成员访问器</summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(SumArgs))]
public class SumArgs
{
    /// <summary>加数一</summary>
    public Int32 a { get; set; }

    /// <summary>加数二</summary>
    public Int32 b { get; set; }
}
#endregion

#region 控制器
/// <summary>冒烟控制器。公开类型，源生成器可生成强类型调用委托</summary>
public class SmokeController
{
    /// <summary>求和</summary>
    public Int32 Sum(Int32 a, Int32 b) => a + b;

    /// <summary>以 DTO 整体入参求和</summary>
    public Int32 SumByArgs(SumArgs args) => args.a + args.b;

    /// <summary>问候</summary>
    public String Greet(String name) => $"Hello, {name}!";

    /// <summary>反转二进制</summary>
    public IPacket Reverse(IPacket pk) => (ArrayPacket)pk.ReadBytes().Reverse().ToArray();

    /// <summary>带默认值参数</summary>
    public String Bump(Int32 a, Int32 b = 10, String tag = "x") => $"{a + b}:{tag}";

    /// <summary>异步返回</summary>
    public Task<Int32> AddAsync(Int32 a, Int32 b) => Task.FromResult(a + b);

    /// <summary>异步无返回</summary>
    public async Task FireAsync() => await Task.Yield();

    /// <summary>流式返回字符串</summary>
    public async IAsyncEnumerable<String> Logs(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return $"line{i}";
        }
    }

    /// <summary>流式返回值类型</summary>
    public async IAsyncEnumerable<Int32> Range(Int32 count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}

/// <summary>依赖注入控制器</summary>
public class ClockController(IClockService clock)
{
    /// <summary>当前时间</summary>
    public String Now() => clock.Now().ToFullString();
}

/// <summary>时钟服务接口</summary>
public interface IClock
{
    /// <summary>当前时间</summary>
    DateTime Now();
}

/// <summary>时钟服务</summary>
public class Clock : IClock
{
    /// <summary>当前时间</summary>
    public DateTime Now() => DateTime.Now;
}

/// <summary>时钟业务服务</summary>
public interface IClockService
{
    /// <summary>当前时间</summary>
    DateTime Now();
}

/// <summary>时钟业务服务实现。构造注入 IClock</summary>
public class ClockService(IClock clock) : IClockService
{
    /// <summary>当前时间</summary>
    public DateTime Now() => clock.Now();
}

/// <summary>有状态控制器。验证每请求实例化</summary>
public class StatefulController
{
    private Int32 _count;

    /// <summary>自增</summary>
    public Int32 Increment() => ++_count;
}

/// <summary>通配控制器</summary>
public class WildcardController
{
    /// <summary>具名方法</summary>
    public String Exact() => "ExactMatch";

    /// <summary>通配方法</summary>
    [Api("Wildcard/*")]
    public String CatchAll() => "Wildcard";
}

/// <summary>设备接入控制器。配合 ClientBase 走完整的登录/心跳/事件/注销链路</summary>
public class DeviceController
{
    /// <summary>登录</summary>
    public LoginResponse Login(LoginRequest request) => new()
    {
        Name = request.Code,
        Token = $"tok-{request.Code}",
    };

    /// <summary>心跳</summary>
    public PingResponse Ping(PingRequest request) => new()
    {
        ServerTime = DateTime.UtcNow.ToLong(),
    };

    /// <summary>事件上报</summary>
    public Int32 PostEvents(EventModel[] events) => events.Length;

    /// <summary>注销</summary>
    public LogoutResponse Logout(String reason) => new() { Token = reason };
}

/// <summary>冒烟客户端。ClientBase 的最小实现</summary>
public class SmokeNodeClient : ClientBase
{
    /// <summary>实例化</summary>
    /// <param name="server">服务端地址</param>
    public SmokeNodeClient(String server) => Server = server;
}

/// <summary>单方法注册控制器。仅 Alpha 被注册，验证生成器为其生成委托并静态保留</summary>
public class SingleController
{
    /// <summary>被注册的方法</summary>
    public String Alpha() => "Alpha!";

    /// <summary>未注册的方法</summary>
    public String Beta() => "Beta!";
}
#endregion
