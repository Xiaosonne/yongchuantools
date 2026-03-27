using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace YongChuanTools.Data
{
    /// <summary>
    /// 线程安全的设备状态存储
    /// </summary>
    public unsafe class EquipmentStore
    {
        private readonly ConcurrentDictionary<string, EquipmentSnapshot> _equipment = new();
        private readonly ConcurrentDictionary<(byte sysType, byte sysAddr), SystemSnapshot> _systems = new();
        private long _messageCount = 0;
        private long _equipCount = 0;
        private long _lastMessageId = 0;

        // ============ 设备状态 ============

        public void UpdateEquipment(UTEquipState state, string sessionId, string remoteEndpoint)
        {
            var addr = state.GetEquipAddr();
            var snap = new EquipmentSnapshot
            {
                Address = addr,
                FormattedAddress = FormatAddress(state),
                SysType = state.sysType,
                SysTypeName = ((EnumEquipStateSysType)state.sysType).ToString(),
                EquipType = state.equipType,
                EquipTypeName = ((EnumEquipStateEquipType)state.equipType).ToString(),
                EquipState = state.equipState,
                EquipStateName = DecodeEquipState(state.equipState),
                SessionId = sessionId,
                RemoteEndpoint = remoteEndpoint,
                LastUpdate = DateTime.Now,
                Description = GetDescription(state)
            };

            _equipment[addr] = snap;
            _equipCount = _equipment.Count;
            Log.Debug("设备状态更新: {Address} [{EquipType}] {State}",
                addr, snap.EquipTypeName, snap.EquipStateName);
        }

        public IEnumerable<EquipmentSnapshot> GetAllEquipment() => _equipment.Values;

        public EquipmentSnapshot? GetEquipment(string address) =>
            _equipment.GetValueOrDefault(address);

        public int EquipmentCount => _equipment.Count;

        // ============ 系统状态 ============

        public void UpdateSystem(UTSysState state, string sessionId, string remoteEndpoint)
        {
            var key = (state.sysType, state.sysAddr);
            var snap = new SystemSnapshot
            {
                SysType = state.sysType,
                SysTypeName = ((EnumEquipStateSysType)state.sysType).ToString(),
                SysAddr = state.sysAddr,
                SysState = state.sysState,
                SysStateName = DecodeSysState(state.sysState),
                SessionId = sessionId,
                RemoteEndpoint = remoteEndpoint,
                Timestamp = DateTime.Now
            };

            _systems[key] = snap;
            Log.Debug("系统状态更新: {SysType}[{SysAddr}] {State}",
                snap.SysTypeName, state.sysAddr, snap.SysStateName);
        }

        public IEnumerable<SystemSnapshot> GetAllSystems() => _systems.Values;

        // ============ 消息计数 ============

        public long RecordMessage(long id)
        {
            _messageCount++;
            _lastMessageId = id;
            return _messageCount;
        }

        public (long totalMessages, long totalEquipment, long lastMessageId, int connectedSessions) GetStats()
        {
            return (_messageCount, _equipCount, _lastMessageId, ConnectedSessionCount);
        }

        public int ConnectedSessionCount { get; set; }

        // ============ 辅助方法 ============

        private string FormatAddress(UTEquipState state)
        {
            switch (state.equipAddrType)
            {
                case 1: return $"{state.sysAddr}-{state.equipAddr >> 16}-{(state.equipAddr << 16) >> 16}";
                case 2: return $"{state.equipAddr}";
                case 3: return $"{state.equipAddr >> 16}-{(state.equipAddr << 16) >> 16}";
                case 4: return $"{state.sysAddr}-{state.equipAddr & 0xff}-{(state.equipAddr & 0xff00) >> 8}-{state.equipAddr >> 16}";
                case 5: return $"{state.sysAddr:x2}-{state.equipAddr}";
                case 6: return $"{state.equipAddr:x8}";
                default: return $"{state.sysAddr}-{state.equipAddr >> 16}-{(state.equipAddr << 16) >> 16}";
            }
        }

        private string DecodeEquipState(ushort state)
        {
            // GB26875 设备状态: 0=正常, 1=报警, 2=故障, 3=离线等
            return state switch
            {
                0 => "正常",
                1 => "报警",
                2 => "故障",
                3 => "屏蔽",
                4 => "正常(待确认)",
                _ => $"未知({state})"
            };
        }

        private string DecodeSysState(ushort state)
        {
            // 系统状态
            return state switch
            {
                0 => "正常",
                1 => "报警",
                2 => "主电故障",
                3 => "备电故障",
                4 => "总线故障",
                _ => $"未知({state})"
            };
        }

        private string GetDescription(UTEquipState state)
        {
            try
            {
                var desc = new byte[30];
                for (int i = 0; i < 30; i++)
                    desc[i] = state.equipDesc[i];
                return System.Text.Encoding.ASCII.GetString(desc).TrimEnd('\0').Trim();
            }
            catch
            {
                return "";
            }
        }
    }
}
