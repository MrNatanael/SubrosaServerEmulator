using System.Buffers.Binary;
using System.Text;

namespace SubrosaServerEmulator.Master.IO;

public class PacketStream : IDisposable
{
    public void WriteU8(byte u8) => Stream.WriteByte(u8);
    public void WriteI16(short i16) => Write(WritePrimitive(BinaryPrimitives.WriteInt16LittleEndian, i16, sizeof(short)));
    public void WriteI32(int i32) => Write(WritePrimitive(BinaryPrimitives.WriteInt32LittleEndian, i32, sizeof(int)));
    public void WriteI64(long i64) => Write(WritePrimitive(BinaryPrimitives.WriteInt64LittleEndian, i64, sizeof(long)));
    public void WriteString(string str)
    {
        var sz = Math.Min(str.Length, STRING_LENGTH);
        var padding = STRING_LENGTH - sz;
        
        Stream.Write(Encoding.UTF8.GetBytes(str), 0, sz);
        if(padding > 0) Write(new byte[padding]);
    }
    public void Write(byte[] buffer) => Stream.Write(buffer, 0, buffer.Length);
    
    public byte ReadU8() => (byte)Stream.ReadByte();
    public short ReadI16() => ReadPrimitive(BinaryPrimitives.ReadInt16LittleEndian, sizeof(short));
    public int ReadI32() => ReadPrimitive(BinaryPrimitives.ReadInt32LittleEndian, sizeof(int));

    public string ReadString()
    {
        var blob = Read(STRING_LENGTH);
        var sz = blob.IndexOf((byte)0x00);
        if (sz == -1) sz = blob.Length;

        return Encoding.UTF8.GetString(blob, 0, sz);
    }
    public byte[] Read(int length)
    {
        if(length > Length - Position)
            throw new ArgumentOutOfRangeException(nameof(length));
        
        var buffer = new byte[length];
        Stream.ReadExactly(buffer, 0, length);
        return buffer;
    }
    
    public T ReadPrimitive<T>(PrimitiveReader<T> reader, int size)
    {
        var buffer = Read(size);
        return reader(buffer);
    }

    public byte[] WritePrimitive<T>(PrimitiveWriter<T> writer, T value, int size)
    {
        var buffer = new byte[size];
        writer(buffer, value);

        return buffer;
    }

    public byte[] ToArray() => Stream.ToArray();
    
    public void Dispose() => Stream.Dispose();
    
    public PacketStream()
    {
        Stream = new();
    }
    public PacketStream(ReadOnlySpan<byte> data)
    {
        Stream = new(data.ToArray());
    }

    public long Length => Stream.Length;
    public long Position => Stream.Position;
    
    private MemoryStream Stream { get; }

    public delegate T PrimitiveReader<out T>(ReadOnlySpan<byte> data);
    public delegate void PrimitiveWriter<in T>(Span<byte> data, T value);

    public const int SIGNATURE = 0x50464437;
    public const int STRING_LENGTH = 0x20;
}