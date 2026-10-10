using System.Diagnostics.CodeAnalysis;
using System.Web;
using NewLife;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.Reflection;
using NewLife.Serialization;

namespace NewLife.Remoting.Http;

/// <summary>Http编码器</summary>
public class HttpEncoder : EncoderBase, IEncoder
{
    #region 属性
    /// <summary>Json主机。提供序列化能力</summary>
    public IJsonHost JsonHost { get; set; } = JsonHelper.Default;

    /// <summary>是否使用Http状态。默认false，使用json包装响应码</summary>
    public Boolean UseHttpStatus { get; set; }
    #endregion

    /// <summary>编码响应数据</summary>
    /// <param name="action">动作名称</param>
    /// <param name="code">错误码</param>
    /// <param name="value">响应值。若为 IPacket/IOwnerPacket 则直接透传，所有权转移给调用方</param>
    /// <returns>编码后的数据包，可能是原始 IOwnerPacket 的直接引用</returns>
    public virtual IPacket? Encode(String action, Int32 code, Object? value)
    {
        //if (value == null) return null;

        // IPacket/IOwnerPacket 直接透传，所有权随之转移给上层 IMessage.Payload
        if (value is IPacket pk) return pk;

        //!!! 对于Http来说，实现了IAccessor的实体类不需要序列化为IPacket
        //if (value is IAccessor acc) return acc.ToPacket();

        // 不支持序列化异常
        if (value is Exception ex) value = ex.GetTrue().Message;

        if (value == null) return null;

        var json = UseHttpStatus ?
            JsonHost.Write(value, false, false, false) :
            JsonHost.Write(CreateEnvelope(action, code, value), false, true, false);
        WriteLog("{0}=>{1}", action, json);

        return (ArrayPacket)json.GetBytes();
    }

