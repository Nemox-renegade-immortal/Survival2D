using System.Numerics;

namespace SlayInspiredPrototype;

public sealed class ScrapPile
{
    public Vector2 Position;
    public float Radius = 16f;
    public int ScrapAmount;
    public bool Collected;

    public ScrapPile(Vector2 position, int scrapAmount)
    {
        Position = position;
        ScrapAmount = scrapAmount;
        Collected = false;
    }
}
