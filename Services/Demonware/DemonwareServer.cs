using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ShaunWhiteReborn.Services.Demonware;

public class DemonwareServer : BackgroundService
{
    private const int AuthPort = 3078;
    private const int LobbyPort = 3036;

    private const int MsgAuthRequest = 18;
    private const int MsgAuthReply = 19;
    private const int LsgTaskReply = 1;
    private const int LsgConnectionId = 4;
    private const int LsgLogin = 7;

    private const uint ServerTicketMagic = 0x43445845;
    private const uint AuthOk = 700;

    private sealed class Connection
    {
        public required NetworkStream Stream { get; init; }
        public required int Port { get; init; }
        public List<byte> Buffer { get; } = new();
        public bool Started;
        public bool IsLobby;
        public byte[]? Key;
        public string User = "";
        public ulong TransactionId = 1;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var loops = new List<Task>();
        foreach (int port in new[] { AuthPort, LobbyPort })
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                loops.Add(AcceptLoopAsync(listener, port, stoppingToken));
            }
            catch (SocketException ex)
            {
                Log($"[DNS]: Couldn't open port {port}: {ex.Message}");
            }
        }

        if (loops.Count > 0)
        {
            await Task.WhenAll(loops);
        }
    }

    private static async Task AcceptLoopAsync(TcpListener listener, int port, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClientAsync(client, port, token), token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            listener.Stop();
        }
    }

    private static void Log(string message) => Console.WriteLine($"[DW] {message}");

    private static async Task HandleClientAsync(TcpClient client, int port, CancellationToken token)
    {
        var connection = new Connection { Stream = client.GetStream(), Port = port };

        try
        {
            using (client)
            {
                byte[] buffer = new byte[65536];
                while (!token.IsCancellationRequested)
                {
                    int read = await connection.Stream.ReadAsync(buffer, token);
                    if (read == 0)
                    {
                        break;
                    }

                    connection.Buffer.AddRange(buffer.AsSpan(0, read).ToArray());
                    await ProcessAsync(connection, token);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log($"[DNS]: Connection error on port {port}: {ex.Message}");
        }
    }

    private static async Task ProcessAsync(Connection connection, CancellationToken token)
    {
        if (!connection.Started && connection.Buffer.Count >= 8 && connection.Buffer[4] == 0xFF && connection.Buffer[5] == 0xFF)
        {
            connection.Buffer.RemoveRange(0, 8);
            connection.IsLobby = true;
        }
        connection.Started = true;

        foreach (var (encrypted, type, payload) in ReadFrames(connection.Buffer))
        {
            if (connection.Port == AuthPort && encrypted == 0 && type == MsgAuthRequest && !connection.IsLobby)
            {
                byte[]? reply = AuthReply(payload);
                if (reply != null)
                {
                    await connection.Stream.WriteAsync(reply, token);
                }
            }
            else
            {
                await HandleLobbyAsync(connection, encrypted, type, payload, token);
            }
        }
    }

    private static List<(byte Encrypted, byte Type, byte[] Payload)> ReadFrames(List<byte> buffer)
    {
        var frames = new List<(byte, byte, byte[])>();
        while (buffer.Count >= 4)
        {
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(buffer.GetRange(0, 4).ToArray());
            if (buffer.Count < 4 + length)
            {
                break;
            }

            byte[] body = buffer.GetRange(4, (int)length).ToArray();
            buffer.RemoveRange(0, 4 + (int)length);
            if (length >= 2)
            {
                frames.Add((body[0], body[1], body[2..]));
            }
        }
        return frames;
    }

    private static byte[] Frame(byte messageType, byte[] payload)
    {
        byte[] frame = new byte[4 + 2 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)(2 + payload.Length));
        frame[4] = 0;
        frame[5] = messageType;
        payload.CopyTo(frame, 6);
        return frame;
    }

    private static byte[] TripleDes(byte[] key, byte[] iv, byte[] data, bool encrypt)
    {
        using var tdes = TripleDES.Create();
        tdes.Mode = CipherMode.CBC;
        tdes.Padding = PaddingMode.None;
        tdes.Key = key;
        tdes.IV = iv;
        using ICryptoTransform transform = encrypt ? tdes.CreateEncryptor() : tdes.CreateDecryptor();
        return transform.TransformFinalBlock(data, 0, data.Length);
    }

    private static byte[] PadTo(byte[] data, int size)
    {
        byte[] padded = new byte[size];
        data.AsSpan(0, Math.Min(data.Length, size)).CopyTo(padded);
        return padded;
    }

    private static uint RandomSeed() => (uint)Random.Shared.NextInt64(0, 1L << 32);

    private static byte[] PackAuthTicket(uint title, uint issued, uint expires, ulong userId, string name, byte[] sessionKey)
    {
        byte[] ticket = new byte[128];
        BinaryPrimitives.WriteUInt32LittleEndian(ticket.AsSpan(0), 0xEFBDADDE);
        ticket[4] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(ticket.AsSpan(5), title);
        BinaryPrimitives.WriteUInt32LittleEndian(ticket.AsSpan(9), issued);
        BinaryPrimitives.WriteUInt32LittleEndian(ticket.AsSpan(13), expires);
        BinaryPrimitives.WriteUInt64LittleEndian(ticket.AsSpan(17), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(ticket.AsSpan(25), userId);
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(63, name.Length)).CopyTo(ticket.AsSpan(33));
        sessionKey.CopyTo(ticket, 97);
        return ticket;
    }

    private static byte[]? AuthReply(byte[] payload)
    {
        uint title = new BitReader(payload).U32();

        NpTicket? npTicket = NpTicket.Parse(payload);
        if (npTicket == null || npTicket.Cookie.Length != 24)
        {
            Log("[AUTH]: No valid PSN ticket in the request.");
            return null;
        }

        byte[] sessionKey = RandomNumberGenerator.GetBytes(24);
        uint now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        byte[] authTicket = PackAuthTicket(title, now, now + 24 * 3600, npTicket.UserId, npTicket.OnlineId, sessionKey);

        uint seed = RandomSeed();
        byte[] encrypted = TripleDes(npTicket.Cookie, TigerHash.InitialVector(seed), PadTo(authTicket, 152), encrypt: true);

        byte[] serverTicket = new byte[128];
        BinaryPrimitives.WriteUInt32LittleEndian(serverTicket.AsSpan(0), ServerTicketMagic);
        BinaryPrimitives.WriteUInt64LittleEndian(serverTicket.AsSpan(4), npTicket.UserId);
        sessionKey.CopyTo(serverTicket, 12);
        byte[] name = Encoding.ASCII.GetBytes(npTicket.OnlineId);
        name.AsSpan(0, Math.Min(63, name.Length)).CopyTo(serverTicket.AsSpan(36));

        var writer = new BitWriter();
        writer.U32(AuthOk);
        writer.U32(seed);
        writer.RawBytes(encrypted);
        writer.RawBytes(serverTicket);

        Log($"[AUTH]: {npTicket.OnlineId} authorized.");
        return Frame(MsgAuthReply, writer.ToArray());
    }

    private static byte[]? FindServerTicket(byte[] payload)
    {
        var reader = new BitReader(payload);
        for (int start = 1; start <= payload.Length * 8 - 128 * 8; start++)
        {
            reader.Position = start;
            if ((uint)reader.Raw(32) != ServerTicketMagic)
            {
                continue;
            }

            reader.Position = start;
            byte[] ticket = new byte[128];
            for (int i = 0; i < 128; i++)
            {
                ticket[i] = (byte)reader.Raw(8);
            }
            return ticket;
        }
        return null;
    }

    private static byte[] LobbyEncrypt(byte[] key, byte messageType, byte[] body)
    {
        byte[] plain = new byte[(5 + body.Length + 7) / 8 * 8];
        BinaryPrimitives.WriteUInt32LittleEndian(plain, 0xDEADBEEF);
        plain[4] = messageType;
        body.CopyTo(plain, 5);

        uint seed = RandomSeed();
        byte[] cipher = TripleDes(key, TigerHash.InitialVector(seed), plain, encrypt: true);

        byte[] frame = new byte[4 + 1 + 4 + cipher.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)(1 + 4 + cipher.Length));
        frame[4] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(5), seed);
        cipher.CopyTo(frame, 9);
        return frame;
    }

    private static byte[] EmptyTaskReply(Connection connection)
    {
        var writer = new BitWriter();
        writer.U64(connection.TransactionId++);
        writer.U32(0);
        writer.U8(4);
        writer.U32(0);
        writer.U32(0);
        return writer.ToArray();
    }

    private static async Task HandleLobbyAsync(Connection connection, byte encrypted, byte type, byte[] payload, CancellationToken token)
    {
        if (encrypted == 0 && type == LsgLogin)
        {
            byte[]? ticket = FindServerTicket(payload);
            if (ticket == null)
            {
                Log("[LOGIN]: Server ticket not found in the request.");
                return;
            }

            connection.Key = ticket[12..36];
            connection.User = Encoding.ASCII.GetString(ticket[36..100]).Split('\0')[0];
            connection.IsLobby = true;
            Log($"[LOGIN]: {connection.User} logged in.");

            var writer = new BitWriter();
            writer.U64((ulong)Random.Shared.NextInt64());
            await connection.Stream.WriteAsync(LobbyEncrypt(connection.Key, LsgConnectionId, writer.ToArray()), token);
            return;
        }

        if (encrypted == 1 && connection.Key != null)
        {
            await connection.Stream.WriteAsync(LobbyEncrypt(connection.Key, LsgTaskReply, EmptyTaskReply(connection)), token);
            return;
        }

        Log($"[LOBBY]: Unhandled message (encrypted={encrypted}, type={type}, {payload.Length} bytes).");
    }
}
