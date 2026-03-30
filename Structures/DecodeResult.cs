namespace YongChuanTools;

/// <summary>
/// Bit-level state description for a single bit position
/// </summary>
public class DecodeBit
{
    public int Bit { get; init; }
    public string Name { get; init; } = "";
    public int Value { get; init; } // 0 or 1
    public string Desc { get; init; } = "";
}

/// <summary>
/// A single decoded field with its wire-bytes, parsed value, and meaning
/// </summary>
public class DecodeField
{
    public string Bytes { get; init; } = "";  // hex string, space-separated, wire order
    public string Value { get; init; } = "";  // human-readable value
    public string Desc { get; init; } = "";   // field meaning
}

/// <summary>
/// A state field that includes per-bit breakdown
/// </summary>
public class DecodeStateField : DecodeField
{
    public List<DecodeBit> Bits { get; init; } = new();
}

/// <summary>
/// The complete decoded protocol tree
/// </summary>
public record DecodeResult
{
    public bool Valid { get; init; }
    public string? Error { get; init; }
    public string? AppTypeName { get; init; }
    public int RawAppType { get; init; }

    public DecodeField? Start { get; init; }
    public DecodeField? Length { get; init; }
    public DecodeField? Seq { get; init; }
    public DecodeField? Time { get; init; }
    public DecodeField? SrcAddr { get; init; }
    public DecodeField? DstAddr { get; init; }
    public DecodeField? Cmd { get; init; }

    public DecodeField? AppHeaderType { get; init; }
    public DecodeField? AppHeaderCount { get; init; }

    // M1 fields
    public DecodeField? SysType { get; init; }
    public DecodeField? SysAddr { get; init; }
    public DecodeStateField? SysState { get; init; }
    public DecodeField? DataTime { get; init; }

    // M2 fields
    public DecodeField? EquipType { get; init; }
    public DecodeField? EquipAddr { get; init; }
    public DecodeStateField? EquipState { get; init; }
    public DecodeField? AddrType { get; init; }
    public DecodeField? EquipDesc { get; init; }
    public DecodeField? EquipTime { get; init; }

    // M3 (not implemented)
    public object? Data { get; init; }

    public DecodeField? Checksum { get; init; }
    public DecodeField? End { get; init; }

    public string RawHex { get; init; } = "";
    public List<string>? Warnings { get; init; }
}
