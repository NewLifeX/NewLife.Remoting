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
    public Object? DecodeResult(String action, IPacket data, IMessage msg, Type returnType)
    {
        var json = data?.ToStr();
        if (LogEnable) WriteLog("{0}[{2:X2}]<={1}", action, json, msg is DefaultMessage dm ? dm.Sequence : 0);

        // 支持基础类型
        if (returnType != null && returnType.GetTypeCode() != TypeCode.Object) return json.ChangeType(returnType);

        if (json.IsNullOrEmpty()) return null;
        if (returnType == null || returnType == typeof(String)) return json;

        // 返回类型可能是列表而不是字典
        var rs = JsonHost.Parse(json);
        if (rs == null) return null;
        if (returnType == typeof(Object)) return rs;

        return Convert(rs, returnType);
    }

    /// <summary>转换为目标类型</summary>
    /// <param name="obj"></param>
    /// <param name="targetType"></param>
    /// <returns></returns>
    public Object? Convert(Object obj, Type targetType) => JsonHost.Convert(obj, targetType);

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

        return new DefaultMessage { Payload = payload, };
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
        rs.Payload = payload;
        if (code is not ApiCode.Ok and not 200) rs.Error = true;

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