using NewLife.Remoting.Clients;

namespace XUnitTest.Samples;

/// <summary>样例集成测试公共辅助</summary>
internal static class SamplesTestHelper
{
    /// <summary>在时间预算内反复登录，直到成功或预算耗尽</summary>
    /// <remarks>
    /// 服务端冷启动（Cube/XCode 初始化与反向建表）在 CI 慢机器上可能持续数十秒，而客户端单次登录的滚动
    /// 超时为 times*1000ms（首次仅 1s，上限 Timeout），固定次数重试会在服务端就绪前耗尽。改为时间预算内
    /// 持续重试，并把最后一次错误带回调用方——CI 日志不显示运行输出，断言消息是唯一的现场线索。
    /// </remarks>
    /// <param name="client">客户端</param>
    /// <param name="source">登录来源标识</param>
    /// <param name="seconds">时间预算（秒）</param>
    /// <returns>是否登录成功；失败时返回最后一次错误信息</returns>
    public static async Task<(Boolean Ok, String? Error)> LoginWithBudgetAsync(this ClientBase client, String? source, Int32 seconds)
    {
        String? error = null;
        var deadline = DateTime.Now.AddSeconds(seconds);
        while (!client.Logined && DateTime.Now < deadline)
        {
            try
            {
                await client.Login(source, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                await Task.Delay(500).ConfigureAwait(false);
            }
        }

        return (client.Logined, error);
    }
}
