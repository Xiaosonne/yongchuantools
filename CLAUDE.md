# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

- **Build**: `dotnet build`
- **Build Release**: `dotnet build -c Release`
- **Run**: `dotnet run`
- **Single file build for win-x64**: `dotnet publish -c Release -r win-x64 --self-contained`

## Project Overview

YongChuanTools is a .NET 6.0 TCP server application that implements the **UTProtocol** - a proprietary binary protocol for fire protection equipment monitoring systems.

### Architecture

```
Program.cs              # Entry point - creates TCP server on port 44370
NetTcpServer.cs         # TcpServer subclass - broadcasts commands to all sessions
NetTcpSession.cs        # TcpSession subclass - handles client connections and protocol parsing
IUserOperation.cs       # Interface for SendCommand and Init operations
Structures/
  UTProtocol.cs         # Core protocol handling - Encap/Decap, ReadSysState, ReadEquip, ReplyMessage
  UTHeader.cs           # Protocol header (seq, version, src/dst addr, cmd, length)
  UTEquipState.cs       # Fire equipment state structure
  UTSysState.cs         # Fire system state structure
  UTResponse.cs         # Acknowledgment response structure
  UTTail.cs             # Protocol tail with checksum
Enums/
  EnumReadTypes.cs      # Read commands (读取部件状态=62, 读取系统状态=61, etc.)
  EnumsAppType.cs       # App types (上传消防部件状态=2, 上传消防系统状态=1)
  EnumCmdTypes.cs       # Command types (发送, 请求, 响应, 确认)
```

### Protocol Structure

UTProtocol is a binary protocol with fixed-length structures:
- `UTHeader` - start marker, sequence, version, time, src/dst addresses, length, cmd
- `UTAppHeader` - app type and count
- Variable payload (system state, equipment state, etc.)
- `UTTail` - checksum and end marker (0x2323)

The `UTProtocol.Decap()` method parses incoming binary data and dispatches to handlers based on `EnumsAppType`. The `UTProtocol.Encap()` methods serialize structures back to bytes.

### Key Patterns

- Sessions are stored in `NetTcpServer.Sessions` dictionary, broadcast via `SendCommand()` and `Init()` methods
- `equipinfos` dictionary in NetTcpSession maps equipment addresses to their state
- Protocol parsing uses unsafe pointer operations and `Marshal` for struct serialization
- Equipment addresses are formatted differently based on `equipAddrType` (1-6)
