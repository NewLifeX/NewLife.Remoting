namespace NewLife.Remoting;

/// <summary>可插拔的重试策略接口（可选）。默认不启用任何重试。</summary>
/// <remarks>
/// 仅当 <see cref="NewLife.Remoting.ApiClient.RetryPolicy"/> 不为 null 且 <see cref="NewLife.Remoting.ApiClient.MaxRetries"/> 大于 0 时生效。
/// 实现方可根据异常类型决定是否重试、重试等待时间，以及是否更换连接。
/// </remarks>
public interface IRetryPolicy
{
    /// <summary>是否应当重试</summary>
    /// <param name="exception">当前异常</param>
    /// <param name="attempt">当前重试序号（从1开始，不含首次尝试）</param>
    /// <param name="delay">建议等待时长</param>
    /// <param name="refreshClient">是否在重试前更换底层连接（Cluster.Return 再 Cluster.Get）</param>
    /// <returns>true 表示进行本次重试；false 表示不重试并抛出异常</returns>
    Boolean ShouldRetry(Exception exception, Int32 attempt, out TimeSpan delay, out Boolean refreshClient);
}
