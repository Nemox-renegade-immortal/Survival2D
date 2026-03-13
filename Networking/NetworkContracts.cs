using System;
using System.Collections.Generic;
using System.Linq;

namespace SlayInspiredPrototype;

public sealed class NetHello
{
    public string Kind { get; set; } = "hello";
    public string Callsign { get; set; } = "";
}

public sealed class NetAssigned
{
    public string Kind { get; set; } = "assigned";
    public int PlayerId { get; set; }
    public int MaxPlayers { get; set; } = 64;
    public int MapSeed { get; set; }
    public int PlayerSnapshotRateHz { get; set; } = 20;
    public int WorldSnapshotRateHz { get; set; } = 8;
}

public sealed class NetPing
{
    public string Kind { get; set; } = "ping";
    public long ClientUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
}

public sealed class NetPong
{
    public string Kind { get; set; } = "pong";
    public long ClientUtcTicks { get; set; }
    public long ServerUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
}

public sealed class NetPlayerState
{
    public string Kind { get; set; } = "state";
    public int PlayerId { get; set; }
    public string Callsign { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float AimAngle { get; set; }
    public int Health { get; set; } = 100;
    public int Armor { get; set; }
    public bool IsAlive { get; set; } = true;
    public int AccentArgb { get; set; }
    public string WeaponName { get; set; } = "Pistol";
    public int WeaponLevel { get; set; } = 1;
    public int WeaponSlot { get; set; }
    public int SelectedHotbarRawIndex { get; set; }
    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public float MoveBlend { get; set; }
    public bool IsReloading { get; set; }
    public bool IsOverdriveActive { get; set; }
    public float ShootAnimation { get; set; }
    public float PickupAnimation { get; set; }
    public float UseAnimation { get; set; }
    public float ReloadAnimation { get; set; }
    public int ShotSequence { get; set; }
    public int GrenadeSequence { get; set; }
    public int TurretSequence { get; set; }
    public int BarricadeSequence { get; set; }
    public int OverdriveSequence { get; set; }
    public int PickupSequence { get; set; }

    public NetPlayerState Clone()
    {
        return new NetPlayerState
        {
            Kind = Kind,
            PlayerId = PlayerId,
            Callsign = Callsign,
            X = X,
            Y = Y,
            AimAngle = AimAngle,
            Health = Health,
            Armor = Armor,
            IsAlive = IsAlive,
            AccentArgb = AccentArgb,
            WeaponName = WeaponName,
            WeaponLevel = WeaponLevel,
            WeaponSlot = WeaponSlot,
            SelectedHotbarRawIndex = SelectedHotbarRawIndex,
            MoveX = MoveX,
            MoveY = MoveY,
            MoveBlend = MoveBlend,
            IsReloading = IsReloading,
            IsOverdriveActive = IsOverdriveActive,
            ShootAnimation = ShootAnimation,
            PickupAnimation = PickupAnimation,
            UseAnimation = UseAnimation,
            ReloadAnimation = ReloadAnimation,
            ShotSequence = ShotSequence,
            GrenadeSequence = GrenadeSequence,
            TurretSequence = TurretSequence,
            BarricadeSequence = BarricadeSequence,
            OverdriveSequence = OverdriveSequence,
            PickupSequence = PickupSequence
        };
    }
}

public sealed class NetZombieState
{
    public int Id { get; set; }
    public int KindId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public bool IsAlive { get; set; }
}

public sealed class NetPickupState
{
    public int Id { get; set; }
    public int TypeId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int Value { get; set; }
}

public sealed class NetScrapState
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int ScrapAmount { get; set; }
}

public sealed class NetTurretState
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float AimAngle { get; set; }
    public float Lifetime { get; set; }
    public float MaxLifetime { get; set; }
}

public sealed class NetLootCrateState
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public float HitFlash { get; set; }
    public bool Destroyed { get; set; }
}

public sealed class NetBulletState
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float Radius { get; set; }
    public float Lifetime { get; set; }
    public int Damage { get; set; }
    public bool FromPlayer { get; set; }
    public int OwnerPlayerId { get; set; }
    public int TintArgb { get; set; }
    public string SourceWeaponName { get; set; } = "";
}

public sealed class NetGrenadeState
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float Radius { get; set; }
    public float Lifetime { get; set; }
    public int Damage { get; set; }
    public float ExplosionRadius { get; set; }
    public int OwnerPlayerId { get; set; }
    public int TintArgb { get; set; }
}

public sealed class NetExplosionState
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; }
    public float Lifetime { get; set; }
    public float MaxLifetime { get; set; }
    public int TintArgb { get; set; }
}

public sealed class NetBarricadeState
{
    public int Tx { get; set; }
    public int Ty { get; set; }
    public int Health { get; set; }
}

public sealed class NetWorldState
{
    public int WaveNumber { get; set; }
    public bool WaveLive { get; set; }
    public int WaveSpawned { get; set; }
    public int WaveTarget { get; set; }
    public int Score { get; set; }
    public float SurvivalTime { get; set; }
    public bool NightWaveStarted { get; set; }
    public bool NightRewardGranted { get; set; }
    public bool IsNight { get; set; }
    public float PhaseTimer { get; set; }
    public float TotalTime { get; set; }
    public float Blend { get; set; }

