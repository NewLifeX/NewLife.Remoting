# ClientBase ʹ��˵��

## ���÷�Χ
- ����ʱ��.NET Framework 4.5+ / .NET Standard 2.0+ / .NET 5~9
- �����ռ䣺`NewLife.Remoting.Clients`
- ���ͣ�`ClientBase`��������ࣩ

## ����
- `ClientBase` ��Ӧ�ò�ͻ��˻��࣬��װ��¼������������֪ͨ������ִ�С��¼��ϱ�������������
- ͬʱ֧�ֻ��� `ApiClient`��TCP/UDP���� `ApiHttpClient`��HTTP/HTTPS + WebSocket����ͨ��ģ�͡�
- �����Զ��صǡ�ʱ��У׼��ʧ�����Զ��У����ı���� API ��ͬʱ�򻯵���ҵ����롣

## ͨ��ģ��
- RPC��`ApiClient` ������ֱ������ˣ���¼�����˿�ֱ���·����
- HTTP��`ApiHttpClient` ���� REST �ӿڣ�֧��ͨ�� WebSocket ��������֪ͨ��
- OAuth���� HTTP ģ���ϵ��� OAuth ��¼��������Ʋ���ÿ������Я����

## ���ٿ�ʼ
1. ����ͻ��������ࣨ��Сʵ�֣�
```csharp
using NewLife.Remoting;
using NewLife.Remoting.Clients;

// ���ݷ���˽ӿ�ǰ׺���Զ��嶯��·��ӳ��
public sealed class MyClient : ClientBase
{
    public MyClient() : base() { InitFeatures(); }
    public MyClient(IClientSetting setting) : base(setting) { InitFeatures(); }

    private void InitFeatures()
    {
        // Ĭ�������� Login/Logout/Ping���ɰ���׷��
        Features |= Features.Notify | Features.Upgrade | Features.CommandReply | Features.PostEvent;

        // �ɸ��Ƕ���ǰ׺�������˿�����/·�ɱ���һ��
        SetActions("Device/");
    }
}
```

2. ������ʹ��
```csharp
using NewLife.Security;
using NewLife.Log;
using NewLife.Remoting.Models;

var setting = new MySetting // ���ʵ�� IClientSetting ���־û�����
{
    Server = "https://api.example.com",
    Code = "dev-001",
    Secret = "secret"
};

var client = new MyClient(setting)
{
    Log = XTrace.Log,                          // ��ѡ��ע����־
    PasswordProvider = new SaltPasswordProvider{ Algorithm = "md5", SaltTime = 60 } // ��ѡ��������Կ����
};

client.OnLogined += (s,e) => XTrace.WriteLine("Logined: {0}", e.Response?.Token);
client.Received += (s,e) => XTrace.WriteLine("Command: {0}", e.Model?.Command);

// ����A���Զ����Ե�¼
client.Open();

// ����B����ʽ��¼
await client.Login("manual");

// ͳһԶ�̵��ã��Զ��ж� HTTP GET/POST���Զ��ص�һ�Σ�
var ping = await client.InvokeAsync<IPingResponse>("Device/Ping", new PingRequest());

// ע������ using/Dispose����Dispose �ڲ��᳢�� Logout
await client.Logout("bye");
```

## ��������/������
- �������Ȩ
  - `Server`������˵�ַ��֧�ֶ��ַ���ŷָ����ͻ��˸��ؾ��⣩��
  - `Code`/`Secret`���ͻ��˱�������Կ��֧���Զ�ע��ʱ������·���ֵ�����浽 `IClientSetting`��
  - `PasswordProvider`����ѡ�����ڶ� `Secret` ����ϣ�������䣨���� `SaltPasswordProvider`����
- ���������л�
  - `Timeout`�����ó�ʱʱ�䣨���룩��Ĭ�� 15000��
  - `JsonHost`�����л��ṩ�ߣ�Ĭ�� `JsonHelper.Default`��
  - `ServiceProvider`������ע���������Զ�ע��/������¼��������ģ�͡�
- ���ܿ���
  - `Features`��`Login`/`Logout`/`Ping`/`Upgrade`/`Notify`/`CommandReply`/`PostEvent` �ɰ������á�
  - `Actions`�������ܶ�Ӧ�Ķ���·���ֵ䣬Ĭ��ǰ׺ `Device/`������ `SetActions` �е�����
- ���
  - `Log`����־��`Tracer`����·׷�١�
  - `Delay`�����һ�������ӳ٣����룩��`GetNow()`���������У׼��ĵ�ǰʱ�䣨����ʱ������

## ��������
- `Open()`����ʱ���Ե�¼������������Զ���¼����������/������ʱ����
- `Login(source?)`����¼�ɹ����Զ����� Token������ʱ��ƫ�Ʋ����� `OnLogined`��
- `Logout(reason?)`�����÷����ע����ֹͣ��ʱ��������״̬��
- `Dispose()`���ڲ�����ע�����ͷ���Դ��

