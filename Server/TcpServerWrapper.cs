using NetCoreServer;
using Serilog;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using YongChuanTools.Core;

namespace YongChuanTools.Server
{
    /// <summary>
    /// 新架构 TCP 服务器 — 基于 NetCoreServer，集成事件发布
    /// </summary>
    public class TcpServerWrapper : TcpServer
    {
        public new int Port { get; }
        public Action<string, EnumReadTypes>? SendCommandHandler { get; set; }

        public TcpServerWrapper(IPAddress address, int port) : base(address, port)
        {
            Port = port;
        }

        protected override TcpSession CreateSession()
        {
            return new TcpSessionWrapper(this, (header, ptr, ctx) =>
            {
                // 协议处理由 TcpSessionWrapper 内部完成
                // 此回调用于特殊扩展场景
            });
        }

        protected override void OnStarted()
        {
            Log.Information("🟢 TCP 服务器启动: 端口 {Port}", Port);
        }

        protected override void OnStopped()
        {
            Log.Information("🔴 TCP 服务器停止: 端口 {Port}", Port);
        }

        protected override void OnError(SocketError error)
        {
            Log.Error("⚠️ TCP 服务器错误: {Error}", error);
        }

        /// <summary>
        /// 广播命令到所有会话
        /// </summary>
        public void BroadcastCommand(string equipAddr, EnumReadTypes type)
        {
            int count = 0;
            foreach (var session in Sessions.Values)
            {
                if (session is TcpSessionWrapper wrapper)
                {
                    wrapper.SendCommand(equipAddr, type);
                    count++;
                }
            }
            Log.Information("广播命令: {EquipAddr} type={Type} → {Count} 个会话",
                equipAddr, type, count);
        }

        /// <summary>
        /// 初始化：模拟设备上报数据
        /// - 有活跃会话：通过 TcpSessionWrapper.SimulateReceive 走真实 Decap 解析
        /// - 无活跃会话：直接通过 UTProtocol.Decap 解析
        /// </summary>
        public unsafe void Init(string hexData)
        {
            // 支持带空格、不带空格、带横线的格式
            hexData = hexData.Replace(" ", "").Replace("-", "").Replace("\r", "").Replace("\n", "");

            if (hexData.Length % 2 != 0)
            {
                Log.Error("Init: hex字符串长度必须是偶数，当前: {Length}", hexData.Length);
                return;
            }

            var bytes = new byte[hexData.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hexData.Substring(i * 2, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
            }

            Log.Information("Init: 模拟报文 ({Length} 字节) 解析中...", bytes.Length);

            // 查找第一个会话作为 sessionInfo 来源
            string sessionId = "simulated";
            string remoteEndpoint = "127.0.0.1:simulated";
            TcpSessionWrapper? targetSession = null;
            foreach (var session in Sessions.Values)
            {
                if (session is TcpSessionWrapper wrapper)
                {
                    targetSession = wrapper;
                    sessionId = wrapper.SessionId;
                    remoteEndpoint = wrapper.RemoteEndpoint;
                    break;
                }
            }

            // 发布原始报文事件（用于显示 hex）
            AppServices.Instance.RecordReceivedBytes(bytes, remoteEndpoint);

            if (targetSession != null)
            {
                // 有活跃会话：通过 SimulateReceive 走真实 Decap 解析路径
                Log.Information("Init: 通过 SimulateReceive 转发到 {SessionId}", sessionId);
                targetSession.SimulateReceive(bytes);
            }
            else
            {
                // 无活跃会话：直接通过 Decap 解析
                Log.Information("Init: 无活跃会话，直接通过 Decap 解析");
                SimulateWithDecap(bytes, sessionId, remoteEndpoint);
            }
        }

        /// <summary>
        /// 无会话时，直接用 Decap 解析模拟数据
        /// </summary>
        private unsafe void SimulateWithDecap(byte[] bytes, string sessionId, string remoteEndpoint)
        {
            var sessionCtx = new SessionContext
            {
                SessionId = sessionId,
                RemoteEndpoint = remoteEndpoint,
                RawBuffer = bytes
            };

            UTProtocol.Decap(bytes, 0, bytes.Length,
                zerolenhandler: header =>
                {
                    Log.Warning("Init: 收到空数据或未知类型 zerolenhandler");
                },
                handlers: new (EnumsAppType, Action<IntPtr>)[] {
                    (EnumsAppType.上传消防系统状态,
                        new Action<IntPtr>(ptr => SimulateSysState(ptr, sessionCtx))),
                    (EnumsAppType.上传消防部件状态,
                        new Action<IntPtr>(ptr => SimulateEquipState(ptr, sessionCtx))),
                    (EnumsAppType.上传消防部件模拟量,
                        new Action<IntPtr>(ptr => SimulateAnalog(ptr, sessionCtx)))
                });
        }

        private unsafe void SimulateEquipState(IntPtr ptr, SessionContext ctx)
        {
            var state = (UTEquipState*)ptr.ToPointer();
            var structSize = Marshal.SizeOf<UTEquipState>();
            byte[] rawBytes = new byte[structSize];
            fixed (byte* destPtr = rawBytes)
            {
                System.Buffer.MemoryCopy(state, destPtr, structSize, structSize);
            }
            string rawHex = BitConverter.ToString(rawBytes).Replace("-", " ");

            AppServices.Instance.Store.UpdateEquipment(*state, ctx.SessionId, ctx.RemoteEndpoint);
            AppServices.Instance.Events.Publish(new EquipmentStateChangedEvent(*state, ctx.SessionId, ctx.RemoteEndpoint, DateTime.Now, rawHex));
        }

        private unsafe void SimulateSysState(IntPtr ptr, SessionContext ctx)
        {
            var state = (UTSysState*)ptr.ToPointer();
            var structSize = Marshal.SizeOf<UTSysState>();
            byte[] rawBytes = new byte[structSize];
            fixed (byte* destPtr = rawBytes)
            {
                System.Buffer.MemoryCopy(state, destPtr, structSize, structSize);
            }
            string rawHex = BitConverter.ToString(rawBytes).Replace("-", " ");

            AppServices.Instance.Store.UpdateSystem(*state, ctx.SessionId, ctx.RemoteEndpoint);
            AppServices.Instance.Events.Publish(new SystemStateChangedEvent(*state, ctx.SessionId, ctx.RemoteEndpoint, DateTime.Now, rawHex));
        }

        private unsafe void SimulateAnalog(IntPtr ptr, SessionContext ctx)
        {
            // 模拟量结构待定，只记录日志
            Log.Warning("Init: 收到模拟量上报（结构待实现）");
        }
    }
}
