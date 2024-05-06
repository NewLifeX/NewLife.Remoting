namespace NewLife.Remoting.Extensions;

/// <summary>网页工具类</summary>
public static class WebHelper
{
    /// <summary>获取用户主机</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static String GetUserHost(this HttpContext context)
    {
        var request = context.Request;

        var str = "";
        if (str.IsNullOrEmpty()) str = request.Headers["X-Remote-Ip"];
        if (str.IsNullOrEmpty()) str = request.Headers["HTTP_X_FORWARDED_FOR"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Real-IP"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Forwarded-For"];
        if (str.IsNullOrEmpty()) str = request.Headers["REMOTE_ADDR"];
        //if (str.IsNullOrEmpty()) str = request.Headers["Host"];
        if (str.IsNullOrEmpty())
        {
            var addr = context.Connection?.RemoteIpAddress;
            if (addr != null)
            {
                if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
                str = addr + "";
            }
        }

        return str;
    }
}