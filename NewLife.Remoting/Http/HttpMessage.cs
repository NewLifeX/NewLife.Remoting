using System.Diagnostics.CodeAnalysis;
using NewLife.Data;
using NewLife.Messaging;

namespace NewLife.Remoting.Http;

/// <summary>Http消息</summary>
public class HttpMessage : IMessage
{
    #region 属性
    /// <summary>是否响应</summary>
    public Boolean Reply { get; set; }

    /// <summary>是否有错</summary>
    public Boolean Error { get; set; }

    /// <summary>单向请求</summary>
    public Boolean OneWay => false;

    /// <summary>头部数据</summary>
    public Packet? Header { get; set; }

    /// <summary>负载数据</summary>
    public Packet? Payload { get; set; }

    /// <summary>请求方法</summary>
    public String? Method { get; set; }

    /// <summary>请求资源</summary>
    public String? Uri { get; set; }

    /// <summary>内容长度</summary>
    public Int32 ContentLength { get; set; } = -1;

    /// <summary>头部集合</summary>
    public IDictionary<String, String>? Headers { get; set; }
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

    private static readonly Byte[] NewLine = new[] { (Byte)'\r', (Byte)'\n', (Byte)'\r', (Byte)'\n' };
    /// <summary>从数据包中读取消息</summary>
    /// <param name="pk"></param>
    /// <returns>是否成功</returns>
    public virtual Boolean Read(Packet pk)
    {
        var p = pk.IndexOf(NewLine);
        if (p < 0) return false;

        Header = pk.Slice(0, p);
        Payload = pk.Slice(p + 4);

        return true;
    }

    /// <summary>解码头部</summary>
    [MemberNotNullWhen(true, nameof(Headers))]
    public virtual Boolean ParseHeaders()
    {
        var pk = Header;
        if (pk == null || pk.Total == 0) return false;

        // 请求方法 GET / HTTP/1.1
        var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        var ss = pk.ToStr().Split("\r\n");
        {
            var kv = ss[0].Split(' ');
            if (kv != null && kv.Length >= 3)
            {
                Method = kv[0].Trim();
                Uri = kv[1].Trim();
            }
        }
        for (var i = 1; i < ss.Length; i++)
        {
            var kv = ss[i].Split(':');
            if (kv != null && kv.Length >= 2)
                dic[kv[0].Trim()] = kv[1].Trim();
        }
        Headers = dic;

        // 内容长度
        if (dic.TryGetValue("Content-Length", out var str))
            ContentLength = str.ToInt();

        return true;
    }

    /// <summary>把消息转为封包</summary>
    /// <returns></returns>
    public virtual Packet ToPacket()
    {
        if (Header == null) throw new ArgumentNullException(nameof(Header));

        // 使用子数据区，不改变原来的头部对象
        var pk = Header.Slice(0, -1);
        pk.Next = NewLine;
        //pk.Next = new[] { (Byte)'\r', (Byte)'\n' };

        var pay = Payload;
        if (pay != null && pay.Total > 0) pk.Append(pay);

        return pk;
    }
    #endregion
}
