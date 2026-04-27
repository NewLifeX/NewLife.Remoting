using Xunit;

namespace XUnitTest.Samples;

/// <summary>集成测试集合定义。将 ZeroServer 和 IoTZero 测试放在同一集合中串行执行，避免 XCode 静态 DAL 连接跨进程污染</summary>
[CollectionDefinition("SamplesIntegration")]
public sealed class SamplesIntegrationCollection { }
