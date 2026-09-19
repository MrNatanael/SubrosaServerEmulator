using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SubrosaServerEmulator.Master.Data;
using SubrosaServerEmulator.Master.Exceptions;
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

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        Info($"Listening at {settings.MasterListenIP}:{settings.MasterListenPort}");
        ReceiveNext();
    }
    public void Dispose()
    {
        IsRunning = false;
        _cts?.Cancel();

        Socket.Dispose();
        AsyncArgs.Dispose();
        _cts?.Dispose();
        
        _gameServersMap.Clear();
        _gameServers.Clear();
        
        _userNameMap.Clear();
        _userIdMap.Clear();
    }
    
    # region SERVER LOOP
    private void ReceiveNext()
    {
        while (IsRunning && !_cts!.IsCancellationRequested)
        {
            Debug("Waiting next packet...");
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

            Debug("Received packet synchronously");
            HandleCompletedReceive(AsyncArgs); // Completed sync
        }
    }
    private void OnReceivePacket(object? sender, SocketAsyncEventArgs e)
    {
        HandleCompletedReceive(e);

        if (IsRunning && !_cts!.IsCancellationRequested)
            ReceiveNext();
    }

    private void HandleCompletedReceive(SocketAsyncEventArgs e)
    {
        if (e.SocketError != SocketError.Success)
        {
            Warning($"Master Server");
            return;
        }

        if (e.BytesTransferred <= 0 || e.RemoteEndPoint is not IPEndPoint remote)
            return;

        ReadOnlySpan<byte> packet = e.Buffer.AsSpan(e.Offset, e.BytesTransferred);
        try
        {
            ProcessPacket(packet, remote);
        }
        catch (InvalidSignatureException)
        {
            Debug("Received packet with invalid signature");
        }
        catch (UnknownPacketException unk)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Received unknown packet 0x{unk.Type:X2}");
            if (packet.Length > 0x05)
            {
                sb.AppendLine($"\t> PACKET DUMP");
                sb.Append('\t');

                for (int i = 0x05; i < packet.Length; i++)
                {
                    var offset = i - 0x05;
                    if (offset > 0)
                    {
                        if (offset % 16 == 0x00)
                        {
                            sb.AppendLine();
                            sb.Append('\t');
                        }
                        else if (offset % 4 == 0x00) sb.Append("  ");
                    }

                    sb.Append($"{i:X2} ");
                }
            }

            Warning(sb.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            Error(ex.Message);
        }
    }
    private void ProcessPacket(ReadOnlySpan<byte> data, IPEndPoint remote)
    {
        Debug($"Received {data.Length} bytes from {remote}");
        using var stream = new PacketStream(data);

        if (stream.ReadI32() != PacketStream.SIGNATURE)
            throw new InvalidSignatureException();

        var type = stream.ReadU8();
        Debug($"Handling packet 0x{type:X2} ({(PacketType)type})");
        switch ((PacketType)type)
        {
            case PacketType.GameServerPing: OnGameServerPing(CreatePacket<GameServerPingPacket>(stream), remote); break;
            case PacketType.ClientAuth: OnClientAuth(CreatePacket<ClientAuthReqPacket>(stream), remote); break;
            case PacketType.ClientAuthAck: OnClientAuthAck(CreatePacket<ClientAuthAckReqPacket>(stream), remote); break;
            case PacketType.ServerList: OnListServers(CreatePacket<ClientServerListReqPacket>(stream), remote); break;
            case PacketType.ClientConnect: OnConnect(CreatePacket<ClientConnectReqPacket>(stream), remote); break;
            default: throw new UnknownPacketException(type);
        }
    }
    #endregion
    
    #region PACKET HANDLERS
    void OnGameServerPing(GameServerPingPacket _, EndPoint remote)
    {
        if(remote is not IPEndPoint ep)
        {
            Warning("Invalid remote endpoint type");
            return;
        }

        var id = BinaryPrimitives.ReadInt32LittleEndian(ep.Address.GetAddressBytes());
        if(_gameServersMap.Add(id))
        {
            Info($"Registered new game server {ep.Address}:{ep.Port}");
            _gameServers.Add(ep);

             
        }
    }
    void OnClientAuth(ClientAuthReqPacket req, EndPoint remote)
    {
        /*
         * TODO: WARNING
         * This is probably not a safe way to check if an user already exists
         * If two users with the same name try to connect, it may generate problems!
         */

        if (!_userNameMap.TryGetValue(req.Username, out var user))
        {
            var biggest = _userNameMap.LastOrDefault().Value?.RegistrationId ?? 0;
            user = new()
            {
                Username = req.Username,
                RegistrationId = biggest + 1,
                RegistrationSeq = biggest + 1
            };
            
            _userNameMap.Add(user.Username, user);
            _userIdMap[user.RegistrationId] = user;
            
            Info($"Registered new client \"{user.Username}\" (ID: {user.RegistrationId}, {user.RegistrationSeq})");
        }

        SendPacket(new ClientAuthResPacket
        {
            RegistrationId = user.RegistrationId,
            Field2 = 1,
            RegistrationSeq = user.RegistrationSeq
        }, remote);
    }

    void OnClientAuthAck(ClientAuthAckReqPacket req, EndPoint remote)
    {
        if(!_userIdMap.TryGetValue(req.RegistrationId, out var user))
            throw new NotImplementedException();
        
        Info($"Authenticated user \"{user.Username}\" (ID: {user.RegistrationId}, {user.RegistrationSeq})");
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
            EndPointArray = _gameServers.ToArray() 
        }, remote);
    }

    void OnConnect(ClientConnectReqPacket req, EndPoint remote)
    {
        if(!_userIdMap.TryGetValue(req.RegistrationId, out var user))
            throw new NotImplementedException();
        
        Info($"Player \"{user.Username}\" (ID: {user.RegistrationId}, {user.RegistrationSeq}) is joining a game...");
        foreach (var server in _gameServers)
        {
            SendPacket(new RegisterClientReqPacket
            {
                RegistrationId = user.RegistrationId,
                Unknown = req.Unknown,
                RegistrationSeq = user.RegistrationSeq,
                Username = user.Username
            }, server);
        }

        SendPacket(new ClientConnectResPacket
        {
            Unknown = req.Unknown,
        }, remote);
    }
    #endregion
    
    T CreatePacket<T>(PacketStream stream) where T : IPacket, new()
    {
        var packet = new T();
        packet.Read(stream);

        return packet;
    }

    void SendPacket(IPacket pck, EndPoint remote)
    {
        using var stream = new PacketStream();
        stream.WriteI32(PacketStream.SIGNATURE);
        stream.WriteU8((byte)pck.Type);
        
        pck.Write(stream);
        Socket.SendTo(stream.ToArray(), remote);
    }
    
    public MasterServer()
    {
        AsyncArgs.Completed += OnReceivePacket;
        AsyncArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        AsyncArgs.SetBuffer(new byte[1024], 0, 1024);
    }

    public bool IsRunning { get; private set; }
    public IReadOnlyList<IPEndPoint> GameServers => _gameServers;
    
    public int UserCount => _userNameMap.Count;
    public IEnumerable<User> Users => _userNameMap.Values;

    protected override string SourceName => "Master Server";
    private Socket Socket { get; } = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    
    private readonly List<IPEndPoint> _gameServers = new();
    private readonly HashSet<int> _gameServersMap = new();

    private readonly Dictionary<string, User> _userNameMap = new();
    private readonly Dictionary<int, User> _userIdMap = new();
    
    private SocketAsyncEventArgs AsyncArgs { get; } = new();
    private CancellationTokenSource? _cts;
}