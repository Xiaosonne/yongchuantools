using System;
using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using static YongChuanTools.UTProtocol;

namespace YongChuanTools
{
    public class NetTcpServer : TcpServer, IUserOperation
    {
        public NetTcpServer(IPAddress address, int port)
            : base(address, port)
        {
        }

        protected override TcpSession CreateSession()
        {
            return new NetTcpSession(this);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"TCP server error with code {error}");
        }
        public void SendCommand(string equipAddr, EnumReadTypes type)
        {
            this.Sessions.Values.Cast<NetTcpSession>()
                .All(s =>
                {
                    s.SendCommand(equipAddr, type);
                    return true;
                });
        }

        public void Init(string data)
        {
            this.Sessions.Values.Cast<IUserOperation>().All(t =>
            {
                t.Init(data);
                return true;
            });
        }
    }

}

