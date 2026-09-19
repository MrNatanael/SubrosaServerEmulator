using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using SubrosaServerEmulator.Master.IO;

namespace SubrosaServerEmulator.Master.Packets;

public class ClientServerListReqPacket : IPacket
{
    public void Read(PacketStream s){}
    public void Write(PacketStream s) {}
    
    public PacketType Type => PacketType.ServerList;
}

public class ClientServerListResPacket : IPacket
{
    public void Read(PacketStream s)
    {
        throw new NotImplementedException();
    }

    public void Write(PacketStream s)
    {
        s.WriteI32(EndPointArray.Length);
        s.WriteI32(Unknown1);
        
        foreach(var ep in EndPointArray)
        {
            var addr = ep.Address.GetAddressBytes().Reverse();
            s.Write(addr.ToArray());
            s.WriteI16((short)ep.Port);
        }
    }

    public PacketType Type => PacketType.ServerList;
    
    public int Unknown1 { get; set; }
    public IPEndPoint[] EndPointArray { get; set; } = [];
}