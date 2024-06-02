using IoT.Data;
using NewLife.Cube;
using NewLife.Web;

namespace IoTZero.Areas.IoT.Controllers;

[IoTArea]
[Menu(30, true, Icon = "fa-product-hunt")]
public class ProductController : EntityController<Product>
{
    static ProductController()
    {
        LogOnChange = true;

        ListFields.RemoveField("Secret", "DataFormat", "DynamicRegister", "FixedDeviceCode", "AuthType", "WhiteIP", "Remark");
        ListFields.RemoveCreateField();

        {
            var df = ListFields.AddListField("Log");
            df.DisplayName = "日志";
            df.Url = "/Admin/Log?category=产品&linkId={Id}";
        }
    }

    protected override IEnumerable<Product> Search(Pager p)
    {
        var id = p["Id"].ToInt(-1);
        if (id > 0)
        {
            var entity = Product.FindById(id);
            if (entity != null) return new[] { entity };
        }

        var code = p["code"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return Product.Search(code, start, end, p["Q"], p);
    }
}