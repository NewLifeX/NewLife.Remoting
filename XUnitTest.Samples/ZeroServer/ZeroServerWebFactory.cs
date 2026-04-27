extern alias ZeroServer;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NewLife;
using NewLife.Log;
using Xunit;

namespace XUnitTest.Samples.ZeroServer;

/// <summary>ZeroServer 集成测试 WebApplicationFactory。使用真实 Kestrel（端口0），完全独立临时数据库</summary>
public sealed class ZeroServerWebFactory : WebApplicationFactory<ZeroServer::Program>, IAsyncLifetime
{
    #region 属性
    /// <summary>服务端真实监听地址，如 http://127.0.0.1:12345</summary>
    public String BaseUrl { get; private set; } = null!;

    /// <summary>临时数据/配置目录（测试结束后删除）</summary>
    public String TempDir { get; private set; } = null!;

    /// <summary>原始工作目录（析构时恢复）</summary>
    private String _origCurrentDir = null!;
    #endregion

    #region WebApplicationFactory 重写
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        XTrace.UseConsole();

        // 每次 WAF 初始化时创建独立的临时目录
        TempDir = Path.Combine(Path.GetTempPath(), "ZeroServerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDir);
        Directory.CreateDirectory(Path.Combine(TempDir, "config"));
        Directory.CreateDirectory(Path.Combine(TempDir, "Data"));

        // 切换工作目录：ClientSetting 的 config/ZeroClient.config 会写到 TempDir
        _origCurrentDir = Environment.CurrentDirectory;
        Environment.CurrentDirectory = TempDir;

        // Testing 环境：Program.cs 会跳过 RegisterService 和 ClientTest 调用
        builder.UseEnvironment("Testing");

        // 覆盖连接串为临时 SQLite 文件，每次测试数据完全隔离
        // ZeroServer 实体 ConnName 映射：Node→"Zero"，NodeOnline/NodeHistory→"StardustData"，Area→"Membership"
        // 同时覆盖 "IoT"，防止 EntityFactory.InitAll() 用 appsettings.json 的相对路径初始化 IoTZero 的实体连接
        var dataDir      = Path.Combine(TempDir, "Data");
        var dbZero       = $"Data Source={Path.Combine(dataDir, "Zero.db")};Provider=Sqlite";
        var dbStardust   = $"Data Source={Path.Combine(dataDir, "StardustData.db")};Provider=Sqlite";
        var dbMembership = $"Data Source={Path.Combine(dataDir, "Membership.db")};Provider=Sqlite";
        var dbIoT        = $"Data Source={Path.Combine(dataDir, "IoT.db")};Provider=Sqlite";

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<String, String?>
            {
                ["ConnectionStrings:Zero"]        = dbZero,
                ["ConnectionStrings:StardustData"] = dbStardust,
                ["ConnectionStrings:Membership"]  = dbMembership,
                ["ConnectionStrings:IoT"]         = dbIoT,
                ["XCodeSetting:ShowSQL"]          = "false",
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder) => base.CreateHost(builder);
    #endregion

    #region IAsyncLifetime
    /// <summary>初始化：启用真实 Kestrel（端口0），触发 WAF 启动服务，读取实际监听地址</summary>
    async Task IAsyncLifetime.InitializeAsync()
    {
        UseKestrel(0);  // 必须在服务器启动前调用：告知 WAF 使用真实 Kestrel 而非 TestServer
        _ = Services;   // 触发 WebApplicationFactory.EnsureHost() → ConfigureHostBuilder → CreateHost
        // WAF 在 CreateHost 后调用 TryExtractHostAddress，自动将实际端口写入 ClientOptions.BaseAddress
        BaseUrl = ClientOptions.BaseAddress?.ToString()?.TrimEnd('/') ?? "";
        XTrace.WriteLine("ZeroServerWebFactory 启动，地址：{0}", BaseUrl);
        await Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
    #endregion

    #region 析构
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // 恢复工作目录
            if (!_origCurrentDir.IsNullOrEmpty())
                Environment.CurrentDirectory = _origCurrentDir;

            // 清理临时目录
            if (!TempDir.IsNullOrEmpty() && Directory.Exists(TempDir))
            {
                try { Directory.Delete(TempDir, true); }
                catch { /* 忽略清理异常 */ }
            }
        }
    }
    #endregion

    #region 辅助
    /// <summary>读取 ZeroClient.config 文件内容（登录后由 ClientSetting.Save 写入）</summary>
    /// <remarks>Config&lt;T&gt; 使用 GetBasePath() 保存到 AppDomain.CurrentDomain.BaseDirectory/Config/ 目录</remarks>
    public String? ReadClientConfigFile()
    {
        var configFile = Path.Combine(AppContext.BaseDirectory, "Config", "ZeroClient.config");
        return File.Exists(configFile) ? File.ReadAllText(configFile) : null;
    }

    /// <summary>删除 ZeroClient.config，让下一次测试从空状态开始</summary>
    public void DeleteClientConfig()
    {
        var configFile = Path.Combine(AppContext.BaseDirectory, "Config", "ZeroClient.config");
        if (File.Exists(configFile)) File.Delete(configFile);
    }
    #endregion
}
