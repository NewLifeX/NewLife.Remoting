using System;
using System.IO;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting;
using XCode;
using XCode.Membership;
using Xunit;

namespace XUnitTest.Samples;

/// <summary>ZeroRpcServer 集成测试夹具。启动完整的 ApiServer（端口0自动分配），并初始化临时 SQLite 数据库</summary>
public sealed class ZeroRpcServerFixture : IAsyncLifetime
{
    #region 属性
    /// <summary>服务端监听端口（系统自动分配）</summary>
    public Int32 Port { get; private set; }

    /// <summary>测试用户 ID</summary>
    public Int32 UserId { get; private set; }

    private ApiServer _server = null!;
    private String _tempDir = null!;
    private String _origDataPath = null!;
    private String _origCurrentDir = null!;
    #endregion

    #region 初始化与清理
    public async Task InitializeAsync()
    {
        XTrace.UseConsole();

        // 独立临时目录，测试互不干扰
        _tempDir = Path.Combine(Path.GetTempPath(), "ZeroRpcTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // 重定向 XCode 数据目录到临时目录，避免污染工作目录
        _origDataPath = NewLife.Setting.Current.DataPath;
        NewLife.Setting.Current.DataPath = _tempDir;

        _origCurrentDir = Environment.CurrentDirectory;
        Environment.CurrentDirectory = _tempDir;

        // 初始化所有 XCode 实体表（自动建表）
        EntityFactory.InitAll();

        // 预置用户，让 UserController 能正常查询到
        var user = new User
        {
            Name = "TestUser",
            DisplayName = "测试用户",
            Enable = true,
        };
        user.Save();
        UserId = user.ID;

        // 启动 ApiServer（端口0自动分配，同时监听 TCP/UDP/WS/HTTP）
        _server = new ApiServer(0)
        {
            Name = "ZeroRpcTestServer",
            Encoder = new JsonEncoder(),
            Log = XTrace.Log,
#if DEBUG
            EncoderLog = XTrace.Log,
#endif
        };

        _server.Register<MyController>();
        _server.Register<UserTestController>();
        _server.Start();

        Port = _server.Port;

        XTrace.WriteLine("ZeroRpcServerFixture 启动，端口：{0}", Port);

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _server.TryDispose();

        // 恢复工作目录
        if (!_origCurrentDir.IsNullOrEmpty()) Environment.CurrentDirectory = _origCurrentDir;
        if (!_origDataPath.IsNullOrEmpty()) NewLife.Setting.Current.DataPath = _origDataPath;

        // 删除临时目录
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); }
            catch { /* 忽略清理异常 */ }
        }

        await Task.CompletedTask;
    }
    #endregion

    #region 内联控制器（镜像 Zero.RpcServer 中的控制器逻辑）
    internal class MyController
    {
        /// <summary>整数加法</summary>
        public Int32 Add(Int32 x, Int32 y) => x + y;

        /// <summary>RC4 加解密往返</summary>
        public IPacket RC4(IPacket pk)
        {
            var data = pk.ToArray();
            var pass = "NewLife".GetBytes();
            return (ArrayPacket)data.RC4(pass);
        }
    }

    [Api("User")]
    internal class UserTestController : IApi, IActionFilter
    {
        public IApiSession Session { get; set; } = null!;

        [Api(nameof(FindByID))]
        public async Task<Object?> FindByID(Int32 id)
        {
            var times = Session["Times"].ToInt();
            times++;
            Session["Times"] = times;

            if (times >= 2)
                throw new ApiException(ApiCode.TooManyRequests, $"调用次数过多！Times={times}");

            await Task.Delay(10);
            var user = User.FindByID(id);
            if (user == null) return null;
            return new { user.ID, user.Name, user.DisplayName, user.Enable };
        }

        public void OnActionExecuting(ControllerContext filterContext) { }
        public void OnActionExecuted(ControllerContext filterContext) { }
    }
    #endregion
}
