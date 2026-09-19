using SubrosaServerEmulator.Master.IO;

namespace SubrosaServerEmulator.Master.Packets;

public class RegisterClientReqPacket : IPacket
{
    public void Read(PacketStream s)
    {
        throw new NotImplementedException();
    }

    public void Write(PacketStream s)
    {
        s.WriteI32(RegistrationId);
        s.WriteI32(Unknown);
        s.WriteI64(Unknown2);
        s.WriteI32(RegistrationSeq);
        s.WriteString(Username);
    }

    public PacketType Type => PacketType.RegisterClient;
    public int RegistrationId { get; set; }
    public int Unknown { get; set; }
    public long Unknown2 { get; set; }
    public int RegistrationSeq { get; set; }
    public string Username { get; set; } = string.Empty;
}