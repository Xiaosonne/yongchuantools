using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using YongChuanTools.Core;
using YongChuanTools.Data;
using YongChuanTools.Display;
using YongChuanTools.Logging;
using YongChuanTools.Server;
using YongChuanTools.Web;
using SC = Spectre.Console;

namespace YongChuanTools
{
    internal class Program
    {
        private static TcpServerWrapper? _tcpServer;
        private static ConsoleDisplay? _console;
        private static IHost? _webHost;
        private static readonly int TcpPort = 44370;
        private static readonly int WebPort = 8080;

        static async Task Main(string[] args)
        {
            // ========== 1. Serilog 初始化（全局） ==========
            var verbose = args.Length > 0 && args[0] == "-v";
            SerilogSetup.Initialize(verbose);

            // ========== 2. 全局服务 ==========
            var services = AppServices.Instance;

            // ========== 3. 控制台展示器 ==========
            _console = new ConsoleDisplay(services.Store, services.Events);

            // ========== 4. Web 仪表盘（与主程序共用 Serilog 全局实例） ==========
            _webHost = Host.CreateDefaultBuilder()
                .UseSerilog((context, config) =>
                {
                    // 与主程序共用相同配置
                    config = SerilogSetup.BuildConfig(verbose);
                })
                .ConfigureWebHostDefaults(web =>
                {
                    web.UseWebRoot("wwwroot");
                    web.ConfigureKestrel(o => o.ListenAnyIP(WebPort));
                    web.ConfigureServices(s =>
                    {
                        s.AddSignalR();
                        s.AddSingleton(services.Events);
                        s.AddSingleton(services.Store);
                        // 事件转发器：订阅 EventAggregator → SignalR 实时推送
                        s.AddSingleton<HubEventForwarder>();
                    });
                    web.Configure(app =>
                    {
                        app.UseSerilogRequestLogging(options =>
                        {
                            // API 请求日志只写文件，不写主控制台
                            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                            {
                                diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
                                diagnosticContext.Set("RequestPath", httpContext.Request.Path);
                            };
                        });
                        app.UseDefaultFiles();
                        app.UseStaticFiles();
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapHub<DashboardHub>("/dashboard");
                            endpoints.MapGet("/api/snapshot", async context =>
                            {
                                var (total, equip, lastId, sessions) = services.Store.GetStats();
                                await context.Response.WriteAsJsonAsync(new
                                {
                                    totalMessages = total,
                                    equipmentCount = equip,
                                    connectedSessions = sessions,
                                    lastMessageId = lastId,
                                    equipment = services.Store.GetAllEquipment(),
                                    systems = services.Store.GetAllSystems()
                                });
                            });
                            endpoints.MapGet("/api/health", async context =>
                            {
                                context.Response.StatusCode = 200;
                                await context.Response.WriteAsJsonAsync(new { status = "ok", port = WebPort });
                            });
                        });
                    });
                })
                .Build();

            await _webHost.StartAsync();

            // ========== 5. TCP 服务器 ==========
            _tcpServer = new TcpServerWrapper(IPAddress.Any, TcpPort);
            _tcpServer.Start();
            _tcpServer.SendCommandHandler = (addr, type) => _tcpServer.BroadcastCommand(addr, type);

            // ========== 6. 渲染初始界面 ==========
            RenderWelcome();
            Console.WriteLine();
            AnsiConsole.MarkupLine("  输入 [cyan]help[/] 查看命令帮助");
            Console.WriteLine();

            // ========== 7. 命令行交互循环 ==========
            await RunCommandLoop();

