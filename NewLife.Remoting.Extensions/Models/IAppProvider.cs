namespace NewLife.Remoting.Extensions.Models;

public interface IAppInfo
{
    String Name { get; }

    Boolean Enable { get; }

    Boolean Authorize(String password, String? ip = null);

    void WriteLog(String action, Boolean success, String message, String? ip, String? clientId);
}

public interface IAppProvider
{
    IAppInfo? FindByName(String? name);

    IAppInfo? Register(String username, String password, Boolean autoRegister, String? ip = null);
}
