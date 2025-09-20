# ��������ܵĶԱȣ�gRPC / ASP.NET Core Minimal APIs��

���ĶԱ� `NewLife.Remoting` �볣�� .NET RPC/Web ��ܵĲ�����ȡ�ᣬ�����ڲ�ͬ��������ѡ�͡�

## ����
- �����
  - NewLife.Remoting��`Tcp`/`Http`/`WebSocket`���ɳ����ӣ�֧�����ӳ��뵥����ģʽ��
  - gRPC��HTTP/2�����裩��˫�����������/�ͻ�������Unary ���á�
  - Minimal APIs��HTTP/1.1/2/3��REST �Ѻã���������/��Ӧ��
- ��Լ�����л�
  - NewLife.Remoting����Ϣ��Լ��`IMessage`/`ApiMessage`����Ĭ�� JSON�����滻 `IEncoder`��`JsonEncoder`/`HttpEncoder`����
  - gRPC��ǿ��Լ��.proto����Ĭ�� Protobuf�������ܡ������ԣ���
  - Minimal APIs��HTTP/JSON��OpenAPI/Swagger ��̬���ơ�
- �����벢��
  - NewLife.Remoting��������/���ӳء�`Multiplex` �������á�`ISocketClient` �¼����ƣ�����˿������Ƶ�����Ϣ����
  - gRPC��HTTP/2 ��·���á���ʽ������Ȼ֧�֡�
  - Minimal APIs�������ӻ� HTTP/2 ���ã��ޡ��Ự����API ���
- ��֤������
  - NewLife.Remoting��`Token` ע�롢`IApiHandler`/`IApiManager` ��չ��`Received` �¼���`ServiceProvider` ע�롣
  - gRPC����������Ԫ���ݣ�Metadata����TLS/Token ��̬���졣
  - Minimal APIs���м����Middleware��+ �����֤/��Ȩ�����
- �ɹ۲���
  - NewLife.Remoting��`ILog`��`ITracer`��������/������־��`ICounter` ��ʱͳ�ơ�`client.Tracer` ����־������ƣ�Debug ����ϸ Trace����
  - gRPC���ձ���� OpenTelemetry/Activity����̬�걸��
  - Minimal APIs��ͬ ASP.NET Core �ܵ���̬��
- ������
  - NewLife.Remoting��`ApiCode`���� `UseHttpStatus` �л� HTTP ״̬�����쳣ӳ��ɶ��ƣ����ݿ��쳣������
  - gRPC��`StatusCode` + `RpcException`��
  - Minimal APIs��HTTP ״̬�� + Լ���� JSON �����塣

## ���ó���
- NewLife.Remoting
  - ����/�豸������Ҫ�����ӡ�˫����Ϣ��������������ͣ���
  - ����Ĳ������������� .proto�����������滻��
  - �Զ���Э��/����/ͳ������ǿ����������� `NewLife.*` ��̬��
- gRPC
  - �����ԡ�ǿ��Լ�������ܶ����ƣ�˫��/��ʽ�����ḻ����̬���졣
- Minimal APIs
  - ���� Web/REST �Ŀ��� API����ǰ��/�����������Ѻã����������ơ�

## ����ӳ��
- ��֤
  - NewLife.Remoting������ע�� `Token`������ `OnLoginAsync` ��ִ�е�¼����������˿��� `IApiHandler` ��ͳһУ�顣
  - gRPC��Metadata + Interceptor��
  - Minimal APIs��Auth �м�� + ��������
- ���������Ӹ���
  - NewLife.Remoting��`ICluster`��������/���ӳأ���`SetServer` ƽ���л�����ѡ `IRetryPolicy` ���ԡ�
  - gRPC��HTTP/2 ��·���ã�������/���ؾ���ͨ�������ػ� Sidecar �ṩ��
- �ɹ۲���
  - NewLife.Remoting��`SlowTrace` ��ֵ��־��`ITracer` ��·�Σ��� Debug ����򿪵ײ� Socket Trace����`ICounter` ����ͳ�ơ�

## ȡ����ע��
- �������Զ������ȣ�Ĭ�� JSON�����������뼯�ɣ�����������ܣ����Զ��� `IEncoder`�������ƣ���
- ����ģ�ͣ��������뵥�����Ͷ��豸/�����Ѻã����򿪷� Web ������£�������ѡ REST/gRPC��
- �ɹ۲��ԣ��������н��� Trace ����������ʱ������־������ Debug �������� Socket Trace��
- ���ԣ�`IRetryPolicy` �� `MaxRetries` Ĭ�Ϲرգ����Է� 401 �쳣��Ч��401 ���õ�¼���ط������������ԡ�

## ���ٶ��ձ�ժҪ��
- ��Լ����Ϣ��JSON/���滻�� | .proto/Protobuf | HTTP/JSON
- ���ӣ�������/��/���� | HTTP/2 ����/�� | HTTP/1.1/2/3
- ���ͣ�֧�֣����� | ֧�֣����� | �� WebSocket/SignalR
- ��֤��Token ע��/��¼���� | Metadata/Interceptor | Middleware/Auth
- �ɹ۲��ԣ�ILog/ITracer/Counter | OTel/Activity | ASP.NET Core ��̬

## ����
- ������Ҫ���������Զ���Э��/���롢������/�豸�Ѻá�ͳһ��־/׷��/ͳ�ƣ�`NewLife.Remoting` �Ǻ���ѡ��
- ������Ҫǿ��Լ�������ԡ������Ƹ����ܡ�������̬������ gRPC��
- ���� Web/�������������� REST��ѡ�� Minimal APIs��
