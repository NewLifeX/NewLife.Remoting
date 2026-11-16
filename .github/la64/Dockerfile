# 龙芯 LA64（linux-loongarch64）运行基底镜像：loong64 Debian + 龙芯 .NET SDK。
# 构建时须在已注册 binfmt（tonistiigi/binfmt --install loong64）的 x64 宿主上执行：
#   curl -fL -o dotnet-sdk-<ver>-linux-loongarch64.tar.gz <龙芯 SDK 发布地址>
#   docker build --platform linux/loong64 -t la64-dotnet:<ver> .github/la64
# SDK 安装包由宿主（x64）下载后 COPY 进来：容器内 curl/证书依赖不稳定（基础镜像构建较早、debian-ports 索引过期）。
# 说明：QEMU_CPU=la464 + DOTNET_EnableHWIntrinsic=0 用于规避 qemu-user 对 LASX/LSX 仿真的缺陷（曾致 SIGILL/SIGSEGV）。
# 全球化：必须保留 ICU。关闭 ICU（DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1）时 .NET 8+ 默认只允许不变区域，
# 测试进程的模块初始化器里 CultureInfo.GetCultureInfo("en-US") 会直接抛 CultureNotFoundException，
# 整个测试程序集在发现阶段就崩掉（现象：全量用例 20 秒内退出、不产出 TRX）。
FROM cnclarechen/numpy-loong64-debian:v1

# 基础镜像构建较早：debian-ports 索引已过期且缺 GPG key，关闭时效/签名校验后尽力补齐 libssl（失败不阻断）
# ICU 是硬依赖：逐个尝试常见包名，全会失败时让构建直接报错，而不是启动时才报“Couldn't find a valid ICU package”
RUN apt-get update -qq -o Acquire::Check-Valid-Until=false -o Acquire::AllowInsecureRepositories=true || true \
 && (apt-get install -y -qq --allow-unauthenticated --no-install-recommends libssl3t64 \
  || apt-get install -y -qq --allow-unauthenticated --no-install-recommends libssl3 || true) \
 && (apt-get install -y -qq --allow-unauthenticated --no-install-recommends libicu76 \
  || apt-get install -y -qq --allow-unauthenticated --no-install-recommends libicu75 \
  || apt-get install -y -qq --allow-unauthenticated --no-install-recommends libicu74 \
  || apt-get install -y -qq --allow-unauthenticated --no-install-recommends libicu72 \
  || apt-get install -y -qq --allow-unauthenticated --no-install-recommends libicu67 \
  || apt-get install -y -qq --allow-unauthenticated --no-install-recommends libicu-dev)

COPY dotnet-sdk-*.tar.gz /tmp/sdk.tar.gz
RUN mkdir -p /usr/share/dotnet \
 && tar -xzf /tmp/sdk.tar.gz -C /usr/share/dotnet \
 && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
 && rm -f /tmp/sdk.tar.gz

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_EnableHWIntrinsic=0 \
    QEMU_CPU=la464 \
    NUGET_PACKAGES=/nuget

# QEMU 用户态仿真下 JIT 代码发布（W^X 双映射 + 指令缓存刷新）不稳定，曾致 SIGILL/内存损坏/属性求值 NRE；
# 关闭 W^X 改用传统可写可执行内存页发布代码
ENV DOTNET_EnableWriteXorExecute=0 \
    MSBUILDDISABLENODEREUSE=1 \
    DOTNET_ROLL_FORWARD=LatestMajor
