using System.Runtime.CompilerServices;

namespace NewLife.Remoting;

/// <summary>动态代码能力检查。用于在不支持运行时动态代码生成的环境（如 NativeAOT）给出明确报错</summary>
internal static class DynamicCode
{
    /// <summary>当前运行时是否支持动态代码生成</summary>
    public static Boolean IsSupported
    {
        get
        {
#if NET5_0_OR_GREATER
            return RuntimeFeature.IsDynamicCodeSupported;
#else
            return true;
#endif
        }
    }

    /// <summary>在不支持动态代码的环境抛出明确异常</summary>
    /// <param name="feature">功能名称，用于组织异常信息</param>
    public static void ThrowIfNotSupported(String feature)
    {
        if (!IsSupported)
            throw new NotSupportedException($"{feature} 依赖运行时动态代码生成，当前环境（如 NativeAOT）不支持；请改用源生成器预生成的静态路径。");
    }
}
