using System.Runtime.InteropServices;
using System.Text;

namespace YongChuanTools
{
    public struct UInt16Analog
    {
        public byte analogType;
        public UInt16 analogValue;
        public UInt16 analogMax;
        public UInt16 analogMin;
    }
    public struct UInt32Analog
    {
        public byte analogType;
        public UInt32 analogValue;
        public UInt32 analogMax;
        public UInt32 analogMin;
    }
    public unsafe struct UTHikStateHeader
    {
        public byte sysType;
        public byte equipType;
        public fixed byte fireEquipId[9];
        public fixed byte fireEquipMac[6];

    }
    /// <summary>
    /// apptype 129
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UTHikSysRunState
    {
        public UTHeader header;
        public UTAppHeader appheader;
        public UTHikStateHeader hikHeader;
        public ushort runState;
        public byte rssiState;
        public byte relayState;
        public fixed byte simCardNo[20];
        public fixed byte reserve[42];
    }
    /// <summary>
    /// apptype 130
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UTHikSysOperateState
    {
        public UTHeader header;
        public UTAppHeader appheader;
        public UTHikStateHeader hikHeader;
        public byte operateState;
        public fixed byte reserve[64];


    }

    /// <summary>
    /// apptype 131
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UTHikEquipRunState
    {
        public UTHeader header;
        public UTAppHeader appheader;
        public UTHikStateHeader hikHeader;
        public byte runState;
        public byte powerState;
        public UInt16Analog analogValue1;
        public byte alarmLevel;
        public uint otherState;
        public byte rssi;
        public fixed byte position[15];
        public UInt32Analog extAnalogValue1;
        public byte analogCount;
        public UInt16Analog analogValues1;
        public UInt16Analog analogValues2;
        public UInt16Analog analogValues3;
        public UInt16Analog analogValues4;
        public UInt16Analog analogValues5;
        public UInt16Analog analogValues6;
        public UInt16Analog analogValues7;
        public UInt16Analog analogValues8;
        public UInt16Analog analogValues9;
        public ushort reserve;
    }

    /// <summary>
    /// apptype 132
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UTHikEquipOperateState
    {
        public UTHeader header;
        public UTAppHeader appheader;
        public UTHikStateHeader hikHeader;

    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UTEquipStateHik
    {
        public UTHeader header;
        public UTAppHeader appHeader;
        public byte sysType;
        public byte sysAddr;
        public byte equipType;
        public fixed byte equipAddr[64];
        public ushort equipState;
        public byte channelAddr;
        public byte multiLineAddr;
        public byte equipPosLength;
        public fixed byte leftData[1024];
        public string ReadEquipPos()
        {
            fixed (byte* str = leftData)
            {
                return Encoding.ASCII.GetString(str, equipPosLength);
            }
        }
        public string ReadTime()
        {
            fixed (byte* str = leftData)
            {
                byte* str1 = str + equipPosLength;
                return Encoding.ASCII.GetString(str1, 6);
            }
        }
        public string ReadExt()
        {
            fixed (byte* str = leftData)
            {
                byte* str1 = str + equipPosLength + 6 + 1;
                byte len = *(str1 - 1);
                return Encoding.ASCII.GetString(str1, len);
            }
        }
    }

}
