using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace SlayInspiredPrototype;

public sealed class TileMap
{
    public const int TileSize = 48;
    private readonly int[,] _tiles;
    private readonly int[,] _barricadeHealth;
    private readonly int[,] _pathMarks;
    private readonly int[,] _pathParentX;
    private readonly int[,] _pathParentY;
    private int _pathSearchId = 1;
    public int Width => _tiles.GetLength(0);
    public int Height => _tiles.GetLength(1);
    public int PixelWidth => Width * TileSize;
    public int PixelHeight => Height * TileSize;

    public TileMap(int width, int height, Random rng)
    {
        _tiles = new int[width, height];
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
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                bool border = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                _tiles[x, y] = border ? 1 : 0;
                _barricadeHealth[x, y] = 0;
            }
        }

        int layoutType = rng.Next(3);
        if (layoutType == 0)
        {
            BuildLaneLayout(rng);
        }
        else if (layoutType == 1)
        {
            BuildRoomLayout(rng);
        }
        else
        {
            BuildCrossLayout(rng);
        }

        ScatterBlocks(rng, 14);
        ClearSpawn(2, 2, 7, 7);
        ClearSpawn(Width - 8, Height - 8, Width - 3, Height - 3);
        ClearSpawn(Width / 2 - 2, Height / 2 - 2, Width / 2 + 2, Height / 2 + 2);
    }

    private void BuildLaneLayout(Random rng)
    {
        for (int x = 5; x < Width - 5; x += 6)
        {
            int gapTop = 2 + rng.Next(Math.Max(3, Height / 3));
            int gapBottom = Height - 4 - rng.Next(Math.Max(3, Height / 3));
            for (int y = 2; y < Height - 2; y++)
            {
                if (y >= gapTop && y <= gapTop + 2) continue;
                if (y >= gapBottom && y <= gapBottom + 2) continue;
                _tiles[x, y] = 1;
            }
        }
    }

    private void BuildRoomLayout(Random rng)
    {
        int midX = Width / 2;
        int midY = Height / 2;

        for (int x = 3; x < Width - 3; x++)
        {
            if ((x > 8 && x < 12) || (x > Width - 13 && x < Width - 9))
            {
                continue;
            }

            _tiles[x, midY] = 1;
        }

        for (int y = 3; y < Height - 3; y++)
        {
            if ((y > 5 && y < 9) || (y > Height - 10 && y < Height - 6))
            {
                continue;
            }

            _tiles[midX, y] = 1;
        }

        for (int i = 0; i < 4; i++)
        {
            int rx = 4 + i * 7;
            int ry = 3 + rng.Next(Height - 8);
            FillRect(rx, ry, rx + 1, ry + 2, 1);
        }
    }

    private void BuildCrossLayout(Random rng)
    {
        int centerX = Width / 2;
        int centerY = Height / 2;

        for (int x = 4; x < Width - 4; x++)
        {
            if (Math.Abs(x - centerX) < 3)
            {
                continue;
            }

            _tiles[x, centerY - 3] = 1;
            _tiles[x, centerY + 3] = 1;
        }

        for (int y = 4; y < Height - 4; y++)
        {
            if (Math.Abs(y - centerY) < 3)
            {
                continue;
            }

            _tiles[centerX - 4, y] = 1;
            _tiles[centerX + 4, y] = 1;
        }

        for (int i = 0; i < 6; i++)
        {
            int x = 3 + rng.Next(Width - 6);
            int y = 3 + rng.Next(Height - 6);
            FillRect(x, y, x + rng.Next(1, 3), y + rng.Next(1, 3), 1);
        }
    }

    private void ScatterBlocks(Random rng, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(2, Width - 3);
            int y = rng.Next(2, Height - 3);
            if (Math.Abs(x - Width / 2) < 3 && Math.Abs(y - Height / 2) < 3)
            {
                continue;
            }

            if (rng.NextDouble() < 0.65)
            {
                FillRect(x, y, x + 1, y + 1, 1);
            }
            else
            {
                _tiles[x, y] = 1;
            }
        }
    }

    private void FillRect(int x1, int y1, int x2, int y2, int value)
    {
        for (int x = Math.Max(1, x1); x <= Math.Min(Width - 2, x2); x++)
        {
            for (int y = Math.Max(1, y1); y <= Math.Min(Height - 2, y2); y++)
            {
                _tiles[x, y] = value;
            }
        }
    }

    private void ClearSpawn(int x1, int y1, int x2, int y2)
    {
        for (int x = x1; x <= x2; x++)
        {
            for (int y = y1; y <= y2; y++)
            {
                if (x >= 1 && y >= 1 && x < Width - 1 && y < Height - 1)
                {
                    _tiles[x, y] = 0;
                    _barricadeHealth[x, y] = 0;
                }
            }
        }
    }

    public bool IsWall(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height)
        {
            return true;
        }

        return _tiles[tx, ty] == 1;
    }

    public bool IsBarricade(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height)
        {
            return false;
        }

        return _tiles[tx, ty] == 2;
    }

    public bool IsBlocking(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height)
        {
            return true;
        }

        return _tiles[tx, ty] != 0;
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
        for (int i = 0; i < 500; i++)
        {
            int x = rng.Next(1, Width - 1);
            int y = rng.Next(1, Height - 1);
            if (_tiles[x, y] == 0)
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

        if (_tiles[tx, ty] != 0)
        {
            return false;
        }

        _tiles[tx, ty] = 2;
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
                yield return (new RectangleF(x * TileSize, y * TileSize, TileSize, TileSize), _tiles[x, y]);
            }
        }
    }

    public int GetTileValue(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            return 1;
        }

        return _tiles[x, y];
    }


    public IEnumerable<(int tx, int ty, int health)> GetBarricades()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (_tiles[x, y] == 2 && _barricadeHealth[x, y] > 0)
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
                if (_tiles[x, y] == 2)
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

            if (_tiles[tx, ty] == 1)
            {
                continue;
            }

            _tiles[tx, ty] = 2;
            _barricadeHealth[tx, ty] = Math.Max(1, health);
        }
    }
}
