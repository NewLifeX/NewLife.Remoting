using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Serialization;

namespace NewLife.Remoting.Clients;

/// <summary>命令服务接口</summary>
public interface ICommandClient
{
    /// <summary>收到命令时触发</summary>
    event EventHandler<CommandEventArgs> Received;

    /// <summary>命令集合</summary>
    IDictionary<String, Delegate> Commands { get; }
}

/// <summary>命令客户端助手</summary>
public static class CommandClientHelper
{
    /// <summary>
    /// 注册服务。收到平台下发的服务调用时，执行注册的方法
    /// </summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command"></param>
    /// <param name="method"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void RegisterCommand(this ICommandClient client, String command, Func<String?, String?> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>
    /// 注册服务。收到平台下发的服务调用时，执行注册的方法
    /// </summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command"></param>
    /// <param name="method"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void RegisterCommand(this ICommandClient client, String command, Func<String?, Task<String?>> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>
    /// 注册服务。收到平台下发的服务调用时，执行注册的方法
    /// </summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command"></param>
    /// <param name="method"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void RegisterCommand(this ICommandClient client, String command, Func<CommandModel, CommandReplyModel> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>
    /// 注册服务。收到平台下发的服务调用时，执行注册的方法
    /// </summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command"></param>
    /// <param name="method"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void RegisterCommand(this ICommandClient client, String command, Func<CommandModel, Task<CommandReplyModel>> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>
    /// 注册服务。收到平台下发的服务调用时，执行注册的方法
    /// </summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command"></param>
    /// <param name="method"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void RegisterCommand(this ICommandClient client, String command, Func<CommandModel, CancellationToken, Task<CommandReplyModel>> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>
    /// 注册服务。收到平台下发的服务调用时，执行注册的方法
    /// </summary>
    /// <param name="client">命令客户端</param>
    /// <param name="command"></param>
    /// <param name="method"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void RegisterCommand(this ICommandClient client, String command, Action<CommandModel> method)
    {
        if (command.IsNullOrEmpty()) command = method.Method.Name;

        client.Commands[command] = method;
    }

    /// <summary>执行命令</summary>
    /// <param name="client">命令客户端</param>
    /// <param name="model"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
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

            rs.Data = result as String ?? result?.ToJson();
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
    private static async Task<Object?> OnCommand(ICommandClient client, CommandModel model, CancellationToken cancellationToken)
    {
        //WriteLog("OnCommand {0}", model.ToJson());

        if (!client.Commands.TryGetValue(model.Command, out var d))
            throw new ApiException(ApiCode.NotFound, $"找不到服务[{model.Command}]");

        if (d is Func<String?, Task<String?>> func1)
            return await func1(model.Argument).ConfigureAwait(false);
        if (d is Func<CommandModel, Task<CommandReplyModel>> func3)
            return await func3(model).ConfigureAwait(false);
        if (d is Func<CommandModel, CancellationToken, Task<CommandReplyModel>> func4)
            return await func4(model, cancellationToken).ConfigureAwait(false);

        if (d is Action<CommandModel> func21) func21(model);

        if (d is Func<CommandModel, CommandReplyModel> func31) return func31(model);
        if (d is Func<String?, String?> func32) return func32(model.Argument);

        //return null;
        throw new ApiException(ApiCode.InternalServerError, $"服务[{model.Command}]的签名[{d}]不正确");
    }
}