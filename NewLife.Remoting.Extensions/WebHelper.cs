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

        return GetRawUrl(uri, k => request.Headers[k] + "");
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

    /// <summary>可信代理列表。来自这些代理的转发头才被信任，支持精确地址、*通配和IPv4 CIDR网段；为空时兼容旧行为信任全部转发头</summary>
    public static String?[]? TrustedProxies { get; set; }

    /// <summary>获取用户主机。优先从转发头解析；配置可信代理后按代理链解析并防伪造，最终返回单一IP地址</summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static String GetUserHost(this HttpContext context)
    {
        var request = context.Request;

        // 直连地址
        var remote = context.Connection?.RemoteIpAddress;
        if (remote != null && remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
        var remoteIp = remote + "";

        var str = "";
        if (str.IsNullOrEmpty()) str = request.Headers["X-Remote-Ip"];
        if (str.IsNullOrEmpty()) str = request.Headers["HTTP_X_FORWARDED_FOR"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Real-IP"];
        if (str.IsNullOrEmpty()) str = request.Headers["X-Forwarded-For"];
        if (str.IsNullOrEmpty()) str = request.Headers["REMOTE_ADDR"];

        if (!str.IsNullOrEmpty())
        {
            var trusted = TrustedProxies;
            if (trusted == null || trusted.Length == 0)
            {
                // 未配置可信代理，兼容旧行为信任全部转发头；多层反代时取链首地址，并折叠为单一IP
                var first = str.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).FirstOrDefault(e => e.Length > 0);
                if (!first.IsNullOrEmpty()) str = first;
            }
            else if (remoteIp.IsNullOrEmpty() || !IsTrustedProxy(remoteIp, trusted))
            {
                // 配置了可信代理但直连来源不可信，转发头可被伪造，直接使用直连地址
                str = remoteIp;
            }
            else
            {
                // 从链尾向左跳过可信代理，取第一个不可信地址作为客户端地址
                var ips = str.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
                var found = (String?)null;
                for (var i = ips.Count - 1; i >= 0; i--)
                {
                    if (!IsTrustedProxy(ips[i], trusted))
                    {
                        found = ips[i];
                        break;
                    }
                }
                str = found ?? (ips.Count > 0 ? ips[0] : remoteIp);
            }
        }

        //if (str.IsNullOrEmpty()) str = request.Headers["Host"];
        if (str.IsNullOrEmpty()) str = remoteIp;

        return str + "";
    }

    /// <summary>判断地址是否在可信代理列表内。列表项支持精确IP、*通配和IPv4 CIDR网段</summary>
    /// <param name="ip">待判断IP</param>
    /// <param name="trusted">可信代理列表</param>
    /// <returns>是否可信</returns>
    private static Boolean IsTrustedProxy(String? ip, String?[] trusted)
    {
        if (ip.IsNullOrEmpty()) return false;

        foreach (var item in trusted)
        {
            var t = item?.Trim();
            if (t.IsNullOrEmpty()) continue;

            if (t == "*") return true;

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