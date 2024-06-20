using IoTZero;
using IoTZero.Services;
using NewLife.Caching;
using NewLife.Cube;
using NewLife.Log;
using NewLife.Reflection;
using XCode;

// 日志输出到控制台，并拦截全局异常
XTrace.UseConsole();

#if DEBUG
XTrace.Log.Level = NewLife.Log.LogLevel.Debug;
#endif

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

InitConfig();

// 配置星尘。借助StarAgent，或者读取配置文件 config/star.config 中的服务器地址、应用标识、密钥
var star = services.AddStardust(null);

// 系统设置
var set = IoTSetting.Current;
services.AddSingleton(set);

// 逐个注册每一个用到的服务，必须做到清晰明了
services.AddSingleton<ThingService>();
services.AddSingleton<DataService>();
services.AddSingleton<QueueService>();

// 注册Remoting所必须的服务
services.AddIoT(set);
//services.AddRemoting(set);

services.AddHttpClient("hc", e => e.Timeout = TimeSpan.FromSeconds(5));

services.AddSingleton<ICache, MemoryCache>();

// 后台服务
services.AddHostedService<ShardTableService>();
services.AddHostedService<DeviceOnlineService>();

// 启用接口响应压缩
services.AddResponseCompression();

services.AddControllersWithViews();

// 引入魔方
services.AddCube();

var app = builder.Build();

// 使用Cube前添加自己的管道
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/CubeHome/Error");

if (Environment.GetEnvironmentVariable("__ASPNETCORE_BROWSER_TOOLS") is null)
    app.UseResponseCompression();

app.UseWebSockets(new WebSocketOptions()
{
    KeepAliveInterval = TimeSpan.FromSeconds(60),
});

// 使用魔方
app.UseCube(app.Environment);

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=CubeHome}/{action=Index}/{id?}");

app.RegisterService("AlarmServer", null, app.Environment.EnvironmentName);

// 反射查找并调用客户端测试，该代码仅用于测试，实际项目中不要这样做
var clientType = "IoTZero.Clients.ClientTest".GetTypeEx();
var test = clientType?.GetMethodEx("Main").As<Func<IServiceProvider, Task>>();
if (test != null) _ = Task.Run(() => test(app.Services));

app.Run();

void InitConfig()
{
    // 把数据目录指向上层，例如部署到 /root/iot/edge/，这些目录放在 /root/iot/
    var set = NewLife.Setting.Current;
    if (set.IsNew)
    {
        set.LogPath = "../Log";
        set.DataPath = "../Data";
        set.BackupPath = "../Backup";
        set.Save();
    }
    var set2 = CubeSetting.Current;
    if (set2.IsNew)
    {
        set2.AvatarPath = "../Avatars";
        set2.UploadPath = "../Uploads";
        set2.Save();
    }
    var set3 = XCodeSetting.Current;
    if (set3.IsNew)
    {
        set3.ShowSQL = false;
        set3.EntityCacheExpire = 60;
        set3.SingleCacheExpire = 60;
        set3.Save();
    }
}