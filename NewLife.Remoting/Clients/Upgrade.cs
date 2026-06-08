using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using NewLife.Log;
using NewLife.Remoting.Models;
#if !NET40
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace NewLife.Remoting.Clients;

/// <summary>升级更新</summary>
/// <remarks>
/// 自动更新的难点在于覆盖正在使用的exe/dll文件，通过改名可以解决。
/// </remarks>
public class Upgrade
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>更新目录。默认./Update</summary>
    public String? UpdatePath { get; set; } = "Update";

    /// <summary>目标目录</summary>
    public String? DestinationPath { get; set; } = ".";

    /// <summary>源文件下载地址</summary>
    public String? Url { get; set; }

    /// <summary>更新源文件</summary>
    public String? SourceFile { get; set; }

    /// <summary>解压缩的临时目录</summary>
    public String? TempPath { get; set; }

    /// <summary>更新模式</summary>
    public UpdateModes Mode { get; set; } = UpdateModes.Standard;

    /// <summary>下载超时时间（秒）。默认30秒</summary>
    public Int32 DownloadTimeoutSeconds { get; set; } = 30;

    /// <summary>落盘等待延迟（毫秒）。sync 后等待存储设备完成回写。Linux ARM64 慢速存储（eMMC/SD）默认 800ms，x86 默认 0</summary>
    public Int32 StorageFlushDelay { get; set; } = GetDefaultFlushDelay();

    private static Int32 GetDefaultFlushDelay()
    {
#if NETCOREAPP || NETSTANDARD2_0_OR_GREATER
        if (Runtime.Linux && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
            return 800;
#endif
        return 0;
    }
    #endregion

    #region 最后错误
    /// <summary>最后一次启动进程的错误消息，用于外部诊断。在 systemd 等隔离环境下，Process.Start 失败的原因无法直接通过返回布尔值表达，此属性可将 catch 的异常详情透传给调用方，最终输出到节点历史（WriteInfoEvent）</summary>
    public String? LastErrorMessage { get; private set; }
    #endregion

    #region 构造
    /// <summary>实例化一个升级对象实例，获取当前应用信息</summary>
    public Upgrade()
    {
        var asm = Assembly.GetEntryAssembly();
        Name = asm?.GetName().Name ?? nameof(Upgrade);
    }
    #endregion

    #region 方法
    /// <summary>下载更新包，支持超时与瞬时失败重试</summary>
    public virtual async Task<Boolean> Download(CancellationToken cancellationToken = default)
    {
        var url = Url;
        if (url.IsNullOrEmpty()) return false;

        var fileName = Path.GetFileName(url);
        if (fileName.IsNullOrEmpty() || fileName.Contains('?')) fileName = "a.zip";

        // 即使更新包存在，也要下载
        var file = UpdatePath.CombinePath(fileName).GetBasePath();
        if (File.Exists(file)) File.Delete(file); ;

        WriteLog("准备下载 {0} 到 {1}", url, file);

        var sw = Stopwatch.StartNew();

        var web = CreateClient();

        // 瞬时网络故障重试两次（共三次尝试）
        var retry = 2;
        while (true)
        {
            try
            {
                file = await DownloadFileAsync(web, url, file, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (Exception ex) when (ex is IOException || ex is HttpRequestException)
            {
                if (retry-- <= 0) throw;
                WriteLog("下载失败，2秒后重试：{0}", ex.Message);
                await TaskEx.Delay(2_000, cancellationToken).ConfigureAwait(false);
            }
        }

        sw.Stop();
        WriteLog("下载完成！{2} 大小{0:n0}字节，耗时{1:n0}ms", file.AsFile().Length, sw.ElapsedMilliseconds, file);

        var md5 = file.AsFile().MD5().ToHex();
        WriteLog("MD5: {0}", md5);

        SourceFile = file;

        return true;
    }

    /// <summary>
    /// 检查文件散列，避免文件损坏
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public Boolean CheckFileHash(String hash)
    {
        if (hash.IsNullOrEmpty()) return false;

        var fi = SourceFile?.AsFile();
        if (fi == null || !fi.Exists) return false;

        var md5 = fi.MD5().ToHex();
        return md5.EqualIgnoreCase(hash);
    }

    /// <summary>解压缩</summary>
    /// <returns></returns>
    public virtual Boolean Extract()
    {
        var file = SourceFile;
        if (file.IsNullOrEmpty() || !File.Exists(file)) return false;

        WriteLog("发现更新包 {0}", file);

        // 解压更新程序包
        if (!file.EndsWithIgnoreCase(".zip")) return false;

        var tmp = TempPath;
        if (tmp.IsNullOrEmpty()) tmp = TempPath = Path.GetTempPath().CombinePath(Path.GetFileNameWithoutExtension(file));
        WriteLog("解压缩到临时目录 {0}", tmp);
        file.AsFile().Extract(tmp, true);

        return true;
    }

    /// <summary>执行更新，拷贝文件。按更新模式清理目标目录</summary>
    public virtual Boolean Update()
    {
        var dest = DestinationPath;
        if (dest.IsNullOrEmpty()) return false;

        // 删除备份文件
        DeleteBackup(dest);

        var tmp = TempPath;
        if (tmp.IsNullOrEmpty() || !Directory.Exists(tmp)) return false;

        WriteLog("发现更新源目录 {0}", tmp);

        // 记录移动文件，更新失败时恢复
        var dic = new Dictionary<String, String>();
        try
        {
            switch (Mode)
            {
                case UpdateModes.Full:
                    // 完整包：清空目标目录（保护配置、更新目录、备份和日志）
                    WriteLog("完整包模式，清空目标目录");
                    {
                        var protectedNames = new[] { "appsettings.json", "appsettings.*.json", "Update", "Log", "Config" };
                        foreach (var item in dest.AsDirectory().GetAllFiles(null, false))
                        {
                            if (!item.Name.EndsWithIgnoreCase(".del") && !protectedNames.Any(e => item.Name.EqualIgnoreCase(e)))
                            {
                                var ori = item.FullName;
                                var del = $"{ori}.del";
                                if (File.Exists(del)) del = $"{ori}_{DateTime.Now:yyMMddHHmmss}.del";
                                WriteLog("MoveTo {0}", del);
                                item.MoveTo(del);
                                dic[ori] = del;
                            }
                        }
                        // 递归清理子目录（保留 Update/ Log/ Config/）
                        foreach (var dir in dest.AsDirectory().GetDirectories())
                        {
                            if (protectedNames.Any(e => dir.Name.EqualIgnoreCase(e))) continue;
                            WriteLog("DeleteDir {0}", dir.FullName);
                            dir.Delete(true);
                        }
                    }
                    break;
                case UpdateModes.Partial:
                    // 部分包：不清理目标，直接覆盖
                    WriteLog("部分包模式，直接覆盖");
                    break;
                case UpdateModes.Standard:
                default:
                    //!!! 此处递归删除，导致也删掉了Update里面的文件
                    // 更新覆盖之前，需要把exe/dll可执行文件移走，否则Linux下覆盖运行中文件会报段错误
                    foreach (var item in dest.AsDirectory().GetAllFiles("*.exe;*.dll", false))
                    {
                        var ori = item.FullName;
                        var del = $"{item.FullName}.del";
                        WriteLog("MoveTo {0}", del);
                        try
                        {
                            //if (File.Exists(del)) File.Delete(del);
                            // 如果.del文件已存在，不能直接删，因为进程可能正在使用（上次升级未完成）。
                            if (File.Exists(del)) del = $"{item.FullName}_{DateTime.Now:yyMMddHHmmss}.del";
                            item.MoveTo(del);

                            dic[ori] = del;
                        }
                        catch (Exception ex)
                        {
                            WriteLog(ex.Message);

                            try
                            {
                                // 删除失败时，移动到临时目录随机文件
                                var target = Path.GetTempFileName();
                                item.MoveTo(target);

                                dic[ori] = target;
                            }
                            catch (Exception ex2)
                            {
                                WriteLog(ex2.Message);
                            }
                        }
                    }

                    // 备份将被覆盖的 JSON 文件（runtimeconfig.json / deps.json 等随 .NET 版本变化的文件），确保 Rollback 能完整恢复
                    {
                        var jsonPatterns = new[] { "*.runtimeconfig.json", "*.deps.json" };
                        foreach (var pattern in jsonPatterns)
                        {
                            foreach (var item in dest.AsDirectory().GetAllFiles(pattern, false))
                            {
                                // 跳过已在 exe/dll 环节备份的文件
                                if (dic.ContainsKey(item.FullName)) continue;

                                var ori = item.FullName;
                                var del = $"{ori}.del";
                                WriteLog("MoveTo {0}", del);
                                try
                                {
                                    if (File.Exists(del)) del = $"{ori}_{DateTime.Now:yyMMddHHmmss}.del";
                                    item.MoveTo(del);
                                    dic[ori] = del;
                                }
                                catch (Exception ex)
                                {
                                    WriteLog(ex.Message);
                                }
                            }
                        }
                    }
                    break;
            }

            // 拷贝替换更新
            CopyAndReplace(tmp, dest);

            //// 删除备份文件
            //DeleteBackup(DestinationPath);
            //!!! 先别急着删除，在Linux上，删除正在使用的文件可能导致进程崩溃

            // 强制 fsync 将页缓存提交到存储介质。同一启动周期内 exec() 通过页缓存读取文件是内核级一致的，
            // sync 的主要价值是防止系统崩溃/断电重启后读到半写入数据。
            if (Runtime.Linux)
            {
                Process.Start(new ProcessStartInfo("sync", "") { UseShellExecute = false })?.WaitForExit(10_000);

                // ARM64 慢速存储额外等待页缓存回写完成
                if (StorageFlushDelay > 0)
                {
                    WriteLog("等待落盘 {0}ms（慢速存储保护）", StorageFlushDelay);
                    Thread.Sleep(StorageFlushDelay);
                }
            }

            WriteLog("更新成功！");
        }
        catch
        {
            WriteLog("更新失败，恢复文件");
            Restore(dic);

            throw;
        }

        return true;
    }

    void Restore(IDictionary<String, String> dic)
    {
        foreach (var item in dic)
        {
            WriteLog("Restore {0}", item.Value);
            if (File.Exists(item.Value))
            {
                if (File.Exists(item.Key))
                {
                    WriteLog("Delete {0}", item.Key);

                    try
                    {
                        File.Delete(item.Key);
                    }
                    catch (Exception ex)
                    {
                        WriteLog(ex.Message);
                    }
                }

                try
                {
                    File.Move(item.Value, item.Key);
                }
                catch (Exception ex)
                {
                    WriteLog(ex.Message);
                }
            }
        }
    }

    /// <summary>回滚更新。将目标目录中备份的 .del 文件恢复为原始文件，用于新版本启动失败（冒烟测试不通过）时回退到旧版本</summary>
    /// <remarks>
    /// 设计理念：自更新最怕新版本有 bug 把自己"玩死"。
    /// 更新完成后旧进程尝试拉起新进程作为冒烟测试——若新版本在 msWait 内异常退出，
    /// 说明新版本存在致命缺陷，此时回滚所有 .del 恢复旧版文件，旧进程继续提供服务。
    /// 即使设备随后重启，加载的仍是经过验证的旧版本。
    /// </remarks>
    /// <returns>恢复的文件数量</returns>
    public Int32 Rollback()
    {
        var dest = DestinationPath;
        if (dest.IsNullOrEmpty()) return 0;

        var count = 0;
        // 扫描目标目录中的 .del 文件（含时间戳变体 xxx_yyMMddHHmmss.del）
        foreach (var item in dest.AsDirectory().GetAllFiles("*.del", false))
        {
            var delName = item.FullName;
            // 去掉 .del 后缀得到原始文件名
            var ori = delName.TrimSuffix(".del");

            // 时间戳变体：xxx_yyMMddHHmmss.del → 原始文件名为 xxx（去掉 _12位时间戳）
            var p = ori.LastIndexOf('_');
            if (p > 0)
            {
                var suffix = ori.Substring(p + 1);
                // 判断是否为 12 位纯数字时间戳（避免误判含下划线的正常文件名）
                if (suffix.Length == 12 && suffix.All(Char.IsDigit))
                    ori = ori.Substring(0, p);
            }

            WriteLog("Rollback {0} -> {1}", item.Name, Path.GetFileName(ori));
            try
            {
                if (File.Exists(ori)) File.Delete(ori);
                File.Move(delName, ori);
                count++;
            }
            catch (Exception ex)
            {
                WriteLog("回滚失败：{0}", ex.Message);
            }
        }

        if (count > 0) WriteLog("回滚完成，共恢复 {0} 个文件", count);

        return count;
    }

    /// <summary>启动当前应用的新进程。当前进程退出</summary>
    public Boolean Run(String name, String args) => Run(name, args, 3_000);

    /// <summary>启动当前应用的新进程</summary>
    /// <param name="name">应用名称（不含扩展名）。如 StarAgent</param>
    /// <param name="args">启动参数</param>
    /// <param name="msWait">等待毫秒数。若进程在此期间未退出视为启动成功</param>
    /// <returns>是否成功拉起</returns>
    public Boolean Run(String name, String args, Int32 msWait)
    {
        // 通过 runtimeconfig.json 判定是否为 .NET Core 应用（muxer 优先）
        var runtimeConfig = $"{name}.runtimeconfig.json".GetFullPath();
        var isNetCore = File.Exists(runtimeConfig);

        String fileName;
        String? dotnetPath = null;
        if (isNetCore)
        {
            // .NET Core 应用：使用 dotnet <name>.dll <args>，跨平台标准写法
            fileName = (name + ".dll").GetFullPath();
            if (!File.Exists(fileName)) throw new FileNotFoundException($"未找到 .NET 应用入口：{fileName}");

            dotnetPath = ResolveDotNetPath();
        }
        else
        {
            // 非 .NET Core 应用：按平台选择可执行文件
            if (Runtime.Windows || Runtime.Mono)
                fileName = (name + ".exe").GetFullPath();
            else if (Runtime.Linux)
                fileName = name.GetFullPath();
            else
                fileName = (name + ".dll").GetFullPath();

            // 如果入口文件不存在，则尝试 dll 启动（.NET Framework 也可能用 dotnet 启动）
            if (!File.Exists(fileName))
            {
                var dllFile = (name + ".dll").GetFullPath();
                if (File.Exists(dllFile))
                {
                    fileName = dllFile;
                    dotnetPath = ResolveDotNetPath();
                    isNetCore = true;
                }
            }

            if (!File.Exists(fileName)) throw new FileNotFoundException($"未找到可执行文件：{fileName}");
        }

        // Linux 上对新二进制文件执行 chmod +x，确保有执行权限
        if (Runtime.Linux && !isNetCore)
        {
            // 等待 chmod 完成（WaitForExit），避免 fire-and-forget 导致权限未及时生效的竞态条件。
            // 在慢速 ARM64 设备上，升级刚完成后系统 I/O 繁忙，chmod 可能超过 1 秒才能完成，
            // 若此时新进程尚无执行权限，UseShellExecute=false 会抛出 EACCES 导致启动失败。
            // 使用绝对路径 /bin/chmod 避免 PATH 解析，UseShellExecute=false 确保同步等待。
            var p = Process.Start(new ProcessStartInfo("/bin/chmod", "+x " + fileName) { UseShellExecute = false });
            p?.WaitForExit(5_000);
        }

        WriteLog("拉起进程 {0} {1}", fileName, args);
        try
        {
            var workingDir = Path.GetDirectoryName(fileName)!;
            Process? p;

            if (isNetCore)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = dotnetPath!,
                    Arguments = $"{fileName} {args}",
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                };

                // 注入 DOTNET_ROOT 环境变量，确保子进程在 systemd 隔离环境下找到运行时
                InjectDotNetRoot(startInfo, dotnetPath!);

                p = Process.Start(startInfo);
            }
            else
            {
                // 直接执行原生可执行文件（Linux 上为无扩展名的二进制文件），同样避免 shell 层
                p = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                });
            }

            if (p == null) return false;

            // 如果进程在指定时间内退出，说明启动失败，捕获 stderr 用于诊断
            if (p.WaitForExit(msWait))
            {
                if (p.ExitCode == 0) return true;

                // 非零退出：读取 stderr 写入 LastErrorMessage
                var stderr = p.StandardError.ReadToEnd();
                LastErrorMessage = !stderr.IsNullOrEmpty()
                    ? $"ExitCode={p.ExitCode}, stderr={stderr}"
                    : $"ExitCode={p.ExitCode}（无错误输出）";
                WriteLog("启动进程失败：{0}", LastErrorMessage);
                return false;
            }

            // 进程持续运行 → 成功
            return true;
        }
        catch (Exception ex)
        {
            // 保存完整异常信息供调用方通过 WriteInfoEvent 上报到节点历史，
            // 方便用户在 Web 管理界面直接看到失败原因（如 dotnet 未找到、文件无权限等）
            LastErrorMessage = ex.ToString();
            WriteLog("启动进程失败：{0}", ex);
            return false;
        }
    }

    /// <summary>解析 dotnet 可执行文件路径</summary>
    /// <remarks>
    /// 优先级：DOTNET_ROOT 环境变量 → /usr/bin/dotnet（Linux 标准软链接，NetRuntime.InstallOnLinux 创建）→ /usr/local/bin/dotnet → 回退 "dotnet"
    /// </remarks>
    /// <returns>dotnet 可执行文件路径</returns>
    internal static String ResolveDotNetPath()
    {
        // 优先使用 DOTNET_ROOT 环境变量
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!dotnetRoot.IsNullOrEmpty())
        {
            var path = Path.Combine(dotnetRoot, "dotnet");
            if (File.Exists(path)) return path;
        }

        // Linux 标准路径：/usr/bin/dotnet（NetRuntime.InstallOnLinux 创建的软链接）
        if (Runtime.Linux)
        {
            if (File.Exists("/usr/bin/dotnet")) return "/usr/bin/dotnet";
            if (File.Exists("/usr/local/bin/dotnet")) return "/usr/local/bin/dotnet";
        }

        // 回退到系统 PATH
        return "dotnet";
    }

    /// <summary>向子进程注入 DOTNET_ROOT 环境变量，确保在 systemd 隔离环境下找到运行时</summary>
    /// <param name="startInfo">进程启动信息</param>
    /// <param name="dotnetPath">已解析的 dotnet 路径</param>
    static void InjectDotNetRoot(ProcessStartInfo startInfo, String dotnetPath)
    {
#if NET5_0_OR_GREATER
        // 如果 dotnetPath 是绝对路径，反推运行时根目录
        if (Path.IsPathRooted(dotnetPath) && Runtime.Linux)
        {
            // /usr/bin/dotnet → /usr, /usr/share/dotnet/dotnet → /usr/share/dotnet
            var binDir = Path.GetDirectoryName(dotnetPath)!;
            var dotnetRoot = Path.GetDirectoryName(binDir);

            // /usr/bin/dotnet → 运行时在 /usr/share/dotnet 而不是 /usr
            if (dotnetRoot != null && (dotnetRoot.EndsWith("/bin") || dotnetRoot!.EndsWith("\\bin")))
                dotnetRoot = Path.Combine(Path.GetDirectoryName(dotnetRoot)!, "share", "dotnet");

            if (!dotnetRoot.IsNullOrEmpty() && Directory.Exists(dotnetRoot))
                startInfo.Environment["DOTNET_ROOT"] = dotnetRoot;
        }

        // 注入当前进程的 DOTNET_ROOT（如果有）
        var currentRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!currentRoot.IsNullOrEmpty() && !startInfo.Environment.ContainsKey("DOTNET_ROOT"))
            startInfo.Environment["DOTNET_ROOT"] = currentRoot;

        // 注入架构特定的 DOTNET_ROOT 变量（ARM64/x64）
        if (startInfo.Environment.TryGetValue("DOTNET_ROOT", out var root) && !root.IsNullOrEmpty())
        {
            var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
            if (arch == System.Runtime.InteropServices.Architecture.Arm64)
                startInfo.Environment["DOTNET_ROOT_ARM64"] = root;
            else if (arch == System.Runtime.InteropServices.Architecture.X64)
                startInfo.Environment["DOTNET_ROOT_X64"] = root;
        }
