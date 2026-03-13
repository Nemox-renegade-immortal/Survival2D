using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlayInspiredPrototype;

public sealed class NetworkClient : IDisposable
{
    private readonly object _sync = new object();
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly ConcurrentQueue<string> _reliableQueue = new ConcurrentQueue<string>();
    private readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0);
    private readonly TaskCompletionSource<NetAssigned> _assignedTcs = new TaskCompletionSource<NetAssigned>(TaskCreationOptions.RunContinuationsAsynchronously);
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Task? _receiveTask;
    private Task? _pingTask;
    private Task? _sendTask;
    private NetSnapshot _latestSnapshot = new NetSnapshot();
    private NetWorldState _latestWorldState = new NetWorldState();
    private NetPlayerState? _pendingState;
    private int _assignedPlayerId = -1;
    private int _mapSeed;
    private DateTime _lastSnapshotUtc = DateTime.MinValue;
    private DateTime _lastWorldSnapshotUtc = DateTime.MinValue;
    private int _roundTripMs;
    private int _lastKnownPlayers = 1;
    private int _playerSnapshotRateHz = 20;
    private int _worldSnapshotRateHz = 8;

    public string Host { get; }
    public int Port { get; }
    public string Status { get; private set; } = "offline";
    public bool IsConnected => _client?.Connected == true;
    public int AssignedPlayerId => _assignedPlayerId;
    public int MapSeed => _mapSeed;
    public int RoundTripMs => _roundTripMs;
    public int SnapshotAgeMs => _lastSnapshotUtc == DateTime.MinValue ? -1 : Math.Max(0, (int)(DateTime.UtcNow - _lastSnapshotUtc).TotalMilliseconds);
    public int PlayerSnapshotRateHz => _playerSnapshotRateHz;
    public int WorldSnapshotRateHz => _worldSnapshotRateHz;
    public int WorldSnapshotAgeMs => _lastWorldSnapshotUtc == DateTime.MinValue ? -1 : Math.Max(0, (int)(DateTime.UtcNow - _lastWorldSnapshotUtc).TotalMilliseconds);
    public int LatestSnapshotTick
    {
        get
        {
            lock (_sync)
            {
                return _latestSnapshot.Tick;
            }
        }
    }

    public int LastKnownPlayers => _lastKnownPlayers;

    public NetworkClient(string host, int port)
    {
        Host = host;
        Port = port;
    }

    public async Task ConnectAsync(string callsign)
    {
        _client = new TcpClient
        {
            NoDelay = true,
            ReceiveTimeout = 15000,
            SendTimeout = 15000
        };

        await _client.ConnectAsync(Host, Port).ConfigureAwait(false);
        _reader = new StreamReader(_client.GetStream());
        _writer = new StreamWriter(_client.GetStream()) { AutoFlush = true };
        Status = "connected";
        _sendTask = Task.Run(SendLoopAsync);
        _receiveTask = Task.Run(ReceiveLoopAsync);
        _pingTask = Task.Run(PingLoopAsync);
        EnqueueReliable(new NetHello { Callsign = callsign });

        Task completed = await Task.WhenAny(_assignedTcs.Task, Task.Delay(TimeSpan.FromSeconds(5), _cts.Token)).ConfigureAwait(false);
        if (completed != _assignedTcs.Task)
        {
            throw new TimeoutException("Server did not send assignment in time.");
        }

        NetAssigned assigned = await _assignedTcs.Task.ConfigureAwait(false);
        _assignedPlayerId = assigned.PlayerId;
        _mapSeed = assigned.MapSeed;
        _playerSnapshotRateHz = Math.Max(1, assigned.PlayerSnapshotRateHz);
        _worldSnapshotRateHz = Math.Max(1, assigned.WorldSnapshotRateHz);
        _lastKnownPlayers = 1;
        Status = $"connected seed {_mapSeed}";
    }

    public void SendState(NetPlayerState state)
    {
        if (!IsConnected)
        {
            return;
        }

        lock (_sync)
        {
            _pendingState = state.Clone();
        }

        _sendSignal.Release();
    }

    public NetSnapshot GetLatestSnapshot()
    {
        lock (_sync)
        {
            return new NetSnapshot
            {
                Kind = _latestSnapshot.Kind,
                Tick = _latestSnapshot.Tick,
                ServerUtcTicks = _latestSnapshot.ServerUtcTicks,
                ConnectedPlayers = _latestSnapshot.ConnectedPlayers,
                Players = _latestSnapshot.Players.ConvertAll(p => p.Clone())
            };
        }
    }

    public NetWorldState GetLatestWorldState()
    {
        lock (_sync)
        {
            return _latestWorldState.Clone();
        }
    }

    private async Task PingLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
                EnqueueReliable(new NetPing { ClientUtcTicks = DateTime.UtcNow.Ticks });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private async Task SendLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                await _sendSignal.WaitAsync(_cts.Token).ConfigureAwait(false);

                if (_writer is null)
                {
                    continue;
                }

                while (_reliableQueue.TryDequeue(out string? json))
                {
                    await _writer.WriteLineAsync(json).ConfigureAwait(false);
                }

                NetPlayerState? state = null;
                lock (_sync)
                {
                    state = _pendingState;
                    _pendingState = null;
                }

                if (state is not null)
                {
                    string json = JsonSerializer.Serialize(state, _jsonOptions);
                    await _writer.WriteLineAsync(json).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Status = "send error: " + ex.Message;
        }
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested && _reader is not null)
            {
                string? line;
                try
                {
                    line = await _reader.ReadLineAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                JsonDocument? doc = null;
                try
                {
                    doc = JsonDocument.Parse(line);
                    if (!doc.RootElement.TryGetProperty("Kind", out JsonElement kindElement))
                    {
                        continue;
                    }

                    string? kind = kindElement.GetString();
                    if (string.Equals(kind, "assigned", StringComparison.OrdinalIgnoreCase))
                    {
                        NetAssigned? assigned = JsonSerializer.Deserialize<NetAssigned>(line, _jsonOptions);
                        if (assigned is not null)
                        {
                            _assignedPlayerId = assigned.PlayerId;
                            _mapSeed = assigned.MapSeed;
                            _playerSnapshotRateHz = Math.Max(1, assigned.PlayerSnapshotRateHz);
                            _worldSnapshotRateHz = Math.Max(1, assigned.WorldSnapshotRateHz);
                            _assignedTcs.TrySetResult(assigned);
                        }
                    }
                    else if (string.Equals(kind, "snapshot", StringComparison.OrdinalIgnoreCase))
                    {
                        NetSnapshot? snapshot = JsonSerializer.Deserialize<NetSnapshot>(line, _jsonOptions);
                        if (snapshot is not null)
                        {
                            lock (_sync)
                            {
                                _latestSnapshot = snapshot;
                                _lastSnapshotUtc = DateTime.UtcNow;
                                _lastKnownPlayers = Math.Max(1, snapshot.ConnectedPlayers);
                            }
                        }
                    }
                    else if (string.Equals(kind, "world", StringComparison.OrdinalIgnoreCase))
                    {
                        NetWorldSnapshot? world = JsonSerializer.Deserialize<NetWorldSnapshot>(line, _jsonOptions);
                        if (world is not null)
                        {
                            lock (_sync)
                            {
                                _latestWorldState = world.World?.Clone() ?? new NetWorldState();
                                _lastWorldSnapshotUtc = DateTime.UtcNow;
                            }
                        }
                    }
                    else if (string.Equals(kind, "pong", StringComparison.OrdinalIgnoreCase))
                    {
                        NetPong? pong = JsonSerializer.Deserialize<NetPong>(line, _jsonOptions);
                        if (pong is not null && pong.ClientUtcTicks > 0)
                        {
                            _roundTripMs = Math.Max(0, (int)(TimeSpan.FromTicks(DateTime.UtcNow.Ticks - pong.ClientUtcTicks).TotalMilliseconds));
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
        catch (Exception ex)
        {
            Status = "client error: " + ex.Message;
            _assignedTcs.TrySetException(ex);
        }
        finally
        {
            Status = "disconnected";
            _assignedTcs.TrySetCanceled();
        }
    }

    private void EnqueueReliable<T>(T payload)
    {
        _reliableQueue.Enqueue(JsonSerializer.Serialize(payload, _jsonOptions));
        _sendSignal.Release();
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _sendSignal.Release(); } catch { }
        try { _client?.Close(); } catch { }
        _client = null;
    }
}
