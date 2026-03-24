using System.Runtime.InteropServices;

namespace YongChuanTools
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UTTail
    {
        public UTTail()
        {
            checksum = 0;
            end = 0x2323;
        }
        public byte checksum;
        public ushort end;
    }
}