#else
        // .NET Framework：使用 EnvironmentVariables
        if (Path.IsPathRooted(dotnetPath) && Runtime.Linux)
        {
            var binDir = Path.GetDirectoryName(dotnetPath)!;
            var dotnetRoot = Path.GetDirectoryName(binDir);

            if (dotnetRoot != null && (dotnetRoot.EndsWith("/bin") || dotnetRoot!.EndsWith("\\bin")))
                dotnetRoot = Path.Combine(Path.GetDirectoryName(dotnetRoot)!, "share", "dotnet");

            if (!dotnetRoot.IsNullOrEmpty() && Directory.Exists(dotnetRoot))
                startInfo.EnvironmentVariables["DOTNET_ROOT"] = dotnetRoot;
        }

        var currentRoot2 = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!currentRoot2.IsNullOrEmpty() && !startInfo.EnvironmentVariables.ContainsKey("DOTNET_ROOT"))
            startInfo.EnvironmentVariables["DOTNET_ROOT"] = currentRoot2;
#endif
    }

    static Process? RunShell(String fileName, String args) => Process.Start(new ProcessStartInfo(fileName, args) { UseShellExecute = true });
    #endregion

    #region 辅助
    /// <summary>终止当前进程</summary>
    public virtual void KillSelf()
    {
        var p = Process.GetCurrentProcess();
        WriteLog("退出当前进程 {0}", p.Id);

        if (!Runtime.IsConsole)
            p.CloseMainWindow();

        // 直接退出进程，不再执行任何代码
        Environment.Exit(0);
    }

    /// <summary>
    /// 执行命令，文件名与参数由空格隔开
    /// </summary>
    /// <param name="cmd"></param>
    public void Run(String cmd)
    {
        if (cmd.IsNullOrEmpty()) return;

        WriteLog("执行命令：{0}", cmd);

        var args = "";
        var p = cmd.IndexOf(' ');
        if (p > 0)
        {
            args = cmd.Substring(p + 1);
            cmd = cmd.Substring(0, p);
        }

        RunShell(cmd, args);
    }

    /// <summary>
    /// 清理不属于当前平台的执行文件
    /// </summary>
    /// <param name="name"></param>
    public void Trim(String name)
    {
        var name2 = name.TrimSuffix(".exe", ".dll");
        if (Runtime.Windows || Runtime.Mono)
        {
            var file = name2.GetFullPath();
            if (File.Exists(file)) File.Delete(file);
        }
        else if (Runtime.Linux)
        {
            var file = (name2 + ".exe").GetFullPath();
            if (File.Exists(file)) File.Delete(file);
        }
    }
    #endregion

    #region 辅助
    private HttpClient? _Client;
    private HttpClient CreateClient()
    {
        if (_Client != null) return _Client;

        return _Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(DownloadTimeoutSeconds),
        };
    }

    /// <summary>下载文件</summary>
    /// <param name="client">客户端</param>
    /// <param name="address">地址</param>
    /// <param name="fileName">文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task<String> DownloadFileAsync(HttpClient client, String address, String fileName, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, address);
        var rs = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        rs.EnsureSuccessStatusCode();

        // 从Http响应头中获取文件名
        var file2 = rs.Content.Headers?.ContentDisposition?.FileName;
        if (!file2.IsNullOrEmpty()) fileName = Path.GetDirectoryName(fileName).CombinePath(file2);
        fileName.EnsureDirectory(true);

        // 删除已存在文件，否则新文件比旧文件小时，写入的文件后面有冗余数据，导致解压缩失败
        if (File.Exists(fileName))
            try
            {
                File.Delete(fileName);
            }
            catch { }

        var ms = rs.Content;
        using var fs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        await ms.CopyToAsync(fs).ConfigureAwait(false);
        await fs.FlushAsync(cancellationToken).ConfigureAwait(false);

        // 截断文件，如果前面删除失败，这里就可能使用旧文件，需要把多余部分截断
        fs.SetLength(fs.Position);

        return fileName;
    }

    /// <summary>删除备份文件</summary>
    /// <param name="dest">目标目录</param>
    public void DeleteBackup(String dest)
    {
        // 删除备份
        var di = dest.AsDirectory();
        var fs = di.GetAllFiles("*.del", true);
        foreach (var item in fs)
        {
            WriteLog("Delete {0}", item);
            try
            {
                item.Delete();
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message);
            }
        }
    }

    /// <summary>拷贝并替换。正在使用锁定的文件不可删除，但可以改名</summary>
    /// <param name="source">源目录</param>
    /// <param name="dest">目标目录</param>
    public void CopyAndReplace(String source, String dest)
    {
        WriteLog("CopyAndReplace {0} => {1}", source, dest);

        var src = source.AsDirectory();

        // 来源目录根，用于截断
        var root = src.FullName.EnsureEnd(Path.DirectorySeparatorChar.ToString());
        foreach (var item in src.GetAllFiles(null, true))
        {
            var name = item.FullName.TrimPrefix(root);
            var dst = dest.CombinePath(name).GetBasePath();

            // 如果是应用配置文件，不要更新
            //if (dst.EndsWithIgnoreCase(".exe.config") ||
            //    dst.EqualIgnoreCase("appsettings.json")) continue;
            // net45下，需要更新 StarAgent.exe.config ，里面的assemblyBinding指定了所依赖 NewLife.Core 的版本
            if (dst.EqualIgnoreCase("appsettings.json")) continue;

            // 拷贝覆盖
            WriteLog("Copy {0}", name);
            try
            {
                item.CopyTo(dst.EnsureDirectory(true), true);
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message);

                // 如果是exe/dll，则先改名，因为可能无法覆盖
                if (/*dst.EndsWithIgnoreCase(".exe", ".dll") &&*/ File.Exists(dst))
                {
                    //// 先尝试删除
                    //WriteLog("Delete {0}", item);
                    //try
                    //{
                    //    File.Delete(dst);
                    //}
                    //catch
                    //{
                    // 直接Move文件，不要删除，否则Linux上可能导致当前进程退出
                    WriteLog("Move {0}", item);
                    var del = $"{dst}.del";
                    //if (File.Exists(del)) File.Delete(del);
                    // 如果.del文件已存在，不能直接删，因为进程可能正在使用（上次升级未完成）。
                    if (File.Exists(del)) del = $"{dst}_{DateTime.Now:yyMMddHHmmss}.del";
                    File.Move(dst, del);
                    //}

                    item.CopyTo(dst, true);
                }
            }
        }

        // 删除临时目录
        WriteLog("Delete {0}", src.FullName);
        src.Delete(true);
    }
    #endregion

    #region 日志
    /// <summary>日志对象</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>输出日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[{Name}]{format}", args);
    #endregion
}