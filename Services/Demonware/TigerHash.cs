using System.Buffers.Binary;
using System.Reflection;

namespace ShaunWhiteReborn.Services.Demonware;

public static class TigerHash
{
    private static readonly ulong[][] T = LoadSBoxes();

    private static ulong[][] LoadSBoxes()
    {
        var asm = Assembly.GetExecutingAssembly();
        string name = asm.GetManifestResourceNames().First(n => n.EndsWith("tiger_sboxes.bin", StringComparison.Ordinal));
        using Stream stream = asm.GetManifestResourceStream(name)!;
        byte[] raw = new byte[4 * 256 * 8];
        stream.ReadExactly(raw);

        var tables = new ulong[4][];
        for (int t = 0; t < 4; t++)
        {
            tables[t] = new ulong[256];
            for (int i = 0; i < 256; i++)
            {
                tables[t][i] = BinaryPrimitives.ReadUInt64BigEndian(raw.AsSpan((t * 256 + i) * 8, 8));
            }
        }
        return tables;
    }

    private static void Round(ref ulong a, ref ulong b, ref ulong c, ulong x, ulong mul)
    {
        c ^= x;
        a -= T[0][c & 0xff] ^ T[1][(c >> 16) & 0xff] ^ T[2][(c >> 32) & 0xff] ^ T[3][(c >> 48) & 0xff];
        b += T[3][(c >> 8) & 0xff] ^ T[2][(c >> 24) & 0xff] ^ T[1][(c >> 40) & 0xff] ^ T[0][(c >> 56) & 0xff];
        b *= mul;
    }

    private static void Pass(ref ulong a, ref ulong b, ref ulong c, ulong[] x, ulong mul)
    {
        for (int i = 0; i < 8; i++)
        {
            switch (i % 3)
            {
                case 0: Round(ref a, ref b, ref c, x[i], mul); break;
                case 1: Round(ref b, ref c, ref a, x[i], mul); break;
                default: Round(ref c, ref a, ref b, x[i], mul); break;
            }
        }
    }

    private static void Schedule(ulong[] x)
    {
        x[0] -= x[7] ^ 0xA5A5A5A5A5A5A5A5UL;
        x[1] ^= x[0];
        x[2] += x[1];
        x[3] -= x[2] ^ (~x[1] << 19);
        x[4] ^= x[3];
        x[5] += x[4];
        x[6] -= x[5] ^ (~x[4] >> 23);
        x[7] ^= x[6];
        x[0] += x[7];
        x[1] -= x[0] ^ (~x[7] << 19);
        x[2] ^= x[1];
        x[3] += x[2];
        x[4] -= x[3] ^ (~x[2] >> 23);
        x[5] ^= x[4];
        x[6] += x[5];
        x[7] -= x[6] ^ 0x0123456789ABCDEFUL;
    }

    private static byte[] Compute(ReadOnlySpan<byte> data)
    {
        ulong a = 0x0123456789ABCDEFUL, b = 0xFEDCBA9876543210UL, c = 0xF096A5B4C3B2E187UL;

        int zeros = (((55 - data.Length) % 64) + 64) % 64;
        byte[] msg = new byte[data.Length + 1 + zeros + 8];
        data.CopyTo(msg);
        msg[data.Length] = 0x01;
        BinaryPrimitives.WriteUInt64LittleEndian(msg.AsSpan(msg.Length - 8), (ulong)data.Length * 8);

        var x = new ulong[8];
        for (int offset = 0; offset < msg.Length; offset += 64)
        {
            for (int i = 0; i < 8; i++)
            {
                x[i] = BinaryPrimitives.ReadUInt64LittleEndian(msg.AsSpan(offset + i * 8, 8));
            }

            ulong aa = a, bb = b, cc = c;
            Pass(ref a, ref b, ref c, x, 5);
            Schedule(x);
            Pass(ref c, ref a, ref b, x, 7);
            Schedule(x);
            Pass(ref b, ref c, ref a, x, 9);
            a ^= aa;
            b -= bb;
            c += cc;
        }

        byte[] result = new byte[24];
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(0, 8), a);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(8, 8), b);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(16, 8), c);
        return result;
    }

    public static byte[] InitialVector(uint seed)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, seed);
        return Compute(bytes)[..8];
    }
}
