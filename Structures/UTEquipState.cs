using System.Runtime.InteropServices;
using System.Text;

namespace YongChuanTools
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UTEquipState
    {
        public UTHeader header;
        public UTAppHeader appheader;
        public byte sysType;
        public byte sysAddr;
        public byte equipType;
        public UInt32 equipAddr;
        public ushort equipState;
        public byte equipAddrType;
        public fixed byte equipDesc[30];
        public fixed byte timestamp[6];
        public UTEquipAddr GetReadAddr()
        {
            var addr= new UTEquipAddr
            {
                sysType = sysType,
                sysAddr = sysAddr,
                equipAddr = equipAddr,
            };
            Marshal.Copy(UTProtocol.GetNowTimetag(), 0, new IntPtr(addr.timestamp), 6);
            return addr;
        }
        public string GetEquipAddr()
        {
            switch (equipAddrType)
            {
                case 1:
                    return $"{sysAddr}-{equipAddr >> 16}-{(equipAddr << 16) >> 16}";
                case 2:
                    return $"{equipAddr}";
                case 3:
                    return $"{equipAddr >> 16}-{(equipAddr << 16) >> 16}";
                case 4:
                    return $"{sysAddr}-{equipAddr & 0xff}-{(equipAddr & 0xff00) >> 8}-{(equipAddr) >> 16}";
                case 5:
                    return $"{sysAddr.ToString("x2")}-{equipAddr}";
                case 6:
                    return $"{equipAddr.ToString("x8")}";
                default:
                    return $"{sysAddr}-{equipAddr >> 16}-{(equipAddr << 16) >> 16}";
            }
        }
        public string Print()
        {
            string str = "";
            fixed (byte* str1 = equipDesc)
            {
                str = Encoding.ASCII.GetString(str1, 30);
            }
            return $"{header.Print()} {appheader.Print()} code:{sysType}/{sysAddr}/{equipType}/{equipAddr&0xff}.{(equipAddr>>8)&0xff}.{(equipAddr >> 16) & 0xff}.{(equipAddr >> 24) & 0xff} sysType:{sysType.ToString("x2")} {((EnumEquipStateSysType)sysType).ToString()}  sysAddr:{sysAddr.ToString("x2")} equipType:{equipType.ToString("x2")} {((EnumEquipStateEquipType)equipType).ToString()} equipState:{equipState.ToString("x4")} equipAddrType:{equipAddrType.ToString("x2")} {((EnumEquipStateEquipType)equipAddrType).ToString()} equipAddr:{GetEquipAddr()} equipDesc:{str}";
        }
    }

}
