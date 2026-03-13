using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace SlayInspiredPrototype;

public enum NetMode
{
    None,
    Host,
    Client
}

public sealed class NetworkManager : IDisposable
{
    private readonly LanDiscoveryService _lanDiscovery = new LanDiscoveryService();
    private readonly string _clientSessionId = Guid.NewGuid().ToString("N");
    private int _localStateRevision;
    private NetworkServer? _server;
    private NetworkClient? _client;

    public NetMode Mode { get; private set; } = NetMode.None;
    public string HostAddress { get; private set; } = "127.0.0.1";
    public int Port { get; private set; } = 7777;
    public int MaxPlayers { get; private set; } = 64;
    public int LocalPlayerId { get; private set; }
    public int MapSeed { get; private set; }

    public int PingMs => Mode == NetMode.Client && _client is not null ? _client.RoundTripMs : 0;
    public int SnapshotAgeMs => Mode == NetMode.Client && _client is not null ? _client.SnapshotAgeMs : 0;
    public int WorldSnapshotAgeMs => Mode == NetMode.Client && _client is not null ? _client.WorldSnapshotAgeMs : 0;
    public int LatestSnapshotTick => Mode == NetMode.Client && _client is not null ? _client.LatestSnapshotTick : 0;
    public int ConnectedPlayers => Mode switch
    {
        NetMode.Host when _server is not null => _server.ConnectedCount,
        NetMode.Client when _client is not null => _client.LastKnownPlayers,
        _ => 1
    };

    public string StatusText
    {
        get
        {
            return Mode switch
            {
                NetMode.Host => _server is null ? "host offline" : $"HOST tcp/{Port} seed {MapSeed} players {_server.ConnectedCount}/{MaxPlayers}",
                NetMode.Client => _client is null ? "client offline" : $"CLIENT {_client.Status} {_client.AssignedPlayerId}@{HostAddress}:{Port} seed {MapSeed} ping {PingMs}ms age {SnapshotAgeMs}ms",
                _ => "offline"
            };
        }
    }

    public void StartHost(int port, int maxPlayers, int? mapSeed = null)
    {
        Stop();
        Port = port;
        MaxPlayers = maxPlayers;
        LocalPlayerId = 0;
        _localStateRevision = 0;
        MapSeed = mapSeed ?? unchecked((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));
        if (MapSeed == 0)
        {
            MapSeed = 1;
        }

        HostAddress = LanDiscoveryService.TryGetBestLocalAddress();
        _server = new NetworkServer(port, maxPlayers, MapSeed);
        _server.Start();
        _lanDiscovery.StartHosting(() => new LanServerInfo
        {
            ServerName = Environment.MachineName,
            Address = HostAddress,
            Port = Port,
            PlayerCount = ConnectedPlayers,
            MaxPlayers = MaxPlayers,
            MapName = $"Seed {MapSeed}",
            MapSeed = MapSeed,
            LastSeenUtc = DateTime.UtcNow
        });
        Mode = NetMode.Host;
    }

    public void Join(string hostAddress, int port, string callsign)
    {
        Stop();
        HostAddress = hostAddress;
        Port = port;
        _localStateRevision = 0;
        _client = new NetworkClient(hostAddress, port);
        _client.ConnectAsync(callsign, _clientSessionId).GetAwaiter().GetResult();
        Mode = NetMode.Client;
        MapSeed = _client.MapSeed;
        LocalPlayerId = _client.AssignedPlayerId >= 0 ? _client.AssignedPlayerId : 1;
    }

    public void Stop()
    {
        Mode = NetMode.None;
        LocalPlayerId = 0;
        MapSeed = 0;
        _localStateRevision = 0;
        _lanDiscovery.StopHosting();
        try { _server?.Dispose(); } catch { }
        try { _client?.Dispose(); } catch { }
        _server = null;
        _client = null;
    }

