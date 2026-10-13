# NewLife.Remoting AOT 兼容性现状与边界

> 基线：NewLife.Core 12.0（v12 分支）｜评估方式：**NativeAOT 真实编译 + 运行实测**（win-x64，.NET 10）｜冒烟工程：`AotSmoke/`、`AotSmoke.Web/`、`AotSmoke.XCode/`
> 范围：核心库 `NewLife.Remoting` 全量；`NewLife.Remoting.Extensions` 的最小 API 宿主（§7）与 XCode 依赖边界（§8）。

---

## 1. 现状摘要

- **核心链路实测全通**：22 场景冒烟 JIT/AOT 双通（§2.1），覆盖 RPC 注册与调用、JSON 编解码、二进制直通、参数默认值、异步动作、流式（引用类型与值类型）、HTTP 入口、依赖注入、每请求实例化、通配路由、源生成命中与回退、内置 DTO 往返、客户端基类登录/心跳/事件/注销全链路、单方法注册、指令下发闭环（客户端命令分发与回执、服务端主动下发、会话管理器路由），以及简单类型直传与升级更新链路。
- **支持方式**：沿用 NewLife.Core 的「泛型/Type 入口保留注解 + 源生成器静态表 + 动态能力守卫」三件套，并为 Remoting 增补第五条生成器通道（动作分发表 + 流式泵 + 签名类型保留），见 §3。
- **告警面已收敛**：`IsAotCompatible` 已对 net8.0/net9.0/net10.0 常驻启用，库自身分析器告警由改造前 **19 条唯一告警** 收敛到 **0 条**；AOT 发布的 ILC 告警 **0 条**（§2.2）。
- **改造中发现并修复 6 处库侧 AOT 缺陷**：HTTP 响应封套与客户端注销/升级参数使用匿名类型被裁剪、动作签名模型类型成员被裁剪等。这些缺陷在 JIT 下不可见，只有真机 AOT 才会暴露（§2.3 前 6 项）。
- **另由新增业务闭环场景暴露 2 处解码缺陷**：①声明 `Object` 返回值时，服务端返回简单类型（`Boolean` 的 `True`、`DateTime` 的完整时间文本）会抛 `Non standard Json string`，`ClientBase.CommandReply` 最易触发；②`Guid`/`TimeSpan` 等以带引号字符串字面量写包的返回值无法解码（默认 JSON 宿主不支持顶层字符串字面量）。两者 JIT 下同样复现，已在解码侧统一加固（§2.3 第 7~8 项）。
- **Extensions（ASP.NET Core）已纳入 AOT 范围**（2026-10-10 决策：不拆新包，在本包内同时提供 MVC 与最小 API 双宿主，详见 §7）。编译期基线：`IsAotCompatible` 对 net8.0+ 常驻启用，自身告警由 **21 条收敛到 0 条**（DAM 注解 + MVC 边界抑制声明）。运行期：最小 API 宿主 **11 场景 JIT/AOT 双通**（2026-10-13），并暴露/修复 1 处内置模型元数据被裁剪的缺陷；XCode 依赖的 `DefaultDeviceService` 实测未通过，见 §8。

---

## 2. 实测数据

### 2.1 运行期：22 场景冒烟（JIT/AOT 双通）

| # | 场景 | # | 场景 |
|---|------|---|------|
| 1 | 泛型注册与服务发现 | 9 | HTTP 入口往返（含响应封套完整性） |
| 2 | TCP 同步调用往返 | 10 | 控制器依赖注入构造 |
| 3 | TCP 异步调用与复杂参数 | 11 | 控制器每请求实例化 |
| 4 | 二进制 Packet 参数与返回 | 12 | 通配路由与 Api 重命名 |
| 5 | 值类型参数默认值与缺参 | 13 | 源生成命中与回退双场景 |
| 6 | 异步动作 Task/Task<T> | 14 | 内置 DTO 运行时类型往返 |
| 7 | 流式 `IAsyncEnumerable<string>` | 15 | AOT 参数承载形态（字典 / 标记 DTO） |
| 8 | 流式 `IAsyncEnumerable<int>`（闭集泵） | 16 | 客户端基类登录/心跳/事件/注销往返 |
| 17 | 单方法注册的静态委托与裁剪保留 | 18 | 客户端命令分发与回执上报 |
| 19 | 服务端主动下发命令与设备回执 | 20 | 会话管理器命令下发与路由 |
| 21 | 简单类型参数与返回值直传 | 22 | 升级信息与更新包处理链路 |

