using System;
using System.Buffers;
using System.ComponentModel;
using System.Text;
using NewLife;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.Remoting;
using NewLife.Remoting.Http;
using Xunit;

namespace XUnitTest;

/// <summary>IOwnerPacket 生命周期管理测试</summary>
/// <remarks>
/// 验证 RPC 流水线中 IOwnerPacket 的所有权转移机制：
/// Controller 返回 IOwnerPacket → Encoder.CreateResponse 纳入 IMessage.Payload 链 → 上层 Dispose IMessage 级联归还 ArrayPool。
/// </remarks>
public class OwnerPacketLifecycleTests
{
    #region JsonEncoder 所有权转移
    [Fact(DisplayName = "JsonEncoder响应消息持有OwnerPacket所有权")]
    public void JsonEncoderResponseOwnsOwnerPacket()
    {
        var encoder = new JsonEncoder();
        var req = new DefaultMessage { Sequence = 1 };

        // 模拟 Controller 返回 IOwnerPacket
        var ownerPk = new OwnerPacket(32);
        var span = ownerPk.GetSpan();
        for (var i = 0; i < span.Length; i++) span[i] = (Byte)(i & 0xFF);

        // CreateResponse 将 ownerPk 纳入 IMessage.Payload 链
        var response = encoder.CreateResponse(req, "api/test", 0, ownerPk);

        Assert.NotNull(response);
        Assert.NotNull(response.Payload);

        // OwnerPacket 应被挂载到 Payload 链的 Next 上
        Assert.NotNull(response.Payload.Next);

        // Dispose IMessage 应级联释放 OwnerPacket（归还 ArrayPool）
        response.Dispose();

        // Dispose 后 Payload 应被置空
        Assert.Null(response.Payload);
    }

    [Fact(DisplayName = "JsonEncoder响应消息Dispose后OwnerPacket缓冲区已归还")]
    public void JsonEncoderResponseDisposeReturnsBuffer()
    {
        var encoder = new JsonEncoder();
        var req = new DefaultMessage { Sequence = 2 };

        var ownerPk = new OwnerPacket(16);
        var buf = ownerPk.Buffer;
        Assert.NotNull(buf);

        var response = encoder.CreateResponse(req, "api/data", 0, ownerPk);

        // Dispose 前 OwnerPacket 缓冲区仍可用
        Assert.NotNull(ownerPk.Buffer);

        // Dispose IMessage，级联释放整个 Payload 链
        response.Dispose();

        // OwnerPacket 已被释放，Buffer 访问应抛出 ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => _ = ownerPk.Buffer);
    }
    #endregion

    #region HttpEncoder 所有权转移
    [Fact(DisplayName = "HttpEncoder响应消息持有OwnerPacket所有权")]
    public void HttpEncoderResponseOwnsOwnerPacket()
    {
        var encoder = new HttpEncoder();

        // 模拟 Controller 返回 IOwnerPacket
        var ownerPk = new OwnerPacket(64);
        var span = ownerPk.GetSpan();
        Encoding.UTF8.GetBytes("test-data").CopyTo(span);

        // HttpEncoder.Encode 对 IPacket 直接透传
        var response = encoder.CreateResponse(new HttpMessage(), "api/test", 200, ownerPk);

        Assert.NotNull(response);
        Assert.IsType<HttpMessage>(response);

        var httpMsg = (HttpMessage)response;
        // HttpEncoder 将 IPacket 直接设为 Payload
        Assert.Same(ownerPk, httpMsg.Payload);

        // Dispose HttpMessage 应释放 Payload（即 OwnerPacket）
        httpMsg.Dispose();

        Assert.Null(httpMsg.Payload);
        Assert.Throws<ObjectDisposedException>(() => _ = ownerPk.Buffer);
    }
    #endregion

    #region HttpMessage Dispose 级联
    [Fact(DisplayName = "HttpMessage_Dispose释放Header和Payload")]
    public void HttpMessageDisposeReleasesHeaderAndPayload()
    {
        var header = new OwnerPacket(32);
        var payload = new OwnerPacket(64);

        var msg = new HttpMessage
        {
            Header = header,
            Payload = payload,
        };

        // Dispose 前缓冲区可用
        Assert.NotNull(header.Buffer);
        Assert.NotNull(payload.Buffer);

        msg.Dispose();

        // Dispose 后引用置空
        Assert.Null(msg.Header);
        Assert.Null(msg.Payload);

        // 缓冲区已归还
        Assert.Throws<ObjectDisposedException>(() => _ = header.Buffer);
        Assert.Throws<ObjectDisposedException>(() => _ = payload.Buffer);
    }

