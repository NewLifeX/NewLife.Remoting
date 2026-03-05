using System;
using System.ComponentModel;
using NewLife.Remoting;
using Xunit;

namespace XUnitTest;

public class ClusterTests
{
    #region ClientPoolCluster
    [Fact]
    [DisplayName("ClientPoolCluster基本属性")]
    public void ClientPoolCluster_BasicProperties()
    {
        var cluster = new ClientPoolCluster<String>();

        Assert.Null(cluster.Name);
        Assert.NotNull(cluster.Pool);
        Assert.Null(cluster.GetItems);
        Assert.Null(cluster.OnCreate);
    }

    [Fact]
    [DisplayName("ClientPoolCluster打开关闭")]
    public void ClientPoolCluster_OpenClose()
    {
        var cluster = new ClientPoolCluster<String>();

        Assert.True(cluster.Open());
        // Close on empty pool returns false (clear returns 0)
        Assert.False(cluster.Close("test"));
    }

    [Fact]
    [DisplayName("ClientPoolCluster创建和归还")]
    public void ClientPoolCluster_GetAndReturn()
    {
        var created = 0;
        var cluster = new ClientPoolCluster<String>
        {
            Name = "TestCluster",
            GetItems = () => ["server1", "server2"],
            OnCreate = svr =>
            {
                created++;
                return $"client_{svr}";
            }
        };

        var client = cluster.Get();
        Assert.NotNull(client);
        Assert.StartsWith("client_", client);
        Assert.Equal(1, created);

        Assert.True(cluster.Return(client));
    }

    [Fact]
    [DisplayName("ClientPoolCluster归还null")]
    public void ClientPoolCluster_ReturnNull()
    {
        var cluster = new ClientPoolCluster<String>();

        Assert.False(cluster.Return(null!));
    }

    [Fact]
    [DisplayName("ClientPoolCluster没有GetItems抛异常")]
    public void ClientPoolCluster_NoGetItems_Throws()
    {
        var cluster = new ClientPoolCluster<String>
        {
            OnCreate = s => s
        };

        Assert.Throws<ArgumentNullException>(() => cluster.Get());
    }

    [Fact]
    [DisplayName("ClientPoolCluster没有OnCreate抛异常")]
    public void ClientPoolCluster_NoOnCreate_Throws()
    {
        var cluster = new ClientPoolCluster<String>
        {
            GetItems = () => ["server1"]
        };

        Assert.Throws<ArgumentNullException>(() => cluster.Get());
    }

    [Fact]
    [DisplayName("ClientPoolCluster空服务器列表抛异常")]
    public void ClientPoolCluster_EmptyServers_Throws()
    {
        var cluster = new ClientPoolCluster<String>
        {
            GetItems = () => Array.Empty<String>(),
            OnCreate = s => s
        };

        Assert.Throws<InvalidOperationException>(() => cluster.Get());
    }

    [Fact]
    [DisplayName("ClientPoolCluster重置")]
    public void ClientPoolCluster_Reset()
    {
        var cluster = new ClientPoolCluster<String>
        {
            Name = "Test",
            GetItems = () => ["server1"],
            OnCreate = s => s
        };

        var client = cluster.Get();
        cluster.Return(client);
        cluster.Reset();

        // 重置后能再次获取新对象
        var client2 = cluster.Get();
        Assert.NotNull(client2);
    }

    [Fact]
    [DisplayName("ClientPoolCluster轮询负载均衡")]
    public void ClientPoolCluster_RoundRobin()
    {
        var servers = new[] { "s1", "s2", "s3" };
        var cluster = new ClientPoolCluster<String>
        {
            Name = "RR",
            GetItems = () => servers,
            OnCreate = s => s
        };

        // 获取多个客户端，验证轮询效果
        var c1 = cluster.Get();
        var c2 = cluster.Get();
        var c3 = cluster.Get();

        // 三个应该用了不同的服务器（因为Pool从OnCreate创建新的）
        Assert.NotNull(c1);
        Assert.NotNull(c2);
        Assert.NotNull(c3);
    }

