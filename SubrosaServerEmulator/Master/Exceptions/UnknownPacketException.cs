namespace SubrosaServerEmulator.Master.Exceptions;

public class UnknownPacketException(byte type) : Exception
{
    public byte Type { get; } = type;
}