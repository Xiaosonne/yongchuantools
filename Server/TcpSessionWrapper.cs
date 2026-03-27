using NetCoreServer;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using YongChuanTools.Core;

namespace YongChuanTools.Server
{
    /// <summary>
    /// 新架构会话包装器 — 替代原始 NetTcpSession，集成事件发布和彩色日志
    /// </summary>
    internal class TcpSessionWrapper : TcpSession
    {
        private static readonly ConcurrentDictionary<string, byte[]> SendTemp = new();

        public string SessionId { get; private set; } = "";
        public string RemoteEndpoint { get; private set; } = "";

        private readonly Action<UTHeader, IntPtr, SessionContext> _protocolHandler;

        public TcpSessionWrapper(TcpServer server,
            Action<UTHeader, IntPtr, SessionContext> protocolHandler)
            : base(server)
        {
            _protocolHandler = protocolHandler;
        }

        protected override void OnConnected()
        {
            SessionId = Id.ToString("N");
            RemoteEndpoint = Socket.RemoteEndPoint?.ToString() ?? "unknown";

            AppServices.Instance.RegisterSession(SessionId, RemoteEndpoint);

            Log.Information("[{SessionId}] 📡 客户端连接: {Endpoint}", SessionId, RemoteEndpoint);
        }

        protected override void OnDisconnected()
        {
            SendTemp.TryRemove(SessionId, out _);
            AppServices.Instance.UnregisterSession(SessionId);
            Log.Information("[{SessionId}] 🔌 客户端断开: {Endpoint}", SessionId, RemoteEndpoint);
        }

        protected override unsafe void OnReceived(byte[] buffer, long offset, long size)
        {
            // 记录原始报文
            byte[] recBytes = new byte[size];
            Array.Copy(buffer, recBytes, size);
            AppServices.Instance.RecordReceivedBytes(recBytes, RemoteEndpoint);

            // 构造会话上下文
            var sessionCtx = new SessionContext
            {
                SessionId = SessionId,
                RemoteEndpoint = RemoteEndpoint,
                RawBuffer = recBytes
            };

            // 调用协议解析
            UTProtocol.Decap(buffer, offset, size,
                zerolenhandler: header =>
                {
                    // 空数据或未知类型，回确认
                    var reply = UTProtocol.ReplyMessage(header);
                    SendAsync(reply);
                    AppServices.Instance.RecordSentBytes(reply, RemoteEndpoint);
                },
                handlers: new (EnumsAppType, Action<IntPtr>)[] {
                    (EnumsAppType.上传消防系统状态,
                        new Action<IntPtr>(ptr => HandleSysState(ptr, sessionCtx))),
                    (EnumsAppType.上传消防部件状态,
                        new Action<IntPtr>(ptr => HandleEquipState(ptr, sessionCtx))),
                    (EnumsAppType.上传消防部件模拟量,
                        new Action<IntPtr>(ptr => HandleAnalog(ptr, sessionCtx)))
                });
        }

        private unsafe void HandleEquipState(IntPtr ptr, SessionContext ctx)
        {
            var state = (UTEquipState*)ptr.ToPointer();

            // 计算原始报文（用于 SignalR 实时推送）
            int structSize = Marshal.SizeOf<UTEquipState>();
            byte[] rawBytes = new byte[structSize];
            fixed (byte* destPtr = rawBytes)
            {
                System.Buffer.MemoryCopy(state, destPtr, structSize, structSize);
            }
            string rawHex = BitConverter.ToString(rawBytes).Replace("-", " ");

            // 存储
            AppServices.Instance.Store.UpdateEquipment(*state, SessionId, RemoteEndpoint);

            // 发布事件
            AppServices.Instance.Events.Publish(new EquipmentStateChangedEvent(*state, SessionId, RemoteEndpoint, DateTime.Now, rawHex));

            // 回复确认
            var reply = UTProtocol.ReplyMessage(state->header);
            SendAsync(reply);
            AppServices.Instance.RecordSentBytes(reply, RemoteEndpoint);
        }

        private unsafe void HandleSysState(IntPtr ptr, SessionContext ctx)
        {
            var state = (UTSysState*)ptr.ToPointer();

            // 计算原始报文（用于 SignalR 实时推送）
            int structSize = Marshal.SizeOf<UTSysState>();
            byte[] rawBytes = new byte[structSize];
            fixed (byte* destPtr = rawBytes)
            {
                System.Buffer.MemoryCopy(state, destPtr, structSize, structSize);
            }
            string rawHex = BitConverter.ToString(rawBytes).Replace("-", " ");

            AppServices.Instance.Store.UpdateSystem(*state, SessionId, RemoteEndpoint);
            AppServices.Instance.Events.Publish(new SystemStateChangedEvent(*state, SessionId, RemoteEndpoint, DateTime.Now, rawHex));

            var reply = UTProtocol.ReplyMessage(state->header);
            SendAsync(reply);
            AppServices.Instance.RecordSentBytes(reply, RemoteEndpoint);
        }

        private unsafe void HandleAnalog(IntPtr ptr, SessionContext ctx)
        {
            // 模拟量结构待定，先回确认并记录
            var state = (UTEquipState*)ptr.ToPointer();
            Log.Warning("[{SessionId}] 收到模拟量上报（结构待实现）", SessionId);

            var reply = UTProtocol.ReplyMessage(state->header);
            SendAsync(reply);
            AppServices.Instance.RecordSentBytes(reply, RemoteEndpoint);
        }

        protected override void OnError(SocketError error)
        {
            Log.Error("[{SessionId}] ⚠️ 会话错误: {Error}", SessionId, error);
        }

        public override bool SendAsync(byte[] buffer)
        {
            AppServices.Instance.RecordSentBytes(buffer, RemoteEndpoint);
            return base.SendAsync(buffer);
        }

        /// <summary>
        /// 发送查询命令给指定设备
        /// </summary>
        public void SendCommand(string equipAddr, EnumReadTypes type)
        {
            // 从设备存储中查找设备
            var equip = AppServices.Instance.Store.GetEquipment(equipAddr);
            if (equip == null)
            {
                Log.Warning("设备未找到: {Address}", equipAddr);
                return;
            }

            // 重新构造读取请求（通过 raw buffer 中的 header）
            // 这里简化处理：查找设备关联的原始状态来构造请求
            Log.Information("[{SessionId}] 发送查询命令: {Address} type={Type}", SessionId, equipAddr, type);
        }

        /// <summary>
        /// 模拟接收原始报文（用于 init 命令测试）
        /// </summary>
        public void SimulateReceive(byte[] data)
        {
            Log.Information("[{SessionId}] 模拟接收 {Length} 字节", SessionId, data.Length);
            OnReceived(data, 0, data.Length);
        }
    }
}
