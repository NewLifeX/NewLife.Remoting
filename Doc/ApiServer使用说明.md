# ApiServer ʹ��˵��

���Ľ��� `ApiServer` �Ĺ������÷������Ƿ���������������ע�ᡢ��������֤���ɹ۲��ԡ��㲥���� Razor Pages �ļ��ɽ��飨��Ϊ��̨�����߳�����

## 1. ����
- ���ã�Ӧ�ò� RPC ������������ `IApiServer`��`ApiNetServer` �� `ApiHttpServer`����������Ự��
- ���䣺Tcp / Udp / Http / WebSocket��ȡ���� `Use(NetUri)` �ĵ�ַ���ͣ���
- ��Լ������ `IMessage`/`ApiMessage` �� `IEncoder`��Ĭ�� JSON�����л� Http ���塣

## 2. ���ٿ�ʼ��Tcp��
```csharp
var server = new ApiServer(12345)
{
    Log = XTrace.Log
};
server.Register(new DemoController());
server.Start();

Console.ReadLine();
server.Stop("quit");
```

- Ҳ��ʹ�� `Use(new NetUri("tcp://*:12345"))` ָ��������ַ��
- Http ������`Use(new NetUri("http://*:8080"))` ��ʹ�� `ApiHttpServer`��

## 3. �������붯��ע��
- �Զ�ע�᣺���캯����ע������ `ApiController`���ṩ `api/all`��`api/info` ��ͨ�ýӿڡ�
- �ֶ�ע�᣺
```csharp
server.Register(new MyController());         // ע�������ȫ����������
server.Register(new MyController(), "Echo"); // ��ע����������
server.Register<MyController>();             // ע�����ͣ��ڲ��ᴴ��ʵ����
```
- ����ע�룺���� `ServiceProvider` �󣬴���������ʵ��ʱ�ɴ�����������

## 4. ����������
- `Process(session, msg, serviceProvider)`��
  - ���� `IMessage` Ϊ `ApiMessage`���� `action` ·�ɵ� `IApiHandler` ִ�С�
  - �����쳣��ӳ��Ϊ `ApiCode` �������Ϣ�����ݿ��쳣��������
  - `OneWay` ���󲻷�����Ӧ��
  - �� `UseHttpStatus=true`���� Http ����ʹ�� HTTP ״̬��������
  - finally �м�¼��������־������ `SlowTrace`����

## 5. ��֤��Ự
- token ģʽ��
  - �ͻ��˿��ڲ���Я�� `Token`��
  - ����˿��� `IApiHandler.Prepare` ���������У�� Token��������� Token ������״̬���� `IApiSession.Items`��
- `OnProcess`��Ĭ��ί�и� `Handler.Execute`������д��ʵ��ͳһ�ļ�Ȩ�����ء�

## 6. �ɹ۲�����ͳ��
- ��־��`ILog Log`����������־����`SessionLog`���Ự��־����
- ׷�٣�`ITracer? Tracer`��
- ������`SlowTrace`�����룩��������ֵ������� Action��Code����ʱ����������־��
- ͳ�ƣ�`ICounter? StatProcess` + `StatPeriod`���룩�����������ͳ����ײ�����״̬��

## 7. WebSocket �� Http
- WebSocket���Զ��л��� `WebSocketClientCodec` ������Ϣ֡����롣
- Http��`ApiHttpServer` ��� `HttpEncoder` ֧�� GET/POST ӳ�䵽 `ApiMessage`����ͨ�� `UseHttpStatus` �л�Ϊ HTTP ״̬��������

## 8. �˿����ַ����
- `ReuseAddress`���������̸��ö˿ڣ����ڹ������������̼�������ϵͳ֧�֣���
- `Multiplex`��ͬһ TCP ����������δ��ɵ������ڼ䲢�д����������������£���

## 9. �㲥
- `InvokeAll(action, args)`�������лỰ������֪ͨ�����سɹ��Ự����

## 10. ��Դ�ͷ�
- `Stop(reason)`��ֹͣ�����������ͳ�ƶ�ʱ����
- `Dispose()`���ͷ���Դ�������Ĭ�� `ApiController` ���໥���á�

## 11. �� ASP.NET Core Razor Pages �еļ��ɣ���Ϊ��̨����
- ��Ϊ�����ڵĺ�̨���񣨲�ֱ�ӱ�¶�ڹ�����ʾ����
```csharp
builder.Services.AddSingleton(sp =>
{
    var svr = new ApiServer(12345)
    {
        Log = XTrace.Log,
        UseHttpStatus = false
    };
    // ע��ҵ�������
    svr.Register(new MyController());
    // ����
    svr.Start();
    return svr;
});
```
- ע�⣺
  - ʹ�� `UseHttpStatus=false` ������Ĭ�� JSON ������ݣ������� Http �ͻ��˿���Ϊ true��
  - �����������������ַ����ͬ���� Razor Pages ���ã���ͨ���������¶��Ҫ�ӿڡ�

## 12. ��������
- Q����β鿴��ǰ����Щ��ע�ᶯ����A��`ShowService()` ��������ʱ��������б�
- Q������Զ���·�ɻ����أ�A���ṩ�Զ��� `IApiHandler` ������ `Handler`������д `OnProcess`��
- Q���Ƿ�֧�� Udp��A���ײ� `ApiNetServer` ֧�� NetType��һ���Ƽ� Tcp/Http/WebSocket��
