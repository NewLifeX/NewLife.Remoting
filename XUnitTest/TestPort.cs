using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NewLife;
using NewLife.Net;

namespace XUnitTest;

/// <summary>测试用显式端口分配。双协议服务器不再依赖 Port=0 的系统随机分配</summary>
/// <remarks>
/// <para>新版 NewLife.Core 取消了 Port=0 的随机端口重试：TCP 与 UDP 的系统保留段相互独立，
/// 双协议服务器用 Port=0 时，先启动的协议选中的端口可能落在另一协议的保留段（WSAEACCES），启动直接失败。</para>
/// <para>本类在候选池内顺序探测第一个 TCP 与 UDP 均可绑定的端口，显式交给服务器，不经过系统分配与跨协议端口继承；
/// 进程内不复用已分配过的端口，避免上一个用例（ReuseAddress 开启、尚未完全释放）与新用例绑到同一端口。</para>
/// <para>探测口径与 NetServer 一致：先按 <c>CheckPort</c> 预检（对端可能设置了 SO_REUSEADDR，裸 bind 会误判“可绑”），再做真实绑定。</para>
/// </remarks>
internal static class TestPort
{
    /// <summary>候选池基址。取 49152 动态端口段以下、避开常见服务端口（如 Syncthing 22000）的区间</summary>
    private const Int32 PoolBase = 38000;

    /// <summary>候选池大小</summary>
    private const Int32 PoolSize = 9000;

    private static Int32 _cursor = -1;

    /// <summary>本次运行已分配过的端口。不复用，避免 ReuseAddress 下两个实例绑到同一端口</summary>
    private static readonly HashSet<Int32> _used = [];

    /// <summary>取一个 TCP 与 UDP 均可绑定的显式端口</summary>
    /// <returns>可用于双协议服务器监听的端口</returns>
    public static Int32 PickDual()
    {
        var start = Interlocked.Increment(ref _cursor);

        for (var i = 0; i < PoolSize; i++)
        {
            var port = PoolBase + (start + i) % PoolSize;

            lock (_used)
            {
                if (!_used.Add(port)) continue;
            }

            if (CanBind(port, SocketType.Stream) && CanBind(port, SocketType.Dgram)) return port;
        }

        throw new InvalidOperationException($"候选池 {PoolBase}~{PoolBase + PoolSize - 1} 内没有同时可用于 TCP/UDP 的端口");
    }

    private static Boolean CanBind(Int32 port, SocketType type)
    {
        var netType = type == SocketType.Stream ? NetType.Tcp : NetType.Udp;

        // 与 NetServer.CreateServer 相同口径：已有监听者的端口一律跳过。
        // 对端可能设置了 SO_REUSEADDR（如 Syncthing 等 Go 程序），此时裸 bind 探测会误判“可绑”，
        // 而 NetServer 会按 CheckPort 拒绝该地址，导致“Failed to listen to all ports”
        if (IPAddress.Any.CheckPort(netType, port)) return false;

        var protocol = type == SocketType.Stream ? System.Net.Sockets.ProtocolType.Tcp : System.Net.Sockets.ProtocolType.Udp;
        using var socket = new Socket(AddressFamily.InterNetwork, type, protocol);
        try
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
