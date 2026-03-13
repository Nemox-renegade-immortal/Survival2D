using System.Drawing;
using System.Numerics;

namespace SlayInspiredPrototype;

public static class Phys
{
    public static Vector2 NormalizeSafe(Vector2 value)
    {
        if (value.LengthSquared() <= 0.0001f)
        {
            return Vector2.Zero;
        }

        return Vector2.Normalize(value);
    }

    public static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static Vector2 FromAngle(float radians) => new(MathF.Cos(radians), MathF.Sin(radians));

    public static bool CirclesOverlap(Vector2 a, float ar, Vector2 b, float br)
    {
        float rr = ar + br;
        return Vector2.DistanceSquared(a, b) <= rr * rr;
    }

    public static bool CircleIntersectsRect(Vector2 center, float radius, RectangleF rect)
    {
        float closestX = Clamp(center.X, rect.Left, rect.Right);
        float closestY = Clamp(center.Y, rect.Top, rect.Bottom);
        float dx = center.X - closestX;
        float dy = center.Y - closestY;
        return dx * dx + dy * dy <= radius * radius;
    }

    public static Vector2 ResolveCircleVsWorld(Vector2 start, Vector2 desired, float radius, TileMap map)
    {
        Vector2 result = start;

        Vector2 attemptX = new(desired.X, result.Y);
        if (!map.CollidesCircle(attemptX, radius))
        {
            result.X = attemptX.X;
        }

        Vector2 attemptY = new(result.X, desired.Y);
        if (!map.CollidesCircle(attemptY, radius))
        {
            result.Y = attemptY.Y;
        }

        result.X = Clamp(result.X, radius, map.PixelWidth - radius);
        result.Y = Clamp(result.Y, radius, map.PixelHeight - radius);
        return result;
    }

    public static RectangleF CircleBounds(Vector2 center, float radius)
    {
        return new RectangleF(center.X - radius, center.Y - radius, radius * 2f, radius * 2f);
    }
}
