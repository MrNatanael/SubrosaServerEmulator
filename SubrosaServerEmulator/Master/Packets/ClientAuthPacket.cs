using SubrosaServerEmulator.Master.IO;

namespace SubrosaServerEmulator.Master.Packets;

public class ClientAuthReqPacket : IPacket
{
    public void Read(PacketStream s)
    {
        Username = s.ReadString();
    }
    public void Write(PacketStream s) => throw new NotImplementedException();

    public PacketType Type => PacketType.ClientAuth;
    public string Username { get; set; } = string.Empty;
}
public class ClientAuthResPacket : IPacket
{
    public void Read(PacketStream s) => throw new NotImplementedException();
    public void Write(PacketStream s)
    {
        s.WriteI32(RegistrationId);
        s.WriteI32(Field2);
        s.WriteI32(RegistrationSeq);
    }

    public PacketType Type => PacketType.ClientAuth;
    
    public int RegistrationId { get; set; }
    public int Field2 { get; set; }
    public int RegistrationSeq { get; set; }
}