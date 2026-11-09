using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>客户端默认语言应用与持久化测试</summary>
[Collection("GlobalState")]
public class ClientLanguageTests
{
    [Fact]
    [DisplayName("客户端构造时应用已保存的默认语言")]
    public void Constructor_AppliesSavedDefaultLanguage()
    {
        using var state = new LocalizationState();
        NewLife.Setting.Current.DefaultLanguage = "zh-CN";
        var setting = new LanguageSetting();

        using var client = new LanguageClient(setting);

        Assert.Equal("zh-CN", Localization.DefaultCulture?.Name);
        Assert.Equal("zh-CN", Localization.LogCulture?.Name);
    }

    [Fact]
    [DisplayName("登录响应应用并保存默认语言到核心配置")]
    public async Task Login_AppliesAndSavesDefaultLanguage()
    {
        using var state = new LocalizationState();
        var setting = new LanguageSetting();
        var response = new LoginResponse { Token = "t", DefaultLanguage = "zh-CN" };
        using var client = new LanguageClient(setting, response);

        await client.Login();

        Assert.Equal("zh-CN", NewLife.Setting.Current.DefaultLanguage);
        Assert.Equal("zh-CN", Localization.DefaultCulture?.Name);
        Assert.Equal("zh-CN", Localization.LogCulture?.Name);
    }

    [Fact]
    [DisplayName("心跳响应应用并保存默认语言到核心配置")]
    public async Task Ping_AppliesAndSavesDefaultLanguage()
    {
        using var state = new LocalizationState();
        var setting = new LanguageSetting();
        using var client = new LanguagePingClient(setting);

        var response = await client.Ping();

        Assert.NotNull(response);
        Assert.Equal("zh-CN", NewLife.Setting.Current.DefaultLanguage);
        Assert.Equal("zh-CN", Localization.DefaultCulture?.Name);
    }

    [Fact]
    [DisplayName("不支持的语言被忽略")]
    public async Task Login_UnsupportedLanguage_Ignored()
    {
        using var state = new LocalizationState();
        var setting = new LanguageSetting();
        var response = new LoginResponse { Token = "t", DefaultLanguage = "xx-XX" };
        using var client = new LanguageClient(setting, response);

        await client.Login();

        Assert.Null(NewLife.Setting.Current.DefaultLanguage);
        Assert.Null(Localization.DefaultCulture);
    }

    private sealed class LanguageSetting : IClientSetting
    {
        public String Server { get; set; } = "tcp://127.0.0.1:1";

        public String Code { get; set; } = "test";

        public String? Secret { get; set; }

        public void Save() { }
    }

    private sealed class LanguageClient : ClientBase
    {
        private readonly ILoginResponse? _response;

        public LanguageClient(IClientSetting setting, ILoginResponse? response = null) : base(setting)
        {
            Features = Features.Login;
            _response = response;
        }

        protected override Task<ILoginResponse?> LoginAsync(ILoginRequest request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }

    private sealed class LanguagePingClient : ClientBase
    {
        public LanguagePingClient(IClientSetting setting) : base(setting) => Features = Features.Ping;

        protected override Task<IPingResponse?> PingAsync(IPingRequest request, CancellationToken cancellationToken)
            => Task.FromResult<IPingResponse?>(new PingResponse { DefaultLanguage = "zh-CN" });
    }

    /// <summary>保存并恢复本地化全局状态</summary>
    private sealed class LocalizationState : IDisposable
    {
        private readonly CultureInfo? _culture = Localization.Culture;
        private readonly CultureInfo? _logCulture = Localization.LogCulture;
        private readonly CultureInfo? _defaultCulture = Localization.DefaultCulture;
        private readonly String? _forcedLanguage = Localization.ForcedLanguage;
        private readonly String? _settingLanguage = NewLife.Setting.Current.DefaultLanguage;

        public LocalizationState()
        {
            // 模拟 StarAgent 启动时按系统语言固定日志语言
            Localization.SetDefaultLanguage(null);
            Localization.SetLanguage(null);
            Localization.LogCulture = Localization.SystemCulture;
            NewLife.Setting.Current.DefaultLanguage = null;
        }

        public void Dispose()
        {
            Localization.SetDefaultLanguage(_defaultCulture?.Name);
            if (_forcedLanguage != null)
                Localization.SetLanguage(_forcedLanguage);
            else
            {
                Localization.SetLanguage(null);
                Localization.Culture = _culture;
            }
            Localization.LogCulture = _logCulture;
            NewLife.Setting.Current.DefaultLanguage = _settingLanguage;
        }
    }
}
