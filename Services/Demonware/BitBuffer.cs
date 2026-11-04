namespace ShaunWhiteReborn.Services.Demonware;

public sealed class BitWriter
{
    private const int TypeUChar8 = 3;
    private const int TypeUInt32 = 8;
    private const int TypeUInt64 = 10;

    private readonly List<bool> _bits = new();

    public BitWriter()
    {
        _bits.Add(true);
    }

    public void Raw(ulong value, int bitCount)
    {
        for (int i = 0; i < bitCount; i++)
        {
            _bits.Add(((value >> i) & 1) != 0);
        }
    }

    public void U8(byte value)
    {
        Raw(TypeUChar8, 5);
        Raw(value, 8);
    }

    public void U32(uint value)
    {
        Raw(TypeUInt32, 5);
        Raw(value, 32);
    }

    public void U64(ulong value)
    {
        Raw(TypeUInt64, 5);
        Raw(value, 64);
    }

    public void RawBytes(ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
        {
            Raw(b, 8);
        }
    }

    public byte[] ToArray()
    {
        byte[] output = new byte[(_bits.Count + 7) / 8];
        for (int i = 0; i < _bits.Count; i++)
        {
            if (_bits[i])
            {
                output[i >> 3] |= (byte)(1 << (i & 7));
            }
        }
        return output;
    }
}

public sealed class BitReader
{
    private readonly byte[] _data;

    public int Position { get; set; }
    public bool TypeChecking { get; }

    public BitReader(byte[] data)
    {
        _data = data;
        TypeChecking = Raw(1) == 1;
    }

    public ulong Raw(int bitCount)
    {
        ulong value = 0;
        for (int i = 0; i < bitCount; i++)
        {
            int byteIndex = Position >> 3;
            int bit = byteIndex < _data.Length ? (_data[byteIndex] >> (Position & 7)) & 1 : 0;
            value |= (ulong)bit << i;
            Position++;
        }
        return value;
    }

    public uint U32()
    {
        if (TypeChecking)
        {
            Raw(5);
        }
        return (uint)Raw(32);
    }
}
