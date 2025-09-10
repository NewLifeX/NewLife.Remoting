namespace NewLife.Remoting.Models;

/// <summary>命令事件参数</summary>
public class CommandEventArgs : EventArgs
{
    /// <summary>命令</summary>
    public CommandModel? Model { get; set; }

    /// <summary>命令原始消息</summary>
    public String? Message { get; set; }

    /// <summary>响应</summary>
    public CommandReplyModel? Reply { get; set; }
}