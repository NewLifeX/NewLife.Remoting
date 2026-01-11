using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest.Remoting;

/// <summary>命令客户端测试</summary>
public class CommandClientTests
{
    /// <summary>测试注册简单字符串命令</summary>
    [Fact]
    public void RegisterCommand_StringFunc_ShouldAddToCommands()
    {
        // Arrange
        var client = new TestCommandClient();
        Func<String?, String?> handler = arg => $"Echo: {arg}";

        // Act
        client.RegisterCommand("Echo", handler);

        // Assert
        Assert.True(client.Commands.ContainsKey("Echo"));
        Assert.Equal(handler, client.Commands["Echo"]);
    }

    /// <summary>测试注册异步字符串命令</summary>
    [Fact]
    public void RegisterCommand_AsyncStringFunc_ShouldAddToCommands()
    {
        // Arrange
        var client = new TestCommandClient();
        Func<String?, Task<String?>> handler = async arg =>
        {
            await Task.Delay(1);
            return $"Async Echo: {arg}";
        };

        // Act
        client.RegisterCommand("AsyncEcho", handler);

        // Assert
        Assert.True(client.Commands.ContainsKey("AsyncEcho"));
    }

    /// <summary>测试注册命令模型处理器</summary>
    [Fact]
    public void RegisterCommand_CommandModelFunc_ShouldAddToCommands()
    {
        // Arrange
        var client = new TestCommandClient();
        Func<CommandModel, CommandReplyModel> handler = model => new CommandReplyModel
        {
            Id = model.Id,
            Status = CommandStatus.已完成,
            Data = "Done"
        };

        // Act
        client.RegisterCommand("Process", handler);

        // Assert
        Assert.True(client.Commands.ContainsKey("Process"));
    }

    /// <summary>测试执行简单字符串命令</summary>
    [Fact]
    public async Task ExecuteCommand_StringFunc_ShouldReturnResult()
    {
        // Arrange
        var client = new TestCommandClient();
        client.RegisterCommand("Echo", (String? arg) => $"Echo: {arg}");

        var model = new CommandModel
        {
            Id = 1,
            Command = "Echo",
            Argument = "Hello"
        };

        // Act
        var result = await client.ExecuteCommand(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(CommandStatus.已完成, result.Status);
        Assert.Equal("Echo: Hello", result.Data);
    }

    /// <summary>测试执行异步命令</summary>
    [Fact]
    public async Task ExecuteCommand_AsyncFunc_ShouldReturnResult()
    {
        // Arrange
        var client = new TestCommandClient();
        client.RegisterCommand("AsyncEcho", async (String? arg) =>
        {
            await Task.Delay(1);
            return $"Async: {arg}";
        });

        var model = new CommandModel
        {
            Id = 2,
            Command = "AsyncEcho",
            Argument = "World"
        };

        // Act
        var result = await client.ExecuteCommand(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(CommandStatus.已完成, result.Status);
        Assert.Equal("Async: World", result.Data);
    }

    /// <summary>测试执行命令模型处理器</summary>
    [Fact]
    public async Task ExecuteCommand_CommandModelFunc_ShouldReturnReply()
    {
        // Arrange
        var client = new TestCommandClient();
        client.RegisterCommand("Process", (CommandModel m) => new CommandReplyModel
        {
            Id = m.Id,
            Status = CommandStatus.已完成,
            Data = $"Processed: {m.Argument}"
        });

        var model = new CommandModel
        {
            Id = 3,
            Command = "Process",
            Argument = "Test"
        };

        // Act
        var result = await client.ExecuteCommand(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Id);
        Assert.Equal(CommandStatus.已完成, result.Status);
        Assert.Equal("Processed: Test", result.Data);
    }

    /// <summary>测试执行带取消令牌的异步命令</summary>
    [Fact]
    public async Task ExecuteCommand_WithCancellationToken_ShouldWork()
    {
        // Arrange
        var client = new TestCommandClient();
        client.RegisterCommand("CancelableCmd", async (CommandModel m, CancellationToken ct) =>
        {
            await Task.Delay(1, ct);
            return new CommandReplyModel
            {
                Id = m.Id,
                Status = CommandStatus.已完成,
                Data = "Cancelable Done"
            };
        });

        var model = new CommandModel
        {
            Id = 4,
            Command = "CancelableCmd",
            Argument = "Data"
        };

        using var cts = new CancellationTokenSource();

        // Act
        var result = await client.ExecuteCommand(model, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(CommandStatus.已完成, result.Status);
    }

    /// <summary>测试执行不存在的命令</summary>
    [Fact]
    public async Task ExecuteCommand_NotFound_ShouldReturnError()
    {
        // Arrange
        var client = new TestCommandClient();

        var model = new CommandModel
        {
            Id = 5,
            Command = "NotExist",
            Argument = "Data"
        };

        // Act
        var result = await client.ExecuteCommand(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(CommandStatus.错误, result.Status);
        Assert.Contains("找不到服务", result.Data);
    }

    /// <summary>测试命令名称不区分大小写</summary>
    [Fact]
    public async Task ExecuteCommand_CaseInsensitive_ShouldWork()
    {
        // Arrange
        var client = new TestCommandClient();
        client.RegisterCommand("TestCmd", (String? arg) => $"Result: {arg}");

        var model = new CommandModel
        {
            Id = 6,
            Command = "testcmd", // 小写
            Argument = "CaseTest"
        };

        // Act
        var result = await client.ExecuteCommand(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(CommandStatus.已完成, result.Status);
        Assert.Equal("Result: CaseTest", result.Data);
    }

    /// <summary>测试Action命令（无返回值）</summary>
    [Fact]
    public async Task ExecuteCommand_ActionFunc_ShouldComplete()
    {
        // Arrange
        var client = new TestCommandClient();
        var executed = false;
        client.RegisterCommand("ActionCmd", (CommandModel m) => { executed = true; });

        var model = new CommandModel
        {
            Id = 7,
            Command = "ActionCmd",
            Argument = "Data"
        };

        // Act
        var result = await client.ExecuteCommand(model);

        // Assert
        Assert.True(executed);
        Assert.NotNull(result);
        Assert.Equal(CommandStatus.已完成, result.Status);
    }

    /// <summary>测试命令处理器内部异常</summary>
    [Fact]
    public async Task ExecuteCommand_ThrowsException_ShouldReturnError()
    {
        // Arrange
        var client = new TestCommandClient();
        client.RegisterCommand("ErrorCmd", (String? arg) =>
        {
            throw new InvalidOperationException("Test error");
#pragma warning disable CS0162
            return "Never";
#pragma warning restore CS0162
        });

        var model = new CommandModel
        {
            Id = 8,
            Command = "ErrorCmd",
            Argument = "Data"
        };

        // Act
        var result = await client.ExecuteCommand(model);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(CommandStatus.错误, result.Status);
        Assert.Contains("Test error", result.Data);
    }

    /// <summary>测试命令客户端实现</summary>
    private class TestCommandClient : ICommandClient
    {
        public event EventHandler<CommandEventArgs>? Received;

        public IDictionary<String, Delegate> Commands { get; } = new Dictionary<String, Delegate>(StringComparer.OrdinalIgnoreCase);

        public void OnReceived(CommandEventArgs e) => Received?.Invoke(this, e);
    }
}