    [Fact(DisplayName = "HttpMessage_Dispose级联释放Payload链")]
    public void HttpMessageDisposeCascadesToPayloadChain()
    {
        var payload = new OwnerPacket(32);
        var next = new OwnerPacket(16);
        payload.Next = next;

        var msg = new HttpMessage { Payload = payload };
        msg.Dispose();

        // 链上所有 OwnerPacket 均被释放
        Assert.Throws<ObjectDisposedException>(() => _ = payload.Buffer);
        Assert.Throws<ObjectDisposedException>(() => _ = next.Buffer);
    }

    [Fact(DisplayName = "HttpMessage重复Dispose不抛异常")]
    public void HttpMessageDoubleDisposeIsSafe()
    {
        var msg = new HttpMessage
        {
            Header = new OwnerPacket(16),
            Payload = new OwnerPacket(16),
        };

        msg.Dispose();
        // 第二次 Dispose 不应抛异常
        var ex = Record.Exception(() => msg.Dispose());
        Assert.Null(ex);
    }
    #endregion

    #region DefaultMessage Dispose 级联
    [Fact(DisplayName = "DefaultMessage_Dispose释放Payload中的OwnerPacket")]
    public void DefaultMessageDisposeReleasesOwnerPacket()
    {
        var ownerPk = new OwnerPacket(48);
        Assert.NotNull(ownerPk.Buffer);

        var msg = new DefaultMessage { Payload = ownerPk };
        msg.Dispose();

        Assert.Null(msg.Payload);
        Assert.Throws<ObjectDisposedException>(() => _ = ownerPk.Buffer);
    }

    [Fact(DisplayName = "DefaultMessage_Dispose级联释放Payload链中所有OwnerPacket")]
    public void DefaultMessageDisposeCascadesToPayloadChain()
    {
        var pk1 = new OwnerPacket(16);
        var pk2 = new OwnerPacket(32);
        pk1.Next = pk2;

        var msg = new DefaultMessage { Payload = pk1 };
        msg.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = pk1.Buffer);
        Assert.Throws<ObjectDisposedException>(() => _ = pk2.Buffer);
    }
    #endregion

    #region 编码器完整流水线
    [Fact(DisplayName = "JsonEncoder完整RPC流水线_OwnerPacket生命周期正确")]
    public void JsonEncoderFullPipelineOwnerPacketLifecycle()
    {
        var encoder = new JsonEncoder();

        // 1. 模拟请求
        var req = new DefaultMessage { Sequence = 42 };
        req.Payload = encoder.Encode("api/binary", null, null);

        // 2. 模拟 Controller 返回 OwnerPacket
        var resultPk = new OwnerPacket(128);
        var data = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
        data.CopyTo(resultPk.GetSpan());

        // 3. 编码响应（所有权转移给 IMessage.Payload 链）
        var response = encoder.CreateResponse(req, "api/binary", 0, resultPk);

        // 4. 模拟网络发送：ToPacket() 序列化消息，所有权从 Payload 转移到线缆包
        //    真实流程：Session.SendMessage(rs) 内部调用 ToPacket()，发送后由网络层释放
        var wirePacket = response.ToPacket();
        Assert.NotNull(wirePacket);
        Assert.True(wirePacket.Total > 0);

        // 5. 发送完毕后释放线缆包（模拟网络层行为），级联释放整个链包括 resultPk
        wirePacket.TryDispose();

        // 6. 验证 OwnerPacket 缓冲区已归还 ArrayPool
        Assert.Throws<ObjectDisposedException>(() => _ = resultPk.Buffer);

        // 7. Dispose IMessage 本身（清理对象引用，此时 Payload 已无所有权不会重复释放）
        response.Dispose();
    }

    [Fact(DisplayName = "JsonEncoder响应未经ToPacket直接Dispose也能释放OwnerPacket")]
    public void JsonEncoderResponseDirectDisposeReleasesOwnerPacket()
    {
        var encoder = new JsonEncoder();
        var req = new DefaultMessage { Sequence = 99 };

        // Controller 返回 OwnerPacket
        var resultPk = new OwnerPacket(64);

        // 编码响应
        var response = encoder.CreateResponse(req, "api/direct", 0, resultPk);

        // 不调用 ToPacket()，直接 Dispose 响应消息（例如 OneWay 场景下响应被丢弃）
        response.Dispose();

        // Payload 仍持有所有权，Dispose 级联释放 resultPk
        Assert.Throws<ObjectDisposedException>(() => _ = resultPk.Buffer);
    }
    #endregion
}
