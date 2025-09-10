using IoT.Data;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using XCode.DataAccessLayer;
using XCode.Shards;

namespace IoTZero.Services;

/// <summary>分表管理</summary>
public class ShardTableService : IHostedService
{
    private readonly IoTSetting _setting;
    private readonly ITracer _tracer;
    private TimerX _timer;

    /// <summary>
    /// 实例化分表管理服务
    /// </summary>
    /// <param name="setting"></param>
    /// <param name="tracer"></param>
    public ShardTableService(IoTSetting setting, ITracer tracer)
    {
        _setting = setting;
        _tracer = tracer;
    }

    /// <summary>
    /// 开始服务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 每小时执行
        _timer = new TimerX(DoShardTable, null, 5_000, 3600 * 1000) { Async = true };

        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    private void DoShardTable(Object state)
    {
        var set = _setting;
        if (set.DataRetention <= 0) return;

        // 保留数据的起点
        var today = DateTime.Today;
        var endday = today.AddDays(-set.DataRetention);

        XTrace.WriteLine("检查数据分表，保留数据起始日期：{0:yyyy-MM-dd}", endday);

        using var span = _tracer?.NewSpan("ShardTable", $"{endday.ToFullString()}");
        try
        {
            // 所有表
            var dal = DeviceData.Meta.Session.Dal;
            var tnames = dal.Tables.Select(e => e.TableName).ToArray();
            var policy = DeviceData.Meta.ShardPolicy as TimeShardPolicy;

            // 删除旧数据
            for (var dt = today.AddYears(-1); dt < endday; dt = dt.AddDays(1))
            {
                var name = policy.Shard(dt).TableName;
                if (name.EqualIgnoreCase(tnames))
                {
                    try
                    {
                        dal.Execute($"Drop Table {name}");
                    }
                    catch { }
                }
            }

            // 新建今天明天的表
            var ts = new List<IDataTable>();
            {
                var table = DeviceData.Meta.Table.DataTable.Clone() as IDataTable;
                table.TableName = policy.Shard(today).TableName;
                ts.Add(table);
            }
            {
                var table = DeviceData.Meta.Table.DataTable.Clone() as IDataTable;
                table.TableName = policy.Shard(today.AddDays(1)).TableName;
                ts.Add(table);
            }

            if (ts.Count > 0)
            {
                XTrace.WriteLine("创建或更新数据表[{0}]：{1}", ts.Count, ts.Join(",", e => e.TableName));

                //dal.SetTables(ts.ToArray());
                dal.Db.CreateMetaData().SetTables(Migration.On, ts.ToArray());
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }

        XTrace.WriteLine("检查数据表完成");
    }
}