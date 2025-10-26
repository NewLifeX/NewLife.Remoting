using System.Text;
using NewLife.Buffers;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;

namespace NewLife.Remoting;

/// <summary>编码器</summary>
public interface IEncoder
{
    ///// <summary>编码 请求/响应</summary>
    ///// <param name="action"></param>
    ///// <param name="code"></param>
    ///// <param name="value"></param>
    ///// <returns></returns>
    //Packet Encode(String action, Int32 code, Packet value);

    /// <summary>创建请求</summary>
    /// <param name="action"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    IMessage CreateRequest(String action, Object? args);

    /// <summary>创建响应</summary>
    /// <param name="msg"></param>
    /// <param name="action"></param>
    /// <param name="code"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    IMessage CreateResponse(IMessage msg, String action, Int32 code, Object? value);

    /// <summary>解码 请求/响应</summary>
    /// <param name="msg">消息</param>
    /// <returns>请求响应报文</returns>
    ApiMessage? Decode(IMessage msg);

    ///// <summary>编码 请求/响应</summary>
    ///// <param name="action">服务动作</param>
    ///// <param name="code">错误码</param>
    ///// <param name="value">参数或结果</param>
    ///// <returns></returns>
    //Packet Encode(String action, Int32 code, Object value);

    /// <summary>解码参数</summary>
    /// <param name="action">动作</param>
    /// <param name="data">数据</param>
    /// <param name="msg">消息</param>
    /// <returns></returns>
    Object? DecodeParameters(String action, IPacket? data, IMessage msg);

    /// <summary>解码结果</summary>
    /// <param name="action"></param>
    /// <param name="data"></param>
    /// <param name="msg">消息</param>
    /// <param name="returnType">返回类型</param>
    /// <returns></returns>
    Object? DecodeResult(String action, IPacket data, IMessage msg, Type returnType);

    /// <summary>转换为目标类型</summary>
    /// <param name="obj"></param>
    /// <param name="targetType"></param>
    /// <returns></returns>
    Object? Convert(Object obj, Type targetType);

    /// <summary>日志提供者</summary>
    ILog Log { get; set; }
}

/// <summary>编码器基类</summary>
public abstract class EncoderBase
{
    #region 编码/解码
    /// <summary>编码。请求/响应</summary>
    /// <param name="action"></param>
    /// <param name="code"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public virtual IPacket Encode(String action, Int32? code, IPacket? value)
    {
        // 内存流，前面留空8字节用于协议头4字节（超长8字节）
        //var ms = new MemoryStream();
        //ms.Seek(8, SeekOrigin.Begin);

        var len = 8 + 1 + Encoding.UTF8.GetByteCount(action) + 4;
        if (code != null && code.Value is not ApiCode.Ok and not 200) len += 4;
        var pk = new OwnerPacket(len);

        // 请求：action + args
        // 响应：action + code + result
        var writer = new SpanWriter(pk.GetSpan());
        writer.Advance(8);
        writer.Write(action);

        // 异常响应才有code。定长4字节
        if (code != null && code.Value is not ApiCode.Ok and not 200) writer.Write(code.Value);

        // 参数或结果。长度部分定长4字节
        if (value != null) writer.Write(value.Total);

        var pk2 = pk.Slice(8, writer.Position - 8, true);
        if (value != null) pk2.Next = value;

        return pk2;
    }

    /// <summary>解码 请求/响应</summary>
    /// <param name="msg">消息</param>
    /// <returns>请求响应报文</returns>
    public virtual ApiMessage? Decode(IMessage msg)
    {
        var message = new ApiMessage();

        // 请求：action + args
        // 响应：action + code + result
        //var ms = msg.Payload!.GetStream();
        var reader = new SpanReader(msg.Payload!.GetSpan());

        message.Action = reader.ReadString();
        if (message.Action.IsNullOrEmpty()) throw new Exception("解码错误，无法找到服务名！");

        // 异常响应才有code
        if (msg.Reply && msg.Error) message.Code = reader.ReadInt32();

        // 参数或结果
        if (reader.Available > 0)
        {
            var len = reader.ReadInt32();
            if (len > 0) message.Data = msg.Payload.Slice(reader.Position, len);
        }

        return message;
    }
    #endregion

    #region 日志
    /// <summary>日志提供者</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public virtual void WriteLog(String format, params Object?[] args) => Log?.Info(format, args);
    #endregion
}