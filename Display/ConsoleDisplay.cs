using Serilog;
using Spectre.Console;
using System;
using System.Runtime.InteropServices;
using System.Text;
using YongChuanTools.Core;
using YongChuanTools.Data;
using SC = Spectre.Console;

namespace YongChuanTools.Display
{
    /// <summary>
    /// 控制台展示器 — 基于 Spectre.Console 的彩色结构化输出
    /// </summary>
    public class ConsoleDisplay : IDisposable
    {
        private readonly EquipmentStore _store;
        private readonly EventAggregator _events;
        private bool _disposed = false;

        public ConsoleDisplay(EquipmentStore store, EventAggregator events)
        {
            _store = store;
            _events = events;
            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            _events.Subscribe<RawMessageReceivedEvent>(OnRawReceived);
            _events.Subscribe<EquipmentStateChangedEvent>(OnEquipmentChanged);
            _events.Subscribe<SystemStateChangedEvent>(OnSystemChanged);
            _events.Subscribe<SessionConnectedEvent>(OnSessionConnected);
            _events.Subscribe<SessionDisconnectedEvent>(OnSessionDisconnected);
            _events.Subscribe<RawMessageSentEvent>(OnRawSent);
        }

        // ============ 事件处理 ============

        private void OnRawReceived(RawMessageReceivedEvent e)
        {
            AnsiConsole.MarkupLine("");
            AnsiConsole.Write(new SC.Rule($"[dim]{e.Timestamp:HH:mm:ss.fff}[/] [yellow]收到原始报文[/] [dim]{e.RemoteEndpoint}[/]").LeftJustified());
            AnsiConsole.MarkupLine($"  [dim]HEX:[/] [cyan]{MessageFormatter.FormatHex(e.Buffer)}[/]");
        }

        private void OnRawSent(RawMessageSentEvent e)
        {
            AnsiConsole.MarkupLine("");
            AnsiConsole.Write(new SC.Rule($"[dim]{e.Timestamp:HH:mm.fff}[/] [green]发送原始报文[/] [dim]{e.RemoteEndpoint}[/]").LeftJustified());
            AnsiConsole.MarkupLine($"  [dim]HEX:[/] [cyan]{MessageFormatter.FormatHex(e.Buffer)}[/]");
        }

        private void OnSessionConnected(SessionConnectedEvent e)
        {
            AnsiConsole.Write(new SC.Rule("[green]会话连接[/]").LeftJustified());
            RenderKeyValueTable(new (string, string, string)[] {
                ("会话ID", e.SessionId, ""),
                ("远程地址", e.RemoteEndpoint, ""),
            });
        }

        private void OnSessionDisconnected(SessionDisconnectedEvent e)
        {
            AnsiConsole.Write(new SC.Rule("[red]会话断开[/]").LeftJustified());
            RenderKeyValueTable(new (string, string, string)[] {
                ("会话ID", e.SessionId, ""),
                ("远程地址", e.RemoteEndpoint, ""),
            });
        }

        private unsafe void OnEquipmentChanged(EquipmentStateChangedEvent e)
        {
            var state = e.State;
            var header = &state.header;
            var timestamp = MessageFormatter.FormatHeaderTime(header);
            var addrBytes = MessageFormatter.FormatAddrBytes(header->srcAddr);

            AnsiConsole.Write(new SC.Rule("[bold cyan]部件状态上报[/]").LeftJustified());

            RenderKeyValueTable(new (string, string, string)[] {
                ("时间", timestamp, ""),
                ("包序号", $"{state.header.seq}", ""),
                ("会话ID", e.SessionId, ""),
                ("命令类型", $"{((EnumCmdTypes)state.header.cmd)} ({state.header.cmd})", ""),
            });

            AnsiConsole.Write(new SC.Rule("[bold]设备信息[/]").LeftJustified());
            RenderKeyValueTable(new (string, string, string)[] {
                ("系统类型", $"[blue]{MessageFormatter.FormatSysType(state.sysType)}[/]", $"({state.sysType})"),
                ("系统地址", $"{state.sysAddr} (0x{state.sysAddr:X2})", ""),
                ("设备类型", $"[yellow]{MessageFormatter.FormatEquipType(state.equipType)}[/]", $"({state.equipType})"),
                ("设备地址", $"[bold]{state.GetEquipAddr()}[/]", $"({state.equipAddr})"),
                ("地址格式", MessageFormatter.FormatAddrType(state.equipAddrType), ""),
                ("设备状态", FormatStateWithColor(state.equipState), $"({state.equipState})"),
                ("设备描述", MessageFormatter.GetEquipDescription(&state), ""),
            });

            AnsiConsole.Write(new SC.Rule("[bold]协议地址[/]").LeftJustified());
            AnsiConsole.MarkupLine($"  [dim]源地址:[/] {addrBytes}  [dim]应用类型:[/] {state.appheader.appType}  [dim]应用计数:[/] {state.appheader.appCount}");
        }

