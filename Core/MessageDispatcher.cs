using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace YongChuanTools.Core
{
    /// <summary>
    /// 消息分发器 — 替代 switch(appType) 的注册表模式
    /// </summary>
    public unsafe class MessageDispatcher
    {
        private readonly ConcurrentDictionary<EnumsAppType, IMessageHandler> _handlers = new();
        private readonly ConcurrentDictionary<EnumsAppType, List<IMessageHandler>> _multiHandlers = new();

        public void Register(IMessageHandler handler)
        {
            if (handler == null) return;

            if (_handlers.ContainsKey(handler.AppType))
            {
                // 支持多处理器（同一类型注册多个处理器）
                _multiHandlers[handler.AppType].Add(handler);
                Log.Debug("多处理器注册: {AppType}", handler.AppType);
            }
            else
            {
                _handlers[handler.AppType] = handler;
                _multiHandlers[handler.AppType] = new List<IMessageHandler> { handler };
                Log.Information("消息处理器注册: {AppType}", handler.AppType);
            }
        }

        public void Unregister(EnumsAppType appType)
        {
            _handlers.TryRemove(appType, out _);
            _multiHandlers.TryRemove(appType, out _);
        }

        public bool Dispatch(UTHeader header, IntPtr payload, SessionContext sessionInfo)
        {
            var appType = (EnumsAppType)(((UTAppHeader*)payload.ToPointer())->appType);

            if (_handlers.TryGetValue(appType, out var handler))
            {
                Log.Debug("消息分发至处理器: {AppType}", appType);
                handler.Handle(header, payload, sessionInfo);
                return true;
            }

            Log.Warning("未找到处理器: AppType={AppType}", appType);
            return false;
        }

        public IEnumerable<EnumsAppType> RegisteredTypes => _handlers.Keys;

        public IMessageHandler? GetHandler(EnumsAppType appType)
        {
            return _handlers.GetValueOrDefault(appType);
        }
    }
}
