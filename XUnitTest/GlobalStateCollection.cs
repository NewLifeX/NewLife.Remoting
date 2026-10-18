using System;
using Xunit;

namespace XUnitTest;

/// <summary>进程级全局状态测试集合</summary>
/// <remarks>改写 <c>Localization.Culture</c> 等全局状态的用例放入本集合，与其它集合串行执行，避免并行读到中间状态。</remarks>
[CollectionDefinition("GlobalState", DisableParallelization = true)]
public class GlobalStateCollection { }
