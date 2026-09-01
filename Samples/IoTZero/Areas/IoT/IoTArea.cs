using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace IoTZero.Areas.IoT;

[DisplayName("设备管理")]
public class IoTArea : AreaBase
{
    public IoTArea() : base(nameof(IoTArea).TrimSuffix("Area")) { }

    static IoTArea() => RegisterArea<IoTArea>();
}