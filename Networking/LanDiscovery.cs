using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlayInspiredPrototype;

public sealed class LanServerInfo
{
    public string ServerName { get; set; } = "Server";
    public string Address { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 7777;
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; } = 64;
    public string MapName { get; set; } = "Arena";
    public int MapSeed { get; set; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    public LanServerInfo Clone()
    {
        return new LanServerInfo
        {
            ServerName = ServerName,
            Address = Address,
            Port = Port,
            PlayerCount = PlayerCount,
            MaxPlayers = MaxPlayers,
            MapName = MapName,
            MapSeed = MapSeed,
            LastSeenUtc = LastSeenUtc
        };
    }
}

internal sealed class LanDiscoveryPacket
{
    public string Kind { get; set; } = "slay_lan_beacon";
    public string ServerName { get; set; } = "Server";
    public int Port { get; set; } = 7777;
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; } = 64;
    public string MapName { get; set; } = "Arena";
    public int MapSeed { get; set; }
}

public sealed class LanDiscoveryService : IDisposable
{
    public const int DiscoveryPort = 47777;

    private readonly object _sync = new object();
    private readonly Dictionary<string, LanServerInfo> _servers = new Dictionary<string, LanServerInfo>(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly TimeSpan _serverTimeout = TimeSpan.FromSeconds(4);

    private UdpClient? _listener;
    private UdpClient? _sender;
    private Task? _listenTask;
    private Task? _hostTask;
    private Func<LanServerInfo>? _hostProvider;

    public LanDiscoveryService()
    {
        StartListening();
    }

    public IReadOnlyList<LanServerInfo> GetServers()
    {
        lock (_sync)
        {
            PruneExpiredServersNoLock();
            return _servers.Values
                .OrderByDescending(s => s.PlayerCount)
                .ThenBy(s => s.ServerName, StringComparer.OrdinalIgnoreCase)
                .Select(s => s.Clone())
                .ToList();
        }
    }

    public void StartHosting(Func<LanServerInfo> hostProvider)
    {
        StopHosting();
        _hostProvider = hostProvider;
        _sender = new UdpClient();
        _sender.EnableBroadcast = true;
        _sender.MulticastLoopback = false;
        _hostTask = Task.Run(HostLoopAsync);
    }

    public void StopHosting()
    {
        _hostProvider = null;
        try { _sender?.Close(); } catch { }
        _sender = null;
        _hostTask = null;
    }

    private void StartListening()
    {
        try
        {
            _listener = new UdpClient(AddressFamily.InterNetwork);
            _listener.Client.ExclusiveAddressUse = false;
            _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.EnableBroadcast = true;
            _listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
            _listenTask = Task.Run(ListenLoopAsync);
        }
        catch
        {
            try { _listener?.Close(); } catch { }
            _listener = null;
            _listenTask = null;
        }
    }

    private async Task ListenLoopAsync()
    {
        UdpClient? listener = _listener;
        if (listener is null)
        {
            return;
        }

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                UdpReceiveResult result = await listener.ReceiveAsync().ConfigureAwait(false);
                string json = System.Text.Encoding.UTF8.GetString(result.Buffer);
                LanDiscoveryPacket? packet;
                try
                {
                    packet = JsonSerializer.Deserialize<LanDiscoveryPacket>(json, _jsonOptions);
                }
                catch
                {
                    continue;
                }

                if (packet is null || !string.Equals(packet.Kind, "slay_lan_beacon", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string address = result.RemoteEndPoint.Address.ToString();
                string key = address + ":" + packet.Port;
                LanServerInfo info = new LanServerInfo
                {
                    ServerName = string.IsNullOrWhiteSpace(packet.ServerName) ? "Server" : packet.ServerName.Trim(),
                    Address = address,
                    Port = packet.Port,
                    PlayerCount = Math.Max(0, packet.PlayerCount),
                    MaxPlayers = Math.Max(1, packet.MaxPlayers),
                    MapName = string.IsNullOrWhiteSpace(packet.MapName) ? "Arena" : packet.MapName.Trim(),
                    MapSeed = packet.MapSeed,
                    LastSeenUtc = DateTime.UtcNow
                };

                lock (_sync)
                {
                    _servers[key] = info;
                    PruneExpiredServersNoLock();
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
        catch
        {
        }
    }

    private async Task HostLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested && _hostProvider is not null && _sender is not null)
            {
                LanServerInfo snapshot = _hostProvider();
                LanDiscoveryPacket packet = new LanDiscoveryPacket
                {
                    ServerName = snapshot.ServerName,
                    Port = snapshot.Port,
                    PlayerCount = snapshot.PlayerCount,
                    MaxPlayers = snapshot.MaxPlayers,
                    MapName = snapshot.MapName,
                    MapSeed = snapshot.MapSeed
                };

                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet, _jsonOptions));
                await _sender.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort)).ConfigureAwait(false);
                await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
        catch
        {
        }
    }

    private void PruneExpiredServersNoLock()
    {
        DateTime cutoff = DateTime.UtcNow - _serverTimeout;
        List<string> stale = new List<string>();
        foreach (KeyValuePair<string, LanServerInfo> pair in _servers)
        {
            if (pair.Value.LastSeenUtc < cutoff)
            {
                stale.Add(pair.Key);
            }
        }

        for (int i = 0; i < stale.Count; i++)
        {
            _servers.Remove(stale[i]);
        }
    }

    public static string TryGetBestLocalAddress()
    {
        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());
            IPAddress? best = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            if (best is not null)
            {
                return best.ToString();
            }
        }
        catch
        {
        }

        return "127.0.0.1";
    }

    public void Dispose()
    {
        _cts.Cancel();
        StopHosting();
        try { _listener?.Close(); } catch { }
        _listener = null;
    }
}
