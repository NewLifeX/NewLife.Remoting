using System;
using System.Text;
using System.ComponentModel;
using NewLife.Data;
using NewLife.Remoting.Http;
using Xunit;

namespace XUnitTest;

public class HttpMessageTests
{
    [Theory]
    [InlineData("GET", "/")]
    [InlineData("POST", "/api")]
    [InlineData("PUT", "/a/b")]
    [InlineData("DELETE", "/items/1")]
    [InlineData("PATCH", "/items/1")]
    [InlineData("HEAD", "/health")]
    [InlineData("OPTIONS", "*")]
    [DisplayName("Read可以解析请求行并填充Method与Uri")]
    public void ReadCanParseMethodAndUri(String method, String uri)
    {
        var text = $"{method} {uri} HTTP/1.1\r\nHost:example\r\n\r\n";
        var pk = new ArrayPacket(Encoding.ASCII.GetBytes(text));

        var msg = new HttpMessage();
        var ok = msg.Read(pk);

        Assert.True(ok);
        Assert.Equal(method, msg.Method);
        Assert.Equal(uri, msg.Uri);
    }

    [Fact(DisplayName = "ParseHeaders解析Host时不截断端口")]
    public void ParseHeaders_ShouldKeepPortInHostHeader()
    {
        var text = "GET / HTTP/1.1\r\nHost: 127.0.0.1:8080\r\n\r\n";
        var pk = new ArrayPacket(Encoding.ASCII.GetBytes(text));

        var msg = new HttpMessage();
        Assert.True(msg.Read(pk));

        Assert.True(msg.ParseHeaders());
        Assert.NotNull(msg.Headers);
        Assert.Equal("127.0.0.1:8080", msg.Headers["Host"]);
    }

    [Fact(DisplayName = "ParseHeaders解析Content-Length并忽略大小写")]
    public void ParseHeaders_ShouldParseContentLength_IgnoringCase()
    {
        var text = "POST /api HTTP/1.1\r\ncontent-length: 123\r\n\r\n";
        var pk = new ArrayPacket(Encoding.ASCII.GetBytes(text));

        var msg = new HttpMessage();
        Assert.True(msg.Read(pk));

        Assert.True(msg.ParseHeaders());
        Assert.Equal(123, msg.ContentLength);
    }

    [Fact(DisplayName = "ParseHeaders支持冒号两侧空白并裁剪")]
    public void ParseHeaders_ShouldTrimWhitespaceAroundNameAndValue()
    {
        var text = "GET / HTTP/1.1\r\n  Host\t:\t127.0.0.1:8080  \r\n\r\n";
        var pk = new ArrayPacket(Encoding.ASCII.GetBytes(text));

        var msg = new HttpMessage();
        Assert.True(msg.Read(pk));

        Assert.True(msg.ParseHeaders());
        Assert.Equal("127.0.0.1:8080", msg.Headers["Host"]);
    }

    [Fact(DisplayName = "ParseHeaders支持空值头部")]
    public void ParseHeaders_ShouldAllowEmptyHeaderValue()
    {
        var text = "GET / HTTP/1.1\r\nX-Empty:\r\n\r\n";
        var pk = new ArrayPacket(Encoding.ASCII.GetBytes(text));

        var msg = new HttpMessage();
        Assert.True(msg.Read(pk));

        Assert.True(msg.ParseHeaders());
        Assert.Equal(String.Empty, msg.Headers["X-Empty"]);
    }

    [Fact(DisplayName = "ParseHeaders遇到不规范请求行不抛异常")]
    public void ParseHeaders_ShouldNotThrow_OnInvalidRequestLine()
    {
        // 缺少空格分隔，ParseHeaders 不应该抛出异常
        var text = "GET/ HTTP/1.1\r\nHost:example\r\n\r\n";
        var pk = new ArrayPacket(Encoding.ASCII.GetBytes(text));

        var msg = new HttpMessage();
        Assert.True(msg.Read(pk));

        var ex = Record.Exception(() => msg.ParseHeaders());
        Assert.Null(ex);
    }

    [Fact(DisplayName = "ParseHeaders不要忽略名称为空白的头部行")]
    public void ParseHeaders_ShouldNotIgnore_WhitespaceOnlyHeaderName()
    {
        var text = "GET / HTTP/1.1\r\n   : value\r\nHost:example\r\n\r\n";
        var pk = new ArrayPacket(Encoding.ASCII.GetBytes(text));

        var msg = new HttpMessage();
        Assert.True(msg.Read(pk));

        Assert.True(msg.ParseHeaders());
        Assert.True(msg.Headers.ContainsKey("") );
        Assert.Equal("example", msg.Headers["Host"]);
    }
}