> 场景 18/19/20 覆盖设备平台最典型的「指令下发」链路：客户端命令委托分发与回执（`CommandModel` 解码 → 命令匹配 → `CommandReplyModel` 上报）、服务端 `InvokeAll("Notify", …)` 主动下发（出站命令模型 JSON 不得退化为空对象）、`SessionManager.PublishAsync → code#json → 事件总线 → 会话路由`。
> 场景 21 双向验证「简单类型不做 JSON 包装」这一设计约定（`Boolean`/`DateTime`/`Guid`/`TimeSpan`/枚举作参数与返回值）；场景 22 覆盖升级链路（`IUpgradeInfo` 接口返回类型往返、更新包哈希校验/解压/覆盖在临时目录内沙箱执行）。
> AOT 判真：原生发布件入口 `Assembly.Location` 为空，而 JIT 基线为磁盘路径。冒烟工程固定「严格 AOT 姿态」（`PublishAot=true` 使 SDK 把 `RuntimeFeature.IsDynamicCodeSupported` 置为 false），两种运行的该值均为 False，**不作判别依据**。

### 2.2 编译期：分析器与 ILC 告警

改造前基线（`IsAotCompatible` 打开后的首轮构建）共 19 条唯一告警：

| 告警 | 条数 | 含义 | 处置 |
|------|:----:|------|------|
| IL2095 / IL2091 | 4 + 4 | 泛型保留注解与 NewLife.Core 的 `IApiClient` 契约不一致 | 按 Core 的 `PublicParameterlessConstructor \| PublicFields \| PublicProperties` 对齐 |
| IL2070 / IL2075 | 4 + 3 | 运行时类型成员枚举（控制器动作发现、流式接口检测、Task 结果属性） | 入口注解传导 + 静态表替代 + 显式声明 |
| IL2067 / IL2072 | 2 + 1 | 运行时类型成员要求未满足（JSON 转换目标类型、值类型默认值构造） | 参数注解 + 改写为无需反射的默认值语义 |
| IL3050 | 1 | 流式值类型元素的运行期泛型实例化 | 源生成器静态泵 + 闭集扎根 + `DynamicCode` 守卫 |

**当前均为 0 条**：库自身构建 0 条分析器告警（net8.0/net9.0/net10.0 实测）；AOT 发布 ILC 告警 0 条（冒烟工程实测）。
说明：ILC 只认 `UnconditionalSuppressMessage`，不认 `#pragma warning disable`——库内声明一律用特性。

### 2.3 改造中发现并修复的库侧缺陷

| # | 缺陷 | 现象（AOT 下） | 修复 |
|---|------|----------------|------|
| 1 | `HttpEncoder.Encode` / `CreateStreamResponse` 用匿名类型包装响应 `new { action, code, data }` | 响应体退化为 `{}`，HTTP 调用全部拿到默认值（TCP 不受影响） | 改用字典封套 `CreateEnvelope`，键名与报文一致 |
| 2 | `ClientBase` 注销/升级参数用匿名类型 `new { reason }` / `new { channel }` | 服务端收到空参数，注销原因与更新通道丢失 | 改用字典承载 |
| 3 | 动作入参/返回类型的成员与无参构造被裁剪 | FastJson 反序列化报 `No parameterless constructor defined for type 'X'`，或参数绑定为空 | 源生成器为动作签名类型发射 `ApiActionTable.Preserve(typeof(T))` |
| 4 | `ApiAction.CompileInvoker` 的 `Expression.Compile()` | NativeAOT 下抛异常（原靠 catch 静默回退） | 前置 `DynamicCode.IsSupported` 判断，直接跳过 |
| 5 | `ApiServer.ProcessStreamAsync` 的 `MakeGenericMethod` | 值类型流式返回直接崩溃 | 生成器静态泵优先，闭集元素类型兜底，最后才回退并在 AOT 下抛明确异常 |
| 6 | `ApiHandler` 的 `Activator.CreateInstance` 造值类型默认值、`Task` 结果属性反射、控制器动作发现反射 | 告警面与裁剪风险 | 表达式树以条件表达式替换 `default(T)`、`Task` 结果改 `DynamicDependency`、动作发现改泛型入口传导保留 |
| 7 | `JsonEncoder`/`HttpEncoder` 的 `DecodeResult` 用 JSON 解析器解析简单类型裸值 | **JIT 下同样复现**：声明 `Object` 返回值时，服务端返回 `Boolean`（写包为 `True`）或 `DateTime`（写包为完整时间文本）会抛 `Non standard Json string`。触发路径如 `ClientBase.CommandReply` 的 `InvokeAsync<Object>`，即「命令回执动作返回布尔值」这一最自然写法 | 解码前判断正文是否可解析，简单类型按目标类型直接转换（声明 `Object` 时返回原样文本）；见 §4「简单类型」约定 |
| 8 | `DecodeResult` 把带引号的字符串字面量交给 JSON 解析器 | **JIT 下同样复现**：`Guid`/`TimeSpan` 等自定义类型由 `JsonHost` 写为 `"…"`，默认 JSON 宿主（FastJson）不支持顶层标量，解析抛 `Non standard Json string`，即「动作返回 `Guid`」在声明同类型时也无法解码 | 字符串字面量取其中内容（含 `\\`/`\"` 反转义），再按目标类型转换 |

