using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>ApiClient 请求头的并发契约</summary>
public class ApiClientHeadersTests
{
    [Fact]
    [DisplayName("Headers 并发写入与枚举不抛集合已修改")]
    public void Headers_ConcurrentWriteAndEnumerate()
    {
        using var client = new ApiClient();

        var errors = 0;
        Parallel.For(0, 500, i =>
        {
            try
            {
                client.Headers["TraceId" + i] = "trace-" + i;

                // 每次调用都会遍历 Headers 注入参数，这里复现“边写边枚举”
                foreach (var item in client.Headers)
                {
                    Assert.NotNull(item.Key);
                }
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
        });

        Assert.Equal(0, errors);
        Assert.Equal(500, client.Headers.Count);
    }
}
