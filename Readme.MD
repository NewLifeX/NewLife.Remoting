# NewLife.Remoting - 协议通信库

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/NewLife.Remoting?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/NewLife.Remoting?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/NewLife.Remoting?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/NewLife.Remoting?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/NewLife.Remoting?label=dev%20nuget&logo=nuget)

![Nuget Downloads](https://img.shields.io/nuget/dt/NewLife.Remoting.Extensions?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/NewLife.Remoting.Extensions?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/NewLife.Remoting.Extensions?label=dev%20nuget&logo=nuget)

协议通信库，包含RPC架构和HTTP架构，提供客户端服务端通信的基础框架，内置SRMP协议实现，并支持实现各种自定义协议。  

源码： https://github.com/NewLifeX/NewLife.Remoting  
Nuget：NewLife.Remoting / NewLife.Remoting.Extensions  



参考目录：

- [Remoting接口文档](Doc/RemotingApi.MD)
- [SRMP简易远程消息协议](Doc/SRMP.MD)



## RPC架构
客户端：**ApiClient**

服务端：**ApiServer**

应用客户端：**ClientBase**

特点：

1. 服务端根据Action把请求转发给各个Controller，用法跟WebApi一致。
2. 简单高性能，通信报文使用二进制序列化，中间没有任何损耗
3. 接口出入参支持灵活的Json序列化（常规接口），同时也支持高效的二进制序列化（图片视频）
4. TCP/UDP长会话，支持服务端主动下发消息
5. 典型连接数，单机1万TCP长连接，最高400万
6. 典型吞吐数，单机10万TPS，最高2266万TPS
7. 服务端可寄宿于控制台、Web项目、桌面应用、IoT嵌入式应用
8. 支持集群部署横向扩展，每个客户端跟其中一个服务端维持长连接，所有请求落到该服务器

代表性应用（**蚂蚁调度AntJob**）：

1. 客户端AntClient继承自ClientBase，通过Tcp/Udp等协议连接服务端ApiServer，进行登录、心跳等操作。
2. 客户端使用应用AppId和AppSecret登录，获得令牌，后续无需携带或验证令牌，直到令牌过期重新登录。




## HTTP架构
客户端：**ApiHttpClient**

服务端：**ASP.NET WebApi**

应用客户端：**ClientBase**

应用服务端：**BaseDeviceController**

特点：

1. 标准ASP.NET WebApi作为服务端，重用现有技术栈，包括接口测试和集群部署管理。
2. 客户端ApiHttpClient是标准HttpClient的进一步封装，支持多服务端地址负载均衡。
3. 默认通信使用Json序列化，不适合传输文件、图片和视频等二进制数据。
4. 服务端提供BaseController基类，封装了令牌验证等鉴权机制
5. 服务端提供BaseDeviceController基类，封装常见的登录、心跳和更新等接口
6. 通过WebSocket长连接实现指令下发，心跳保活
7. 典型连接数，单机1000并发连接
8. 典型吞吐数，单机1万TPS
9. 服务端仅寄宿于 Kestrel和IIS
10. 支持集群部署横向扩展，客户端每次请求都可能分流到不同应用服务器

代表性应用（**轻量级IoT平台ZeroIoT**）：

1. 客户端HttpDevice继承自ClientBase，通过Http/Https协议连接服务端WebApi，进行登录、注销、心跳和更新等操作。
2. 客户端使用设备DeviceCode和DeviceSecret登录，获得令牌，后续每次请求头都需要带上令牌。
3. 在心跳时检测并维持WebSocket长连接。


## 快速拥有

​	使用NewLife组件的最简便方式是从Nuget引用，例如在项目Nuget管理中搜索`NewLife.Remoting` 并引入。

​	NewLife组件由社区共创20多年，使用MIT开源协议，**任何人可任意修改并再次发行**（无需声明来源）！许多企业基于此构建内部开发框架时，甚至可通过批量替换源码中所有`NewLife`字符串为贵公司名实现私有化定制。

​	团队始终秉承开放态度，不仅支持VisualStudio（最新正式版）打开解决方案编译，也兼容`dotnet build`命令行编译，项目文件摒弃复杂功能以追求简单易用，真正做到开箱即用。

​	我们公开强命名证书`newlife.snk`以支持独自编译替换程序集。



​	命令行中运行以下命令快速体验NewLife组件：

```
dotnet new install NewLife.Templates
dotnet new rpcserver --name RpcServer
cd RpcServer
dotnet run
```



## 新生命项目矩阵
各项目默认支持net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5，旧版（2024.0801）支持net4.0/net2.0  

|                               项目                               | 年份  | 说明                                                                                        |
| :--------------------------------------------------------------: | :---: | ------------------------------------------------------------------------------------------- |
|                             基础组件                             |       | 支撑其它中间件以及产品项目                                                                  |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 核心库，日志、配置、缓存、网络、序列化、APM性能追踪                                         |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | 大数据中间件，单表百亿级，MySql/SQLite/SqlServer/Oracle/PostgreSql/达梦，自动分表，读写分离 |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 网络库，单机千万级吞吐率（2266万tps），单机百万级连接（400万Tcp长连接）                     |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | 协议通信库，提供CS应用通信框架，支持Http/RPC通信框架，高吞吐，物联网设备低开销易接入        |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 魔方快速开发平台，集成了用户权限、SSO登录、OAuth服务端等，单表100亿级项目验证               |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | 服务管理组件，把应用安装成为操作系统守护进程，Windows服务、Linux的Systemd                   |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero零代脚手架，基于NewLife组件生态的项目模板NewLife.Templates，Web、WebApi、Service        |
|                              中间件                              |       | 对接知名中间件平台                                                                          |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis客户端，微秒级延迟，百万级吞吐，丰富的消息队列，百亿级数据量项目验证                   |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | RocketMQ纯托管客户端，支持Apache RocketMQ和阿里云消息队列，十亿级项目验                     |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | 物联网消息协议，MqttClient/MqttServer，客户端支持阿里云物联网                               |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT标准库，定义物联网领域的各种通信协议标准规范                                             |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII，基于IoT标准库实现，支持ZeroIoT平台和IoTEdge网关            |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | 西门子PLC协议，基于IoT标准库实现，支持IoT平台和IoTEdge                                      |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | 地图组件库，封装百度地图、高德地图、腾讯地图、天地图                                        |
|    [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio)    | 2023  | 音频编解码库，PCM/ADPCMA/G711A/G722U/WAV/AAC                                                |
|                             产品平台                             |       | 产品平台级，编译部署即用，个性化自定义                                                      |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 星尘，分布式服务平台，节点管理、APM监控中心、配置中心、注册中心、发布中心                   |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 蚂蚁调度，分布式大数据计算平台（实时/离线），蚂蚁搬家分片思想，万亿级数据量项目验证         |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | 企业ERP，产品管理、客户管理、销售管理、供应商管理                                           |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | 码神工具，众多开发者工具，网络、串口、加解密、正则表达式、Modbus、MQTT                      |
|           [EasyIO](https://github.com/NewLifeX/EasyIO)           | 2023  | 简易文件存储，支持分布式系统中文件集中存储。                                                |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 产品级反向代理，NAT代理、Http代理                                                           |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | Http压力测试工具                                                                            |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git源代码管理系统                                                                           |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 嵌入式操作系统，完全独立自主，支持ARM Cortex-M芯片架构                                      |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | 嵌入式工业计算机，物联网边缘网关，高性能.NET8主机，应用于工业、农业、交通、医疗             |
|                          FIoT物联网平台                          | 2020  | 物联网整体解决方案，建筑、环保、农业，软硬件及大数据分析一体化，单机十万级点位项目验证      |
|                        UWB高精度室内定位                         | 2020  | 厘米级（10~20cm）高精度室内定位，软硬件一体化，与其它系统联动，大型展厅项目验证             |



## 新生命开发团队
![XCode](https://newlifex.com/logo.png)  

新生命团队（NewLife）成立于2002年，是新时代物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。  
团队主导的80多个开源项目已被广泛应用于各行业，Nuget累计下载量高达400余万次。  
团队开发的大数据中间件NewLife.XCode、蚂蚁调度计算平台AntJob、星尘分布式平台Stardust、缓存队列组件NewLife.Redis以及物联网平台FIoT，均成功应用于电力、高校、互联网、电信、交通、物流、工控、医疗、文博等行业，为客户提供了大量先进、可靠、安全、高质量、易扩展的产品和系统集成服务。  

我们将不断通过服务的持续改进，成为客户长期信赖的合作伙伴，通过不断的创新和发展，成为国内优秀的IoT服务供应商。  

`新生命团队始于2002年，部分开源项目具有20年以上漫长历史，源码库保留有2010年以来所有修改记录`  
网站：https://newlifex.com  
开源：https://github.com/newlifex  
QQ群：1600800/1600838  
微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)  
