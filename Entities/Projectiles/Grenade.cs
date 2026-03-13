using System.Drawing;
using System.Numerics;

namespace SlayInspiredPrototype;

public sealed class Grenade
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Lifetime;
    public int Damage;
    public float ExplosionRadius;
    public bool Dead;
    public int OwnerPlayerIndex;
    public Color Tint;

    public Grenade(Vector2 position, Vector2 velocity, float radius, float lifetime, int damage, float explosionRadius, int ownerPlayerIndex, Color tint)
    {
        Position = position;
        Velocity = velocity;
        Radius = radius;
        Lifetime = lifetime;
        Damage = damage;
        ExplosionRadius = explosionRadius;
        OwnerPlayerIndex = ownerPlayerIndex;
        Dead = false;
        Tint = tint;
    }
}
