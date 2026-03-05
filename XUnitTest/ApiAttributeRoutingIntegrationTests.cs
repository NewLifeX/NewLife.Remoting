using System;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>ApiAttribute路由与访问控制集成测试</summary>
/// <remarks>
/// 验证 [Api] 特性在控制器级和方法级的路由重命名、访问过滤、
/// 大小写不敏感匹配以及通配路由等行为。
/// </remarks>
public class ApiAttributeRoutingIntegrationTests : DisposeBase
{
    private readonly ApiServer _Server;
    private readonly Int32 _Port;

    public ApiAttributeRoutingIntegrationTests()
    {
        _Server = new ApiServer(0)
        {
            Log = XTrace.Log,
            ShowError = true,
        };
        _Server.Register<RenamedController>();
        _Server.Register<SelectiveApiController>();
        _Server.Register<NoApiAttrController>();
        _Server.Start();

        _Port = _Server.Port;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _Server.TryDispose();
    }

    #region 控制器级路由重命名
    [Fact(DisplayName = "ApiAttribute_控制器级路由重命名")]
    public async Task ControllerLevelRenameTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 控制器标记 [Api("Custom")]，方法标记 [Api("Ping")]
        // 注册为 Custom/Ping
        var result = await client.InvokeAsync<String>("Custom/Ping");
        Assert.Equal("Pong", result);
    }

    [Fact(DisplayName = "ApiAttribute_原始控制器名不可访问")]
    public async Task OriginalNameNotAccessibleTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 原始名 Renamed/Ping 应返回404
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<String>("Renamed/Ping"));

        Assert.Equal(404, ex.Code);
    }

    [Fact(DisplayName = "ApiAttribute_方法级路由重命名")]
    public async Task MethodLevelRenameTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 方法标记 [Api("Health")]，应通过 Custom/Health 访问
        var result = await client.InvokeAsync<String>("Custom/Health");
        Assert.Equal("OK", result);
    }

    [Fact(DisplayName = "ApiAttribute_方法原始名不可访问")]
    public async Task MethodOriginalNameNotAccessibleTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 原始方法名 Custom/HealthCheck 应返回404
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<String>("Custom/HealthCheck"));

        Assert.Equal(404, ex.Code);
    }
    #endregion

    #region 选择性暴露（requireApi模式）
    [Fact(DisplayName = "ApiAttribute_requireApi仅暴露标记方法")]
    public async Task SelectiveExposureTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 有 [Api] 标记的方法可访问
        var result = await client.InvokeAsync<String>("Selective/Visible");
        Assert.Equal("CanSee", result);
    }

    [Fact(DisplayName = "ApiAttribute_requireApi未标记方法不可访问")]
    public async Task UnmarkedMethodNotAccessibleTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 类上有 [Api]，未标记 [Api] 的公开方法不暴露
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<String>("Selective/Hidden"));

        Assert.Equal(404, ex.Code);
    }

    [Fact(DisplayName = "ApiAttribute_API列表仅包含标记方法")]
    public async Task ApiListOnlyMarkedMethodsTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var apis = await client.InvokeAsync<String[]>("Api/All");
        Assert.NotNull(apis);

        // Selective 控制器中只有 Visible 应出现
        Assert.Contains(apis, a => a.Contains("Selective/Visible"));
        Assert.DoesNotContain(apis, a => a.Contains("Selective/Hidden"));
    }
    #endregion

    #region 无ApiAttribute的普通控制器（全部公有方法暴露）
    [Fact(DisplayName = "无ApiAttribute_所有公有方法都暴露")]
    public async Task NoApiAttrAllPublicExposedTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        // 无 [Api] 特性的控制器，所有公有方法暴露
        var r1 = await client.InvokeAsync<String>("NoApiAttr/MethodA");
        var r2 = await client.InvokeAsync<String>("NoApiAttr/MethodB");

        Assert.Equal("A", r1);
        Assert.Equal("B", r2);
    }

    [Fact(DisplayName = "无ApiAttribute_API列表包含所有公有方法")]
    public async Task NoApiAttrAllInListTest()
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var apis = await client.InvokeAsync<String[]>("Api/All");
        Assert.NotNull(apis);

        Assert.Contains(apis, a => a.Contains("NoApiAttr/MethodA"));
        Assert.Contains(apis, a => a.Contains("NoApiAttr/MethodB"));
    }
    #endregion

    #region 路由大小写不敏感
    [Theory(DisplayName = "路由大小写不敏感")]
    [InlineData("NoApiAttr/MethodA")]
    [InlineData("noapiattr/methoda")]
    [InlineData("NOAPIATTR/METHODA")]
    [InlineData("NoApiAttr/methoda")]
    public async Task CaseInsensitiveRoutingTest(String action)
    {
        using var client = new ApiClient($"tcp://127.0.0.1:{_Port}");

        var result = await client.InvokeAsync<String>(action);
        Assert.Equal("A", result);
    }
    #endregion

    #region 通配路由
    [Fact(DisplayName = "通配路由_匹配未注册的方法")]
    public async Task WildcardRoutingTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server.Register<WildcardController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        // WildcardController 注册了 Wildcard/* 通配
        var result = await client.InvokeAsync<String>("Wildcard/AnyMethod");
        Assert.NotNull(result);
        Assert.StartsWith("Wildcard", result);
    }

    [Fact(DisplayName = "通配路由_具名路由优先于通配")]
    public async Task NamedRouteOverWildcardTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server.Register<WildcardController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        // Exact 方法已单独注册，应优先命中
        var result = await client.InvokeAsync<String>("Wildcard/Exact");
        Assert.Equal("ExactMatch", result);
    }
    #endregion

    #region 注册单个方法
    [Fact(DisplayName = "注册单个方法_仅暴露指定方法")]
    public async Task RegisterSingleMethodTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };

        var ctrl = new MultiMethodController();
        server.Register(ctrl, "MethodA");
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        // MethodA 可访问
        var result = await client.InvokeAsync<String>("MultiMethod/MethodA");
        Assert.Equal("A", result);

        // MethodB 不可访问
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => client.InvokeAsync<String>("MultiMethod/MethodB"));
        Assert.Equal(404, ex.Code);
    }
    #endregion

    #region 注册实例vs类型
    [Fact(DisplayName = "注册类型_每次请求新建实例")]
    public async Task RegisterTypeNewInstanceTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };
        server.Register<StatefulController>();
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        // 注册类型时每次创建新实例，计数不累加
        var r1 = await client.InvokeAsync<Int32>("Stateful/Increment");
        var r2 = await client.InvokeAsync<Int32>("Stateful/Increment");

        Assert.Equal(1, r1);
        Assert.Equal(1, r2); // 每次新实例，不累加
    }

    [Fact(DisplayName = "注册实例_共享状态累加")]
    public async Task RegisterInstanceSharedStateTest()
    {
        using var server = new ApiServer(0) { Log = XTrace.Log, ShowError = true };

        var ctrl = new StatefulController();
        server.Register(ctrl, null);
        server.Start();

        using var client = new ApiClient($"tcp://127.0.0.1:{server.Port}");

        // 注册实例时共享状态
        var r1 = await client.InvokeAsync<Int32>("Stateful/Increment");
        var r2 = await client.InvokeAsync<Int32>("Stateful/Increment");
        var r3 = await client.InvokeAsync<Int32>("Stateful/Increment");

        Assert.Equal(1, r1);
        Assert.Equal(2, r2);
        Assert.Equal(3, r3);
    }
    #endregion

    #region 辅助类
    [Api("Custom")]
    class RenamedController
    {
        [Api("Ping")]
        public String Ping() => "Pong";

        [Api("Health")]
        public String HealthCheck() => "OK";
    }

    [Api("Selective")]
    class SelectiveApiController
    {
        [Api("Visible")]
        public String Visible() => "CanSee";

        // 无 [Api] 标记，在 requireApi 模式下不暴露
        public String Hidden() => "CantSee";
    }

    class NoApiAttrController
    {
        public String MethodA() => "A";
        public String MethodB() => "B";
    }

    class WildcardController
    {
        public String Exact() => "ExactMatch";

        [Api("Wildcard/*")]
        public String CatchAll() => "Wildcard";
    }

    class MultiMethodController
    {
        public String MethodA() => "A";
        public String MethodB() => "B";
    }

    class StatefulController
    {
        private Int32 _count;

        public Int32 Increment() => ++_count;
    }
    #endregion
}