---

## 3. 关键机制与生成器体系

### 3.1 关键机制备忘

1. **`DynamicallyAccessedMembers` 只对 `Type`/`String` 参数与泛型参数生效**；放在 `Object`/`Task` 等参数上会被 ILC 忽略。
2. **保留要求必须经「泛型/Type 入口」传导**：`ApiServer.Register<TService>()` 的 `TService` 携带
   `PublicMethods | NonPublicMethods | PublicConstructors`，调用点 `Register<MyController>()` 天然保留其动作方法与构造。
   实例注册入口 `Register(Object, String?)` 是宽面入口，已用 `RequiresUnreferencedCode` 显式声明裁剪语义并给出替代指引。
3. **运行时类型入口的成员保留由生成器承接**：动作签名的参数/返回类型经 `ApiActionTable.Preserve(typeof(T))` 保留公开无参构造与公开成员。
4. **动态能力不可改造**：以 `DynamicCode.IsSupported`（`RuntimeFeature.IsDynamicCodeSupported`）守卫，AOT 下抛明确异常而非静默错。
5. **运行时类型 JSON 序列化依赖成员保留**：匿名类型与未保留成员的类型在 AOT 下序列化为 `{}`（§4、§5）。

### 3.2 生成器体系（静态化通道）

| 生成器 | 承接通道 | 生成物 |
|---|---|---|
| `ApiActionTableGenerator` | `ApiServer.Register<TService>()` / `IApiManager.Register<TService>()`（控制器全部动作）与 `Register(controller, "Method")`（单方法）调用点 | `ApiActionTable` 的「控制器类型+方法名 → 强类型调用委托」登记、值类型流式泵扎根、签名类型保留 |

- **非 AOT 条件编译**：生成物在 JIT 下同样生效（免反射、免表达式编译与暖机）；生成器缺席或未覆盖场景自动回退原反射路径，语义不变。
- **启动顺序**：生成物以 `[ModuleInitializer]` 注册，先于任何静态构造；分发形态为随主包 `analyzers/dotnet/cs` 分发，下游零配置。
- **主动跳过**：重载同名、`ref`/`out`/`in`、`params`、指针/`ref struct`、生成代码不可访问的私有/受保护成员、跨程序集非公开成员、传 `Type` 变量而非具体类型——一律跳过并回退反射。
- **宽面入口的补救**：单方法注册走 `Register(controller, "Method")`（宽面 `Object` 入口，本身无法携带保留要求），生成器为调用点可见的 `(控制器类型, 方法名)` 生成强类型委托，静态引用目标方法保住其代码，同时由 `ApiActionTable.RegisterInvoker` 的类型保留注解保住其**可反射元数据**，使宽面入口的按名查找在 AOT 下同样成立。

### 3.3 运行期静态表

`ApiActionTable`（`ApiActionTable.cs`）承载三类登记：

| 成员 | 用途 |
|------|------|
| `RegisterInvoker` / `TryGetInvoker` | 控制器类型+方法名 → 强类型调用委托，命中时零反射调用；类型参数携带方法/构造保留注解，保证宽面入口的按名查找在 AOT 下可用 |
| `RegisterStreamPump` / `TryGetStreamPump` | 值类型元素 → 流式泵；闭集元素类型（Boolean…Guid 共 17 种）在静态构造中扎根 |
| `Preserve` | 声明动作签名类型的成员保留（公开无参构造、公开字段、公开属性） |

