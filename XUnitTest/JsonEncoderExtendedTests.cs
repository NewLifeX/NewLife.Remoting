using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.Remoting;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest;

/// <summary>JsonEncoder更多单元测试</summary>
public class JsonEncoderExtendedTests
{
    [Fact]
    [DisplayName("DecodeParameters空数据")]
    public void DecodeParameters_NullData()
    {
        var encoder = new JsonEncoder();

        var result = encoder.DecodeParameters("test", null, new DefaultMessage());

        Assert.Null(result);
    }

    [Fact]
    [DisplayName("DecodeParameters空包")]
    public void DecodeParameters_EmptyPacket()
    {
        var encoder = new JsonEncoder();
        var data = new ArrayPacket(Array.Empty<Byte>());

        var result = encoder.DecodeParameters("test", data, new DefaultMessage());

        Assert.Null(result);
    }

    [Fact]
    [DisplayName("DecodeParameters非Json字符串")]
    public void DecodeParameters_PlainString()
    {
        var encoder = new JsonEncoder();
        var data = (ArrayPacket)"hello world".GetBytes();

        var result = encoder.DecodeParameters("test", data, new DefaultMessage());

        Assert.Equal("hello world", result);
    }

    [Fact]
    [DisplayName("DecodeParameters_Json对象")]
    public void DecodeParameters_JsonObject()
    {
        var encoder = new JsonEncoder();
        var json = "{\"name\":\"test\",\"value\":42}";
        var data = (ArrayPacket)json.GetBytes();

        var result = encoder.DecodeParameters("test", data, new DefaultMessage());

        Assert.NotNull(result);
    }

    [Fact]
    [DisplayName("DecodeResult空数据")]
    public void DecodeResult_NullData()
    {
        var encoder = new JsonEncoder();

        var result = encoder.DecodeResult("test", null!, new DefaultMessage(), typeof(String));

        Assert.Null(result);
    }

    [Fact]
    [DisplayName("DecodeResult字符串类型")]
    public void DecodeResult_StringType()
    {
        var encoder = new JsonEncoder();
        var data = (ArrayPacket)"hello".GetBytes();

        var result = encoder.DecodeResult("test", data, new DefaultMessage(), typeof(String));

        Assert.Equal("hello", result);
    }

    [Fact]
    [DisplayName("DecodeResult基础类型Int32")]
    public void DecodeResult_Int32Type()
    {
        var encoder = new JsonEncoder();
        var data = (ArrayPacket)"42".GetBytes();

        var result = encoder.DecodeResult("test", data, new DefaultMessage(), typeof(Int32));

        Assert.Equal(42, result);
    }

    [Fact]
    [DisplayName("DecodeResult_ObjectType")]
    public void DecodeResult_ObjectType()
    {
        var encoder = new JsonEncoder();
        var data = (ArrayPacket)"{\"key\":\"value\"}".GetBytes();

        var result = encoder.DecodeResult("test", data, new DefaultMessage(), typeof(Object));

        Assert.NotNull(result);
    }

    [Fact]
    [DisplayName("DecodeResult_NullReturnType")]
    public void DecodeResult_NullReturnType()
    {
        var encoder = new JsonEncoder();
        var data = (ArrayPacket)"some text".GetBytes();

        var result = encoder.DecodeResult("test", data, new DefaultMessage(), null!);

        Assert.Equal("some text", result);
    }

    [Fact]
    [DisplayName("Convert转换对象")]
    public void Convert_Object()
    {
        var encoder = new JsonEncoder();
        var dict = new Dictionary<String, Object> { ["Name"] = "test" };

        // Convert 内部委托给 JsonHost
        var result = encoder.Convert(dict, typeof(Object));
        Assert.NotNull(result);
    }

    [Fact]
    [DisplayName("JsonHost可替换")]
    public void JsonHost_Replaceable()
    {
        var encoder = new JsonEncoder();
        var host = JsonHelper.Default;
        encoder.JsonHost = host;

        Assert.Equal(host, encoder.JsonHost);
    }

    [Fact]
    [DisplayName("CreateRequest基本调用")]
    public void CreateRequest_Basic()
    {
        var encoder = new JsonEncoder();

        var msg = encoder.CreateRequest("Api/Test", new { name = "test" });

        Assert.NotNull(msg);
        Assert.NotNull(msg.Payload);
        Assert.False(msg.Reply);
    }

    [Fact]
    [DisplayName("CreateRequest空参数")]
    public void CreateRequest_NullArgs()
    {
        var encoder = new JsonEncoder();

        var msg = encoder.CreateRequest("Api/Test", null);

        Assert.NotNull(msg);
    }

    [Fact]
    [DisplayName("CreateRequest字符串参数")]
    public void CreateRequest_StringArgs()
    {
        var encoder = new JsonEncoder();

        var msg = encoder.CreateRequest("Api/Test", "hello");

        Assert.NotNull(msg);
        Assert.NotNull(msg.Payload);
    }

    [Fact]
    [DisplayName("CreateResponse成功")]
    public void CreateResponse_Success()
    {
        var encoder = new JsonEncoder();
        var reqMsg = new DefaultMessage();

        var resMsg = encoder.CreateResponse(reqMsg, "Api/Test", 200, "OK");

        Assert.NotNull(resMsg);
        Assert.True(resMsg.Reply);
    }

    [Fact]
    [DisplayName("CreateResponse错误码")]
    public void CreateResponse_Error()
    {
        var encoder = new JsonEncoder();
        var reqMsg = new DefaultMessage();

        var resMsg = encoder.CreateResponse(reqMsg, "Api/Test", 500, "Error");

        Assert.NotNull(resMsg);
        Assert.True(resMsg.Error);
    }
}
