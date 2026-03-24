using System.Runtime.InteropServices;

namespace YongChuanTools
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UTSysState
    {
        public UTHeader header;
        public UTAppHeader appHeader;
        public byte sysType;
        public byte sysAddr;
        public ushort sysState;
        public fixed byte time[6];
        public string Print()
        {
            byte[] bts = new byte[6];
            fixed (byte* ptrtime = time)
            {
                Marshal.Copy(new IntPtr(ptrtime), bts, 0, 6);
            }
            return $"{header.Print()} {appHeader.Print()}  sysType:{sysType.ToString("x2")} {((EnumEquipStateSysType)sysType).ToString()} sysAddr:{sysAddr.ToString("x2")} sysState:{sysState.ToString("x4")} time:{string.Join(" - ", bts.Select(q => q.ToString("x2")))}";
        }
    }

}