---

## 4. 下游使用指引

- **启用方式**：`<PublishAot>true</PublishAot>` 写在**应用工程**属性组内；**不要**用命令行全局属性传入（`-p:PublishAot=true` 会波及多 TFM 依赖项目的 net45 等目标，报 `NETSDK1207`）。
- **控制器注册**：优先使用泛型入口 `server.Register<MyController>()`。该入口携带保留注解，AOT 下动作方法不会被裁剪，且源生成器会为其生成强类型调用委托。
  单方法注册 `server.Register(controllerInstance, "Method")` 走宽面入口，本身标注 `RequiresUnreferencedCode`；若该调用点对源生成器可见（编译期能确定控制器类型与方法名常量），生成器会为其生成委托并保留可反射元数据，AOT 下同样可用；传 `Type` 变量等运行期才确定的写法仍不支持。
- **调用参数（关键）**：`InvokeAsync<T>(action, args)` 的 `args` 是运行时 `Object`，AOT 下匿名类型与未保留类型的成员会被裁剪，服务端将收到 `{}`。
  请使用以下三种形态之一：
  1. **字典**：`new Dictionary<String, Object?> { ["a"] = 3, ["b"] = 4 }`（最稳妥，走反射无关路径）；
  2. **带标记的 DTO**：类型打 `[System.Text.Json.Serialization.JsonSerializable(typeof(T))]`，由 NewLife.Core 源生成器落成员访问器表；
  3. **二进制**：`IPacket` / `IAccessor` 直通。
  > 该约束同时适用于请求参数与响应模型；作为动作签名出现的类型额外由 `Preserve` 保留。
- **简单类型（设计约定）**：**动作返回简单类型时直接传文本，不做 JSON 包装**，只有复杂对象才走 JSON 包装。实际报文形态：`Boolean`→`True`/`False`、`DateTime`→完整时间文本、枚举→名称、字符串→裸文本、`Guid`/`TimeSpan` 等→带引号字符串字面量、数值→数字文本。
  客户端解码时按**声明的返回类型**转换：声明同类型即可直接用（如 `InvokeAsync<Guid>`）；**声明 `Object` 时拿到原样文本**，由调用方按需 `ToInt()`/`ToDateTime()`/`ToBoolean()` 转换。
  > 该约定意味着跨语言客户端（JS/Python/Go）需按文本解析而非严格 JSON 解析标量；复杂对象仍为规范 JSON。
- **JSON 宿主**：默认 `JsonHelper.Default`。如需 `System.Text.Json`，可注入 `NewLife.Serialization.SystemJson`（构造时传入应用的 `JsonSerializerContext`），或通过 `ServiceProvider` 注册 `IJsonHost`。
- **遇到 "不支持 AOT/裁剪" 警告**：1) 确认该 API 是否在主链路；2) 需要就用泛型入口/保留注解处理；3) 动态能力 API（表达式编译、运行期泛型实例化）在 AOT 下必须关闭或规避（`DynamicCode.IsSupported`）。
- **排障提示**：`IsDynamicCodeSupported=False` 表示动态代码不可用（严格 AOT 姿态下 JIT 运行同样为 False）；`Assembly.Location` 为空是 AOT 正常表现，不要用它判断目录。

---

## 5. 已知边界与收口记录（只增不删）

