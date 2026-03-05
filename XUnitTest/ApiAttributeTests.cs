using System;
using System.ComponentModel;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

public class ApiAttributeTests
{
    [Fact]
    [DisplayName("构造函数设置Name")]
    public void Constructor_SetsName()
    {
        var attr = new ApiAttribute("test/action");

        Assert.Equal("test/action", attr.Name);
    }

    [Fact]
    [DisplayName("空名称")]
    public void Constructor_EmptyName()
    {
        var attr = new ApiAttribute("");

        Assert.Equal("", attr.Name);
    }

    [Fact]
    [DisplayName("Name属性可读写")]
    public void Name_CanBeSetAndGet()
    {
        var attr = new ApiAttribute("initial");
        attr.Name = "modified";

        Assert.Equal("modified", attr.Name);
    }

    [Fact]
    [DisplayName("验证AttributeUsage")]
    public void AttributeUsage_AllowsClassAndMethod()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(ApiAttribute), typeof(AttributeUsageAttribute))!;

        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Class));
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Method));
        Assert.False(usage.AllowMultiple);
    }
}
