using Serilog;
using System;
using System.Runtime.InteropServices;
using YongChuanTools.Core;
using YongChuanTools.Data;

namespace YongChuanTools.Handlers
{
    /// <summary>
    /// 部件状态处理器 — 处理 appType=2 (上传消防部件状态)
    /// </summary>
    public class EquipStateHandler : IMessageHandler
    {
        public EnumsAppType AppType => EnumsAppType.上传消防部件状态;
        public bool Enabled => true;

        private readonly EventAggregator _events;
        private readonly EquipmentStore _store;
        private readonly Action<UTHeader> _replyCallback;

        public EquipStateHandler(EventAggregator events, EquipmentStore store, Action<UTHeader> replyCallback)
        {
            _events = events;
            _store = store;
            _replyCallback = replyCallback;
        }

        public unsafe void Handle(UTHeader header, IntPtr payload, SessionContext sessionInfo)
        {
            try
            {
                // payload 指向 UTEquipState（包含 header + appHeader + 数据）
                var state = (UTEquipState*)payload.ToPointer();

                Log.Information("部件状态上报 {@EquipInfo}",
                    new
                    {
                        Seq = state->header.seq,
                        SysType = (EnumEquipStateSysType)state->sysType,
                        SysAddr = state->sysAddr,
                        EquipType = (EnumEquipStateEquipType)state->equipType,
                        EquipAddr = state->GetEquipAddr(),
                        EquipState = state->equipState,
                        SessionId = sessionInfo.SessionId
                    });

                // 计算原始报文（用于 SignalR 实时推送）
                int structSize = Marshal.SizeOf<UTEquipState>();
                byte[] rawBytes = new byte[structSize];
                fixed (byte* ptr = rawBytes)
                {
                    Buffer.MemoryCopy(state, ptr, structSize, structSize);
                }
                string rawHex = BitConverter.ToString(rawBytes).Replace("-", " ");

                // 存储
                _store.UpdateEquipment(*state, sessionInfo.SessionId, sessionInfo.RemoteEndpoint);

                // 发布事件
                _events.Publish(new EquipmentStateChangedEvent(*state, sessionInfo.SessionId, sessionInfo.RemoteEndpoint, DateTime.Now, rawHex));

                // 回复确认
                _replyCallback?.Invoke(state->header);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "EquipStateHandler 处理异常");
            }
        }
    }
}
