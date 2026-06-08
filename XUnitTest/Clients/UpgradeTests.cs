using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using Xunit;

namespace XUnitTest.Clients;

public class UpgradeTests
{
    [Fact]
    [DisplayName("默认属性")]
    public void DefaultProperties()
    {
        var upgrade = new Upgrade();

        Assert.NotNull(upgrade.Name);
        Assert.Equal("Update", upgrade.UpdatePath);
        Assert.Equal(".", upgrade.DestinationPath);
        Assert.Null(upgrade.Url);
        Assert.Null(upgrade.SourceFile);
        Assert.Null(upgrade.TempPath);
        Assert.Equal(UpdateModes.Standard, upgrade.Mode);
        Assert.Equal(30, upgrade.DownloadTimeoutSeconds);
        // StorageFlushDelay 在不同平台上值不同，仅验证 >=0
        Assert.True(upgrade.StorageFlushDelay >= 0);
    }

    [Fact]
    [DisplayName("属性可写")]
    public void Properties_Writable()
    {
        var upgrade = new Upgrade
        {
            Name = "TestApp",
            UpdatePath = "MyUpdate",
            DestinationPath = "/opt/app",
            Url = "http://example.com/update.zip",
            SourceFile = "/tmp/update.zip",
            TempPath = "/tmp/extract",
            Mode = UpdateModes.Full,
            DownloadTimeoutSeconds = 600,
            StorageFlushDelay = 1000,
        };

        Assert.Equal("TestApp", upgrade.Name);
        Assert.Equal("MyUpdate", upgrade.UpdatePath);
        Assert.Equal("/opt/app", upgrade.DestinationPath);
        Assert.Equal("http://example.com/update.zip", upgrade.Url);
        Assert.Equal("/tmp/update.zip", upgrade.SourceFile);
        Assert.Equal("/tmp/extract", upgrade.TempPath);
        Assert.Equal(UpdateModes.Full, upgrade.Mode);
        Assert.Equal(600, upgrade.DownloadTimeoutSeconds);
        Assert.Equal(1000, upgrade.StorageFlushDelay);
    }

    [Fact]
    [DisplayName("CheckFileHash空hash")]
    public void CheckFileHash_EmptyHash()
    {
        var upgrade = new Upgrade();

        Assert.False(upgrade.CheckFileHash(""));
        Assert.False(upgrade.CheckFileHash(null!));
    }

    [Fact]
    [DisplayName("CheckFileHash无源文件")]
    public void CheckFileHash_NoSourceFile()
    {
        var upgrade = new Upgrade();

        Assert.False(upgrade.CheckFileHash("abc123"));
    }

    [Fact]
    [DisplayName("CheckFileHash源文件不存在")]
    public void CheckFileHash_FileNotExist()
    {
        var upgrade = new Upgrade
        {
            SourceFile = "nonexistent_file.zip"
        };

        Assert.False(upgrade.CheckFileHash("abc123"));
    }

    [Fact]
    [DisplayName("Download空Url返回false")]
    public async Task Download_EmptyUrl()
    {
        var upgrade = new Upgrade();

        var result = await upgrade.Download();

        Assert.False(result);
    }

    [Fact]
    [DisplayName("Extract无源文件返回false")]
    public void Extract_NoSourceFile()
    {
        var upgrade = new Upgrade();

        Assert.False(upgrade.Extract());
    }

    [Fact]
    [DisplayName("Extract源文件不存在返回false")]
    public void Extract_FileNotExist()
    {
        var upgrade = new Upgrade
        {
            SourceFile = "nonexistent.zip"
        };

        Assert.False(upgrade.Extract());
    }

    [Fact]
    [DisplayName("Update空目标目录返回false")]
    public void Update_EmptyDestination()
    {
        var upgrade = new Upgrade
        {
            DestinationPath = null
        };

        Assert.False(upgrade.Update());
    }

    [Fact]
    [DisplayName("Update无临时目录返回false")]
    public void Update_NoTempPath()
    {
        var upgrade = new Upgrade
        {
            DestinationPath = ".",
            TempPath = null
        };

        Assert.False(upgrade.Update());
    }

    [Fact]
    [DisplayName("ResolveDotNetPath返回非空字符串")]
    public void ResolveDotNetPath_ReturnsNonEmpty()
    {
        var path = Upgrade.ResolveDotNetPath();

        Assert.NotNull(path);
        Assert.NotEmpty(path);
    }

    [Fact]
    [DisplayName("DownloadTimeoutSeconds默认值30")]
    public void DownloadTimeoutSeconds_Default()
    {
        var upgrade = new Upgrade();
        Assert.Equal(30, upgrade.DownloadTimeoutSeconds);
    }

    [Fact]
    [DisplayName("UpdateModes默认值为Standard")]
    public void UpdateModes_Default()
    {
        var upgrade = new Upgrade();
        Assert.Equal(UpdateModes.Standard, upgrade.Mode);
    }

    [Fact]
    [DisplayName("UpdateModes所有枚举值可设置")]
    public void UpdateModes_AllValues()
    {
        foreach (UpdateModes mode in Enum.GetValues(typeof(UpdateModes)))
        {
            var upgrade = new Upgrade { Mode = mode };
            Assert.Equal(mode, upgrade.Mode);
        }
    }

