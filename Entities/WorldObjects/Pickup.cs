using System.Numerics;

namespace SlayInspiredPrototype;

public enum PickupType
{
    Medkit,
    Ammo,
    Armor,
    Credits,
    Adrenaline
}

public sealed class Pickup
{
    public PickupType Type;
    public Vector2 Position;
    public float Radius = 14f;
    public bool Collected;
    public int Value;

    public Pickup(PickupType type, Vector2 position, int value)
    {
        Type = type;
        Position = position;
        Value = value;
    }
}
