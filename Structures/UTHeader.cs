using System.Runtime.InteropServices;
using System.Text;

namespace YongChuanTools
{
    public struct UTAppHeader
    {
        public byte appType;
        public byte appCount;
        public string Print()
        {
            return $"appType:{appType.ToString("x2")} {((EnumsAppType)appType).ToString()} appCount:{appCount}";
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UTHeader
    {
        public ushort start;
        public ushort seq;
        public ushort version;
        public fixed byte time[6];
        public fixed byte srcAddr[6];
        public fixed byte dstAddr[6];
        public ushort length;
        public byte cmd;

        public string Print()
        {
            StringBuilder sb = new StringBuilder($"start:{start.ToString("x4")} seq:{seq.ToString("x4")} version:{version.ToString("x4")}");
            byte[] bts = new byte[6];
            fixed (byte* ptrtime = time)
            {
                Marshal.Copy(new IntPtr(ptrtime), bts, 0, 6);
            }
            sb.Append(" time:" + string.Join("-", bts.Select(q => q.ToString())));
            fixed (byte* ptrtime = srcAddr)
            {
                Marshal.Copy(new IntPtr(ptrtime), bts, 0, 6);
            }
            sb.Append(" srcAddr:" + string.Join("-", bts.Select(q => q.ToString("x2"))));
            fixed (byte* ptrtime = dstAddr)
            {
                Marshal.Copy(new IntPtr(ptrtime), bts, 0, 6);
            }
            sb.Append(" dstAddr:" + string.Join("-", bts.Select(q => q.ToString("x2"))));
            sb.Append($" appLen:{length} cmd:{((EnumCmdTypes)cmd).ToString()} ");
            return sb.ToString();
        }
        public unsafe UTHeader GetResponseHeader()
        {
            var temp = this;

            byte[] tempaddr = new byte[6];
            fixed (byte* src1 = this.srcAddr)
            {
                fixed (byte* dest1 = this.dstAddr)
                {
                    Marshal.Copy(new IntPtr(src1), tempaddr, 0, 6);
                    Marshal.Copy(tempaddr, 0, new IntPtr(temp.dstAddr), 6);

                    Marshal.Copy(new IntPtr(dest1), tempaddr, 0, 6);
                    Marshal.Copy(tempaddr, 0, new IntPtr(temp.srcAddr), 6);
                }
            }


            Marshal.Copy(UTProtocol.GetNowTimetag(), 0, new IntPtr(temp.time), 6);
            temp.length = 0;
            temp.cmd = (byte)EnumCmdTypes.响应;

            return temp;
        }
        public byte Checksum()
        {
            return UTProtocol.Checksum(this);
        }
    }

}
