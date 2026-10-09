using System.ComponentModel;
using Microsoft.CodeAnalysis;
using NewLife.Remoting;
using NewLife.SourceGenerator;
using Xunit;

namespace XUnitTest.SourceGenerator;

/// <summary>Api 动作静态表生成器测试。覆盖「生成命中」与「回退」双场景</summary>
public class ApiActionTableGeneratorTests
{
    #region 常量
    private static readonly MetadataReference[] _refs =
    [
        MetadataReference.CreateFromFile(typeof(ApiServer).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(NewLife.Log.XTrace).Assembly.Location),
    ];

    private static readonly String[] _symbols = ["NET5_0_OR_GREATER"];

    private const String PlainSource = """
using System.Collections.Generic;
using NewLife.Remoting;

/// <summary>普通控制器</summary>
public class HomeController
{
    /// <summary>求和</summary>
    public Int32 Sum(Int32 a, Int32 b) => a + b;

    /// <summary>回显</summary>
    public String Echo(String text) => text;

    /// <summary>无返回</summary>
    public void Fire() { }

    /// <summary>流式返回</summary>
    public IAsyncEnumerable<Int32> Range(Int32 count) => Gen(count);

    private static async IAsyncEnumerable<Int32> Gen(Int32 count)
    {
        for (var i = 0; i < count; i++) yield return i;
    }
}

/// <summary>注册入口</summary>
public static class Entry
{
    /// <summary>注册</summary>
    public static void Setup(ApiServer server) => server.Register<HomeController>();
}
""";

    private const String ApiAttributeSource = """
using NewLife.Remoting;

/// <summary>要求 Api 特性的控制器</summary>
[Api("Secure")]
public class SecureController
{
    /// <summary>公开无特性，不暴露</summary>
    public Int32 Hidden(Int32 a) => a;

    /// <summary>私有但带特性，暴露</summary>
    [Api("Secret")]
    private String Secret(String text) => text;

    /// <summary>静态带特性，暴露</summary>
    [Api("Static")]
    public static Int32 StaticSum(Int32 a, Int32 b) => a + b;
}

/// <summary>注册入口</summary>
public static class Entry
{
    /// <summary>注册</summary>
    public static void Setup(ApiServer server) => server.Register<SecureController>();
}
""";

    private const String UnsupportedSource = """
using NewLife.Remoting;

/// <summary>含无法生成签名的控制器</summary>
public class TrickyController
{
    /// <summary>重载一</summary>
    public Int32 Load(Int32 a) => a;

    /// <summary>重载二</summary>
    public Int32 Load(String a) => 0;

    /// <summary>引用参数</summary>
    public void Ref_(ref Int32 a) => a++;

    /// <summary>可变参数</summary>
    public Int32 Sum(params Int32[] values) => values.Length;

    /// <summary>正常方法</summary>
    public Int32 Ok(Int32 a) => a;
}

/// <summary>注册入口</summary>
public static class Entry
{
    /// <summary>注册</summary>
    public static void Setup(ApiServer server) => server.Register<TrickyController>();
}
""";

    private const String NoRegisterSource = """
using NewLife.Remoting;

/// <summary>未注册的控制器</summary>
public class IdleController
{
    /// <summary>方法</summary>
    public Int32 Sum(Int32 a, Int32 b) => a + b;
}
""";

    private const String NestedSource = """
using NewLife.Remoting;

namespace Deep.Space
{
    /// <summary>测试类</summary>
    public class OuterTests
    {
        internal class NestedController
        {
            public Int32 Hello(Int32 a) => a;
        }

        /// <summary>注册</summary>
        public void Setup(ApiServer server) => server.Register<NestedController>();
    }
}
""";

    private const String PrivateNestedSource = """
using NewLife.Remoting;

namespace Deep.Space
{
    /// <summary>测试类</summary>
    public class OuterTests
    {
        class PrivateController
        {
            public Int32 Hello(Int32 a) => a;
        }

        /// <summary>注册</summary>
        public void Setup(ApiServer server) => server.Register<PrivateController>();
    }
}
""";

    private const String DtoSource = """
using NewLife.Remoting;

/// <summary>参数模型</summary>
public class SendArgs
{
    /// <summary>值</summary>
    public Int32 Value { get; set; }
}

/// <summary>返回模型</summary>
public class SendResult
{
    /// <summary>值</summary>
    public Int32 Value { get; set; }
}

/// <summary>控制器</summary>
public class DtoController
{
    /// <summary>发送</summary>
    public SendResult Send(SendArgs args) => new SendResult { Value = args.Value };
}

/// <summary>注册入口</summary>
public static class Entry
{
    /// <summary>注册</summary>
    public static void Setup(ApiServer server) => server.Register<DtoController>();
}
""";
    #endregion

