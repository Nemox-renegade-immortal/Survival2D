using System.Drawing;
using System.Numerics;

namespace SlayInspiredPrototype;

public sealed class Bullet
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Lifetime;
    public int Damage;
    public bool Dead;
    public bool FromPlayer;
    public int OwnerPlayerIndex;
    public Color Tint;
    public string SourceWeaponName;

    public Bullet(Vector2 position, Vector2 velocity, float radius, float lifetime, int damage, bool fromPlayer, int ownerPlayerIndex, Color tint, string sourceWeaponName = "")
    {
        Position = position;
        Velocity = velocity;
        Radius = radius;
        Lifetime = lifetime;
        Damage = damage;
        Dead = false;
        FromPlayer = fromPlayer;
        OwnerPlayerIndex = ownerPlayerIndex;
        Tint = tint;
        SourceWeaponName = sourceWeaponName;
    }
}