    /// <summary>解码参数</summary>
    /// <param name="action"></param>
    /// <param name="data"></param>
    /// <param name="msg"></param>
    /// <returns></returns>
    public virtual Object? DecodeParameters(String action, IPacket? data, IMessage msg)
    {
        if (data == null || data.Total == 0) return null;

        /*
         * 数据内容解析需要根据http数据类型来判定使用什么格式处理
         * **/

        var str = data.ToStr();
        WriteLog("{0}<={1}", action, str);
        if (str.IsNullOrEmpty()) return null;

        var ctype = new String[0];
        if (msg is HttpMessage hmsg && str[0] == '{')
            if (hmsg.ParseHeaders()) ctype = (hmsg.Headers["Content-type"] + "").Split(';');

        if (ctype.Contains("application/json"))
        {
            // 返回类型可能是列表而不是字典
            var obj = JsonHost.Parse(str);

            if (obj is not IDictionary<String, Object?> dic)
                return obj;

            var rs = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dic)
                if (item.Value is String str2)
                    rs[item.Key] = HttpUtility.UrlDecode(str2);
                else
                    rs[item.Key] = item.Value;
            return rs;
        }
        else
        {
            var dic = str.SplitAsDictionary("=", "&");
            var rs = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dic)
                rs[item.Key] = HttpUtility.UrlDecode(item.Value);
            return rs;
        }
    }

    /// <summary>创建流式响应帧。HTTP 模式输出 SSE 格式（text/event-stream）</summary>
    /// <param name="msg">原始请求消息</param>
    /// <param name="action">动作名称</param>
    /// <param name="code">错误码</param>
    /// <param name="value">流数据块</param>
    /// <param name="isLast">是否最后一帧</param>
    /// <returns></returns>
    public virtual IMessage CreateStreamResponse(IMessage msg, String action, Int32 code, Object? value, Boolean isLast)
    {
        if (isLast && value == null)
        {
            // SSE 结束：发送空 data
            var endResponse = msg.CreateReply()!;
            endResponse.SetBody((ArrayPacket)"data: \n\n".GetBytes());
            return endResponse;
        }

        // 编码为 JSON
        var json = UseHttpStatus
            ? JsonHost.Write(value!, false, false, false)
            : JsonHost.Write(CreateEnvelope(action, code, value), false, true, false);

        WriteLog("{0}=>{1}", action, json);

        var sseData = $"data: {json}\n\n";
        var response = msg.CreateReply()!;
        response.SetBody((ArrayPacket)sseData.GetBytes());

        return response;
    }

    /// <summary>解码结果</summary>
    /// <param name="action"></param>
    /// <param name="data"></param>
    /// <param name="msg">消息</param>
    /// <param name="returnType">返回类型</param>
    /// <returns></returns>
    public virtual Object? DecodeResult(String action, IPacket data, IMessage msg, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] Type returnType)
    {
        var json = data?.ToStr();
        WriteLog("{0}<={1}", action, json);

        // 支持基础类型
        if (returnType != null && returnType.GetTypeCode() != TypeCode.Object) return json.ChangeType(returnType);

        if (json.IsNullOrEmpty()) return null;
        if (returnType == null || returnType == typeof(String)) return json;

        // 复杂对象走 JSON 包装；简单类型直接传输文本，不做 JSON 包装
        if (!IsParseableJson(json)) return ConvertScalar(json, returnType);

        // 返回类型可能是列表而不是字典
        var rs = JsonHost.Parse(json);
        if (rs == null) return null;
        if (returnType == typeof(Object)) return rs;

        return Convert(rs, returnType);
    }

    /// <summary>判断正文能否交给 JSON 解析器。仅接受结构化 JSON 与 JSON 数值</summary>
    /// <param name="json">待判断正文</param>
    /// <returns>能否交由解析器处理</returns>
    /// <remarks>
    /// 非结构化正文含简单类型的直接文本：Boolean 为 True/False，DateTime 为完整时间文本，
    /// 字符串为裸文本，Guid/TimeSpan 等为带引号的字符串字面量。默认 JSON 宿主（FastJson）
    /// 不支持顶层标量，交给解析器会抛“Non standard Json string”，须由 <see cref="ConvertScalar"/> 处理
    /// </remarks>
    private static Boolean IsParseableJson(String json)
    {
        var text = json.Trim();
        if (text.Length == 0) return false;

        var c = text[0];
        if (c is '{' or '[') return true;
        if (text is "true" or "false" or "null") return true;

        // JSON 数值：数字、正负号、小数点与指数符号，排除日期时间等以数字开头但含其它字符的文本
        if (c != '-' && (c < '0' || c > '9')) return false;
        for (var i = 1; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch is >= '0' and <= '9' || ch is '.' or 'e' or 'E' or '+' or '-') continue;

            return false;
        }

        return true;
    }

    /// <summary>转换简单类型文本。字符串字面量取其中内容，其余按目标类型直接转换</summary>
    /// <param name="json">响应正文</param>
    /// <param name="returnType">返回类型</param>
    /// <returns>转换结果</returns>
    private static Object? ConvertScalar(String json, Type returnType)
    {
        var text = json.Trim();

        // JSON 字符串字面量取其中内容。默认 JSON 宿主不支持顶层字符串字面量，如 Guid/TimeSpan 的响应正文
        if (text.Length > 1 && text[0] == '"' && text[^1] == '"')
            text = text[1..^1].Replace("\\\\", "\\").Replace("\\\"", "\"");

        // 声明 Object 时按原样文本返回，由调用方按需调用 ToInt/ToDateTime 等转换
        return returnType == typeof(Object) ? text : text.ChangeType(returnType);
    }

    /// <summary>转换为目标类型</summary>
    /// <param name="obj"></param>
    /// <param name="targetType"></param>
    /// <returns></returns>
    public virtual Object? Convert(Object obj, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] Type targetType) => JsonHost.Convert(obj, targetType);

    /// <summary>构造 HTTP 三段式响应封套</summary>
    /// <param name="action">动作名称</param>
    /// <param name="code">错误码</param>
    /// <param name="value">响应值</param>
    /// <returns>响应封套。键名即报文键名</returns>
    /// <remarks>使用字典而非匿名类型：NativeAOT 下匿名类型的公开成员会被裁剪，序列化将得到空对象</remarks>
    private static Dictionary<String, Object?> CreateEnvelope(String action, Int32 code, Object? value) => new()
    {
        ["action"] = action,
        ["code"] = code,
        ["data"] = value,
    };

    #region 编码/解码
    /// <summary>创建请求</summary>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public virtual IMessage CreateRequest(String action, Object? args)
    {
        // 请求方法 GET / HTTP/1.1
        var req = new HttpMessage();
        var sb = Pool.StringBuilder.Get();
        sb.Append("GET ");
        sb.Append(action);

        // 准备参数，二进制优先
        IPacket? pk = null;
        if (args != null)
            if (args is IPacket pk2)
                pk = pk2;
            else if (args is IAccessor acc)
                pk = acc.ToPacket();
            else if (args is Byte[] buf)
                pk = new ArrayPacket(buf);
            else if (args is String str2)
                pk = (ArrayPacket)str2.GetBytes();
            else if (args is DateTime dt)
                pk = (ArrayPacket)dt.ToFullString().GetBytes();
            else if (args.GetType().GetTypeCode() != TypeCode.Object)
                pk = (ArrayPacket)(args + "").GetBytes();
            else
            {
                // url参数
                sb.Append('?');
                if (args.GetType().GetTypeCode() != TypeCode.Object)
                    sb.Append(args);
                else
                    foreach (var item in args.ToDictionary())
                        sb.AppendFormat("{0}={1}", item.Key, item.Value);
            }
        sb.AppendLine(" HTTP/1.1");

        if (pk != null && pk.Total > 0)
        {
            sb.AppendFormat("Content-Length:{0}\r\n", pk.Total);
            sb.AppendLine("Content-Type:application/json");
        }
        sb.Append("Connection:keep-alive");

        req.Header = (ArrayPacket)sb.Return(true).GetBytes();

        return req;
    }

    /// <summary>创建响应消息</summary>
    /// <param name="msg">原始请求消息</param>
    /// <param name="action">动作名称</param>
    /// <param name="code">错误码</param>
    /// <param name="value">响应值。若为 IOwnerPacket，其所有权转移给返回的 HttpMessage.Payload</param>
    /// <returns>Http响应消息，Dispose 时级联释放 Payload 中的 IOwnerPacket</returns>
    public virtual IMessage CreateResponse(IMessage msg, String action, Int32 code, Object? value)
    {
        if (code <= 0 && UseHttpStatus) code = 200;

        // 编码响应数据包。若 value 是 IOwnerPacket 则直接透传为 Payload，所有权转移
        var pk = Encode(action, code, value);

        // 构造响应消息
        var rs = new HttpMessage();
        rs.SetBody(pk);
        if (code >= 500) rs.Kind = MessageKinds.Error;

        // HTTP/1.1 502 Bad Gateway
        var sb = Pool.StringBuilder.Get();
        sb.Append("HTTP/1.1 ");

        if (UseHttpStatus)
        {
            sb.Append(code);
            if (code < 500)
                sb.AppendLine(" OK");
            else
                sb.AppendLine(" Error");
        }
        else
            sb.AppendLine("200 OK");

        sb.AppendFormat("Content-Length:{0}\r\n", pk?.Total ?? 0);
        sb.AppendLine("Content-Type:application/json");
        sb.Append("Connection:keep-alive");

        rs.Header = (ArrayPacket)sb.Return(true).GetBytes();

        return rs;
    }

    private static Byte[] NewLife => "\r\n"u8.ToArray();
    /// <summary>解码 请求/响应</summary>
    /// <param name="msg">消息</param>
    /// <returns>请求响应报文</returns>
    public override ApiMessage? Decode(IMessage msg)
    {
        if (msg is not HttpMessage http || http.Header == null) return null;

        // 分析请求方法 GET / HTTP/1.1
        var span = http.Header.GetSpan();
        var p = span.IndexOf(NewLife);
        if (p <= 0) return null;

        var line = span[..p].ToStr();

        var ss = line.Split(' ');
        if (ss.Length < 3) return null;

        var message = new ApiMessage();

        // 第二段是地址
        var uri = new Uri(ss[1], UriKind.RelativeOrAbsolute);
        if (!uri.IsAbsoluteUri)
        {
            var url = ss[1];
            p = url.IndexOf('?');
            if (p > 0)
            {
                message.Action = url.Substring(1, p - 1);
                message.Data = (ArrayPacket)url.Substring(p + 1).GetBytes();
            }
            else
            {
                message.Action = url.Substring(1);
                message.Data = http.Payload;
            }
        }
        else
        {
            if (!uri.Query.IsNullOrEmpty())
            {
                message.Action = uri.AbsolutePath;
                message.Data = (ArrayPacket)uri.Query.GetBytes();
            }
            else
            {
                message.Action = uri.AbsolutePath;
                message.Data = http.Payload;
            }
            if (message.Action.Length > 1) message.Action = message.Action.Substring(1);
        }

        return message;
    }
    #endregion
}