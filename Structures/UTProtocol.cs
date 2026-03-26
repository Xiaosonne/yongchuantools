using NetCoreServer;
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
                Console.WriteLine(" body " + body->Print());

                if (body->cmd == (byte)EnumCmdTypes.发送)
                {
                    int applen = body->length;
                    if (body->length > 0)
                    {
                        UTAppHeader* appheader = (UTAppHeader*)(start + Marshal.SizeOf<UTHeader>());
                        Console.WriteLine("appheader " + appheader->Print());
                        //todo 优化多条目信息
                        switch ((EnumsAppType)appheader->appType)
                        {
                            case EnumsAppType.上传消防系统状态:
                                UTSysState* state = (UTSysState*)start;
                                Console.WriteLine("上传消防系统状态############" + state->Print());
                                if (handlers != null)
                                    handlers.Where(s => s.type == EnumsAppType.上传消防系统状态).
                                         ToList().ForEach(s => s.handler(new IntPtr(state)));
                                else if (zerolenhandler != null)
                                    zerolenhandler(*body);
                                break;
                            case EnumsAppType.上传消防部件状态:

                                UTEquipState* state2 = (UTEquipState*)start;
                                Console.WriteLine("上传消防部件状态############" + state2->Print());
                                if (handlers != null)
                                    handlers.Where(s => s.type == EnumsAppType.上传消防部件状态).
                                    ToList().ForEach(s => s.handler(new IntPtr(state2)));
                                else if (zerolenhandler != null)
                                    zerolenhandler(*body);
                                break;
                            case EnumsAppType.上传消防部件模拟量:
                                //URTEquipState2* state2 = (URTEquipState2*)start;
                                //Console.WriteLine(state2->Print());
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
            catch (Exception arg)
            {
                Console.WriteLine($"OnReceived error {arg}");
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
    }

}
