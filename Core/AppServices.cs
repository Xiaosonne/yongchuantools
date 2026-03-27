using Serilog;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using YongChuanTools.Data;
using YongChuanTools.Handlers;

namespace YongChuanTools.Core
{
    /// <summary>
    /// 全局应用服务 — 单例，持有所有核心组件
    /// </summary>
    public class AppServices : IDisposable
    {
        private static readonly Lazy<AppServices> _instance = new(() => new AppServices());
        public static AppServices Instance => _instance.Value;

        public EventAggregator Events { get; } = new();
        public EquipmentStore Store { get; } = new();
        public MessageDispatcher Dispatcher { get; } = new();
        public ConcurrentDictionary<string, SessionInfo> Sessions { get; } = new();
        private bool _disposed = false;

        private AppServices()
        {
            Log.Information("运行时 Marshal.SizeOf<UTHeader>() = {Size}", Marshal.SizeOf<UTHeader>());
            // 注册协议处理器
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            // 部件状态
            Dispatcher.Register(new EquipStateHandler(
                Events, Store,
                header => { })); // 回复由 NetTcpSession 发送

            // 系统状态
            Dispatcher.Register(new SysStateHandler(
                Events, Store,
                header => { }));

            // 模拟量（待实现）
            Dispatcher.Register(new AnalogHandler(
                header => { }));

            Log.Information("应用服务初始化完成，已注册 {Count} 个处理器", Dispatcher.RegisteredTypes.Count());
        }

        /// <summary>
        /// 从现有 TcpSession 派生，新架构使用 TcpSessionWrapper
        /// </summary>
        public void RegisterSession(string sessionId, string endpoint)
        {
            Sessions[sessionId] = new SessionInfo
            {
                SessionId = sessionId,
                RemoteEndpoint = endpoint,
                ConnectedAt = DateTime.Now
            };
            Store.ConnectedSessionCount = Sessions.Count;
            Events.Publish(new SessionConnectedEvent(sessionId, endpoint));
            Log.Information("会话注册: {SessionId} @ {Endpoint}", sessionId, endpoint);
        }

        public void UnregisterSession(string sessionId)
        {
            if (Sessions.TryRemove(sessionId, out var info))
            {
                Store.ConnectedSessionCount = Sessions.Count;
                Events.Publish(new SessionDisconnectedEvent(sessionId, info.RemoteEndpoint));
                Log.Information("会话注销: {SessionId}", sessionId);
            }
        }

        public SessionInfo? GetSession(string sessionId) =>
            Sessions.GetValueOrDefault(sessionId);

        public void RecordReceivedBytes(byte[] buffer, string endpoint)
        {
            Events.Publish(new RawMessageReceivedEvent(buffer, endpoint, DateTime.Now));
        }

        public void RecordSentBytes(byte[] buffer, string endpoint)
        {
            Events.Publish(new RawMessageSentEvent(buffer, endpoint, DateTime.Now));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }

    public class SessionInfo
    {
        public string SessionId { get; init; } = "";
        public string RemoteEndpoint { get; init; } = "";
        public DateTime ConnectedAt { get; init; }
    }
}
