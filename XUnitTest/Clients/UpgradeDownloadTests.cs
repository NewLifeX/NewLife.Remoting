using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Http;
using NewLife.Remoting.Clients;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>升级包下载测试。覆盖响应头超时、响应体超时与失败重连重试</summary>
public class UpgradeDownloadTests
{
    [Fact]
    [DisplayName("响应头超时快速失败，并按重试次数重建连接")]
    public async Task Download_HeaderTimeout_RetriesThenThrows()
    {
        using var server = new StubHttpServer(headerDelay: TimeSpan.FromSeconds(5));
        var dir = NewTempDir();
        try
        {
            var upgrade = new TestUpgrade
            {
                Url = server.Url,
                UpdatePath = dir,
                HeaderTimeout = TimeSpan.FromSeconds(1),
                DownloadTimeout = TimeSpan.FromSeconds(10),
                RetryCount = 1,
                RetryDelay = TimeSpan.FromMilliseconds(100),
            };

            await Assert.ThrowsAsync<TimeoutException>(() => upgrade.Download());

            Assert.Equal(2, server.RequestCount);
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    [DisplayName("首次请求失败后重建连接重试成功且不复用旧连接")]
    public async Task Download_HttpError_RetriesWithNewConnection()
    {
        var body = new Byte[] { 1, 2, 3, 4, 5 };
        using var server = new StubHttpServer(failFirst: true, body: body);
        var dir = NewTempDir();
        try
        {
            var upgrade = new TestUpgrade
            {
                Url = server.Url,
                UpdatePath = dir,
                HeaderTimeout = TimeSpan.FromSeconds(2),
                DownloadTimeout = TimeSpan.FromSeconds(10),
                RetryCount = 2,
                RetryDelay = TimeSpan.FromMilliseconds(100),
            };

            var rs = await upgrade.Download();

            Assert.True(rs);
            Assert.Equal(2, server.RequestCount);
            Assert.Equal(2, server.ConnectionCount);
            Assert.NotNull(upgrade.SourceFile);
            Assert.Equal(body, File.ReadAllBytes(upgrade.SourceFile!));
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    [DisplayName("响应体超时失败")]
    public async Task Download_BodyTimeout_Throws()
    {
        var body = new Byte[1024 * 1024];
        using var server = new StubHttpServer(body: body, bodyDelay: TimeSpan.FromSeconds(5));
        var dir = NewTempDir();
        try
        {
            var upgrade = new TestUpgrade
            {
                Url = server.Url,
                UpdatePath = dir,
                HeaderTimeout = TimeSpan.FromSeconds(2),
                DownloadTimeout = TimeSpan.FromSeconds(1),
                RetryCount = 0,
            };

            await Assert.ThrowsAsync<TimeoutException>(() => upgrade.Download());
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    [DisplayName("响应头超时后重建连接重试成功")]
    public async Task Download_HeaderTimeout_RetriesAndSucceeds()
    {
        var body = new Byte[] { 1, 2, 3, 4, 5 };
        using var server = new StubHttpServer(headerDelay: TimeSpan.FromSeconds(5), headerDelayFirst: true, body: body);
        var dir = NewTempDir();
        try
        {
            var upgrade = new TestUpgrade
            {
                Url = server.Url,
                UpdatePath = dir,
                HeaderTimeout = TimeSpan.FromSeconds(1),
                DownloadTimeout = TimeSpan.FromSeconds(10),
                RetryCount = 1,
                RetryDelay = TimeSpan.FromMilliseconds(100),
            };

            var rs = await upgrade.Download();

            Assert.True(rs);
            Assert.Equal(2, server.RequestCount);
            Assert.Equal(body, File.ReadAllBytes(upgrade.SourceFile!));
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    [DisplayName("响应体超时后重建连接重试成功")]
    public async Task Download_BodyTimeout_RetriesAndSucceeds()
    {
        var body = new Byte[1024 * 1024];
        new Random(12345).NextBytes(body);
        using var server = new StubHttpServer(body: body, bodyDelay: TimeSpan.FromSeconds(5), bodyDelayFirst: true);
        var dir = NewTempDir();
        try
        {
            var upgrade = new TestUpgrade
            {
                Url = server.Url,
                UpdatePath = dir,
                HeaderTimeout = TimeSpan.FromSeconds(2),
                DownloadTimeout = TimeSpan.FromSeconds(1),
                RetryCount = 1,
                RetryDelay = TimeSpan.FromMilliseconds(100),
            };

            var rs = await upgrade.Download();

            Assert.True(rs);
            Assert.Equal(2, server.RequestCount);
            Assert.Equal(body, File.ReadAllBytes(upgrade.SourceFile!));
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    [DisplayName("响应头 Content-Disposition 指定文件名")]
    public async Task Download_UsesContentDispositionFileName()
    {
        var body = new Byte[] { 9, 8, 7 };
        using var server = new StubHttpServer(body: body, contentDisposition: "attachment; filename=\"actual.zip\"");
        var dir = NewTempDir();
        try
        {
            var upgrade = new TestUpgrade
            {
                Url = server.Url,
                UpdatePath = dir,
                HeaderTimeout = TimeSpan.FromSeconds(2),
                DownloadTimeout = TimeSpan.FromSeconds(10),
                RetryCount = 0,
            };

            Assert.True(await upgrade.Download());
            Assert.Equal("actual.zip", Path.GetFileName(upgrade.SourceFile));
            Assert.Equal(body, File.ReadAllBytes(upgrade.SourceFile!));
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    [DisplayName("响应体不完整时下载失败")]
    public async Task Download_IncompleteBody_Throws()
    {
        var body = new Byte[64 * 1024];
        using var server = new StubHttpServer(body: body, truncateBody: true);
        var dir = NewTempDir();
        try
        {
            var upgrade = new TestUpgrade
            {
                Url = server.Url,
                UpdatePath = dir,
                HeaderTimeout = TimeSpan.FromSeconds(2),
                DownloadTimeout = TimeSpan.FromSeconds(10),
                RetryCount = 0,
            };

            var ex = await Record.ExceptionAsync(() => upgrade.Download());

            Assert.NotNull(ex);
            Assert.True(ex is IOException || ex is HttpRequestException, ex.ToString());
            Assert.Null(upgrade.SourceFile);
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    [DisplayName("调用方取消下载时不重试")]
    public async Task Download_CallerCancellation_DoesNotRetry()
    {
        using var server = new StubHttpServer(headerDelay: TimeSpan.FromSeconds(5));
        var dir = NewTempDir();
        try
        {
            using var cts = new CancellationTokenSource(500);
            var upgrade = new TestUpgrade
            {
                Url = server.Url,
                UpdatePath = dir,
                HeaderTimeout = TimeSpan.FromSeconds(10),
                DownloadTimeout = TimeSpan.FromSeconds(10),
                RetryCount = 2,
                RetryDelay = TimeSpan.FromMilliseconds(100),
            };

            var ex = await Record.ExceptionAsync(() => upgrade.Download(cts.Token));

            Assert.IsAssignableFrom<OperationCanceledException>(ex);
            Assert.Equal(1, server.RequestCount);
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    [DisplayName("超时设置为0时不限制下载时长")]
    public async Task Download_ZeroTimeout_NoLimit()
    {
        var body = new Byte[4096];
        new Random(6789).NextBytes(body);
        using var server = new StubHttpServer(body: body, bodyDelay: TimeSpan.FromMilliseconds(800));
        var dir = NewTempDir();
        try
        {
            var upgrade = new TestUpgrade
            {
                Url = server.Url,
                UpdatePath = dir,
                HeaderTimeout = TimeSpan.Zero,
                DownloadTimeout = TimeSpan.Zero,
                RetryCount = 0,
            };

            Assert.True(await upgrade.Download());
            Assert.Equal(body, File.ReadAllBytes(upgrade.SourceFile!));
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private sealed class TestUpgrade : Upgrade
    {
        public TimeSpan HeaderTimeout { get; set; } = TimeSpan.FromSeconds(15);

        public TimeSpan DownloadTimeout { get; set; } = TimeSpan.FromSeconds(300);

        public Int32 RetryCount { get; set; } = 2;

        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

        protected override HttpDownloadOptions CreateDownloadOptions() => new()
        {
            HeaderTimeout = HeaderTimeout,
            DownloadTimeout = DownloadTimeout,
            RetryCount = RetryCount,
            RetryDelay = RetryDelay,
        };
    }

    private static String NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "UpgradeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteDir(String dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch { }
    }

    /// <summary>最小 HTTP 桩。支持延迟响应头、延迟响应体、首次失败、截断响应体与自定义文件名</summary>
    private sealed class StubHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly TimeSpan _headerDelay;
        private readonly TimeSpan _bodyDelay;
        private readonly Boolean _failFirst;
        private readonly Boolean _headerDelayFirst;
        private readonly Boolean _bodyDelayFirst;
        private readonly Boolean _truncateBody;
        private readonly String? _contentDisposition;
        private readonly Byte[] _body;
        private Int32 _requestCount;
        private Int32 _connectionCount;

        public String Url { get; }

        public Int32 RequestCount => Volatile.Read(ref _requestCount);

        public Int32 ConnectionCount => Volatile.Read(ref _connectionCount);

        public StubHttpServer(
            TimeSpan headerDelay = default,
            TimeSpan bodyDelay = default,
            Boolean failFirst = false,
            Boolean headerDelayFirst = false,
            Boolean bodyDelayFirst = false,
            Boolean truncateBody = false,
            String? contentDisposition = null,
            Byte[]? body = null)
        {
            _headerDelay = headerDelay;
            _bodyDelay = bodyDelay;
            _failFirst = failFirst;
            _headerDelayFirst = headerDelayFirst;
            _bodyDelayFirst = bodyDelayFirst;
            _truncateBody = truncateBody;
            _contentDisposition = contentDisposition;
            _body = body ?? [1, 2, 3];

            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/staragent.zip";

            _ = Task.Run(AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cts.Token);
                }
                catch
                {
                    break;
                }

                Interlocked.Increment(ref _connectionCount);
                _ = Task.Run(() => HandleAsync(client));
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using var c = client;
            try
            {
                using var s = c.GetStream();

                // 同一个连接可能承载多次请求（首次 503 后保持连接），用于验证重试是否新建连接
                while (!_cts.IsCancellationRequested)
                {
                    // 读取请求头
                    var buf = new Byte[4096];
                    var sb = new StringBuilder();
                    while (!sb.ToString().Contains("\r\n\r\n"))
                    {
                        var n = await s.ReadAsync(buf, 0, buf.Length, _cts.Token).ConfigureAwait(false);
                        if (n <= 0) return;

                        sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                        if (sb.Length > 8192) break;
                    }

                    var count = Interlocked.Increment(ref _requestCount);

                    if (_headerDelay > TimeSpan.Zero && (!_headerDelayFirst || count == 1))
                        await Task.Delay(_headerDelay, _cts.Token).ConfigureAwait(false);

                    if (_failFirst && count == 1)
                    {
                        // 不发送 Connection: close，保持连接可复用；重试若复用该连接，ConnectionCount 将为 1
                        await WriteAsync(s, "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                        continue;
                    }

                    var header = String.IsNullOrEmpty(_contentDisposition)
                        ? $"HTTP/1.1 200 OK\r\nContent-Type: application/zip\r\nContent-Length: {_body.Length}\r\nConnection: close\r\n\r\n"
                        : $"HTTP/1.1 200 OK\r\nContent-Type: application/zip\r\nContent-Length: {_body.Length}\r\nContent-Disposition: {_contentDisposition}\r\nConnection: close\r\n\r\n";
                    await WriteAsync(s, header).ConfigureAwait(false);

                    if (_bodyDelay > TimeSpan.Zero && (!_bodyDelayFirst || count == 1))
                    {
                        // 先发一小段，随后停顿，触发响应体超时
                        var first = Math.Min(1024, _body.Length);
                        await s.WriteAsync(_body.AsMemory(0, first), _cts.Token).ConfigureAwait(false);
                        await Task.Delay(_bodyDelay, _cts.Token).ConfigureAwait(false);
                        if (_truncateBody) return;
                        if (_body.Length > first) await s.WriteAsync(_body.AsMemory(first), _cts.Token).ConfigureAwait(false);
                    }
                    else if (_truncateBody)
                    {
                        // Content-Length 大于实际发送量，模拟上游提前断流
                        var first = Math.Max(0, _body.Length - 1);
                        await s.WriteAsync(_body.AsMemory(0, first), _cts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await s.WriteAsync(_body, _cts.Token).ConfigureAwait(false);
                    }

                    return;
                }
            }
            catch
            {
                // 客户端超时断开属于预期
            }
        }

        private static async Task WriteAsync(NetworkStream s, String text)
        {
            var buf = Encoding.ASCII.GetBytes(text);
            await s.WriteAsync(buf);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _cts.Dispose();
        }
    }
}
