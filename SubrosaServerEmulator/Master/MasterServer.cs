using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.Sqlite;
using SubrosaServerEmulator.Master.Data;
using SubrosaServerEmulator.Master.IO;
using SubrosaServerEmulator.Master.Packets;

namespace SubrosaServerEmulator.Master;

public class MasterServer : LogSource, IDisposable
{
    public void Run(ServerSettings settings)
    {
        if (IsRunning) throw new InvalidOperationException("Instance already running.");

        Debug($"Binding to {settings.MasterListenIP}:{settings.MasterListenPort}");

        Socket.Bind(new IPEndPoint(IPAddress.Parse(settings.MasterListenIP), settings.MasterListenPort));
        IsRunning = true;
        _lastPrune = Environment.TickCount64;

        Info($"Listening at {settings.MasterListenIP}:{settings.MasterListenPort}");
        ReceiveNext();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsRunning = false;

        AsyncArgs.Completed -= OnReceivePacket;

        Socket.Dispose();
        AsyncArgs.Dispose();
        _db.Dispose();

        _gameServersByIp.Clear();
        _gameServers = Array.Empty<IPEndPoint>();
        _sessionsByName.Clear();
        _sessionsById.Clear();
    }

    private void ReceiveNext()
    {
        while (IsRunning)
        {
            bool pending;
            try
            {
                pending = Socket.ReceiveFromAsync(AsyncArgs);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (pending)
                return;

            if (!HandleCompletedReceive(AsyncArgs))
                return;
        }
    }

    private void OnReceivePacket(object? sender, SocketAsyncEventArgs e)
    {
        if (HandleCompletedReceive(e))
            ReceiveNext();
    }
    private bool HandleCompletedReceive(SocketAsyncEventArgs e)
    {
        if (!IsRunning) return false;

        switch (e.SocketError)
        {
            case SocketError.Success:
                _consecutiveReceiveErrors = 0;
                break;

            case SocketError.OperationAborted:
            case SocketError.Shutdown:
            case SocketError.NotSocket:
            case SocketError.Interrupted:
                return false;

            case SocketError.ConnectionReset:
                return true;

            default:
                Warning($"Receive failed: {e.SocketError}");
                
                if (++_consecutiveReceiveErrors >= MaxConsecutiveReceiveErrors)
                {
                    Error($"Too many consecutive receive errors, stopping ({e.SocketError})");
                    IsRunning = false;
                    return false;
                }

                return true;
        }

        if (e.BytesTransferred <= 0 || e.RemoteEndPoint is not IPEndPoint remote)
            return true;

        try
        {
            ProcessPacket(e.Buffer.AsSpan(e.Offset, e.BytesTransferred), remote);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            Error(ex.Message);
        }

        return true;
    }

    private void ProcessPacket(ReadOnlySpan<byte> data, IPEndPoint remote)
    {
        if (data.Length < HeaderSize)
            return;

        using var stream = new PacketStream(data);

        if (stream.ReadI32() != PacketStream.SIGNATURE)
            return;

        var type = stream.ReadU8();
        switch ((PacketType)type)
        {
            case PacketType.GameServerPing: OnGameServerPing(CreatePacket<GameServerPingPacket>(stream), remote); break;
            case PacketType.ClientAuth: OnClientAuth(CreatePacket<ClientAuthReqPacket>(stream), remote); break;
            case PacketType.ClientAuthAck: OnClientAuthAck(CreatePacket<ClientAuthAckReqPacket>(stream), remote); break;
            case PacketType.ServerList: OnListServers(CreatePacket<ClientServerListReqPacket>(stream), remote); break;
            case PacketType.ClientConnect: OnConnect(CreatePacket<ClientConnectReqPacket>(stream), remote); break;
            default: LogUnknownPacket(type, data); break;
        }

        PruneIfDue(Environment.TickCount64);
    }

    private void LogUnknownPacket(int type, ReadOnlySpan<byte> packet)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Received unknown packet 0x{type:X2}");

        if (packet.Length > HeaderSize)
        {
            sb.AppendLine("\t> PACKET DUMP");
            sb.Append('\t');

            for (int i = HeaderSize; i < packet.Length; i++)
            {
                var offset = i - HeaderSize;
                if (offset > 0)
                {
                    if (offset % 16 == 0)
                    {
                        sb.AppendLine();
                        sb.Append('\t');
                    }
                    else if (offset % 4 == 0) sb.Append("  ");
                }

                sb.Append($"{packet[i]:X2} "); // was {i:X2} (printed the index, not the byte)
            }
        }

        Warning(sb.ToString());
    }

