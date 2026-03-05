using System;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>多控制器路由与高级调用集成测试</summary>
/// <remarks>
/// 验证 ApiServer 注册多个控制器时的命名空间路由、
/// 异步方法返回 Task 的处理、IApi 接口注入 Session 等行为。
/// </remarks>
public class MultiControllerRoutingTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public MultiControllerRoutingTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<UserController>();
        _Server.Register<OrderController>();
        _Server.Register<AsyncController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 多控制器命名空间路由
    [Fact(DisplayName = "多控制器_按命名空间路由")]
    public async Task MultiControllerNamespaceRoutingTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // User 控制器
        var userName = await client.InvokeAsync<String>("User/GetName", new { id = 1 });
        Assert.Equal("User_1", userName);

        // Order 控制器
        var orderInfo = await client.InvokeAsync<String>("Order/GetOrder", new { orderId = 100 });
        Assert.Equal("Order_100", orderInfo);
    }

    [Fact(DisplayName = "多控制器_API列表包含所有服务")]
    public async Task MultiControllerApiListTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var apis = await client.InvokeAsync<String[]>("Api/All");
        Assert.NotNull(apis);

        // 应包含 User 和 Order 控制器的方法
        Assert.Contains(apis, a => a.Contains("User/GetName"));
        Assert.Contains(apis, a => a.Contains("Order/GetOrder"));
        Assert.Contains(apis, a => a.Contains("Async/GetValue"));
    }

    [Fact(DisplayName = "多控制器_调用不存在的控制器")]
    public async Task NonExistentControllerTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("NonExistent/Action"));

        Assert.Equal(404, ex.Code);
    }

    [Fact(DisplayName = "多控制器_调用不存在的方法")]
    public async Task NonExistentMethodTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("User/NonExistentMethod"));

        Assert.Equal(404, ex.Code);
    }
    #endregion

    #region 异步控制器方法
    [Fact(DisplayName = "异步控制器_Task返回值")]
    public async Task AsyncControllerTaskReturnTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<Int32>("Async/GetValue");
        Assert.Equal(42, result);
    }

    [Fact(DisplayName = "异步控制器_TaskString返回值")]
    public async Task AsyncControllerTaskStringReturnTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<String>("Async/GetMessage", new { name = "World" });
        Assert.Equal("Hello, World!", result);
    }

    [Fact(DisplayName = "异步控制器_Task异常传播")]
    public async Task AsyncControllerExceptionTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<Object>("Async/ThrowAsync"));

        Assert.Equal(500, ex.Code);
    }
    #endregion

    #region IApi接口Session注入
    [Fact(DisplayName = "IApi接口_Session注入")]
    public async Task IApiSessionInjectionTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<Boolean>("User/HasSession");
        Assert.True(result);
    }
    #endregion

    #region 注册实例 vs 注册类型
    [Fact(DisplayName = "注册实例_共享控制器状态")]
    public async Task RegisterInstanceSharedStateTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };

        var counter = new CounterController();
        server.Register(counter, null);
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        // 实例注册共享状态
        var r1 = await client.InvokeAsync<Int32>("Counter/Increment");
        var r2 = await client.InvokeAsync<Int32>("Counter/Increment");
        var r3 = await client.InvokeAsync<Int32>("Counter/Increment");

        Assert.Equal(1, r1);
        Assert.Equal(2, r2);
        Assert.Equal(3, r3);
    }
    #endregion

    #region IPacket参数和返回值
    [Fact(DisplayName = "IPacket_二进制参数和返回")]
    public async Task PacketParameterAndReturnTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server.Register<PacketController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var data = new Byte[256];
        for (var i = 0; i < data.Length; i++) data[i] = (Byte)(i & 0xFF);

        var result = await client.InvokeAsync<Packet>("Packet/Echo", data);
        Assert.NotNull(result);
        Assert.Equal(data.Length, result.Total);
        Assert.True(data.SequenceEqual(result.ToArray()));
    }

    [Fact(DisplayName = "IPacket_XOR变换")]
    public async Task PacketTransformTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server.Register<PacketController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        var data = new Byte[128];
        Array.Fill(data, (Byte)0xAA);

        var result = await client.InvokeAsync<Packet>("Packet/Xor", data);
        Assert.NotNull(result);
        Assert.Equal(data.Length, result.Total);

        var expected = data.Select(b => (Byte)(b ^ 0xFF)).ToArray();
        Assert.True(expected.SequenceEqual(result.ToArray()));
    }
    #endregion

    #region 辅助类
    class UserController : IApi
    {
        public IApiSession? Session { get; set; }

        public String GetName(Int32 id) => $"User_{id}";

        public Boolean HasSession() => Session != null;
    }

    class OrderController
    {
        public String GetOrder(Int32 orderId) => $"Order_{orderId}";
    }

    class AsyncController
    {
        public async Task<Int32> GetValue()
        {
            await Task.Delay(10);
            return 42;
        }

        public async Task<String> GetMessage(String name)
        {
            await Task.Delay(10);
            return $"Hello, {name}!";
        }

        public async Task<String> ThrowAsync()
        {
            await Task.Delay(10);
            throw new InvalidOperationException("异步异常");
        }
    }

    class CounterController
    {
        private Int32 _count;

        public Int32 Increment() => ++_count;
    }

    class PacketController
    {
        public IPacket Echo(IPacket pk) => pk;

        public IPacket Xor(IPacket pk)
        {
            var buf = pk.ReadBytes();
            for (var i = 0; i < buf.Length; i++) buf[i] ^= 0xFF;
            return (ArrayPacket)buf;
        }
    }
    #endregion
}
