using Serilog;
using System;
using System.Runtime.InteropServices;
using YongChuanTools.Core;
using YongChuanTools.Data;

namespace YongChuanTools.Handlers
{
    /// <summary>
    /// 系统状态处理器 — 处理 appType=1 (上传消防系统状态)
    /// </summary>
    public class SysStateHandler : IMessageHandler
    {
        public EnumsAppType AppType => EnumsAppType.上传消防系统状态;
        public bool Enabled => true;

        private readonly EventAggregator _events;
        private readonly EquipmentStore _store;
        private readonly Action<UTHeader> _replyCallback;

        public SysStateHandler(EventAggregator events, EquipmentStore store, Action<UTHeader> replyCallback)
        {
            _events = events;
            _store = store;
            _replyCallback = replyCallback;
        }

        public unsafe void Handle(UTHeader header, IntPtr payload, SessionContext sessionInfo)
        {
            try
            {
                var state = (UTSysState*)payload.ToPointer();

                Log.Information("系统状态上报 {@SysInfo}",
                    new
                    {
                        Seq = state->header.seq,
                        SysType = (EnumEquipStateSysType)state->sysType,
                        SysAddr = state->sysAddr,
                        SysState = state->sysState,
                        SessionId = sessionInfo.SessionId
                    });

                // 计算原始报文（用于 SignalR 实时推送）
                int structSize = Marshal.SizeOf<UTSysState>();
                byte[] rawBytes = new byte[structSize];
                fixed (byte* ptr = rawBytes)
                {
                    Buffer.MemoryCopy(state, ptr, structSize, structSize);
                }
                string rawHex = BitConverter.ToString(rawBytes).Replace("-", " ");

                _store.UpdateSystem(*state, sessionInfo.SessionId, sessionInfo.RemoteEndpoint);
                _events.Publish(new SystemStateChangedEvent(*state, sessionInfo.SessionId, sessionInfo.RemoteEndpoint, DateTime.Now, rawHex));
                _replyCallback?.Invoke(state->header);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SysStateHandler 处理异常");
            }
        }
    }
}