    private void PruneIfDue(long now)
    {
        if (now - _lastPrune < PruneIntervalMs) return;
        _lastPrune = now;

        var serversChanged = false;
        foreach (var (ip, entry) in _gameServersByIp)
        {
            if (now - entry.LastSeen <= GameServerTimeoutMs) continue;

            _gameServersByIp.Remove(ip);
            serversChanged = true;
            Info($"Game server {entry.EndPoint} timed out");
        }

        if (serversChanged)
            PublishGameServers();

        var evicted = 0;
        foreach (var (id, session) in _sessionsById)
        {
            if (now - session.LastSeen <= SessionTimeoutMs) continue;

            _sessionsById.TryRemove(id, out _);
            _sessionsByName.TryRemove(session.User.Username, out _);
            evicted++;
        }

        if (evicted > 0)
            Debug($"Evicted {evicted} idle user session(s)");
    }

    private void PublishGameServers()
    {
        var snapshot = new IPEndPoint[_gameServersByIp.Count];
        var i = 0;
        foreach (var entry in _gameServersByIp.Values)
            snapshot[i++] = entry.EndPoint;

        _gameServers = snapshot;
    }
    
    void OnGameServerPing(GameServerPingPacket _, IPEndPoint remote)
    {
        var now = Environment.TickCount64;

        if (_gameServersByIp.TryGetValue(remote.Address, out var entry))
        {
            entry.LastSeen = now;

            if (!entry.EndPoint.Equals(remote))
            {
                Info($"Game server {entry.EndPoint} moved to {remote}");
                entry.EndPoint = remote;
                PublishGameServers();
            }

            return;
        }

        _gameServersByIp[remote.Address] = new GameServerEntry(remote, now);
        PublishGameServers();
        Info($"Registered new game server {remote.Address}:{remote.Port}");
    }

    void OnClientAuth(ClientAuthReqPacket req, EndPoint remote)
    {
        var now = Environment.TickCount64;

        if (_sessionsByName.TryGetValue(req.Username, out var session))
        {
            session.LastSeen = now;
        }
        else
        {
            session = CacheSession(LoadOrRegisterUser(req.Username), now);
        }

        var user = session.User;
        SendPacket(new ClientAuthResPacket
        {
            RegistrationId = user.RegistrationId,
            Field2 = 1,
            RegistrationSeq = user.Status
        }, remote);
    }

    void OnClientAuthAck(ClientAuthAckReqPacket req, EndPoint remote)
    {
        if (!TryGetUser(req.RegistrationId, Environment.TickCount64, out var user))
        {
            Warning($"Auth ack for unknown registration ID {req.RegistrationId}");
            return;
        }

        Info($"Authenticated user \"{user.Username}\" (ID: {user.RegistrationId}, {user.Status})");
        SendPacket(new ClientAuthAckResPacket
        {
            Status = 1,
            RegistrationId = user.RegistrationId
        }, remote);
    }

    void OnListServers(ClientServerListReqPacket _, EndPoint remote)
    {
        SendPacket(new ClientServerListResPacket
        {
            EndPointArray = _gameServers
        }, remote);
    }

    void OnConnect(ClientConnectReqPacket req, EndPoint remote)
    {
        if (!TryGetUser(req.RegistrationId, Environment.TickCount64, out var user))
        {
            Warning($"Connect request for unknown registration ID {req.RegistrationId}");
            return;
        }

        Info($"Player \"{user.Username}\" (ID: {user.RegistrationId}, {user.Status}) is joining a game...");

        var servers = _gameServers;
        if (servers.Length > 0)
        {
            var payload = Serialize(new RegisterClientReqPacket
            {
                Unknown1 = user.RegistrationId,
                UserRegistrationId = user.RegistrationId,
                UserStatus = user.Status,
                Username = user.Username
            });

            foreach (var server in servers)
                SendRaw(payload, server);
        }

        SendPacket(new ClientConnectResPacket
        {
            Unknown = req.Unknown,
        }, remote);
    }

    private Session CacheSession(User user, long now)
    {
        var session = new Session(user, now);
        _sessionsByName[user.Username] = session;
        _sessionsById[user.RegistrationId] = session;
        return session;
    }

    private bool TryGetUser(int registrationId, long now, out User user)
    {
        if (_sessionsById.TryGetValue(registrationId, out var session))
        {
            session.LastSeen = now;
            user = session.User;
            return true;
        }

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT username FROM users WHERE registration_id = $id;";
        cmd.Parameters.AddWithValue("$id", registrationId);

        if (cmd.ExecuteScalar() is string username)
        {
            user = CacheSession(new User
            {
                Username = username,
                RegistrationId = registrationId,
                Status = DefaultStatus
            }, now).User;
            return true;
        }

        user = null!;
        return false;
    }