| 项 | 位置 | 现状与理由 |
|---|------|-----------|
| 匿名类型作为调用参数 | 应用侧调用点 | 匿名类型由编译器生成且不可标注，裁剪后成员丢失，序列化为 `{}`。**AOT 下请改用字典或带 `[JsonSerializable]` 标记的 DTO**；JIT 下不受影响。 |
| 私有/受保护动作方法（`[Api]` 控制器的非公开成员） | 应用侧控制器 | 生成代码位于独立类型，无法访问私有成员，故不走静态表；由 `Register<TService>()` 的保留注解保证元数据不被裁剪，运行期回退反射调用。 |
| 运行期才确定注册目标（`Register(Type` 变量`, "Method")`、`Register(instance, methodVariable)`） | 应用侧调用点 | 生成器无法静态确定 `(控制器类型, 方法名)`，不走静态表；宽面入口已用 `RequiresUnreferencedCode` 声明裁剪语义。请改用泛型入口或常量方法名。 |
| 跨程序集非公开动作、`ref`/`out`/`params`、指针与 `ref struct` 签名 | 源生成器 | 无法用委托安全表达，生成器主动跳过并回退反射。 |
| `AssemblyX` 程序集信息探测 | `ApiController.Info`、`ClientBase` | AOT 下按空值降级，不参与业务主链。 |
| `NewLife.Remoting.Extensions`（ASP.NET Core MVC + NewLife.XCode） | 独立包 | **已纳入 AOT 范围（2026-10-10）**：不拆新包，本包内同时提供 MVC 与最小 API 双宿主。MVC 侧为设计边界（官方支持矩阵：MVC ❌），已用抑制声明标注并以 `InterfaceModelBinder` 为边界；最小 API 宿主 2026-10-13 已落地并通过 11 场景 AOT 冒烟；XCode 依赖（`DefaultDeviceService`）实测未通过，探针与证据见 §8。详见 §7/§8。 |
| `Samples/*`（IoTZero / ZeroServer / Zero.RpcServer / Demo） | 样例工程 | 依赖 XCode / Redis / Stardust，不纳入 AOT 范围。 |
| 追踪 `NewSpan(..., new { ... })` 的标签对象 | `ClientBase` 等 | 标签仅用于 APM 展示，AOT 下匿名标签对象退化为空对象，不影响业务数据；暂不改造。 |
| HTTP 通道的服务端流式推送与 WebSocket 握手 | `ApiHttpSession.SendMessage`、`ApiHttpHandler` | 非 AOT 差异，而是设计边界：HTTP 为请求-响应模型，流式推送抛 `NotSupportedException`，WS 握手直接回 501。故流式冒烟只覆盖 TCP，属预期。 |
| WS/SSE 客户端通道（`WsChannel`/`SseChannel`/`WsChannelCore`） | `NewLife.Remoting.Clients` | 面向旧平台（非 NETCOREAPP）的接收通道，服务端侧 SRMP-over-WS 已取消，无内建服务端可对测，故不纳入冒烟；通道内无动态代码与运行期反射，风险面为既有逻辑本身。 |
| 基础类型裸值写包不是合法 JSON | `JsonEncoder.EncodeValue` | **设计约定，非缺陷**：简单类型以文本直传（`True`、完整时间、枚举名、裸字符串），只有复杂对象走 JSON 包装。客户端解码侧已按声明类型/原样文本统一加固（§2.3 第 7~8 项）；跨语言客户端需按文本解析标量。详见《架构设计》收口记录第 9 项。 |

---

## 6. 复现方法

```powershell
# 1) 分析器全量告警（库自身构建，期望 0 条；IsAotCompatible 已随 csproj 对 net8.0+ 常驻启用）
dotnet build NewLife.Remoting\NewLife.Remoting.csproj -c Release

# 2) 源生成器单测
dotnet test XUnitTest.SourceGenerator -c Release

# 3) 冒烟实测（22 场景工程；PublishAot 写在工程文件内）
dotnet run -c Release --project AotSmoke\AotSmoke.csproj                    # JIT 姿态基线
dotnet publish AotSmoke\AotSmoke.csproj -c Release -r win-x64 -o out        # NativeAOT 真编译（期望 0 条 ILC 告警）
.\out\AotSmoke.exe                                                          # AOT 运行，逐场景 PASS/FAIL（非 0 退出码即失败）

# 4) 回归
dotnet test XUnitTest -c Release
```

### 6.1 下游包消费验证（分析器是否随包自动生效）

只在开发机验证一次即可，用于确认「下游零配置」这一声明：

```powershell
# ① 打本地包
dotnet pack NewLife.Remoting\NewLife.Remoting.csproj -c Release -o ..\rp-pkg

# ② 新建消费者工程（net10.0 控制台，NuGet.config 只加本地包源）
#    仅 <PackageReference Include="NewLife.Remoting" Version="..." />，不引用生成器项目
#    工程内 PublishAot=true，代码里 server.Register<ConsumerController>() 并断言
#    ApiActionTable.TryGetInvoker(typeof(ConsumerController), "Sum", out _) 命中

# ③ 检查生成物与分析器生效
dotnet build -c Release -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=obj\gen
#    期望：obj\gen\NewLife.Remoting.SourceGenerator\...\ApiActionRegistrar.g.cs 存在且含 RegisterInvoker 行

# ④ AOT 发布并运行
dotnet publish -c Release -r win-x64 -o out
.\out\<应用>.exe        # 期望：FastInvoker 命中 -> True，RPC 往返通过，退出码 0
```

