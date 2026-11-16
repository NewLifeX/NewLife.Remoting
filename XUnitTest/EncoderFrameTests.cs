using System;
using System.ComponentModel;
using NewLife.Buffers;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

/// <summary>SRMP 帧编解码的健壮性</summary>
/// <remarks>
/// 帧头按“动作名长度前缀 + 动作名 + 数据长度”组织，长度前缀是 7 位变长（1~5 字节），
/// 超长动作名与伪造的长度字段都必须正确处理，否则会写爆缓冲或越界切片。
/// </remarks>
public class EncoderFrameTests
{
    [Fact]
    [DisplayName("超长动作名编码后可原样解码")]
    public void Encode_LongAction_RoundTrip()
    {
        var encoder = new JsonEncoder();
        var action = new String('a', 300);
        var data = "payload"u8.ToArray();

        var pk = encoder.Encode(action, null, new ArrayPacket(data));
        using var msg = new DefaultMessage();
        msg.SetBody(pk);
        var rs = encoder.Decode(msg);

        Assert.NotNull(rs);
        Assert.Equal(action, rs!.Action);
        Assert.Equal(data, rs.Data!.ToArray());
    }

    [Fact]
    [DisplayName("长度字段越界视为无效帧")]
    public void Decode_InvalidLength_ReturnsNull()
    {
        var encoder = new JsonEncoder();
        var action = "api/test";

        // 手工构造：动作名 + 声称 999 字节的数据长度，但后面没有数据
        var buf = new Byte[64];
        var writer = new SpanWriter(buf);
        writer.Write(action);
        writer.Write(999);

        using var msg = new DefaultMessage();
        msg.SetBody(new ArrayPacket(buf, 0, writer.Position));

        Assert.Null(encoder.Decode(msg));
    }

    [Fact]
    [DisplayName("截断的长度字段视为无效帧")]
    public void Decode_TruncatedLength_ReturnsNull()
    {
        var encoder = new JsonEncoder();
        var action = "api/test";

        // 动作名后只留 2 字节，不足以凑齐 4 字节长度字段
        var buf = new Byte[64];
        var writer = new SpanWriter(buf);
        writer.Write(action);
        writer.Advance(2);

        using var msg = new DefaultMessage();
        msg.SetBody(new ArrayPacket(buf, 0, writer.Position));

        Assert.Null(encoder.Decode(msg));
    }
}
