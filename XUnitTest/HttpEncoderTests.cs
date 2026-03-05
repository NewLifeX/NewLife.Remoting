using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife;
using NewLife.Data;
using NewLife.Remoting.Http;
using Xunit;

namespace XUnitTest;

public class HttpEncoderTests
{
    [Fact]
    [DisplayName("Encode空值返回null")]
    public void Encode_NullValue_ReturnsNull()
    {
        var encoder = new HttpEncoder();
        var result = encoder.Encode("test", 0, null);

        Assert.Null(result);
    }

    [Fact]
    [DisplayName("Encode异常值返回消息")]
    public void Encode_ExceptionValue()
    {
        var encoder = new HttpEncoder();
        var ex = new InvalidOperationException("test error");
        var result = encoder.Encode("test", 500, ex);

        Assert.NotNull(result);
        var json = result!.ToStr();
        Assert.Contains("test error", json);
    }

    [Fact]
    [DisplayName("Encode_IPacket直接透传")]
    public void Encode_PacketValue_PassThrough()
    {
        var encoder = new HttpEncoder();
        var pk = new ArrayPacket("hello"u8.ToArray());
        var result = encoder.Encode("test", 0, pk);

        Assert.Equal(pk, result);
    }

    [Fact]
    [DisplayName("Encode对象序列化为Json")]
    public void Encode_ObjectValue()
    {
        var encoder = new HttpEncoder();
        var result = encoder.Encode("test", 0, new { name = "test", age = 18 });

        Assert.NotNull(result);
        var json = result!.ToStr();
        Assert.Contains("test", json);
    }

    [Fact]
    [DisplayName("Encode使用HttpStatus模式")]
    public void Encode_UseHttpStatus()
    {
        var encoder = new HttpEncoder { UseHttpStatus = true };
        var result = encoder.Encode("test", 200, new { name = "hello" });

        Assert.NotNull(result);
        var json = result!.ToStr();
        // UseHttpStatus模式下不包装action/code
        Assert.Contains("hello", json);
    }

    [Fact]
    [DisplayName("DecodeParameters空数据")]
    public void DecodeParameters_NullData()
    {
        var encoder = new HttpEncoder();
        var result = encoder.DecodeParameters("test", null, new HttpMessage());

        Assert.Null(result);
    }

    [Fact]
    [DisplayName("DecodeParameters表单数据")]
    public void DecodeParameters_FormData()
    {
        var encoder = new HttpEncoder();
        var data = new ArrayPacket("key1=value1&key2=value2"u8.ToArray());
        var msg = new HttpMessage();

        var result = encoder.DecodeParameters("test", data, msg);

        Assert.NotNull(result);
        var dic = result as IDictionary<String, Object?>;
        Assert.NotNull(dic);
        Assert.Equal("value1", dic!["key1"]?.ToString());
        Assert.Equal("value2", dic["key2"]?.ToString());
    }

    [Fact]
    [DisplayName("DecodeResult空数据")]
    public void DecodeResult_NullData()
    {
        var encoder = new HttpEncoder();
        var result = encoder.DecodeResult("test", null!, new HttpMessage(), typeof(String));

        Assert.Null(result);
    }

    [Fact]
    [DisplayName("DecodeResult返回字符串")]
    public void DecodeResult_StringReturn()
    {
        var encoder = new HttpEncoder();
        var data = new ArrayPacket("hello world"u8.ToArray());
        var result = encoder.DecodeResult("test", data, new HttpMessage(), typeof(String));

        Assert.Equal("hello world", result);
    }

    [Fact]
    [DisplayName("DecodeResult基础类型")]
    public void DecodeResult_PrimitiveType()
    {
        var encoder = new HttpEncoder();
        var data = new ArrayPacket("42"u8.ToArray());
        var result = encoder.DecodeResult("test", data, new HttpMessage(), typeof(Int32));

        Assert.Equal(42, result);
    }

    [Fact]
    [DisplayName("CreateRequest_GET请求")]
    public void CreateRequest_GetRequest()
    {
        var encoder = new HttpEncoder();
        var msg = encoder.CreateRequest("api/test", null);

        Assert.NotNull(msg);
        Assert.IsType<HttpMessage>(msg);
        var http = (HttpMessage)msg;
        Assert.NotNull(http.Header);
        var header = http.Header!.ToStr();
        Assert.Contains("GET", header);
        Assert.Contains("api/test", header);
        Assert.Contains("HTTP/1.1", header);
    }