---

## 7. Extensions（ASP.NET Core）纳入 AOT 范围

### 7.1 决策（2026-10-10）

**不拆分新项目**，在 `NewLife.Remoting.Extensions` 包内同时提供两套宿主入口，按发布方式二选一：

| 宿主 | 接入方式 | AOT |
|------|---------|-----|
| ASP.NET Core MVC（现状） | `AddRemoting()` + `AddControllers()`，继承 `BaseController`/`BaseDeviceController` | ❌ 官方支持矩阵中 MVC 不支持 AOT |
| 最小 API（已落地 2026-10-13） | `AddRemotingCore()` + `MapRemoting()` | ✅ 已通过 AOT 冒烟 |

依据：最小 API 本身就是 ASP.NET Core 的一部分，与 MVC 控制器在同一路由管道内共存，因此「改最小 API」不损失给 ASP.NET 项目引用的能力；真正的硬约束是 **AOT 与 MVC 互斥**。

### 7.2 包内依赖边界（决定可复用范围）

| 组成 | 依赖 | AOT |
|------|------|-----|
| `WsCommandSession` / `SseCommandSession` | `System.Net.WebSockets` + 文本协议（非 ASP.NET 中间件） | ✅ 可复用（冒烟已覆盖 WS 指令下发） |
| `TokenService` | `NewLife.Web`(JWT)、`Security`、少量 `System.Reflection` | ✅ 可复用（冒烟覆盖颁发/续期/吊销） |
| `WebHelper` | ASP.NET Http 抽象 | ✅ |
| `Controllers/*`、`ModelBinders/*`、`ApiFilterAttribute`、`RemotingExtensions.AddRemoting` | **MVC** | ❌ MVC 宿主专属，已声明边界；`AddRemotingCore`/`MapRemoting` 不触碰 MVC 类型 |
| `Endpoints/*`（鉴权与端点编排自带实现） | 无 MVC、无 XCode 依赖 | ✅ 与 MVC 侧互为镜像，一致性由镜像测试锁定 |
| `DefaultDeviceService` | **XCode** | ❌ 实测未通过，见 §8 |

### 7.3 已完成的编译期基线

`IsAotCompatible` 已对 net8.0+ 常驻启用，自身告警 **21 条 → 0 条**（net6.0~net10.0 实测）：

| 告警 | 条数 | 位置 | 处置 |
|------|:----:|------|------|
| IL2090 | 16 | `DefaultDeviceService` 静态构造按名称查找 `FindByCode`/`FindBySessionId` | 为泛型参数 `TDevice`/`TOnline` 加 `DynamicallyAccessedMembers(PublicMethods)`，保留设备实体公开方法 |
| IL2026 | 3 | `InterfaceModelBinder` 按运行时类型反序列化 | 抑制声明 + 文档登记 MVC 边界（MVC 宿主不支持 AOT） |
| IL3050 | 2 | `InterfaceModelBinderProvider.GetBinder` 实例化绑定器 | 同上 |

> 实测结论：分析器随主包 `analyzers/dotnet/cs` 正常分发，消费者工程**无需任何额外配置**即自动生成动作委托；JIT 与 NativeAOT 两种姿态下 `FastInvoker` 均命中且 RPC 往返正常（ILC 0 告警）。

> CI 防回归：`.github/workflows/aot.yml`（win-x64 冒烟与分析器零告警硬约束阻断；linux-x64 与 XCode 探针为观察期）。
>
> 说明：受限环境下 NewLife.Core 的 `MachineInfo` 首次探测会向 stderr 打印系统错误后按空值降级，属环境行为（正常 Windows 运行环境不出现），不影响场景结果与退出码。

### 7.4 最小 API 宿主实测（2026-10-13，`AotSmoke.Web`）

`AotSmoke.Web` 进程内启动最小 API 宿主（`AddRemotingCore` + `MapRemoting`），设备服务使用内存实现（不依赖 XCode），以 HTTP/WebSocket 客户端跑 11 个场景：

| # | 场景 | 结果 |
|:-:|------|:----:|
| 01~04 | 登录颁发令牌、心跳装载设备、GET 心跳无请求体、事件上报计数 | PASS |
| 05~08 | 匿名指令下发与回执、命令回执入库、升级返回绝对地址、缺少编码报错 | PASS |
| 09~11 | 无令牌返回 401、WebSocket 长连接接收指令、注销后令牌立即失效 | PASS |

