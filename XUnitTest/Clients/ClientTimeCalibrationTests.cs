using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>登录失败信封时间校准测试。服务端在错误信封中附带 serverTime，客户端据此校准时间差后重试</summary>
public class ClientTimeCalibrationTests
{
    [Fact]
    [DisplayName("调用异常重新包装后保留响应信封")]
    public async Task InvokeAsync_Wrap_PreservesEnvelope()
    {
        using var client = new EnvelopeClient(new SimpleSetting(), BuildEnvelope(DateTime.Now)) { Features = (Features)0 };

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.InvokeAsync<Object>("Device/Error"));

        Assert.Equal(500, ex.Code);
        Assert.StartsWith("[Device/Error]", ex.Message);

        var dic = Assert.IsAssignableFrom<IDictionary<String, Object?>>(ex.Data["response"]);
        Assert.True(dic.ContainsKey("serverTime"));
    }

    [Fact]
    [DisplayName("登录失败按信封中的服务端时间校准时间差")]
    public async Task Login_Failure_FixesTimeFromEnvelope()
    {
        var serverTime = DateTime.Now.AddHours(1);
        using var client = new EnvelopeClient(new SimpleSetting(), BuildEnvelope(serverTime));

        // 已取消令牌：登录失败后不自动启动重试定时器，避免后台重复登录
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<ApiException>(() => client.Login(null, cts.Token));

        var delta = (client.GetNow() - serverTime).TotalSeconds;
        Assert.InRange(delta, -5, 5);
    }

    /// <summary>构造错误信封。字段形态与 HttpEncoder 的 CreateEnvelope 一致</summary>
    /// <param name="serverTime">服务端时间</param>
    /// <returns>错误信封</returns>
    private static IDictionary<String, Object?> BuildEnvelope(DateTime serverTime) => new Dictionary<String, Object?>
    {
        ["action"] = "Device/Login",
        ["code"] = 500,
        ["data"] = null,
        ["serverTime"] = serverTime.ToUniversalTime().ToLong(),
    };

    private sealed class SimpleSetting : IClientSetting
    {
        public String Server { get; set; } = "tcp://127.0.0.1:1";

        public String Code { get; set; } = "test";

        public String? Secret { get; set; }

        public void Save() { }
    }

    /// <summary>模拟底层抛出带响应信封的业务异常</summary>
    private sealed class EnvelopeClient(IClientSetting setting, IDictionary<String, Object?> envelope) : ClientBase(setting)
    {
        protected override Task<TResult> OnInvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TResult>(String action, Object? args, CancellationToken cancellationToken)
        {
            var ex = new ApiException(500, "Secret validation failed.")
            {
                Data =
                {
                    ["response"] = envelope,
                }
            };

            throw ex;
        }
    }
}
