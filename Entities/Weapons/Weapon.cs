using System;
using System.Collections.Generic;
using System.Linq;

namespace SlayInspiredPrototype;

public enum WeaponUpgradeKind
{
    Damage,
    FireRate,
    Reload,
    Accuracy,
    Magazine
}

public sealed class Weapon
{
    public string Name { get; init; } = "Rifle";
    public string Description { get; init; } = "Balanced starter rifle.";
    public int UnlockCost { get; init; } = 0;
    public float FireInterval { get; init; } = 0.12f;
    public float BulletSpeed { get; init; } = 860f;
    public int Damage { get; init; } = 20;
    public int ClipSize { get; init; } = 24;
    public int StartingReserve { get; init; } = 144;
    public int AmmoPickupAmount { get; init; } = 36;
    public float ReloadTime { get; init; } = 1.15f;
    public float BulletRadius { get; init; } = 5f;
    public float SpreadRadians { get; init; } = 0.03f;
    public int Pellets { get; init; } = 1;
    public float BulletLifetime { get; init; } = 1.2f;
    public float Recoil { get; init; } = 1f;
    public float ZombieSlowSeconds { get; init; } = 0.8f;
    public float ZombieSlowMultiplier { get; init; } = 0.82f;
    public float ZombieKnockback { get; init; } = 18f;
}

public sealed class WeaponState
{
    public Weapon Definition { get; private set; }
    public int AmmoInClip;
    public int AmmoReserve;
    public bool Unlocked;
    public int Level { get; private set; }
    public int Experience { get; private set; }
    public int StatPoints { get; private set; }
    public int DamageTier { get; private set; }
    public int FireRateTier { get; private set; }
    public int ReloadTier { get; private set; }
    public int AccuracyTier { get; private set; }
    public int MagazineTier { get; private set; }
    public bool IsEmpty => ReferenceEquals(Definition, WeaponCatalog.Empty);

    public WeaponState(Weapon definition, bool unlocked)
    {
        Definition = definition;
        Reset(unlocked);
    }

    public int NextLevelExperience => 70 + Math.Max(0, Level - 1) * 45;
    public float LevelProgress => Level <= 0 ? 0f : Math.Clamp(Experience / (float)Math.Max(1, NextLevelExperience), 0f, 1f);
    public int EffectiveClipSize => Math.Max(1, Definition.ClipSize + MagazineTier * Math.Max(1, Definition.ClipSize / 8));
    public int EffectiveDamage => Math.Max(1, (int)MathF.Round(Definition.Damage * (1f + DamageTier * 0.14f)));
    public float EffectiveFireInterval => Definition.FireInterval / MathF.Max(0.35f, 1f + FireRateTier * 0.09f);
    public float EffectiveReloadTime => Definition.ReloadTime / MathF.Max(0.35f, 1f + ReloadTier * 0.08f);
    public float EffectiveSpread => MathF.Max(0.005f, Definition.SpreadRadians * MathF.Max(0.45f, 1f - AccuracyTier * 0.09f));
    public float EffectiveRecoil => MathF.Max(0.2f, Definition.Recoil * MathF.Max(0.55f, 1f - AccuracyTier * 0.05f));
    public float EffectiveZombieSlowSeconds => Definition.ZombieSlowSeconds * (1f + DamageTier * 0.04f);
    public float EffectiveZombieSlowMultiplier => Math.Clamp(Definition.ZombieSlowMultiplier - AccuracyTier * 0.015f, 0.38f, 1f);
    public float EffectiveZombieKnockback => Definition.ZombieKnockback * (1f + DamageTier * 0.08f);

    public void Replace(Weapon definition, bool unlocked)
    {
        Definition = definition;
        Reset(unlocked);
    }

    public void Reset(bool unlocked)
    {
        Unlocked = unlocked && !ReferenceEquals(Definition, WeaponCatalog.Empty);
        Level = Unlocked ? 1 : 0;
        Experience = 0;
        StatPoints = 0;
        DamageTier = 0;
        FireRateTier = 0;
        ReloadTier = 0;
        AccuracyTier = 0;
        MagazineTier = 0;
        AmmoInClip = Unlocked ? EffectiveClipSize : 0;
        AmmoReserve = Unlocked ? Definition.StartingReserve : 0;
    }

    public bool AddExperience(int amount)
    {
        if (!Unlocked || IsEmpty || amount <= 0)
        {
            return false;
        }

        bool leveledUp = false;
        Experience += amount;
        while (Experience >= NextLevelExperience)
        {
            Experience -= NextLevelExperience;
            Level++;
            StatPoints++;
            leveledUp = true;
        }

        return leveledUp;
    }