            // ========== 8. 关闭 ==========
            await Shutdown();
        }

        static void RenderWelcome()
        {
            AnsiConsole.Write(new SC.FigletText("YongChuanTools").Centered().Color(SC.Color.Blue));
            AnsiConsole.Write(new SC.Rule($"[cyan]GB26875 消防协议 · v2.0[/]").LeftJustified());
            Console.WriteLine();
            AnsiConsole.MarkupLine($"  [green]🟢 TCP 服务:[/]   端口 [yellow]{TcpPort}[/]");
            AnsiConsole.MarkupLine($"  [blue]🌐 Web 仪表盘:[/] http://localhost:[yellow]{WebPort}[/]");
            AnsiConsole.MarkupLine($"  [dim]📁 日志目录:[/]   {SerilogSetup.LogDir}/");
            AnsiConsole.MarkupLine($"  [dim]📋 API 日志:[/]   {SerilogSetup.LogDir}/api-*.log");
        }

        static async Task RunCommandLoop()
        {
            while (true)
            {
                try
                {
                    var input = await Task.Run(() => Console.ReadLine());
                    if (string.IsNullOrWhiteSpace(input)) continue;

                    var cmd = input.Trim();
                    var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var verb = parts[0].ToLower();

                    var handled = await ProcessCommand(verb, parts);
                    if (!handled)
                    {
                        AnsiConsole.MarkupLine($"  [red]未知命令:[/] {verb}  → 输入 [cyan]help[/] 查看帮助");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "命令处理异常");
                }
            }
        }

        static async Task<bool> ProcessCommand(string verb, string[] parts)
        {
            switch (verb)
            {
                case "help":
                case "?":
                    ConsoleDisplay.PrintHelp();
                    return true;

                case "status":
                case "stat":
                case "dashboard":
                    var (total, equip, lastId, sessions) = AppServices.Instance.Store.GetStats();
                    var table = new SC.Table { Title = new SC.TableTitle("[bold]状态摘要[/]") };
                    table.AddColumn("项目");
                    table.AddColumn("值");
                    table.AddRow("监听端口", $"{TcpPort}");
                    table.AddRow("连接会话", $"{sessions}");
                    table.AddRow("设备数量", $"{equip}");
                    table.AddRow("总消息", $"{total}");
                    table.AddRow("Web仪表盘", $"http://localhost:{WebPort}");
                    table.AddRow("日志目录", SerilogSetup.LogDir);
                    AnsiConsole.Write(table);
                    return true;

                case "sessions":
                    ListSessions();
                    return true;

                case "devices":
                case "dev":
                    ListDevices();
                    return true;

                case "device":
                    if (parts.Length < 2)
                    {
                        AnsiConsole.MarkupLine("  [red]用法:[/] device <地址>");
                        return true;
                    }
                    ShowDevice(parts[1]);
                    return true;

                case "hex":
                    if (parts.Length < 2)
                    {
                        AnsiConsole.MarkupLine("  [red]用法:[/] hex <hex字符串>");
                        return true;
                    }
                    AnsiConsole.MarkupLine($"  [yellow]发送原始HEX:[/] {parts[1]}");
                    return true;

                case "init":
                    if (parts.Length < 2)
                    {
                        AnsiConsole.MarkupLine("  [red]用法:[/] init <hex字符串>");
                        return true;
                    }
                    // 拼接所有 parts（跳过动词），支持多行 hex 粘贴
                    var hexInput = string.Join(" ", parts.Skip(1));
                    _tcpServer?.Init(hexInput);
                    AnsiConsole.MarkupLine($"  [green]已发送模拟Init数据[/]");
                    return true;

                case "test":
                    // 内置正确测试数据：75字节 部件状态上报 (appType=2)
                    // 00-01: start=0x4040
                    // 02-03: seq=0x1ECA
                    // 04-05: version=0x0101
                    // 06-11: time=10:29:17 2026-03-26
                    // 12-17: srcAddr=26:CD:0E:00:00:00
                    // 18-23: dstAddr=01:00:00:00:00:00
                    // 24-25: length=48 (payload after header)
                    // 26: cmd=2 (发送)
                    // 27: appType=2 (上传消防部件状态)
                    // 28: appCount=1
                    // 29: sysType=1 (通用)
                    // 30: sysAddr=2
                    // 31: equipType=1 (通用)
                    // 32-35: equipAddr=4
                    // 36-37: equipState=3 (屏蔽)
                    // 38: equipAddrType=1
                    // 39-68: equipDesc="01-02-004" (30 bytes, null-padded)
                    // 69-74: timestamp=10:29:17 2026-03-26
                    _tcpServer?.Init("40 40 CA 1E 01 01 2D 1A 10 1A 03 1A 26 CD 0E 00 00 00 01 00 00 00 00 00 30 00 02 02 01 01 02 01 04 00 00 00 03 00 01 30 31 2D 30 32 2D 30 30 34 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 2D 1A 10 1A 03 1A");
                    AnsiConsole.MarkupLine("  [green]测试命令已发送[/]");
                    return true;

                case "read":
                    if (parts.Length < 3)
                    {
                        AnsiConsole.MarkupLine("  [red]用法:[/] read <设备地址> <类型(61/62/63/65)>");
                        return true;
                    }
                    if (Enum.TryParse<EnumReadTypes>(parts[2], out var readType))
                    {
                        _tcpServer?.BroadcastCommand(parts[1], readType);
                        Log.Information("查询命令: {Addr} {Type}", parts[1], readType);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"  [red]无效类型:[/] {parts[2]}");
                    }
                    return true;

                case "clear":
                case "cls":
                    AnsiConsole.Clear();
                    RenderWelcome();
                    return true;

                case "quit":
                case "exit":
                    await Shutdown();
                    return true;

                default:
                    return false;
            }
        }

        static void ListSessions()
        {
            var sessions = AppServices.Instance.Sessions;
            if (sessions.Count == 0)
            {
                AnsiConsole.MarkupLine("  [dim]暂无连接会话[/]");
                return;
            }

            var table = new SC.Table { Title = new SC.TableTitle($"[bold]会话列表 ({sessions.Count})[/]") };
            table.AddColumn("会话ID");
            table.AddColumn("远程地址");
            table.AddColumn("连接时间");

            foreach (var s in sessions.Values)
            {
                table.AddRow(
                    $"[cyan]{s.SessionId[..Math.Min(8, s.SessionId.Length)]}[/]...",
                    s.RemoteEndpoint,
                    s.ConnectedAt.ToString("HH:mm:ss"));
            }
            AnsiConsole.Write(table);
        }

        static void ListDevices()
        {
            var devices = AppServices.Instance.Store.GetAllEquipment();
            var list = new System.Collections.Generic.List<EquipmentSnapshot>(devices);

            if (list.Count == 0)
            {
                AnsiConsole.MarkupLine("  [dim]暂无设备数据[/]");
                return;
            }

            var table = new SC.Table { Title = new SC.TableTitle($"[bold]设备列表 ({list.Count})[/]") };
            table.AddColumn("地址");
            table.AddColumn("系统");
            table.AddColumn("设备类型");
            table.AddColumn("状态");
            table.AddColumn("描述");
            table.AddColumn("更新时间");

            foreach (var d in list)
            {
                var stateColor = d.EquipState switch
                {
                    0 => "green",
                    1 => "red",
                    2 => "yellow",
                    3 => "grey",
                    _ => "white"
                };
                var stateName = d.EquipState switch
                {
                    0 => "正常",
                    1 => "报警",
                    2 => "故障",
                    3 => "屏蔽",
                    _ => $"({d.EquipState})"
                };

                table.AddRow(
                    $"[cyan]{d.FormattedAddress}[/]",
                    d.SysTypeName,
                    d.EquipTypeName,
                    $"[{stateColor}]{stateName}[/]",
                    d.Description.Length > 15 ? d.Description[..15] + "..." : d.Description,
                    d.LastUpdate.ToString("HH:mm:ss"));
            }
            AnsiConsole.Write(table);
        }

        static void ShowDevice(string address)
        {
            var device = AppServices.Instance.Store.GetEquipment(address);
            if (device == null)
            {
                AnsiConsole.MarkupLine($"  [red]未找到设备:[/] {address}");
                return;
            }

            var table = new SC.Table { Title = new SC.TableTitle($"[bold]设备详情: {address}[/]") };
            table.AddColumn("属性");
            table.AddColumn("值");

            table.AddRow("地址", device.FormattedAddress);
            table.AddRow("系统类型", $"{device.SysTypeName} ({device.SysType})");
            table.AddRow("设备类型", $"{device.EquipTypeName} ({device.EquipType})");
            table.AddRow("设备状态", $"{device.EquipStateName} ({device.EquipState})");
            table.AddRow("会话ID", device.SessionId);
            table.AddRow("更新时间", device.LastUpdate.ToString("yyyy-MM-dd HH:mm:ss"));
            if (!string.IsNullOrEmpty(device.Description))
                table.AddRow("描述", device.Description);

            AnsiConsole.Write(table);
        }

        static async Task Shutdown()
        {
            Log.Information("关闭中...");

            _tcpServer?.Stop();
            _tcpServer?.Dispose();
            await _webHost?.StopAsync()!;
            _webHost?.Dispose();
            _console?.Dispose();

            SerilogSetup.Shutdown();
            Environment.Exit(0);
        }
    }
}
