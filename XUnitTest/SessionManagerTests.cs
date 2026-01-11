using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Web;
using Xunit;
using Moq;

namespace XUnitTest;

/// <summary>会话管理器测试</summary>
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class SessionManagerTests
{
    #region ICommandSession测试
    [Fact(DisplayName = "命令会话基本属性")]
    public void CommandSessionBasicProperties()
    {
        var session = new CommandSession
        {
            Code = "device001"
        };

        Assert.Equal("device001", session.Code);
        Assert.True(session.Active);
    }

    [Fact(DisplayName = "命令会话处理")]
    public async Task CommandSessionHandleAsync()
    {
        var session = new CommandSession
        {
            Code = "device001"
        };

        var cmd = new CommandModel
        {
            Id = 1,
            Command = "test"
        };

        // 基类HandleAsync默认返回已完成任务
        await session.HandleAsync(cmd, null, CancellationToken.None);
        Assert.True(true);
    }
    #endregion

    #region SessionManager测试
    [Fact(DisplayName = "会话管理器添加和获取")]
    public void SessionManagerAddAndGet()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICacheProvider>(new MemoryCacheProvider());
        var sp = services.BuildServiceProvider();

        using var manager = new SessionManager(sp);

        var session = new CommandSession { Code = "device001" };
        manager.Add(session);

        var result = manager.Get("device001");
        Assert.NotNull(result);
        Assert.Same(session, result);
    }

    [Fact(DisplayName = "会话管理器删除")]
    public void SessionManagerRemove()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICacheProvider>(new MemoryCacheProvider());
        var sp = services.BuildServiceProvider();

        using var manager = new SessionManager(sp);

        var session = new CommandSession { Code = "device002" };
        manager.Add(session);

        Assert.NotNull(manager.Get("device002"));

        manager.Remove(session);

        Assert.Null(manager.Get("device002"));
    }

    [Fact(DisplayName = "会话管理器不存在的会话")]
    public void SessionManagerGetNotExist()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICacheProvider>(new MemoryCacheProvider());
        var sp = services.BuildServiceProvider();

        using var manager = new SessionManager(sp);

        var result = manager.Get("notexist");
        Assert.Null(result);
    }

    [Fact(DisplayName = "会话管理器发布消息")]
    public async Task SessionManagerPublishAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICacheProvider>(new MemoryCacheProvider());
        var sp = services.BuildServiceProvider();

        using var manager = new SessionManager(sp);

        var session = new TestCommandSession { Code = "device003" };
        manager.Add(session);

        var cmd = new CommandModel
        {
            Id = 1,
            Command = "test",
            Argument = "arg1"
        };

        await manager.PublishAsync("device003", cmd, null, CancellationToken.None);

        // 等待消息处理
        await Task.Delay(100);

        Assert.True(session.Handled);
        Assert.Equal("test", session.LastCommand?.Command);
    }

    [Fact(DisplayName = "会话管理器空命令检查")]
    public async Task SessionManagerPublishNullCommand()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICacheProvider>(new MemoryCacheProvider());
        var sp = services.BuildServiceProvider();

        using var manager = new SessionManager(sp);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            manager.PublishAsync("device001", null!, null, CancellationToken.None));
    }
    #endregion

    #region ITokenService Mock测试
    [Fact(DisplayName = "令牌服务Mock测试")]
    public void TokenServiceMock()
    {
        var mockTokenService = new Mock<ITokenService>();
        mockTokenService.Setup(x => x.IssueToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new TokenModel
            {
                AccessToken = "test_token",
                TokenType = "JWT",
                ExpireIn = 3600
            });

        var token = mockTokenService.Object.IssueToken("device001", "client001");

        Assert.Equal("test_token", token.AccessToken);
        Assert.Equal("JWT", token.TokenType);
        Assert.Equal(3600, token.ExpireIn);
    }
    #endregion

    #region 辅助类
    /// <summary>测试命令会话</summary>
    private class TestCommandSession : CommandSession
    {
        public bool Handled { get; private set; }
        public CommandModel? LastCommand { get; private set; }

        public override Task HandleAsync(CommandModel command, string? message, CancellationToken cancellationToken)
        {
            Handled = true;
            LastCommand = command;
            return Task.CompletedTask;
        }
    }

    /// <summary>内存缓存提供者（简化实现用于测试）</summary>
    private class MemoryCacheProvider : ICacheProvider
    {
        private readonly ICache _cache = new MemoryCache();

        public ICache Cache { get => _cache; set { } }

        public ICache InnerCache { get => _cache; set { } }

        public IProducerConsumer<T> GetQueue<T>(string topic) => _cache.GetQueue<T>(topic);

        public IProducerConsumer<T> GetQueue<T>(string topic, string? group) => _cache.GetQueue<T>(topic);

        public IProducerConsumer<T> GetInnerQueue<T>(string topic) => _cache.GetQueue<T>(topic);

        public IProducerConsumer<T> GetStack<T>(string topic) => _cache.GetStack<T>(topic);

        public IDisposable AcquireLock(string key, int msTimeout) => throw new NotImplementedException();
    }
    #endregion
}
