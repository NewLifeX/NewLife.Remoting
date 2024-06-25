using NewLife.Data;
using NewLife.Http;
using NewLife.Model;
using NewLife.Net;
using NewLife.Serialization;

namespace NewLife.Remoting.Http;

/// <summary>Http编解码器</summary>
public class HttpCodec : Handler
{
    #region 属性
    /// <summary>允许分析头部。默认false</summary>
    /// <remarks>
    /// 分析头部对性能有一定损耗
    /// </remarks>
    public Boolean AllowParseHeader { get; set; }

    /// <summary>Json主机。提供序列化能力</summary>
    public IJsonHost JsonHost { get; set; } = JsonHelper.Default;
    #endregion

    /// <summary>写入数据</summary>
    /// <param name="context"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        // Http编码器仅支持Tcp
        if (context.Owner is ISocket sock && sock.Local != null && sock.Local.Type != NetType.Tcp)
            return base.Write(context, message);

        if (message is HttpMessage http)
            message = http.ToPacket();

        return base.Write(context, message);
    }

    /// <summary>读取数据</summary>
    /// <param name="context"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public override Object? Read(IHandlerContext context, Object message)
    {
        // Http编码器仅支持Tcp
        if (context.Owner is ISocket sock && sock.Local != null && sock.Local.Type != NetType.Tcp)
            return base.Read(context, message);

        if (message is not Packet pk) return base.Read(context, message);

        // 是否Http请求
        var isGet = pk.Count >= 4 && pk[0] == 'G' && pk[1] == 'E' && pk[2] == 'T' && pk[3] == ' ';
        var isPost = pk.Count >= 5 && pk[0] == 'P' && pk[1] == 'O' && pk[2] == 'S' && pk[3] == 'T' && pk[4] == ' ';

        // 该连接第一包检查是否Http
        var ext = context.Owner as IExtend ?? throw new ArgumentOutOfRangeException(nameof(context.Owner));
        if (ext["Encoder"] is not HttpEncoder)
        {
            // 第一个请求必须是GET/POST，才执行后续操作
            if (!isGet && !isPost) return base.Read(context, message);

            ext["Encoder"] = new HttpEncoder { JsonHost = JsonHost };
        }

        // 检查是否有未完成消息
        if (ext["Message"] is HttpMessage msg)
        {
            // 数据包拼接到上一个未完整消息中
            if (msg.Payload == null)
                msg.Payload = pk.Clone(); //拷贝一份，避免缓冲区重用
            else
                msg.Payload.Append(pk.Clone());//拷贝一份，避免缓冲区重用

            // 消息完整才允许上报
            if (msg.ContentLength == 0 || msg.ContentLength > 0 && msg.Payload != null && msg.Payload.Total >= msg.ContentLength)
            {
                // 移除消息
                ext["Message"] = null;

                // 匹配输入回调，让上层事件收到分包信息
                //context.FireRead(msg);
                return base.Read(context, msg);
            }
        }
        else
        {
            // 解码得到消息
            msg = new HttpMessage();
            if (!msg.Read(pk)) throw new XException("Http请求头不完整");

            if (AllowParseHeader && !msg.ParseHeaders()) throw new XException("Http头部解码失败");

            // GET请求一次性过来，暂时不支持头部被拆为多包的场景
            if (isGet)
            {
                // 匹配输入回调，让上层事件收到分包信息
                //context.FireRead(msg);
                return base.Read(context, msg);
            }
            // POST可能多次，最典型的是头部和主体分离
            else
            {
                // 消息完整才允许上报
                if (msg.ContentLength == 0 || msg.ContentLength > 0 && msg.Payload != null && msg.Payload.Total >= msg.ContentLength)
                {
                    // 匹配输入回调，让上层事件收到分包信息
                    //context.FireRead(msg);
                    return base.Read(context, msg);
                }
                else
                {
                    // 请求不完整，拷贝一份，避免缓冲区重用
                    if (msg.Header != null) msg.Header = msg.Header.Clone();
                    if (msg.Payload != null)
                    {
                        // payload有长度时才能复制，否则会造成数据错误
                        if (msg.Payload.Count > 0)
                        {
                            msg.Payload = msg.Payload.Clone();
                        }
                        else
                        {
                            msg.Payload = null;
                        }
                    }

                    ext["Message"] = msg;
                }
            }
        }

        return null;
    }
}