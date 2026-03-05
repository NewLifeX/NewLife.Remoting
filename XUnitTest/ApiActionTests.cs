using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using NewLife.Data;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

public class ApiActionTests
{
    // 用于测试的辅助控制器
    public class TestController
    {
        public String Hello() => "world";

        public Int32 Add(Int32 a, Int32 b) => a + b;

        public void DoVoid() { }

        public Task<String> GetAsync() => Task.FromResult("async");

        public Task DoVoidAsync() => Task.CompletedTask;
    }

    [Api("Custom")]
    public class CustomController
    {
        [Api("action/do")]
        public String DoSomething() => "done";

        public String Normal() => "normal";
    }

    [Api("")]
    public class EmptyApiController
    {
        [Api("root/action")]
        public String Action() => "ok";
    }

    [Fact]
    [DisplayName("默认构造函数")]
    public void DefaultConstructor()
    {
        var action = new ApiAction();

        Assert.Null(action.Name);
        Assert.Null(action.Type);
        Assert.Null(action.Method);
        Assert.Null(action.Controller);
        Assert.False(action.IsPacketParameter);
        Assert.False(action.IsPacketReturn);
        Assert.NotNull(action.Items);
        Assert.NotNull(action.StatProcess);
    }

    [Fact]
    [DisplayName("MethodInfo构造函数")]
    public void ConstructorWithMethod()
    {
        var method = typeof(TestController).GetMethod(nameof(TestController.Hello))!;
        var action = new ApiAction(method, typeof(TestController));

        Assert.Equal("Test/Hello", action.Name);
        Assert.Equal(typeof(TestController), action.Type);
        Assert.Equal(method, action.Method);
        Assert.True(action.IsNoParameter);
        Assert.False(action.IsPacketParameter);
        Assert.False(action.IsPacketReturn);
        Assert.NotNull(action.FastInvoker);
    }

    [Fact]
    [DisplayName("多参数方法")]
    public void Constructor_MultipleParameters()
    {
        var method = typeof(TestController).GetMethod(nameof(TestController.Add))!;
        var action = new ApiAction(method, typeof(TestController));

        Assert.Equal("Test/Add", action.Name);
        Assert.False(action.IsNoParameter);
        Assert.False(action.IsPacketParameter);
    }

    [Fact]
    [DisplayName("Void返回")]
    public void Constructor_VoidReturn()
    {
        var method = typeof(TestController).GetMethod(nameof(TestController.DoVoid))!;
        var action = new ApiAction(method, typeof(TestController));

        Assert.NotNull(action.FastInvoker);
    }

    [Fact]
    [DisplayName("异步Task返回")]
    public void Constructor_TaskReturn()
    {
        var method = typeof(TestController).GetMethod(nameof(TestController.GetAsync))!;
        var action = new ApiAction(method, typeof(TestController));

        Assert.Equal("Test/GetAsync", action.Name);
        Assert.False(action.IsPacketReturn);
    }

    [Fact]
    [DisplayName("自定义Api特性名")]
    public void GetName_CustomAttribute()
    {
        var method = typeof(CustomController).GetMethod(nameof(CustomController.DoSomething))!;
        var name = ApiAction.GetName(typeof(CustomController), method);

        Assert.Equal("action/do", name);
    }

    [Fact]
    [DisplayName("类级别Api特性名")]
    public void GetName_ClassAttribute()
    {
        var method = typeof(CustomController).GetMethod(nameof(CustomController.Normal))!;
        var name = ApiAction.GetName(typeof(CustomController), method);

        Assert.Equal("Custom/Normal", name);
    }

    [Fact]
    [DisplayName("空Api特性名使用方法名")]
    public void GetName_EmptyApiAttribute()
    {
        var method = typeof(EmptyApiController).GetMethod(nameof(EmptyApiController.Action))!;
        var name = ApiAction.GetName(typeof(EmptyApiController), method);

        // 方法上有Api("root/action")，包含/，typeName为空
        Assert.Equal("root/action", name);
    }

    [Fact]
    [DisplayName("FastInvoker调用")]
    public void FastInvoker_Works()
    {
        var method = typeof(TestController).GetMethod(nameof(TestController.Add))!;
        var action = new ApiAction(method, typeof(TestController));

        var controller = new TestController();
        var result = action.FastInvoker!(controller, [3, 5]);

        Assert.Equal(8, result);
    }

    [Fact]
    [DisplayName("FastInvoker调用Void方法")]
    public void FastInvoker_VoidMethod()
    {
        var method = typeof(TestController).GetMethod(nameof(TestController.DoVoid))!;
        var action = new ApiAction(method, typeof(TestController));

        var controller = new TestController();
        var result = action.FastInvoker!(controller, []);

        Assert.Null(result);
    }

    [Fact]
    [DisplayName("扩展数据Items")]
    public void Items_SetAndGet()
    {
        var action = new ApiAction();
        action["key1"] = "value1";
        action["key2"] = 42;

        Assert.Equal("value1", action["key1"]);
        Assert.Equal(42, action["key2"]);
        Assert.Null(action["nonexist"]);
    }

    [Fact]
    [DisplayName("ToString输出")]
    public void ToString_Format()
    {
        var method = typeof(TestController).GetMethod(nameof(TestController.Add))!;
        var action = new ApiAction(method, typeof(TestController));

        var str = action.ToString();
        Assert.Contains("Add", str);
        Assert.Contains("Int32", str);
    }

    [Fact]
    [DisplayName("LastSession可读写")]
    public void LastSession_SetAndGet()
    {
        var action = new ApiAction();
        Assert.Null(action.LastSession);

        action.LastSession = "session123";
        Assert.Equal("session123", action.LastSession);
    }
}
