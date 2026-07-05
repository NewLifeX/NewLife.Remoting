using System;
using System.Collections.Generic;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest.Models;

/// <summary>CommandModel扩展字段测试</summary>
public class CommandModelTests
{
    #region CommandModel新增字段
    [Fact(DisplayName = "CommandModel新增Status字段默认值")]
    public void CommandModel_NewStatusField_DefaultValue()
    {
        var model = new CommandModel();

        Assert.Equal(CommandStatus.就绪, model.Status);
    }

    [Fact(DisplayName = "CommandModel新增字段读写")]
    public void CommandModel_NewFields_ReadWrite()
    {
        var model = new CommandModel
        {
            Id = 123,
            Command = "Restart",
            Status = CommandStatus.已完成,
        };

        Assert.Equal(123, model.Id);
        Assert.Equal("Restart", model.Command);
        Assert.Equal(CommandStatus.已完成, model.Status);
    }

    [Fact(DisplayName = "CommandModel已有字段不受影响")]
    public void CommandModel_ExistingFields_StillWork()
    {
        var model = new CommandModel
        {
            Id = 1,
            Command = "Test",
            Argument = "arg1",
            StartTime = DateTime.UtcNow,
            Expire = DateTime.UtcNow.AddHours(1),
            TraceId = "trace-abc"
        };

        Assert.Equal(1, model.Id);
        Assert.Equal("Test", model.Command);
        Assert.Equal("arg1", model.Argument);
        Assert.Equal("trace-abc", model.TraceId);
        Assert.True(model.Expire > model.StartTime);
    }
    #endregion

    #region CommandStatus枚举新增值
    [Theory(DisplayName = "CommandStatus枚举值验证")]
    [InlineData(CommandStatus.就绪, 0)]
    [InlineData(CommandStatus.处理中, 1)]
    [InlineData(CommandStatus.已完成, 2)]
    [InlineData(CommandStatus.取消, 3)]
    [InlineData(CommandStatus.错误, 4)]
    public void CommandStatus_NewValues_CorrectIntValue(CommandStatus status, Int32 expectedInt)
    {
        Assert.Equal(expectedInt, (Int32)status);
    }

    [Fact(DisplayName = "CommandStatus状态值互不冲突")]
    public void CommandStatus_AllValues_NoConflicts()
    {
        var values = new HashSet<Int32>();
        foreach (CommandStatus status in Enum.GetValues(typeof(CommandStatus)))
        {
            Assert.True(values.Add((Int32)status), $"CommandStatus {(Int32)status} 重复");
        }
    }

    [Fact(DisplayName = "CommandStatus共计5个状态")]
    public void CommandStatus_Count_IsFive()
    {
        var count = Enum.GetValues(typeof(CommandStatus)).Length;
        Assert.Equal(5, count);
    }

    [Fact(DisplayName = "CommandStatus_旧值不受影响")]
    public void CommandStatus_OldValues_Unchanged()
    {
        Assert.Equal(0, (Int32)CommandStatus.就绪);
        Assert.Equal(1, (Int32)CommandStatus.处理中);
        Assert.Equal(2, (Int32)CommandStatus.已完成);
        Assert.Equal(3, (Int32)CommandStatus.取消);
        Assert.Equal(4, (Int32)CommandStatus.错误);
    }
    #endregion

    #region CommandReplyModel字段
    [Fact(DisplayName = "CommandReplyModel字段读写")]
    public void CommandReplyModel_Fields_ReadWrite()
    {
        var model = new CommandReplyModel
        {
            Id = 456,
            Status = CommandStatus.已完成,
            Data = "ok"
        };

        Assert.Equal(456, model.Id);
        Assert.Equal(CommandStatus.已完成, model.Status);
        Assert.Equal("ok", model.Data);
    }
    #endregion
}
