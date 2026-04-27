extern alias IoTZero;

using IoTZero::IoT.Data;
using IoTZero::IoTEdge;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NewLife;
using NewLife.Log;
using Xunit;

namespace XUnitTest.Samples.IoTZero;

/// <summary>IoTZero 集成测试 WebApplicationFactory。使用真实 Kestrel（端口0），完全独立临时数据库</summary>
public sealed class IoTZeroWebFactory : WebApplicationFactory<IoTZero::Program>, IAsyncLifetime
{
    #region 属性
    /// <summary>服务端真实监听地址，如 http://127.0.0.1:12345</summary>
    public String BaseUrl { get; private set; } = null!;

    /// <summary>临时数据/配置目录（测试结束后删除）</summary>
    public String TempDir { get; private set; } = null!;

    /// <summary>原始工作目录（析构时恢复）</summary>
    private String _origCurrentDir = null!;

    /// <summary>共享测试状态：已登录的客户端（贯穿全部 9 个测试步骤）</summary>
    public HttpDevice TestClient { get; private set; } = null!;

    /// <summary>共享测试状态：客户端设置（DeviceCode/DeviceSecret 在 Step1 登录后回填）</summary>
    public ClientSetting TestSetting { get; private set; } = null!;

    /// <summary>共享测试状态：设备编码（Step1 登录后填充，后续步骤使用）</summary>
    public String TestCode { get; set; } = null!;

    /// <summary>共享测试状态：设备 ID（Step2 验证实体后填充，后续步骤使用）</summary>
    public Int32 TestDeviceId { get; set; }
    #endregion

    #region WebApplicationFactory 重写
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        XTrace.UseConsole();

        // 每次 WAF 初始化时创建独立的临时目录
        TempDir = Path.Combine(Path.GetTempPath(), "IoTZeroTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDir);
        Directory.CreateDirectory(Path.Combine(TempDir, "config"));
        Directory.CreateDirectory(Path.Combine(TempDir, "Data"));

        // 切换工作目录：ClientSetting 的 config/IoTClient.config 会写到 TempDir
        _origCurrentDir = Environment.CurrentDirectory;
        Environment.CurrentDirectory = TempDir;

        // Testing 环境：Program.cs 会跳过 RegisterService 和 ClientTest 调用
        builder.UseEnvironment("Testing");

        // 覆盖连接串为临时 SQLite 文件，每次测试数据完全隔离
        var dbIoT        = $"Data Source={Path.Combine(TempDir, "Data", "IoT.db")};Provider=Sqlite";
        var dbIoTData    = $"Data Source={Path.Combine(TempDir, "Data", "IoTData.db")};ShowSql=false;Provider=Sqlite";
        var dbMembership = $"Data Source={Path.Combine(TempDir, "Data", "Membership.db")};Provider=Sqlite";

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<String, String?>
            {
                ["ConnectionStrings:IoT"]        = dbIoT,
                ["ConnectionStrings:IoTData"]    = dbIoTData,
                ["ConnectionStrings:Membership"] = dbMembership,
                ["XCodeSetting:ShowSQL"]         = "false",
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder) => base.CreateHost(builder);
    #endregion

    #region IAsyncLifetime
    /// <summary>初始化：启用真实 Kestrel（端口0），触发 WAF 启动服务，读取实际监听地址，预置产品数据</summary>
    async Task IAsyncLifetime.InitializeAsync()
    {
        UseKestrel(0);  // 必须在服务器启动前调用：告知 WAF 使用真实 Kestrel 而非 TestServer
        _ = Services;   // 触发 WebApplicationFactory.EnsureHost() → ConfigureHostBuilder → CreateHost
        // WAF 在 CreateHost 后调用 TryExtractHostAddress，自动将实际端口写入 ClientOptions.BaseAddress
        BaseUrl = ClientOptions.BaseAddress?.ToString()?.TrimEnd('/') ?? "";
        XTrace.WriteLine("IoTZeroWebFactory 启动，地址：{0}", BaseUrl);
        // XCode 静态 DAL 在进程内持久化，同一进程多次运行测试会复用共享数据库中的历史数据。
        // 在服务启动后、测试开始前，清空所有业务表，确保每次测试数据完全隔离。
        // 注意：必须在 _ = Services 之后执行（DAL 连接已建立），并在 SeedProduct 之前执行（避免误清产品数据）。
        CleanTestData();
        // 预置测试所需的 Product（EdgeGateway），否则 OnRegister 会抛出“无效产品”
        SeedProduct();
        // 初始化共享测试状态：创建客户端（Step1 时以空 DeviceCode/DeviceSecret 触发自动注册）
        TestSetting = new ClientSetting { Server = BaseUrl };
        TestClient  = new HttpDevice(TestSetting) { Log = XTrace.Log };
        await Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        (TestClient as IDisposable)?.Dispose();
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
    /// <summary>读取 IoTClient.config 文件内容（登录后由 ClientSetting.Save 写入）</summary>
    /// <remarks>Config&lt;T&gt; 使用 GetBasePath() 保存到 AppDomain.CurrentDomain.BaseDirectory/Config/ 目录</remarks>
    public String? ReadClientConfigFile()
    {
        var configFile = Path.Combine(AppContext.BaseDirectory, "Config", "IoTClient.config");
        return File.Exists(configFile) ? File.ReadAllText(configFile) : null;
    }

    /// <summary>预置产品数据（EdgeGateway），供设备自动注册使用</summary>
    /// <remarks>OnRegister 验证：Product.FindByCode(ProductKey) != null &amp;&amp; product.Enable == true</remarks>
    private static void SeedProduct()
    {
        var product = Product.FindByCode("EdgeGateway");
        if (product == null)
        {
            product = new Product
            {
                Code   = "EdgeGateway",
                Name   = "边缘网关（集成测试）",
                Enable = true,
            };
            product.Insert();
        }
        else if (!product.Enable)
        {
            product.Enable = true;
            product.Update();
        }

        XTrace.WriteLine("IoTZero 产品已预置，Code=EdgeGateway, Id={0}", product.Id);
    }

    /// <summary>清空业务表数据。XCode 静态 DAL 在进程内持久化，同一进程多次运行测试须在每次测试前重置数据</summary>
    private static void CleanTestData()
    {
        // 直接执行 SQL 删除，绕过实体缓存，速度快；删除后清空实体缓存
        DeviceOnline.Meta.Session.Execute("DELETE FROM DeviceOnline");
        DeviceHistory.Meta.Session.Execute("DELETE FROM DeviceHistory");
        Device.Meta.Session.Execute("DELETE FROM Device");
        Product.Meta.Session.Execute("DELETE FROM Product");

        // 清空实体缓存，避免残留对象影响后续 FindAll 查询
        DeviceOnline.Meta.Cache.Clear("清空测试数据");
        DeviceHistory.Meta.Cache.Clear("清空测试数据");
        Device.Meta.Cache.Clear("清空测试数据");
        Product.Meta.Cache.Clear("清空测试数据");
        DeviceOnline.Meta.SingleCache.Clear("清空测试数据");
        Device.Meta.SingleCache.Clear("清空测试数据");

        XTrace.WriteLine("IoTZero 测试数据已清空");
    }
    #endregion
}