    [Fact]
    [DisplayName("部分包模式下Update不移动已有文件")]
    public void Update_PartialMode_DoesNotMoveExisting()
    {
        // 创建临时目录结构
        var destDir = Path.Combine(Path.GetTempPath(), "UpgradeTest_" + Guid.NewGuid().ToString("N"));
        var tmpDir = Path.Combine(Path.GetTempPath(), "UpgradeTest_Tmp_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(destDir);
            Directory.CreateDirectory(tmpDir);

            // 在目标目录创建模拟 exe 和 dll
            var testExe = Path.Combine(destDir, "test.exe");
            var testDll = Path.Combine(destDir, "test.dll");
            File.WriteAllText(testExe, "old");
            File.WriteAllText(testDll, "old");

            // 在临时目录创建新文件
            File.WriteAllText(Path.Combine(tmpDir, "newfile.txt"), "new");

            var upgrade = new Upgrade
            {
                DestinationPath = destDir,
                TempPath = tmpDir,
                Mode = UpdateModes.Partial,
            };

            var result = upgrade.Update();

            Assert.True(result);
            // 原文件不应该被移走（没有被改为 .del）
            Assert.True(File.Exists(testExe));
            Assert.True(File.Exists(testDll));
            Assert.False(File.Exists(testExe + ".del"));
            Assert.False(File.Exists(testDll + ".del"));
            // 新文件已拷贝
            Assert.True(File.Exists(Path.Combine(destDir, "newfile.txt")));
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    [DisplayName("标准包模式下Update将exe和dll移为del")]
    public void Update_StandardMode_MovesExeAndDll()
    {
        var destDir = Path.Combine(Path.GetTempPath(), "UpgradeTest_" + Guid.NewGuid().ToString("N"));
        var tmpDir = Path.Combine(Path.GetTempPath(), "UpgradeTest_Tmp_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(destDir);
            Directory.CreateDirectory(tmpDir);

            var testExe = Path.Combine(destDir, "test.exe");
            var testDll = Path.Combine(destDir, "test.dll");
            var testTxt = Path.Combine(destDir, "data.txt");
            File.WriteAllText(testExe, "old");
            File.WriteAllText(testDll, "old");
            File.WriteAllText(testTxt, "data");

            File.WriteAllText(Path.Combine(tmpDir, "newfile.txt"), "new");

            var upgrade = new Upgrade
            {
                DestinationPath = destDir,
                TempPath = tmpDir,
                Mode = UpdateModes.Standard,
            };

            var result = upgrade.Update();

            Assert.True(result);
            // exe/dll 被移走（变为 .del）
            Assert.True(File.Exists(testExe + ".del") || !File.Exists(testExe));
            Assert.True(File.Exists(testDll + ".del") || !File.Exists(testDll));
            // 非可执行文件保留
            Assert.True(File.Exists(testTxt));
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    [DisplayName("完整包模式下Update清空目标目录但保留配置")]
    public void Update_FullMode_CleansDestination()
    {
        var destDir = Path.Combine(Path.GetTempPath(), "UpgradeTest_" + Guid.NewGuid().ToString("N"));
        var tmpDir = Path.Combine(Path.GetTempPath(), "UpgradeTest_Tmp_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(destDir);
            Directory.CreateDirectory(tmpDir);

            File.WriteAllText(Path.Combine(destDir, "test.exe"), "old");
            File.WriteAllText(Path.Combine(destDir, "test.dll"), "old");
            File.WriteAllText(Path.Combine(destDir, "appsettings.json"), "config");
            File.WriteAllText(Path.Combine(tmpDir, "newfile.txt"), "new");

            var upgrade = new Upgrade
            {
                DestinationPath = destDir,
                TempPath = tmpDir,
                Mode = UpdateModes.Full,
            };

            var result = upgrade.Update();

            Assert.True(result);
            // 配置文件保留
            Assert.True(File.Exists(Path.Combine(destDir, "appsettings.json")));
            // 新文件已拷贝
            Assert.True(File.Exists(Path.Combine(destDir, "newfile.txt")));
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    [DisplayName("Rollback恢复del文件")]
    public void Rollback_RestoresDelFiles()
    {
        var destDir = Path.Combine(Path.GetTempPath(), "UpgradeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(destDir);

            // 模拟标准模式：创建 .dll.del 和 .exe.del 备份文件
            var testExeDel = Path.Combine(destDir, "test.exe.del");
            var testDllDel = Path.Combine(destDir, "test.dll.del");
            File.WriteAllText(testExeDel, "backup");
            File.WriteAllText(testDllDel, "backup");

            var upgrade = new Upgrade { DestinationPath = destDir };
            var count = upgrade.Rollback();

            Assert.Equal(2, count);
            // 原始文件已恢复
            Assert.True(File.Exists(Path.Combine(destDir, "test.exe")));
            Assert.True(File.Exists(Path.Combine(destDir, "test.dll")));
            // .del 备份文件已消失
            Assert.False(File.Exists(testExeDel));
            Assert.False(File.Exists(testDllDel));
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    [DisplayName("Rollback恢复时间戳变体del文件")]
    public void Rollback_RestoresTimestampedDelFiles()
    {
        var destDir = Path.Combine(Path.GetTempPath(), "UpgradeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(destDir);

            // 模拟时间戳变体：test.dll → 已存在 .del 所以生成 test.dll_260608153000.del
            var delFile = Path.Combine(destDir, "test.dll_260608153000.del");
            File.WriteAllText(delFile, "backup");

            var upgrade = new Upgrade { DestinationPath = destDir };
            var count = upgrade.Rollback();

            Assert.Equal(1, count);
            // 原始文件已恢复（去掉 _12位时间戳.del）
            Assert.True(File.Exists(Path.Combine(destDir, "test.dll")));
            Assert.False(File.Exists(delFile));
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    [DisplayName("Rollback空目录返回0")]
    public void Rollback_EmptyDir()
    {
        var destDir = Path.Combine(Path.GetTempPath(), "UpgradeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(destDir);

            var upgrade = new Upgrade { DestinationPath = destDir };
            var count = upgrade.Rollback();

            Assert.Equal(0, count);
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }
}
