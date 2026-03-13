using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace SlayInspiredPrototype;

public sealed class TileMap
{
    public const int TileSize = 48;
    public const int TileStoneWall = 1;
    public const int TileBarricade = 2;
    public const int TileTree = 3;
    public const int TileWoodWall = 4;
    public const int TileDoorClosed = 5;
    public const int TileDoorOpen = 6;
    public const int TileRock = 7;
    public const int TileBush = 8;

    public const int FloorGrass = 1;
    public const int FloorStone = 2;
    public const int FloorShore = 3;
    public const int FloorWater = 4;

    private readonly int[,] _tiles;
    private readonly int[,] _floor;
    private readonly int[,] _barricadeHealth;
    private readonly int[,] _pathMarks;
    private readonly int[,] _pathParentX;
    private readonly int[,] _pathParentY;
    private readonly List<Point> _doors = new List<Point>();
    private int _pathSearchId = 1;

    public int Width => _tiles.GetLength(0);
    public int Height => _tiles.GetLength(1);
    public int PixelWidth => Width * TileSize;
    public int PixelHeight => Height * TileSize;

    public TileMap(int width, int height, Random rng)
    {
        _tiles = new int[width, height];
        _floor = new int[width, height];
        _barricadeHealth = new int[width, height];
        _pathMarks = new int[width, height];
        _pathParentX = new int[width, height];
        _pathParentY = new int[width, height];
        Rebuild(rng);
    }

    private void BeginPathSearch()
    {
        _pathSearchId++;
        if (_pathSearchId == int.MaxValue)
        {
            Array.Clear(_pathMarks, 0, _pathMarks.Length);
            _pathSearchId = 1;
        }
    }

