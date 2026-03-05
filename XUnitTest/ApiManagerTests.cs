using System;
using System.ComponentModel;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest;

/// <summary>ApiManager单元测试</summary>
public class ApiManagerTests
{
    /// <summary>测试用控制器</summary>
    public class TestController
    {
        public String Hello(String name) => $"Hello {name}";

        public Int32 Add(Int32 a, Int32 b) => a + b;

        public void DoNothing() { }
    }

    /// <summary>带Api特性的控制器</summary>
    [Api("Test")]
    public class ApiTestController
    {
        [Api("SayHi")]
        public String SayHi() => "Hi";

        // 没有ApiAttribute的方法不应被注册
        public String Hidden() => "hidden";
    }

    [Fact]
    [DisplayName("注册泛型控制器")]
    public void Register_Generic()
    {
        var manager = new ApiManager(null!);
        manager.Register<TestController>();

        Assert.True(manager.Services.Count > 0);
        Assert.NotNull(manager.Find("Test/Hello"));
        Assert.NotNull(manager.Find("Test/Add"));
        Assert.NotNull(manager.Find("Test/DoNothing"));
    }

    [Fact]
    [DisplayName("注册对象控制器")]
    public void Register_Object()
    {
        var manager = new ApiManager(null!);
        var ctrl = new TestController();
        manager.Register(ctrl, null);

        Assert.True(manager.Services.Count > 0);
        Assert.NotNull(manager.Find("Test/Hello"));
    }

    [Fact]
    [DisplayName("注册对象单方法")]
    public void Register_SingleMethod()
    {
        var manager = new ApiManager(null!);
        var ctrl = new TestController();
        manager.Register(ctrl, "Hello");

        Assert.NotNull(manager.Find("Test/Hello"));
    }

    [Fact]
    [DisplayName("注册空控制器抛异常")]
    public void Register_NullController()
    {
        var manager = new ApiManager(null!);

        Assert.Throws<ArgumentNullException>(() => manager.Register(null!, null));
    }

    [Fact]
    [DisplayName("注册不存在方法抛异常")]
    public void Register_InvalidMethod()
    {
        var manager = new ApiManager(null!);
        var ctrl = new TestController();

        Assert.Throws<ArgumentOutOfRangeException>(() => manager.Register(ctrl, "NonExistMethod"));
    }

    [Fact]
    [DisplayName("查找不存在的Action")]
    public void Find_NotFound()
    {
        var manager = new ApiManager(null!);

        Assert.Null(manager.Find("NoSuchAction"));
    }

    [Fact]
    [DisplayName("查找大小写不敏感")]
    public void Find_CaseInsensitive()
    {
        var manager = new ApiManager(null!);
        manager.Register<TestController>();

        Assert.NotNull(manager.Find("test/hello"));
        Assert.NotNull(manager.Find("TEST/HELLO"));
    }

    [Fact]
    [DisplayName("通配符匹配")]
    public void Find_Wildcard()
    {
        var manager = new ApiManager(null!);
        var action = new ApiAction(typeof(TestController).GetMethod("Hello")!, typeof(TestController));
        manager.Services["Test/*"] = action;

        // 局部通配符
        var result = manager.Find("Test/Anything");
        Assert.NotNull(result);
    }

    [Fact]
    [DisplayName("全局通配符匹配")]
    public void Find_GlobalWildcard()
    {
        var manager = new ApiManager(null!);
        var action = new ApiAction(typeof(TestController).GetMethod("Hello")!, typeof(TestController));
        manager.Services["*"] = action;

        var result = manager.Find("Any/Action");
        Assert.NotNull(result);
    }

    [Fact]
    [DisplayName("Add方法")]
    public void Add_Method()
    {
        var manager = new ApiManager(null!);
        var action = new ApiAction(typeof(TestController).GetMethod("Hello")!, typeof(TestController));
        manager.Add(action);

        Assert.NotNull(manager.Find(action.Name));
    }

    [Fact]
    [DisplayName("带Api特性的控制器只注册标记方法")]
    public void Register_WithApiAttribute()
    {
        var manager = new ApiManager(null!);
        manager.Register<ApiTestController>();

        // SayHi有ApiAttribute标记应被注册
        Assert.NotNull(manager.Find("Test/SayHi"));
        // Hidden没有ApiAttribute标记不应被注册
        Assert.Null(manager.Find("Test/Hidden"));
    }

    [Fact]
    [DisplayName("CreateController使用已有对象")]
    public void CreateController_ExistingController()
    {
        var manager = new ApiManager(null!);
        var ctrl = new TestController();
        manager.Register(ctrl, null);

        var action = manager.Find("Test/Hello")!;
        var result = manager.CreateController(action, null!);

        Assert.Same(ctrl, result);
    }

    [Fact]
    [DisplayName("CreateController创建新实例")]
    public void CreateController_NewInstance()
    {
        var manager = new ApiManager(null!);
        manager.Register<TestController>();

        var action = manager.Find("Test/Hello")!;
        var result = manager.CreateController(action, null!);

        Assert.NotNull(result);
        Assert.IsType<TestController>(result);
    }
}
