using System;
using System.Collections.Generic;
using System.IO;
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
/// <para>多目标框架并行跑测试时，每个测试进程通过临时目录下的独占文件锁认领一个分片，候选端口集合互不相交。</para>
/// <para>探测口径与 NetServer 一致：先按 <c>CheckPort</c> 预检（对端可能设置了 SO_REUSEADDR，裸 bind 会误判“可绑”），再做真实绑定。</para>
/// </remarks>
internal static class TestPort
{
    /// <summary>候选池基址。取 49152 动态端口段以下、避开常见服务端口（如 Syncthing 22000）的区间</summary>
    private const Int32 PoolBase = 38000;

    /// <summary>候选池大小</summary>
    private const Int32 PoolSize = 9000;

    /// <summary>候选池分片数。多目标框架并行跑测试时，每个测试进程独占一个分片，避免两个测试进程选到同一端口</summary>
    private const Int32 ShardCount = 8;

    /// <summary>本进程占用的分片与分片锁。进程退出时由操作系统释放锁</summary>
    private static readonly (Int32 Shard, FileStream? Lock) _slot = AcquireSlot();

    /// <summary>本进程分片在候选池中的起始偏移</summary>
    private static readonly Int32 _shardOffset = _slot.Shard * (PoolSize / ShardCount);

    private static Int32 _cursor = -1;

    /// <summary>占用一个跨进程独占的分片</summary>
    /// <remarks>
    /// 仅按进程号取模会出现两个测试进程落在同一分片的情况（进程号相差分片数的整数倍），
    /// 此时两边探测到同一批“空闲”端口再同时绑定，后绑定者启动失败。
    /// 这里改用临时目录下的独占文件锁标记分片：先打开者独占，其余进程顺延到下一个分片。
    /// </remarks>
    /// <returns>分片序号及持有到进程退出的分片锁</returns>
    private static (Int32 Shard, FileStream? Lock) AcquireSlot()
    {
        var dir = Path.GetTempPath();
        var first = (Int32)(Environment.ProcessId % ShardCount);

        for (var i = 0; i < ShardCount; i++)
        {
            var shard = (first + i) % ShardCount;
            try
            {
                var stream = new FileStream(
                    Path.Combine(dir, $"NewLife.Remoting.TestPort.{shard}.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return (shard, stream);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        // 同机并发测试进程超过分片数时退化为按进程号取模，端口探测仍能挡住多数冲突
        return (first, null);
    }

    /// <summary>本次运行已分配过的端口。不复用，避免 ReuseAddress 下两个实例绑到同一端口</summary>
    /// <remarks>同时把 port+1 一并占位：ApiServer 在 HttpPort=0 时会自动使用主端口+1 作为 HTTP 端口，
    /// 若不占位，并行执行的其它用例可能把该端口再次分配给自己的 SRMP 监听，造成随机绑定失败。</remarks>
    private static readonly HashSet<Int32> _used = [];

    /// <summary>取一个 TCP 与 UDP 均可绑定的显式端口</summary>
    /// <returns>可用于双协议服务器监听的端口</returns>
    public static Int32 PickDual()
    {
        // 只在本进程的分片内探测，保证不同测试进程的候选端口集合互不相交；
        // 末位留空，使 port 与 port+1 始终落在同一分片内
        var slice = PoolSize / ShardCount - 1;
        var start = Interlocked.Increment(ref _cursor);

        for (var i = 0; i < slice; i++)
        {
            var port = PoolBase + _shardOffset + (Int32)((start + i) % slice);

            lock (_used)
            {
                // 同时占住 port+1，避免它被别的用例选作主端口，与 HttpPort=0 自动占用的端口冲突
                if (_used.Contains(port) || _used.Contains(port + 1)) continue;

                _used.Add(port);
                _used.Add(port + 1);
            }

            if (CanBind(port, SocketType.Stream) && CanBind(port, SocketType.Dgram)) return port;
        }

        throw new InvalidOperationException($"分片 {_slot.Shard} 的候选端口段 {PoolBase + _shardOffset}~{PoolBase + _shardOffset + slice - 1} 内没有同时可用于 TCP/UDP 的端口");
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
