using System.Runtime.InteropServices;

namespace YongChuanTools
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UTEquipAddr
    {
        public byte sysType;
        public byte sysAddr;
        public UInt32 equipAddr;
        public fixed byte timestamp[6];
    }

}
