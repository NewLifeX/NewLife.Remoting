extern alias IoTZero;

using System.Text.Json;
using IoTZero::IoT.Data;
using IoTZero::IoTEdge;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Security;
using XCode;
using Xunit;

namespace XUnitTest.Samples.IoTZero;

/// <summary>
/// IoTZero HTTP 全链路集成测试。
/// 使用 WebApplicationFactory 启动真实 Kestrel（端口自动分配），HttpDevice 通过 HTTP 连接。
/// 单个测试方法串行执行 9 步，覆盖：自动注册登录、配置持久化、服务端实体验证、注销/重登、
/// 心跳计数、WebSocket 通知建立、SendCommand 投递、升级检查、事件上报。
/// </summary>
[Collection("SamplesIntegration")]
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class IoTZeroIntegrationTests : IClassFixture<IoTZeroWebFactory>
{
    private readonly IoTZeroWebFactory _factory;

    public IoTZeroIntegrationTests(IoTZeroWebFactory factory) => _factory = factory;

    #region 9 步串行集成测试
    [Fact(DisplayName = "IoTZero_9步全链路集成测试")]
    public async Task FullIntegrationFlow_AllNineSteps()
    {
        // 确保 Factory 已启动（首次访问时触发 ConfigureWebHost + CreateHost）
        Assert.False(_factory.BaseUrl.IsNullOrEmpty(), "BaseUrl 未初始化，Factory 启动失败");
        XTrace.WriteLine("IoTZero 测试服务地址：{0}", _factory.BaseUrl);

        // 清空上次测试残留数据：XCode DAL 连接 IoT 持久化在 AppBase/Data 目录，
        // 跨测试运行累积；若不清理，Step3 的 Assert.Empty(DeviceOnline) 会因旧记录而误判失败
        DeviceOnline.Delete("1=1");
        DeviceHistory.Delete("1=1");

        // 创建新的 ClientSetting（空 DeviceCode/DeviceSecret，触发服务端自动注册）
        var setting = new ClientSetting { Server = _factory.BaseUrl };

        using var client = new HttpDevice(setting);
        client.Log = XTrace.Log;

        // Step 1 - 自动注册登录
        var (code, secret) = await Step1_AutoRegisterLogin(client, setting);

        // Step 2 - 服务端实体验证（通过 XCode 直接查询，同进程内共享 DAL 连接）
        var deviceId = await Step2_VerifyServerEntities(code);

        // Step 3 - 注销
        await Step3_Logout(client, deviceId);

        // Step 4 - 修改密钥后重登
        await Step4_ReLoginWithNewSecret(client, setting, code, deviceId);

        // Step 5 - 心跳
        await Step5_Ping(client, deviceId);

        // Step 6 - WebSocket 建立确认
        await Step6_WaitWebSocket(deviceId);

        // Step 7 - SendCommand 投递（Timeout=0，服务端立即返回，客户端通过 WebSocket 收到命令）
        await Step7_SendCommandAndReceive(client, code);

        // Step 8 - 升级检查
        await Step8_Upgrade(client);

        // Step 9 - 事件上报
        await Step9_PostEvents(client);
    }
    #endregion

    #region Step 1 — 自动注册登录
    private async Task<(String code, String secret)> Step1_AutoRegisterLogin(HttpDevice client, ClientSetting setting)
    {
        XTrace.WriteLine("=== Step 1: 自动注册登录 ===");

        // 确保以空 DeviceCode/DeviceSecret 开始，触发服务端自动注册
        setting.DeviceCode   = null!;
        setting.DeviceSecret = null!;

        await client.Login(null, CancellationToken.None).ConfigureAwait(false);

        // 客户端状态验证
        Assert.True(client.Logined, "Login 后 Logined 应为 true");

        // 服务端应已写回 DeviceCode 和 DeviceSecret（通过 IClientSetting.Code/Secret 接口）
        Assert.False(setting.DeviceCode.IsNullOrEmpty(),   "服务端应已回填 DeviceCode");
        Assert.False(setting.DeviceSecret.IsNullOrEmpty(), "服务端应已回填 DeviceSecret");

        XTrace.WriteLine("自动注册成功，DeviceCode={0}", setting.DeviceCode);

        // 等待 ClientSetting.Save() 写文件（异步 IO，稍作等待）
        await Task.Delay(300).ConfigureAwait(false);

        // 配置文件持久化验证（ClientSetting 的 [Config("IoTClient")] 写到 config/IoTClient.config）
        var configContent = _factory.ReadClientConfigFile();
        Assert.False(configContent.IsNullOrEmpty(), "config/IoTClient.config 应已写入磁盘");
        Assert.Contains(setting.DeviceCode,   configContent!, StringComparison.Ordinal);
        Assert.Contains(setting.DeviceSecret, configContent!, StringComparison.Ordinal);

        XTrace.WriteLine("配置文件已写入，内容长度={0}", configContent!.Length);

        return (setting.DeviceCode, setting.DeviceSecret);
    }
    #endregion

    #region Step 2 — 服务端实体验证
    private async Task<Int32> Step2_VerifyServerEntities(String code)
    {
        XTrace.WriteLine("=== Step 2: 验证服务端实体 ===");

        // 等待服务端异步写库完成（Device/DeviceOnline 由 Save 同步写入，无需长等待）
        await Task.Delay(300).ConfigureAwait(false);

        // 验证 Device 实体已创建（FindByCode 单参数）
        var device = Device.FindByCode(code);
        Assert.NotNull(device);
        Assert.Equal(code, device.Code);
        Assert.True(device.Enable, "新注册的 Device 应为启用状态");
        Assert.False(device.Secret.IsNullOrEmpty(), "Device.Secret 不应为空");

        XTrace.WriteLine("Device 已创建，ID={0}, Enable={1}", device.Id, device.Enable);

        // 验证 DeviceOnline 已创建（DeviceOnline 无 FindAllByDeviceId，使用 FindAll 表达式）
        var onlines = DeviceOnline.FindAll(DeviceOnline._.DeviceId == device.Id, null, null, 0, 0);
        XTrace.WriteLine("DeviceOnline 数量={0}, DeviceId={1}", onlines.Count, device.Id);
        if (onlines.Count == 0)
        {
            // 诊断：打印所有 DeviceOnline 记录
            var allOnlines = DeviceOnline.FindAll(null, null, null, 0, 0);
            XTrace.WriteLine("所有 DeviceOnline({0}):", allOnlines.Count);
            foreach (var o in allOnlines)
                XTrace.WriteLine("  Id={0}, DeviceId={1}, SessionId={2}", o.Id, o.DeviceId, o.SessionId);
        }
        Assert.NotEmpty(onlines);

        XTrace.WriteLine("DeviceOnline 数量={0}", onlines.Count);

        // 验证 DeviceHistory 已记录登录（DefaultDeviceService.OnLogin 写 source+"登录"，source=Http）
        // WriteHistory 调用 SaveAsync，XCode EntityQueue ~1秒刷新，需轮询等待
        var logins = await WaitForDeviceHistory(device.Id, "Http登录").ConfigureAwait(false);
        Assert.NotEmpty(logins);
        Assert.True(logins[0].Success, "第一次登录的 DeviceHistory 应为成功");

        XTrace.WriteLine("DeviceHistory 登录记录={0}", logins.Count);

        return device.Id;
    }
    #endregion

    #region Step 3 — 注销
    private async Task Step3_Logout(HttpDevice client, Int32 deviceId)
    {
        XTrace.WriteLine("=== Step 3: 注销 ===");

        await client.Logout("集成测试注销", CancellationToken.None).ConfigureAwait(false);

        Assert.False(client.Logined, "Logout 后 Logined 应为 false");

        // 等待服务端写库
        await Task.Delay(500).ConfigureAwait(false);

        // DeviceOnline 应已清除（直接查 DB，绕过实体缓存）
        var onlines = DeviceOnline.FindAll(DeviceOnline._.DeviceId == deviceId, null, null, 0, 0);
        if (onlines.Count > 0)
        {
            foreach (var o in onlines)
                XTrace.WriteLine("残留 DeviceOnline: Id={0}, DeviceId={1}, SessionId={2}, UpdateTime={3}", o.Id, o.DeviceId, o.SessionId, o.UpdateTime);
        }
        Assert.True(onlines.Count == 0, $"DeviceOnline 应已清除，但有 {onlines.Count} 条记录，SessionIds=[{String.Join(",", onlines.Select(o => o.SessionId))}]");

        // DeviceHistory 应有下线记录（DefaultDeviceService.Logout 写 source+"设备下线"，source=Http）
        // WriteHistory 调用 SaveAsync，XCode EntityQueue ~1秒刷新，需轮询等待
        var logouts = await WaitForDeviceHistory(deviceId, "Http设备下线").ConfigureAwait(false);
        Assert.NotEmpty(logouts);

        XTrace.WriteLine("注销成功，DeviceHistory 下线记录={0}", logouts.Count);
    }
    #endregion

    #region Step 4 — 修改密钥后重登
    private async Task Step4_ReLoginWithNewSecret(HttpDevice client, ClientSetting setting, String code, Int32 deviceId)
    {
        XTrace.WriteLine("=== Step 4: 修改密钥后重登 ===");

        // 在服务端直接修改 Device.Secret（模拟运维修改密钥场景）
        var device = Device.FindByCode(code)!;
        var newSecret = Rand.NextString(16);
        device.Secret = newSecret;
        device.Update();

        // 客户端同步使用新密钥（通过 IClientSetting.Secret 接口设置 DeviceSecret）
        setting.DeviceSecret = newSecret;

        // 重新登录
        await client.Login("重登测试", CancellationToken.None).ConfigureAwait(false);

        Assert.True(client.Logined, "修改密钥后重登，Logined 应为 true");

        // 等待写库
        await Task.Delay(300).ConfigureAwait(false);

        // DeviceOnline 应重新出现
        var onlines = DeviceOnline.FindAll(DeviceOnline._.DeviceId == deviceId);
        Assert.NotEmpty(onlines);

        // 登录历史应有 2 条记录（第一次自动注册 + 本次）
        // WriteHistory 调用 SaveAsync，XCode EntityQueue ~1秒刷新，需轮询等待
        var logins = await WaitForDeviceHistory(deviceId, "Http登录", 2).ConfigureAwait(false);
        Assert.True(logins.Count >= 2, $"应有至少 2 条登录历史，实际={logins.Count}");

        XTrace.WriteLine("重登成功，DeviceHistory 登录记录共={0}", logins.Count);
    }
    #endregion

    #region Step 5 — 心跳
    private async Task Step5_Ping(HttpDevice client, Int32 deviceId)
    {
        XTrace.WriteLine("=== Step 5: 心跳 ===");

        // 记录心跳前的 Pings（DeviceOnline 的 AdditionalFields 有 Pings 累加字段）
        var onlineBefore = DeviceOnline.Find(DeviceOnline._.DeviceId == deviceId);
        var pingsBefore  = onlineBefore?.Pings ?? 0;

        var rs = await client.Ping(CancellationToken.None).ConfigureAwait(false);
        Assert.NotNull(rs);

        // 等待服务端写库
        await Task.Delay(300).ConfigureAwait(false);

        // 刷新实体缓存后验证 Pings 增加
        var onlineAfter = DeviceOnline.Find(DeviceOnline._.DeviceId == deviceId);
        Assert.NotNull(onlineAfter);
        Assert.True(onlineAfter.Pings > pingsBefore,
            $"Ping 后 Pings 应增加，before={pingsBefore}, after={onlineAfter.Pings}");

        XTrace.WriteLine("心跳成功，Pings={0}", onlineAfter.Pings);
    }
    #endregion

    #region Step 6 — WebSocket 建立确认
    private async Task Step6_WaitWebSocket(Int32 deviceId)
    {
        XTrace.WriteLine("=== Step 6: 等待 WebSocket 建立 ===");

        // HttpDevice 登录后会自动建立 WebSocket 通知连接（Features 包含 Notify）
        // 轮询最长 5 秒等待 DeviceOnline.WebSocket = true
        var deadline = DateTime.Now.AddSeconds(5);
        DeviceOnline? online = null;
        while (DateTime.Now < deadline)
        {
            online = DeviceOnline.Find(DeviceOnline._.DeviceId == deviceId);
            if (online?.WebSocket == true) break;
            await Task.Delay(200).ConfigureAwait(false);
        }

        Assert.NotNull(online);
        Assert.True(online.WebSocket, "DeviceOnline.WebSocket 应在 5 秒内变为 true");

        XTrace.WriteLine("WebSocket 已建立，SessionId={0}", online.SessionId);
    }
    #endregion

    #region Step 7 — SendCommand 投递
    private async Task Step7_SendCommandAndReceive(HttpDevice client, String code)
    {
        XTrace.WriteLine("=== Step 7: SendCommand 投递 ===");

        var tcs = new TaskCompletionSource<CommandEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.Received += (_, e) =>
        {
            // 只接受我们发出的命令；不设置 e.Reply，避免触发 Thing/ServiceReply（NotImplementedException）
            if (e.Model?.Command == "test:echo")
                tcs.TrySetResult(e);
        };

        // 通过 HttpClient 调用 [AllowAnonymous] 的 Device/SendCommand 接口
        // SendCommand 在 Service 层使用应用令牌验证，此处用已登录的设备令牌代替（测试环境 TokenService 不区分令牌来源）
        using var http = new HttpClient();
        var deviceToken = ((NewLife.Remoting.IApiClient)client).Token;
        if (!deviceToken.IsNullOrEmpty())
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {deviceToken}");
        var payload = JsonSerializer.Serialize(new
        {
            Code     = code,
            Command  = "test:echo",
            Argument = "hello",
            Timeout  = 0,
        });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        var response = await http.PostAsync($"{_factory.BaseUrl}/Device/SendCommand", content).ConfigureAwait(false);
        XTrace.WriteLine("SendCommand HTTP 状态={0}", response.StatusCode);

        // 等待客户端通过 WebSocket 收到命令（最多 10 秒）
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmdEvt = await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        Assert.NotNull(cmdEvt.Model);
        Assert.Equal("test:echo", cmdEvt.Model!.Command);
        Assert.Equal("hello",     cmdEvt.Model.Argument);

        XTrace.WriteLine("已收到命令，Command={0}, Argument={1}", cmdEvt.Model.Command, cmdEvt.Model.Argument);
    }
    #endregion

    #region Step 8 — 升级检查
    private async Task Step8_Upgrade(HttpDevice client)
    {
        XTrace.WriteLine("=== Step 8: 升级检查 ===");

        // 无新版本时应返回 null，不应抛出异常
        IUpgradeInfo? info = null;
        var ex = await Record.ExceptionAsync(async () =>
        {
            info = await client.Upgrade(null, CancellationToken.None).ConfigureAwait(false);
        });

        Assert.Null(ex);
        // info 为 null 表示无可用升级，合法
        XTrace.WriteLine("升级检查成功，info={0}", info == null ? "null（无升级）" : info.Version);
    }
    #endregion

    #region Step 9 — 事件上报
    private async Task Step9_PostEvents(HttpDevice client)
    {
        XTrace.WriteLine("=== Step 9: 事件上报 ===");

        var events = new[]
        {
            new EventModel { Name = "TestEvent1", Type = "info",  Remark = "IoTZero 集成测试事件1" },
            new EventModel { Name = "TestEvent2", Type = "alert", Remark = "IoTZero 集成测试事件2" },
        };

        var count = await client.PostEvents(events).ConfigureAwait(false);

        Assert.Equal(2, count);
        XTrace.WriteLine("事件上报成功，返回数量={0}", count);
    }
    #endregion

    #region 辅助方法
    /// <summary>轮询等待 SaveAsync 写库并按条件查询 DeviceHistory（直接查 DB，绕过实体缓存）。
    /// XCode EntityQueue 约每 1 秒刷新一次，最多等待 5 秒。</summary>
    /// <param name="deviceId">设备 ID</param>
    /// <param name="action">动作名称</param>
    /// <param name="minCount">最少记录数，达到后提前返回</param>
    private static async Task<IList<DeviceHistory>> WaitForDeviceHistory(Int32 deviceId, String action, Int32 minCount = 1)
    {
        IList<DeviceHistory> result = [];
        for (var i = 0; i < 25; i++)
        {
            await Task.Delay(200).ConfigureAwait(false);
            // 使用 FindAll + 显式 WHERE 直接查 DB，绕过实体缓存，避免 SaveAsync 未提交时缓存为空
            result = DeviceHistory.FindAll(DeviceHistory._.DeviceId == deviceId & DeviceHistory._.Action == action, null, null, 0, 0);
            if (result.Count >= minCount) break;
        }
        return result;
    }
    #endregion
}
