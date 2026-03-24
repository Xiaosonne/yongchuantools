using System.Runtime.InteropServices;

namespace YongChuanTools
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UTSysAddr
    {
        public byte sysType;
        public byte sysAddr;
    }

}
