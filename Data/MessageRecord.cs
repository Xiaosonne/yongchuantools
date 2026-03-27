using System;

namespace YongChuanTools.Data
{
    /// <summary>
    /// 消息记录 — 完整记录每次收发的原始报文和解析结果
    /// </summary>
    public class MessageRecord
    {
        public long Id { get; init; }
        public DateTime Timestamp { get; init; }
        public string Direction { get; init; } = ""; // "Received" | "Sent"
        public string RemoteEndpoint { get; init; } = "";
        public EnumsAppType AppType { get; init; }
        public string AppTypeName => AppType.ToString();
        public byte[] RawData { get; init; } = Array.Empty<byte>();
        public string RawHex => BitConverter.ToString(RawData).Replace("-", " ");
        public UTHeader? Header { get; init; }
        public string? DecodedSummary { get; init; }
        public bool IsValid { get; init; }
        public string? Error { get; init; }

        public string DirectionIcon => Direction == "Received" ? "📥" : "📤";
        public string TimestampStr => Timestamp.ToString("HH:mm:ss.fff");
        public string TimestampFull => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
    }

    /// <summary>
    /// 设备状态快照 — 用于仪表盘展示
    /// </summary>
    public class EquipmentSnapshot
    {
        public string Address { get; init; } = "";
        public string FormattedAddress { get; init; } = "";
        public byte SysType { get; init; }
        public string SysTypeName { get; init; } = "";
        public byte EquipType { get; init; }
        public string EquipTypeName { get; init; } = "";
        public ushort EquipState { get; init; }
        public string EquipStateName { get; init; } = "";
        public string SessionId { get; init; } = "";
        public string RemoteEndpoint { get; init; } = "";
        public DateTime LastUpdate { get; init; }
        public string Description { get; init; } = "";
    }

    /// <summary>
    /// 系统状态快照
    /// </summary>
    public class SystemSnapshot
    {
        public byte SysType { get; init; }
        public string SysTypeName { get; init; } = "";
        public byte SysAddr { get; init; }
        public ushort SysState { get; init; }
        public string SysStateName { get; init; } = "";
        public string SessionId { get; init; } = "";
        public string RemoteEndpoint { get; init; } = "";
        public DateTime Timestamp { get; init; }
    }
}
