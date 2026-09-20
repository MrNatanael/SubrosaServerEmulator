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
        s.WriteI32(Unknown1);
        s.WriteI32(UserRegistrationId);
        s.WriteI64(Unknown2);
        s.WriteI32(UserStatus);
        s.WriteString(Username);
    }

    public PacketType Type => PacketType.RegisterClient;
    public int Unknown1 { get; set; }
    public int UserRegistrationId { get; set; }
    public long Unknown2 { get; set; }
    public int UserStatus { get; set; }
    public string Username { get; set; } = string.Empty;
}