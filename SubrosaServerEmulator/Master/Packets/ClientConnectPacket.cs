using System.Net;
using SubrosaServerEmulator.Master.IO;

namespace SubrosaServerEmulator.Master.Packets;

public class ClientConnectReqPacket : IPacket 
{
    public void Read(PacketStream s)
    {
        RegistrationId = s.ReadI32();
        Unknown = s.ReadI32();

        ServerAddress = new IPAddress(s.Read(0x04).Reverse().ToArray());
        ServerPort = s.ReadI16();
    }

    public void Write(PacketStream s)
    {
        throw new NotImplementedException();
    }

    public PacketType Type => PacketType.ClientConnect;
    public int RegistrationId { get; set; }
    public int Unknown { get; set; }
    
    
    // TODO: I'm not sure yet if this should have ip and port, I just received 0x00
    public IPAddress ServerAddress { get; set; } = IPAddress.Any;
    public short ServerPort { get; set; }
}
public class ClientConnectResPacket : IPacket 
{
    public void Read(PacketStream s) => throw  new NotImplementedException();
    public void Write(PacketStream s)
    {
        s.WriteI32(Unknown);
    }

    public PacketType Type => PacketType.ClientConnect;
    public int Unknown { get; set; }
}