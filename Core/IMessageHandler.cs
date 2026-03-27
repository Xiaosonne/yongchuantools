using System;

namespace YongChuanTools.Core
{
    /// <summary>
    /// 协议消息处理器接口 — 替代 switch 分散逻辑
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>处理的 App 类型</summary>
        EnumsAppType AppType { get; }

        /// <summary>是否启用</summary>
        bool Enabled { get; }

        /// <summary>
        /// 处理接收到的消息
        /// </summary>
        /// <param name="header">协议头</param>
        /// <param name="payload">消息体指针（已定位到 appHeader 之后）</param>
        /// <param name="sessionInfo">会话信息</param>
        void Handle(UTHeader header, IntPtr payload, SessionContext sessionInfo);
    }

    /// <summary>
    /// 会话上下文 — 传递会话相关信息给处理器
    /// </summary>
    public class SessionContext
    {
        public string SessionId { get; set; } = "";
        public string RemoteEndpoint { get; set; } = "";
        public byte[] RawBuffer { get; set; } = Array.Empty<byte>();
    }
}