    [Fact]
    [DisplayName("CreateRequest带Byte参数")]
    public void CreateRequest_WithByteArray()
    {
        var encoder = new HttpEncoder();
        var data = new Byte[] { 1, 2, 3, 4 };
        var msg = encoder.CreateRequest("api/test", data);

        Assert.NotNull(msg);
        var http = (HttpMessage)msg;
        Assert.NotNull(http.Header);
        var header = http.Header!.ToStr();
        Assert.Contains("Content-Length:4", header);
    }

    [Fact]
    [DisplayName("CreateRequest带字符串参数")]
    public void CreateRequest_WithString()
    {
        var encoder = new HttpEncoder();
        var msg = encoder.CreateRequest("api/test", "hello");

        Assert.NotNull(msg);
        var http = (HttpMessage)msg;
        Assert.NotNull(http.Header);
    }

    [Fact]
    [DisplayName("CreateResponse正常响应")]
    public void CreateResponse_Normal()
    {
        var encoder = new HttpEncoder();
        var reqMsg = new HttpMessage();
        var msg = encoder.CreateResponse(reqMsg, "test", 0, new { data = "ok" });

        Assert.NotNull(msg);
        var http = (HttpMessage)msg;
        Assert.NotNull(http.Header);
        var header = http.Header!.ToStr();
        Assert.Contains("200 OK", header);
        Assert.Contains("Content-Length:", header);
    }

    [Fact]
    [DisplayName("CreateResponse错误响应")]
    public void CreateResponse_Error()
    {
        var encoder = new HttpEncoder();
        var reqMsg = new HttpMessage();
        var msg = encoder.CreateResponse(reqMsg, "test", 500, "error");

        Assert.NotNull(msg);
        var http = (HttpMessage)msg;
        Assert.True(http.Error);
    }

    [Fact]
    [DisplayName("CreateResponse_UseHttpStatus")]
    public void CreateResponse_UseHttpStatus()
    {
        var encoder = new HttpEncoder { UseHttpStatus = true };
        var reqMsg = new HttpMessage();
        var msg = encoder.CreateResponse(reqMsg, "test", 200, "ok");

        var http = (HttpMessage)msg;
        var header = http.Header!.ToStr();
        Assert.Contains("200", header);
        Assert.Contains("OK", header);
    }

    [Fact]
    [DisplayName("CreateResponse_HttpStatus500")]
    public void CreateResponse_UseHttpStatus_500()
    {
        var encoder = new HttpEncoder { UseHttpStatus = true };
        var reqMsg = new HttpMessage();
        var msg = encoder.CreateResponse(reqMsg, "test", 500, "error");

        var http = (HttpMessage)msg;
        var header = http.Header!.ToStr();
        Assert.Contains("500", header);
        Assert.Contains("Error", header);
    }

    [Fact]
    [DisplayName("Decode_GET请求")]
    public void Decode_GetRequest()
    {
        var encoder = new HttpEncoder();

        // 构造一个GET请求消息
        var headerStr = "GET /api/test HTTP/1.1\r\nHost:localhost\r\nConnection:keep-alive";
        var headerBytes = System.Text.Encoding.UTF8.GetBytes(headerStr);
        var http = new HttpMessage
        {
            Header = new ArrayPacket(headerBytes),
        };

        var apiMsg = encoder.Decode(http);

        Assert.NotNull(apiMsg);
        Assert.Equal("api/test", apiMsg!.Action);
    }

    [Fact]
    [DisplayName("Decode_POST请求")]
    public void Decode_PostRequest()
    {
        var encoder = new HttpEncoder();

        var headerStr = "POST /api/submit HTTP/1.1\r\nHost:localhost";
        var headerBytes = System.Text.Encoding.UTF8.GetBytes(headerStr);
        var http = new HttpMessage
        {
            Header = new ArrayPacket(headerBytes),
        };

        var apiMsg = encoder.Decode(http);

        Assert.NotNull(apiMsg);
        Assert.Equal("api/submit", apiMsg!.Action);
    }

    [Fact]
    [DisplayName("Decode空Header返回null")]
    public void Decode_NullHeader()
    {
        var encoder = new HttpEncoder();
        var http = new HttpMessage();

        var result = encoder.Decode(http);
        Assert.Null(result);
    }

    [Fact]
    [DisplayName("Convert转换")]
    public void Convert_Works()
    {
        var encoder = new HttpEncoder();
        var result = encoder.Convert("42", typeof(Int32));

        Assert.Equal(42, result);
    }
}
