using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using NetCoreServer;
using static YongChuanTools.UTProtocol;

namespace YongChuanTools
{


    internal class NetTcpSession : TcpSession, IUserOperation
    {
        private static readonly ConcurrentDictionary<string, byte[]> SendTemp = new ConcurrentDictionary<string, byte[]>();

        private string ipport;

        private string guid;

        public NetTcpSession(TcpServer server)
            : base(server)
        {
        }

        protected override void OnConnected()
        {
            ipport = base.Socket.RemoteEndPoint.ToString();
            guid = base.Id.ToString("N");
            Console.WriteLine(ipport + " connection");
        }

        protected override void OnDisconnected()
        {
            SendTemp.TryRemove(guid, out var _);
            Console.WriteLine(ipport + " disconnection");
        }
        HashSet<string> equips = new HashSet<string>();
        Dictionary<string, (TcpSession, UTEquipState header)> equipinfos = new Dictionary<string, (TcpSession, UTEquipState header)>();
        protected override unsafe void OnReceived(byte[] buffer, long offset, long size)
        {
            byte[] recBytes = new byte[size];
            Array.Copy(buffer, recBytes, size);
            Console.WriteLine(ipport + " Receive buffer:" + string.Join(" ", recBytes.Select((byte x) => x.ToString("X2"))));
            UTProtocol.Decap(buffer, offset, size, (UTHeader header) =>
            {
                SendAsync(UTProtocol.ReplyMessage(header));

            }, (EnumsAppType.上传消防部件状态,
                new Action<IntPtr>((IntPtr ptr) =>
            {
                UTEquipState* state = (UTEquipState*)ptr.ToPointer();
                if (equips.Add(state->GetEquipAddr()))
                    equipinfos.Add(state->GetEquipAddr(), (this, *state));
                else
                    equipinfos[state->GetEquipAddr()] = (this, *state);
                
                SendAsync(UTProtocol.ReplyMessage(state->header));
            }
            )));
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"[server] {base.Socket.RemoteEndPoint} error with code {error}");
        }

        public override bool SendAsync(byte[] buffer)
        {
            Console.WriteLine(ipport + " Send buffer:" + string.Join(" ", buffer.Select((byte x) => x.ToString("X2"))));
            return base.SendAsync(buffer);
        }
        public void SendCommand(string equipAddr, EnumReadTypes type)
        {
            if (equipinfos.ContainsKey(equipAddr) && type == EnumReadTypes.读取部件状态)
            {
                (var _, UTEquipState state) = equipinfos[equipAddr];
                byte[] data = UTProtocol.ReadEquip(state.header, EnumReadTypes.读取部件状态, state.GetReadAddr());
                this.SendAsync(data);
            };
            //if (equipinfos.ContainsKey(equipAddr) && type == EnumReadTypes.读取部件状态)
            //{
            //    (var _, UTSysState state) = equipinfos[equipAddr];
            //    byte[] data = UTProtocol.ReadEquip(state.header, EnumReadTypes.读取部件状态, state.GetReadAddr());
            //    this.SendAsync(data);
            //};

        }

        public void Init(string data)
        {
            data = data.Replace(" ", "");
            var da = Enumerable.Range(0, data.Length / 2).Select(i => byte.Parse(data.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null)).ToArray();
            this.OnReceived(da, 0, da.Length);
        }
    }
}
