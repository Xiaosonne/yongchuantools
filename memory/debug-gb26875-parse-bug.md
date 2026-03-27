# GB26875 协议解析调试记录

**日期:** 2026-03-27
**项目:** YongChuanTools (.NET 6.0 TCP 服务器，GB26875 消防协议)

---

## 本次调试的核心问题

### 1. `init` 命令解析被跳过 — 字节长度不匹配

**现象:** `init` 命令输入后，hex 数据正确显示在控制台，但设备数量始终为 0，"部件状态上报" 没有出现。

**根因:** 用户提供的测试 hex 有 72 字节，但协议头 `header.length = 48`，意味着协议解析需要 `27 + 48 = 75` 字节。代码中的校验条件:

```csharp
if (header.length > 0 && bytes.Length >= HEADER_SIZE + header.length)
// 72 >= 75 → false → 解析被跳过
```

**经验:** `init` 命令的 hex 输入端必须提供完整长度的报文，否则解析会被 guard 跳过。调试时应首先检查日志中是否有 `跳过解析` 的 warning。

---

### 2. 两套并行的解析路径导致数据丢失

**现象:** `设备数量=0` 且无 "部件状态上报" 日志，尽管 RawMessageReceivedEvent 被正确发布（hex 显示正常）。

**根因:** 代码中存在两套解析路径:

1. **Live TCP 路径:** `TcpSessionWrapper.OnReceived()` → `UTProtocol.Decap()` → 直接调内部 `HandleEquipState()` 等
2. **Init 路径（旧的）:** `TcpServerWrapper.Init()` → `MessageDispatcher.Dispatch()` → `EquipStateHandler.Handle()`

两者的本质区别:
- `Decap()` 传递给 handler 的是 **整个 buffer 的起始指针** (`new IntPtr(start)`)，handler 直接 cast 为 `UTEquipState*`
- `Dispatcher.Dispatch()` 假设 payload 指向 **header 之后** 的位置（`ptr + HEADER_SIZE`），但实际传入时的数据结构不匹配

**经验:** 当一个功能（live TCP）正常工作但另一个（init）不正常时，首先要检查两者的数据路径是否一致，而不是假设它们走的是同一段代码。

---

### 3. Serilog 日志在 Console.WriteLine 之后消失

**现象:** 在 `Init()` 中添加了 `Log.Information()` 调试语句，但这些日志既不出现在控制台也不出现在日志文件中，然而 `System.Console.WriteLine()` 的输出正常显示。

**根因:** `Events.Publish(RawMessageReceivedEvent)` 触发了 `ConsoleDisplay.OnRawReceived()` 中的 `AnsiConsole.MarkupLine()`，该方法可能在内部调用了某些 Serilog 配置相关的操作，导致全局 logger 状态异常。后续的 `Log.Information()` 调用静默失败（无异常抛出但日志不写入）。

**经验:** 在事件处理回调中使用 `System.Console.WriteLine()` 而不是 `Log.Information()` 进行调试，避免日志系统被事件回调链污染。

---

### 4. xUnit NuGet 包不完整导致测试项目失败

**现象:** 尝试创建 xUnit 测试项目时，dotnet restore 成功但编译失败，错误为找不到 xunit 程序集。

**根因:** 机器的 NuGet 缓存 `E:/NuGet/` 中 xunit 2.9.x 版本只有元数据文件（nuspec），没有实际的 DLL。xunit 2.5.3 有完整 DLL 但版本较旧。

**经验:**
- 创建测试项目前先 `dotnet add package xunit --version 2.5.3` 确认能解析
- 或者用 self-contained 测试程序代替 NuGet 测试框架
- 机器缓存问题不一定是包版本的问题，可能只是缓存损坏

---

## 协议结构关键数据

```
Marshal.SizeOf<UTHeader>() = 27 bytes (confirmed at runtime)
  Offset 0-1:   start (ushort, 0x4040)
  Offset 2-3:   seq (ushort, little-endian)
  Offset 4-5:   version (ushort)
  Offset 6-11:  time[6] (Second/Minute/Hour/Day/Month/Year)
  Offset 12-17: srcAddr[6]
  Offset 18-23: dstAddr[6]
  Offset 24-25: length (ushort, payload length AFTER header)
  Offset 26:    cmd (byte: 2=发送, 3=确认, 4=请求, 5=响应)

UTAppHeader (at offset 27 of full buffer):
  Offset 0: appType (byte: 1=系统状态, 2=部件状态, 3=模拟量)
  Offset 1: appCount (byte)

UTEquipState (full struct = 75 bytes):
  Offset 0-26:   UTHeader
  Offset 27-28:  UTAppHeader
  Offset 29:     sysType (byte)
  Offset 30:     sysAddr (byte)
  Offset 31:     equipType (byte)
  Offset 32-35:  equipAddr (uint)
  Offset 36-37:  equipState (ushort)
  Offset 38:     equipAddrType (byte)
  Offset 39-68:  equipDesc[30]
  Offset 69-74:  timestamp[6]
```

---

## UTProtocol.Decap() 的数据传递方式

`Decap()` 传递给每个 handler 的是 **整个 buffer 的起始指针**，handler 直接 cast 为目标结构体指针:

```csharp
// Decap 内部:
s.handler(new IntPtr(start));  // start = buffer[0]

// Handler 内部:
var state = (UTEquipState*)ptr.ToPointer();
// ptr[0..26] = header, ptr[27..28] = appheader, ...
```

**注意:** `MessageDispatcher.Dispatch()` 读取 appHeader 的方式:
```csharp
var appType = (EnumsAppType)(((UTAppHeader*)payload.ToPointer())->appType);
```
它假设 payload 指向 UTAppHeader 的开始位置。如果传入 `new IntPtr(ptr + HEADER_SIZE)`（header 之后），则读取位置正确。但如果传入整个 buffer 起始位置（header 开始），则 appType 读取的是 header 的 start 字段，错误。

---

## 已确认的 pre-existing bugs

1. **`RecordMessage()` 从未被调用** — `总消息` 计数器永远是 0。`EquipmentStore.RecordMessage()` 只定义但从未在代码库中被调用。

2. **`TcpSessionWrapper.SendCommand()` 是空壳** — TODO stub，广播命令功能未实现。

3. **`ConsoleDisplay.OnEquipmentChanged` 使用栈上地址** — `var header = &state.header` 取栈变量地址，事件发布后该地址无效（async 或其他订阅者调用时会出界）。

---

## 修复内容摘要

| 文件 | 修改 |
|------|------|
| `Server/TcpServerWrapper.cs` | 重写 `Init()` 方法，新增 `SimulateWithDecap()` / `SimulateEquipState()` / `SimulateSysState()` / `SimulateAnalog()`，直接调用 `UTProtocol.Decap()` 走与 live TCP 相同的解析路径 |
| `Program.cs` | 新增 `test` 命令，硬编码 75 字节正确测试数据，方便快速验证解析是否正常 |

---

## 调试命令备忘

```bash
# 测试 init 命令（需要 75 字节完整报文）
printf 'init <75字节hex>
status
quit
' | timeout 10 dotnet run --project YongChuanTools.csproj

# 使用内置 test 命令（硬编码正确数据）
printf 'test
status
quit
' | timeout 10 dotnet run --project YongChuanTools.csproj

# 查看日志文件
tail -f bin/Debug/net6.0/logs/yongchuan-YYYYMMDD.log

# 杀死占用端口的进程
cmd //c "tasklist | findstr dotnet"  # 查找 PID
cmd //c "taskkill /F /PID <PID>"
```
