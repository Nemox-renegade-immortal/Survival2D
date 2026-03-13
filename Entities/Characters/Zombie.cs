using System.Drawing;
using System.Numerics;

namespace SlayInspiredPrototype;

public enum ZombieKind
{
    Grunt,
    Runner,
    Tank,
    Spitter,
    Leaper,
    Exploder,
    Necromancer,
    Howler,
    Boss
}

public sealed class Zombie : Entity
{
    public int NetworkId;
    public ZombieKind Kind;
    public float MoveSpeed;
    public int ContactDamage;
    public float AttackCooldown;
    public float ContactInterval;
    public int RewardScore;
    public bool IsRanged;
    public float PreferredRange;
    public float ProjectileSpeed;
    public int ProjectileDamage;
    public int BurstProjectiles = 1;
    public float ProjectileSpread;
    public bool IsBoss;
    public float SpecialCooldown;
    public float SpecialInterval;
    public float ExplosionRadius;
    public int ExplosionDamage;
    public float LeapDistance;
    public float PathRefreshTimer;
    public Vector2 PathWaypoint;
    public float BuffTimer;
    public int BarricadeDamage;
    public Color Tint;
    public float SlowTimer;
    public float SlowMultiplier = 1f;
    public Vector2 KnockbackVelocity;

    public float EffectiveMoveSpeed => MoveSpeed * (BuffTimer > 0f ? 1.28f : 1f) * (SlowTimer > 0f ? Math.Clamp(SlowMultiplier, 0.2f, 1f) : 1f);

    public static Zombie Create(ZombieKind kind, Vector2 position, int wave)
    {
        switch (kind)
        {
            case ZombieKind.Runner:
                return new Zombie
                {
                    Kind = kind,
                    Position = position,
                    Radius = 13f,
                    MaxHealth = 24 + wave * 2,
                    Health = 24 + wave * 2,
                    MoveSpeed = 175f + wave * 3f,
                    ContactDamage = 10 + wave / 2,
                    ContactInterval = 0.5f,
                    RewardScore = 15,
                    BarricadeDamage = 12,
                    Tint = Color.FromArgb(235, 170, 80)
                };
            case ZombieKind.Tank:
                return new Zombie
                {
                    Kind = kind,
                    Position = position,
                    Radius = 22f,
                    MaxHealth = 120 + wave * 14,
                    Health = 120 + wave * 14,
                    MoveSpeed = 72f + wave * 1.6f,
                    ContactDamage = 24 + wave,
                    ContactInterval = 0.92f,
                    RewardScore = 40,
                    BarricadeDamage = 28,
                    Tint = Color.FromArgb(145, 70, 42)
                };
            case ZombieKind.Spitter:
                return new Zombie
                {
                    Kind = kind,
                    Position = position,
                    Radius = 16f,
                    MaxHealth = 42 + wave * 4,
                    Health = 42 + wave * 4,
                    MoveSpeed = 100f + wave * 2f,
                    ContactDamage = 7 + wave / 2,
                    ContactInterval = 0.9f,
                    RewardScore = 24,
                    IsRanged = true,
                    PreferredRange = 310f,
                    ProjectileSpeed = 350f + wave * 6f,
                    ProjectileDamage = 9 + wave / 2,
                    BarricadeDamage = 10,
                    Tint = Color.FromArgb(120, 120, 230)
                };
            case ZombieKind.Leaper:
                return new Zombie
                {
                    Kind = kind,
                    Position = position,
                    Radius = 15f,
                    MaxHealth = 34 + wave * 3,
                    Health = 34 + wave * 3,
                    MoveSpeed = 132f + wave * 2.6f,
                    ContactDamage = 15 + wave / 2,
                    ContactInterval = 0.66f,
                    RewardScore = 30,
                    SpecialInterval = 2.3f,
                    SpecialCooldown = 1.2f,
                    LeapDistance = 125f,
                    BarricadeDamage = 16,
                    Tint = Color.FromArgb(125, 225, 120)
                };
            case ZombieKind.Exploder:
                return new Zombie
                {
                    Kind = kind,
                    Position = position,
                    Radius = 17f,
                    MaxHealth = 46 + wave * 5,
                    Health = 46 + wave * 5,
                    MoveSpeed = 116f + wave * 2.1f,
                    ContactDamage = 6 + wave / 3,
                    ContactInterval = 0.8f,
                    RewardScore = 35,
                    ExplosionRadius = 96f,
                    ExplosionDamage = 18 + wave,
                    BarricadeDamage = 20,
                    Tint = Color.FromArgb(255, 140, 70)
                };
            case ZombieKind.Necromancer:
                return new Zombie
                {
                    Kind = kind,
                    Position = position,
                    Radius = 18f,
                    MaxHealth = 68 + wave * 7,
                    Health = 68 + wave * 7,
                    MoveSpeed = 88f + wave * 1.6f,
                    ContactDamage = 10 + wave / 2,
                    ContactInterval = 0.9f,
                    RewardScore = 48,
                    IsRanged = true,
                    PreferredRange = 340f,
                    ProjectileSpeed = 330f + wave * 5f,
                    ProjectileDamage = 10 + wave / 2,
                    BurstProjectiles = 2,
                    ProjectileSpread = 0.18f,
                    SpecialInterval = 6f,
                    SpecialCooldown = 2.5f,
                    BarricadeDamage = 14,
                    Tint = Color.FromArgb(166, 95, 215)
                };
            case ZombieKind.Howler:
                return new Zombie
                {
                    Kind = kind,
                    Position = position,
                    Radius = 17f,
                    MaxHealth = 58 + wave * 5,
                    Health = 58 + wave * 5,
                    MoveSpeed = 105f + wave * 2f,
                    ContactDamage = 12 + wave / 2,
                    ContactInterval = 0.82f,
                    RewardScore = 44,
                    SpecialInterval = 5f,
                    SpecialCooldown = 1.8f,
                    BarricadeDamage = 12,
                    Tint = Color.FromArgb(70, 195, 170)
                };
            case ZombieKind.Boss:
                return new Zombie
                {
                    Kind = kind,
                    Position = position,
                    Radius = 30f,
                    MaxHealth = 420 + wave * 26,
                    Health = 420 + wave * 26,
                    MoveSpeed = 86f + wave * 1.4f,
                    ContactDamage = 28 + wave,
                    ContactInterval = 0.68f,
                    RewardScore = 200,
                    IsRanged = true,
                    PreferredRange = 330f,
                    ProjectileSpeed = 430f + wave * 5f,
                    ProjectileDamage = 12 + wave / 2,
                    BurstProjectiles = 5,
                    ProjectileSpread = 0.9f,
                    IsBoss = true,
                    SpecialInterval = 6f,
                    SpecialCooldown = 2.4f,
                    BarricadeDamage = 38,
                    Tint = Color.FromArgb(180, 70, 170)
                };
            default:
                return new Zombie
                {
                    Kind = kind,
                    Position = position,
                    Radius = 16f,
                    MaxHealth = 44 + wave * 4,
                    Health = 44 + wave * 4,
                    MoveSpeed = 110f + wave * 2f,
                    ContactDamage = 12 + wave / 2,
                    ContactInterval = 0.72f,
                    RewardScore = 20,
                    BarricadeDamage = 12,
                    Tint = Color.FromArgb(215, 90, 90)
                };
        }
    }
}