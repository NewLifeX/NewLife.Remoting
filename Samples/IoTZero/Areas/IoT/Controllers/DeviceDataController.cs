using IoT.Data;
using NewLife;
using NewLife.Algorithms;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Web;
using XCode;
using XCode.Membership;

namespace IoTZero.Areas.IoT.Controllers;

[IoTArea]
[Menu(0, false)]
public class DeviceDataController : EntityController<DeviceData>
{
    static DeviceDataController()
    {
        ListFields.RemoveField("Id");
        ListFields.AddListField("Value", null, "Kind");

        {
            var df = ListFields.GetField("Name") as ListField;
            //df.DisplayName = "主题";
            df.Url = "/IoT/DeviceData?deviceId={DeviceId}&name={Name}";
        }
        ListFields.TraceUrl("TraceId");
    }

    protected override IEnumerable<DeviceData> Search(Pager p)
    {
        var deviceId = p["deviceId"].ToInt(-1);
        var name = p["name"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        if (start.Year < 2000)
        {
            start = DateTime.Today;
            p["dtStart"] = start.ToString("yyyy-MM-dd");
            p["dtEnd"] = start.ToString("yyyy-MM-dd");
        }

        if (deviceId > 0 && p.PageSize == 20 && !name.IsNullOrEmpty() && !name.StartsWithIgnoreCase("raw-", "channel-")) p.PageSize = 14400;

        var list = DeviceData.Search(deviceId, name, start, end, p["Q"], p);

        // 单一设备绘制曲线
        if (list.Count > 0 && deviceId > 0)
        {
            var list2 = list.Where(e => !e.Name.StartsWithIgnoreCase("raw-", "channel-") && e.Value.ToDouble(-1) >= 0).OrderBy(e => e.Id).ToList();

            // 绘制曲线图
            if (list2.Count > 0)
            {
                var topics = list2.Select(e => e.Name).Distinct().ToList();
                var datax = list2.GroupBy(e => e.CreateTime).ToDictionary(e => e.Key, e => e.ToList());
                //var topics = list2.GroupBy(e => e.Topic).ToDictionary(e => e.Key, e => e.ToList());
                var chart = new ECharts
                {
                    Height = 400,
                };
                //chart.SetX(list2, _.CreateTime, e => e.CreateTime.ToString("mm:ss"));

                // 构建X轴
                var minT = datax.Keys.Min();
                var maxT = datax.Keys.Max();
                var step = p["sample"].ToInt(-1);
                if (step > 0)
                {
                    if (step <= 60)
                    {
                        minT = new DateTime(minT.Year, minT.Month, minT.Day, minT.Hour, minT.Minute, 0, minT.Kind);
                        maxT = new DateTime(maxT.Year, maxT.Month, maxT.Day, maxT.Hour, maxT.Minute, 0, maxT.Kind);
                    }
                    else
                    {
                        minT = new DateTime(minT.Year, minT.Month, minT.Day, minT.Hour, 0, 0, minT.Kind);
                        maxT = new DateTime(maxT.Year, maxT.Month, maxT.Day, maxT.Hour, 0, 0, maxT.Kind);
                        //step = 3600;
                    }
                    var times = new List<DateTime>();
                    for (var dt = minT; dt <= maxT; dt = dt.AddSeconds(step))
                    {
                        times.Add(dt);
                    }

                    if (step < 60)
                    {
                        chart.XAxis = [new XAxis
                        {
                            Data = times.Select(e => e.ToString("HH:mm:ss")).ToArray(),
                        }];
                    }
                    else
                    {
                        chart.XAxis = [new XAxis
                        {
                            Data = times.Select(e => e.ToString("dd-HH:mm")).ToArray(),
                        }];
                    }
                }
                else
                {
                    chart.XAxis = [new XAxis
                    {
                        Data = datax.Keys.Select(e => e.ToString("HH:mm:ss")).ToArray(),
                    }];
                }
                chart.SetY("数值");

                var max = -9999.0;
                var min = 9999.0;
                var dps = DeviceProperty.FindAllByDeviceId(deviceId);
                var sample = new AverageSampling();
                //var sample = new LTTBSampling();
                foreach (var item in topics)
                {
                    var name2 = item;

                    // 使用属性名
                    var dp = dps.FirstOrDefault(e => e.Name == item);
                    if (dp != null && !dp.NickName.IsNullOrEmpty()) name2 = dp.NickName;

                    var series = new SeriesLine
                    {
                        Name = name2,
                        Type = "line",
                        //Data = tps2.Select(e => Math.Round(e.Value)).ToArray(),
                        Smooth = true,
                    };

                    if (step > 0)
                    {
                        //var minD = minT.Date.ToInt();
                        var tps = new List<TimePoint>();
                        foreach (var elm in datax)
                        {
                            // 可能该Topic在这个时刻没有数据，写入空
                            var v = elm.Value.FirstOrDefault(e => e.Name == item);
                            if (v != null)
                                tps.Add(new TimePoint { Time = v.CreateTime.ToInt(), Value = v.Value.ToDouble() });
                        }

                        var tps2 = sample.Process(tps.ToArray(), step);

                        series.Data = tps2.Select(e => (Object)Math.Round(e.Value, 2)).ToArray();

                        var m1 = tps2.Select(e => e.Value).Min();
                        if (m1 < min) min = m1;
                        var m2 = tps2.Select(e => e.Value).Max();
                        if (m2 > max) max = m2;
                    }
                    else
                    {
                        var list3 = new List<Object>();
                        foreach (var elm in datax)
                        {
                            // 可能该Topic在这个时刻没有数据，写入空
                            var v = elm.Value.FirstOrDefault(e => e.Name == item);
                            if (v != null)
                                list3.Add(v.Value);
                            else
                                list3.Add('-');
                        }
                        series.Data = list3.ToArray();

                        var m1 = list3.Where(e => e + "" != "-").Select(e => e.ToDouble()).Min();
                        if (m1 < min) min = m1;
                        var m2 = list3.Where(e => e + "" != "-").Select(e => e.ToDouble()).Max();
                        if (m2 > max) max = m2;
                    }

                    // 单一曲线，显示最大最小和平均
                    if (topics.Count == 1)
                    {
                        name = name2;
                        series["markPoint"] = new
                        {
                            data = new[] {
                                new{ type="max",name="Max"},
                                new{ type="min",name="Min"},
                            }
                        };
                        series["markLine"] = new
                        {
                            data = new[] {
                                new{ type="average",name="Avg"},
                            }
                        };
                    }

                    // 降采样策略 lttb/average/max/min/sum
                    series["sampling"] = "lttb";
                    series["symbol"] = "none";

                    // 开启动画
                    series["animation"] = true;

                    chart.Add(series);
                }
                chart.SetTooltip();
                chart.YAxis = [new YAxis
                {
                    Name = "数值",
                    Type = "value",
                    Min = Math.Ceiling(min) - 1,
                    Max = Math.Ceiling(max),
                }];
                ViewBag.Charts = new[] { chart };

                // 减少数据显示，避免卡页面
                list = list.Take(100).ToList();

                var ar = Device.FindById(deviceId);
                if (ar != null) ViewBag.Title = topics.Count == 1 ? $"{name} - {ar}数据" : $"{ar}数据";
            }
        }

        return list;
    }
}