    private User LoadOrRegisterUser(string username)
    {
        using (var select = _db.CreateCommand())
        {
            select.CommandText = "SELECT registration_id FROM users WHERE username = $username;";
            select.Parameters.AddWithValue("$username", username);

            if (select.ExecuteScalar() is { } existing)
            {
                var loaded = new User
                {
                    Username = username,
                    RegistrationId = Convert.ToInt32(existing),
                    Status = DefaultStatus
                };

                Debug($"Loaded client \"{loaded.Username}\" (ID: {loaded.RegistrationId}, {loaded.Status})");
                return loaded;
            }
        }

        using var transaction = _db.BeginTransaction();

        int id;
        using (var idCmd = _db.CreateCommand())
        {
            idCmd.Transaction = transaction;
            idCmd.CommandText = "SELECT COALESCE(MAX(registration_id), 0) + 1 FROM users;";
            id = Convert.ToInt32(idCmd.ExecuteScalar());
        }

        using (var insert = _db.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                                 INSERT INTO users (username, registration_id, registration_seq)
                                 VALUES ($username, $id, $seq);
                                 """;
            insert.Parameters.AddWithValue("$username", username);
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.AddWithValue("$seq", DefaultStatus);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();

        var user = new User { Username = username, RegistrationId = id, Status = DefaultStatus };
        Info($"Registered new client \"{user.Username}\" (ID: {user.RegistrationId}, {user.Status})");
        return user;
    }
    
    T CreatePacket<T>(PacketStream stream) where T : IPacket, new()
    {
        var packet = new T();
        packet.Read(stream);

        return packet;
    }

    private static byte[] Serialize(IPacket pck)
    {
        using var stream = new PacketStream();
        stream.WriteI32(PacketStream.SIGNATURE);
        stream.WriteU8((byte)pck.Type);

        pck.Write(stream);
        return stream.ToArray();
    }

    void SendPacket(IPacket pck, EndPoint remote) => SendRaw(Serialize(pck), remote);

    private void SendRaw(byte[] payload, EndPoint remote)
    {
        try
        {
            Socket.SendTo(payload, remote);
        }
        catch (SocketException ex)
        {
            Warning($"Send to {remote} failed: {ex.SocketErrorCode}");
        }
        catch (ObjectDisposedException)
        {
            
        }
    }
    
    void InitDB()
    {
        _db.Open();

        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = """
                              CREATE TABLE IF NOT EXISTS users (
                                  username TEXT PRIMARY KEY,
                                  registration_id INTEGER NOT NULL UNIQUE,
                                  registration_seq INTEGER NOT NULL DEFAULT 1
                              );
                              """;
            cmd.ExecuteNonQuery();
        }

        // The original CREATE TABLE had no registration_seq column even though the queries used it.
        // Add it to databases created by that version (no-op if it already exists).
        bool hasSeq;
        using (var check = _db.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('users') WHERE name = 'registration_seq';";
            hasSeq = Convert.ToInt32(check.ExecuteScalar()) > 0;
        }

        if (!hasSeq)
        {
            using var alter = _db.CreateCommand();
            alter.CommandText = "ALTER TABLE users ADD COLUMN registration_seq INTEGER NOT NULL DEFAULT 1;";
            alter.ExecuteNonQuery();
        }
    }

    public MasterServer()
    {
        if (OperatingSystem.IsWindows())
        {
            const int SIO_UDP_CONNRESET = -1744830452;
            Socket.IOControl(SIO_UDP_CONNRESET, [0], null);
        }

        AsyncArgs.Completed += OnReceivePacket;
        AsyncArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        AsyncArgs.SetBuffer(new byte[1024], 0, 1024);

        InitDB();
    }
    
    public bool IsRunning
    {
        get => _isRunning;
        private set => _isRunning = value;
    }

    public IReadOnlyList<IPEndPoint> GameServers => _gameServers;
    
    public int UserCount => _sessionsById.Count;
    public IEnumerable<User> Users => _sessionsById.Values.Select(s => s.User);

    protected override string SourceName => "Master Server";
    private Socket Socket { get; } = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

    private sealed class GameServerEntry(IPEndPoint endPoint, long lastSeen)
    {
        public IPEndPoint EndPoint = endPoint;
        public long LastSeen = lastSeen;
    }

    private sealed class Session(User user, long lastSeen)
    {
        public readonly User User = user;
        public long LastSeen = lastSeen;
    }

    private readonly Dictionary<IPAddress, GameServerEntry> _gameServersByIp = new();
    private volatile IPEndPoint[] _gameServers = Array.Empty<IPEndPoint>();

    private readonly ConcurrentDictionary<string, Session> _sessionsByName = new();
    private readonly ConcurrentDictionary<int, Session> _sessionsById = new();

    private readonly SqliteConnection _db = new("Data Source=master.db;Pooling=False");

    private SocketAsyncEventArgs AsyncArgs { get; } = new();

    private volatile bool _isRunning;
    private bool _disposed;
    private long _lastPrune;
    private int _consecutiveReceiveErrors;
    
    private const long GameServerTimeoutMs = 2 * 60 * 1000;
    
    private const long SessionTimeoutMs = 30 * 60 * 1000;

    private const long PruneIntervalMs = 30 * 1000;

    private const int MaxConsecutiveReceiveErrors = 100;
    private const int HeaderSize = 5;
    private const int DefaultStatus = 1;
}