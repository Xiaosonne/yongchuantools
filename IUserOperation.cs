using static YongChuanTools.UTProtocol;

namespace YongChuanTools
{
    public interface IUserOperation
    {
        void SendCommand(string equipAddr, EnumReadTypes type);
        void Init(string data);
    }
}
