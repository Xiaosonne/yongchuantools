using NetCoreServer;
using System.Net;

namespace YongChuanTools
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            var server = new NetTcpServer(IPAddress.Any, 44370);
            server.Start();
            string str = null;
            while (true)
            {
                str = Console.ReadLine();
                if (str.StartsWith("read"))
                {
                    var arr = str.Split(" ");
                    server.SendCommand(arr[1], (EnumReadTypes)int.Parse(arr[2]));
                }
                if (str.StartsWith("init"))
                {
                    var arr = str.Split("#");
                    server.Init(arr[1]);
                }
            };
        }
    }
}