JIT 与 NativeAOT（win-x64，`IsDynamicCodeSupported=False`）双姿态 **11/11 通过**，ILC 告警 0 条。

实现要点（首次 AOT 运行暴露并修复的缺陷）：

| 现象 | 根因 | 处置 |
|------|------|------|
| 登录请求解析为空对象、指令下发报 `No parameterless constructor defined for type 'CommandInModel'` | 端点按运行时类型（接口模型的 DI 实现类型）读写模型，FastJson 需要可反射的构造与公开成员；NativeAOT 下内置模型元数据被裁剪 | 在端点管线方法上以 `[DynamicDependency(...)]` 显式保留 11 个内置模型的构造与公开成员；应用自定义模型需自行保留（`[DynamicDependency]` 或 `[JsonSerializable]` 标记） |

> 端点请求体按 DI 实现类型反序列化、响应统一封装为 `code/data` 信封，与 MVC 宿主的 `ApiFilterAttribute` 行为一致，设备客户端可在两套宿主间无感切换。端点与 `BaseDeviceController` 互为镜像实现，修改端点逻辑须两侧同步（镜像测试锁定）。

---

## 8. XCode 依赖的 AOT 运行期探针（2026-10-13）

### 8.1 结论

`DefaultDeviceService<TDevice,TOnline>` 依赖 **NewLife.XCode 实体**（`XCode 12.2.2026.901`），当前**无法**在 NativeAOT 下运行；`AotSmoke.XCode` 作为探针长期留存，XCode 补齐 AOT 支持后应转为 CI 硬约束。

| 姿态 | 结果 | 说明 |
|------|:----:|------|
| JIT（`dotnet run`） | **6/6 通过** | 建表/新增、唯一索引查询与更新、在线记录读写、设备服务登录/心跳/注销 |
| NativeAOT（win-x64） | **0/6 通过** | 首次实体操作即抛 `TypeInitializationException` |

### 8.2 失败证据

```
[0] System.TypeInitializationException: A type initializer threw an exception.
[1] System.Exception: Fail to create object type=XCode.Entity`1+DefaultEntityFactory[[AotSmoke.XCode.Data.Device, ...]] parameters=
        No parameterless constructor defined for type 'XCode.Entity`1+DefaultEntityFactory[AotSmoke.XCode.Data.Device]'.
[2] System.MissingMethodException: No parameterless constructor defined for type 'XCode.Entity`1+DefaultEntityFactory[AotSmoke.XCode.Data.Device]'.
```

发布期告警（XCode 侧，非 Remoting 侧）：

| 告警 | 位置 | 含义 |
|------|------|------|
| IL2104 / IL3053 | `XCode.dll` | XCode 程序集自身产生裁剪与 AOT 分析告警 |
| IL3000 ×4 | `DriverLoader.Load`、`DbBase.GetProviderFactory` | 使用 `Assembly.Location`，单文件/AOT 下恒为空串 |

### 8.3 根因与解除条件

XCode 通过**运行时反射构造实体工厂**（`Entity<T>.Meta.Factory` 用反射创建 `DefaultEntityFactory<T>`，且需要无参构造），该构造在 AOT 裁剪下被移除。解除条件（按 XCode 侧）：

1. 实体工厂改为编译期确定的构造（如 `new()` 约束、源生成器注册工厂），不再依赖反射实例化；
2. 清理 `Assembly.Location` 用法（改用 `AppContext.BaseDirectory`）；
3. 为 XCode 项目启用 `IsAotCompatible` 并清零自身告警。

Remoting 侧已就绪：`DefaultDeviceService` 泛型参数的保留注解（§7.3 IL2090 处置）与端点宿主均无需再改，XCode 达标后探针即可全绿。

### 8.4 复现方法

```powershell
# JIT 姿态基线（期望 6/6 通过）
dotnet run -c Release --project AotSmoke.XCode\AotSmoke.XCode.csproj

# NativeAOT 真编译与运行（当前期望 0/6，失败信息见 8.2）
dotnet publish AotSmoke.XCode\AotSmoke.XCode.csproj -c Release -r win-x64 -o out-xcode
.\out-xcode\AotSmoke.XCode.exe
```
