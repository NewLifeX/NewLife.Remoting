namespace NewLife.Remoting.Models;

/// <summary>应用信息接口</summary>
public interface IAppModel
{
    /// <summary>名称</summary>
    String Name { get; }

    /// <summary>启用</summary>
    Boolean Enable { get; }

    /// <summary>验证授权</summary>
    /// <param name="password"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    Boolean Authorize(String? password, String? ip = null);

    /// <summary>写日志</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="message"></param>
    /// <param name="ip"></param>
    /// <param name="clientId"></param>
    void WriteLog(String action, Boolean success, String message, String? ip, String? clientId);
}