    public bool TrySpendPoint(WeaponUpgradeKind kind)
    {
        if (!Unlocked || IsEmpty || StatPoints <= 0)
        {
            return false;
        }

        StatPoints--;

        switch (kind)
        {
            case WeaponUpgradeKind.Damage:
                DamageTier++;
                break;
            case WeaponUpgradeKind.FireRate:
                FireRateTier++;
                break;
            case WeaponUpgradeKind.Reload:
                ReloadTier++;
                break;
            case WeaponUpgradeKind.Accuracy:
                AccuracyTier++;
                break;
            case WeaponUpgradeKind.Magazine:
                MagazineTier++;
                AmmoInClip = Math.Min(EffectiveClipSize, AmmoInClip + Math.Max(1, Definition.ClipSize / 8));
                break;
            default:
                StatPoints++;
                return false;
        }

        ClampAmmo();
        return true;
    }

    public void ClampAmmo()
    {
        AmmoInClip = Math.Clamp(AmmoInClip, 0, EffectiveClipSize);
        AmmoReserve = Math.Max(0, AmmoReserve);
    }
}

public static class WeaponCatalog
{
    public static Weapon Empty { get; } = new()
    {
        Name = "Empty",
        Description = "Unused slot.",
        UnlockCost = 0,
        FireInterval = 999f,
        BulletSpeed = 0f,
        Damage = 0,
        ClipSize = 0,
        StartingReserve = 0,
        AmmoPickupAmount = 0,
        ReloadTime = 0f,
        BulletRadius = 0f,
        SpreadRadians = 0f,
        Pellets = 0,
        BulletLifetime = 0f,
        Recoil = 0f,
        ZombieSlowSeconds = 0f,
        ZombieSlowMultiplier = 1f,
        ZombieKnockback = 0f
    };

    public static Weapon Rifle { get; } = new()
    {
        Name = "Pistol",
        Description = "Starter sidearm. Fast draw, low recoil and decent ammo economy.",
        UnlockCost = 0,
        FireInterval = 0.19f,
        BulletSpeed = 880f,
        Damage = 14,
        ClipSize = 12,
        StartingReserve = 96,
        AmmoPickupAmount = 24,
        ReloadTime = 1.02f,
        BulletRadius = 4.5f,
        SpreadRadians = 0.028f,
        Pellets = 1,
        BulletLifetime = 1.18f,
        Recoil = 0.7f,
        ZombieSlowSeconds = 0.95f,
        ZombieSlowMultiplier = 0.72f,
        ZombieKnockback = 30f
    };

    public static Weapon Smg { get; } = new()
    {
        Name = "SMG",
        Description = "Fast hose for close brawls.",
        UnlockCost = 80,
        FireInterval = 0.065f,
        BulletSpeed = 820f,
        Damage = 11,
        ClipSize = 36,
        StartingReserve = 216,
        AmmoPickupAmount = 54,
        ReloadTime = 1.35f,
        BulletRadius = 4f,
        SpreadRadians = 0.08f,
        Pellets = 1,
        BulletLifetime = 1.1f,
        Recoil = 1.18f,
        ZombieSlowSeconds = 0.75f,
        ZombieSlowMultiplier = 0.82f,
        ZombieKnockback = 18f
    };

    public static Weapon Shotgun { get; } = new()
    {
        Name = "Shotgun",
        Description = "Heavy spread for rooms and panic moments.",
        UnlockCost = 120,
        FireInterval = 0.8f,
        BulletSpeed = 740f,
        Damage = 8,
        ClipSize = 8,
        StartingReserve = 48,
        AmmoPickupAmount = 12,
        ReloadTime = 1.55f,
        BulletRadius = 4f,
        SpreadRadians = 0.34f,
        Pellets = 7,
        BulletLifetime = 0.55f,
        Recoil = 1.65f,
        ZombieSlowSeconds = 1.55f,
        ZombieSlowMultiplier = 0.56f,
        ZombieKnockback = 52f
    };

    public static Weapon Carbine { get; } = new()
    {
        Name = "Carbine",
        Description = "High damage precision shot platform.",
        UnlockCost = 150,
        FireInterval = 0.28f,
        BulletSpeed = 980f,
        Damage = 34,
        ClipSize = 14,
        StartingReserve = 84,
        AmmoPickupAmount = 24,
        ReloadTime = 1.45f,
        BulletRadius = 5f,
        SpreadRadians = 0.02f,
        Pellets = 1,
        BulletLifetime = 1.4f,
        Recoil = 1.35f,
        ZombieSlowSeconds = 1.2f,
        ZombieSlowMultiplier = 0.68f,
        ZombieKnockback = 42f
    };

    public static IReadOnlyList<Weapon> All { get; } = new[] { Rifle, Smg, Shotgun, Carbine };

    public static Weapon? FindByName(string name) => All.FirstOrDefault(w => w.Name == name);
}
