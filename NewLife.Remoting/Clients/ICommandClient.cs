using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Serialization;

namespace NewLife.Remoting.Clients;

/// <summary>命令客户端接口</summary>
/// <remarks>
/// 定义客户端接收和处理服务端下发命令的能力。
/// 支持注册命令处理委托，服务端下发命令时自动分发到对应的处理方法。
/// </remarks>
public interface ICommandClient
{
    /// <summary>收到命令时触发</summary>
    event EventHandler<CommandEventArgs> Received;

    /// <summary>命令集合</summary>
    /// <remarks>注册到客户端的命令与委托映射表，键为命令名称（不区分大小写）</remarks>
    IDictionary<String, Delegate> Commands { get; }
}

/// <summary>命令客户端助手</summary>
/// <remarks>
/// 提供命令注册和执行的扩展方法，支持多种委托签名。
/// 命令执行时会自动匹配已注册的委托并调用。
/// </remarks>
public static class CommandClientHelper
{
    /// <summary>注册服务。收到平台下发的服务调用时，执行注册的方法</summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command">命令名称。为空时使用方法名</param>
    /// <param name="method">处理方法。接收参数字符串，返回结果字符串</param>
    public static void RegisterCommand(this ICommandClient client, String command, Func<String?, String?> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>注册服务。收到平台下发的服务调用时，执行注册的方法</summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command">命令名称。为空时使用方法名</param>
    /// <param name="method">异步处理方法。接收参数字符串，返回结果字符串</param>
    public static void RegisterCommand(this ICommandClient client, String command, Func<String?, Task<String?>> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>注册服务。收到平台下发的服务调用时，执行注册的方法</summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command">命令名称。为空时使用方法名</param>
    /// <param name="method">处理方法。接收命令模型，返回响应模型</param>
    public static void RegisterCommand(this ICommandClient client, String command, Func<CommandModel, CommandReplyModel> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>注册服务。收到平台下发的服务调用时，执行注册的方法</summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command">命令名称。为空时使用方法名</param>
    /// <param name="method">异步处理方法。接收命令模型，返回响应模型</param>
    public static void RegisterCommand(this ICommandClient client, String command, Func<CommandModel, Task<CommandReplyModel>> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>注册服务。收到平台下发的服务调用时，执行注册的方法</summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command">命令名称。为空时使用方法名</param>
    /// <param name="method">异步处理方法。接收命令模型和取消令牌，返回响应模型</param>
    public static void RegisterCommand(this ICommandClient client, String command, Func<CommandModel, CancellationToken, Task<CommandReplyModel>> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>注册服务。收到平台下发的服务调用时，执行注册的方法</summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command">命令名称。为空时使用方法名</param>
    /// <param name="method">处理方法。仅接收命令模型，无返回值</param>
    public static void RegisterCommand(this ICommandClient client, String command, Action<CommandModel> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>执行命令</summary>
    /// <remarks>
    /// 根据命令名称查找已注册的委托并执行。
    /// 支持多种委托签名，自动适配调用方式。
    /// 执行失败时返回错误状态和消息。
    /// </remarks>
    /// <param name="client">命令客户端</param>
    /// <param name="model">命令模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令响应模型</returns>
    public static async Task<CommandReplyModel> ExecuteCommand(this ICommandClient client, CommandModel model, CancellationToken cancellationToken = default)
    {
        using var span = DefaultTracer.Instance?.NewSpan("ExecuteCommand", $"{model.Command}({model.Argument})");
        var rs = new CommandReplyModel { Id = model.Id, Status = CommandStatus.已完成 };
        try
        {
            var result = await OnCommand(client, model, cancellationToken).ConfigureAwait(false);
            if (result is CommandReplyModel reply)
            {
                reply.Id = model.Id;
                if (reply.Status == CommandStatus.就绪 || reply.Status == CommandStatus.处理中)
                    reply.Status = CommandStatus.已完成;

                return reply;
            }

            if (result != null)
                rs.Data = result as String ?? (client as ClientBase)?.JsonHost.Write(result) ?? result.ToJson();

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            XTrace.WriteException(ex);

            rs.Data = ex.Message;
            if (ex is ApiException aex && aex.Code == 400)
                rs.Status = CommandStatus.取消;
            else
                rs.Status = CommandStatus.错误;
        }

        return rs;
    }

    /// <summary>分发执行服务</summary>
    /// <param name="client">命令客户端</param>
    /// <param name="model">命令模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果对象</returns>
    private static async Task<Object?> OnCommand(ICommandClient client, CommandModel model, CancellationToken cancellationToken)
    {
        if (!client.Commands.TryGetValue(model.Command, out var d))
            throw new ApiException(ApiCode.NotFound, $"找不到服务[{model.Command}]");

        if (d is Func<String?, Task<String?>> func1)
            return await func1(model.Argument).ConfigureAwait(false);
        if (d is Func<CommandModel, Task<CommandReplyModel>> func3)
            return await func3(model).ConfigureAwait(false);
        if (d is Func<CommandModel, CancellationToken, Task<CommandReplyModel>> func4)
            return await func4(model, cancellationToken).ConfigureAwait(false);

        if (d is Action<CommandModel> func21)
        {
            func21(model);
            return null;
        }

        if (d is Func<CommandModel, CommandReplyModel> func31) return func31(model);
        if (d is Func<String?, String?> func32) return func32(model.Argument);

        throw new ApiException(ApiCode.InternalServerError, $"服务[{model.Command}]的签名[{d}]不正确");
    }
}