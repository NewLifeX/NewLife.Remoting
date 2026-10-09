using System.Diagnostics.CodeAnalysis;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.Reflection;
using NewLife.Serialization;

namespace NewLife.Remoting;

/// <summary>Json编码器</summary>
public class JsonEncoder : EncoderBase, IEncoder
{
    /// <summary>Json主机。提供序列化能力</summary>
    public IJsonHost JsonHost { get; set; } = JsonHelper.Default;

    /// <summary>解码参数</summary>
    /// <param name="action">动作</param>
    /// <param name="data">数据</param>
    /// <param name="msg">消息</param>
    /// <returns></returns>
    public Object? DecodeParameters(String action, IPacket? data, IMessage msg)
    {
        if (data == null || data.Total == 0) return null;

        var json = data.ToStr().Trim();
        if (LogEnable) WriteLog("{0}[{2:X2}]<={1}", action, json, msg is DefaultMessage dm ? dm.Sequence : 0);

        // 接口只有一个入参时，客户端可能用基础类型封包传递
        if (json.IsNullOrEmpty() || json[0] != '{' && json[0] != '[') return json;

        // 返回类型可能是列表而不是字典
        return JsonHost.Parse(json);
    }

    /// <summary>解码结果</summary>
    /// <param name="action"></param>
    /// <param name="data"></param>
    /// <param name="msg">消息</param>
    /// <param name="returnType">返回类型</param>
    /// <returns></returns>
    public Object? DecodeResult(String action, IPacket data, IMessage msg, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] Type returnType)
    {
        var json = data?.ToStr();
        if (LogEnable) WriteLog("{0}[{2:X2}]<={1}", action, json, msg is DefaultMessage dm ? dm.Sequence : 0);

        // 支持基础类型
        if (returnType != null && returnType.GetTypeCode() != TypeCode.Object) return json.ChangeType(returnType);

        if (json.IsNullOrEmpty()) return null;
        if (returnType == null || returnType == typeof(String)) return json;

        // 基础类型裸值（Boolean 的 True、DateTime 的完整时间、裸字符串）不是合法 JSON，直接解析会抛异常，按文本返回
        if (!IsParseableJson(json)) return json;

        // 返回类型可能是列表而不是字典
        var rs = JsonHost.Parse(json);
        if (rs == null) return null;
        if (returnType == typeof(Object)) return rs;

        return Convert(rs, returnType);
    }

    /// <summary>判断文本能否交给 JSON 解析器。仅接受结构化 JSON、带引号字符串与 JSON 数值</summary>
    /// <param name="json">待判断文本</param>
    /// <returns>能否交由解析器处理</returns>
    /// <remarks>
    /// 基础类型裸值不是合法 JSON：Boolean 编码为 True/False，DateTime 编码为完整时间文本，字符串编码为裸文本，
    /// 交给解析器会抛“Non standard Json string”。这些内容须按原样返回
    /// </remarks>
    private static Boolean IsParseableJson(String json)
    {
        var text = json.Trim();
        if (text.Length == 0) return false;

        var c = text[0];
        if (c is '{' or '[' or '"') return true;
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

    /// <summary>转换为目标类型</summary>
    /// <param name="obj"></param>
    /// <param name="targetType"></param>
    /// <returns></returns>
    public Object? Convert(Object obj, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] Type targetType) => JsonHost.Convert(obj, targetType);

    /// <summary>创建请求消息</summary>
    /// <param name="action">动作名称</param>
    /// <param name="args">请求参数。若为 IPacket/IOwnerPacket 则直接挂载到 Payload 链</param>
    /// <returns>请求消息，其 Payload 包含编码后的 action 和 args</returns>
    public virtual IMessage CreateRequest(String action, Object? args)
    {
        // 二进制优先
        var pk = EncodeValue(args, out var str);

        if (LogEnable)
        {
            if (str.IsNullOrEmpty() && pk != null) str = $"[{pk?.Total}]";
            WriteLog("{0}=>{1}", action, str);
        }

        var payload = Encode(action, null, pk);

        var msg = new DefaultMessage();
        msg.SetBody(payload);

        return msg;
    }

    /// <summary>创建响应消息</summary>
    /// <param name="msg">原始请求消息，用于创建配对的响应</param>
    /// <param name="action">动作名称</param>
    /// <param name="code">错误码。0/200 表示成功</param>
    /// <param name="value">响应值。若为 IOwnerPacket，所有权转移给返回的 IMessage.Payload 链</param>
    /// <returns>响应消息，Dispose 时级联释放 Payload 链中的 IOwnerPacket</returns>
    public IMessage CreateResponse(IMessage msg, String action, Int32 code, Object? value)
    {
        // 编码响应数据包，二进制优先。
        // 若 value 是 IPacket/IOwnerPacket，将直接挂载到 Payload 链的 Next 上，
        // 所有权转移给返回的 IMessage，由上层 Dispose 级联释放
        var pk = EncodeValue(value, out var str);

        if (LogEnable)
        {
            if (str.IsNullOrEmpty() && pk != null) str = $"[{pk?.Total}]";
            WriteLog("{0}[{2:X2}]=>{1}", action, str, msg is DefaultMessage dm ? dm.Sequence : 0);
        }

        var payload = Encode(action, code, pk);

        // 构造响应消息
        var rs = msg.CreateReply()!;
        rs.SetBody(payload);
        if (code is not ApiCode.Ok and not 200) rs.Kind = MessageKinds.Error;

        return rs;
    }

    /// <summary>创建流式响应帧</summary>
    /// <param name="msg">原始请求消息</param>
    /// <param name="action">动作名称</param>
    /// <param name="code">错误码</param>
    /// <param name="value">流数据块</param>
    /// <param name="isLast">是否最后一帧</param>
    /// <returns></returns>
    public IMessage CreateStreamResponse(IMessage msg, String action, Int32 code, Object? value, Boolean isLast)
    {
        var pk = EncodeValue(value, out var str);

        if (LogEnable)
        {
            if (str.IsNullOrEmpty() && pk != null) str = $"[{pk?.Total}]";
            var marker = isLast ? "[End]" : "[Stream]";
            WriteLog("{0}[{2:X2}]{3}=>{1}", action, str, msg is DefaultMessage dm ? dm.Sequence : 0, marker);
        }

        var payload = EncodeStreamChunk(action, code, pk, isLast);

        // 流式帧也使用 CreateReply 保持一致性，客户端通过临时 Received 处理器接收
        var rs = msg.CreateReply()!;
        rs.SetBody(payload);
        if (code is not ApiCode.Ok and not 200) rs.Kind = MessageKinds.Error;

        return rs;
    }

    /// <summary>编码值对象为数据包</summary>
    /// <param name="value">要编码的值。IPacket/IOwnerPacket 直接透传，其他类型序列化为 JSON/字符串</param>
    /// <param name="str">输出日志用的字符串表示</param>
    /// <returns>编码后的数据包，若为 IOwnerPacket 则保留原始引用</returns>
    internal IPacket? EncodeValue(Object? value, out String str)
    {
        str = "";
        IPacket? pk = null;

        if (value != null)
        {
            // IPacket/IOwnerPacket 直接透传，所有权随链转移
            if (value is IPacket pk2)
                pk = pk2;
            else if (value is IAccessor acc)
                pk = acc.ToPacket();
            else if (value is Byte[] buf)
                pk = new ArrayPacket(buf);
            else if (value is String str2)
                pk = (ArrayPacket)(str = str2).GetBytes();
            else if (value is DateTime dt)
                pk = (ArrayPacket)(str = dt.ToFullString()).GetBytes();
            else if (value.GetType().GetTypeCode() != TypeCode.Object)
                pk = (ArrayPacket)(str = value + "").GetBytes();
            else
            {
                // 不支持序列化异常
                if (value is Exception ex)
                    value = str = ex.GetTrue().Message;
                else
                    str = JsonHost.Write(value, false, false, false);

                pk = (ArrayPacket)str.GetBytes();
            }
        }

        return pk;
    }
}