    public List<NetZombieState> Zombies { get; set; } = new List<NetZombieState>();
    public List<NetPickupState> Pickups { get; set; } = new List<NetPickupState>();
    public List<NetScrapState> Scraps { get; set; } = new List<NetScrapState>();
    public List<NetTurretState> Turrets { get; set; } = new List<NetTurretState>();
    public List<NetLootCrateState> Crates { get; set; } = new List<NetLootCrateState>();
    public List<NetBulletState> Bullets { get; set; } = new List<NetBulletState>();
    public List<NetGrenadeState> Grenades { get; set; } = new List<NetGrenadeState>();
    public List<NetExplosionState> Explosions { get; set; } = new List<NetExplosionState>();
    public List<NetBarricadeState> Barricades { get; set; } = new List<NetBarricadeState>();

    public NetWorldState Clone()
    {
        return new NetWorldState
        {
            WaveNumber = WaveNumber,
            WaveLive = WaveLive,
            WaveSpawned = WaveSpawned,
            WaveTarget = WaveTarget,
            Score = Score,
            SurvivalTime = SurvivalTime,
            NightWaveStarted = NightWaveStarted,
            NightRewardGranted = NightRewardGranted,
            IsNight = IsNight,
            PhaseTimer = PhaseTimer,
            TotalTime = TotalTime,
            Blend = Blend,
            Zombies = Zombies.Select(z => new NetZombieState
            {
                Id = z.Id,
                KindId = z.KindId,
                X = z.X,
                Y = z.Y,
                VelocityX = z.VelocityX,
                VelocityY = z.VelocityY,
                Health = z.Health,
                MaxHealth = z.MaxHealth,
                IsAlive = z.IsAlive
            }).ToList(),
            Pickups = Pickups.Select(p => new NetPickupState
            {
                Id = p.Id,
                TypeId = p.TypeId,
                X = p.X,
                Y = p.Y,
                Value = p.Value
            }).ToList(),
            Scraps = Scraps.Select(s => new NetScrapState
            {
                Id = s.Id,
                X = s.X,
                Y = s.Y,
                ScrapAmount = s.ScrapAmount
            }).ToList(),
            Turrets = Turrets.Select(t => new NetTurretState
            {
                Id = t.Id,
                X = t.X,
                Y = t.Y,
                AimAngle = t.AimAngle,
                Lifetime = t.Lifetime,
                MaxLifetime = t.MaxLifetime
            }).ToList(),
            Crates = Crates.Select(c => new NetLootCrateState
            {
                Id = c.Id,
                X = c.X,
                Y = c.Y,
                Radius = c.Radius,
                Health = c.Health,
                MaxHealth = c.MaxHealth,
                HitFlash = c.HitFlash,
                Destroyed = c.Destroyed
            }).ToList(),
            Bullets = Bullets.Select(b => new NetBulletState
            {
                Id = b.Id,
                X = b.X,
                Y = b.Y,
                VelocityX = b.VelocityX,
                VelocityY = b.VelocityY,
                Radius = b.Radius,
                Lifetime = b.Lifetime,
                Damage = b.Damage,
                FromPlayer = b.FromPlayer,
                OwnerPlayerId = b.OwnerPlayerId,
                TintArgb = b.TintArgb,
                SourceWeaponName = b.SourceWeaponName
            }).ToList(),
            Grenades = Grenades.Select(g => new NetGrenadeState
            {
                Id = g.Id,
                X = g.X,
                Y = g.Y,
                VelocityX = g.VelocityX,
                VelocityY = g.VelocityY,
                Radius = g.Radius,
                Lifetime = g.Lifetime,
                Damage = g.Damage,
                ExplosionRadius = g.ExplosionRadius,
                OwnerPlayerId = g.OwnerPlayerId,
                TintArgb = g.TintArgb
            }).ToList(),
            Explosions = Explosions.Select(e => new NetExplosionState
            {
                Id = e.Id,
                X = e.X,
                Y = e.Y,
                Radius = e.Radius,
                Lifetime = e.Lifetime,
                MaxLifetime = e.MaxLifetime,
                TintArgb = e.TintArgb
            }).ToList(),
            Barricades = Barricades.Select(b => new NetBarricadeState
            {
                Tx = b.Tx,
                Ty = b.Ty,
                Health = b.Health
            }).ToList()
        };
    }
}

public sealed class NetSnapshot
{
    public string Kind { get; set; } = "snapshot";
    public int Tick { get; set; }
    public long ServerUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
    public int ConnectedPlayers { get; set; }
    public List<NetPlayerState> Players { get; set; } = new List<NetPlayerState>();
}

public sealed class NetWorldSnapshot
{
    public string Kind { get; set; } = "world";
    public int Tick { get; set; }
    public long ServerUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
    public NetWorldState World { get; set; } = new NetWorldState();
}
