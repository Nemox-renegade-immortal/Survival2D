using System.Drawing;
using System.Numerics;

namespace SlayInspiredPrototype;

public sealed class Explosion
{
    public Vector2 Position;
    public float Radius;
    public float Lifetime;
    public float MaxLifetime;
    public Color Tint;

    public Explosion(Vector2 position, float radius, float lifetime, Color tint)
    {
        Position = position;
        Radius = radius;
        Lifetime = lifetime;
        MaxLifetime = lifetime;
        Tint = tint;
    }
}
