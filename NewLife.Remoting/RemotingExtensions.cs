using NewLife.Log;

namespace NewLife.Remoting;

/// <summary>扩展方法</summary>
public static class RemotingExtensions
{
    /// <summary>生成设备代码</summary>
    /// <param name="machineInfo"></param>
    /// <param name="formula"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static String? BuildCode(this MachineInfo machineInfo, String formula = "MD5({UUID}@{Guid}@{Serial}@{DiskID}@{Macs})")
    {
        if (formula.IsNullOrEmpty()) throw new ArgumentNullException(nameof(formula));

        var ss = (formula + "").Split('(', ')');
        if (ss.Length < 2) throw new ArgumentOutOfRangeException(nameof(formula));

        var uid = ss[1];
        foreach (var pi in machineInfo.GetType().GetProperties())
        {
            var name = $"{{{pi.Name}}}";
            if (uid.Contains(name))
                uid = uid.Replace(name, pi.GetValue(machineInfo, null) + "");
        }

        {
            var name = "{Macs}";
            if (uid.Contains(name))
            {
                var mcs = NetHelper.GetMacs().Select(e => e.ToHex("-")).OrderBy(e => e).Join(",");
                uid = uid.Replace(name, mcs);
            }
        }

        if (uid.Contains('{') || uid.Contains('}')) XTrace.WriteLine("编码公式有误，存在未解析变量，uid={0}", uid);
        if (!uid.IsNullOrEmpty())
        {
            // 使用产品类别加密一下，确保不同类别有不同编码
            var buf = uid.GetBytes();
            switch (ss[0].ToLower())
            {
                case "crc": buf = buf.Crc().GetBytes(); break;
                case "crc16": buf = buf.Crc16().GetBytes(); break;
                case "md5": buf = buf.MD5(); break;
                case "md5_16": buf = uid.MD5_16().ToHex(); break;
                default:
                    break;
            }

            return buf.ToHex();
        }

        return null;
    }
}
