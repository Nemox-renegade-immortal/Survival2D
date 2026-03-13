using System;
using System.Numerics;

namespace SlayInspiredPrototype;

public sealed class LootCrate
{
    public Vector2 Position;
    public float Radius;
    public int MaxHealth;
    public int Health;
    public float HitFlash;
    public bool Destroyed;

    public LootCrate(Vector2 position, int health = 42, float radius = 18f)
    {
        Position = position;
        Radius = radius;
        MaxHealth = Math.Max(1, health);
        Health = MaxHealth;
        HitFlash = 0f;
        Destroyed = false;
    }

    public void Update(float dt)
    {
        HitFlash = MathF.Max(0f, HitFlash - dt);
    }

    public bool ApplyDamage(int damage)
    {
        if (Destroyed || damage <= 0)
        {
            return false;
        }

        Health = Math.Max(0, Health - damage);
        HitFlash = 0.16f;
        if (Health <= 0)
        {
            Destroyed = true;
            HitFlash = 0.28f;
            return true;
        }

        return false;
    }
}
