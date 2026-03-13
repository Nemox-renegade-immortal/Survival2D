using System.Numerics;

namespace SlayInspiredPrototype;

public abstract class Entity
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public int Health;
    public int MaxHealth;

    public bool IsAlive => Health > 0;
}
