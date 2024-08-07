﻿using NewLife.Data;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Serialization;

namespace NewLife.Remoting;

/// <summary>Json编码器</summary>
public class JsonEncoder : EncoderBase, IEncoder
{
    /// <summary>Json主机。提供序列化能力</summary>
    public IJsonHost JsonHost { get; set; } = JsonHelper.Default;

    /// <summary>编码。请求/响应</summary>
    /// <param name="action"></param>
    /// <param name="code"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public virtual Packet Encode(String action, Int32? code, Packet? value)
    {
        // 内存流，前面留空8字节用于协议头4字节（超长8字节）
        var ms = new MemoryStream();
        ms.Seek(8, SeekOrigin.Begin);

        // 请求：action + args
        // 响应：action + code + result
        var writer = new BinaryWriter(ms);
        writer.Write(action);

        // 异常响应才有code。定长4字节
        if (code != null && code.Value is not ApiCode.Ok and not 200) writer.Write(code.Value);

        // 参数或结果。长度部分定长4字节
        var pk = value;
        if (pk != null) writer.Write(pk.Total);

        var rs = new Packet(ms.GetBuffer(), 8, (Int32)ms.Length - 8)
        {
            Next = pk
        };

        return rs;
    }

    /// <summary>解码参数</summary>
    /// <param name="action">动作</param>
    /// <param name="data">数据</param>
    /// <param name="msg">消息</param>
    /// <returns></returns>
    public Object? DecodeParameters(String action, Packet? data, IMessage msg)
    {
        if (data == null || data.Total == 0) return null;

        var json = data.ToStr().Trim();
        WriteLog("{0}[{2:X2}]<={1}", action, json, msg is DefaultMessage dm ? dm.Sequence : 0);

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
    public Object? DecodeResult(String action, Packet data, IMessage msg, Type returnType)
    {
        var json = data?.ToStr();
        WriteLog("{0}[{2:X2}]<={1}", action, json, msg is DefaultMessage dm ? dm.Sequence : 0);

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

    /// <summary>创建请求</summary>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public virtual IMessage CreateRequest(String action, Object? args)
    {
        // 二进制优先
        var pk = EncodeValue(args, out var str);

        if (Log != null && str.IsNullOrEmpty() && pk != null) str = $"[{pk?.Total}]";
        WriteLog("{0}=>{1}", action, str);

        var payload = Encode(action, null, pk);

        return new DefaultMessage { Payload = payload, };
    }

    /// <summary>创建响应</summary>
    /// <param name="msg"></param>
    /// <param name="action"></param>
    /// <param name="code"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public IMessage CreateResponse(IMessage msg, String action, Int32 code, Object? value)
    {
        // 编码响应数据包，二进制优先
        var pk = EncodeValue(value, out var str);

        if (Log != null && str.IsNullOrEmpty() && pk != null) str = $"[{pk?.Total}]";
        WriteLog("{0}[{2:X2}]=>{1}", action, str, msg is DefaultMessage dm ? dm.Sequence : 0);

        var payload = Encode(action, code, pk);

        // 构造响应消息
        var rs = msg.CreateReply()!;
        rs.Payload = payload;
        if (code is not ApiCode.Ok and not 200) rs.Error = true;

        return rs;
    }

    internal Packet? EncodeValue(Object? value, out String str)
    {
        str = "";
        Packet? pk = null;

        if (value != null)
        {
            if (value is Packet pk2)
                pk = pk2;
            else if (value is IAccessor acc)
                pk = acc.ToPacket();
            else if (value is Byte[] buf)
                pk = new Packet(buf);
            else if (value is String str2)
                pk = (str = str2).GetBytes();
            else if (value is DateTime dt)
                pk = (str = dt.ToFullString()).GetBytes();
            else if (value.GetType().GetTypeCode() != TypeCode.Object)
                pk = (str = value + "").GetBytes();
            else
            {
                // 不支持序列化异常
                if (value is Exception ex)
                    value = str = ex.GetTrue().Message;
                else
                    str = JsonHost.Write(value, false, false, false);

                pk = str.GetBytes();
            }
        }

        return pk;
    }
}