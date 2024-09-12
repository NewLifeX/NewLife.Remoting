using System;
using System.Collections.Generic;
using System.IO;
using NewLife;
using NewLife.Buffers;
using NewLife.Data;
using NewLife.Messaging;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest;

public class JsonEncoderTests
{
    [Fact]
    public void EncodeRequest()
    {
        var encoder = new JsonEncoder();

        var name = "api/test";
        Packet? value = null;

        // 简洁请求
        {
            value = null;
            var pk = (ArrayPacket)encoder.Encode(name, null, value);
            Assert.Equal(1 + name.Length, pk.Total);
            Assert.Null(pk.Next);
            Assert.Equal(8, pk.Offset);

            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());
            if (reader.FreeCapacity > 0) Assert.Equal(0u, reader.ReadUInt32());
        }
        // 简洁请求，带空数据
        {
            value = new Byte[0];
            var pk = (ArrayPacket)encoder.Encode(name, null, value);
            Assert.Equal(1 + name.Length + 4, pk.Total);
            Assert.NotNull(pk.Next);
            Assert.Equal(value, pk.Next);
            Assert.Equal(8, pk.Offset);

            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());
            if (reader.FreeCapacity > 0) Assert.Equal(0u, reader.ReadUInt32());
        }
        // 标准请求，带数据体
        {
            value = Rand.NextBytes(64);
            var pk = (ArrayPacket)encoder.Encode(name, null, value);
            Assert.Equal(1 + name.Length + 4 + value.Count, pk.Total);
            Assert.NotNull(pk.Next);
            Assert.Equal(value, pk.Next);
            Assert.Equal(8, pk.Offset);

            // 拷贝一次，拉平。因为SpanReader不支持跨包读取
            pk = (ArrayPacket)pk.ToArray();
            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());

            var len = reader.ReadInt32();
            Assert.Equal(value.Total, len);
            var buf = reader.ReadBytes(len);
            Assert.Equal(value.ToHex(64), buf.ToHex(64));
        }
    }

    [Fact]
    public void EncodeResponse()
    {
        var encoder = new JsonEncoder();

        var name = "api/test";
        Packet? value = null;

        // 简洁响应
        {
            // 错误码200等同于0，表示成功
            value = null;
            var pk = (ArrayPacket)encoder.Encode(name, 200, value);
            Assert.Equal(1 + name.Length, pk.Total);
            Assert.Null(pk.Next);
            Assert.Equal(8, pk.Offset);

            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());
            if (reader.FreeCapacity > 0) Assert.Equal(0u, reader.ReadUInt32());
        }
        // 简洁响应，带异常
        {
            value = new Byte[0];
            var pk = (ArrayPacket)encoder.Encode(name, 500, value);
            Assert.Equal(1 + name.Length + 4 + 4 + value.Count, pk.Total);
            Assert.NotNull(pk.Next);
            Assert.Equal(value, pk.Next);
            Assert.Equal(8, pk.Offset);

            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());
            // 错误码占4字节
            Assert.Equal(500u, reader.ReadUInt32());
        }
        // 标准响应，带数据体
        {
            value = Rand.NextBytes(64);
            var pk = (ArrayPacket)encoder.Encode(name, 0, value);
            Assert.Equal(1 + name.Length + 4 + value.Count, pk.Total);
            Assert.NotNull(pk.Next);
            Assert.Equal(value, pk.Next);
            Assert.Equal(8, pk.Offset);

            // 拷贝一次，拉平。因为SpanReader不支持跨包读取
            pk = (ArrayPacket)pk.ToArray();
            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());
            // 正常响应不需要错误码，直接写数据体长度
            Assert.Equal(value.Count, reader.ReadInt32());

            var span = reader.ReadBytes(value.Count);
            Assert.Equal(value.ToArray(), span);

            var hex1 = value.ToHex(64);
            var hex2 = span.ToHex(64);
            Assert.Equal(hex1, hex2);
            //Assert.Equal(value.Count, (Int32)pk.Slice(1 + name.Length, 4).ReadUInt32());
            //Assert.Equal(value.ToHex(), pk.Slice(1 + name.Length + 4, value.Count).ToHex());
        }
    }

    [Fact]
    public void CreateRequest()
    {
        var encoder = new JsonEncoder();

        var name = "api/test";

        // 简洁请求
        {
            var req = encoder.CreateRequest(name, null);
            var pk = (ArrayPacket)req.Payload;
            Assert.Equal(1 + name.Length, pk.Total);
            Assert.Null(pk.Next);
            Assert.Equal(8, pk.Offset);

            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());
            if (reader.FreeCapacity > 0) Assert.Equal(0u, reader.ReadUInt32());

            var am = encoder.Decode(req);
            Assert.Equal(name, am.Action);
            Assert.Equal(0, am.Code);
            Assert.Null(am.Data);

            // 解压参数
            var ps = encoder.DecodeParameters(name, am.Data, req);
            Assert.Null(ps);
        }
    }

    [Fact]
    public void CreateRequestWithEmptyArgs()
    {
        var encoder = new JsonEncoder();

        var name = "api/test";

        // 简洁请求，带空数据
        {
            var args = new Object();
            var req = encoder.CreateRequest(name, args);
            var pk = (ArrayPacket)req.Payload;
            Assert.Equal(1 + name.Length + 4 + args.ToJson().Length, pk.Total);
            Assert.NotNull(pk.Next);
            Assert.Equal(8, pk.Offset);

            // 拷贝一次，拉平。因为SpanReader不支持跨包读取
            pk = (ArrayPacket)req.Payload.ToArray();
            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());

            // 2字节长度，就是{}
            Assert.Equal(2, reader.ReadInt32());

            var json = args.ToJson();
            Assert.Equal(2, json.Length);
            Assert.Equal(json, reader.ReadString(-1));

            var am = encoder.Decode(req);
            Assert.Equal(name, am.Action);
            Assert.Equal(0, am.Code);
            Assert.Equal(json, am.Data.ToStr());

            // 解压参数
            var ps = encoder.DecodeParameters(name, am.Data, req);
            //Assert.Equal(typeof(Object), ps.GetType());
            Assert.Empty(ps as IDictionary<String, Object>);
        }
    }

    [Fact]
    public void CreateRequestFull()
    {
        var encoder = new JsonEncoder();

        var name = "api/test";

        // 标准请求，带数据体
        {
            var args = new UserInfo { Name = "Stone", Age = 18 };
            var req = encoder.CreateRequest(name, args);
            var pk = (ArrayPacket)req.Payload;
            Assert.Equal(1 + name.Length + 4 + args.ToJson().Length, pk.Total);
            Assert.NotNull(pk.Next);
            Assert.Equal(8, pk.Offset);

            Assert.Equal(name, pk.Slice(1, name.Length).ToStr());
            var json = args.ToJson();
            Assert.Equal(json, pk.Slice(1 + name.Length + 4, json.Length).ToStr());

            var am = encoder.Decode(req);
            Assert.Equal(name, am.Action);
            Assert.Equal(0, am.Code);
            Assert.Equal(json, am.Data.ToStr());

            // 解压参数
            var ps = encoder.DecodeParameters(name, am.Data, req) as IDictionary<String, Object>;
            Assert.Equal("Stone", ps["name"]);
            Assert.Equal(18, ps["age"]);
        }
    }

    class UserInfo
    {
        public String Name { get; set; }
        public Int32 Age { get; set; }
    }

    [Fact]
    public void CreateResponse()
    {
        var encoder = new JsonEncoder();

        var name = "api/test";
        var req = new DefaultMessage { Sequence = Rand.Next() };

        // 简洁响应
        {
            // 错误码200等同于0，表示成功
            var res = encoder.CreateResponse(req, name, 200, null);
            var pk = (ArrayPacket)res.Payload;
            Assert.Equal(1 + name.Length, pk.Total);
            Assert.Null(pk.Next);
            Assert.Equal(8, pk.Offset);

            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());
            if (reader.FreeCapacity > 0) Assert.Equal(0, reader.ReadInt32());

            var dm = res as DefaultMessage;
            Assert.True(dm.Reply);
            Assert.False(dm.Error);
            Assert.Equal(req.Sequence, dm.Sequence);

            var am = encoder.Decode(res);
            Assert.Equal(name, am.Action);
            Assert.Equal(0, am.Code);
            Assert.Null(am.Data);

            // 解压参数
            var ps = encoder.DecodeResult(name, am.Data, req, null);
            Assert.Null(ps);
        }
    }

    [Fact]
    public void CreateResponseWithError()
    {
        var encoder = new JsonEncoder();

        var name = "api/test";
        var req = new DefaultMessage { Sequence = Rand.Next() };

        // 简洁响应，带空数据
        {
            var value = "this is an error message";
            var res = encoder.CreateResponse(req, name, 500, value);
            var pk = (ArrayPacket)res.Payload;
            Assert.Equal(1 + name.Length + 4 + 4 + value.Length, pk.Total);
            Assert.NotNull(pk.Next);
            Assert.Equal(8, pk.Offset);

            // 拷贝一次，拉平。因为SpanReader不支持跨包读取
            pk = (ArrayPacket)pk.ToArray();
            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());
            Assert.Equal(500, reader.ReadInt32());

            var len = reader.ReadInt32();
            Assert.Equal(value.Length, len);
            Assert.Equal(value, reader.ReadString(len));

            var dm = res as DefaultMessage;
            Assert.True(dm.Reply);
            Assert.True(dm.Error);
            Assert.Equal(req.Sequence, dm.Sequence);

            var am = encoder.Decode(res);
            Assert.Equal(name, am.Action);
            Assert.Equal(500, am.Code);
            Assert.Equal(value, am.Data.ToStr());

            // 解压参数
            var ps = encoder.DecodeResult(name, am.Data, req, null);
            Assert.Equal(value, ps);
        }
    }

    [Fact]
    public void CreateResponseFull()
    {
        var encoder = new JsonEncoder();

        var name = "api/test";
        var req = new DefaultMessage { Sequence = Rand.Next() };

        // 标准响应，带数据体
        {
            var value = new UserInfo { Name = "Stone", Age = 18 };
            var res = encoder.CreateResponse(req, name, 0, value);
            var pk = (ArrayPacket)res.Payload;
            Assert.Equal(1 + name.Length + 4 + value.ToJson().Length, pk.Total);
            Assert.NotNull(pk.Next);
            Assert.Equal(8, pk.Offset);

            // 拷贝一次，拉平。因为SpanReader不支持跨包读取
            pk = (ArrayPacket)pk.ToArray();
            var reader = new SpanReader(pk.GetSpan());

            Assert.Equal(name, reader.ReadString());

            var json = value.ToJson();
            Assert.Equal(json.Length, reader.ReadInt32());
            Assert.Equal(json, reader.ReadString(-1));

            var dm = res as DefaultMessage;
            Assert.True(dm.Reply);
            Assert.False(dm.Error);
            Assert.Equal(req.Sequence, dm.Sequence);

            var am = encoder.Decode(res);
            Assert.Equal(name, am.Action);
            Assert.Equal(0, am.Code);
            Assert.Equal(json, am.Data.ToStr());

            // 解压参数
            var ps = encoder.DecodeResult(name, am.Data, req, value.GetType());
            Assert.Equal(value.GetType(), ps.GetType());
            Assert.Equal(json, ps.ToJson());

            ps = encoder.DecodeResult(name, am.Data, req, typeof(Object));
            var dic = ps as IDictionary<String, Object>;
            Assert.Equal(2, dic.Count);
            Assert.Equal("Stone", dic["name"]);
            Assert.Equal(18, dic["age"]);
        }
    }

    //[Fact]
    //public void DecodeParameters()
    //{
    //}

    //[Fact]
    //public void DecodeResult()
    //{
    //}

    [Fact]
    public void Convert()
    {
        var encoder = new JsonEncoder();

        var value = new UserInfo { Name = "Stone", Age = 18 };
        var json = value.ToJson();
        var obj = new JsonParser(json).Decode();

        var rs = encoder.Convert(obj, typeof(UserInfo));
        Assert.Equal(value.ToJson(), rs.ToJson());
    }

    [Fact]
    public void EncodeValue()
    {
        var encoder = new JsonEncoder();

        {
            var value = new Packet(Rand.NextBytes(64));
            var pk = encoder.EncodeValue(value, out var str);
            Assert.Equal(value, pk);
            Assert.Empty(str);
        }
        {
            var value = Rand.NextBytes(64);
            var pk = encoder.EncodeValue(value, out var str);
            Assert.Equal(value.ToHex(), pk.ToHex(64));
            Assert.Empty(str);
        }
        {
            var value = new UserInfo2 { Name = "Stone", Age = 18 };
            var pk = encoder.EncodeValue(value, out var str);
            Assert.Equal(1 + value.Name.Length + 1, pk.Total);
            Assert.Empty(str);
        }
        {
            var value = DateTime.Now;
            var pk = encoder.EncodeValue(value, out var str);
            Assert.Equal(value.ToFullString(), pk.ToStr());
            Assert.Equal(value.ToFullString(), str);
        }
        {
            var value = 123.456d;
            var pk = encoder.EncodeValue(value, out var str);
            Assert.Equal(value.ToString(), pk.ToStr());
            Assert.Equal(value.ToString(), str);
        }
        {
            var value = new UserInfo { Name = "Stone", Age = 18 };
            var pk = encoder.EncodeValue(value, out var str);
            var json = value.ToJson();
            Assert.Equal(json, pk.ToStr());
            Assert.Equal(json, str);
        }
        {
            var value = new Exception("this is an error");
            var pk = encoder.EncodeValue(value, out var str);
            Assert.Equal(value.Message, pk.ToStr());
            Assert.Equal(value.Message, str);
        }
    }

    class UserInfo2 : IAccessor
    {
        public String Name { get; set; }
        public Int32 Age { get; set; }

        public Boolean Read(Stream stream, Object context)
        {
            var reader = new Binary { Stream = stream, EncodeInt = true };
            Name = reader.Read<String>();
            Age = reader.Read<Int32>();

            return true;
        }

        public Boolean Write(Stream stream, Object context)
        {
            var writer = new Binary { Stream = stream, EncodeInt = true };
            writer.Write(Name);
            writer.Write(Age);

            return true;
        }
    }
}
