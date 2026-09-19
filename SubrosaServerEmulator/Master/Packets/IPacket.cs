using SubrosaServerEmulator.Master.IO;

namespace SubrosaServerEmulator.Master.Packets;

public interface IPacket
{
    void Read(PacketStream s);
    void Write(PacketStream s);
    
    public PacketType Type { get; }
}

public enum PacketType : byte
{
    GameServerPing = 0x40,
    RegisterClient = 0x42,
    ClientAuth = 0x48,
    ClientAuthAck = 0x49,
    ServerList = 0x4A,
    ClientConnect = 0x4B
}