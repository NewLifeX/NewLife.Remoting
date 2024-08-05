﻿# Remoting接口文档

​	Remoting通信支持RPC和HTTP两种架构，[参阅这里](https://github.com/NewLifeX/NewLife.Remoting)，使用完全相同的接口出入参格式，用法一致，不再分开表述。

​	参考：

​	[简易远程消息协议](https://newlifex.com/core/srmp)

​	[**ApiHttpClient客户端**](https://newlifex.com/core/api_http)



## 基础知识

​	针对常见基础功能，封装了客户端与服务端实现，例如登录、注销和心跳等。



### 主要部件

​	**ClientBase**是客户端基类，推荐应用客户端类继承该基类，仅需简单配置即可使用；

​	**BaseDeviceController**是服务端控制器基类，推荐应用服务端控制器类继承该基类，也可以直接拷贝该基类代码来使用；



### 功能设置

​	默认实现的功能比较多，都集中在ClientBase基类内部，需要通过Features来设置启用哪些功能。一般写在应用客户端构造函数内。例如ZeroIoT中的HttpDevice构造函数：

```c#
public HttpDevice(ClientSetting setting) : base(setting)
{
    // 设置动作，开启下行通知
    Features = Features.Login | Features.Logout | Features.Ping | Features.Notify | Features.Upgrade;
    SetActions("Device/");
    Actions[Features.CommandReply] = "Thing/ServiceReply";
}
```

​	不仅通过Features设置了需要开启的功能，还通过SetActions指定了服务端接口的路径前缀（如Device/Login）。其中CommandReply接口比较特殊，独立设置其路径。



​	又如星尘节点客户端StarClient，星尘代理StarAgent和码神工具CrazyCoder都通过它连接星尘服务端节点管理平台，提供登录、注销、心跳、更新和指令下发等功能：

```c#
public StarClient()
{
    Features = Features.Login | Features.Logout | Features.Ping | Features.Upgrade | Features.Notify | Features.CommandReply | Features.PostEvent;
    SetActions("Node/");

    Log = XTrace.Log;
}
```



​	再来看看星尘应用客户端AppClient，所有通过nuget引入Stardust的应用，都通过AppClient连接星尘服务端应用管理平台，提供登录、心跳、指令下发、监控数据上报、服务注册与消费等功能：

```c#
public AppClient()
{
    Features = Features.Login | Features.Ping | Features.Notify | Features.CommandReply;
    SetActions("App/");

    Log = XTrace.Log;
}
```



### 客户端属性

​	ClientBase客户端主要属性。

| 名称             | 类型                          | 说明                                                         |
| ---------------- | ----------------------------- | ------------------------------------------------------------ |
| Server           | String                        | 服务端地址。支持http/tcp/udp，支持客户端负载均衡，多地址逗号分隔 |
| Code             | String                        | 编码。设备编码DeviceCode，或应用标识AppId                    |
| Secret           | String                        | 密钥。设备密钥DeviceSecret，或应用密钥AppSecret              |
| PasswordProvider | IPasswordProvider             | 密码提供者。用于保护密码传输，默认提供者为空，密码将明文传输。 |
| OnLogined        | <LoginEventArgs>              | 登录完成后触发                                               |
| Received         | <CommandEventArgs>            | 收到下行命令时触发                                           |
| Status           | LoginStatus                   | 登录状态                                                     |
| Delay            | Int32                         | 请求到服务端并返回的延迟时间。单位ms                         |
| Span             | TimeSpan                      | 时间差。服务器时间减去客户端时间                             |
| MaxFails         | Int32                         | 最大失败数。心跳上报失败时进入失败队列，并稍候重试。重试超过该数时，新的数据将被抛弃，默认1440次，约24小时 |
| Commands         | IDictionary<String, Delegate> | 命令集合。注册到客户端的命令与委托                           |
| Features         | Features                      | 客户端功能特性。默认登录注销心跳，可添加更新等               |
| Actions          | IDictionary<Features, String> | 各功能的动作集合。记录每一种功能所对应的动作接口路径。       |
| JsonHost         | IJsonHost                     | Json主机。提供序列化能力                                     |
| Setting          | IClientSetting                | 客户端设置                                                   |



### 异步登录

​	客户端刚启动时，网络可能尚未就绪。此时直接调用Login就会抛出异常。ClientBase提供了Open方法，内部通过定时器不断检测网络，并在网络恢复时调用Login，直到成功为止。

​	有可能客户端网络没有问题，而服务端网络有问题，或者中途网络无法连通，或者甚至服务端还没有启动。Open内部的定时登录机制同样有效，直到登录成功为止。



## 接口详解

​	各个接口按需选用，都不是必须实现。如果没有特殊说明，各接口都是客户端向服务端发起请求，并等待服务端响应。

​	各个应用接口前缀可能不同，下文使用变量表示，例如`/{Prefix}/Login`在ZeroIoT中就是`/Device/Login`。该前缀由前文功能设置中的SetActions指定，并保存在Actions属性中。少数场景可能会修改接口名，例如`/Device/Register`，具体以Actions中为准。

​	各接口请求中，非必要字段可以选填，应尽量提供相关信息，以支持服务端各项功能。

​	各接口默认使用POST请求方式，参数经Json序列化放在Body中。如果使用GET请求，则参数拼接在Url中。

​	登录获取令牌后，所有接口请求均需要在请求头中携带令牌，HTTP请求头：`Authentication: Bearer {token}` 。



​	正常响应格式如下（code==0），下文响应表格仅列出data部分：

```json
{
	"code": 0,
    "data": { /* 各接口响应表格字段 */ }
}
```

​	异常响应格式如下（code!=0），data部分为异常字符串：

```json
{
	"code": 401,
	"data": "未登录"
}
```



### 登录Login

​	**地址**：`/{Prefix}/Login`

​	**功能**：向服务端提交编码和密钥，服务端验证通过后颁发令牌给客户端，后续所有接口调用带上令牌作为身份标识。

​	**说明**：登录请求一般还提供ClientId、版本信息、IP地址与MAC地址、唯一机器码、本地时间等信息，服务端接口能验证客户端身份，并颁发令牌。服务端找不到关联身份时，也可以根据相关信息注册一个身份，并通过登录响应返回编码和原始明文密钥。



​	**请求**（ILoginRequest）：

| 名字     | 类型   | 必要 | 说明                                    |
| -------- | ------ | :--: | --------------------------------------- |
| Code     | String |  √   | 编码                                    |
| Secret   | String |  √   | 密钥                                    |
| ClientId | String |  √   | 实例。应用可能多实例部署，ip@proccessid |
| Version  | String |      | 版本                                    |
| Compile  | Int64  |      | 编译时间。UTC毫秒                       |
| IP       | String |      | 本地IP地址                              |
| Macs     | String |      | MAC地址                                 |
| UUID     | String |      | 唯一标识                                |
| Time     | Int64  |      | 本地时间。UTC毫秒                       |



​	**响应**：

| 名字       | 类型   | 必要 | 说明                                              |
| ---------- | ------ | :--: | ------------------------------------------------- |
| Code       | String |      | 编码。平台下发新编码，仅用于自动注册              |
| Secret     | String |      | 密钥。平台下发新密钥，仅用于自动注册              |
| Token      | String |  √   | 令牌。后续所有接口调用均需要在请求头携带该令牌。  |
| Expire     | Int32  |      | 令牌过期时间。单位秒。默认7200秒                  |
| Time       | Int64  |      | 本地时间。客户端用于计算延迟，Unix毫秒（UTC）     |
| ServerTime | Int64  |      | 服务器时间。客户端用于计算时间差，Unix毫秒（UTC） |



### 注销Logout

​	**功能**：向服务端申请，注销当前令牌。



### 心跳Ping

​	**功能**：定时向服务端发送心跳，主要目的是链路保活，同时上报客户端的一些性能数据。



### 升级更新Upgrade

​	**功能**：定时请求服务端更新接口，获取满足当前客户端条件的升级更新信息，并根据信息执行自动更新流程。



### 下行通知Notify

​	**功能**：服务端通过下行链路（HTTP是WebSocket，RPC是TCP/UDP）向客户端发送命令，指示客户端即时执行特定流程。

​	**方向**：服务端->客户端



### 命令响应CommandReply

​	**功能**：客户端执行完成服务端下发的命令后，通过CommandReply接口向上汇报执行状态与结果。



### 上报事件PostEvent

​	功能：客户端批量上报带有时间戳的事件日志。

