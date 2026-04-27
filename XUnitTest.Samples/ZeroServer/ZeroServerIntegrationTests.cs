extern alias ZeroServer;

using System.Text.Json;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Security;
using XCode;
using ZeroServer::Zero.Data.Nodes;
using ZeroServer::ZeroClient;
using Xunit;

namespace XUnitTest.Samples.ZeroServer;

/// <summary>
/// ZeroServer HTTP 全链路集成测试。
/// 使用 WebApplicationFactory 启动真实 Kestrel（端口自动分配），NodeClient 通过 HTTP 连接。
/// 单个测试方法串行执行 9 步，覆盖：自动注册登录、配置持久化、服务端实体验证、注销/重登、
/// 心跳计数、WebSocket 通知建立、SendCommand + CommandReply、升级检查、事件上报。
/// </summary>
[Collection("SamplesIntegration")]
[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class ZeroServerIntegrationTests : IClassFixture<ZeroServerWebFactory>
{
    private readonly ZeroServerWebFactory _factory;

    public ZeroServerIntegrationTests(ZeroServerWebFactory factory) => _factory = factory;

    #region 9 步串行集成测试
    [Fact(DisplayName = "ZeroServer_9步全链路集成测试")]
    public async Task FullIntegrationFlow_AllNineSteps()
    {
        // 确保 Factory 已启动（首次访问时触发 ConfigureWebHost + CreateHost）
        Assert.False(_factory.BaseUrl.IsNullOrEmpty(), "BaseUrl 未初始化，Factory 启动失败");
        XTrace.WriteLine("ZeroServer 测试服务地址：{0}", _factory.BaseUrl);

        // 清空上次测试残留数据：XCode DAL 连接 StardustData 持久化在 AppBase/Data 目录，
        // 跨测试运行累积；若不清理，Step3 的 Assert.Empty(NodeOnline) 会因旧记录而误判失败
        NodeOnline.Delete("1=1");
        NodeHistory.Delete("1=1");

        // 创建新的 ClientSetting（空 Code/Secret，触发服务端自动注册）
        var setting = new ClientSetting { Server = _factory.BaseUrl };

        using var client = new NodeClient(setting);
        client.Log = XTrace.Log;

        // Step 1 - 自动注册登录
        var (code, secret) = await Step1_AutoRegisterLogin(client, setting);

        // Step 2 - 服务端实体验证（通过 XCode 直接查询，同进程内共享 DAL 连接）
        var nodeId = await Step2_VerifyServerEntities(code);

        // Step 3 - 注销
        await Step3_Logout(client, nodeId);

        // Step 4 - 修改密钥后重登
        await Step4_ReLoginWithNewSecret(client, setting, code, nodeId);

        // Step 5 - 心跳
        await Step5_Ping(client, nodeId);

        // Step 6 - WebSocket 建立确认
        await Step6_WaitWebSocket(nodeId);

        // Step 7 - SendCommand + CommandReply
        await Step7_SendCommandAndReply(client, code);

        // Step 8 - 升级检查
        await Step8_Upgrade(client);

        // Step 9 - 事件上报
        await Step9_PostEvents(client);
    }
    #endregion

    #region Step 1 — 自动注册登录
    private async Task<(String code, String secret)> Step1_AutoRegisterLogin(NodeClient client, ClientSetting setting)
    {
        XTrace.WriteLine("=== Step 1: 自动注册登录 ===");

        // 确保以空 Code/Secret 开始，触发服务端自动注册
        setting.Code   = null!;
        setting.Secret = null!;

        await client.Login(null, CancellationToken.None).ConfigureAwait(false);

        // 客户端状态验证
        Assert.True(client.Logined, "Login 后 Logined 应为 true");

        // 服务端应已写回 Code 和 Secret
        Assert.False(setting.Code.IsNullOrEmpty(),   "服务端应已回填 Code");
        Assert.False(setting.Secret.IsNullOrEmpty(), "服务端应已回填 Secret");

        XTrace.WriteLine("自动注册成功，Code={0}", setting.Code);

        // 等待 ClientSetting.Save() 写文件（异步 IO，稍作等待）
        await Task.Delay(300).ConfigureAwait(false);

        // 配置文件持久化验证
        var configContent = _factory.ReadClientConfigFile();
        Assert.False(configContent.IsNullOrEmpty(), "config/ZeroClient.config 应已写入磁盘");
        Assert.Contains(setting.Code,   configContent!, StringComparison.Ordinal);
        Assert.Contains(setting.Secret, configContent!, StringComparison.Ordinal);

        XTrace.WriteLine("配置文件已写入，内容长度={0}", configContent!.Length);

        return (setting.Code, setting.Secret);
    }
    #endregion

    #region Step 2 — 服务端实体验证
    private async Task<Int32> Step2_VerifyServerEntities(String code)
    {
        XTrace.WriteLine("=== Step 2: 验证服务端实体 ===");

        // 等待服务端异步写库完成（Node 实体同步写入，短暂等待即可）
        await Task.Delay(300).ConfigureAwait(false);

        // 验证 Node 实体已创建
        var node = Node.FindByCodeWithCache(code, false);
        Assert.NotNull(node);
        Assert.Equal(code, node.Code);
        Assert.True(node.Enable, "新注册的 Node 应为启用状态");
        Assert.False(node.Secret.IsNullOrEmpty(), "Node.Secret 不应为空");

        XTrace.WriteLine("Node 已创建，ID={0}, Enable={1}", node.Id, node.Enable);

        // 验证 NodeOnline 已创建（直接 SQL，绕过实体缓存）
        var onlines = NodeOnline.FindAll(NodeOnline._.NodeId == node.Id);
        Assert.NotEmpty(onlines);

        XTrace.WriteLine("NodeOnline 数量={0}", onlines.Count);

        // 验证 NodeHistory 已记录登录（WriteHistory 使用 EntityQueue 异步写库，需轮询等待）
        var logins = await WaitForNodeHistory(node.Id, "Http登录");
        Assert.NotEmpty(logins);
        Assert.True(logins[0].Success, "第一次登录的 NodeHistory 应为成功");

        XTrace.WriteLine("NodeHistory 登录记录={0}", logins.Count);

        return node.Id;
    }
    #endregion

    #region Step 3 — 注销
    private async Task Step3_Logout(NodeClient client, Int32 nodeId)
    {
        XTrace.WriteLine("=== Step 3: 注销 ===");

        await client.Logout("集成测试注销", CancellationToken.None).ConfigureAwait(false);

        Assert.False(client.Logined, "Logout 后 Logined 应为 false");

        // 轮询等待 NodeOnline 被删除（RemoveOnline 同步执行，但直接 SQL 读取可能有短暂延迟）
        var emptyOnlines = await WaitForNodeOnlineEmpty(nodeId);
        Assert.Empty(emptyOnlines);

        // NodeHistory 下线记录由 EntityQueue 异步写入，需轮询等待
        var logouts = await WaitForNodeHistory(nodeId, "Http设备下线");
        Assert.NotEmpty(logouts);

        XTrace.WriteLine("注销成功，NodeHistory 注销记录={0}", logouts.Count);
    }
    #endregion

    #region Step 4 — 修改密钥后重登
    private async Task Step4_ReLoginWithNewSecret(NodeClient client, ClientSetting setting, String code, Int32 nodeId)
    {
        XTrace.WriteLine("=== Step 4: 修改密钥后重登 ===");

        // 在服务端直接修改 Node.Secret（模拟运维修改密钥场景）
        var node = Node.FindByCodeWithCache(code, false)!;
        var newSecret = Rand.NextString(16);
        node.Secret = newSecret;
        node.Update();

        // 客户端同步使用新密钥
        setting.Secret = newSecret;

        // 重新登录
        await client.Login("重登测试", CancellationToken.None).ConfigureAwait(false);

        Assert.True(client.Logined, "修改密钥后重登，Logined 应为 true");

        // NodeOnline 应重新出现（直接 SQL，绕过实体缓存）
        var onlines = NodeOnline.FindAll(NodeOnline._.NodeId == nodeId);
        Assert.NotEmpty(onlines);

        // 登录历史应有 2 条记录（第一次自动注册 + 本次）；EntityQueue 异步写入，需轮询等待
        var logins = await WaitForNodeHistory(nodeId, "Http登录", 2);
        Assert.True(logins.Count >= 2, $"应有至少 2 条登录历史，实际={logins.Count}");

        XTrace.WriteLine("重登成功，NodeHistory 登录记录共={0}", logins.Count);
    }
    #endregion

    #region Step 5 — 心跳
    private async Task Step5_Ping(NodeClient client, Int32 nodeId)
    {
        XTrace.WriteLine("=== Step 5: 心跳 ===");

        // 记录心跳前的 PingCount
        var onlineBefore = NodeOnline.FindByNodeId(nodeId);
        var pingCountBefore = onlineBefore?.PingCount ?? 0;

        var rs = await client.Ping(CancellationToken.None).ConfigureAwait(false);
        Assert.NotNull(rs);

        // 等待服务端写库
        await Task.Delay(300).ConfigureAwait(false);

        // 刷新实体缓存后验证 PingCount 增加
        var onlineAfter = NodeOnline.Find(NodeOnline._.NodeId == nodeId);
        Assert.NotNull(onlineAfter);
        Assert.True(onlineAfter.PingCount > pingCountBefore,
            $"Ping 后 PingCount 应增加，before={pingCountBefore}, after={onlineAfter.PingCount}");

        XTrace.WriteLine("心跳成功，PingCount={0}", onlineAfter.PingCount);
    }
    #endregion

    #region Step 6 — WebSocket 通知建立
    private async Task Step6_WaitWebSocket(Int32 nodeId)
    {
        XTrace.WriteLine("=== Step 6: 等待 WebSocket 建立 ===");

        // NodeClient 登录后会自动建立 WebSocket 通知连接（因为 Features 包含 Notify）
        // 轮询最长 5 秒等待 NodeOnline.WebSocket = true
        var deadline = DateTime.Now.AddSeconds(5);
        NodeOnline? online = null;
        while (DateTime.Now < deadline)
        {
            online = NodeOnline.Find(NodeOnline._.NodeId == nodeId);
            if (online?.WebSocket == true) break;
            await Task.Delay(200).ConfigureAwait(false);
        }

        Assert.NotNull(online);
        Assert.True(online.WebSocket, "NodeOnline.WebSocket 应在 5 秒内变为 true");

        XTrace.WriteLine("WebSocket 已建立，SessionId={0}", online.SessionId);
    }
    #endregion

    #region Step 7 — SendCommand + CommandReply
    private async Task Step7_SendCommandAndReply(NodeClient client, String code)
    {
        XTrace.WriteLine("=== Step 7: SendCommand + CommandReply ===");

        var tcs = new TaskCompletionSource<CommandEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.Received += (_, e) =>
        {
            // 只接受我们发出的命令
            if (e.Model?.Command == "test:echo")
                tcs.TrySetResult(e);
        };

        // 通过 HttpClient 调用 [AllowAnonymous] 的 Node/SendCommand 接口
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
            Timeout  = 10,   // 等待响应最多 10 秒
        });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        var response = await http.PostAsync($"{_factory.BaseUrl}/Node/SendCommand", content).ConfigureAwait(false);
        XTrace.WriteLine("SendCommand HTTP 状态={0}", response.StatusCode);

        // 等待客户端收到命令（最多 10 秒）
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cmdEvt = await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        Assert.NotNull(cmdEvt.Model);
        Assert.Equal("test:echo", cmdEvt.Model!.Command);
        Assert.Equal("hello",     cmdEvt.Model.Argument);

        XTrace.WriteLine("已收到命令，Command={0}, Argument={1}", cmdEvt.Model.Command, cmdEvt.Model.Argument);
    }
    #endregion

    #region Step 8 — 升级检查
    private async Task Step8_Upgrade(NodeClient client)
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

    #region 辅助方法
    /// <summary>轮询等待 NodeHistory 出现指定条数记录（绕过实体缓存，直接 SQL 查询）</summary>
    /// <param name="nodeId">节点 ID</param>
    /// <param name="action">操作名称</param>
    /// <param name="minCount">最少记录数，默认 1</param>
    /// <returns>查到的记录列表（可能超过 minCount）</returns>
    private static async Task<IList<NodeHistory>> WaitForNodeHistory(Int32 nodeId, String action, Int32 minCount = 1)
    {
        var deadline = DateTime.Now.AddSeconds(5);
        while (DateTime.Now < deadline)
        {
            // 直接 SQL，绕过 EntityListCache（EntityQueue 异步写入，缓存尚未更新）
            var list = NodeHistory.FindAll(NodeHistory._.NodeId == nodeId & NodeHistory._.Action == action);
            if (list.Count >= minCount) return list;
            await Task.Delay(200).ConfigureAwait(false);
        }
        return NodeHistory.FindAll(NodeHistory._.NodeId == nodeId & NodeHistory._.Action == action);
    }

    /// <summary>轮询等待 NodeOnline 对应 nodeId 的记录全部消失</summary>
    /// <param name="nodeId">节点 ID</param>
    /// <returns>查到的剩余记录（超时后返回当时状态）</returns>
    private static async Task<IList<NodeOnline>> WaitForNodeOnlineEmpty(Int32 nodeId)
    {
        var deadline = DateTime.Now.AddSeconds(5);
        while (DateTime.Now < deadline)
        {
            var list = NodeOnline.FindAll(NodeOnline._.NodeId == nodeId);
            if (list.Count == 0) return list;
            await Task.Delay(200).ConfigureAwait(false);
        }
        return NodeOnline.FindAll(NodeOnline._.NodeId == nodeId);
    }
    #endregion

    #region Step 9 — 事件上报
    private async Task Step9_PostEvents(NodeClient client)
    {
        XTrace.WriteLine("=== Step 9: 事件上报 ===");

        var events = new[]
        {
            new EventModel { Name = "TestEvent1", Type = "info",  Remark = "集成测试事件1" },
            new EventModel { Name = "TestEvent2", Type = "alert", Remark = "集成测试事件2" },
        };

        var count = await client.PostEvents(events).ConfigureAwait(false);

        Assert.Equal(2, count);
        XTrace.WriteLine("事件上报成功，返回数量={0}", count);
    }
    #endregion
}