    public void Rebuild(Random rng)
    {
        _doors.Clear();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                bool border = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                _tiles[x, y] = border ? TileStoneWall : 0;
                _floor[x, y] = 0;
                _barricadeHealth[x, y] = 0;
            }
        }

        BuildIrregularBoundary(rng);
        PaintGroundBlobs(rng, FloorGrass, Math.Max(10, Width * Height / 180));
        PaintGroundBlobs(rng, FloorStone, Math.Max(7, Width * Height / 260));
        CarveMainPaths(rng);
        BuildRiver(rng);
        BuildWoodCabins(rng, Math.Max(4, Width * Height / 800));
        BuildStoneRidges(rng, Math.Max(5, Width * Height / 900));
        BuildTreeGroves(rng, Math.Max(8, Width * Height / 500));
        BuildBushPatches(rng, Math.Max(10, Width * Height / 560));
        BuildRockFields(rng, Math.Max(6, Width * Height / 700));
        ScatterFenceSegments(rng, Math.Max(6, Width * Height / 750));
        CarveMainPaths(rng);

        ClearSpawn(2, 2, 10, 10, true);
        ClearSpawn(Width - 11, Height - 11, Width - 3, Height - 3, true);
        ClearSpawn(Width / 2 - 4, Height / 2 - 4, Width / 2 + 4, Height / 2 + 4, true);
    }

    private void BuildIrregularBoundary(Random rng)
    {
        int topThickness = 2 + rng.Next(2);
        int bottomThickness = 2 + rng.Next(2);
        for (int x = 1; x < Width - 1; x++)
        {
            topThickness = Math.Clamp(topThickness + rng.Next(-1, 2), 1, 4);
            bottomThickness = Math.Clamp(bottomThickness + rng.Next(-1, 2), 1, 4);
            for (int y = 1; y <= topThickness; y++)
            {
                if (rng.NextDouble() < 0.32)
                {
                    _tiles[x, y] = TileTree;
                    _floor[x, y] = FloorGrass;
                }
                else
                {
                    _tiles[x, y] = TileStoneWall;
                }
            }

            for (int y = Height - 1 - bottomThickness; y < Height - 1; y++)
            {
                if (rng.NextDouble() < 0.32)
                {
                    _tiles[x, y] = TileTree;
                    _floor[x, y] = FloorGrass;
                }
                else
                {
                    _tiles[x, y] = TileStoneWall;
                }
            }
        }

        int leftThickness = 2 + rng.Next(2);
        int rightThickness = 2 + rng.Next(2);
        for (int y = 1; y < Height - 1; y++)
        {
            leftThickness = Math.Clamp(leftThickness + rng.Next(-1, 2), 1, 4);
            rightThickness = Math.Clamp(rightThickness + rng.Next(-1, 2), 1, 4);
            for (int x = 1; x <= leftThickness; x++)
            {
                if (rng.NextDouble() < 0.28)
                {
                    _tiles[x, y] = TileRock;
                    _floor[x, y] = FloorStone;
                }
                else
                {
                    _tiles[x, y] = TileStoneWall;
                }
            }

            for (int x = Width - 1 - rightThickness; x < Width - 1; x++)
            {
                if (rng.NextDouble() < 0.28)
                {
                    _tiles[x, y] = TileRock;
                    _floor[x, y] = FloorStone;
                }
                else
                {
                    _tiles[x, y] = TileStoneWall;
                }
            }
        }
    }

    private void PaintGroundBlobs(Random rng, int floorType, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int cx = rng.Next(3, Width - 3);
            int cy = rng.Next(3, Height - 3);
            int rx = rng.Next(2, 7);
            int ry = rng.Next(2, 7);
            PaintFloorEllipse(cx, cy, rx, ry, floorType, rng, 0.78f);
        }
    }

    private void PaintFloorEllipse(int cx, int cy, int rx, int ry, int floorType, Random rng, float density)
    {
        for (int x = cx - rx; x <= cx + rx; x++)
        {
            if (x <= 0 || x >= Width - 1)
            {
                continue;
            }

            for (int y = cy - ry; y <= cy + ry; y++)
            {
                if (y <= 0 || y >= Height - 1)
                {
                    continue;
                }

                float nx = (x - cx) / (float)Math.Max(1, rx);
                float ny = (y - cy) / (float)Math.Max(1, ry);
                float dist = nx * nx + ny * ny;
                if (dist > 1f)
                {
                    continue;
                }

                float chance = density - dist * 0.28f;
                if (rng.NextDouble() <= chance)
                {
                    _floor[x, y] = floorType;
                }
            }
        }
    }

    private void CarveMainPaths(Random rng)
    {
        CarveMeanderingPath(new Point(2, Height / 2 + rng.Next(-3, 4)), new Point(Width - 3, Height / 2 + rng.Next(-3, 4)), 2, FloorStone);
        CarveMeanderingPath(new Point(Width / 2 + rng.Next(-4, 5), 2), new Point(Width / 2 + rng.Next(-4, 5), Height - 3), 2, FloorStone);
        CarveMeanderingPath(new Point(4, 5 + rng.Next(Math.Max(1, Height - 10))), new Point(Width - 5, 5 + rng.Next(Math.Max(1, Height - 10))), 1, FloorGrass);
    }

    private void CarveMeanderingPath(Point start, Point end, int halfWidth, int floorType)
    {
        Vector2 pos = new Vector2(start.X, start.Y);
        Vector2 target = new Vector2(end.X, end.Y);
        int guard = Width * Height;
        while (Vector2.DistanceSquared(pos, target) > 4f && guard-- > 0)
        {
            Vector2 dir = Vector2.Normalize(target - pos);
            Vector2 side = new Vector2(-dir.Y, dir.X);
            float wobble = MathF.Sin((pos.X + pos.Y) * 0.24f) * 0.8f;
            pos += dir * 1.2f + side * wobble * 0.12f;
            int cx = Math.Clamp((int)MathF.Round(pos.X), 1, Width - 2);
            int cy = Math.Clamp((int)MathF.Round(pos.Y), 1, Height - 2);
            for (int x = cx - halfWidth; x <= cx + halfWidth; x++)
            {
                for (int y = cy - halfWidth; y <= cy + halfWidth; y++)
                {
                    if (x <= 0 || y <= 0 || x >= Width - 1 || y >= Height - 1)
                    {
                        continue;
                    }

                    if (IsRiverSurfaceFloor(_floor[x, y]))
                    {
                        continue;
                    }

                    _tiles[x, y] = 0;
                    _barricadeHealth[x, y] = 0;
                    _floor[x, y] = floorType;
                }
            }
        }
    }

    private void BuildRiver(Random rng)
    {
        bool vertical = rng.NextDouble() < 0.58;
        int span = vertical ? Height : Width;
        float axisLimit = vertical ? Width - 4f : Height - 4f;
        float axis = vertical
            ? rng.Next(Math.Max(4, Width / 4), Math.Max(5, Width - Width / 4))
            : rng.Next(Math.Max(4, Height / 4), Math.Max(5, Height - Height / 4));

        float drift = 0f;
        float sinPhase = (float)rng.NextDouble() * MathF.PI * 2f;
        int baseHalfWidth = Math.Clamp(Math.Min(Width, Height) / 16, 2, 4);
        int shoreWidth = baseHalfWidth + 1;

        for (int i = 1; i < span - 1; i++)
        {
            float sway = MathF.Sin(i * 0.18f + sinPhase) * (vertical ? Width : Height) * 0.03f;
            drift = Math.Clamp(drift + ((float)rng.NextDouble() - 0.5f) * 0.9f, -2.8f, 2.8f);
            axis = Math.Clamp(axis + drift * 0.18f + sway * 0.05f, 3f, axisLimit);
            int halfWidth = Math.Clamp(baseHalfWidth + (int)MathF.Round(MathF.Sin(i * 0.12f + sinPhase * 0.7f) * 1.1f), 2, baseHalfWidth + 2);

            int cx = vertical ? (int)MathF.Round(axis) : i;
            int cy = vertical ? i : (int)MathF.Round(axis);
            PaintRiverDisc(cx, cy, halfWidth, shoreWidth, rng);

            if (i > 6 && i < span - 7 && rng.NextDouble() < 0.055)
            {
                int ox = vertical ? rng.Next(-2, 3) : 0;
                int oy = vertical ? 0 : rng.Next(-2, 3);
                PaintRiverDisc(cx + ox, cy + oy, halfWidth + 1, shoreWidth + 1, rng);
            }
        }
    }

    private void PaintRiverDisc(int cx, int cy, int halfWidth, int shoreWidth, Random rng)
    {
        int radius = shoreWidth + 2;
        for (int x = cx - radius; x <= cx + radius; x++)
        {
            if (x <= 0 || x >= Width - 1)
            {
                continue;
            }

            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (y <= 0 || y >= Height - 1)
                {
                    continue;
                }

                float dx = x - cx;
                float dy = y - cy;
                float distSq = dx * dx + dy * dy;
                float noise = MathF.Sin((x + cx) * 0.55f + (y - cy) * 0.41f) * 0.35f;
                float waterRadius = halfWidth + noise;
                float shoreRadius = shoreWidth + 0.75f + noise * 0.45f;

                if (distSq <= waterRadius * waterRadius)
                {
                    _floor[x, y] = FloorWater;
                    if (_tiles[x, y] == TileBarricade)
                    {
                        _tiles[x, y] = 0;
                        _barricadeHealth[x, y] = 0;
                    }
                }
                else if (distSq <= shoreRadius * shoreRadius && _floor[x, y] != FloorWater)
                {
                    _floor[x, y] = FloorShore;
                }
            }
        }
    }

    private static bool IsRiverSurfaceFloor(int floorType)
    {
        return floorType == FloorWater || floorType == FloorShore;
    }

    private bool IsRiverSurface(int tx, int ty)
    {
        return tx >= 0 && ty >= 0 && tx < Width && ty < Height && IsRiverSurfaceFloor(_floor[tx, ty]);
    }


    private void BuildWoodCabins(Random rng, int count)
    {
        for (int i = 0; i < count; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 40 && !placed; attempt++)
            {
                int w = rng.Next(4, 8);
                int h = rng.Next(4, 7);
                int x = rng.Next(4, Math.Max(5, Width - w - 4));
                int y = rng.Next(4, Math.Max(5, Height - h - 4));
                if (!CanPlaceFeature(x - 1, y - 1, x + w, y + h, 0))
                {
                    continue;
                }

                for (int ix = x; ix < x + w; ix++)
                {
                    for (int iy = y; iy < y + h; iy++)
                    {
                        bool edge = ix == x || iy == y || ix == x + w - 1 || iy == y + h - 1;
                        _tiles[ix, iy] = edge ? TileWoodWall : 0;
                        _floor[ix, iy] = FloorStone;
                    }
                }

                int side = rng.Next(4);
                int doorX = x + w / 2;
                int doorY = y + h / 2;
                switch (side)
                {
                    case 0:
                        doorY = y;
                        doorX = x + 1 + rng.Next(Math.Max(1, w - 2));
                        break;
                    case 1:
                        doorY = y + h - 1;
                        doorX = x + 1 + rng.Next(Math.Max(1, w - 2));
                        break;
                    case 2:
                        doorX = x;
                        doorY = y + 1 + rng.Next(Math.Max(1, h - 2));
                        break;
                    default:
                        doorX = x + w - 1;
                        doorY = y + 1 + rng.Next(Math.Max(1, h - 2));
                        break;
                }

                _tiles[doorX, doorY] = TileDoorClosed;
                _doors.Add(new Point(doorX, doorY));

                PaintFloorEllipse(x + w / 2, y + h / 2, w, h, FloorGrass, rng, 0.24f);
                placed = true;
            }
        }
    }

    private void BuildStoneRidges(Random rng, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = rng.Next(5, Width - 5);
            float y = rng.Next(5, Height - 5);
            float angle = (float)rng.NextDouble() * MathF.PI * 2f;
            int steps = rng.Next(6, 16);
            for (int step = 0; step < steps; step++)
            {
                int cx = Math.Clamp((int)MathF.Round(x), 2, Width - 3);
                int cy = Math.Clamp((int)MathF.Round(y), 2, Height - 3);
                int radius = rng.Next(1, 3);
                for (int tx = cx - radius; tx <= cx + radius; tx++)
                {
                    for (int ty = cy - radius; ty <= cy + radius; ty++)
                    {
                        if (tx <= 1 || ty <= 1 || tx >= Width - 2 || ty >= Height - 2)
                        {
                            continue;
                        }

                        float dist = Vector2.DistanceSquared(new Vector2(tx, ty), new Vector2(cx, cy));
                        if (dist > radius * radius + 0.3f)
                        {
                            continue;
                        }

                        if (_tiles[tx, ty] != 0 || IsRiverSurface(tx, ty))
                        {
                            continue;
                        }

                        _tiles[tx, ty] = rng.NextDouble() < 0.18 ? TileRock : TileStoneWall;
                        _floor[tx, ty] = FloorStone;
                    }
                }

                angle += (float)(rng.NextDouble() - 0.5) * 0.9f;
                x += MathF.Cos(angle) * (1.2f + (float)rng.NextDouble() * 1.5f);
                y += MathF.Sin(angle) * (1.2f + (float)rng.NextDouble() * 1.5f);
            }
        }
    }

    private void BuildTreeGroves(Random rng, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int cx = rng.Next(4, Width - 4);
            int cy = rng.Next(4, Height - 4);
            int radius = rng.Next(2, 5);
            PaintFloorEllipse(cx, cy, radius + 2, radius + 2, FloorGrass, rng, 0.88f);
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                for (int y = cy - radius; y <= cy + radius; y++)
                {
                    if (x <= 1 || y <= 1 || x >= Width - 2 || y >= Height - 2)
                    {
                        continue;
                    }

                    float nx = (x - cx) / (float)Math.Max(1, radius);
                    float ny = (y - cy) / (float)Math.Max(1, radius);
                    float dist = nx * nx + ny * ny;
                    if (dist > 1f)
                    {
                        continue;
                    }

                    if (_tiles[x, y] == 0 && !IsRiverSurface(x, y) && rng.NextDouble() < 0.56 - dist * 0.22f)
                    {
                        _tiles[x, y] = TileTree;
                    }
                }
            }
        }
    }

    private void BuildBushPatches(Random rng, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int cx = rng.Next(4, Width - 4);
            int cy = rng.Next(4, Height - 4);
            int radius = rng.Next(1, 4);
            PaintFloorEllipse(cx, cy, radius + 2, radius + 2, FloorGrass, rng, 0.92f);
            for (int x = cx - radius - 1; x <= cx + radius + 1; x++)
            {
                for (int y = cy - radius - 1; y <= cy + radius + 1; y++)
                {
                    if (x <= 1 || y <= 1 || x >= Width - 2 || y >= Height - 2)
                    {
                        continue;
                    }

                    float nx = (x - cx) / (float)Math.Max(1, radius + 1);
                    float ny = (y - cy) / (float)Math.Max(1, radius + 1);
                    float dist = nx * nx + ny * ny;
                    if (dist > 1f)
                    {
                        continue;
                    }

                    if (_tiles[x, y] != 0 || IsRiverSurface(x, y))
                    {
                        continue;
                    }

                    float chance = 0.76f - dist * 0.28f + (float)rng.NextDouble() * 0.08f;
                    if (rng.NextDouble() < chance)
                    {
                        _tiles[x, y] = TileBush;
                    }
                }
            }
        }
    }

    private void BuildRockFields(Random rng, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int cx = rng.Next(4, Width - 4);
            int cy = rng.Next(4, Height - 4);
            int radius = rng.Next(2, 4);
            PaintFloorEllipse(cx, cy, radius + 1, radius + 1, FloorStone, rng, 0.82f);
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                for (int y = cy - radius; y <= cy + radius; y++)
                {
                    if (x <= 1 || y <= 1 || x >= Width - 2 || y >= Height - 2)
                    {
                        continue;
                    }

                    float dist = Vector2.DistanceSquared(new Vector2(x, y), new Vector2(cx, cy));
                    if (dist > radius * radius + 0.2f)
                    {
                        continue;
                    }

                    if (_tiles[x, y] == 0 && !IsRiverSurface(x, y) && rng.NextDouble() < 0.34)
                    {
                        _tiles[x, y] = TileRock;
                    }
                }
            }
        }
    }

    private void ScatterFenceSegments(Random rng, int count)
    {
        for (int i = 0; i < count; i++)
        {
            bool horizontal = rng.NextDouble() < 0.5;
            int length = rng.Next(3, 8);
            int x = rng.Next(3, Width - 3);
            int y = rng.Next(3, Height - 3);
            bool canPlace = true;
            for (int j = 0; j < length; j++)
            {
                int tx = x + (horizontal ? j : 0);
                int ty = y + (horizontal ? 0 : j);
                if (tx <= 1 || ty <= 1 || tx >= Width - 2 || ty >= Height - 2 || _tiles[tx, ty] != 0 || IsRiverSurface(tx, ty))
                {
                    canPlace = false;
                    break;
                }
            }

            if (!canPlace)
            {
                continue;
            }

            int gapIndex = length >= 5 && rng.NextDouble() < 0.4 ? rng.Next(1, length - 1) : -1;
            for (int j = 0; j < length; j++)
            {
                int tx = x + (horizontal ? j : 0);
                int ty = y + (horizontal ? 0 : j);
                _floor[tx, ty] = FloorGrass;
                if (j == gapIndex)
                {
                    _tiles[tx, ty] = TileDoorClosed;
                    _doors.Add(new Point(tx, ty));
                }
                else
                {
                    _tiles[tx, ty] = TileWoodWall;
                }
            }
        }
    }

    private bool CanPlaceFeature(int x1, int y1, int x2, int y2, int padding)
    {
        for (int x = x1 - padding; x <= x2 + padding; x++)
        {
            if (x <= 1 || x >= Width - 2)
            {
                return false;
            }

            for (int y = y1 - padding; y <= y2 + padding; y++)
            {
                if (y <= 1 || y >= Height - 2)
                {
                    return false;
                }

                if (_tiles[x, y] != 0 || IsRiverSurface(x, y))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void ClearSpawn(int x1, int y1, int x2, int y2, bool stoneFloor)
    {
        for (int x = x1; x <= x2; x++)
        {
            for (int y = y1; y <= y2; y++)
            {
                if (x >= 1 && y >= 1 && x < Width - 1 && y < Height - 1)
                {
                    _tiles[x, y] = 0;
                    _barricadeHealth[x, y] = 0;
                    _floor[x, y] = stoneFloor ? FloorStone : 0;
                }
            }
        }

        _doors.RemoveAll(p => p.X >= x1 && p.X <= x2 && p.Y >= y1 && p.Y <= y2);
    }

    public void UpdateDoors(IReadOnlyList<Vector2> actors)
    {
        float triggerDistanceSq = TileSize * TileSize * 1.6f;
        for (int i = 0; i < _doors.Count; i++)
        {
            Point door = _doors[i];
            if (door.X <= 0 || door.Y <= 0 || door.X >= Width - 1 || door.Y >= Height - 1)
            {
                continue;
            }

            Vector2 center = TileCenter(door.X, door.Y);
            bool open = false;
            for (int a = 0; a < actors.Count; a++)
            {
                if (Vector2.DistanceSquared(actors[a], center) <= triggerDistanceSq)
                {
                    open = true;
                    break;
                }
            }

            _tiles[door.X, door.Y] = open ? TileDoorOpen : TileDoorClosed;
        }
    }

    public bool IsWall(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height)
        {
            return true;
        }

        int value = _tiles[tx, ty];
        return value == TileStoneWall || value == TileTree || value == TileWoodWall || value == TileDoorClosed;
    }

    public bool IsBarricade(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height)
        {
            return false;
        }

        return _tiles[tx, ty] == TileBarricade;
    }

    public bool IsBlocking(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height)
        {
            return true;
        }

        int value = _tiles[tx, ty];
        return value != 0 && value != TileDoorOpen && value != TileRock && value != TileBush;
    }

    public bool IsBuildableSurface(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height)
        {
            return false;
        }

        return _floor[tx, ty] != FloorWater;
    }

    public float GetSurfaceSpeedMultiplier(Vector2 world)
    {
        (int tx, int ty) = ToTile(world);
        return _floor[tx, ty] switch
        {
            FloorWater => 0.72f,
            FloorShore => 0.9f,
            _ => 1f
        };
    }

    public bool IsConcealing(Vector2 world)
    {
        (int tx, int ty) = ToTile(world);
        return tx >= 0 && ty >= 0 && tx < Width && ty < Height && _tiles[tx, ty] == TileBush;
    }

    public float GetVisibilityMultiplier(Vector2 world)
    {
        return IsConcealing(world) ? 0.46f : 1f;
    }

    public bool IsBuildableCircle(Vector2 center, float radius)
    {
        if (CollidesCircle(center, radius))
        {
            return false;
        }

        int minX = Math.Max(0, (int)MathF.Floor((center.X - radius) / TileSize));
        int minY = Math.Max(0, (int)MathF.Floor((center.Y - radius) / TileSize));
        int maxX = Math.Min(Width - 1, (int)MathF.Floor((center.X + radius) / TileSize));
        int maxY = Math.Min(Height - 1, (int)MathF.Floor((center.Y + radius) / TileSize));

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (IsBuildableSurface(x, y))
                {
                    continue;
                }

                RectangleF rect = new RectangleF(x * TileSize, y * TileSize, TileSize, TileSize);
                if (Phys.CircleIntersectsRect(center, radius, rect))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public int GetBarricadeHealth(int tx, int ty)
    {
        return IsBarricade(tx, ty) ? _barricadeHealth[tx, ty] : 0;
    }

    public bool CollidesCircle(Vector2 center, float radius)
    {
        int minX = Math.Max(0, (int)MathF.Floor((center.X - radius) / TileSize));
        int minY = Math.Max(0, (int)MathF.Floor((center.Y - radius) / TileSize));
        int maxX = Math.Min(Width - 1, (int)MathF.Floor((center.X + radius) / TileSize));
        int maxY = Math.Min(Height - 1, (int)MathF.Floor((center.Y + radius) / TileSize));

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (!IsBlocking(x, y))
                {
                    continue;
                }

                RectangleF rect = new RectangleF(x * TileSize, y * TileSize, TileSize, TileSize);
                if (Phys.CircleIntersectsRect(center, radius, rect))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public Vector2 GetRandomFreePoint(Random rng)
    {
        for (int i = 0; i < 700; i++)
        {
            int x = rng.Next(1, Width - 1);
            int y = rng.Next(1, Height - 1);
            if (_tiles[x, y] == 0 && _floor[x, y] != FloorWater)
            {
                return TileCenter(x, y);
            }
        }

        return TileCenter(2, 2);
    }

    public Vector2 TileCenter(int tx, int ty)
    {
        return new Vector2(tx * TileSize + TileSize / 2f, ty * TileSize + TileSize / 2f);
    }

    public (int tx, int ty) ToTile(Vector2 world)
    {
        int tx = Math.Clamp((int)(world.X / TileSize), 0, Width - 1);
        int ty = Math.Clamp((int)(world.Y / TileSize), 0, Height - 1);
        return (tx, ty);
    }

    public bool TryPlaceBarricade(Vector2 world, out Vector2 placedCenter)
    {
        (int tx, int ty) = ToTile(world);
        placedCenter = TileCenter(tx, ty);

        if (tx <= 1 || ty <= 1 || tx >= Width - 2 || ty >= Height - 2)
        {
            return false;
        }

        if (_tiles[tx, ty] != 0 || !IsBuildableSurface(tx, ty))
        {
            return false;
        }

        _tiles[tx, ty] = TileBarricade;
        _barricadeHealth[tx, ty] = 120;
        return true;
    }

    private bool DamageBarricadeAt(int tx, int ty, int damage, out Vector2 hitCenter)
    {
        hitCenter = TileCenter(tx, ty);
        if (!IsBarricade(tx, ty))
        {
            return false;
        }

        _barricadeHealth[tx, ty] -= Math.Max(1, damage);
        if (_barricadeHealth[tx, ty] <= 0)
        {
            _tiles[tx, ty] = 0;
            _barricadeHealth[tx, ty] = 0;
        }

        return true;
    }

    public bool TryDamageBarricadeNear(Vector2 world, int damage)
    {
        (int tx, int ty) = ToTile(world);
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                int nx = tx + ox;
                int ny = ty + oy;
                if (DamageBarricadeAt(nx, ny, damage, out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryDamageBlockingBarricade(Vector2 from, Vector2 to, int damage, out Vector2 hitCenter)
    {
        hitCenter = from;
        Vector2 delta = to - from;
        float length = delta.Length();
        if (length <= 1f)
        {
            if (TryDamageBarricadeNear(from, damage))
            {
                return true;
            }

            hitCenter = from;
            return false;
        }

        int steps = Math.Max(1, (int)(length / Math.Max(14f, TileSize * 0.32f)));
        Vector2 step = delta / steps;
        Vector2 pos = from;

        for (int i = 0; i <= steps; i++)
        {
            (int tx, int ty) = ToTile(pos);
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oy = -1; oy <= 1; oy++)
                {
                    int nx = tx + ox;
                    int ny = ty + oy;
                    if (DamageBarricadeAt(nx, ny, damage, out hitCenter))
                    {
                        return true;
                    }
                }
            }

            pos += step;
        }

        if (TryDamageBarricadeNear(from, damage))
        {
            return true;
        }

        hitCenter = from;
        return false;
    }

    public bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float length = delta.Length();
        if (length <= 1f)
        {
            return true;
        }

        int steps = Math.Max(1, (int)(length / 16f));
        Vector2 step = delta / steps;
        Vector2 pos = from;
        for (int i = 0; i <= steps; i++)
        {
            (int tx, int ty) = ToTile(pos);
            if (IsBlocking(tx, ty))
            {
                return false;
            }

            pos += step;
        }

        return true;
    }

    public Vector2 GetNextStepToward(Vector2 from, Vector2 to)
    {
        if (HasLineOfSight(from, to))
        {
            return to;
        }

        (int startX, int startY) = ToTile(from);
        (int targetX, int targetY) = ToTile(to);
        if ((startX == targetX && startY == targetY) || IsBlocking(startX, startY))
        {
            return from;
        }

        BeginPathSearch();

        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        _pathMarks[startX, startY] = _pathSearchId;
        _pathParentX[startX, startY] = startX;
        _pathParentY[startX, startY] = startY;

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };
        bool found = false;

        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            if (x == targetX && y == targetY)
            {
                found = true;
                break;
            }

            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                {
                    continue;
                }

                if (_pathMarks[nx, ny] == _pathSearchId || IsBlocking(nx, ny))
                {
                    continue;
                }

                _pathMarks[nx, ny] = _pathSearchId;
                _pathParentX[nx, ny] = x;
                _pathParentY[nx, ny] = y;
                queue.Enqueue((nx, ny));
            }
        }

        if (!found)
        {
            int bestX = -1;
            int bestY = -1;
            int bestScore = int.MaxValue;

            for (int x = 1; x < Width - 1; x++)
            {
                for (int y = 1; y < Height - 1; y++)
                {
                    if (_pathMarks[x, y] != _pathSearchId)
                    {
                        continue;
                    }

                    int score = Math.Abs(x - targetX) + Math.Abs(y - targetY);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestX = x;
                        bestY = y;
                    }
                }
            }

            if (bestX < 0)
            {
                return from;
            }

            targetX = bestX;
            targetY = bestY;
        }

        int cx = targetX;
        int cy = targetY;

        while (!((cx == startX && cy == startY) || (_pathParentX[cx, cy] == startX && _pathParentY[cx, cy] == startY)))
        {
            int px = _pathParentX[cx, cy];
            int py = _pathParentY[cx, cy];
            cx = px;
            cy = py;
        }

        return TileCenter(cx, cy);
    }

    public IEnumerable<(RectangleF rect, int floorType)> GetGroundTiles(Rectangle view)
    {
        int minX = Math.Max(0, view.Left / TileSize - 1);
        int minY = Math.Max(0, view.Top / TileSize - 1);
        int maxX = Math.Min(Width - 1, view.Right / TileSize + 1);
        int maxY = Math.Min(Height - 1, view.Bottom / TileSize + 1);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (_floor[x, y] == 0)
                {
                    continue;
                }

                yield return (new RectangleF(x * TileSize, y * TileSize, TileSize, TileSize), _floor[x, y]);
            }
        }
    }

    public IEnumerable<(RectangleF rect, int value)> GetDrawTiles(Rectangle view)
    {
        int minX = Math.Max(0, view.Left / TileSize - 1);
        int minY = Math.Max(0, view.Top / TileSize - 1);
        int maxX = Math.Min(Width - 1, view.Right / TileSize + 1);
        int maxY = Math.Min(Height - 1, view.Bottom / TileSize + 1);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                int value = _tiles[x, y];
                if (value == 0)
                {
                    continue;
                }

                yield return (new RectangleF(x * TileSize, y * TileSize, TileSize, TileSize), value);
            }
        }
    }

    public int GetTileValue(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            return TileStoneWall;
        }

        return _tiles[x, y];
    }

    public int GetFloorValue(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            return 0;
        }

        return _floor[x, y];
    }

    public IEnumerable<(int tx, int ty, int health)> GetBarricades()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (_tiles[x, y] == TileBarricade && _barricadeHealth[x, y] > 0)
                {
                    yield return (x, y, _barricadeHealth[x, y]);
                }
            }
        }
    }

    public void SetBarricades(IEnumerable<(int tx, int ty, int health)> barricades)
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (_tiles[x, y] == TileBarricade)
                {
                    _tiles[x, y] = 0;
                    _barricadeHealth[x, y] = 0;
                }
            }
        }

        foreach ((int tx, int ty, int health) in barricades)
        {
            if (tx <= 1 || ty <= 1 || tx >= Width - 2 || ty >= Height - 2)
            {
                continue;
            }

            if (_tiles[tx, ty] == TileStoneWall || _tiles[tx, ty] == TileTree || _tiles[tx, ty] == TileWoodWall || _tiles[tx, ty] == TileDoorClosed || _tiles[tx, ty] == TileRock || _tiles[tx, ty] == TileBush)
            {
                continue;
            }

            _tiles[tx, ty] = TileBarricade;
            _barricadeHealth[tx, ty] = Math.Max(1, health);
        }
    }
}
