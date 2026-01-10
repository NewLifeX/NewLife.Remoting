using System.Diagnostics.CodeAnalysis;
using NewLife;
using NewLife.Data;
using NewLife.Messaging;

namespace NewLife.Remoting.Http;

/// <summary>Http消息</summary>
public class HttpMessage : IMessage, IDisposable
{
    #region 属性
    /// <summary>是否响应</summary>
    public Boolean Reply { get; set; }

    /// <summary>是否有错</summary>
    public Boolean Error { get; set; }

    /// <summary>单向请求</summary>
    public Boolean OneWay { get; set; }

    /// <summary>头部数据</summary>
    public IPacket? Header { get; set; }

    /// <summary>负载数据</summary>
    public IPacket? Payload { get; set; }

    /// <summary>请求方法</summary>
    public String? Method { get; set; }

    /// <summary>请求资源</summary>
    public String? Uri { get; set; }

    /// <summary>内容长度</summary>
    public Int32 ContentLength { get; set; } = -1;

    /// <summary>头部集合</summary>
    public IDictionary<String, String>? Headers { get; set; }
    #endregion

    #region 构造
    /// <summary>销毁</summary>
    public void Dispose()
    {
        Header.TryDispose();
        Payload.TryDispose();
    }
    #endregion

    #region 方法
    /// <summary>根据请求创建配对的响应消息</summary>
    /// <returns></returns>
    public IMessage CreateReply()
    {
        if (Reply) throw new Exception("不能根据响应消息创建响应消息");

        var msg = new HttpMessage
        {
            Reply = true
        };

        return msg;
    }

    private static readonly Byte[] NewLine = [(Byte)'\r', (Byte)'\n', (Byte)'\r', (Byte)'\n'];
    /// <summary>从数据包中读取消息</summary>
    /// <param name="pk"></param>
    /// <returns>是否成功</returns>
    public virtual Boolean Read(IPacket pk)
    {
        var span = pk.GetSpan();

        var p = span.IndexOf(NewLine);
        if (p < 0) return false;

        Header = pk.Slice(0, p, false);
        Payload = pk.Slice(p + 4, -1, false);

        // 高性能解析请求行：METHOD SP URI SP HTTP/x.y

        var lineEnd = span[..p].IndexOf((Byte)'\n');
        if (lineEnd <= 0) return true;

        var firstLine = span[..lineEnd];
        if (firstLine.Length > 0 && firstLine[^1] == (Byte)'\r') firstLine = firstLine[..^1];

        var sp1 = firstLine.IndexOf((Byte)' ');
        if (sp1 <= 0) return true;

        var sp2 = firstLine[(sp1 + 1)..].IndexOf((Byte)' ');
        if (sp2 <= 0) return true;
        sp2 += sp1 + 1;

        var methodSpan = firstLine[..sp1];
        var uriSpan = firstLine.Slice(sp1 + 1, sp2 - sp1 - 1);

        if (methodSpan.Length > 0) Method = methodSpan.ToStr();
        if (uriSpan.Length > 0) Uri = uriSpan.ToStr();

        return true;
    }

    /// <summary>解码头部</summary>
    [MemberNotNullWhen(true, nameof(Headers))]
    public virtual Boolean ParseHeaders()
    {
        var pk = Header;
        if (pk == null || pk.Total == 0) return false;

        var span = pk.GetSpan();
        if (span.IsEmpty) return false;

        // 第一行：请求行 GET / HTTP/1.1
        var lineEnd = span.IndexOf((Byte)'\n');
        if (lineEnd < 0) return false;

        var firstLine = span[..lineEnd];
        span = span[(lineEnd + 1)..];
        if (firstLine[^1] == (Byte)'\r') firstLine = firstLine[..^1];

        // METHOD SP URI SP HTTP/x.y
        var sp1 = firstLine.IndexOf((Byte)' ');
        if (sp1 > 0)
        {
            var rest = firstLine[(sp1 + 1)..];
            var sp2 = rest.IndexOf((Byte)' ');
            if (sp2 > 0)
            {
                Method = firstLine[..sp1].ToStr();
                Uri = rest[..sp2].ToStr();
            }
        }

        // 头部行：Name: Value（Value 可能包含冒号）
        var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        while (!span.IsEmpty)
        {
            lineEnd = span.IndexOf((Byte)'\n');

            ReadOnlySpan<Byte> line;
            if (lineEnd >= 0)
            {
                line = span[..lineEnd];
                span = span[(lineEnd + 1)..];
            }
            else
            {
                line = span;
                span = default;
            }

            if (!line.IsEmpty && line[^1] == (Byte)'\r') line = line[..^1];
            if (line.IsEmpty) continue;

            var colon = line.IndexOf((Byte)':');
            if (colon <= 0) continue;

            var name = line[..colon].Trim().ToStr();
            dic[name] = line[(colon + 1)..].Trim().ToStr();
        }

        Headers = dic;

        // 内容长度
        if (dic.TryGetValue("Content-Length", out var str))
            ContentLength = str.ToInt();

        return true;
    }

    /// <summary>把消息转为封包</summary>
    /// <returns></returns>
    public virtual IPacket ToPacket()
    {
        if (Header == null) throw new ArgumentNullException(nameof(Header));

        // 使用子数据区，不改变原来的头部对象
        var pk = Header.Slice(0, -1, false);
        pk.Append(NewLine);
        //pk.Next = new[] { (Byte)'\r', (Byte)'\n' };

        var pay = Payload;
        if (pay != null && pay.Total > 0) pk.Append(pay);

        return pk;
    }
    #endregion
}