    public void PushLocalState(
        string callsign,
        Color accent,
        Vector2 position,
        float aimAngle,
        int health,
        bool isAlive,
        int armor = 0,
        int maxHealth = 100,
        string weaponName = "Pistol",
        int weaponLevel = 1,
        int weaponSlot = 0,
        int selectedHotbarRawIndex = 0,
        int currentWeaponAmmoInClip = 0,
        int currentWeaponAmmoReserve = 0,
        int credits = 0,
        int scrap = 0,
        int barricadeKits = 0,
        int kills = 0,
        int turretCharges = 0,
        int maxTurretCharges = 1,
        float adrenaline = 0f,
        float maxAdrenaline = 100f,
        Vector2 moveInput = default,
        float moveBlend = 0f,
        float fireTimer = 0f,
        int weaponStateSequence = 0,
        bool isReloading = false,
        float reloadTimer = 0f,
        bool isOverdriveActive = false,
        float shootAnimation = 0f,
        float pickupAnimation = 0f,
        float useAnimation = 0f,
        float reloadAnimation = 0f,
        int shotSequence = 0,
        int grenadeSequence = 0,
        int turretSequence = 0,
        int barricadeSequence = 0,
        int overdriveSequence = 0,
        int pickupSequence = 0)
    {
        NetPlayerState state = new NetPlayerState
        {
            PlayerId = LocalPlayerId,
            Callsign = callsign,
            X = position.X,
            Y = position.Y,
            AimAngle = aimAngle,
            Health = health,
            MaxHealth = maxHealth,
            Armor = armor,
            IsAlive = isAlive,
            AccentArgb = accent.ToArgb(),
            WeaponName = weaponName,
            WeaponLevel = weaponLevel,
            WeaponSlot = weaponSlot,
            SelectedHotbarRawIndex = selectedHotbarRawIndex,
            CurrentWeaponAmmoInClip = currentWeaponAmmoInClip,
            CurrentWeaponAmmoReserve = currentWeaponAmmoReserve,
            Credits = credits,
            Scrap = scrap,
            BarricadeKits = barricadeKits,
            Kills = kills,
            TurretCharges = turretCharges,
            MaxTurretCharges = maxTurretCharges,
            Adrenaline = adrenaline,
            MaxAdrenaline = maxAdrenaline,
            MoveX = moveInput.X,
            MoveY = moveInput.Y,
            MoveBlend = moveBlend,
            ClientStateRevision = ++_localStateRevision,
            WeaponStateSequence = weaponStateSequence,
            FireTimer = fireTimer,
            IsReloading = isReloading,
            ReloadTimer = reloadTimer,
            IsOverdriveActive = isOverdriveActive,
            ShootAnimation = shootAnimation,
            PickupAnimation = pickupAnimation,
            UseAnimation = useAnimation,
            ReloadAnimation = reloadAnimation,
            ShotSequence = shotSequence,
            GrenadeSequence = grenadeSequence,
            TurretSequence = turretSequence,
            BarricadeSequence = barricadeSequence,
            OverdriveSequence = overdriveSequence,
            PickupSequence = pickupSequence
        };

        if (Mode == NetMode.Host)
        {
            _server?.SetHostState(state);
        }
        else if (Mode == NetMode.Client)
        {
            if (_client is not null && _client.AssignedPlayerId > 0)
            {
                state.PlayerId = _client.AssignedPlayerId;
                LocalPlayerId = _client.AssignedPlayerId;
            }

            _client?.SendState(state);
        }
    }

    public void PushWorldState(NetWorldState state)
    {
        if (Mode == NetMode.Host)
        {
            _server?.SetHostWorldState(state);
        }
    }

    public NetWorldState GetWorldState()
    {
        return Mode switch
        {
            NetMode.Host when _server is not null => _server.GetHostWorldState(),
            NetMode.Client when _client is not null => _client.GetLatestWorldState(),
            _ => new NetWorldState()
        };
    }

    public IReadOnlyList<LanServerInfo> GetLanServers()
    {
        return _lanDiscovery.GetServers();
    }

