# 新生命远程通信库（NewLife.Remoting）变更日志

## v3.7.2026.0201 (2026-02-01)

### 新功能
- ApiServer 支持动态端口分配
- 新增 ApiServer/Client 全量单元测试

### Bug修复
- 修正动态注册下发证书时，错误把密钥赋值给 Code 的问题
- 修正通过 Http 请求 ApiServer 时，服务端按 json 返回，不要走 IAccessor 的问题

### 优化改进
- 优化 OAuth 异常处理，细化 HTTP 状态码返回
- 优化 HttpMessage 请求行与头部解析，增强兼容性
- 单元测试端口改为动态分配，提升 CI 稳定性
- 优化 test.yml 构建与测试流程

### 文档改进
- 增强接口文档与注释
- 完善设备服务注释文档
- 补充命令客户端单元测试

### 依赖更新
- 升级 NewLife.* 相关组件到最新版本

---

## v3.7.2026.0102 (2026-01-02)

### 新功能
- ApiServer 支持异步方法
- ClientBase 客户端支持事件总线
- 支持 EventHub 分发 WebSocket 事件
- 增加阿里云 OpenAPI 客户端
- ICacheProvider 支持创建默认事件总线

### 优化改进
- 优化 SessionManager/WsCommandSession，完善代码注释
- WsCommandSession 支持事件广播与 IEventDispatcher
- 重构 WebSocket 通信，统一消息处理与收发
- 引入 IEventDispatcher 架构来分发消息
- 引入 ITraceMessage，从接收消息中解码 TraceId
- 优先使用 IJsonHost 序列化
- 支持配置 SaltPasswordProvider 的算法和盐值时间
- 加大接收缓冲区
- 等待大循环等长耗时（>500ms）任务，采用 LongRunning 创建，避免占用线程池

### Bug修复
- 禁止异常 CommandModel 进入命令处理流程
- 释放 WebSocket，使用安全销毁，因为 WebSocket 关闭时经常会抛出异常
- 修正对管道处理器的使用
- 修正在线记录更新返回 0 时的处理逻辑

---

## v3.6.2025.1112 (2025-11-12)

### 新功能
- 支持 .NET 10

### Bug修复
- 修正使用缓存时，必须增加 Name 前缀，解决了星尘平台中 Node 和 App 相互冲突的问题

---

## v3.2.2024.1202 (2024-12-02)

### Bug修复
- 修正 RPC 传输超大报文（>64k）时解码失败的问题
- 修正 IPacket.ReadBytes 导致链表式数据读取错误
- 修正单元测试 BigMessage 大报文响应时出现混乱的问题

### 优化改进
- 响应结果可能是 IOwnerPacket，需要释放资源，把缓冲区还给池
- 优化登录状态，注销后进入 Ready，允许重新登录，修正 StarAgent 切换服务器时无法自动重新登录的问题
