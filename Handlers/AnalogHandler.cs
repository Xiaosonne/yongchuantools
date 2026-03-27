using Serilog;
using System;
using YongChuanTools.Core;

namespace YongChuanTools.Handlers
{
    /// <summary>
    /// 模拟量处理器 — 处理 appType=3 (上传消防部件模拟量)
    /// 目前协议结构未知，先记录日志并返回确认
    /// </summary>
    public class AnalogHandler : IMessageHandler
    {
        public EnumsAppType AppType => EnumsAppType.上传消防部件模拟量;
        public bool Enabled => true;

        private readonly Action<UTHeader> _replyCallback;

        public AnalogHandler(Action<UTHeader> replyCallback)
        {
            _replyCallback = replyCallback;
        }

        public unsafe void Handle(UTHeader header, IntPtr payload, SessionContext sessionInfo)
        {
            // TODO: 等拿到 GB26875 模拟量报文格式后补充结构解析
            Log.Warning("收到模拟量上报 (appType=3)，会话: {SessionId}，结构待实现",
                sessionInfo.SessionId);

            // 先回确认，避免设备一直重发
            _replyCallback?.Invoke(header);
        }
    }
}
