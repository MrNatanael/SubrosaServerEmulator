using SubrosaServerEmulator.Master.IO;

namespace SubrosaServerEmulator.Master.Packets;

public class GameServerPingPacket : IPacket
{
    public void Read(PacketStream s) {}
    public void Write(PacketStream s) {}
    public PacketType Type => PacketType.GameServerPing;
}