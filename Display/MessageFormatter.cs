using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace YongChuanTools.Display
{
    /// <summary>
    /// 报文格式化器 — 把二进制报文翻译成人类可读的描述
    /// </summary>
    public static unsafe class MessageFormatter
    {
        /// <summary>
        /// 格式化字节数组为十六进制字符串
        /// </summary>
        public static string FormatHex(byte[] data, int bytesPerLine = 16)
        {
            if (data == null || data.Length == 0) return "(空)";

            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("X2"));
                if (i < data.Length - 1) sb.Append(' ');
                if ((i + 1) % bytesPerLine == 0 && i < data.Length - 1)
                    sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// 从 UTHeader 提取时间戳
        /// </summary>
        public static string FormatHeaderTime(UTHeader* header)
        {
            byte[] bts = new byte[6];
            for (int i = 0; i < 6; i++)
                bts[i] = header->time[i];
            return $"20{bts[5]:X2}-{bts[4]:X2}-{bts[3]:X2} {bts[2]:X2}:{bts[1]:X2}:{bts[0]:X2}";
        }

        /// <summary>
        /// 从字节数组提取地址字符串
        /// </summary>
        public static string FormatAddrBytes(byte* ptr, int len = 6)
        {
            byte[] bts = new byte[6];
            for (int i = 0; i < len; i++)
                bts[i] = ptr[i];
            return BitConverter.ToString(bts, 0, len).Replace("-", ":");
        }

        /// <summary>
        /// 格式化设备状态为可读描述
        /// </summary>
        public static string FormatEquipState(ushort state)
        {
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

        /// <summary>
        /// 格式化系统状态为可读描述
        /// </summary>
        public static string FormatSysState(ushort state)
        {
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

        /// <summary>
        /// 格式化设备类型
        /// </summary>
        public static string FormatEquipType(byte type)
        {
            try { return ((EnumEquipStateEquipType)type).ToString(); }
            catch { return $"未知({type})"; }
        }

        /// <summary>
        /// 格式化系统类型
        /// </summary>
        public static string FormatSysType(byte type)
        {
            try { return ((EnumEquipStateSysType)type).ToString(); }
            catch { return $"未知({type})"; }
        }

        /// <summary>
        /// 从 UTEquipState 提取描述字符串
        /// </summary>
        public static string GetEquipDescription(UTEquipState* state)
        {
            var desc = new byte[30];
            for (int i = 0; i < 30; i++)
                desc[i] = state->equipDesc[i];
            return Encoding.ASCII.GetString(desc).TrimEnd('\0').Trim();
        }

        /// <summary>
        /// 格式化地址类型编号
        /// </summary>
        public static string FormatAddrType(byte addrType)
        {
            return addrType switch
            {
                1 => "类型1 (sysAddr-区域-编号)",
                2 => "类型2 (纯数字)",
                3 => "类型3 (区域-编号)",
                4 => "类型4 (sysAddr-层-区-编号)",
                5 => "类型5 (十六进制sysAddr-编号)",
                6 => "类型6 (8位十六进制)",
                _ => $"未知({addrType})"
            };
        }

        /// <summary>
        /// 解析设备地址（按类型格式化）
        /// </summary>
        public static string FormatEquipAddr(UTEquipState* state)
        {
            var type = state->equipAddrType;
            var addr = state->equipAddr;
            var sysAddr = state->sysAddr;

            switch (type)
            {
                case 1: return $"{sysAddr}-{addr >> 16}-{(addr << 16) >> 16}";
                case 2: return $"{addr}";
                case 3: return $"{addr >> 16}-{(addr << 16) >> 16}";
                case 4: return $"{sysAddr}-{addr & 0xff}-{(addr & 0xff00) >> 8}-{addr >> 16}";
                case 5: return $"{sysAddr:X2}-{addr}";
                case 6: return $"{addr:X8}";
                default: return $"{sysAddr}-{addr >> 16}-{(addr << 16) >> 16}";
            }
        }
    }
}
