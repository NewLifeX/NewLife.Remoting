# NewLife.Remoting AOT 兼容性现状与边界

> 基线：NewLife.Core 12.0（v12 分支）｜评估方式：**NativeAOT 真实编译 + 运行实测**（win-x64，.NET 10）｜冒烟工程：`AotSmoke/`
> 范围：核心库 `NewLife.Remoting`。`NewLife.Remoting.Extensions` 与样例不纳入，边界见 §5。

---

## 1. 现状摘要

- **核心链路实测全通**：17 场景冒烟 JIT/AOT 双通（§2.1），覆盖 RPC 注册与调用、JSON 编解码、二进制直通、参数默认值、异步动作、流式（引用类型与值类型）、HTTP 入口、依赖注入、每请求实例化、通配路由、源生成命中与回退、内置 DTO 往返、客户端基类登录/心跳/事件/注销全链路、单方法注册。
- **支持方式**：沿用 NewLife.Core 的「泛型/Type 入口保留注解 + 源生成器静态表 + 动态能力守卫」三件套，并为 Remoting 增补第五条生成器通道（动作分发表 + 流式泵 + 签名类型保留），见 §3。
- **告警面已收敛**：`IsAotCompatible` 已对 net8.0/net9.0/net10.0 常驻启用，库自身分析器告警由改造前 **19 条唯一告警** 收敛到 **0 条**；AOT 发布的 ILC 告警 **0 条**（§2.2）。
- **改造中发现并修复 3 处库侧 AOT 缺陷**：HTTP 响应封套与客户端注销/升级参数使用匿名类型被裁剪、动作签名模型类型成员被裁剪。这些缺陷在 JIT 下不可见，只有真机 AOT 才会暴露（§2.3）。

---

## 2. 实测数据

### 2.1 运行期：17 场景冒烟（JIT/AOT 双通）

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
| | | 17 | 单方法注册的静态委托与裁剪保留 |

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
| `NewLife.Remoting.Extensions`（ASP.NET Core MVC + NewLife.XCode） | 独立包 | MVC 与 XCode 均为反射重度依赖，不在 NativeAOT 支持范围。如需，应改为最小 API + Core 的 `HttpDispatchTable` 静态分发，属独立任务。 |
| `Samples/*`（IoTZero / ZeroServer / Zero.RpcServer / Demo） | 样例工程 | 依赖 XCode / Redis / Stardust，不纳入 AOT 范围。 |
| 追踪 `NewSpan(..., new { ... })` 的标签对象 | `ClientBase` 等 | 标签仅用于 APM 展示，AOT 下匿名标签对象退化为空对象，不影响业务数据；暂不改造。 |

---

## 6. 复现方法

```powershell
# 1) 分析器全量告警（库自身构建，期望 0 条；IsAotCompatible 已随 csproj 对 net8.0+ 常驻启用）
dotnet build NewLife.Remoting\NewLife.Remoting.csproj -c Release

# 2) 源生成器单测
dotnet test XUnitTest.SourceGenerator -c Release

# 3) 冒烟实测（17 场景工程；PublishAot 写在工程文件内）
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

> 实测结论：分析器随主包 `analyzers/dotnet/cs` 正常分发，消费者工程**无需任何额外配置**即自动生成动作委托；JIT 与 NativeAOT 两种姿态下 `FastInvoker` 均命中且 RPC 往返正常（ILC 0 告警）。

> CI 防回归：`.github/workflows/aot.yml`（win-x64 冒烟与分析器零告警硬约束阻断；linux-x64 冒烟为观察期）。
>
> 说明：受限环境下 NewLife.Core 的 `MachineInfo` 首次探测会向 stderr 打印系统错误后按空值降级，属环境行为（正常 Windows 运行环境不出现），不影响场景结果与退出码。
