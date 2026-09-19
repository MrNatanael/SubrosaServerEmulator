using SubrosaServerEmulator.Master.IO;

namespace SubrosaServerEmulator.Master.Packets;

public class ClientAuthAckReqPacket : IPacket
{
    public void Read(PacketStream s)
    {
        RegistrationId = s.ReadI32();
        Status = s.ReadI32();
    }
    public void Write(PacketStream s) => throw new NotImplementedException();

    public PacketType Type => PacketType.ClientAuthAck;
    public int RegistrationId { get; set; }
    public int Status { get; set; }
}
public class ClientAuthAckResPacket : IPacket
{
    public void Read(PacketStream s) => throw new NotImplementedException();
    public void Write(PacketStream s)
    {
        s.WriteI32(Status);
        s.WriteI32(RegistrationId);
    }

    public PacketType Type => PacketType.ClientAuthAck;
    public int Status { get; set; }
    public int RegistrationId { get; set; }
}