## Զ�̵���
- `InvokeAsync<TResult>(action, args)`��ͳһ������ڡ�
  - HTTP �Զ��ж� GET/POST������Ϊ��/�������͡��� `action` �� `Get` ��ͷ/���� `/get` ʱʹ�� GET��
  - ������ 401��Unauthorized���������Զ��ص�һ�κ����Ե�ǰ����
- `GetAsync<TResult>(action, args)`��HTTP ר�� GET����ֱ�ӵ��ã���
- `Invoke<TResult>(...)`��ͬ����װ��

## ������ʱ��У׼
- `Ping()`���ϱ�����״̬�����ܡ�ʧ������У���� `MaxFails`���ȴ��������ԣ�����˿�ͨ����Ӧ�����������ڡ�
- `FixTime()`���ڲ����������ӳ� `Delay` ��ʱ��ƫ�� `_span`��`GetNow()` ���ض����ı���ʱ��ʱ�䡣

## ����֪ͨ������
- ���� `Features.Notify` ��HTTP ģʽ�Զ�ά�� WebSocket �������Խ�������֪ͨ��
- �����
  - �¼���`Received`���յ�����ʱ��������
  - ȥ�أ���ͬ `Id` ������ִֻ��һ�Ρ�
  - ����/��ʱ���� `Expire`/`StartTime` �Զ��������ӳ�ִ�У��ӳ�ִ���ں�̨�Ŷӡ�
  - ִ����ɿ�ͨ�� `CommandReply` �ϱ��������� `Features.CommandReply`����

## �¼��ϱ�
- `WriteEvent(type, name, remark)`��д���¼����в��ɶ�ʱ�������ϱ� `PostEvents`��
- ʧ���¼����뱾��ʧ�ܶ��У�������ָ������ԡ�

## ��������
- ���� `Features.Upgrade` �󣬵��� `Upgrade(channel?)`����ѯ���¡����ء���ϣУ�顢��ѹ�븲�ǣ���ѡִ��Ԥ��װ�ű�/ִ������ǿ�Ƹ��º�� `Restart()`��
- `BuildUrl(relativeUrl)`���� HTTP ģʽ�£�����Ե�ַתΪ���ڵ�ǰ�����ַ�ľ��Ե�ַ��

## ��չ�㣨������д��
- `SetActions(prefix)`��ͳһ���嶯��·��ǰ׺��ӳ�䡣
- `CreateHttp(urls)`/`CreateRpc(urls)`���Զ���ײ�ͻ������ã��� Header�����Բ��ԡ�SocketLog �ȣ���
- `BuildLoginRequest()`/`FillLoginRequest(req)`������汾������ʱ�䡢������Ϣ���豸Ψһ��ʶ��ʱ����ȡ�
- `BuildPingRequest()`/`FillPingRequest(req)`���ḻ�����ϱ����ݡ�
- `UpgradeAsync(channel)`���Զ���������Ϣ��ȡ��
- `OnPing(state)`�������������Զ�����Ϊ���������Ѳ�죩��

## ��������
- ������� HTTP GET/POST��
  - Ĭ�Ϲ��򣺲���Ϊ��/�������ͣ��������� `Get` ��ͷ/���� `/get`�����Դ�Сд���� GET������ POST��
- Ϊʲô���Զ��صǣ�
  - �����÷��� 401��Unauthorized��ʱ����������� `Login` ���������л�������¼��ִ��һ�ε�¼��Ȼ������ԭ����
- ���ַ��θ��ؾ��⣿
  - `Server` ֧�ֶ��ŷָ������ַ������ʱ�����ַ���Զ��л���
- ��λ�ȡ������һ�µĵ�ǰʱ�䣿
  - ʹ�� `GetNow()`����������һ������/��¼��ʱ�����б���ʱ��У׼��

## ����ʾ�����������ִ��
```csharp
var client = new MyClient(new MySetting
{
    Server = "https://api.example.com",
    Code = "dev-001",
    Secret = "secret"
})
{
    Log = XTrace.Log
};

client.Received += async (s, e) =>
{
    if (e.Model?.Command == "Reboot")
    {
        // TODO: ִ������
        await client.CommandReply(new CommandReplyModel
        {
            Id = e.Model.Id,
            Status = CommandStatus.�ɹ�,
            Data = "��ִ������"
        });
    }
};

client.Open();
```

## ע������
- `ClientBase` Ϊ�������ͣ��붨���������ʹ�á�
- �߲����µ�״̬��չ�����б�֤�̰߳�ȫ��
- ��־��׷�٣�����������������Ҫ�� Info ������־��Debug ��������ڶ�λ���⡣

## �����¼
- �ĵ��״���ӣ����ܻ��� `ClientBase` �ĵ���ʹ�÷�ʽ�����ʵ����
