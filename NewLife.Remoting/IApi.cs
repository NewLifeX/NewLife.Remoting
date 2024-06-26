﻿using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("XUnitTest, PublicKey=00240000048000001401000006020000002400005253413100080000010001000d41eb3bdab5c2150958b46c95632b7e4dcb0af77ed8637bd8543875bc2443d01273143bb46655a48a92efa76251adc63ccca6d0e9cef2e0ce93e32b5043bea179a6c710981be4a71703a03e10960643f7df091f499cf60183ef0e4e4e2eebf26e25cea0eebf87c8a6d7f8130c283fc3f747cb90623f0aaa619825e3fcd82f267a0f4bfd26c9f2a6b5a62a6b180b4f6d1d091fce6bd60a9aa9aa5b815b833b44e0f2e58b28a354cb20f52f31bb3b3a7c54f515426537e41f9c20c07e51f9cab8abc311daac19a41bd473a51c7386f014edf1863901a5c29addc89da2f2659c9c1e95affd6997396b9680e317c493e974a813186da277ff9c1d1b30e33cb5a2f6")]

namespace NewLife.Remoting;

/// <summary>Api接口</summary>
/// <remarks>
/// 在基于令牌Token的无状态验证模式中，可以借助Token重写IApiHandler.Prepare，来达到同一个Token共用相同的IApiSession.Items
/// </remarks>
public interface IApi
{
    /// <summary>会话</summary>
    IApiSession Session { get; set; }
}