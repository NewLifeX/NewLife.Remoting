using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NewLife.SourceGenerator;

/// <summary>Api 动作静态表生成器。扫描 <c>ApiServer.Register&lt;T&gt;()</c> 等控制器注册调用点，
/// 为 T 的动作方法生成「强类型调用委托」与「流式泵」注册</summary>
/// <remarks>
/// <para>生成物以 <c>[ModuleInitializer]</c> 注册进 <c>NewLife.Remoting.ApiActionTable</c>，
/// 运行期命中时零反射调用、零表达式编译；未命中（非泛型注册入口、重载、ref/out/params、
/// 生成代码不可访问的私有/受保护成员、跨程序集成员等）自动回退原有表达式编译与反射路径，运行语义不变。</para>
/// <para>私有/受保护成员无法从生成代码直接调用，故不在本生成器覆盖范围内，由 <c>Register&lt;TService&gt;()</c>
/// 的保留注解保证其元数据不被裁剪，运行期走反射调用。</para>
/// <para>值类型 <c>IAsyncEnumerable&lt;T&gt;</c> 的元素类型在此静态扎根，避免 NativeAOT 下不可用的运行期泛型实例化。</para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ApiActionTableGenerator : IIncrementalGenerator
{
    #region 常量
    private const String RegisterMethodName = "Register";
    private const String HostNamespace = "NewLife.Remoting";
    private const String ApiAttributeName = "NewLife.Remoting.ApiAttribute";
    private const String AsyncEnumerableName = "IAsyncEnumerable";
    private const String AsyncEnumerableNamespace = "System.Collections.Generic";
    private const String TaskNamespace = "System.Threading.Tasks";
    #endregion

    #region 生成入口
    /// <summary>初始化增量生成管线</summary>
    /// <param name="context">生成上下文</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(IsCandidate, Extract)
            .Where(static x => x != null)
            .SelectMany(static (x, _) => x!);

        var combined = context.CompilationProvider.Combine(candidates.Collect());

        context.RegisterSourceOutput(combined, static (spc, pair) => Emit(spc, pair.Left, pair.Right));
    }
    #endregion

    #region 提取
    // 廉价语法谓词：只放行名为 Register 且带单个类型实参的调用
    private static Boolean IsCandidate(SyntaxNode node, CancellationToken token)
        => node is InvocationExpressionSyntax inv && GetGenericName(inv) != null;

    private static GenericNameSyntax? GetGenericName(InvocationExpressionSyntax inv)
    {
        SimpleNameSyntax? name = inv.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name,
            SimpleNameSyntax simple => simple,
            _ => null,
        };

        return name is GenericNameSyntax g && g.Identifier.Text == RegisterMethodName && g.TypeArgumentList.Arguments.Count == 1 ? g : null;
    }

    // 语义提取：确认是 NewLife.Remoting 的控制器注册入口，取控制器类型并生成注册行
    private static ImmutableArray<String> Extract(GeneratorSyntaxContext context, CancellationToken token)
    {
        var inv = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        if (model.GetSymbolInfo(inv, token).Symbol is not IMethodSymbol method) return default;
        if (method.Name != RegisterMethodName || method.TypeArguments.Length != 1) return default;
        if (!IsRegisterEntry(method.ContainingType)) return default;
        if (method.TypeArguments[0] is not INamedTypeSymbol controller) return default;

        return BuildFragment(controller, model.Compilation);
    }

    // 注册入口：ApiServer.Register<T>() 或 IApiManager/ApiManager.Register<T>()
    private static Boolean IsRegisterEntry(INamedTypeSymbol? type)
    {
        if (type == null) return false;
        if (type.ContainingNamespace?.ToDisplayString() != HostNamespace) return false;

        if (type.Name is "ApiServer" or "IApiManager" or "ApiManager") return true;

        return type.AllInterfaces.Any(i => i.Name == "IApiManager" && i.ContainingNamespace?.ToDisplayString() == HostNamespace);
    }
    #endregion

    #region 构建
    // 为控制器生成注册行：动作调用委托 + 值类型流式泵
    private static ImmutableArray<String> BuildFragment(INamedTypeSymbol controller, Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<String>();

        if (controller.TypeKind != TypeKind.Class || controller.IsStatic || controller.IsAbstract) return builder.ToImmutable();
        if (controller.IsUnboundGenericType) return builder.ToImmutable();
        if (controller.TypeArguments.Any(e => e is ITypeParameterSymbol)) return builder.ToImmutable();
        if (!compilation.IsSymbolAccessibleWithin(controller, compilation.Assembly)) return builder.ToImmutable();

        var typeName = controller.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var requireApi = HasApiAttribute(controller, true);

        // 收集候选动作：规则与 ApiManager.RegisterAll 保持一致。
        // 派生类重写基类成员时只保留最派生的一个；同名而非重写关系（new 隐藏、重载）整体跳过，
        // 避免与原反射路径的「首个匹配」产生歧义
        var byName = new Dictionary<String, IMethodSymbol>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        for (var current = controller; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is not IMethodSymbol m) continue;
                if (m.MethodKind != MethodKind.Ordinary || m.IsImplicitlyDeclared) continue;
                if (m.IsGenericMethod) continue;
                if (!IsExposed(m, requireApi)) continue;

                if (IsOverriddenBy(byName.Values, m)) continue;

                if (byName.TryGetValue(m.Name, out var exist))
                {
                    if (!SymbolEqualityComparer.Default.Equals(exist, m)) duplicates.Add(m.Name);
                }
                else
                    byName[m.Name] = m;
            }
        }

        foreach (var name in byName.Keys.OrderBy(e => e, StringComparer.Ordinal))
        {
            if (duplicates.Contains(name)) continue;

            var m = byName[name];
            var call = BuildCall(m, typeName, compilation);
            if (call == null) continue;

            builder.Add($"global::NewLife.Remoting.ApiActionTable.RegisterInvoker(typeof({typeName}), \"{name}\", {call});");

            // 入参与返回类型的成员保留：运行时类型 JSON 绑定在裁剪与 AOT 下需要公开无参构造与公开成员
            foreach (var t in GetSignatureTypes(m))
            {
                var tn = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                builder.Add($"global::NewLife.Remoting.ApiActionTable.Preserve(typeof({tn}));");
            }

            // 流式泵：值类型元素在 AOT 下无法运行期泛型实例化，这里编译期静态扎根
            var element = GetStreamElementType(m.ReturnType);
            if (element != null && element.IsValueType && element.TypeKind != TypeKind.TypeParameter)
            {
                var elementName = element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                builder.Add($"global::NewLife.Remoting.ApiActionTable.RegisterStreamPump(typeof({elementName}), global::NewLife.Remoting.ApiStreamPump.PumpAsync<{elementName}>);");
            }
        }

        return builder.ToImmutable();
    }

    // 暴露规则与 ApiManager.RegisterAll 的绑定标志一致：
    // 有 [Api] 的控制器要求方法自身带 [Api]（公开/非公开/实例/静态均可），否则取公开实例方法
    private static Boolean IsExposed(IMethodSymbol m, Boolean requireApi)
        => requireApi ? HasApiAttribute(m, false) : m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic;

    // 生成强类型调用委托；无法用委托安全表达时返回 null，交由反射回退
    private static String? BuildCall(IMethodSymbol m, String typeName, Compilation compilation)
    {
        if (!compilation.IsSymbolAccessibleWithin(m, compilation.Assembly)) return null;
        if (m.ReturnsByRef || m.ReturnsByRefReadonly) return null;
        if (IsUnsupported(m.ReturnType)) return null;

        foreach (var p in m.Parameters)
        {
            if (p.RefKind != RefKind.None) return null;
            if (p.IsParams) return null;
            if (IsUnsupported(p.Type)) return null;
        }

        var args = String.Join(", ", m.Parameters.Select((p, i) =>
        {
            var pn = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return p.Type.IsValueType
                ? $"(args[{i}] == null ? default({pn}) : ({pn})args[{i}]!)"
                : $"({pn})args[{i}]!";
        }));

        var target = m.IsStatic ? $"{typeName}.{m.Name}" : $"(({typeName})instance!).{m.Name}";
        var call = $"{target}({args})";

        return m.ReturnsVoid ? $"(instance, args) => {{ {call}; return null; }}" : $"(instance, args) => {call}";
    }

    // 指针、函数指针、ref struct 等无法装箱或无法在委托签名中表达的类型
    private static Boolean IsUnsupported(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer) return true;
        if (type is IArrayTypeSymbol array && IsUnsupported(array.ElementType)) return true;
        if (type.IsRefLikeType) return true;

        return false;
    }

    // 派生类重写基类成员时，基类声明不再单独登记
    private static Boolean IsOverriddenBy(IEnumerable<IMethodSymbol> existing, IMethodSymbol baseMethod)
    {
        foreach (var item in existing)
        {
            for (var o = item.OverriddenMethod; o != null; o = o.OverriddenMethod)
            {
                if (SymbolEqualityComparer.Default.Equals(o, baseMethod)) return true;
            }
        }

        return false;
    }

    // 取 IAsyncEnumerable<T> 的元素类型，兼容 Task<IAsyncEnumerable<T>>
    private static ITypeSymbol? GetStreamElementType(ITypeSymbol returnType)
    {
        var t = UnwrapTask(returnType);

        if (t is INamedTypeSymbol { IsGenericType: true } seq
            && seq.Name == AsyncEnumerableName
            && seq.ContainingNamespace?.ToDisplayString() == AsyncEnumerableNamespace
            && seq.TypeArguments.Length == 1)
            return seq.TypeArguments[0];

        return null;
    }

    // 取动作签名中需要保留成员的具名类型：各入参类型与解包后的返回类型
    private static IEnumerable<ITypeSymbol> GetSignatureTypes(IMethodSymbol m)
    {
        var seen = new HashSet<String>(StringComparer.Ordinal);

        foreach (var p in m.Parameters)
        {
            if (IsPreservable(p.Type) && seen.Add(p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                yield return p.Type;
        }

        var rt = GetStreamElementType(m.ReturnType) ?? UnwrapTask(m.ReturnType);
        if (IsPreservable(rt) && seen.Add(rt.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            yield return rt;
    }

    // 只需为具名非 BCL 类型保留成员；基元类型、字符串、数组等无需处理
    private static Boolean IsPreservable(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named) return false;
        if (named.SpecialType != SpecialType.None) return false;
        if (named.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface)) return false;
        if (named.TypeArguments.Any(e => e is ITypeParameterSymbol)) return false;

        return named.ContainingNamespace?.ToDisplayString() is not ("System" or "System.Collections" or "System.Collections.Generic");
    }

    // 解包 Task<T>
    private static ITypeSymbol UnwrapTask(ITypeSymbol returnType)
    {
        if (returnType is INamedTypeSymbol { IsGenericType: true } task
            && task.Name == "Task"
            && task.ContainingNamespace?.ToDisplayString() == TaskNamespace
            && task.TypeArguments.Length == 1)
            return task.TypeArguments[0];

        return returnType;
    }

    // 是否带 [Api]；inherit 为 true 时沿基类链查找（对齐 Type.GetCustomAttribute<T>(inherit: true)）
    private static Boolean HasApiAttribute(ISymbol symbol, Boolean inherit)
    {
        for (var t = symbol as INamedTypeSymbol; t != null; t = inherit ? t.BaseType : null)
        {
            if (t.GetAttributes().Any(e => e.AttributeClass?.ToDisplayString() == ApiAttributeName)) return true;

            if (!inherit) break;
        }

        if (symbol is IMethodSymbol)
            return symbol.GetAttributes().Any(e => e.AttributeClass?.ToDisplayString() == ApiAttributeName);

        return false;
    }
    #endregion

    #region 产出
    // 输出生成源码。仅 .NET 5+（含模块初始化器）且在可用语言版本下生成
    private static void Emit(SourceProductionContext context, Compilation compilation, ImmutableArray<String> items)
    {
        if (items.IsDefaultOrEmpty) return;

        var set = new SortedSet<String>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!String.IsNullOrEmpty(item)) set.Add(item);
        }
        if (set.Count == 0) return;

        var tree = compilation.SyntaxTrees.FirstOrDefault();
        if (tree?.Options is not CSharpParseOptions options) return;
        if (!options.PreprocessorSymbolNames.Contains("NET5_0_OR_GREATER")) return;

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("namespace NewLife.SourceGenerator.Generated");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>源生成的 Api 动作静态表注册。NewLife.Remoting 源生成器自动生成，请勿修改</summary>");
        builder.AppendLine("    internal static class ApiActionRegistrar");
        builder.AppendLine("    {");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("        internal static void Initialize()");
        builder.AppendLine("        {");
        foreach (var line in set)
        {
            builder.Append("            ").AppendLine(line);
        }
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        context.AddSource("ApiActionRegistrar.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }
    #endregion
}