    #region 测试
    /// <summary>普通控制器：公开实例方法全部生成强类型调用委托</summary>
    [Fact]
    [DisplayName("普通控制器生成动作调用委托")]
    public void GenerateInvokers()
    {
        var (compilation, result) = GeneratorTestHost.RunWithSymbols(new ApiActionTableGenerator(), PlainSource, _symbols, _refs);
        var sources = GeneratorTestHost.GetGeneratedSources(result);
        var code = Assert.Single(sources).Value;

        Assert.Contains("ApiActionTable.RegisterInvoker(typeof(global::HomeController), \"Sum\"", code);
        Assert.Contains("ApiActionTable.RegisterInvoker(typeof(global::HomeController), \"Echo\"", code);
        Assert.Contains("ApiActionTable.RegisterInvoker(typeof(global::HomeController), \"Fire\"", code);

        // 值类型元素流式返回：静态扎根流式泵
        Assert.Contains("ApiActionTable.RegisterStreamPump(typeof(int), global::NewLife.Remoting.ApiStreamPump.PumpAsync<int>)", code);

        Assert.Empty(compilation.GetDiagnostics().Where(e => e.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>带 Api 特性的控制器：只有带特性的方法生成，生成代码可访问的公开成员照常生成</summary>
    /// <remarks>私有/受保护成员无法从生成代码调用，不在覆盖范围内，由保留注解保证其元数据并回退反射路径</remarks>
    [Fact]
    [DisplayName("Api特性控制器按特性筛选方法")]
    public void GenerateApiAttributeMembers()
    {
        var (compilation, result) = GeneratorTestHost.RunWithSymbols(new ApiActionTableGenerator(), ApiAttributeSource, _symbols, _refs);
        var code = Assert.Single(GeneratorTestHost.GetGeneratedSources(result)).Value;

        // 无 [Api] 的公开方法不暴露
        Assert.DoesNotContain("\"Hidden\"", code);
        // 带 [Api] 的公开静态方法暴露
        Assert.True(code.Contains("\"StaticSum\""), code);
        // 带 [Api] 的私有方法生成代码不可访问，回退反射路径
        Assert.DoesNotContain("\"Secret\"", code);

        Assert.Empty(compilation.GetDiagnostics().Where(e => e.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>重载、ref、params 等无法安全生成的签名跳过，其余照常生成</summary>
    [Fact]
    [DisplayName("无法生成签名的动作跳过并回退")]
    public void SkipUnsupportedSignatures()
    {
        var (compilation, result) = GeneratorTestHost.RunWithSymbols(new ApiActionTableGenerator(), UnsupportedSource, _symbols, _refs);
        var code = Assert.Single(GeneratorTestHost.GetGeneratedSources(result)).Value;

        Assert.DoesNotContain("\"Load\"", code);
        Assert.DoesNotContain("\"Ref_\"", code);
        Assert.DoesNotContain("\"Sum\"", code);
        Assert.Contains("\"Ok\"", code);

        Assert.Empty(compilation.GetDiagnostics().Where(e => e.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>入参与返回模型类型生成成员保留，使运行时类型 JSON 绑定在 AOT 下可用</summary>
    [Fact]
    [DisplayName("签名模型类型生成成员保留")]
    public void GenerateSignaturePreserve()
    {
        var (compilation, result) = GeneratorTestHost.RunWithSymbols(new ApiActionTableGenerator(), DtoSource, _symbols, _refs);
        var code = Assert.Single(GeneratorTestHost.GetGeneratedSources(result)).Value;

        Assert.True(code.Contains("ApiActionTable.Preserve(typeof(global::SendArgs))"), code);
        Assert.True(code.Contains("ApiActionTable.Preserve(typeof(global::SendResult))"), code);
        Assert.Empty(compilation.GetDiagnostics().Where(e => e.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>没有泛型注册调用时不产出任何代码，保持原反射路径</summary>
    [Fact]
    [DisplayName("无注册调用时不生成代码")]
    public void NoRegisterNoSource()
    {
        var (compilation, result) = GeneratorTestHost.RunWithSymbols(new ApiActionTableGenerator(), NoRegisterSource, _symbols, _refs);
        var sources = GeneratorTestHost.GetGeneratedSources(result);

        Assert.Empty(sources);
        Assert.Empty(compilation.GetDiagnostics().Where(e => e.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>嵌套的内部控制器（同程序集可见）照常生成</summary>
    [Fact]
    [DisplayName("嵌套内部控制器生成调用委托")]
    public void GenerateNestedController()
    {
        var (compilation, result) = GeneratorTestHost.RunWithSymbols(new ApiActionTableGenerator(), NestedSource, _symbols, _refs);
        var sources = GeneratorTestHost.GetGeneratedSources(result);
        var code = Assert.Single(sources).Value;

        Assert.True(code.Contains("\"Hello\""), code);
        Assert.Empty(compilation.GetDiagnostics().Where(e => e.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>私有嵌套控制器在生成代码中不可见，不生成并回退反射路径</summary>
    [Fact]
    [DisplayName("私有嵌套控制器回退反射路径")]
    public void SkipPrivateNestedController()
    {
        var (compilation, result) = GeneratorTestHost.RunWithSymbols(new ApiActionTableGenerator(), PrivateNestedSource, _symbols, _refs);

        Assert.Empty(GeneratorTestHost.GetGeneratedSources(result));
        Assert.Empty(compilation.GetDiagnostics().Where(e => e.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>低版本目标（无 NET5_0_OR_GREATER）不生成模块初始化器</summary>
    [Fact]
    [DisplayName("低版本目标不生成代码")]
    public void SkipLowTargetFramework()
    {
        var (_, result) = GeneratorTestHost.RunWithSymbols(new ApiActionTableGenerator(), PlainSource, ["NET45"], _refs);
        var sources = GeneratorTestHost.GetGeneratedSources(result);

        Assert.Empty(sources);
    }
    #endregion
}
