# ApiClient ʹ��˵��

���Ľ��� `ApiClient` �Ĺ������÷����������ӡ����á���֤�����ԡ��ɹ۲������� ASP.NET Core Razor Pages �еļ��ɽ��顣

## 1. ����
- ���ã�����Ӧ�õ� RPC �ͻ��ˣ����ֵ�����˵ĳ����ӣ�֧������-��Ӧ�����˵������͡�
- ���䣺Tcp / Http / WebSocket���ɷ�������ַ��������
- ���أ�֧�ֵ�ַ�б������ӳأ������£������ӣ���ʱ�ӣ���

## 2. ���ٿ�ʼ
```csharp
var client = new ApiClient("tcp://127.0.0.1:12345");
client.Log = XTrace.Log; // ��ѡ
client.Open();

var hello = await client.InvokeAsync<string>("demo/hello", new { name = "world" });
Console.WriteLine(hello);

client.Close("done");
```

- ָ�������ַ������/�ֺŷָ��������򵥵ĸ������л���`new ApiClient("tcp://a:1234,tcp://b:1234")`��
- Http ������ֱ��ʹ�� `ApiHttpClient`��`new ApiHttpClient("http://host:port")`��

## 3. �����뼯Ⱥ
- `Servers`������˵�ַ���ϡ�
- `UsePool`���Ƿ�ʹ�����ӳأ�true Ϊ�����ӣ������£���false Ϊ�����ӣ���ʱ�ӣ���
- `Local`���� IP �����°󶨱��ص�ַ��
- `Cluster`���ڲ�ʹ�� `ClientSingleCluster` �� `ClientPoolCluster` ���� `ISocketClient`��

## 4. Զ�̵���
- `InvokeAsync<TResult>(action, args, ct)`���첽����-��Ӧ��
- `Invoke<TResult>(action, args)`��ͬ�������ȴ����ڲ�������ʱ���ƣ���
- `InvokeOneWay(action, args, flag=0)`�������ͣ����ȴ�Ӧ��
- ������������ͣ����� `Received` �¼��������Է���˵�֪ͨ��Ϣ��

## 5. ��֤�� Token
- `Token`�������ã�����ʱ���Զ�ע�뵽�������ϣ��� `Token`����
  - �ֵ������ԭ��ע�룻�������������д������ `Token`������ת�����ֵ��ע�롣
- ���ӽ�������������󣬿ͻ��˻ᴥ�� `OnNewSession`��Ĭ���첽���� `OnLoginAsync(client, force=true)`������дʵ�ֵ�¼��
- ��ʽ��¼��`await client.LoginAsync()` ��Լ�Ⱥ������ִ�е�¼��

## 6. ����������
- 401��Unauthorized��������Զ����� `OnLoginAsync(force=true)`������ͬһ�������ط�һ�Σ����������Դ�����
- ��ѡ���ԣ�
  - `IRetryPolicy? RetryPolicy` + `int MaxRetries`��Ĭ�� 0�����ڶԷ� 401 �쳣�������޴����ԡ�
  - ���Կɿ����Ƿ�ȴ����ȴ�ʱ�����Ƿ�������ǰ�л����ӡ�
- ��ʱ��`Timeout`�����룩����������ʱ�����Լ����Ϣ�׳� `TaskCanceledException`��

## 7. ����������л�
- `Encoder`��Ĭ�� `JsonEncoder`��
- `JsonHost`���Զ��� JSON ���л���Ϊ�����Сд�����ڸ�ʽ�����Կ�ֵ�ȣ���
- `EncoderLog`����������־���ɶ��� `XTrace.Log`����

## 8. �ɹ۲�����ͳ��
- ��־��`ILog Log`��`ILog SocketLog`��
- ׷�٣�`ITracer? Tracer`��`client.Tracer` �ĵײ� Socket Trace ����־������ơ���������־����Ϊ Debug ʱ�򿪣��Լ��ٳ��������ڼ��������뿪����
- �����ã�`SlowTrace`�����룩��������ֵ�����������־��
- ͳ�ƣ�`ICounter? StatInvoke`������ `StatPeriod`���룩���������������ͳ�ơ�

## 9. WebSocket / Http �����
- ����ַΪ WebSocket ʱ���ͻ��˹��߻�ע�� `WebSocketClientCodec` �����Ĭ�ϴ�����������������Ϣ����ͨ�š�
- Http �ͻ��˽���ʹ�� `ApiHttpClient`������ `Encoder` �� Filter���� `TokenHttpFilter`����������� Http ���塣

## 10. ��Դ�ͷ�
- `Close(reason)`���رռ�Ⱥ�����ӣ��ͷŶ�ʱ����
- `Dispose()`������ʱ�Զ��رգ�GC������ʽ�ͷţ�Dispose����

## 11. �� ASP.NET Core Razor Pages �еļ���
- ע��Ϊ������������ʱ Open��
```csharp
builder.Services.AddSingleton(sp =>
{
    var cli = new ApiClient("tcp://127.0.0.1:12345")
    {
        Log = XTrace.Log,
        Token = "your-token",
        UsePool = false,
    };
    cli.Open();
    return cli;
});
```
- �� `PageModel` ��ʹ�ã�
```csharp
public class IndexModel : PageModel
{
    private readonly ApiClient _client;
    public IndexModel(ApiClient client) => _client = client;

    public async Task OnGet()
    {
        var info = await _client.InvokeAsync<IDictionary<string, object>>("api/info");
        ViewData["ServerInfo"] = info;
    }
}
```
- ע�⣺
  - ���ֵ����볤���ӣ�����ÿ�������½����ӡ�
  - ���Ӧ����־��������Ƿ����ײ� Trace��

## 12. ��������
- Q�����ָ��ʹ�ñ���ĳ������ַ��A������ `Local`��
- Q�����ƽ���л�����˵�ַ��A������ `SetServer(newUris)`���ڲ������´λ�ȡ����ʱ��Ч��
- Q����ν��շ����������Ϣ��A������ `Received` �¼���
