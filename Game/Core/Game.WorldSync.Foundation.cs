using System.Drawing;
using System.Numerics;

namespace SlayInspiredPrototype;

public sealed partial class Game
{
    private NetWorldState BuildWorldSyncState()
    {
        NetWorldState state = new NetWorldState
        {
            WaveNumber = _waveNumber,
            WaveLive = _waveLive,
            WaveSpawned = _waveSpawned,
            WaveTarget = _waveTarget,
            Score = _score,
            SurvivalTime = _survivalTime,
            NightWaveStarted = _nightWaveStarted,
            NightRewardGranted = _nightRewardGranted,
            IsNight = _dayNight.IsNight,
            PhaseTimer = _dayNight.PhaseTimer,
            TotalTime = _dayNight.TotalTime,
            Blend = _dayNight.Blend
        };

        for (int i = 0; i < _zombies.Count; i++)
        {
            Zombie zombie = _zombies[i];
            state.Zombies.Add(new NetZombieState
            {
                Id = zombie.NetworkId,
                KindId = (int)zombie.Kind,
                X = zombie.Position.X,
                Y = zombie.Position.Y,
                VelocityX = zombie.Velocity.X,
                VelocityY = zombie.Velocity.Y,
                Health = zombie.Health,
                MaxHealth = zombie.MaxHealth,
                IsAlive = zombie.IsAlive
            });
        }

        for (int i = 0; i < _pickups.Count; i++)
        {
            Pickup pickup = _pickups[i];
            if (pickup.Collected)
            {
                continue;
            }

            state.Pickups.Add(new NetPickupState
            {
                Id = i,
                TypeId = (int)pickup.Type,
                X = pickup.Position.X,
                Y = pickup.Position.Y,
                Value = pickup.Value
            });
        }

        for (int i = 0; i < _scrapPiles.Count; i++)
        {
            ScrapPile scrap = _scrapPiles[i];
            if (scrap.Collected)
            {
                continue;
            }

            state.Scraps.Add(new NetScrapState
            {
                Id = i,
                X = scrap.Position.X,
                Y = scrap.Position.Y,
                ScrapAmount = scrap.ScrapAmount
            });
        }

        for (int i = 0; i < _turrets.Count; i++)
        {
            Turret turret = _turrets[i];
            state.Turrets.Add(new NetTurretState
            {
                Id = i,
                X = turret.Position.X,
                Y = turret.Position.Y,
                AimAngle = turret.AimAngle,
                Lifetime = turret.Lifetime,
                MaxLifetime = turret.MaxLifetime
            });
        }

        for (int i = 0; i < _lootCrates.Count; i++)
        {
            LootCrate crate = _lootCrates[i];
            state.Crates.Add(new NetLootCrateState
            {
                Id = crate.NetworkId > 0 ? crate.NetworkId : i + 1,
                X = crate.Position.X,
                Y = crate.Position.Y,
                Radius = crate.Radius,
                Health = crate.Health,
                MaxHealth = crate.MaxHealth,
                HitFlash = crate.HitFlash,
                Destroyed = crate.Destroyed
            });
        }

        for (int i = 0; i < _bullets.Count; i++)
        {
            Bullet bullet = _bullets[i];
            state.Bullets.Add(new NetBulletState
            {
                Id = i,
                X = bullet.Position.X,
                Y = bullet.Position.Y,
                VelocityX = bullet.Velocity.X,
                VelocityY = bullet.Velocity.Y,
                Radius = bullet.Radius,
                Lifetime = bullet.Lifetime,
                Damage = bullet.Damage,
                FromPlayer = bullet.FromPlayer,
                OwnerPlayerId = bullet.OwnerPlayerIndex,
                TintArgb = bullet.Tint.ToArgb(),
                SourceWeaponName = bullet.SourceWeaponName
            });
        }

        for (int i = 0; i < _grenades.Count; i++)
        {
            Grenade grenade = _grenades[i];
            state.Grenades.Add(new NetGrenadeState
            {
                Id = i,
                X = grenade.Position.X,
                Y = grenade.Position.Y,
                VelocityX = grenade.Velocity.X,
                VelocityY = grenade.Velocity.Y,
                Radius = grenade.Radius,
                Lifetime = grenade.Lifetime,
                Damage = grenade.Damage,
                ExplosionRadius = grenade.ExplosionRadius,
                OwnerPlayerId = grenade.OwnerPlayerIndex,
                TintArgb = grenade.Tint.ToArgb()
            });
        }

        for (int i = 0; i < _explosions.Count; i++)
        {
            Explosion explosion = _explosions[i];
            state.Explosions.Add(new NetExplosionState
            {
                Id = i,
                X = explosion.Position.X,
                Y = explosion.Position.Y,
                Radius = explosion.Radius,
                Lifetime = explosion.Lifetime,
                MaxLifetime = explosion.MaxLifetime,
                TintArgb = explosion.Tint.ToArgb()
            });
        }

        foreach ((int tx, int ty, int health) in Map.GetBarricades())
        {
            state.Barricades.Add(new NetBarricadeState
            {
                Tx = tx,
                Ty = ty,
                Health = health
            });
        }

        return state;
    }

    private void PushWorldSyncState()
    {
        if (_network.Mode == NetMode.Host)
        {
            _network.PushWorldState(BuildWorldSyncState());
        }
    }
}
