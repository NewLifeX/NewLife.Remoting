using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NewLife.Remoting;

/// <summary>NativeAOT 元数据保留根。保证通信模型在裁剪后仍可被反射枚举，供 JSON 序列化/反序列化使用</summary>
/// <remarks>
/// 登录/心跳等动作的返回类型常以接口（如 <see cref="Models.ILoginResponse"/>）声明，泛型保留注解只作用于接口本身，
/// 实际反序列化的具体类型成员仍会被裁剪，导致响应字段全部丢失（表现为登录成功但令牌为空，后续调用鉴权失败）。
/// 因此对内置模型统一以 DynamicDependency 显式保留公开成员与无参构造。
/// 新增内置消息模型必须同步登记。
/// </remarks>
internal static class AotRoots
{
#if NET5_0_OR_GREATER
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Models.LoginRequest))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Models.LoginResponse))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Models.LogoutResponse))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Models.PingRequest))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Models.PingResponse))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Models.CommandModel))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Models.CommandInModel))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Models.CommandReplyModel))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Models.EventModel))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Models.UpgradeInfo))]
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Ensure() { }
}
