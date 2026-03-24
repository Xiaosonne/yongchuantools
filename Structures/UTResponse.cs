using System.Runtime.InteropServices;

namespace YongChuanTools
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UTResponse
    {
        public UTHeader header;
        public byte check;
        public ushort end;
    }

}
