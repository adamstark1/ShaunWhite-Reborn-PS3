using System.Buffers.Binary;
using System.Text;

namespace ShaunWhiteReborn.Services.Demonware;

public sealed class NpTicket
{
    public ulong UserId { get; init; }
    public string OnlineId { get; init; } = "";
    public byte[] Cookie { get; init; } = Array.Empty<byte>();

    public static NpTicket? Parse(byte[] data)
    {
        int i = data.AsSpan().IndexOf(new byte[] { 0x21, 0x01, 0x00, 0x00 });
        if (i < 0 || i + 12 > data.Length)
        {
            return null;
        }

        int p = i + 8;
        int sectionLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 2, 2));
        p += 4;
        int end = Math.Min(p + sectionLength, data.Length);

        var fields = new List<byte[]>();
        while (p + 4 <= end)
        {
            int n = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 2, 2));
            if (p + 4 + n > data.Length)
            {
                break;
            }
            fields.Add(data.AsSpan(p + 4, n).ToArray());
            p += 4 + n;
        }

        if (fields.Count < 11)
        {
            return null;
        }

        return new NpTicket
        {
            UserId = fields[4].Length == 8 ? BinaryPrimitives.ReadUInt64BigEndian(fields[4]) : 0,
            OnlineId = Encoding.ASCII.GetString(fields[5]).Split('\0')[0],
            Cookie = fields[10],
        };
    }
}