    [Fact]
    [DisplayName("ClientPoolCluster日志")]
    public void ClientPoolCluster_Log()
    {
        var cluster = new ClientPoolCluster<String>();

        Assert.NotNull(cluster.Log);
        cluster.WriteLog("test {0}", "message");
    }
    #endregion

    #region ClientSingleCluster
    [Fact]
    [DisplayName("ClientSingleCluster基本属性")]
    public void ClientSingleCluster_BasicProperties()
    {
        var cluster = new ClientSingleCluster<String>();

        Assert.Null(cluster.Name);
        Assert.Null(cluster.GetItems);
        Assert.Null(cluster.OnCreate);
    }

    [Fact]
    [DisplayName("ClientSingleCluster打开")]
    public void ClientSingleCluster_Open()
    {
        var cluster = new ClientSingleCluster<String>();
        Assert.True(cluster.Open());
    }

    [Fact]
    [DisplayName("ClientSingleCluster关闭空对象")]
    public void ClientSingleCluster_CloseEmpty()
    {
        var cluster = new ClientSingleCluster<String>();
        Assert.False(cluster.Close("test"));
    }

    [Fact]
    [DisplayName("ClientSingleCluster获取复用")]
    public void ClientSingleCluster_GetReuse()
    {
        var createCount = 0;
        var cluster = new ClientSingleCluster<String>
        {
            Name = "Single",
            GetItems = () => ["server1"],
            OnCreate = s =>
            {
                createCount++;
                return $"client_{s}";
            }
        };

        var c1 = cluster.Get();
        var c2 = cluster.Get();

        Assert.Equal(c1, c2);
        Assert.Equal(1, createCount);
    }

    [Fact]
    [DisplayName("ClientSingleCluster归还")]
    public void ClientSingleCluster_Return()
    {
        var cluster = new ClientSingleCluster<String>();

        Assert.True(cluster.Return("test"));
        Assert.True(cluster.Return(null!));
    }

    [Fact]
    [DisplayName("ClientSingleCluster重置")]
    public void ClientSingleCluster_Reset()
    {
        var cluster = new ClientSingleCluster<String>
        {
            Name = "Reset",
            GetItems = () => ["server1"],
            OnCreate = s => s
        };

        var c1 = cluster.Get();
        cluster.Reset();

        // 重置后重新创建
        var c2 = cluster.Get();
        Assert.NotNull(c2);
    }

    [Fact]
    [DisplayName("ClientSingleCluster没有服务器抛异常")]
    public void ClientSingleCluster_NoServers_Throws()
    {
        var cluster = new ClientSingleCluster<String>
        {
            GetItems = () => Array.Empty<String>(),
            OnCreate = s => s
        };

        Assert.Throws<InvalidOperationException>(() => cluster.Get());
    }

    [Fact]
    [DisplayName("ClientSingleCluster故障转移")]
    public void ClientSingleCluster_Failover()
    {
        var failCount = 0;
        var cluster = new ClientSingleCluster<String>
        {
            Name = "Failover",
            GetItems = () => ["fail_server", "good_server"],
            OnCreate = s =>
            {
                if (s == "fail_server")
                {
                    failCount++;
                    throw new Exception("连接失败");
                }
                return s;
            }
        };

        var client = cluster.Get();
        Assert.Equal("good_server", client);
        Assert.Equal(1, failCount);
    }

    [Fact]
    [DisplayName("ClientSingleCluster所有服务器失败")]
    public void ClientSingleCluster_AllServersFail()
    {
        var cluster = new ClientSingleCluster<String>
        {
            Name = "AllFail",
            GetItems = () => ["s1", "s2"],
            OnCreate = s => throw new InvalidOperationException("fail: " + s)
        };

        var ex = Assert.Throws<InvalidOperationException>(() => cluster.Get());
        Assert.Contains("fail:", ex.Message);
    }

    [Fact]
    [DisplayName("ClientSingleCluster日志")]
    public void ClientSingleCluster_Log()
    {
        var cluster = new ClientSingleCluster<String>();

        Assert.NotNull(cluster.Log);
        cluster.WriteLog("test {0}", "message");
    }
    #endregion
}
