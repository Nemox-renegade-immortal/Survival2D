using System.Numerics;

namespace SlayInspiredPrototype;

public sealed class Turret
{
    public Vector2 Position;
    public float Radius;
    public float Lifetime;
    public float MaxLifetime;
    public float FireCooldown;
    public float FireInterval;
    public float Range;
    public int Damage;
    public float BulletSpeed;
    public float AimAngle;

    public bool Dead => Lifetime <= 0f;

    public Turret(Vector2 position, float radius, float lifetime, float fireInterval, float range, int damage, float bulletSpeed)
    {
        Position = position;
        Radius = radius;
        Lifetime = lifetime;
        MaxLifetime = lifetime;
        FireInterval = fireInterval;
        Range = range;
        Damage = damage;
        BulletSpeed = bulletSpeed;
    }
}
