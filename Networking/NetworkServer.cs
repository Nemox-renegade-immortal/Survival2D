using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlayInspiredPrototype;

public sealed class NetworkServer : IDisposable
{
    private sealed class ClientPeer
    {
        public int PlayerId;
        public TcpClient Client = null!;
        public StreamReader Reader = null!;
        public StreamWriter Writer = null!;
        public NetPlayerState State = new NetPlayerState();
        public DateTime LastSeenUtc = DateTime.UtcNow;
        public readonly ConcurrentQueue<string> ReliableQueue = new ConcurrentQueue<string>();
        public readonly SemaphoreSlim SendSignal = new SemaphoreSlim(0);
        public readonly object PendingSync = new object();
        public string? PendingPlayerSnapshotJson;
        public string? PendingWorldSnapshotJson;
        public Task? WriterTask;
        public volatile bool IsClosed;
    }

    private readonly object _sync = new object();
    private readonly Dictionary<int, ClientPeer> _clients = new Dictionary<int, ClientPeer>();
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private TcpListener? _listener;
    private Task? _acceptTask;
    private Task? _broadcastTask;
    private int _tick;
    private int _worldTick;
    private int _nextId = 1;
    private NetPlayerState _hostState = new NetPlayerState { PlayerId = 0, Callsign = "HOST", AccentArgb = unchecked((int)0xFF5CD8FF), WeaponName = "Pistol", WeaponLevel = 1 };
    private NetWorldState _hostWorldState = new NetWorldState();

    public int Port { get; }
    public int MaxPlayers { get; }
    public int MapSeed { get; }
    public int PlayerSnapshotRateHz { get; } = 20;
    public int WorldSnapshotRateHz { get; } = 8;
    public bool IsRunning => _listener is not null;
    public string Status { get; private set; } = "offline";

    public NetworkServer(int port, int maxPlayers, int mapSeed)
    {
        Port = port;
        MaxPlayers = Math.Max(2, maxPlayers);
        MapSeed = mapSeed;
    }

    public void Start()
    {
        if (_listener is not null)
        {
            return;
        }

        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        Status = $"hosting seed {MapSeed}";
        _acceptTask = Task.Run(AcceptLoopAsync);
        _broadcastTask = Task.Run(BroadcastLoopAsync);
    }

    public void SetHostState(NetPlayerState state)
    {
        lock (_sync)
        {
            _hostState = state?.Clone() ?? new NetPlayerState();
            _hostState.PlayerId = 0;
        }
    }

    public List<NetPlayerState> GetSnapshotPlayers()
    {
        lock (_sync)
        {
            List<NetPlayerState> all = new List<NetPlayerState> { _hostState.Clone() };
            foreach (ClientPeer client in _clients.Values)
            {
                all.Add(client.State.Clone());
            }

            return all;
        }
    }

    public void SetHostWorldState(NetWorldState state)
    {
        lock (_sync)
        {
            _hostWorldState = state?.Clone() ?? new NetWorldState();
        }
    }

    public NetWorldState GetHostWorldState()
    {
        lock (_sync)
        {
            return _hostWorldState.Clone();
        }
    }

    public bool TryApplyDamageToPlayer(int playerId, int damage)
    {
        if (damage <= 0)
        {
            return false;
        }

        lock (_sync)
        {
            if (!_clients.TryGetValue(playerId, out ClientPeer? peer) || !peer.State.IsAlive)
            {
                return false;
            }

            int absorbed = Math.Min(peer.State.Armor, damage);
            peer.State.Armor -= absorbed;
            damage -= absorbed;
            peer.State.Health = Math.Max(0, peer.State.Health - damage);
            peer.State.IsAlive = peer.State.Health > 0;
            return true;
        }
    }

    public bool TryApplyPickupToPlayer(int playerId, PickupType type, int value)
    {
        lock (_sync)
        {
            if (!_clients.TryGetValue(playerId, out ClientPeer? peer))
            {
                return false;
            }

            switch (type)
            {
                case PickupType.Medkit:
                    peer.State.Health = Math.Min(100, peer.State.Health + Math.Max(0, value));
                    peer.State.IsAlive = peer.State.Health > 0;
                    break;
                case PickupType.Armor:
                    peer.State.Armor = Math.Max(0, peer.State.Armor + Math.Max(0, value));
                    break;
            }

            peer.State.PickupAnimation = Math.Max(peer.State.PickupAnimation, 0.45f);
            peer.State.PickupSequence++;
            return true;
        }
    }


    public int ConnectedCount
    {
        get
        {
            lock (_sync)
            {
                return 1 + _clients.Count;
            }
        }
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested && _listener is not null)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                tcpClient.NoDelay = true;
                tcpClient.ReceiveTimeout = 15000;
                tcpClient.SendTimeout = 15000;

