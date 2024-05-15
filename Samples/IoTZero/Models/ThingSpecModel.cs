using NewLife.IoT.ThingSpecification;

namespace NewLife.IoT.Models;

/// <summary>物模型上报</summary>
public class ThingSpecModel
{
    /// <summary>设备编码</summary>
    public String DeviceCode { get; set; }

    /// <summary>物模型</summary>
    public ThingSpec Spec { get; set; }
}