    public IReadOnlyList<RemotePlayerView> GetRemotePlayers()
    {
        List<RemotePlayerView> result = new List<RemotePlayerView>();

        if (Mode == NetMode.Host && _server is not null)
        {
            foreach (NetPlayerState state in _server.GetSnapshotPlayers())
            {
                if (state.PlayerId == 0)
                {
                    continue;
                }

                result.Add(ToView(state));
            }
        }
        else if (Mode == NetMode.Client && _client is not null)
        {
            NetSnapshot snapshot = _client.GetLatestSnapshot();
            foreach (NetPlayerState state in snapshot.Players)
            {
                int localId = _client.AssignedPlayerId >= 0 ? _client.AssignedPlayerId : LocalPlayerId;
                if (state.PlayerId == localId)
                {
                    continue;
                }

                result.Add(ToView(state));
            }
        }

        return result;
    }

    public NetPlayerState? GetLocalPlayerState()
    {
        if (Mode != NetMode.Client || _client is null)
        {
            return null;
        }

        int localId = _client.AssignedPlayerId >= 0 ? _client.AssignedPlayerId : LocalPlayerId;
        if (localId <= 0)
        {
            return null;
        }

        NetSnapshot snapshot = _client.GetLatestSnapshot();
        NetPlayerState? state = snapshot.Players.FirstOrDefault(p => p.PlayerId == localId);
        return state?.Clone();
    }

    public bool ApplyRemotePlayerDamage(int playerId, int damage)
    {
        return Mode == NetMode.Host && _server is not null && _server.TryApplyDamageToPlayer(playerId, damage);
    }

    public bool ApplyRemotePlayerPickup(int playerId, PickupType type, int value)
    {
        return Mode == NetMode.Host && _server is not null && _server.TryApplyPickupToPlayer(playerId, type, value);
    }

    public bool ApplyRemotePlayerScrap(int playerId, int value)
    {
        return Mode == NetMode.Host && _server is not null && _server.TryApplyScrapToPlayer(playerId, value);
    }

    public bool AwardRemotePlayerKill(int playerId, int reward)
    {
        return Mode == NetMode.Host && _server is not null && _server.TryAwardKillToPlayer(playerId, reward);
    }

    private static RemotePlayerView ToView(NetPlayerState state)
    {
        return new RemotePlayerView
        {
            PlayerId = state.PlayerId,
            Callsign = string.IsNullOrWhiteSpace(state.Callsign) ? $"P{state.PlayerId}" : state.Callsign,
            Position = new Vector2(state.X, state.Y),
            TargetPosition = new Vector2(state.X, state.Y),
            AimAngle = state.AimAngle,
            Health = state.Health,
            Armor = state.Armor,
            IsAlive = state.IsAlive,
            WeaponName = string.IsNullOrWhiteSpace(state.WeaponName) ? "Pistol" : state.WeaponName,
            WeaponLevel = Math.Max(1, state.WeaponLevel),
            WeaponSlot = state.WeaponSlot,
            SelectedHotbarRawIndex = state.SelectedHotbarRawIndex,
            Accent = Color.FromArgb(state.AccentArgb),
            MoveInput = new Vector2(state.MoveX, state.MoveY),
            MoveBlend = state.MoveBlend,
            IsReloading = state.IsReloading,
            IsOverdriveActive = state.IsOverdriveActive,
            ShootAnimation = state.ShootAnimation,
            PickupAnimation = state.PickupAnimation,
            UseAnimation = state.UseAnimation,
            ReloadAnimation = state.ReloadAnimation,
            ShotSequence = state.ShotSequence,
            GrenadeSequence = state.GrenadeSequence,
            TurretSequence = state.TurretSequence,
            BarricadeSequence = state.BarricadeSequence,
            OverdriveSequence = state.OverdriveSequence,
            PickupSequence = state.PickupSequence
        };
    }

    public void Dispose()
    {
        Stop();
        _lanDiscovery.Dispose();
    }
}
