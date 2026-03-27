using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Serilog;

namespace YongChuanTools.Core
{
    /// <summary>
    /// 事件聚合器 — 进程内发布/订阅，替代硬编码回调
    /// </summary>
    public class EventAggregator
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();
        private readonly object _lock = new();

        /// <summary>
        /// 订阅事件
        /// </summary>
        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            lock (_lock)
            {
                var list = _subscribers.GetOrAdd(typeof(TEvent), _ => new List<Delegate>());
                list.Add(handler);
                Log.Debug("事件订阅: {EventType}", typeof(TEvent).Name);
            }
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            lock (_lock)
            {
                if (_subscribers.TryGetValue(typeof(TEvent), out var list))
                {
                    list.Remove(handler);
                }
            }
        }

        /// <summary>
        /// 发布事件（同步）
        /// </summary>
        public void Publish<TEvent>(TEvent eventData)
        {
            List<Delegate> handlersCopy;
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(typeof(TEvent), out var list)) return;
                handlersCopy = new List<Delegate>(list);
            }

            foreach (var handler in handlersCopy)
            {
                try
                {
                    ((Action<TEvent>)handler)(eventData);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "事件处理异常: {EventType}", typeof(TEvent).Name);
                }
            }
        }
    }

    // ============ 核心事件类型 ============

    /// <summary>收到原始报文事件</summary>
    public record RawMessageReceivedEvent(byte[] Buffer, string RemoteEndpoint, DateTime Timestamp);

    /// <summary>设备状态更新事件</summary>
    public record EquipmentStateChangedEvent(UTEquipState State, string SessionId, string RemoteEndpoint, DateTime Timestamp, string RawHex);

    /// <summary>系统状态更新事件</summary>
    public record SystemStateChangedEvent(UTSysState State, string SessionId, string RemoteEndpoint, DateTime Timestamp, string RawHex);

    /// <summary>会话连接事件</summary>
    public record SessionConnectedEvent(string SessionId, string RemoteEndpoint);

    /// <summary>会话断开事件</summary>
    public record SessionDisconnectedEvent(string SessionId, string RemoteEndpoint);

    /// <summary>发送原始报文事件</summary>
    public record RawMessageSentEvent(byte[] Buffer, string RemoteEndpoint, DateTime Timestamp);
}
