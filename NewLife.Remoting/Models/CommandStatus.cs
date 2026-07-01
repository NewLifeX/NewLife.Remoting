namespace NewLife.Remoting.Models;

/// <summary>命令状态</summary>
public enum CommandStatus
{
    /// <summary>就绪</summary>
    就绪 = 0,

    /// <summary>处理中</summary>
    处理中 = 1,

    /// <summary>已完成</summary>
    已完成 = 2,

    /// <summary>取消</summary>
    取消 = 3,

    /// <summary>错误</summary>
    错误 = 4,

    /// <summary>已发送。指令已通过事件总线发出，等待设备确认收到</summary>
    已发送 = 5,

    /// <summary>已送达。设备已确认收到指令，正在执行或等待执行</summary>
    已送达 = 6,

    /// <summary>已过期。指令在服务端或传输途中超过有效期</summary>
    已过期 = 7,

    /// <summary>已超时。等待设备响应超时，指令可能已送达但未收到回复</summary>
    已超时 = 8,
}