using NetCoreServer;
using Serilog;
using System;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace YongChuanTools
{
    public static unsafe partial class UTProtocol
    {


        public static byte Checksum<T>(T stru) where T : struct
        {
            byte[] buf = new byte[Marshal.SizeOf<T>()];
            fixed (byte* ptr = buf)
            {
                byte* start = (byte*)&stru;
                Marshal.Copy(new IntPtr(start + 2), buf, 0, Marshal.SizeOf<T>() - 2);
            }
            return (byte)(buf.Sum(t => t) & 0xff);
        }
        public static byte Checksum(byte[] totaldata)
        {
            int arr = 0;
            for (int i = 2; i < (totaldata.Length - 3); i++)
            {
                arr += totaldata[i];
            }
            return (byte)(arr & 0xff);
        }

        public static byte[] ReadSysState(UTHeader header, EnumReadTypes readType, UTSysAddr sys)
        {
            var resp = header.GetResponseHeader();
            var appheader = new UTAppHeader
            {
                appCount = 1,
                appType = (byte)readType,
            };
            resp.cmd = (byte)EnumCmdTypes.请求;

            UTTail tail = new UTTail();
            var l1 = Marshal.SizeOf<UTHeader>();
            var l2 = Marshal.SizeOf<UTAppHeader>();
            var l3 = Marshal.SizeOf<UTSysAddr>();
            var l4 = Marshal.SizeOf<UTTail>();
            resp.length = (byte)(l2 + l3);
            byte[] total = new byte[l1 + l2 + l3 + l4];
            Encap(total, 0, resp, appheader, sys, tail);
            var checksum = Checksum(total);
            total[total.Length - 3] = checksum;
            return total;
        }
        public static byte[] ReadEquip(UTHeader header, EnumReadTypes readType, UTEquipAddr equip)
        {
            var resp = header.GetResponseHeader();
            resp.cmd = (byte)EnumCmdTypes.请求;
            UTAppHeader appheader = new UTAppHeader
            {
                appCount = 1,
                appType = (byte)readType,
            };
            UTTail tail = new UTTail();
            var l1 = Marshal.SizeOf<UTHeader>();
            var l2 = Marshal.SizeOf<UTAppHeader>();
            var l3 = Marshal.SizeOf<UTEquipAddr>();
            var l4 = Marshal.SizeOf<UTTail>();
            resp.length = (byte)(l2 + l3);
            byte[] total = new byte[l1 + l2 + l3 + l4];
            Encap(total, 0, resp, appheader, equip, tail);
            var checksum = Checksum(total);
            total[total.Length - 3] = checksum;
            return total;
        }


        public static void Encap<T1>(byte[] dst, int offset, T1 t1)
        {
            fixed (byte* start = dst)
            {
                Marshal.StructureToPtr(t1, new IntPtr(start + offset), false);
            }
        }
        public static void Encap<T1, T2>(byte[] dst, int offset, T1 t1, T2 t2)
        {
            Encap(dst, offset, t1);
            Encap(dst, offset + Marshal.SizeOf<T1>(), t2);
        }
        public static void Encap<T1, T2, T3>(byte[] dst, int offset, T1 t1, T2 t2, T3 t3)
        {
            Encap(dst, offset, t1);
            Encap(dst, offset + Marshal.SizeOf<T1>(), t2, t3);

        }
        public static void Encap<T1, T2, T3, T4>(byte[] dst, int offset, T1 t1, T2 t2, T3 t3, T4 t4)
        {
            Encap(dst, offset, t1);
            Encap(dst, offset + Marshal.SizeOf<T1>(), t2, t3, t4);
        }

        public static void Decap(byte[] buffer, long offset, long size, Action<UTHeader> zerolenhandler, params (EnumsAppType type, Action<IntPtr> handler)[] handlers)
        {
            try
            {
                var ptr1 = Marshal.AllocHGlobal((int)size);
                Marshal.Copy(buffer, (int)offset, ptr1, (int)size);
                byte* start = (byte*)ptr1.ToPointer();

                UTHeader* body = (UTHeader*)start;
                Log.Information("Decap: size={Size} cmd=0x{cmd:X2} length={Length} seq={Seq}", size, body->cmd, body->length, body->seq);

                if (body->cmd == (byte)EnumCmdTypes.发送)
                {
                    if (body->length > 0)
                    {
                        // UTHeader actual protocol size is 23 bytes; Marshal.SizeOf may return 24 due to struct alignment padding
                        UTAppHeader* appheader = (UTAppHeader*)(start + Marshal.SizeOf<UTHeader>());
                        Log.Information("Decap: appType={AppType} appCount={AppCount} headerSize={HS}", appheader->appType, appheader->appCount, Marshal.SizeOf<UTHeader>());
                        switch ((EnumsAppType)appheader->appType)
                        {
                            case EnumsAppType.上传消防系统状态:
                                if (handlers != null)
                                    handlers.Where(s => s.type == EnumsAppType.上传消防系统状态).
                                         ToList().ForEach(s => s.handler(new IntPtr(start)));
                                else if (zerolenhandler != null)
                                    zerolenhandler(*body);
                                break;
                            case EnumsAppType.上传消防部件状态:
                                if (handlers != null)
                                    handlers.Where(s => s.type == EnumsAppType.上传消防部件状态).
                                    ToList().ForEach(s => s.handler(new IntPtr(start)));
                                else if (zerolenhandler != null)
                                    zerolenhandler(*body);
                                break;
                            case EnumsAppType.上传消防部件模拟量:
                                zerolenhandler(*body);
                                break;
                            default:
                                zerolenhandler(*body);
                                break;
                        }
                    }
                    else
                    {
                        if (zerolenhandler != null)
                            zerolenhandler(*body);
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "OnReceived error");
            }
        }
        public static byte[] ReplyMessage(UTHeader header)
        {
            var temp = header.GetResponseHeader();
            byte[] buf2 = new byte[Marshal.SizeOf<UTResponse>()];
            temp.cmd = (byte)EnumCmdTypes.确认;
            UTResponse resp = new UTResponse();
            resp.header = temp;
            resp.check = temp.Checksum();
            resp.end = 0x2323;
            fixed (byte* ptr = buf2)
            {
                Marshal.StructureToPtr<UTResponse>(resp, new IntPtr(ptr), false);
            }
            return buf2;
        }

        public static byte[] GetNowTimetag()
        {
            DateTime dtNow = DateTime.Now;
            return new byte[6]
            {
            Convert.ToByte(dtNow.Second),
            Convert.ToByte(dtNow.Minute),
            Convert.ToByte(dtNow.Hour),
            Convert.ToByte(dtNow.Day),
            Convert.ToByte(dtNow.Month),
            Convert.ToByte(dtNow.Year.ToString().Substring(2, 2))
            };
        }

        // ─── Bit-level state decode ───────────────────────────────────────────────

        public static DecodeStateField DecodeEquipStateBits(ushort state)
        {
            var bits = new List<DecodeBit>
            {
                new() { Bit = 0, Name = "正常",   Value = (state & 0x0001) != 0 ? 1 : 0, Desc = "bit0=1: 正常" },
                new() { Bit = 1, Name = "报警",   Value = (state & 0x0002) != 0 ? 1 : 0, Desc = "bit1=1: 报警" },
                new() { Bit = 2, Name = "故障",   Value = (state & 0x0004) != 0 ? 1 : 0, Desc = "bit2=1: 故障" },
                new() { Bit = 3, Name = "屏蔽",   Value = (state & 0x0008) != 0 ? 1 : 0, Desc = "bit3=1: 屏蔽" },
            };
            return new DecodeStateField
            {
                Bytes = $"{(byte)(state & 0xFF):X2} {(byte)(state >> 8):X2}",
                Value = $"0x{state:X4}",
                Bits = bits,
                Desc = "设备状态字 — bit 位定义(GB26875.3-2011)"
            };
        }

        public static DecodeStateField DecodeSysStateBits(ushort state)
        {
            var bits = new List<DecodeBit>
            {
                new() { Bit = 0, Name = "正常",     Value = (state & 0x0001) != 0 ? 1 : 0, Desc = "bit0=1: 正常" },
                new() { Bit = 1, Name = "报警",     Value = (state & 0x0002) != 0 ? 1 : 0, Desc = "bit1=1: 报警" },
                new() { Bit = 2, Name = "主电故障", Value = (state & 0x0004) != 0 ? 1 : 0, Desc = "bit2=1: 主电故障" },
                new() { Bit = 3, Name = "备电故障", Value = (state & 0x0008) != 0 ? 1 : 0, Desc = "bit3=1: 备电故障" },
                new() { Bit = 4, Name = "总线故障", Value = (state & 0x0010) != 0 ? 1 : 0, Desc = "bit4=1: 总线故障" },
                new() { Bit = 5, Name = "设备掉线", Value = (state & 0x0020) != 0 ? 1 : 0, Desc = "bit5=1: 设备掉线" },
                new() { Bit = 6, Name = "屏蔽",     Value = (state & 0x0040) != 0 ? 1 : 0, Desc = "bit6=1: 屏蔽" },
            };
            return new DecodeStateField
            {
                Bytes = $"{(byte)(state & 0xFF):X2} {(byte)(state >> 8):X2}",
                Value = $"0x{state:X4}",
                Bits = bits,
                Desc = "系统状态字 — bit 位定义(GB26875.3-2011, 需与设备厂家确认bit位含义)"
            };
        }

        // ─── State name formatting ───────────────────────────────────────────────

        /// <summary>Returns "[正常] [报警] [故障]" style label string from equip state bits</summary>
        public static string FormatEquipStateName(ushort state)
        {
            var active = new List<string>();
            if ((state & 0x0001) != 0) active.Add("[正常]");
            if ((state & 0x0002) != 0) active.Add("[报警]");
            if ((state & 0x0004) != 0) active.Add("[故障]");
            if ((state & 0x0008) != 0) active.Add("[屏蔽]");
            return active.Count > 0 ? string.Join(" ", active) : "[]";
        }

        /// <summary>Returns "[正常] [报警] [主电故障]..." style label string from sys state bits</summary>
        public static string FormatSysStateName(ushort state)
        {
            var active = new List<string>();
            if ((state & 0x0001) != 0) active.Add("[正常]");
            if ((state & 0x0002) != 0) active.Add("[报警]");
            if ((state & 0x0004) != 0) active.Add("[主电故障]");
            if ((state & 0x0008) != 0) active.Add("[备电故障]");
            if ((state & 0x0010) != 0) active.Add("[总线故障]");
            if ((state & 0x0020) != 0) active.Add("[设备掉线]");
            if ((state & 0x0040) != 0) active.Add("[屏蔽]");
            return active.Count > 0 ? string.Join(" ", active) : "[]";
        }

        // ─── Full-frame decode ────────────────────────────────────────────────────

        /// <summary>
        /// Decode a complete hex frame (with or without 0x5555 start marker).
        /// Supports both standard (0x5555) and proprietary (0x4040) start codes.
        /// Returns null if the frame cannot be decoded.
        /// </summary>
        public static DecodeResult? DecodeFrame(string hexInput)
        {
            byte[] raw;
            try
            {
                raw = HexToBytes(hexInput);
            }
            catch
            {
                return null;
            }

            if (raw.Length < 32)
                return new DecodeResult { Valid = false, Error = "TRUNCATED", RawHex = hexInput.ToUpperInvariant().Replace(" ", "") };

            // Detect start marker
            ushort startMarker = (ushort)(raw[0] | (raw[1] << 8));
            bool isStandard = startMarker == 0x5555;
            bool isProprietary = startMarker == 0x4040;

            if (!isStandard && !isProprietary)
                return new DecodeResult { Valid = false, Error = "INVALID_START", RawHex = Hexify(raw) };

            int offset = isStandard ? 0 : 0;

            // Read UTHeader fields (fixed 27 bytes)
            // struct UTHeader: start(2)+seq(2)+version(2)+time(6)+srcAddr(6)+dstAddr(6)+length(2)+cmd(1) = 27
            ushort seq = (ushort)(raw[offset + 4] | (raw[offset + 5] << 8));
            ushort length = (ushort)(raw[offset + 23] | (raw[offset + 24] << 8)); // little-endian ushort
            byte cmd = raw[offset + 26];

            // Read 6-byte time field
            byte[] timeBytes = new byte[6];
            Array.Copy(raw, offset + 6, timeBytes, 0, 6);
            string timeStr = DecodeBcdTime(timeBytes);

            // Read addresses (6 bytes each)
            byte[] srcAddr = new byte[6];
            byte[] dstAddr = new byte[6];
            Array.Copy(raw, offset + 12, srcAddr, 0, 6);
            Array.Copy(raw, offset + 18, dstAddr, 0, 6);

            // Read AppHeader (2 bytes: appType + appCount)
            int appHeaderOffset = offset + 27;
            if (appHeaderOffset + 2 > raw.Length)
                return new DecodeResult { Valid = false, Error = "TRUNCATED", RawHex = Hexify(raw) };

            byte appType = raw[appHeaderOffset];
            byte appCount = raw[appHeaderOffset + 1];

            string appTypeName = appType switch
            {
                1 => "上传消防系统状态",
                2 => "上传消防部件状态",
                3 => "上传消防部件模拟量",
                _ => $"未知({appType})"
            };

            var result = new DecodeResult
            {
                Valid = true,
                RawAppType = appType,
                AppTypeName = appTypeName,
                RawHex = Hexify(raw),
                Warnings = new List<string>(),
                Start = new DecodeField { Bytes = BytesToHex(raw, offset, 2), Value = $"0x{startMarker:X4}", Desc = "起始标记" },
                Seq = new DecodeField { Bytes = BytesToHex(raw, offset + 4, 2), Value = seq.ToString(), Desc = "序列号" },
                Time = new DecodeField { Bytes = BytesToHex(raw, offset + 6, 6), Value = timeStr, Desc = "报文时间(BCD码, 6字节: 年月日时分秒)" },
                SrcAddr = new DecodeField { Bytes = BytesToHex(raw, offset + 12, 6), Value = FormatAddr(srcAddr), Desc = "源地址(6字节)" },
                DstAddr = new DecodeField { Bytes = BytesToHex(raw, offset + 18, 6), Value = FormatAddr(dstAddr), Desc = "目的地址(6字节)" },
                Length = new DecodeField { Bytes = BytesToHex(raw, offset + 23, 2), Value = length.ToString(), Desc = "报文长度(低字节在前, little-endian)" },
                Cmd = new DecodeField { Bytes = raw[offset + 26].ToString("X2"), Value = $"0x{cmd:X2}", Desc = $"命令: {(EnumCmdTypes)cmd}" },
                AppHeaderType = new DecodeField { Bytes = raw[appHeaderOffset].ToString("X2"), Value = appType.ToString(), Desc = $"应用数据类型: {appTypeName}" },
                AppHeaderCount = new DecodeField { Bytes = raw[appHeaderOffset + 1].ToString("X2"), Value = appCount.ToString(), Desc = "应用数据个数" },
            };

            // Read data section based on appType
            int dataOffset = appHeaderOffset + 2;

            if (appType == 1 && dataOffset + 10 <= raw.Length)
            {
                // M1: 系统状态 (10 bytes: sysType(1)+sysAddr(1)+sysState(2)+time(6))
                byte sysType = raw[dataOffset];
                byte sysAddr = raw[dataOffset + 1];
                ushort sysState = (ushort)(raw[dataOffset + 2] | (raw[dataOffset + 3] << 8));
                byte[] dataTime = new byte[6];
                Array.Copy(raw, dataOffset + 4, dataTime, 0, 6);

                string sysTypeName = Enum.IsDefined(typeof(EnumEquipStateSysType), sysType)
                    ? ((EnumEquipStateSysType)sysType).ToString()
                    : $"未知({sysType})";

                result = result with
                {
                    SysType = new DecodeField { Bytes = raw[dataOffset].ToString("X2"), Value = sysTypeName, Desc = "系统类型" },
                    SysAddr = new DecodeField { Bytes = raw[dataOffset + 1].ToString("X2"), Value = sysAddr.ToString(), Desc = "系统地址" },
                    SysState = DecodeSysStateBits(sysState),
                    DataTime = new DecodeField { Bytes = BytesToHex(dataTime, 0, 6), Value = DecodeBcdTime(dataTime), Desc = "设备自报时间(BCD)" }
                };
            }
            else if (appType == 2 && dataOffset + 47 <= raw.Length)
            {
                // M2: 设备状态 (47 bytes: sysType(1)+sysAddr(1)+equipType(1)+equipAddr(4)+equipState(2)+addrType(1)+desc(30)+time(6))
                byte sysType = raw[dataOffset];
                byte sysAddr = raw[dataOffset + 1];
                byte equipType = raw[dataOffset + 2];
                uint equipAddr = BitConverter.ToUInt32(raw, dataOffset + 3);
                ushort equipState = (ushort)(raw[dataOffset + 7] | (raw[dataOffset + 8] << 8));
                byte addrType = raw[dataOffset + 9];
                byte[] equipDesc = new byte[30];
                Array.Copy(raw, dataOffset + 10, equipDesc, 0, 30);
                byte[] equipTime = new byte[6];
                Array.Copy(raw, dataOffset + 40, equipTime, 0, 6);

                string sysTypeName = Enum.IsDefined(typeof(EnumEquipStateSysType), sysType)
                    ? ((EnumEquipStateSysType)sysType).ToString()
                    : $"未知({sysType})";
                string equipTypeName = Enum.IsDefined(typeof(EnumEquipStateEquipType), equipType)
                    ? ((EnumEquipStateEquipType)equipType).ToString()
                    : $"未知({equipType})";

                result = result with
                {
                    SysType = new DecodeField { Bytes = raw[dataOffset].ToString("X2"), Value = sysTypeName, Desc = "系统类型" },
                    SysAddr = new DecodeField { Bytes = raw[dataOffset + 1].ToString("X2"), Value = sysAddr.ToString(), Desc = "系统地址" },
                    EquipType = new DecodeField { Bytes = raw[dataOffset + 2].ToString("X2"), Value = equipTypeName, Desc = "设备类型" },
                    EquipAddr = new DecodeField { Bytes = BytesToHex(raw, dataOffset + 3, 4), Value = FormatEquipAddr(equipAddr, addrType, sysAddr), Desc = "设备地址" },
                    EquipState = DecodeEquipStateBits(equipState),
                    AddrType = new DecodeField { Bytes = raw[dataOffset + 9].ToString("X2"), Value = addrType.ToString(), Desc = $"地址类型({addrType})" },
                    EquipDesc = new DecodeField { Bytes = BytesToHex(equipDesc, 0, 30), Value = DecodeAscii(equipDesc), Desc = "设备描述(ASCII)" },
                    EquipTime = new DecodeField { Bytes = BytesToHex(equipTime, 0, 6), Value = DecodeBcdTime(equipTime), Desc = "设备时间(BCD)" }
                };
            }
            else if (appType == 3)
            {
                result = result with { Data = null };
            }
            else
            {
                return new DecodeResult { Valid = false, Error = "TRUNCATED", RawHex = Hexify(raw) };
            }

            // Read tail (checksum byte + end ushort)
            int tailOffset = raw.Length - 3;
            if (tailOffset >= 0)
            {
                byte checksum = raw[tailOffset];
                ushort endMarker = (ushort)(raw[tailOffset + 1] | (raw[tailOffset + 2] << 8));
                result = result with
                {
                    Checksum = new DecodeField { Bytes = checksum.ToString("X2"), Value = $"0x{checksum:X2}", Desc = "校验和" },
                    End = new DecodeField { Bytes = $"{(byte)endMarker:X2} {(byte)(endMarker >> 8):X2}", Value = $"0x{endMarker:X4}", Desc = "结束标记" }
                };

                // Verify checksum
                byte expectedChecksum = Checksum(raw);
                if (checksum != expectedChecksum)
                    result.Warnings!.Add($"CHECKSUM_MISMATCH: expected 0x{expectedChecksum:X2}, got 0x{checksum:X2}");
            }

            return result;
        }

        // ─── Helpers ───────────────────────────────────────────────────────────────

        private static string BytesToHex(byte[] data, int offset, int count)
        {
            var parts = new string[count];
            for (int i = 0; i < count; i++)
                parts[i] = data[offset + i].ToString("X2");
            return string.Join(" ", parts);
        }

        private static string Hexify(byte[] data) =>
            BitConverter.ToString(data).Replace("-", " ");

        private static byte[] HexToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "").Replace("\r", "").Replace("\n", "").Trim();
            if (hex.Length % 2 != 0) throw new FormatException("Invalid hex length");
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        private static string DecodeBcdTime(byte[] bcd)
        {
            // BCD: each byte encodes two decimal digits (high nibble = tens, low nibble = units)
            // Format: YYYY MM DD HH mm ss  (6 bytes)
            try
            {
                int sec = (bcd[0] & 0x0F) + ((bcd[0] >> 4) * 10);
                int min = (bcd[1] & 0x0F) + ((bcd[1] >> 4) * 10);
                int hour = (bcd[2] & 0x0F) + ((bcd[2] >> 4) * 10);
                int day = (bcd[3] & 0x0F) + ((bcd[3] >> 4) * 10);
                int month = (bcd[4] & 0x0F) + ((bcd[4] >> 4) * 10);
                int year = (bcd[5] & 0x0F) + ((bcd[5] >> 4) * 10) + 2000;
                return $"{year:D4}-{month:D2}-{day:D2} {hour:D2}:{min:D2}:{sec:D2}";
            }
            catch
            {
                return BitConverter.ToString(bcd).Replace("-", " ");
            }
        }

        private static string DecodeAscii(byte[] data)
        {
            // Strip null and non-printable chars
            var sb = new System.Text.StringBuilder();
            foreach (var b in data)
                if (b >= 0x20 && b <= 0x7E) sb.Append((char)b);
            return sb.ToString().Trim();
        }

        private static string FormatAddr(byte[] addr) =>
            string.Join(":", addr.Select(b => b.ToString("X2")));

        private static string FormatEquipAddr(uint equipAddr, byte addrType, byte sysAddr)
        {
            return addrType switch
            {
                1 => $"{sysAddr}-{(ushort)(equipAddr >> 16):D4}-{(ushort)(equipAddr & 0xFFFF):D4}",
                2 => $"{equipAddr}",
                3 => $"{(ushort)(equipAddr >> 16)}-{(ushort)(equipAddr & 0xFFFF)}",
                4 => $"{sysAddr}-{equipAddr & 0xFF}-{(equipAddr >> 8) & 0xFF}-{(ushort)(equipAddr >> 16)}",
                5 => $"{sysAddr:X2}-{equipAddr}",
                6 => $"{equipAddr:X8}",
                _ => $"{sysAddr}-{(ushort)(equipAddr >> 16):X4}-{(ushort)(equipAddr & 0xFFFF):X4}"
            };
        }
    }
}
