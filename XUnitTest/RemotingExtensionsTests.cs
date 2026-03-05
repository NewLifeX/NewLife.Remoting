using System;
using System.ComponentModel;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

public class RemotingExtensionsTests
{
    private MachineInfo GetMachineInfo()
    {
        return MachineInfo.Current ?? MachineInfo.RegisterAsync().Result;
    }

    [Fact]
    [DisplayName("BuildCode_MD5公式")]
    public void BuildCode_MD5()
    {
        var mi = GetMachineInfo();

        var code = mi.BuildCode("MD5({UUID}@{Guid}@{Serial}@{DiskID}@{Macs})");

        Assert.NotNull(code);
        Assert.NotEmpty(code);
        // MD5返回16字节 = 32字符十六进制
        Assert.Equal(32, code!.Length);
    }

    [Fact]
    [DisplayName("BuildCode_CRC公式")]
    public void BuildCode_CRC()
    {
        var mi = GetMachineInfo();

        var code = mi.BuildCode("CRC({UUID})");

        Assert.NotNull(code);
        Assert.NotEmpty(code);
    }

    [Fact]
    [DisplayName("BuildCode_CRC16公式")]
    public void BuildCode_CRC16()
    {
        var mi = GetMachineInfo();

        var code = mi.BuildCode("CRC16({UUID})");

        Assert.NotNull(code);
        Assert.NotEmpty(code);
    }

    [Fact]
    [DisplayName("BuildCode_MD5_16公式")]
    public void BuildCode_MD5_16()
    {
        var mi = GetMachineInfo();

        var code = mi.BuildCode("MD5_16({UUID})");

        Assert.NotNull(code);
        Assert.NotEmpty(code);
    }

    [Fact]
    [DisplayName("BuildCode空公式抛异常")]
    public void BuildCode_EmptyFormula_Throws()
    {
        var mi = GetMachineInfo();

        Assert.Throws<ArgumentNullException>(() => mi.BuildCode(""));
    }

    [Fact]
    [DisplayName("BuildCode无效公式格式")]
    public void BuildCode_InvalidFormat_Throws()
    {
        var mi = GetMachineInfo();

        Assert.Throws<ArgumentOutOfRangeException>(() => mi.BuildCode("InvalidFormula"));
    }

    [Fact]
    [DisplayName("BuildCode相同输入相同输出")]
    public void BuildCode_SameInput_SameOutput()
    {
        var mi = GetMachineInfo();
        var formula = "MD5({UUID}@{Guid})";

        var code1 = mi.BuildCode(formula);
        var code2 = mi.BuildCode(formula);

        Assert.Equal(code1, code2);
    }

    [Fact]
    [DisplayName("BuildCode使用Macs变量")]
    public void BuildCode_WithMacs()
    {
        var mi = GetMachineInfo();

        var code = mi.BuildCode("MD5({Macs})");

        Assert.NotNull(code);
        Assert.NotEmpty(code);
    }

    [Fact]
    [DisplayName("BuildCode未知算法直接返回十六进制")]
    public void BuildCode_UnknownAlgorithm()
    {
        var mi = GetMachineInfo();

        var code = mi.BuildCode("UNKNOWN({UUID})");

        // 未知算法不做任何转换，直接将buf转为Hex
        Assert.NotNull(code);
    }
}
