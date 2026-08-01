using System;
using System.ComponentModel;
using System.Threading.Tasks;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

/// <summary>ApiServer.OnProcessAsync 处理器路由测试</summary>
public class ApiServerAsyncHandlerTests
{
    private class TestServer : ApiServer
    {
        public Object? InvokeOnProcessAsync(IApiSession session, String action, IPacket? args, IMessage msg, IServiceProvider sp)
            => OnProcessAsync(session, action, args, msg, sp).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>仅同步接口的旧处理器</summary>
    private class SyncHandler : IApiHandler
    {
        public Int32 SyncCount;

        public Object? Execute(IApiSession session, String action, Object? args, IMessage msg, IServiceProvider serviceProvider)
        {
            SyncCount++;
            return null;
        }
    }

    /// <summary>自定义异步处理器</summary>
    private class AsyncHandler : IAsyncApiHandler
    {
        public Int32 AsyncCount;

        public Object? Execute(IApiSession session, String action, Object? args, IMessage msg, IServiceProvider serviceProvider) => null;

        public Task<Object?> ExecuteAsync(IApiSession session, String action, Object? args, IMessage msg, IServiceProvider serviceProvider)
        {
            AsyncCount++;
            return Task.FromResult<Object?>(null);
        }
    }

    /// <summary>仅重写同步 Execute 的旧子类（兼容场景）</summary>
    private class LegacyExecuteHandler : ApiHandler
    {
        public Int32 SyncCount;

        public override Object? Execute(IApiSession session, String action, Object? args, IMessage msg, IServiceProvider serviceProvider)
        {
            SyncCount++;
            return null;
        }
    }

    /// <summary>重写 ExecuteAsync 的子类</summary>
    private class AsyncOverrideHandler : ApiHandler
    {
        public Int32 AsyncCount;

        public override Task<Object?> ExecuteAsync(IApiSession session, String action, Object? args, IMessage msg, IServiceProvider serviceProvider)
        {
            AsyncCount++;
            return Task.FromResult<Object?>(null);
        }
    }

    [Fact]
    [DisplayName("ApiHandler实现异步接口")]
    public void ApiHandler_ImplementsAsyncInterface()
    {
        Assert.IsAssignableFrom<IAsyncApiHandler>(new ApiHandler());
        Assert.IsAssignableFrom<IAsyncApiHandler>(new TokenApiHandler());
    }

    [Fact]
    [DisplayName("仅同步旧处理器走Execute")]
    public void SyncHandler_GoesSyncPath()
    {
        var handler = new SyncHandler();
        using var server = new TestServer { Handler = handler };

        server.InvokeOnProcessAsync(null!, "Test/Info", null, null!, null!);

        Assert.Equal(1, handler.SyncCount);
    }

    [Fact]
    [DisplayName("自定义异步处理器走ExecuteAsync")]
    public void AsyncHandler_GoesAsyncPath()
    {
        var handler = new AsyncHandler();
        using var server = new TestServer { Handler = handler };

        server.InvokeOnProcessAsync(null!, "Test/Info", null, null!, null!);

        Assert.Equal(1, handler.AsyncCount);
    }

    [Fact]
    [DisplayName("仅重写Execute的旧子类回退同步路径")]
    public void LegacyExecuteHandler_GoesSyncPath()
    {
        var handler = new LegacyExecuteHandler();
        using var server = new TestServer { Handler = handler };

        server.InvokeOnProcessAsync(null!, "Test/Info", null, null!, null!);

        Assert.Equal(1, handler.SyncCount);
    }

    [Fact]
    [DisplayName("重写ExecuteAsync的子类走异步路径")]
    public void AsyncOverrideHandler_GoesAsyncPath()
    {
        var handler = new AsyncOverrideHandler();
        using var server = new TestServer { Handler = handler };

        server.InvokeOnProcessAsync(null!, "Test/Info", null, null!, null!);

        Assert.Equal(1, handler.AsyncCount);
    }
}
