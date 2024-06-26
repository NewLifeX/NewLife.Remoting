﻿using NewLife.Data;

namespace NewLife.Remoting;

/// <summary>Api请求/响应</summary>
public class ApiMessage
{
    /// <summary>动作</summary>
    public String Action { get; set; } = null!;

    /// <summary>响应码。请求没有该字段</summary>
    public Int32 Code { get; set; }

    /// <summary>数据。请求参数或响应内容</summary>
    public Packet? Data { get; set; }

    /// <summary>已重载。友好表示该消息</summary>
    /// <returns></returns>
    public override String ToString() => Code > 0 ? $"{Action}[{Code}]" : Action;
}