                int assignedId;
                lock (_sync)
                {
                    if (_clients.Count >= MaxPlayers - 1)
                    {
                        tcpClient.Close();
                        continue;
                    }

                    assignedId = _nextId++;
                }

                _ = Task.Run(() => HandleClientAsync(tcpClient, assignedId));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Status = "server error: " + ex.Message;
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, int assignedId)
    {
        ClientPeer peer = new ClientPeer
        {
            PlayerId = assignedId,
            Client = tcpClient,
            Reader = new StreamReader(tcpClient.GetStream()),
            Writer = new StreamWriter(tcpClient.GetStream()) { AutoFlush = true },
            State = new NetPlayerState
            {
                PlayerId = assignedId,
                Callsign = "P" + assignedId,
                AccentArgb = unchecked((int)0xFFC0C0C0),
                WeaponName = "Pistol",
                WeaponLevel = 1
            }
        };

        peer.WriterTask = Task.Run(() => WriterLoopAsync(peer));

        lock (_sync)
        {
            _clients[assignedId] = peer;
        }

        EnqueueReliable(peer, new NetAssigned
        {
            PlayerId = assignedId,
            MaxPlayers = MaxPlayers,
            MapSeed = MapSeed,
            PlayerSnapshotRateHz = PlayerSnapshotRateHz,
            WorldSnapshotRateHz = WorldSnapshotRateHz
        });

        try
        {
            while (!_cts.IsCancellationRequested && tcpClient.Connected && !peer.IsClosed)
            {
                string? line;
                try
                {
                    line = await peer.Reader.ReadLineAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                peer.LastSeenUtc = DateTime.UtcNow;

                JsonDocument? doc = null;
                try
                {
                    doc = JsonDocument.Parse(line);
                    if (!doc.RootElement.TryGetProperty("Kind", out JsonElement kindElement))
                    {
                        continue;
                    }

                    string? kind = kindElement.GetString();
                    if (string.Equals(kind, "hello", StringComparison.OrdinalIgnoreCase))
                    {
                        NetHello? hello = JsonSerializer.Deserialize<NetHello>(line, _jsonOptions);
                        if (hello is not null && !string.IsNullOrWhiteSpace(hello.Callsign))
                        {
                            peer.State.Callsign = hello.Callsign.Trim();
                        }
                    }
                    else if (string.Equals(kind, "state", StringComparison.OrdinalIgnoreCase))
                    {
                        NetPlayerState? state = JsonSerializer.Deserialize<NetPlayerState>(line, _jsonOptions);
                        if (state is not null)
                        {
                            state.PlayerId = assignedId;
                            if (string.IsNullOrWhiteSpace(state.Callsign))
                            {
                                state.Callsign = peer.State.Callsign;
                            }

                            peer.State = state;
                        }
                    }
                    else if (string.Equals(kind, "ping", StringComparison.OrdinalIgnoreCase))
                    {
                        NetPing? ping = JsonSerializer.Deserialize<NetPing>(line, _jsonOptions);
                        if (ping is not null)
                        {
                            EnqueueReliable(peer, new NetPong
                            {
                                ClientUtcTicks = ping.ClientUtcTicks,
                                ServerUtcTicks = DateTime.UtcNow.Ticks
                            });
                        }
                    }
                }
                catch (JsonException)
                {
                }
                finally
                {
                    doc?.Dispose();
                }
            }
        }
        catch
        {
        }
        finally
        {
            RemovePeer(assignedId);
        }
    }

    private async Task WriterLoopAsync(ClientPeer peer)
    {
        try
        {
            while (!_cts.IsCancellationRequested && !peer.IsClosed && peer.Client.Connected)
            {
                await peer.SendSignal.WaitAsync(_cts.Token).ConfigureAwait(false);

                while (peer.ReliableQueue.TryDequeue(out string? json))
                {
                    await peer.Writer.WriteLineAsync(json).ConfigureAwait(false);
                }

                string? worldJson;
                string? playerJson;
                lock (peer.PendingSync)
                {
                    worldJson = peer.PendingWorldSnapshotJson;
                    peer.PendingWorldSnapshotJson = null;
                    playerJson = peer.PendingPlayerSnapshotJson;
                    peer.PendingPlayerSnapshotJson = null;
                }

                if (!string.IsNullOrWhiteSpace(worldJson))
                {
                    await peer.Writer.WriteLineAsync(worldJson).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(playerJson))
                {
                    await peer.Writer.WriteLineAsync(playerJson).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            peer.IsClosed = true;
        }
    }

    private async Task BroadcastLoopAsync()
    {
        int playerIntervalMs = Math.Max(16, 1000 / PlayerSnapshotRateHz);
        int worldIntervalMs = Math.Max(playerIntervalMs, 1000 / WorldSnapshotRateHz);
        DateTime nextPlayerBroadcastUtc = DateTime.UtcNow;
        DateTime nextWorldBroadcastUtc = DateTime.UtcNow;

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                await Task.Delay(10, _cts.Token).ConfigureAwait(false);

                DateTime now = DateTime.UtcNow;
                List<int> staleIds = new List<int>();
                List<ClientPeer> peers;
                lock (_sync)
                {
                    peers = _clients.Values.ToList();
                    foreach (ClientPeer peer in peers)
                    {
                        if ((now - peer.LastSeenUtc).TotalSeconds > 15 || peer.IsClosed || !peer.Client.Connected)
                        {
                            staleIds.Add(peer.PlayerId);
                        }
                    }
                }

                for (int i = 0; i < staleIds.Count; i++)
                {
                    RemovePeer(staleIds[i]);
                }

                if (now >= nextPlayerBroadcastUtc)
                {
                    string playerJson = JsonSerializer.Serialize(BuildPlayerSnapshot(), _jsonOptions);
                    BroadcastLatestPlayerSnapshot(playerJson);
                    nextPlayerBroadcastUtc = now.AddMilliseconds(playerIntervalMs);
                }

                if (now >= nextWorldBroadcastUtc)
                {
                    string worldJson = JsonSerializer.Serialize(BuildWorldSnapshot(), _jsonOptions);
                    BroadcastLatestWorldSnapshot(worldJson);
                    nextWorldBroadcastUtc = now.AddMilliseconds(worldIntervalMs);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Status = "broadcast error: " + ex.Message;
        }
    }

    private NetSnapshot BuildPlayerSnapshot()
    {
        NetSnapshot snapshot = new NetSnapshot { Tick = Interlocked.Increment(ref _tick), ServerUtcTicks = DateTime.UtcNow.Ticks };
        lock (_sync)
        {
            snapshot.Players.Add(_hostState.Clone());
            snapshot.ConnectedPlayers = 1 + _clients.Count;
            foreach (ClientPeer client in _clients.Values)
            {
                snapshot.Players.Add(client.State.Clone());
            }
        }

        return snapshot;
    }

    private NetWorldSnapshot BuildWorldSnapshot()
    {
        lock (_sync)
        {
            return new NetWorldSnapshot
            {
                Tick = Interlocked.Increment(ref _worldTick),
                ServerUtcTicks = DateTime.UtcNow.Ticks,
                World = _hostWorldState.Clone()
            };
        }
    }

    private void BroadcastLatestPlayerSnapshot(string json)
    {
        List<ClientPeer> peers;
        lock (_sync)
        {
            peers = _clients.Values.ToList();
        }

        foreach (ClientPeer peer in peers)
        {
            lock (peer.PendingSync)
            {
                peer.PendingPlayerSnapshotJson = json;
            }

            peer.SendSignal.Release();
        }
    }

    private void BroadcastLatestWorldSnapshot(string json)
    {
        List<ClientPeer> peers;
        lock (_sync)
        {
            peers = _clients.Values.ToList();
        }

        foreach (ClientPeer peer in peers)
        {
            lock (peer.PendingSync)
            {
                peer.PendingWorldSnapshotJson = json;
            }

            peer.SendSignal.Release();
        }
    }

    private void EnqueueReliable<T>(ClientPeer peer, T payload)
    {
        peer.ReliableQueue.Enqueue(JsonSerializer.Serialize(payload, _jsonOptions));
        peer.SendSignal.Release();
    }

    private void RemovePeer(int playerId)
    {
        ClientPeer? peer = null;
        lock (_sync)
        {
            if (_clients.TryGetValue(playerId, out peer))
            {
                _clients.Remove(playerId);
            }
        }

        if (peer is null)
        {
            return;
        }

        peer.IsClosed = true;
        try { peer.SendSignal.Release(); } catch { }
        try { peer.Client.Close(); } catch { }
    }

    public void Dispose()
    {
        _cts.Cancel();

        try { _listener?.Stop(); } catch { }
        _listener = null;

        List<ClientPeer> peers;
        lock (_sync)
        {
            peers = _clients.Values.ToList();
            _clients.Clear();
        }

        foreach (ClientPeer peer in peers)
        {
            peer.IsClosed = true;
            try { peer.SendSignal.Release(); } catch { }
            try { peer.Client.Close(); } catch { }
        }
    }
}