        private unsafe void OnSystemChanged(SystemStateChangedEvent e)
        {
            var state = e.State;
            var header = &state.header;
            var timestamp = MessageFormatter.FormatHeaderTime(header);

            AnsiConsole.Write(new SC.Rule("[bold magenta]系统状态上报[/]").LeftJustified());

            RenderKeyValueTable(new (string, string, string)[] {
                ("时间", timestamp, ""),
                ("包序号", $"{state.header.seq}", ""),
                ("会话ID", e.SessionId, ""),
                ("系统类型", $"[blue]{MessageFormatter.FormatSysType(state.sysType)}[/]", $"({state.sysType})"),
                ("系统地址", $"{state.sysAddr} (0x{state.sysAddr:X2})", ""),
                ("系统状态", FormatStateWithColor(state.sysState), $"({state.sysState})"),
            });
        }

        // ============ 辅助方法 ============

        private void RenderKeyValueTable((string key, string value, string note)[] rows)
        {
            var table = new SC.Table { ShowHeaders = false, ShowFooters = false, Border = SC.TableBorder.None };
            table.AddColumn("");
            table.AddColumn("");

            foreach (var (key, value, note) in rows)
            {
                var valueText = string.IsNullOrEmpty(note) ? value : $"{value} [dim]({note})[/]";
                table.AddRow($"[dim]{key}:[/]", valueText);
            }

            AnsiConsole.Write(table);
        }

        private string FormatStateWithColor(ushort state)
        {
            var name = MessageFormatter.FormatEquipState(state);
            return state switch
            {
                0 => $"[green]{name}[/]",
                1 => $"[bold red]{name}[/]",
                2 => $"[yellow]{name}[/]",
                3 => $"[dim]{name}[/]",
                _ => $"[grey]{name}[/]"
            };
        }

        // ============ 仪表盘展示 ============

        public void RenderDashboard()
        {
            var (total, equip, lastId, sessions) = _store.GetStats();

            AnsiConsole.Clear();
            AnsiConsole.Write(new SC.FigletText("YongChuanTools").Centered().Color(SC.Color.Blue));

            var panel = new SC.Panel(new SC.Text($"[bold]GB26875 消防协议监控工具[/]"));
            AnsiConsole.Write(panel);
        }

        /// <summary>
        /// 打印使用帮助
        /// </summary>
        public static void PrintHelp()
        {
            var table = new SC.Table { Title = new SC.TableTitle("[bold]控制台命令[/]"), Border = SC.TableBorder.Rounded };
            table.AddColumn("命令");
            table.AddColumn("说明");
            table.AddColumn("示例");

            var rows = new[]
            {
                ("help", "显示本帮助", "help"),
                ("status", "显示状态摘要", "status"),
                ("sessions", "列出所有连接会话", "sessions"),
                ("devices", "列出所有设备", "devices"),
                ("device <addr>", "查看指定设备详情", "device 01-001-001"),
                ("hex <hexstr>", "发送原始HEX报文", "hex 5AA5..."),
                ("init <hexstr>", "模拟设备上报报文", "init 5AA5..."),
                ("read <addr> <type>", "查询设备状态", "read 01-001-001 62"),
                ("dashboard", "刷新仪表盘", "dashboard"),
                ("clear", "清屏", "clear"),
                ("quit / exit", "退出程序", "quit"),
            };

            foreach (var row in rows)
            {
                table.AddRow($"[cyan]{row.Item1}[/]", row.Item2, $"[dim]{row.Item3}[/]");
            }

            AnsiConsole.Write(table);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
