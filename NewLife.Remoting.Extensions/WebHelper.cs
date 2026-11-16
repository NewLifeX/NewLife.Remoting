using Microsoft.AspNetCore.Http.Extensions;
using NewLife.Web;

namespace NewLife.Remoting.Extensions;

/// <summary>网页工具类</summary>
public static class WebHelper
{
    /// <summary>获取原始请求Url，支持反向代理</summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static Uri GetRawUrl(this HttpRequest request)
    {
        Uri? uri = null;

        // 取请求头
        if (uri == null)
        {
            var url = request.GetEncodedUrl();
            uri = new Uri(url);
        }

        // 转发头可能被任意客户端伪造，只有直连来源在可信代理列表内才采信
        var trustHeaders = IsTrustedRequest(request);

        return GetRawUrl(uri, trustHeaders ? k => request.Headers[k] + "" : _ => "");
    }

    private static Uri GetRawUrl(Uri uri, Func<String, String> headers)
    {
        var str = headers("HTTP_X_REQUEST_URI");
        if (str.IsNullOrEmpty()) str = headers("X-Request-Uri");

        if (str.IsNullOrEmpty())
        {
            // 阿里云CDN默认支持 X-Client-Scheme: https
            var scheme = headers("HTTP_X_CLIENT_SCHEME");
            if (scheme.IsNullOrEmpty()) scheme = headers("X-Client-Scheme");

            // nginx
            if (scheme.IsNullOrEmpty()) scheme = headers("HTTP_X_FORWARDED_PROTO");
            if (scheme.IsNullOrEmpty()) scheme = headers("X-Forwarded-Proto");

            if (!scheme.IsNullOrEmpty()) str = scheme + "://" + uri.ToString().Substring("://");
        }

        if (!str.IsNullOrEmpty()) uri = new Uri(uri, str);

        var uriInfo = new UriInfo(uri.ToString());
        //经反代时需要处理非80或443默认端口
        var port = headers("X-Forwarded-Port").ToInt();
        if (port > 0 && port != 80 && port != 443)
        {
            uriInfo.Port = port;
            uri = new Uri(uriInfo.ToString()!);
        }

        return uri;
    }

    /// <summary>可信代理列表。来自这些代理的转发头才被信任，支持精确地址、*通配和IPv4 CIDR网段；为空时不信任任何转发头</summary>
    /// <remarks>
    /// 默认不信任 <c>X-Forwarded-For</c>/<c>X-Real-IP</c> 等转发头，避免客户端伪造设备 IP 与审计记录。
    /// 反向代理部署需显式配置代理地址；确需信任全部来源时设置 <c>"*"</c>。
    /// </remarks>
    public static String?[]? TrustedProxies { get; set; }

    /// <summary>获取用户主机。仅在直连来源为可信代理时解析转发头，最终返回单一IP地址</summary>
    /// <param name="context"></param>
    /// <returns>客户端 IP，无法获取时返回空字符串</returns>
    public static String GetUserHost(this HttpContext context)
    {
        var request = context.Request;

        // 直连地址
        var remote = context.Connection?.RemoteIpAddress;
        if (remote != null && remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
        var remoteIp = remote + "";

        var trusted = TrustedProxies;
        if (trusted == null || trusted.Length == 0 || !IsTrustedProxy(remoteIp, trusted)) return remoteIp;

        var str = request.Headers["X-Remote-Ip"] + "";
        if (str.IsNullOrEmpty()) str = request.Headers["HTTP_X_FORWARDED_FOR"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Real-IP"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Forwarded-For"];
        if (str.IsNullOrEmpty()) str = request.Headers["REMOTE_ADDR"];
        if (str.IsNullOrEmpty()) return remoteIp;

        // 从链尾向左跳过可信代理，取第一个不可信地址作为客户端地址
        var ips = str.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
        for (var i = ips.Count - 1; i >= 0; i--)
        {
            if (!IsTrustedProxy(ips[i], trusted)) return ips[i];
        }

        return ips.Count > 0 ? ips[0] : remoteIp;
    }

    /// <summary>判断当前请求的直连来源是否为可信代理</summary>
    /// <param name="request">HTTP 请求</param>
    /// <returns>可信时返回 true</returns>
    private static Boolean IsTrustedRequest(HttpRequest request)
    {
        var trusted = TrustedProxies;
        if (trusted == null || trusted.Length == 0) return false;

        var remote = request.HttpContext.Connection?.RemoteIpAddress;
        if (remote != null && remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();

        return IsTrustedProxy(remote + "", trusted);
    }

    /// <summary>判断地址是否在可信代理列表内。列表项支持精确IP、*通配和IPv4 CIDR网段</summary>
    /// <param name="ip">待判断IP</param>
    /// <param name="trusted">可信代理列表</param>
    /// <returns>是否可信</returns>
    private static Boolean IsTrustedProxy(String? ip, String?[] trusted)
    {
        foreach (var item in trusted)
        {
            if (item?.Trim() == "*") return true;
        }

        if (ip.IsNullOrEmpty()) return false;

        foreach (var item in trusted)
        {
            var t = item?.Trim();
            if (t.IsNullOrEmpty()) continue;

            if (t.Contains('/'))
            {
                if (MatchCidr(ip!, t)) return true;
            }
            else if (t.IsMatch(ip))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>IPv4 CIDR网段匹配</summary>
    /// <param name="ip">IPv4地址</param>
    /// <param name="cidr">网段，如 10.0.0.0/8</param>
    /// <returns>是否属于该网段</returns>
    private static Boolean MatchCidr(String ip, String cidr)
    {
        var ss = cidr.Split('/');
        if (ss.Length != 2) return false;

        var mask = ss[1].ToInt();
        if (mask is < 0 or > 32) return false;

        var ip1 = ParseIpv4(ip);
        var ip2 = ParseIpv4(ss[0]);
        if (ip1 < 0 || ip2 < 0) return false;

        if (mask == 0) return true;

        var bits = (~((1L << (32 - mask)) - 1)) & 0xFFFFFFFFL;
        return (ip1 & bits) == (ip2 & bits);
    }

    /// <summary>解析IPv4地址为整数，非法返回-1</summary>
    /// <param name="ip">IPv4地址</param>
    /// <returns>整型地址</returns>
    private static Int64 ParseIpv4(String? ip)
    {
        if (ip.IsNullOrEmpty()) return -1;

        var ss = ip.Split('.');
        if (ss.Length != 4) return -1;

        var n = 0L;
        foreach (var item in ss)
        {
            var b = item.ToInt();
            if (b is < 0 or > 255) return -1;

            n = (n << 8) | (UInt32)b;
        }

        return n;
    }
}