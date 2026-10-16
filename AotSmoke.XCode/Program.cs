using System.Runtime.CompilerServices;
using System.Text;
using AotSmoke.XCode.Data;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using XCode;

namespace AotSmoke.XCode;

/// <summary>XCode 设备服务 NativeAOT 冒烟</summary>
/// <remarks>
/// <para>实测 <c>DefaultDeviceService&lt;TDevice,TOnline&gt;</c> 与 XCode 实体（SQLite）在 NativeAOT 下的运行期可用性：
/// 建表/增删改查、唯一索引查询、在线记录读写，以及设备服务的登录/心跳/注销链路。</para>
/// <para>JIT 与 NativeAOT 双跑，逐场景输出 PASS/FAIL，非 0 退出码即失败。XCode 尚未声明 AOT 兼容，
/// 本工程同时充当其 AOT 进展的探针，结论见《功能清单》EXT-6 与《架构设计》收口记录。</para>
/// </remarks>
internal static class Program
{
    #region 字段
    private static readonly List<String> _failedNames = [];
    private static Int32 _total;
    private static Int32 _failed;
    private static IDeviceService2 _service = null!;
    private static DeviceContext _context = null!;
    #endregion

    #region 入口
    private static async Task<Int32> Main()
    {
        Console.WriteLine($"=== XCode 设备服务 AOT 冒烟 === IsDynamicCodeSupported={RuntimeFeature.IsDynamicCodeSupported}");

        // 独立临时目录，避免污染工作目录与用户数据
        var dir = Path.Combine(Path.GetTempPath(), "AotSmokeXCode_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        NewLife.Setting.Current.DataPath = dir;
        Directory.SetCurrentDirectory(dir);

        Console.WriteLine($"数据目录：{dir}");

        // AOT 保留实体工厂构造：XCode 拦截器初始化经 Type/反射创建实体工厂，
        // 需以具体类型静态引用 GetFactory<T>()，否则 DefaultEntityFactory<T> 无参构造会被裁剪
        _ = EntityFactory.GetFactory<Device>();
        _ = EntityFactory.GetFactory<DeviceOnline>();

        try
        {
            await Run("01 实体建表与新增", InsertDevice);
            await Run("02 唯一索引查询与更新", QueryAndUpdate);
            await Run("03 在线记录写入与查询", OnlineRecord);
            await Run("04 设备服务登录（含在线记录）", LoginFlow);
            await Run("05 设备服务心跳", PingFlow);
            await Run("06 设备服务注销", LogoutFlow);

            Console.WriteLine(_failed == 0
                ? $"=== 全部通过 {_total}/{_total} ==="
                : $"=== 失败 {_failed}/{_total}：{String.Join(", ", _failedNames)} ===");
        }
        catch (Exception ex)
        {
            _failed++;
            _failedNames.Add("初始化");
            Console.WriteLine($"FAIL 初始化 :: {ex.GetType().Name}: {ex.Message}");
        }

        return _failed;
    }

    private static async Task Run(String name, Action action)
    {
        _total++;
        try
        {
            action();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            _failedNames.Add(name);
            Console.WriteLine($"FAIL {name} :: {ex.GetType().Name}: {ex.Message}");

            // 逐层打印内部异常，便于定位根因（XCode 的失败常常包在类型初始化的内层异常里）
            var sb = new StringBuilder();
            var cur = ex;
            for (var i = 0; cur != null && i < 5; i++)
            {
                sb.AppendLine($"    [{i}] {cur.GetType().FullName}: {cur.Message}");
                cur = cur.InnerException;
            }
            // 带堆栈的完整链路：AOT 裁剪类失败必须凭调用栈定位具体入口
            sb.AppendLine(ex.ToString());
            Console.Write(sb.ToString());
        }

        await Task.CompletedTask;
    }
    #endregion

    #region 场景
    private static void InsertDevice()
    {
        // 触发实体初始化与自动建表（Migration=On）
        var device = new Device
        {
            Name = "AOT设备",
            Code = "dev-aot",
            Secret = "abc",
            Enable = true,
            Period = 60,
        };
        device.Insert();

        if (device.Id <= 0) throw new Exception("自增主键无效");
    }

    private static void QueryAndUpdate()
    {
        var device = Device.FindByCode("dev-aot") ?? throw new Exception("按编码查询未命中");

        device.Name = "AOT设备-改";
        var rs = device.Update();
        if (rs <= 0) throw new Exception("更新失败");

        var count = Device.FindCount(Device._.Code == "dev-aot");
        if (count != 1) throw new Exception($"统计错误：{count}");

        var find = Device.Find(Device._.Id == device.Id) ?? throw new Exception("按主键查询未命中");
        if (find.Name != "AOT设备-改") throw new Exception($"字段未更新：{find.Name}");
    }

    private static void OnlineRecord()
    {
        var device = Device.FindByCode("dev-aot") ?? throw new Exception("设备不存在");

        var online = new DeviceOnline
        {
            DeviceId = device.Id,
            SessionId = "s-001",
            LoginTime = DateTime.Now,
            LastActive = DateTime.Now,
            LongLink = true,
        };
        online.Insert();

        var find = DeviceOnline.FindBySessionId("s-001") ?? throw new Exception("在线记录未落库");
        find.Pings++;
        if (find.Update() <= 0) throw new Exception("在线记录更新失败");
    }

    private static void LoginFlow()
    {
        _service = CreateService();

        _context = new DeviceContext();
        var request = new LoginRequest
        {
            Code = "dev-aot",
            Secret = "abc",
            ClientId = "client-aot",
        };

        var rs = _service.Login(_context, request, "Http");
        if (rs == null) throw new Exception("登录返回空");
        if (_context.Device == null) throw new Exception("未装载设备");
        if (_context.Online == null) throw new Exception("未创建在线记录");
        if (DeviceOnline.FindBySessionId("client-aot") == null) throw new Exception("在线记录未落库");
    }

    private static void PingFlow()
    {
        // 走设备服务的在线记录更新链路
        var rs = _service.Ping(_context, new PingRequest { IP = "127.0.0.1", Uptime = 120 }, null);
        if (rs == null) throw new Exception("心跳返回空");

        var online = DeviceOnline.FindBySessionId("client-aot") ?? throw new Exception("在线记录已丢失");
        if (online.Pings < 1) throw new Exception($"心跳次数未累加：{online.Pings}");
    }

    private static void LogoutFlow()
    {
        var online = _service.Logout(_context, "冒烟", "Http");

        if (online == null) throw new Exception("注销未返回在线记录");
        if (_service.GetDevice("dev-aot") == null) throw new Exception("设备缓存查询失败");
    }
    #endregion

    #region 辅助
    /// <summary>构造设备服务。默认实现直接使用 XCode 实体，覆盖真实数据访问链路</summary>
    private static IDeviceService2 CreateService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITracer, DefaultTracer>();
        services.AddSingleton<ICacheProvider, CacheProvider>();

        var provider = services.BuildServiceProvider();

        return new SmokeDeviceService(
            new SessionManager(provider),
            new SaltPasswordProvider { Algorithm = "md5", SaltTime = 60 },
            provider.GetRequiredService<ICacheProvider>(),
            provider);
    }

    /// <summary>冒烟设备服务。直接继承默认实现，不做任何重写</summary>
    private class SmokeDeviceService(ISessionManager sessionManager, IPasswordProvider passwordProvider, ICacheProvider cacheProvider, IServiceProvider serviceProvider)
        : DefaultDeviceService<Device, DeviceOnline>(sessionManager, passwordProvider, cacheProvider, serviceProvider);
    #endregion
}
