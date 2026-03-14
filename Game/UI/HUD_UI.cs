using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using ImGuiNET;

namespace SlayInspiredPrototype;

public sealed partial class Game
{
    private readonly Dictionary<int, Vector2> _remotePlayerSmoothing = new();
    private int _inventoryUpgradeWeaponIndex;
    private int _inventoryDragRawIndex = -1;
    private int _inventoryDragHoverRawIndex = -1;
    private InventoryViewMode _inventoryViewMode = InventoryViewMode.Inventory;
    private int _craftRecipeIndex;
    private int _upgradeWorkbenchIndex;

    private enum InventoryViewMode
    {
        Inventory,
        Craft,
        Upgrades
    }

    private enum InventorySlotKind
    {
        Weapon,
        Scrap,
        Credits,
        Barricade,
        Grenade,
        Turret,
        Overdrive,
        Armor,
        Health,
        Empty
    }

    private readonly struct InventoryViewSlot
    {
        public readonly string Title;
        public readonly string Value;
        public readonly string Description;
        public readonly Color Accent;
        public readonly int WeaponIndex;
        public readonly bool Highlighted;
        public readonly InventorySlotKind Kind;

        public bool IsEmpty => Kind == InventorySlotKind.Empty;

        public InventoryViewSlot(string title, string value, string description, Color accent, InventorySlotKind kind, int weaponIndex = -1, bool highlighted = false)
        {
            Title = title;
            Value = value;
            Description = description;
            Accent = accent;
            WeaponIndex = weaponIndex;
            Highlighted = highlighted;
            Kind = kind;
        }
    }


    private readonly struct CraftRecipeView
    {
        public readonly string Title;
        public readonly string Cost;
        public readonly string Description;
        public readonly string ActionText;
        public readonly Color Accent;
        public readonly InventorySlotKind IconKind;

        public CraftRecipeView(string title, string cost, string description, string actionText, Color accent, InventorySlotKind iconKind)
        {
            Title = title;
            Cost = cost;
            Description = description;
            ActionText = actionText;
            Accent = accent;
            IconKind = iconKind;
        }
    }


    private readonly struct UpgradeWorkbenchView
    {
        public readonly string Title;
        public readonly string Subtitle;
        public readonly string Description;
        public readonly Color Accent;
        public readonly InventorySlotKind IconKind;

        public UpgradeWorkbenchView(string title, string subtitle, string description, Color accent, InventorySlotKind iconKind)
        {
            Title = title;
            Subtitle = subtitle;
            Description = description;
            Accent = accent;
            IconKind = iconKind;
        }
    }

    private bool UseNativeImGuiUi() => true;

    public void DrawImGui(Vector2 displaySize)
    {
        Vector2 screen = displaySize;
        ImDrawListPtr bg = ImGui.GetBackgroundDrawList();
        ImDrawListPtr fg = ImGui.GetForegroundDrawList();

        bool gameplayOverlay = _phase == GamePhase.Playing || _phase == GamePhase.Paused || _phase == GamePhase.GameOver;
        if (gameplayOverlay)
        {
            DrawImGuiWorld(bg, screen);
            DrawImGuiDayNightOverlay(bg, screen);
            DrawImGuiWorldGlow(bg, screen);
            if (_screenShaderEnabled)
            {
                DrawImGuiAtmosphereShader(bg, screen);
            }
            DrawImGuiCrosshair(fg);
        }
        else
        {
            DrawImGuiMenuBackdrop(bg, screen);
        }

        switch (_phase)
        {
            case GamePhase.Splash:
                DrawImGuiSplashScreen(fg, screen);
                break;
            case GamePhase.Title:
                DrawImGuiTitle(fg, screen);
                break;
            case GamePhase.Settings:
                DrawImGuiSettings(fg, screen);
                break;
            case GamePhase.MultiplayerMenu:
                DrawImGuiMultiplayerMenu(fg, screen);
                break;
            case GamePhase.LanBrowser:
                DrawImGuiLanBrowser(fg, screen);
                break;
            case GamePhase.HostSetup:
                DrawImGuiHostSetup(fg, screen);
                break;
            case GamePhase.JoinSetup:
                DrawImGuiJoinSetup(fg, screen);
                break;
            case GamePhase.Loading:
                DrawImGuiLoadingScreen(fg, screen);
                break;
            case GamePhase.Playing:
                DrawImGuiHud(fg, screen);
                break;
            case GamePhase.Paused:
                DrawImGuiHud(fg, screen);
                DrawImGuiTint(fg, screen, 150);
                DrawImGuiPause(fg, screen);
                break;
            case GamePhase.GameOver:
                DrawImGuiHud(fg, screen);
                DrawImGuiTint(fg, screen, 168);
                DrawImGuiGameOver(fg, screen);
                break;
        }

        if (gameplayOverlay)
        {
            DrawImGuiDamageOverlay(fg, screen);
        }

        if (_announcementTimer > 0f)
        {
            DrawImGuiAnnouncement(fg, screen);
        }
    }

    private void DrawImGuiWorld(ImDrawListPtr draw, Vector2 screen)
    {
        _worldDrawBounds = BuildWorldDrawBounds(new Size((int)screen.X, (int)screen.Y), 120f);
        DrawImGuiFloor(draw, screen);
        DrawImGuiTiles(draw, screen);
        DrawImGuiLootCrates(draw);
        DrawImGuiScrap(draw);
        DrawImGuiPickups(draw);
        DrawImGuiTurrets(draw);
        DrawImGuiBullets(draw);
        DrawImGuiGrenades(draw);
        DrawImGuiExplosions(draw);
        DrawImGuiZombies(draw);
        DrawImGuiPlayers(draw);
        DrawImGuiRemotePlayers(draw);
        DrawImGuiWorldParticles(draw);
    }

    private void DrawImGuiFloor(ImDrawListPtr draw, Vector2 screen)
    {
        float darkness = _dayNight.Darkness;
        uint top = ToU32(Mix(Color.FromArgb(26, 36, 48), Color.FromArgb(8, 12, 20), darkness));
        uint bottom = ToU32(Mix(Color.FromArgb(10, 14, 22), Color.FromArgb(2, 4, 8), darkness));
        draw.AddRectFilledMultiColor(Vector2.Zero, screen, top, top, bottom, bottom);

        Rectangle view = new Rectangle((int)Camera.X, (int)Camera.Y, (int)screen.X, (int)screen.Y);
        foreach ((RectangleF rect, int floorType) in Map.GetGroundTiles(view))
        {
            Vector2 min = WorldToScreen(new Vector2(rect.Left, rect.Top));
            Vector2 max = WorldToScreen(new Vector2(rect.Right, rect.Bottom));
            if (floorType == TileMap.FloorGrass)
            {
                Color grassTop = Mix(Color.FromArgb(42, 96, 60), Color.FromArgb(16, 34, 24), darkness);
                Color grassBottom = Mix(Color.FromArgb(24, 54, 34), Color.FromArgb(8, 18, 12), darkness);
                draw.AddRectFilledMultiColor(min, max, ToU32(Color.FromArgb(120, grassTop)), ToU32(Color.FromArgb(120, grassTop)), ToU32(Color.FromArgb(132, grassBottom)), ToU32(Color.FromArgb(132, grassBottom)));

                float sway = 2f + 1.5f * (0.5f + 0.5f * MathF.Sin(_backgroundPulse * 2f + rect.Left * 0.05f + rect.Top * 0.03f));
                uint grassLine = ToU32(Color.FromArgb((int)(26f + (1f - darkness) * 18f), 122, 182, 118));
                for (float x = min.X + 8f; x < max.X - 6f; x += 10f)
                {
                    draw.AddLine(new Vector2(x, max.Y - 7f), new Vector2(x + sway, max.Y - 16f), grassLine, 1f);
                }
            }
            else if (floorType == TileMap.FloorStone)
            {
                Color stoneTop = Mix(Color.FromArgb(78, 82, 92), Color.FromArgb(24, 28, 34), darkness);
                Color stoneBottom = Mix(Color.FromArgb(52, 56, 64), Color.FromArgb(12, 16, 20), darkness);
                draw.AddRectFilledMultiColor(min, max, ToU32(Color.FromArgb(110, stoneTop)), ToU32(Color.FromArgb(110, stoneTop)), ToU32(Color.FromArgb(128, stoneBottom)), ToU32(Color.FromArgb(128, stoneBottom)));
                draw.AddLine(new Vector2(min.X + 6f, min.Y + 9f), new Vector2(max.X - 8f, max.Y - 11f), ToU32(Color.FromArgb(38, 220, 226, 232)), 1f);
            }
            else if (floorType == TileMap.FloorShore)
            {
                Color shoreTop = Mix(Color.FromArgb(132, 126, 88), Color.FromArgb(52, 48, 34), darkness);
                Color shoreBottom = Mix(Color.FromArgb(84, 96, 64), Color.FromArgb(20, 24, 16), darkness);
                draw.AddRectFilledMultiColor(min, max, ToU32(Color.FromArgb(132, shoreTop)), ToU32(Color.FromArgb(132, shoreTop)), ToU32(Color.FromArgb(148, shoreBottom)), ToU32(Color.FromArgb(148, shoreBottom)));
                uint reed = ToU32(Color.FromArgb((int)(22f + (1f - darkness) * 16f), 214, 226, 172));
                for (float x = min.X + 9f; x < max.X - 7f; x += 12f)
                {
                    draw.AddLine(new Vector2(x, max.Y - 6f), new Vector2(x + 2f, max.Y - 15f), reed, 1f);
                }
            }
            else if (floorType == TileMap.FloorWater)
            {
                Color waterTop = Mix(Color.FromArgb(56, 150, 196), Color.FromArgb(16, 60, 86), darkness);
                Color waterBottom = Mix(Color.FromArgb(10, 74, 116), Color.FromArgb(4, 18, 34), darkness);
                draw.AddRectFilledMultiColor(min, max, ToU32(Color.FromArgb(160, waterTop)), ToU32(Color.FromArgb(160, waterTop)), ToU32(Color.FromArgb(188, waterBottom)), ToU32(Color.FromArgb(188, waterBottom)));
                uint ripple = ToU32(Color.FromArgb((int)(24f + (1f - darkness) * 22f), 228, 246, 255));
                float phase = _backgroundPulse * 2.2f + rect.Left * 0.05f + rect.Top * 0.03f;
                for (int i = 0; i < 3; i++)
                {
                    float y = min.Y + 8f + i * 10f + MathF.Sin(phase + i) * 2f;
                    draw.AddLine(new Vector2(min.X + 5f, y), new Vector2(max.X - 5f, y), ripple, 1f);
                }
            }
        }

        int grid = TileMap.TileSize;
        uint gridColor = ToU32(Mix(Color.FromArgb(22, 74, 98, 118), Color.FromArgb(14, 56, 80, 112), darkness));
        int startX = (int)(Camera.X / grid) * grid;
        int startY = (int)(Camera.Y / grid) * grid;

        for (int x = startX; x < Camera.X + screen.X + grid; x += grid)
        {
            float sx = x - Camera.X;
            draw.AddLine(new Vector2(sx, 0f), new Vector2(sx, screen.Y), gridColor, 1f);
        }

        for (int y = startY; y < Camera.Y + screen.Y + grid; y += grid)
        {
            float sy = y - Camera.Y;
            draw.AddLine(new Vector2(0f, sy), new Vector2(screen.X, sy), gridColor, 1f);
        }
    }

    private void DrawImGuiTiles(ImDrawListPtr draw, Vector2 screen)
    {
        Rectangle view = new Rectangle((int)Camera.X, (int)Camera.Y, (int)screen.X, (int)screen.Y);
        foreach ((RectangleF rect, int value) in Map.GetDrawTiles(view))
        {
            if (value == 0)
            {
                continue;
            }

            Vector2 min = WorldToScreen(new Vector2(rect.Left, rect.Top));
            Vector2 max = WorldToScreen(new Vector2(rect.Right, rect.Bottom));
            Vector2 center = (min + max) * 0.5f;
            int tx = (int)(rect.X / TileMap.TileSize);
            int ty = (int)(rect.Y / TileMap.TileSize);

            switch (value)
            {
                case TileMap.TileTree:
                    {
                        draw.AddRectFilled(new Vector2(center.X - 5f, max.Y - 17f), new Vector2(center.X + 5f, max.Y - 4f), ToU32(Color.FromArgb(220, 96, 66, 38)), 2f);
                        draw.AddCircleFilled(new Vector2(center.X, center.Y - 4f), 15f, ToU32(Color.FromArgb(214, 44, 118, 76)), 22);
                        draw.AddCircleFilled(new Vector2(center.X - 10f, center.Y + 1f), 10f, ToU32(Color.FromArgb(196, 38, 104, 70)), 18);
                        draw.AddCircleFilled(new Vector2(center.X + 10f, center.Y + 1f), 10f, ToU32(Color.FromArgb(196, 46, 132, 82)), 18);
                        break;
                    }
                case TileMap.TileBush:
                    {
                        draw.AddCircleFilled(center + new Vector2(-8f, 5f), 9f, ToU32(Color.FromArgb(214, 44, 108, 66)), 18);
                        draw.AddCircleFilled(center + new Vector2(0f, -1f), 12f, ToU32(Color.FromArgb(224, 74, 150, 92)), 20);
                        draw.AddCircleFilled(center + new Vector2(9f, 5f), 8f, ToU32(Color.FromArgb(208, 38, 102, 60)), 18);
                        draw.AddLine(new Vector2(center.X - 12f, max.Y - 8f), new Vector2(center.X - 7f, max.Y - 15f), ToU32(Color.FromArgb(120, 210, 238, 206)), 1f);
                        break;
                    }
                case TileMap.TileRock:
                    {
                        draw.AddCircleFilled(center + new Vector2(-7f, 4f), 8f, ToU32(Color.FromArgb(202, 86, 96, 106)), 18);
                        draw.AddCircleFilled(center + new Vector2(1f, -1f), 10f, ToU32(Color.FromArgb(218, 126, 134, 142)), 18);
                        draw.AddCircleFilled(center + new Vector2(10f, 4f), 7f, ToU32(Color.FromArgb(198, 90, 100, 110)), 18);
                        draw.AddLine(center + new Vector2(-1f, -6f), center + new Vector2(5f, 7f), ToU32(Color.FromArgb(140, 46, 52, 60)), 1.2f);
                        break;
                    }
                case TileMap.TileDoorClosed:
                case TileMap.TileDoorOpen:
                    {
                        bool vertical = IsDoorVerticalAt(tx, ty);
                        draw.AddRect(min + new Vector2(3f, 3f), max - new Vector2(3f, 3f), ToU32(Color.FromArgb(176, 86, 58, 34)), 2f, ImDrawFlags.None, 2f);
                        Vector2 gapMin = vertical ? new Vector2(center.X - 9f, min.Y + 5f) : new Vector2(min.X + 5f, center.Y - 9f);
                        Vector2 gapMax = vertical ? new Vector2(center.X + 9f, max.Y - 5f) : new Vector2(max.X - 5f, center.Y + 9f);
                        draw.AddRectFilled(gapMin, gapMax, ToU32(Color.FromArgb(value == TileMap.TileDoorOpen ? 24 : 42, 10, 10, 12)), 1f);

                        Vector2 panelMin;
                        Vector2 panelMax;
                        if (value == TileMap.TileDoorOpen)
                        {
                            panelMin = vertical ? new Vector2(max.X - 15f, min.Y + 7f) : new Vector2(min.X + 7f, min.Y + 6f);
                            panelMax = vertical ? new Vector2(max.X - 6f, max.Y - 7f) : new Vector2(max.X - 7f, min.Y + 15f);
                        }
                        else
                        {
                            panelMin = vertical ? new Vector2(center.X - 6f, min.Y + 6f) : new Vector2(min.X + 6f, center.Y - 6f);
                            panelMax = vertical ? new Vector2(center.X + 6f, max.Y - 6f) : new Vector2(max.X - 6f, center.Y + 6f);
                        }

                        draw.AddRectFilled(panelMin, panelMax, ToU32(Color.FromArgb(220, 168, 118, 72)), 1.5f);
                        draw.AddRect(panelMin, panelMax, ToU32(Color.FromArgb(188, 74, 46, 28)), 1.5f, ImDrawFlags.None, 1.2f);
                        if (vertical)
                        {
                            for (float x = panelMin.X + 3f; x < panelMax.X - 2f; x += 4f)
                            {
                                draw.AddLine(new Vector2(x, panelMin.Y + 2f), new Vector2(x, panelMax.Y - 2f), ToU32(Color.FromArgb(88, 250, 226, 192)), 1f);
                            }
                        }
                        else
                        {
                            for (float y = panelMin.Y + 3f; y < panelMax.Y - 2f; y += 4f)
                            {
                                draw.AddLine(new Vector2(panelMin.X + 2f, y), new Vector2(panelMax.X - 2f, y), ToU32(Color.FromArgb(88, 250, 226, 192)), 1f);
                            }
                        }
                        break;
                    }
                default:
                    {
                        Color baseColor = value switch
                        {
                            TileMap.TileStoneWall => Color.FromArgb(74, 84, 96),
                            TileMap.TileBarricade => Color.FromArgb(132, 96, 60),
                            TileMap.TileWoodWall => Color.FromArgb(142, 96, 62),
                            _ => Color.FromArgb(74, 84, 96)
                        };
                        Color topColor = Mix(baseColor, Color.White, 0.12f);
                        Color bottomColor = Mix(baseColor, Color.Black, 0.22f);
                        draw.AddRectFilledMultiColor(min, max, ToU32(topColor), ToU32(topColor), ToU32(bottomColor), ToU32(bottomColor));
                        draw.AddRect(min, max, ToU32(Color.FromArgb(160, Mix(baseColor, Color.Black, 0.35f))), 0f, ImDrawFlags.None, 1f);

                        if (value == TileMap.TileWoodWall)
                        {
                            uint plank = ToU32(Color.FromArgb(72, 246, 224, 194));
                            for (float x = min.X + 9f; x < max.X - 8f; x += 10f)
                            {
                                draw.AddLine(new Vector2(x, min.Y + 5f), new Vector2(x, max.Y - 5f), plank, 1f);
                            }
                        }
                        break;
                    }
            }
        }
    }

    private void DrawImGuiLootCrates(ImDrawListPtr draw)
    {
        IntPtr crateTexture = TextureCache.GetOrLoad("Assets/obstacles/crate.png");
        IntPtr damagedTexture = TextureCache.GetOrLoad("Assets/obstacles/crate_damaged.png");

        foreach (LootCrate crate in _lootCrates)
        {
            if (!IsVisible(crate.Position, crate.Radius + 20f))
            {
                continue;
            }

            Vector2 p = WorldToScreen(crate.Position);
            Vector2 min = p - new Vector2(crate.Radius, crate.Radius);
            Vector2 max = p + new Vector2(crate.Radius, crate.Radius);
            float flash = Math.Clamp(crate.HitFlash * 6f, 0f, 1f);
            bool damaged = crate.Health <= Math.Max(1, crate.MaxHealth / 2);
            IntPtr texture = damaged ? damagedTexture : crateTexture;

            draw.AddRectFilled(min + new Vector2(2f, 4f), max + new Vector2(2f, 4f), ToU32(Color.FromArgb(38, 0, 0, 0)), 4f);

            if (texture != IntPtr.Zero)
            {
                Color tint = flash > 0f
                    ? Mix(Color.White, Color.FromArgb(255, 255, 224, 192), flash * 0.55f)
                    : Color.White;
                draw.AddImage(texture, min, max, Vector2.Zero, Vector2.One, ToU32(tint));
                draw.AddRect(min, max, ToU32(Color.FromArgb(120, 255, 255, 255)), 4f, ImDrawFlags.None, 1f);
            }
            else
            {
                Color wood = Mix(damaged ? Color.FromArgb(118, 82, 58) : Color.FromArgb(152, 100, 58), Color.FromArgb(235, 196, 158), flash * 0.35f);
                Color trim = Mix(Color.FromArgb(84, 56, 30), Color.White, flash * 0.2f);
                draw.AddRectFilled(min, max, ToU32(wood), 4f);
                draw.AddRect(min, max, ToU32(Color.FromArgb(220, trim)), 4f, ImDrawFlags.None, 1.4f);
                draw.AddLine(new Vector2(min.X + 4f, p.Y), new Vector2(max.X - 4f, p.Y), ToU32(Color.FromArgb(180, 208, 170, 126)), 2f);
                draw.AddLine(new Vector2(p.X, min.Y + 4f), new Vector2(p.X, max.Y - 4f), ToU32(Color.FromArgb(180, 208, 170, 126)), 2f);
            }

            if (crate.Health < crate.MaxHealth || Vector2.DistanceSquared(Player.Position, crate.Position) <= 170f * 170f)
            {
                RectangleF hpRect = new RectangleF(min.X - 2f, min.Y - 8f, crate.Radius * 2f + 4f, 4f);
                DrawSimpleBar(draw, hpRect, crate.Health / (float)Math.Max(1, crate.MaxHealth), Color.FromArgb(220, 210, 152, 76));
            }
        }
    }

    private void DrawImGuiScrap(ImDrawListPtr draw)
    {
        foreach (ScrapPile scrap in _scrapPiles)
        {
            if (!IsVisible(scrap.Position, scrap.Radius + 16f))
            {
                continue;
            }

            Vector2 p = WorldToScreen(scrap.Position);
            draw.AddCircleFilled(p, scrap.Radius + 4f, ToU32(Color.FromArgb(64, 182, 192, 210)), 20);
            draw.AddCircleFilled(p, scrap.Radius, ToU32(Color.FromArgb(200, 142, 154, 166)), 20);
        }
    }

    private void DrawImGuiPickups(ImDrawListPtr draw)
    {
        foreach (Pickup pickup in _pickups)
        {
            if (!IsVisible(pickup.Position, pickup.Radius + 16f))
            {
                continue;
            }

            Color color = pickup.Type switch
            {
                PickupType.Medkit => Color.FromArgb(72, 190, 102),
                PickupType.Ammo => Color.FromArgb(214, 162, 62),
                PickupType.Armor => Color.FromArgb(84, 128, 216),
                PickupType.Credits => Color.FromArgb(212, 122, 60),
                PickupType.Adrenaline => Color.FromArgb(214, 148, 66),
                _ => Color.Magenta
            };

            Vector2 p = WorldToScreen(pickup.Position);
            draw.AddCircleFilled(p, pickup.Radius + 4f, ToU32(Color.FromArgb(40, color)), 18);
            draw.AddCircleFilled(p, pickup.Radius, ToU32(Color.FromArgb(220, color)), 18);
            draw.AddCircle(p, pickup.Radius, ToU32(Color.FromArgb(100, 255, 255, 255)), 18, 1f);
        }
    }

    private void DrawImGuiTurrets(ImDrawListPtr draw)
    {
        foreach (Turret turret in _turrets)
        {
            if (!IsVisible(turret.Position, turret.Radius + 24f))
            {
                continue;
            }

            Vector2 p = WorldToScreen(turret.Position);
            draw.AddCircleFilled(p + new Vector2(0f, 4f), turret.Radius * 0.82f, ToU32(Color.FromArgb(34, 0, 0, 0)), 24);
            draw.AddCircleFilled(p, turret.Radius, ToU32(Color.FromArgb(205, 38, 108, 124)), 24);
            draw.AddCircle(p, turret.Radius, ToU32(Color.FromArgb(160, 205, 230, 236)), 24, 1.2f);
            Vector2 tip = p + Phys.FromAngle(turret.AimAngle) * 18f;
            draw.AddLine(p, tip, ToU32(Color.FromArgb(230, 225, 232, 238)), 3.6f);

            float ratio = turret.MaxLifetime <= 0f ? 0f : Math.Clamp(turret.Lifetime / turret.MaxLifetime, 0f, 1f);
            RectangleF lifeRect = new RectangleF(p.X - 22f, p.Y - turret.Radius - 16f, 44f, 4f);
            DrawSimpleBar(draw, lifeRect, ratio, Color.FromArgb(220, 76, 182, 214));
            string label = $"{Math.Max(0f, turret.Lifetime):0.0}s";
            Vector2 labelSize = ImGui.CalcTextSize(label);
            draw.AddText(new Vector2(p.X - labelSize.X * 0.5f, lifeRect.Y - 14f), ToU32(Color.FromArgb(228, 214, 240, 248)), label);
        }
    }

    private void DrawImGuiBullets(ImDrawListPtr draw)
    {
        foreach (Bullet bullet in _bullets)
        {
            if (!IsVisible(bullet.Position, bullet.Radius + 24f))
            {
                continue;
            }

            Vector2 p = WorldToScreen(bullet.Position);
            Vector2 dir = Phys.NormalizeSafe(bullet.Velocity);
            float trailLength = Math.Clamp(bullet.Velocity.Length() * 0.02f, 10f, 24f);
            Vector2 tail = p - dir * trailLength;
            Color glowColor = bullet.FromPlayer ? Mix(bullet.Tint, Color.White, 0.18f) : Mix(bullet.Tint, Color.White, 0.04f);
            draw.AddLine(tail, p, ToU32(Color.FromArgb(110, glowColor)), Math.Max(1.8f, bullet.Radius * 1.2f));
            draw.AddLine(tail, p, ToU32(Color.FromArgb(45, Color.White)), Math.Max(1f, bullet.Radius * 0.55f));
            if (_dayNight.Darkness > 0.08f)
            {
                DrawGlow(draw, p, 10f + bullet.Radius * 3f, Color.FromArgb(42, glowColor));
            }
            draw.AddCircleFilled(p, bullet.Radius + 2f, ToU32(Color.FromArgb(60, bullet.Tint)), 14);
            draw.AddCircleFilled(p, bullet.Radius, ToU32(Color.FromArgb(235, Mix(bullet.Tint, Color.White, 0.22f))), 14);
        }

        foreach (Bullet bullet in _clientPredictedBullets)
        {
            if (!IsVisible(bullet.Position, bullet.Radius + 24f))
            {
                continue;
            }

            Vector2 p = WorldToScreen(bullet.Position);
            Vector2 dir = Phys.NormalizeSafe(bullet.Velocity);
            float trailLength = Math.Clamp(bullet.Velocity.Length() * 0.02f, 10f, 24f);
            Vector2 tail = p - dir * trailLength;
            Color glowColor = Mix(bullet.Tint, Color.White, 0.22f);
            draw.AddLine(tail, p, ToU32(Color.FromArgb(72, glowColor)), Math.Max(1.4f, bullet.Radius));
            draw.AddLine(tail, p, ToU32(Color.FromArgb(28, Color.White)), Math.Max(0.8f, bullet.Radius * 0.45f));
            draw.AddCircleFilled(p, bullet.Radius + 1.5f, ToU32(Color.FromArgb(34, bullet.Tint)), 12);
            draw.AddCircleFilled(p, bullet.Radius, ToU32(Color.FromArgb(150, Mix(bullet.Tint, Color.White, 0.24f))), 12);
        }
    }

    private void DrawImGuiGrenades(ImDrawListPtr draw)
    {
        foreach (Grenade grenade in _grenades)
        {
            if (!IsVisible(grenade.Position, grenade.Radius + 14f))
            {
                continue;
            }

            Vector2 p = WorldToScreen(grenade.Position);
            draw.AddCircleFilled(p + new Vector2(0f, 4f), grenade.Radius * 0.72f, ToU32(Color.FromArgb(30, 0, 0, 0)), 16);
            draw.AddCircleFilled(p, grenade.Radius, ToU32(Color.FromArgb(220, 188, 88, 44)), 16);
            draw.AddCircle(p, grenade.Radius, ToU32(Color.FromArgb(150, 255, 220, 196)), 16, 1f);
        }

        foreach (Grenade grenade in _clientPredictedGrenades)
        {
            if (!IsVisible(grenade.Position, grenade.Radius + 14f))
            {
                continue;
            }

            Vector2 p = WorldToScreen(grenade.Position);
            draw.AddCircleFilled(p + new Vector2(0f, 4f), grenade.Radius * 0.72f, ToU32(Color.FromArgb(18, 0, 0, 0)), 16);
            draw.AddCircleFilled(p, grenade.Radius, ToU32(Color.FromArgb(150, 214, 120, 66)), 16);
            draw.AddCircle(p, grenade.Radius, ToU32(Color.FromArgb(90, 255, 232, 208)), 16, 1f);
        }
    }

    private void DrawImGuiExplosions(ImDrawListPtr draw)
    {
        foreach (Explosion ex in _explosions)
        {
            if (!IsVisible(ex.Position, ex.Radius + 12f))
            {
                continue;
            }

            float alpha = ex.MaxLifetime <= 0f ? 0f : ex.Lifetime / ex.MaxLifetime;
            Vector2 p = WorldToScreen(ex.Position);
            draw.AddCircleFilled(p, ex.Radius, ToU32(Color.FromArgb((int)(alpha * 58f), ex.Tint)), 36);
            draw.AddCircle(p, ex.Radius, ToU32(Color.FromArgb((int)(alpha * 235), Mix(ex.Tint, Color.White, 0.08f))), 36, 2.8f);
        }
    }

    private void DrawImGuiZombies(ImDrawListPtr draw)
    {
        foreach (Zombie enemy in _zombies)
        {
            if (!IsVisible(enemy.Position, enemy.Radius + 22f))
            {
                continue;
            }

            Vector2 p = WorldToScreen(enemy.Position);
            draw.AddCircleFilled(p + new Vector2(0f, 4f), enemy.Radius * 0.8f, ToU32(Color.FromArgb(28, 0, 0, 0)), 24);
            draw.AddCircleFilled(p, enemy.Radius, ToU32(Color.FromArgb(220, enemy.Tint)), 24);
            draw.AddCircle(p, enemy.Radius, ToU32(Color.FromArgb(105, 255, 255, 255)), 24, 1.1f);
            DrawSimpleBar(draw, new RectangleF(p.X - enemy.Radius, p.Y - enemy.Radius - 10f, enemy.Radius * 2f, 6f), enemy.Health / (float)Math.Max(1, enemy.MaxHealth), Color.FromArgb(214, 68, 82));
        }
    }

    private void DrawImGuiBushOverlay(ImDrawListPtr draw, Vector2 center, float radius, Vector2 worldPosition)
    {
        if (!Map.IsConcealing(worldPosition))
        {
            return;
        }

        draw.AddCircleFilled(center + new Vector2(-8f, 4f), radius * 0.62f, ToU32(Color.FromArgb(86, 44, 114, 70)), 20);
        draw.AddCircleFilled(center + new Vector2(0f, -1f), radius * 0.78f, ToU32(Color.FromArgb(96, 84, 166, 104)), 22);
        draw.AddCircleFilled(center + new Vector2(9f, 5f), radius * 0.56f, ToU32(Color.FromArgb(88, 38, 102, 62)), 20);
    }

    private void DrawImGuiPlayers(ImDrawListPtr draw)
    {
        foreach (Player player in _players)
        {
            if (!IsVisible(player.Position, player.Radius + 30f))
            {
                continue;
            }

            float bob = MathF.Sin(_survivalTime * 9.5f + player.Position.X * 0.012f) * 1.6f * player.MoveBlend;
            float lift = player.PickupAnimation * 6f + bob;
            float recoil = player.ShootAnimation * 7f;
            float useGlow = player.UseAnimation * 18f;
            float reloadRing = player.ReloadAnimation * 12f;
            float radius = player.Radius + player.UseAnimation * 1.2f;
            Vector2 p = WorldToScreen(player.Position + new Vector2(0f, -lift));

            if (player.IsOverdriveActive)
            {
                draw.AddCircleFilled(p, radius + 9f + useGlow * 0.12f, ToU32(Color.FromArgb(34, 255, 160, 72)), 28);
            }

            draw.AddCircleFilled(p + new Vector2(0f, 5f), radius * 0.82f, ToU32(Color.FromArgb(34, 0, 0, 0)), 24);
            draw.AddCircleFilled(p, radius, ToU32(Color.FromArgb(GetEntityAlpha(230, player.Position), player.Accent)), 24);
            draw.AddCircle(p, radius, ToU32(Color.FromArgb(GetEntityAlpha(124, player.Position, 0.8f), 255, 255, 255)), 24, 1.2f);

            if (reloadRing > 0.04f)
            {
                draw.AddCircle(p, radius + 4f + reloadRing * 0.3f, ToU32(Color.FromArgb(120, 190, 228, 255)), 26, 1.4f);
            }

            Vector2 dir = Phys.FromAngle(player.AimAngle);
            Vector2 muzzleBase = p + dir * (radius * 0.44f - recoil * 0.12f);
            Vector2 tip = p + dir * (24f + recoil * 0.35f);
            bool localPlayer = _players.Count > 0 && ReferenceEquals(player, _players[0]);
            int selectedRawIndex = localPlayer ? GetSelectedHotbarRawIndex() : player.SelectedWeaponIndex;
            bool drawWeapon = !localPlayer || (selectedRawIndex >= 0 && selectedRawIndex < Player.ActiveWeaponSlots && player.SelectedWeaponIndex == selectedRawIndex);
            if (drawWeapon)
            {
                draw.AddLine(muzzleBase, tip, ToU32(player.IsOverdriveActive ? Color.FromArgb(255, 196, 92) : Color.FromArgb(236, 242, 252)), 4f);
            }
            else
            {
                DrawImGuiHeldItem(draw, selectedRawIndex, tip, dir);
            }

            if (player.PickupAnimation > 0.01f)
            {
                float pulse = player.PickupAnimation * 10f;
                draw.AddCircle(p + new Vector2(0f, -radius - 6f), 4f + pulse * 0.15f, ToU32(Color.FromArgb(110, 214, 226, 236)), 18, 1.3f);
            }

            draw.AddText(new Vector2(p.X - 18f, p.Y - radius - 24f), ToU32(Color.WhiteSmoke), player.Callsign);
            DrawImGuiBushOverlay(draw, p, radius + 2f, player.Position);
        }
    }

    private void DrawImGuiHeldItem(ImDrawListPtr draw, int rawIndex, Vector2 hand, Vector2 forward)
    {
        switch (rawIndex)
        {
            case 4:
                {
                    Vector2 min = new Vector2(hand.X - 7f, hand.Y - 6f);
                    Vector2 max = new Vector2(hand.X + 7f, hand.Y + 6f);
                    draw.AddRectFilled(min, max, ToU32(Color.FromArgb(220, 98, 106, 116)), 2f);
                    draw.AddRect(min, max, ToU32(Color.FromArgb(210, 226, 234, 242)), 2f, ImDrawFlags.None, 1.4f);
                    break;
                }
            case 5:
                draw.AddCircleFilled(hand, 6f, ToU32(Color.FromArgb(226, 192, 148, 86)), 18);
                draw.AddCircle(hand, 6f, ToU32(Color.FromArgb(220, 255, 238, 202)), 18, 1.4f);
                break;
            case 6:
                {
                    Vector2 min = new Vector2(hand.X - 8f + forward.X * 2f, hand.Y - 6f + forward.Y * 2f);
                    Vector2 max = new Vector2(min.X + 16f, min.Y + 12f);
                    draw.AddRectFilled(min, max, ToU32(Color.FromArgb(220, 116, 84, 54)), 2f);
                    draw.AddRect(min, max, ToU32(Color.FromArgb(210, 238, 220, 200)), 2f, ImDrawFlags.None, 1.6f);
                    break;
                }
            case 7:
                draw.AddCircleFilled(hand, 6f, ToU32(Color.FromArgb(226, 168, 104, 48)), 18);
                draw.AddCircle(hand, 6f, ToU32(Color.FromArgb(220, 250, 232, 190)), 18, 1.4f);
                draw.AddLine(hand + new Vector2(2f, -2f), hand + new Vector2(7f, -8f), ToU32(Color.FromArgb(220, 250, 232, 190)), 1.4f);
                break;
            case 8:
                draw.AddCircleFilled(hand, 7f, ToU32(Color.FromArgb(226, 76, 122, 142)), 18);
                draw.AddCircle(hand, 7f, ToU32(Color.FromArgb(220, 216, 246, 252)), 18, 1.4f);
                draw.AddLine(hand, hand + forward * 12f, ToU32(Color.FromArgb(220, 216, 246, 252)), 1.4f);
                break;
            case 9:
                {
                    Vector2 min = new Vector2(hand.X - 7f, hand.Y - 9f);
                    Vector2 max = new Vector2(hand.X + 7f, hand.Y + 9f);
                    draw.AddRectFilled(min, max, ToU32(Color.FromArgb(226, 138, 88, 44)), 2f);
                    draw.AddRect(min, max, ToU32(Color.FromArgb(220, 255, 228, 182)), 2f, ImDrawFlags.None, 1.4f);
                    draw.AddLine(new Vector2(hand.X - 3f, hand.Y - 12f), new Vector2(hand.X + 3f, hand.Y - 12f), ToU32(Color.FromArgb(220, 255, 228, 182)), 1.4f);
                    break;
                }
        }
    }

    private void DrawImGuiRemotePlayers(ImDrawListPtr draw)
    {
        foreach (RemotePlayerView remote in _remotePlayers)
        {
            if (!IsVisible(remote.Position, 44f))
            {
                continue;
            }

            float r = 16f + remote.UseAnimation * 1.1f;
            float bob = MathF.Sin(_survivalTime * 8.6f + remote.PlayerId * 0.7f) * 1.4f * remote.MoveBlend;
            float lift = remote.PickupAnimation * 5f + bob;
            float recoil = remote.ShootAnimation * 6f;
            Vector2 p = WorldToScreen(remote.Position + new Vector2(0f, -lift));

            if (remote.IsOverdriveActive)
            {
                draw.AddCircleFilled(p, r + 8f, ToU32(Color.FromArgb(30, 255, 160, 72)), 26);
            }

            draw.AddCircleFilled(p + new Vector2(0f, 4f), r * 0.8f, ToU32(Color.FromArgb(24, 0, 0, 0)), 24);
            draw.AddCircleFilled(p, r, ToU32(Color.FromArgb(GetEntityAlpha(214, remote.Position), remote.Accent)), 24);
            draw.AddCircle(p, r, ToU32(Color.FromArgb(GetEntityAlpha(104, remote.Position, 0.8f), 255, 255, 255)), 24, 1.1f);

            if (remote.ReloadAnimation > 0.04f)
            {
                draw.AddCircle(p, r + 4f, ToU32(Color.FromArgb(96, 192, 226, 255)), 24, 1.2f);
            }

            Vector2 dir = Phys.FromAngle(remote.AimAngle);
            Vector2 tip = p + dir * (20f + recoil * 0.28f);
            Vector2 muzzleBase = p + dir * (r * 0.38f - recoil * 0.08f);
            if (remote.SelectedHotbarRawIndex >= 0 && remote.SelectedHotbarRawIndex < Player.ActiveWeaponSlots && remote.WeaponSlot == remote.SelectedHotbarRawIndex)
            {
                draw.AddLine(muzzleBase, tip, ToU32(Color.FromArgb(204, 236, 242, 250)), 3.2f);
            }
            else
            {
                DrawImGuiHeldItem(draw, remote.SelectedHotbarRawIndex, tip, dir);
            }
            draw.AddText(new Vector2(p.X - 18f, p.Y - 30f), ToU32(Color.WhiteSmoke), remote.Callsign);
            DrawImGuiBushOverlay(draw, p, r + 2f, remote.Position);
        }
    }

    private void DrawImGuiCrosshair(ImDrawListPtr draw)
    {
        Vector2 p = new Vector2(_lastMouseScreen.X, _lastMouseScreen.Y);
        uint outer = ToU32(Color.FromArgb(180, 6, 8, 10));
        uint inner = ToU32(Mix(Player.Accent, Color.White, 0.12f));
        draw.AddCircle(p, 8f, outer, 24, 3f);
        draw.AddCircle(p, 8f, inner, 24, 1.5f);
        draw.AddCircleFilled(p, 2.2f, ToU32(Color.White), 12);
    }

    private void DrawImGuiDayNightOverlay(ImDrawListPtr draw, Vector2 screen)
    {
        if (_phase != GamePhase.Playing && _phase != GamePhase.Paused && _phase != GamePhase.GameOver)
        {
            return;
        }

        int alpha = (int)(16f + _dayNight.Darkness * 108f);
        draw.AddRectFilled(Vector2.Zero, screen, ToU32(Color.FromArgb(alpha, 6, 10, 18)));
    }

    private void DrawImGuiAtmosphereShader(ImDrawListPtr draw, Vector2 screen)
    {
        if (_phase != GamePhase.Playing && _phase != GamePhase.Paused && _phase != GamePhase.GameOver)
        {
            return;
        }

        float darkness = _dayNight.Darkness;
        float band = Math.Clamp(Math.Min(screen.X, screen.Y) * 0.18f, 56f, 180f);
        Color edgeA = Mix(Color.FromArgb(12, 0, 0, 0), Color.FromArgb(82, 0, 8, 16), darkness);
        Color edgeB = Mix(Color.FromArgb(6, 70, 128, 160), Color.FromArgb(104, 12, 18, 32), darkness);
        draw.AddRectFilledMultiColor(new Vector2(0f, 0f), new Vector2(screen.X, band), ToU32(edgeB), ToU32(edgeB), ToU32(edgeA), ToU32(edgeA));
        draw.AddRectFilledMultiColor(new Vector2(0f, screen.Y - band), new Vector2(screen.X, screen.Y), ToU32(edgeA), ToU32(edgeA), ToU32(edgeB), ToU32(edgeB));
        draw.AddRectFilledMultiColor(new Vector2(0f, 0f), new Vector2(band, screen.Y), ToU32(edgeB), ToU32(edgeA), ToU32(edgeA), ToU32(edgeB));
        draw.AddRectFilledMultiColor(new Vector2(screen.X - band, 0f), new Vector2(screen.X, screen.Y), ToU32(edgeA), ToU32(edgeB), ToU32(edgeB), ToU32(edgeA));

        if (darkness < 0.55f)
        {
            for (int i = 0; i < 3; i++)
            {
                float shift = (_backgroundPulse * 42f + i * 180f) % (screen.X + screen.Y * 0.6f);
                Vector2 a = new Vector2(-120f + shift, -20f);
                Vector2 b = new Vector2(120f + shift, screen.Y + 20f);
                draw.AddLine(a, b, ToU32(Color.FromArgb(10, 180, 228, 255)), 24f);
            }
        }
        else
        {
            DrawGlow(draw, WorldToScreen(Player.Position), 180f + darkness * 40f, Color.FromArgb((int)(10f + darkness * 18f), 86, 140, 190));
        }

        DrawImGuiAtmosphereShaderDetails(draw, screen);
    }

    private void DrawImGuiWorldGlow(ImDrawListPtr draw, Vector2 screen)
    {
        if ((_phase != GamePhase.Playing && _phase != GamePhase.Paused && _phase != GamePhase.GameOver) || _dayNight.Darkness <= 0.04f)
        {
            return;
        }

        Player player = Player;
        DrawGlow(draw, WorldToScreen(player.Position), 120f, Color.FromArgb((int)(18 + _dayNight.Darkness * 48f), player.Accent));

        foreach (Turret turret in _turrets)
        {
            if (IsVisible(turret.Position, 120f))
            {
                DrawGlow(draw, WorldToScreen(turret.Position), 84f, Color.FromArgb(30, 80, 210, 220));
            }
        }

        foreach (Pickup pickup in _pickups)
        {
            if (!IsVisible(pickup.Position, 90f))
            {
                continue;
            }

            Color color = pickup.Type switch
            {
                PickupType.Medkit => Color.FromArgb(42, 72, 190, 102),
                PickupType.Ammo => Color.FromArgb(38, 214, 162, 62),
                PickupType.Armor => Color.FromArgb(38, 84, 128, 216),
                PickupType.Credits => Color.FromArgb(34, 212, 122, 60),
                PickupType.Adrenaline => Color.FromArgb(36, 214, 148, 66),
                _ => Color.FromArgb(28, 255, 0, 255)
            };
            DrawGlow(draw, WorldToScreen(pickup.Position), 44f, color);
        }

        foreach (Explosion ex in _explosions)
        {
            if (IsVisible(ex.Position, ex.Radius + 24f))
            {
                float alpha = ex.MaxLifetime <= 0f ? 0f : ex.Lifetime / ex.MaxLifetime;
                DrawGlow(draw, WorldToScreen(ex.Position), ex.Radius * 1.15f, Color.FromArgb((int)(34f * alpha), ex.Tint));
            }
        }
    }

    private void DrawGlow(ImDrawListPtr draw, Vector2 center, float radius, Color color)
    {
        if (radius <= 2f || color.A <= 0)
        {
            return;
        }

        for (int i = 4; i >= 1; i--)
        {
            float t = i / 4f;
            float ringRadius = radius * (0.3f + t * 0.7f);
            int alpha = (int)(color.A * t * t * 0.7f);
            draw.AddCircleFilled(center, ringRadius, ToU32(Color.FromArgb(alpha, color)), 30);
        }
    }

    private void DrawImGuiDamageOverlay(ImDrawListPtr draw, Vector2 screen)
    {
        if (_bloodOverlay <= 0.001f)
        {
            return;
        }

        int edgeAlpha = (int)(24f + _bloodOverlay * 98f);
        uint edge = ToU32(Color.FromArgb(edgeAlpha, 120, 8, 8));
        uint edgeStrong = ToU32(Color.FromArgb((int)(edgeAlpha * 1.25f), 168, 12, 12));
        float band = Math.Clamp(Math.Min(screen.X, screen.Y) * 0.12f, 56f, 120f);
        draw.AddRectFilledMultiColor(new Vector2(0f, 0f), new Vector2(screen.X, band), edgeStrong, edgeStrong, edge, edge);
        draw.AddRectFilledMultiColor(new Vector2(0f, screen.Y - band), new Vector2(screen.X, screen.Y), edge, edge, edgeStrong, edgeStrong);
        draw.AddRectFilledMultiColor(new Vector2(0f, 0f), new Vector2(band, screen.Y), edgeStrong, edge, edge, edgeStrong);
        draw.AddRectFilledMultiColor(new Vector2(screen.X - band, 0f), new Vector2(screen.X, screen.Y), edge, edgeStrong, edgeStrong, edge);

        Vector2 hitDir = Phys.NormalizeSafe(_damageKick);
        if (hitDir.LengthSquared() > 0.0001f)
        {
            Vector2 blot = new Vector2(screen.X * 0.5f, screen.Y * 0.5f) - hitDir * Math.Min(screen.X, screen.Y) * 0.22f;
            DrawGlow(draw, blot, 90f + _damagePulse * 42f, Color.FromArgb((int)(46f + _bloodOverlay * 42f), 168, 10, 10));
        }
    }

    private void DrawImGuiTint(ImDrawListPtr draw, Vector2 screen, int alpha)
    {
        draw.AddRectFilled(Vector2.Zero, screen, ToU32(Color.FromArgb(alpha, 7, 10, 16)));
    }

    private void DrawImGuiTitle(ImDrawListPtr draw, Vector2 screen)
    {
        BeginModalWindow("Main Menu", screen, 520f, 460f);
        ImGui.Text("Survival 2D Alpha");
        ImGui.Separator();
        ImGui.TextWrapped("Testing create game...");
        ImGui.Spacing();

        if (ImGui.Button("Solo run", new Vector2(-1f, 0f))) ActivateTitleButton("solo");
        if (ImGui.Button("Multiplayer", new Vector2(-1f, 0f))) ActivateTitleButton("multi");
        if (ImGui.Button("Settings", new Vector2(-1f, 0f))) ActivateTitleButton("settings");
        if (ImGui.Button(_shuffleArenaOnStart ? "Arena shuffle: ON" : "Arena shuffle: OFF", new Vector2(-1f, 0f))) ActivateTitleButton("shuffle");
        if (ImGui.Button("Exit", new Vector2(-1f, 0f))) ActivateTitleButton("exit");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text($"Difficulty: {GetDifficultyName()}");
        ImGui.Text($"Slots: {_maxPlayers}");
        ImGui.Text(_network.StatusText);
        ImGui.End();
    }

    private void DrawImGuiMultiplayerMenu(ImDrawListPtr draw, Vector2 screen)
    {
        BeginModalWindow("Multiplayer", screen, 540f, 360f);
        ImGui.Text("Multiplayer");
        ImGui.Separator();
        ImGui.TextWrapped("Host a server, join by IP, or browse LAN servers on the local network.");
        ImGui.Spacing();

        if (ImGui.Button("Host server", new Vector2(-1f, 0f))) ActivateMultiplayerButton("host");
        if (ImGui.Button("Join by IP", new Vector2(-1f, 0f))) ActivateMultiplayerButton("join");
        if (ImGui.Button("LAN servers", new Vector2(-1f, 0f))) ActivateMultiplayerButton("lan");
        if (ImGui.Button("Back", new Vector2(-1f, 0f))) ActivateMultiplayerButton("back");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text($"Known LAN servers: {_network.GetLanServers().Count}");
        ImGui.Text($"Port: {_joinPort}");
        ImGui.Text($"Slots: {_maxPlayers}");
        ImGui.Text(_network.StatusText);
        ImGui.End();
    }

    private void DrawImGuiLanBrowser(ImDrawListPtr draw, Vector2 screen)
    {
        IReadOnlyList<LanServerInfo> servers = _network.GetLanServers();
        BeginModalWindow("LAN Servers", screen, 660f, 430f);
        ImGui.Text("LAN servers");
        ImGui.Separator();
        ImGui.TextWrapped("Discovered hosts on the local network. Click a server to join it.");
        ImGui.Spacing();

        if (servers.Count == 0)
        {
            ImGui.TextDisabled("No LAN servers found yet.");
        }
        else
        {
            for (int i = 0; i < servers.Count; i++)
            {
                LanServerInfo server = servers[i];
                string buttonText = $"{server.ServerName}  [{server.PlayerCount}/{server.MaxPlayers}]  {server.Address}:{server.Port}";
                if (ImGui.Button(buttonText, new Vector2(-1f, 0f)))
                {
                    ActivateLanButton($"lan:{server.Address}:{server.Port}");
                }
                ImGui.TextDisabled($"Map: {server.MapName} | Seed: {server.MapSeed}");
            }
        }

        ImGui.Spacing();
        if (ImGui.Button("Refresh LAN scan", new Vector2(-1f, 0f))) ActivateLanButton("refresh");
        if (ImGui.Button("Back", new Vector2(-1f, 0f))) ActivateLanButton("back");
        ImGui.End();
    }

    private void DrawImGuiSettings(ImDrawListPtr draw, Vector2 screen)
    {
        BeginModalWindow("Settings", screen, 780f, 470f);

        float navWidth = 220f;
        ImGui.BeginChild("settings_tabs", new Vector2(navWidth, 0f), ImGuiChildFlags.Borders);
        ImGui.Text("Tabs");
        ImGui.Separator();

        foreach (SettingsTab tab in SettingsTabOrder)
        {
            bool selected = _settingsTab == tab;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ToVec4(Color.FromArgb(88, 104, 132)));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ToVec4(Color.FromArgb(98, 118, 148)));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, ToVec4(Color.FromArgb(108, 130, 162)));
            }

            string label = GetSettingsTabName(tab);
            if (ImGui.Button(label, new Vector2(-1f, 42f)))
            {
                ActivateSettingsButton($"tab:{GetSettingsTabToken(tab)}");
            }

            if (selected)
            {
                ImGui.PopStyleColor(3);
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button("Back", new Vector2(-1f, 0f))) ActivateSettingsButton("back");
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("settings_main", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
        switch (_settingsTab)
        {
            case SettingsTab.Gameplay:
                DrawImGuiGameplaySettingsTab();
                break;
            case SettingsTab.Interface:
                DrawImGuiInterfaceSettingsTab();
                break;
            case SettingsTab.Graphic:
                DrawImGuiGraphicSettingsTab();
                break;
            case SettingsTab.Audio:
                DrawImGuiAudioSettingsTab();
                break;
            case SettingsTab.Network:
                DrawImGuiNetworkSettingsTab();
                break;
        }

        ImGui.EndChild();
        ImGui.End();
    }

    private void DrawImGuiGameplaySettingsTab()
    {
        ImGui.Text(GetSettingsPanelTitle());
        ImGui.TextDisabled("Type your nickname directly, then press Enter or click outside the field.");
        ImGui.Separator();

        ImGui.TextDisabled("Nickname");
        ImGui.SetNextItemWidth(-1f);
        bool submitted = ImGui.InputText("##settings_nickname", ref _nicknameInputBuffer, 24, ImGuiInputTextFlags.EnterReturnsTrue);
        CommitImGuiTextIfNeeded(submitted, () => CommitNicknameInput());

        if (ImGui.Button("Reset nickname", new Vector2(-1f, 0f))) ActivateSettingsButton("nicknamereset");
        if (ImGui.Button($"Difficulty: {GetDifficultyName()}", new Vector2(-1f, 0f))) ActivateSettingsButton("difficulty");
        if (ImGui.Button(_showHints ? "Hints: ON" : "Hints: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("hints");
        if (ImGui.Button(_shuffleArenaOnStart ? "Arena shuffle: ON" : "Arena shuffle: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("shuffle");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled($"Nickname: {_preferredCallsign}");
        ImGui.TextDisabled($"Current difficulty: {GetDifficultyName()}");
        ImGui.TextDisabled(_shuffleArenaOnStart ? "Arena reshuffles when a new run starts." : "Arena seed stays sticky until you toggle shuffle back on.");
        ImGui.TextDisabled("Nickname saves into Assets/Settings/game_settings.json.");
    }

    private void DrawImGuiInterfaceSettingsTab()
    {
        ImGui.Text(GetSettingsPanelTitle());
        ImGui.TextDisabled(GetSettingsPanelSubtitle());
        ImGui.Separator();

        if (ImGui.Button($"HUD scale: {GetUiScaleName()}", new Vector2(-1f, 0f))) ActivateSettingsButton("uiscale");
        if (ImGui.Button(_showFpsHud ? "FPS HUD: ON" : "FPS HUD: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("fpshud");
        if (ImGui.Button(_showPerfHud ? "Perf HUD: ON" : "Perf HUD: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("perfhud");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled($"HUD scale preset: {GetUiScaleName()}");
        ImGui.TextDisabled("HUD now auto-shrinks with smaller window sizes and respects your preset on top.");
        ImGui.TextDisabled(_showPerfHud ? "Perf HUD also forces FPS HUD on so the numbers stay honest." : "Enable overlays here when you want raw numbers.");
    }

    private void DrawImGuiGraphicSettingsTab()
    {
        ImGui.Text(GetSettingsPanelTitle());
        ImGui.TextDisabled(GetSettingsPanelSubtitle());
        ImGui.Separator();

        if (ImGui.Button(_menuArtworkEnabled ? "Menu backdrop: ARTWORK" : "Menu backdrop: BLACK", new Vector2(-1f, 0f))) ActivateSettingsButton("menuart");
        if (ImGui.Button(_splashLogoEnabled ? "Splash logo: ON" : "Splash logo: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("splashlogo");
        if (ImGui.Button(_screenShaderEnabled ? "Screen shader: ON" : "Screen shader: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("shader");
        if (ImGui.Button(_worldParticlesEnabled ? "World particles: ON" : "World particles: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("particles");
        if (ImGui.Button(_screenShakeEnabled ? "Screen shake: ON" : "Screen shake: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("screenshake");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(_menuArtworkEnabled ? "Menu artwork is enabled when a background file exists." : "Menu background is forced to pure black.");
        ImGui.TextDisabled(_splashLogoEnabled ? "Splash logo will render when a logo asset exists." : "Splash logo is hidden even if an asset exists.");
        ImGui.TextDisabled(_screenShaderEnabled ? "Atmosphere shader and vignette are enabled." : "Extra screen shader is disabled.");
        ImGui.TextDisabled(_worldParticlesEnabled ? "Impact particles are visible." : "World particles are hidden for a cleaner frame.");
        ImGui.TextDisabled(_screenShakeEnabled ? "Screen shake is active." : "Screen shake is disabled.");
        ImGui.TextDisabled($"Renderer: {_rendererLabel}");
    }

    private void DrawImGuiAudioSettingsTab()
    {
        ImGui.Text(GetSettingsPanelTitle());
        ImGui.TextDisabled("Volume is now slider-based, not that old caveman click-click bullshit.");
        ImGui.Separator();

        bool audioEnabled = _sound.Enabled;
        if (ImGui.Checkbox("Audio enabled", ref audioEnabled))
        {
            _sound.Enabled = audioEnabled;
            _sound.PlayUi(UiSound.Click);
            SavePersistentSettings();
            SetAnnouncement(_sound.Enabled ? "Audio enabled" : "Audio disabled", 1f);
        }

        ImGui.BeginDisabled(!_sound.Enabled);

        int master = _sound.MasterVolumePercent;
        if (ImGui.SliderInt("Master volume", ref master, 0, 100, "%d%%"))
        {
            SetMasterVolumePercent(master);
            SavePersistentSettings();
        }

        int ui = _sound.UiVolumePercent;
        if (ImGui.SliderInt("UI volume", ref ui, 0, 100, "%d%%"))
        {
            SetUiVolumePercent(ui);
            SavePersistentSettings();
        }

        int world = _sound.WorldVolumePercent;
        if (ImGui.SliderInt("World volume", ref world, 0, 100, "%d%%"))
        {
            SetWorldVolumePercent(world);
            SavePersistentSettings();
        }

        ImGui.EndDisabled();

        bool spatial = _sound.SpatialAudioEnabled;
        if (ImGui.Checkbox("3D audio", ref spatial))
        {
            _sound.SpatialAudioEnabled = spatial;
            _sound.PlayUi(UiSound.Click);
            SavePersistentSettings();
            SetAnnouncement(_sound.SpatialAudioEnabled ? "3D audio enabled" : "3D audio disabled", 1f);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(_sound.Enabled ? "Sound and UI click audio are live." : "Everything is muted from the menu level.");
        ImGui.TextDisabled($"Master {_sound.MasterVolumePercent}% · UI {_sound.UiVolumePercent}% · World {_sound.WorldVolumePercent}%");
        ImGui.TextDisabled(_sound.SpatialAudioEnabled ? "3D panning is enabled." : "All audio is forced to 2D center mix.");
    }

    private void DrawImGuiNetworkSettingsTab()
    {
        ImGui.Text(GetSettingsPanelTitle());
        ImGui.TextDisabled(GetSettingsPanelSubtitle());
        ImGui.Separator();

        if (ImGui.Button($"Join target: {_joinAddress}:{_joinPort} · Paste clipboard", new Vector2(-1f, 0f))) ActivateSettingsButton("joinpaste");
        if (ImGui.Button("Join target: reset localhost", new Vector2(-1f, 0f))) ActivateSettingsButton("joinreset");
        if (ImGui.Button(_showNetworkDebug ? "Net debug: ON" : "Net debug: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("netdebug");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(_showNetworkDebug ? "Packet and snapshot stats are visible in HUD." : "HUD stays clean until debug is enabled.");
        ImGui.TextDisabled($"Stored join: {_joinAddress}:{_joinPort} · slots {_maxPlayers}");
        ImGui.TextDisabled(_network.StatusText);
    }

    private void DrawImGuiHostSetup(ImDrawListPtr draw, Vector2 screen)
    {
        BeginModalWindow("Host Setup", screen, 540f, 330f);
        ImGui.Text("Host session");
        ImGui.Separator();
        ImGui.TextDisabled("Type the values directly. Press Enter or click outside the field to apply.");
        ImGui.Spacing();

        ImGui.TextDisabled("Port");
        ImGui.SetNextItemWidth(-1f);
        bool portSubmitted = ImGui.InputText("##host_port", ref _hostPortInputBuffer, 6, ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.EnterReturnsTrue);
        CommitImGuiTextIfNeeded(portSubmitted, () => CommitHostPortInput());

        ImGui.TextDisabled("Slots");
        ImGui.SetNextItemWidth(-1f);
        bool slotsSubmitted = ImGui.InputText("##host_slots", ref _hostSlotsInputBuffer, 3, ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.EnterReturnsTrue);
        CommitImGuiTextIfNeeded(slotsSubmitted, () => CommitHostSlotsInput());

        ImGui.Spacing();
        ImGui.TextDisabled($"Active host setup: tcp/{_joinPort} · slots {_maxPlayers}");
        ImGui.Spacing();

        if (ImGui.Button("Start host match", new Vector2(-1f, 0f))) ActivateHostButton("hoststart");
        if (ImGui.Button("Back", new Vector2(-1f, 0f))) ActivateHostButton("back");

        ImGui.End();
    }

    private void DrawImGuiJoinSetup(ImDrawListPtr draw, Vector2 screen)
    {
        BeginModalWindow("Join Setup", screen, 560f, 360f);
        ImGui.Text("Join session");
        ImGui.Separator();
        ImGui.TextDisabled("Type the server address and port directly. Press Enter or click outside the field to apply.");
        ImGui.Spacing();

        ImGui.TextDisabled("Address");
        ImGui.SetNextItemWidth(-1f);
        bool addressSubmitted = ImGui.InputText("##join_address", ref _joinAddressInputBuffer, 96, ImGuiInputTextFlags.EnterReturnsTrue);
        CommitImGuiTextIfNeeded(addressSubmitted, () => CommitJoinAddressInput());

        ImGui.TextDisabled("Port");
        ImGui.SetNextItemWidth(-1f);
        bool portSubmitted = ImGui.InputText("##join_port", ref _joinPortInputBuffer, 6, ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.EnterReturnsTrue);
        CommitImGuiTextIfNeeded(portSubmitted, () => CommitJoinPortInput());

        ImGui.Spacing();
        ImGui.TextDisabled($"Target: {_joinAddress}:{_joinPort}");
        ImGui.Spacing();

        if (ImGui.Button("Use localhost", new Vector2(-1f, 0f))) ActivateJoinButton("localhost");
        if (ImGui.Button("Connect and start", new Vector2(-1f, 0f))) ActivateJoinButton("joinstart");
        if (ImGui.Button("Back", new Vector2(-1f, 0f))) ActivateJoinButton("back");

        ImGui.End();
    }

    private void DrawImGuiPause(ImDrawListPtr draw, Vector2 screen)
    {

        BeginModalWindow("Paused", screen, 420f, 300f);
        ImGui.Text("Game paused");
        ImGui.TextDisabled(GetPauseMenuSubtitle());
        ImGui.Separator();
        if (ImGui.Button("Resume", new Vector2(-1f, 0f))) ActivatePausedButton("resume");
        if (ImGui.Button("Settings", new Vector2(-1f, 0f))) ActivatePausedButton("settings");
        if (ImGui.Button(GetPauseSessionActionLabel(), new Vector2(-1f, 0f))) ActivatePausedButton(GetPauseSessionActionId());
        if (ImGui.Button("Exit", new Vector2(-1f, 0f))) ActivatePausedButton("exit");
        ImGui.End();
    }

    private void DrawImGuiGameOver(ImDrawListPtr draw, Vector2 screen)
    {
        BeginModalWindow("Run Ended", screen, 420f, 280f);
        ImGui.Text($"Score: {_score}");
        ImGui.Text($"Best: {_bestScore}");
        ImGui.Text($"Wave: {_waveNumber}");
        ImGui.Text($"Time: {_survivalTime:0.0}s");
        ImGui.Separator();
        if (ImGui.Button("Retry", new Vector2(-1f, 0f))) ActivateGameOverButton("retry");
        if (ImGui.Button("Return to title", new Vector2(-1f, 0f))) ActivateGameOverButton("title");
        ImGui.End();
    }

    private void DrawImGuiAnnouncement(ImDrawListPtr draw, Vector2 screen)
    {
        float s = GetAdaptiveImGuiHudScale(screen);
        float width = Math.Clamp(420f * s, 300f, screen.X - 32f);
        float height = 58f * s;
        ImGui.SetNextWindowPos(new Vector2((screen.X - width) * 0.5f, 16f * s), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.92f);
        ImGui.Begin("##announcement", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        ImGui.SetWindowFontScale(Math.Clamp(0.9f * s, 0.82f, 1.10f));
        ImGui.TextColored(ToVec4(Player.Accent), "NOTICE");
        ImGui.SameLine();
        ImGui.TextWrapped(_announcement);
        ImGui.End();
    }

    private void DrawImGuiHud(ImDrawListPtr draw, Vector2 screen)
    {
        Player player = Player;
        List<InventoryViewSlot> inventorySlots = BuildInventorySlots(player);
        float s = GetAdaptiveImGuiHudScale(screen);
        float margin = 14f * s;
        float panelGap = 10f * s;
        float leftW = 292f * s;
        float rightW = 292f * s;
        float phaseW = Math.Clamp(340f * s, 250f, screen.X - 32f);
        float hotbarHeight = 134f * s;
        float chatReserved = GetChatReservedHeight(screen, s);

        BeginOverlayWindow("##phase_panel", new Vector2((screen.X - phaseW) * 0.5f, margin), new Vector2(phaseW, 60f * s), 0.88f, s);
        ImGui.TextColored(ToVec4(_dayNight.IsNight ? Color.FromArgb(255, 194, 118) : Color.FromArgb(104, 220, 255)), _dayNight.IsNight ? $"Night wave {_waveNumber}" : "Day phase");
        ImGui.SameLine();
        ImGui.TextDisabled(FormatPhaseTimer(_dayNight.RemainingTime));
        ImGui.Separator();
        ImGui.TextDisabled(_nightWaveStarted ? "Combat live · keep breathing" : "Scavenge · repair · reload");
        ImGui.SameLine();
        ImGui.TextDisabled($"Players {_network.ConnectedPlayers}");
        ImGui.End();

        BeginOverlayWindow("##status_panel", new Vector2(margin, margin), new Vector2(leftW, 166f * s), 0.88f, s);
        ImGui.TextColored(ToVec4(player.Accent), player.Callsign);
        ImGui.SameLine();
        ImGui.TextDisabled($"Score {_score}");
        ImGui.ProgressBar(player.Health / (float)Math.Max(1, player.MaxHealth), new Vector2(-1f, 0f), $"HP {player.Health}/{player.MaxHealth}");
        ImGui.ProgressBar(player.Armor / 100f, new Vector2(-1f, 0f), $"Armor {player.Armor}/100");
        ImGui.ProgressBar(player.Adrenaline / Math.Max(1f, player.MaxAdrenaline), new Vector2(-1f, 0f), player.IsOverdriveActive ? "Overdrive LIVE" : $"Overdrive {(int)player.Adrenaline}%");
        ImGui.Separator();
        ImGui.Text($"Scrap {player.Scrap}   Credits {player.Credits}");
        ImGui.Text($"Barricades {player.BarricadeKits}   Turrets {player.TurretCharges}/{player.MaxTurretCharges}");
        ImGui.Text($"Kills {player.Kills}   Crates {_lootCrates.Count}");
        ImGui.End();

        BeginOverlayWindow("##weapon_panel", new Vector2(screen.X - rightW - margin, margin), new Vector2(rightW, 178f * s), 0.88f, s);
        int selectedRawIndex = GetSelectedHotbarRawIndex();
        if (selectedRawIndex >= 0 && selectedRawIndex < Player.ActiveWeaponSlots && player.SelectedWeaponIndex == selectedRawIndex)
        {
            ImGui.TextColored(ToVec4(GetWeaponColor(player.Weapon)), player.Weapon.Name);
            ImGui.SameLine();
            ImGui.TextDisabled($"Lv {player.CurrentWeapon.Level} · Slot {GetSelectedHotbarDisplayIndex() + 1}");
            ImGui.ProgressBar(player.CurrentWeapon.LevelProgress, new Vector2(-1f, 0f), $"XP {player.CurrentWeapon.Experience}/{player.CurrentWeapon.NextLevelExperience}");
            ImGui.Text($"Ammo {player.CurrentWeapon.AmmoInClip}/{player.CurrentWeapon.AmmoReserve}");
            ImGui.Text($"Damage {player.GetShotDamage()}   Clip {player.GetCurrentClipSize()}");
            ImGui.Text($"Fire {player.GetEffectiveFireInterval():0.000}s   Reload {player.GetEffectiveReloadTime():0.00}s");
            ImGui.Text($"Spread {player.GetCurrentSpreadRadians():0.000}   Recoil {player.GetCurrentRecoil():0.00}");
            ImGui.Text($"Slow {player.GetZombieSlowSeconds():0.0}s   Knock {player.GetZombieKnockback():0}   Move {player.MoveSpeed:0}");
        }
        else
        {
            ImGui.TextColored(ToVec4(GetHotbarSlotAccent(player, selectedRawIndex)), GetHotbarSlotTitle(player, selectedRawIndex));
            ImGui.SameLine();
            ImGui.TextDisabled($"Slot {GetSelectedHotbarDisplayIndex() + 1}");
            ImGui.Separator();
            ImGui.Text($"State {GetHotbarSlotValue(player, selectedRawIndex)}");
            ImGui.Text(selectedRawIndex switch
            {
                4 => "Passive resource stack. Handy to keep visible while looting.",
                5 => "Credits are passive. Slot it if you want a quick visual tracker.",
                6 => "Place cover with E while this slot is selected.",
                7 => "Throw with E or RMB. Wheel-scroll keeps it armed in hand.",
                8 => "Deploy on a valid tile with E.",
                9 => player.IsOverdriveActive ? "Combat burst is live right now." : "Charge it up, then hit E to pop it.",
                _ => "Hands empty. Pick another slot or drag gear here."
            });
            ImGui.Text($"Scrap {player.Scrap}   Credits {player.Credits}");
            ImGui.Text($"Barricades {player.BarricadeKits}   Turrets {player.TurretCharges}/{player.MaxTurretCharges}");
            ImGui.Text($"Move {player.MoveSpeed:0}");
        }
        ImGui.Text($"Grenade {(player.GrenadeCooldownTimer <= 0f ? "READY" : $"{player.GrenadeCooldownTimer:0.0}s")}   Overdrive {(int)player.Adrenaline}%");
        if (_showNetworkDebug)
        {
            ImGui.Separator();
            ImGui.Text($"Ping {_network.PingMs} ms   Snapshot {_network.LatestSnapshotTick}");
            ImGui.Text($"Age {_network.SnapshotAgeMs} ms   Remote {_remotePlayers.Count}");
        }
        ImGui.End();

        float slotGap = 8f * s;
        float slotWidth = Math.Clamp((screen.X - 220f * s) / 9f - slotGap, 64f * s, 96f * s);
        float hotbarWidth = slotWidth * 9f + slotGap * 8f + 22f * s;
        BeginOverlayWindow("##hotbar_panel", new Vector2((screen.X - hotbarWidth) * 0.5f, screen.Y - hotbarHeight - margin), new Vector2(hotbarWidth, hotbarHeight), 0.94f, s);
        ImGui.TextDisabled("Hotbar");
        ImGui.SameLine();
        ImGui.TextColored(ToVec4(GetHotbarSlotAccent(player, selectedRawIndex)), $"Slot {GetSelectedHotbarDisplayIndex() + 1}");
        ImGui.SameLine();
        ImGui.TextDisabled("1-9 · wheel · click");
        ImGui.Dummy(new Vector2(0f, 4f * s));
        for (int displayIndex = 0; displayIndex < 9; displayIndex++)
        {
            if (displayIndex > 0)
            {
                ImGui.SameLine();
            }

            int rawIndex = _inventoryLayout[displayIndex];
            InventoryViewSlot slot = inventorySlots[Math.Clamp(rawIndex, 0, inventorySlots.Count - 1)];
            bool active = displayIndex == GetSelectedHotbarDisplayIndex();
            if (DrawHotbarTile($"hud_hotbar_{displayIndex}", slot, new Vector2(slotWidth, 78f * s), displayIndex, active))
            {
                ActivateHotbarSlot(player, displayIndex, new Size((int)screen.X, (int)screen.Y));
            }
        }

        ImGui.Dummy(new Vector2(0f, 10f * s));
        ImGui.PushTextWrapPos(0f);
        ImGui.TextColored(ToVec4(GetHotbarSlotAccent(player, selectedRawIndex)), GetHotbarSlotTitle(player, selectedRawIndex));
        ImGui.SameLine();
        ImGui.TextDisabled(GetHotbarSlotValue(player, selectedRawIndex));
        ImGui.SameLine();
        ImGui.TextDisabled(GetHotbarSlotHelpText(player, selectedRawIndex));
        ImGui.PopTextWrapPos();
        ImGui.End();

        if (_remotePlayers.Count > 0)
        {
            float remoteHeight = Math.Min(186f * s, 42f * s + _remotePlayers.Count * 34f * s);
            float squadBottom = screen.Y - hotbarHeight - margin - chatReserved - panelGap;
            BeginOverlayWindow("##squad_panel", new Vector2(margin, squadBottom - remoteHeight), new Vector2(280f * s, remoteHeight), 0.84f, s);
            ImGui.Text("Squad");
            ImGui.Separator();
            foreach (RemotePlayerView remote in _remotePlayers)
            {
                ImGui.TextColored(ToVec4(remote.Accent), remote.Callsign);
                ImGui.SameLine();
                ImGui.TextDisabled($"HP {remote.Health} · AR {remote.Armor} · {remote.WeaponName}");
            }
            ImGui.End();
        }

        if (_showFpsHud || _showPerfHud)
        {
            float[] graph = BuildFrameGraphOrdered();
            float perfHeight = 188f * s;
            float perfWidth = 268f * s;
            float perfBottom = screen.Y - hotbarHeight - margin - panelGap;
            BeginOverlayWindow("##perf_panel", new Vector2(screen.X - perfWidth - margin, perfBottom - perfHeight), new Vector2(perfWidth, perfHeight), 0.82f, s);
            ImGui.Text($"FPS {_renderFps:0}  UPS {_updateFps:0}");
            ImGui.TextDisabled(_rendererLabel);
            ImGui.Text($"Update {_updateMs:0.00} ms  Render {_renderMs:0.00} ms");
            ImGui.PlotLines("##framems", ref graph[0], graph.Length, 0, null, 0f, 40f, new Vector2(0f, 82f * s));
            ImGui.End();
        }

        DrawImGuiChatOverlay(screen, s, _phase == GamePhase.Playing || _phase == GamePhase.Paused);

        if (_inventoryOpen)
        {
            DrawImGuiInventory(draw, screen, player, 1f);
        }
    }

    private List<InventoryViewSlot> BuildInventorySlots(Player player)
    {
        List<InventoryViewSlot> slots = new List<InventoryViewSlot>(32);
        for (int i = 0; i < 4; i++)
        {
            WeaponState state = player.Arsenal[i];
            if (state.Unlocked && !state.IsEmpty)
            {
                string value = $"{state.AmmoInClip}/{state.AmmoReserve}";
                string desc = $"{state.Definition.Name} ready. Put it on the hotbar strip or keep it in the backpack. Drag with held left mouse.";
                slots.Add(new InventoryViewSlot(state.Definition.Name.ToUpper(), value, desc, GetWeaponColor(state.Definition), InventorySlotKind.Weapon, i, player.SelectedWeaponIndex == i));
            }
            else
            {
                slots.Add(new InventoryViewSlot("EMPTY", "-", "This weapon slot is empty. Craft another weapon and it will appear here automatically.", Color.FromArgb(74, 80, 88), InventorySlotKind.Empty));
            }
        }

        slots.Add(new InventoryViewSlot("SCRAP", player.Scrap.ToString(), "Core crafting material. Open the Craft menu with C to turn it into gear and support tools.", Color.FromArgb(118, 126, 138), InventorySlotKind.Scrap));
        slots.Add(new InventoryViewSlot("CREDITS", player.Credits.ToString(), "Spend credits on unlocks, upgrades and advanced crafting recipes.", Color.FromArgb(176, 130, 78), InventorySlotKind.Credits));
        slots.Add(player.BarricadeKits > 0
            ? new InventoryViewSlot("BARRICADE", player.BarricadeKits.ToString(), "Portable cover. Drop it in front of a lane to slow the dead down.", Color.FromArgb(150, 106, 70), InventorySlotKind.Barricade)
            : new InventoryViewSlot("EMPTY", "-", "No barricade kits yet. Craft one first and it will pop into the bag automatically.", Color.FromArgb(74, 80, 88), InventorySlotKind.Empty));
        slots.Add(new InventoryViewSlot("GRENADE", player.GrenadeCooldownTimer <= 0f ? "READY" : $"{player.GrenadeCooldownTimer:0.0}s", "Throwable explosive. Right mouse still works, but you can also bind or activate it from the hotbar.", Color.FromArgb(184, 128, 78), InventorySlotKind.Grenade));
        slots.Add(player.TurretCharges > 0
            ? new InventoryViewSlot("TURRET", $"{player.TurretCharges}/{player.MaxTurretCharges}", "Deployable auto-gun. Keep at least one charge if you want emergency cover fire.", Color.FromArgb(76, 144, 182), InventorySlotKind.Turret)
            : new InventoryViewSlot("EMPTY", "-", "No turret charge stored. Craft a turret battery and it will show up here.", Color.FromArgb(74, 80, 88), InventorySlotKind.Empty));
        slots.Add(player.Adrenaline > 0f || player.IsOverdriveActive
            ? new InventoryViewSlot("OVERDRIVE", player.IsOverdriveActive ? "LIVE" : $"{(int)player.Adrenaline}%", "Combat burst mode. Charge it up, then shove it into a hotbar slot you can hit fast.", Color.FromArgb(214, 134, 72), InventorySlotKind.Overdrive, -1, player.IsOverdriveActive)
            : new InventoryViewSlot("EMPTY", "-", "Overdrive is empty. Build a cell or earn charge in combat and it will appear here.", Color.FromArgb(74, 80, 88), InventorySlotKind.Empty));

        while (slots.Count < 32)
        {
            slots.Add(new InventoryViewSlot("EMPTY", "-", "Free backpack space. Drag something here if you want the hotbar cleaner.", Color.FromArgb(74, 80, 88), InventorySlotKind.Empty));
        }

        return slots;
    }

    private void DrawImGuiInventory(ImDrawListPtr draw, Vector2 screen, Player player, float s)
    {
        List<InventoryViewSlot> slots = BuildInventorySlots(player);
        List<CraftRecipeView> recipes = BuildCraftRecipes(player);
        _inventorySelectedIndex = Math.Clamp(_inventorySelectedIndex, 0, slots.Count - 1);
        _inventoryUpgradeWeaponIndex = Math.Clamp(_inventoryUpgradeWeaponIndex, 0, Math.Min(3, player.Arsenal.Count - 1));
        _craftRecipeIndex = Math.Clamp(_craftRecipeIndex, 0, recipes.Count - 1);
        _inventoryDragHoverRawIndex = _inventoryDragRawIndex >= 0 ? _inventoryDragRawIndex : -1;

        Vector2 size = new Vector2(Math.Min(1168f, screen.X - 48f), Math.Min(702f, screen.Y - 42f));
        Vector2 pos = new Vector2((screen.X - size.X) * 0.5f, (screen.Y - size.Y) * 0.5f);
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.972f);
        ImGui.Begin("Inventory", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings);

        Color headerAccent = _inventoryViewMode == InventoryViewMode.Inventory
            ? Color.FromArgb(96, 148, 220)
            : _inventoryViewMode == InventoryViewMode.Craft
                ? Color.FromArgb(188, 132, 76)
                : Color.FromArgb(90, 160, 208);
        string headerTitle = _inventoryViewMode == InventoryViewMode.Inventory ? "Inventory" : _inventoryViewMode == InventoryViewMode.Craft ? "Craft" : "Upgrades";
        string headerSubtitle = _inventoryViewMode == InventoryViewMode.Inventory
            ? "Clean layout, fast drag, clearer item states."
            : _inventoryViewMode == InventoryViewMode.Craft
                ? "Build weapons, kits and batteries."
                : "Buff support gear, weapons and survival stats.";

        DrawInventorySectionHeader(headerTitle, headerSubtitle, headerAccent);
        ImGui.Dummy(new Vector2(0f, 6f));
        DrawInventorySummaryPill($"Quick {CountFilledDisplaySlots(slots, 0, 8)}/9", Color.FromArgb(96, 148, 220));
        ImGui.SameLine();
        DrawInventorySummaryPill($"Bag {CountFilledDisplaySlots(slots, 9, 31)}/23", Color.FromArgb(104, 112, 124));
        ImGui.SameLine();
        DrawInventorySummaryPill($"Weapons {CountWeaponSlots(player)}", Color.FromArgb(118, 96, 176));
        ImGui.SameLine();
        DrawInventorySummaryPill($"Ready {CountReadySupportSlots(player)}", Color.FromArgb(204, 140, 84));
        ImGui.Separator();

        float navWidth = 148f;
        ImGui.BeginChild("inv_nav", new Vector2(navWidth, 0f), ImGuiChildFlags.Borders);
        DrawInventorySectionHeader("Sections", "Pick a page", Color.FromArgb(126, 136, 148));
        ImGui.Dummy(new Vector2(0f, 8f));
        if (DrawInventoryModeButton("mode_inventory", "Inventory", InventorySlotKind.Weapon, Color.FromArgb(86, 128, 200), _inventoryViewMode == InventoryViewMode.Inventory))
        {
            _inventoryViewMode = InventoryViewMode.Inventory;
        }

        ImGui.Dummy(new Vector2(0f, 6f));

        if (DrawInventoryModeButton("mode_craft", "Craft", InventorySlotKind.Scrap, Color.FromArgb(164, 112, 72), _inventoryViewMode == InventoryViewMode.Craft))
        {
            _inventoryViewMode = InventoryViewMode.Craft;
        }

        ImGui.Dummy(new Vector2(0f, 6f));
        if (DrawInventoryModeButton("mode_upgrades", "Upgrades", InventorySlotKind.Turret, Color.FromArgb(90, 144, 196), _inventoryViewMode == InventoryViewMode.Upgrades))
        {
            _inventoryViewMode = InventoryViewMode.Upgrades;
        }

        ImGui.Dummy(new Vector2(0f, 12f));
        ImGui.Separator();
        DrawInventorySectionHeader("Resources", "Live values", Color.FromArgb(126, 136, 148));
        ImGui.Text($"Scrap {player.Scrap}");
        ImGui.Text($"Credits {player.Credits}");
        ImGui.Text($"Kits {player.BarricadeKits}");
        ImGui.Text($"Turrets {player.TurretCharges}/{player.MaxTurretCharges}");
        ImGui.Text($"Overdrive {(int)player.Adrenaline}%");
        ImGui.Dummy(new Vector2(0f, 10f));
        ImGui.Separator();
        DrawInventorySectionHeader("Keys", "Fast access", Color.FromArgb(126, 136, 148));
        ImGui.TextDisabled("Tab = bag");
        ImGui.TextDisabled("C = craft");
        ImGui.TextDisabled("1-9 = hotbar");
        ImGui.TextDisabled("E = use item");
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("inv_main", new Vector2(0f, 0f), ImGuiChildFlags.None);
        if (_inventoryViewMode == InventoryViewMode.Inventory)
        {
            DrawInventoryLoadoutPanel(screen, player, slots);
        }
        else if (_inventoryViewMode == InventoryViewMode.Craft)
        {
            DrawCraftMenuPanel(player, recipes);
        }
        else
        {
            DrawUpgradeMenuPanel(player);
        }
        ImGui.EndChild();
        ImGui.End();

        if (_inventoryViewMode == InventoryViewMode.Inventory && _inventoryDragRawIndex >= 0)
        {
            int dragDisplayIndex = Math.Clamp(_inventoryDragRawIndex, 0, _inventoryLayout.Length - 1);
            int dragRawIndex = _inventoryLayout[dragDisplayIndex];
            InventoryViewSlot dragSlot = slots[Math.Clamp(dragRawIndex, 0, slots.Count - 1)];
            DrawInventoryDragGhost(ImGui.GetForegroundDrawList(), dragSlot, ImGui.GetMousePos() + new Vector2(16f, 18f));
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (_inventoryDragHoverRawIndex >= 0 && _inventoryDragHoverRawIndex != _inventoryDragRawIndex)
                {
                    (_inventoryLayout[_inventoryDragRawIndex], _inventoryLayout[_inventoryDragHoverRawIndex]) = (_inventoryLayout[_inventoryDragHoverRawIndex], _inventoryLayout[_inventoryDragRawIndex]);
                    _inventorySelectedIndex = _inventoryLayout[_inventoryDragHoverRawIndex];
                }

                _inventoryDragRawIndex = -1;
                _inventoryDragHoverRawIndex = -1;
            }
        }
    }

    private void DrawInventoryLoadoutPanel(Vector2 screen, Player player, List<InventoryViewSlot> slots)
    {
        float gridWidth = Math.Min(768f, ImGui.GetContentRegionAvail().X * 0.66f);
        ImGui.BeginChild("inv_grid", new Vector2(gridWidth, 0f), ImGuiChildFlags.Borders);
        DrawInventorySectionHeader("Quick access", "Top row mirrors the hotbar exactly.", Color.FromArgb(96, 148, 220));
        ImGui.Dummy(new Vector2(0f, 8f));

        float quickSpacing = 6f;
        float quickWidth = MathF.Floor((ImGui.GetContentRegionAvail().X - quickSpacing * 8f) / 9f);
        float quickHeight = 74f;
        for (int displayIndex = 0; displayIndex < 9; displayIndex++)
        {
            int rawIndex = _inventoryLayout[displayIndex];
            InventoryViewSlot slot = slots[Math.Clamp(rawIndex, 0, slots.Count - 1)];
            bool selected = rawIndex == _inventorySelectedIndex;
            bool dragging = displayIndex == _inventoryDragRawIndex;
            bool dropTarget = _inventoryDragRawIndex >= 0 && displayIndex == _inventoryDragHoverRawIndex && displayIndex != _inventoryDragRawIndex;
            if (displayIndex > 0)
            {
                ImGui.SameLine();
            }

            bool clicked = DrawInventoryTile($"inv_quick_{displayIndex}", slot, new Vector2(quickWidth, quickHeight), (displayIndex + 1).ToString(), selected, dragging, true, dropTarget);
            HandleInventorySlotInput(displayIndex, rawIndex, slot, clicked);
        }

        ImGui.Dummy(new Vector2(0f, 12f));
        ImGui.Separator();
        DrawInventorySectionHeader("Backpack", "Everything else lives here. Drag it up when you need it.", Color.FromArgb(116, 124, 134));
        ImGui.Dummy(new Vector2(0f, 8f));

        const int backpackColumns = 6;
        float packSpacing = 8f;
        float packWidth = MathF.Floor((ImGui.GetContentRegionAvail().X - packSpacing * (backpackColumns - 1)) / backpackColumns);
        float packHeight = 82f;
        for (int displayIndex = 9; displayIndex < 32; displayIndex++)
        {
            int rawIndex = _inventoryLayout[displayIndex];
            InventoryViewSlot slot = slots[Math.Clamp(rawIndex, 0, slots.Count - 1)];
            bool selected = rawIndex == _inventorySelectedIndex;
            bool dragging = displayIndex == _inventoryDragRawIndex;
            bool dropTarget = _inventoryDragRawIndex >= 0 && displayIndex == _inventoryDragHoverRawIndex && displayIndex != _inventoryDragRawIndex;
            int localIndex = displayIndex - 9;
            if (localIndex % backpackColumns != 0)
            {
                ImGui.SameLine();
            }

            bool clicked = DrawInventoryTile($"inv_pack_{displayIndex}", slot, new Vector2(packWidth, packHeight), $"{displayIndex + 1:00}", selected, dragging, false, dropTarget);
            HandleInventorySlotInput(displayIndex, rawIndex, slot, clicked);
        }

        ImGui.Dummy(new Vector2(0f, 10f));
        if (_inventoryDragRawIndex >= 0)
        {
            int fromDisplay = _inventoryDragRawIndex + 1;
            string hoverText = _inventoryDragHoverRawIndex >= 0 ? $" -> {_inventoryDragHoverRawIndex + 1}" : string.Empty;
            ImGui.TextDisabled($"Dragging slot {fromDisplay}{hoverText}");
            ImGui.TextDisabled("Release over another tile to swap it. Empty tiles work too.");
        }
        else
        {
            ImGui.TextDisabled("Hold LMB on any non-empty tile, drag, then release to swap places.");
        }
        ImGui.EndChild();

        ImGui.SameLine();
        DrawInventoryDetailsPanel(screen, player, slots);
    }

    private void DrawInventoryDetailsPanel(Vector2 screen, Player player, List<InventoryViewSlot> slots)
    {
        ImGui.BeginChild("inv_detail", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
        int selectedRawIndex = Math.Clamp(_inventorySelectedIndex, 0, slots.Count - 1);
        InventoryViewSlot selectedSlot = slots[selectedRawIndex];
        int displaySlot = FindInventoryDisplayIndex(selectedRawIndex);
        string locationLabel = GetInventoryDisplayLocationLabel(displaySlot);

        DrawInventoryDetailHero(selectedSlot, locationLabel, displaySlot + 1);
        ImGui.Dummy(new Vector2(0f, 10f));
        ImGui.PushTextWrapPos(0f);
        ImGui.TextWrapped(selectedSlot.Description);
        ImGui.PopTextWrapPos();
        ImGui.Dummy(new Vector2(0f, 8f));

        bool canSendToQuick = !selectedSlot.IsEmpty && displaySlot >= 9;
        bool canSendToBag = !selectedSlot.IsEmpty && displaySlot < 9;
        float actionWidth = (ImGui.GetContentRegionAvail().X - 8f) * 0.5f;
        if (!canSendToQuick) ImGui.BeginDisabled();
        if (ImGui.Button("Send to hotbar", new Vector2(actionWidth, 0f)))
        {
            if (TryMoveInventoryRawIndexToRange(slots, selectedRawIndex, 0, 8, GetSelectedHotbarDisplayIndex()))
            {
                SetAnnouncement($"{selectedSlot.Title} moved to hotbar", 0.8f);
            }
        }
        if (!canSendToQuick) ImGui.EndDisabled();

        ImGui.SameLine();
        if (!canSendToBag) ImGui.BeginDisabled();
        if (ImGui.Button("Send to backpack", new Vector2(-1f, 0f)))
        {
            if (TryMoveInventoryRawIndexToRange(slots, selectedRawIndex, 9, 31, 9))
            {
                SetAnnouncement($"{selectedSlot.Title} moved to backpack", 0.8f);
            }
        }
        if (!canSendToBag) ImGui.EndDisabled();

        ImGui.Dummy(new Vector2(0f, 10f));
        ImGui.Separator();
        DrawInventorySectionHeader("Details", selectedSlot.WeaponIndex >= 0 ? "Weapon data" : "Slot actions", selectedSlot.Accent);
        ImGui.Dummy(new Vector2(0f, 6f));

        if (selectedSlot.WeaponIndex >= 0)
        {
            WeaponState selectedWeapon = player.Arsenal[selectedSlot.WeaponIndex];
            ImGui.TextWrapped(selectedWeapon.Definition.Description);
            ImGui.Dummy(new Vector2(0f, 6f));
            if (selectedWeapon.Unlocked)
            {
                if (ImGui.Button("Equip weapon", new Vector2(-1f, 0f)))
                {
                    player.TrySelectWeapon(selectedSlot.WeaponIndex);
                }
                ImGui.ProgressBar(selectedWeapon.LevelProgress, new Vector2(-1f, 0f), $"XP {selectedWeapon.Experience}/{selectedWeapon.NextLevelExperience}");
                ImGui.Text($"Damage {selectedWeapon.EffectiveDamage}   Clip {selectedWeapon.EffectiveClipSize}");
                ImGui.Text($"Fire {selectedWeapon.EffectiveFireInterval:0.000}s   Reload {selectedWeapon.EffectiveReloadTime:0.00}s");
                ImGui.Text($"Spread {selectedWeapon.EffectiveSpread:0.000}   Recoil {selectedWeapon.EffectiveRecoil:0.00}");
                ImGui.Text($"Slow {selectedWeapon.EffectiveZombieSlowSeconds:0.0}s   Knock {selectedWeapon.EffectiveZombieKnockback:0}");
                if (ImGui.CollapsingHeader("Weapon tuning"))
                {
                    ImGui.Text($"Weapon points {selectedWeapon.StatPoints}");
                    bool canSpend = selectedWeapon.StatPoints > 0;
                    DrawWeaponUpgradeButton(selectedWeapon, WeaponUpgradeKind.Damage, $"Damage + ({selectedWeapon.DamageTier})", canSpend);
                    DrawWeaponUpgradeButton(selectedWeapon, WeaponUpgradeKind.FireRate, $"Fire rate + ({selectedWeapon.FireRateTier})", canSpend);
                    DrawWeaponUpgradeButton(selectedWeapon, WeaponUpgradeKind.Reload, $"Reload + ({selectedWeapon.ReloadTier})", canSpend);
                    DrawWeaponUpgradeButton(selectedWeapon, WeaponUpgradeKind.Accuracy, $"Accuracy + ({selectedWeapon.AccuracyTier})", canSpend);
                    DrawWeaponUpgradeButton(selectedWeapon, WeaponUpgradeKind.Magazine, $"Magazine + ({selectedWeapon.MagazineTier})", canSpend);
                }
            }
            else
            {
                bool afford = player.Credits >= selectedWeapon.Definition.UnlockCost;
                if (!afford) ImGui.BeginDisabled();
                if (ImGui.Button($"Unlock for {selectedWeapon.Definition.UnlockCost} credits", new Vector2(-1f, 0f)))
                {
                    if (player.TryPurchaseWeapon(selectedWeapon.Definition.Name))
                    {
                        SetAnnouncement($"{selectedWeapon.Definition.Name} unlocked", 1f);
                    }
                }
                if (!afford) ImGui.EndDisabled();
            }
        }
        else
        {
            switch (selectedSlot.Kind)
            {
                case InventorySlotKind.Scrap:
                    if (ImGui.Button("Open craft menu", new Vector2(-1f, 0f)))
                    {
                        _inventoryViewMode = InventoryViewMode.Craft;
                    }
                    break;
                case InventorySlotKind.Barricade:
                    if (ImGui.Button("Place barricade", new Vector2(-1f, 0f)))
                    {
                        ActivateInventoryRawSlot(player, selectedRawIndex, new Size((int)screen.X, (int)screen.Y));
                    }
                    break;
                case InventorySlotKind.Grenade:
                    if (ImGui.Button("Throw grenade", new Vector2(-1f, 0f)))
                    {
                        ActivateInventoryRawSlot(player, selectedRawIndex, new Size((int)screen.X, (int)screen.Y));
                    }
                    break;
                case InventorySlotKind.Turret:
                    if (ImGui.Button("Deploy turret", new Vector2(-1f, 0f)))
                    {
                        ActivateInventoryRawSlot(player, selectedRawIndex, new Size((int)screen.X, (int)screen.Y));
                    }
                    break;
                case InventorySlotKind.Overdrive:
                    if (ImGui.Button("Activate overdrive", new Vector2(-1f, 0f)))
                    {
                        ActivateInventoryRawSlot(player, selectedRawIndex, new Size((int)screen.X, (int)screen.Y));
                    }
                    break;
                case InventorySlotKind.Empty:
                    ImGui.TextDisabled("Empty backpack space.");
                    ImGui.TextDisabled("Drop another tile here to keep the quickbar cleaner.");
                    break;
                default:
                    ImGui.TextDisabled("This slot is passive. Move it wherever it keeps the layout tidy.");
                    break;
            }

            if (ImGui.CollapsingHeader("Operator upgrades"))
            {
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Damage, $"Damage boost ({player.DamageUpgradeRank})");
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.FireRate, $"Fire rate boost ({player.FireRateUpgradeRank})");
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Reload, $"Reload boost ({player.ReloadUpgradeRank})");
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Mobility, $"Mobility boost ({player.MobilityUpgradeRank})");
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Vitality, $"Vitality boost ({player.VitalityUpgradeRank})");
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Grenades, $"Grenades boost ({player.GrenadeUpgradeRank})");
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Turrets, $"Turrets boost ({player.TurretUpgradeRank})");
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Overdrive, $"Overdrive boost ({player.OverdriveUpgradeRank})");
            }
        }

        ImGui.Dummy(new Vector2(0f, 10f));
        ImGui.Separator();
        ImGui.TextDisabled("Tab = inventory · C = craft · 1-9 = hotbar · E = use selected item");
        ImGui.EndChild();
    }

    private void DrawInventorySectionHeader(string title, string subtitle, Color accent)
    {
        ImGui.TextColored(ToVec4(accent), title);
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(subtitle);
        }
    }

    private void DrawInventorySummaryPill(string text, Color accent)
    {
        Vector2 pos = ImGui.GetCursorScreenPos();
        Vector2 textSize = ImGui.CalcTextSize(text);
        Vector2 size = new Vector2(textSize.X + 28f, 26f);
        ImGui.Dummy(size);
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Vector2 min = pos;
        Vector2 max = pos + size;
        dl.AddRectFilled(min, max, ToU32(Color.FromArgb(118, Mix(accent, Color.Black, 0.56f))), 13f);
        dl.AddRect(min, max, ToU32(Color.FromArgb(144, Mix(accent, Color.White, 0.18f))), 13f, ImDrawFlags.None, 1.1f);
        dl.AddCircleFilled(new Vector2(min.X + 12f, min.Y + size.Y * 0.5f), 3.5f, ToU32(Color.FromArgb(228, accent)), 14);
        dl.AddText(new Vector2(min.X + 20f, min.Y + 5f), ToU32(Color.FromArgb(232, 236, 242, 246)), text);
    }

    private int CountFilledDisplaySlots(List<InventoryViewSlot> slots, int startDisplayIndex, int endDisplayIndex)
    {
        int count = 0;
        for (int i = Math.Max(0, startDisplayIndex); i <= Math.Min(endDisplayIndex, _inventoryLayout.Length - 1); i++)
        {
            int rawIndex = _inventoryLayout[i];
            if (!slots[Math.Clamp(rawIndex, 0, slots.Count - 1)].IsEmpty)
            {
                count++;
            }
        }
        return count;
    }

    private int CountWeaponSlots(Player player)
    {
        int count = 0;
        for (int i = 0; i < Math.Min(Player.ActiveWeaponSlots, player.Arsenal.Count); i++)
        {
            if (player.Arsenal[i].Unlocked && !player.Arsenal[i].IsEmpty)
            {
                count++;
            }
        }
        return count;
    }

    private int CountReadySupportSlots(Player player)
    {
        int count = 0;
        if (player.BarricadeKits > 0) count++;
        if (player.GrenadeCooldownTimer <= 0f) count++;
        if (player.TurretCharges > 0) count++;
        if (player.Adrenaline > 0f || player.IsOverdriveActive) count++;
        return count;
    }

    private string GetInventoryDisplayLocationLabel(int displayIndex)
    {
        return displayIndex < 9 ? $"Quick slot {displayIndex + 1}" : $"Backpack slot {displayIndex + 1}";
    }

    private string GetHotbarSlotHelpText(Player player, int rawIndex)
    {
        return rawIndex switch
        {
            4 => "Passive resource stack for quick checks while looting.",
            5 => "Credit tracker. Useful when you are saving for unlocks.",
            6 => "Place cover with E when this slot is armed.",
            7 => "Throw with E or RMB. READY means the boom is available now.",
            8 => "Deploy on a valid tile with E.",
            9 => player.IsOverdriveActive ? "Burst is active right now." : "Charge it, arm it, then smash E.",
            _ => "Selected slot. Click, scroll or press a number to switch fast."
        };
    }

    private bool TryMoveInventoryRawIndexToRange(List<InventoryViewSlot> slots, int rawIndex, int startDisplayIndex, int endDisplayIndex, int fallbackDisplayIndex)
    {
        if (rawIndex < 0 || rawIndex >= slots.Count || slots[rawIndex].IsEmpty)
        {
            return false;
        }

        int currentDisplayIndex = FindInventoryDisplayIndex(rawIndex);
        if (currentDisplayIndex >= startDisplayIndex && currentDisplayIndex <= endDisplayIndex)
        {
            return false;
        }

        int targetDisplayIndex = -1;
        for (int i = Math.Max(0, startDisplayIndex); i <= Math.Min(endDisplayIndex, _inventoryLayout.Length - 1); i++)
        {
            int candidateRawIndex = _inventoryLayout[i];
            if (slots[Math.Clamp(candidateRawIndex, 0, slots.Count - 1)].IsEmpty)
            {
                targetDisplayIndex = i;
                break;
            }
        }

        if (targetDisplayIndex < 0)
        {
            targetDisplayIndex = Math.Clamp(fallbackDisplayIndex, startDisplayIndex, endDisplayIndex);
        }

        if (targetDisplayIndex == currentDisplayIndex)
        {
            return false;
        }

        (_inventoryLayout[currentDisplayIndex], _inventoryLayout[targetDisplayIndex]) = (_inventoryLayout[targetDisplayIndex], _inventoryLayout[currentDisplayIndex]);
        _inventorySelectedIndex = rawIndex;
        return true;
    }

    private void DrawInventoryDetailHero(InventoryViewSlot slot, string locationLabel, int displaySlot)
    {
        Vector2 pos = ImGui.GetCursorScreenPos();
        Vector2 size = new Vector2(Math.Max(0f, ImGui.GetContentRegionAvail().X), 96f);
        ImGui.Dummy(size);
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Vector2 min = pos;
        Vector2 max = pos + size;
        Color accent = slot.IsEmpty ? Color.FromArgb(92, 100, 110) : slot.Accent;
        dl.AddRectFilled(min, max, ToU32(Color.FromArgb(128, Mix(accent, Color.Black, 0.52f))), 16f);
        dl.AddRect(min, max, ToU32(Color.FromArgb(166, Mix(accent, Color.White, 0.2f))), 16f, ImDrawFlags.None, 1.2f);
        if (!slot.IsEmpty)
        {
            DrawInventoryLineAccent(dl, min, max, accent, 16f, true);
        }

        RectangleF iconRect = new RectangleF(min.X + 16f, min.Y + 18f, 30f, 30f);
        DrawInventoryIcon(dl, iconRect, slot);
        dl.AddText(new Vector2(iconRect.Right + 12f, min.Y + 16f), ToU32(Color.FromArgb(238, 238, 244, 248)), slot.Title);
        dl.AddText(new Vector2(iconRect.Right + 12f, min.Y + 44f), ToU32(Color.FromArgb(214, 214, 222, 232)), string.IsNullOrWhiteSpace(slot.Value) ? "—" : slot.Value);

        string badge = $"#{displaySlot:00}";
        Vector2 badgeSize = ImGui.CalcTextSize(badge);
        Vector2 badgeMin = new Vector2(max.X - badgeSize.X - 44f, min.Y + 16f);
        Vector2 badgeMax = new Vector2(max.X - 16f, min.Y + 40f);
        dl.AddRectFilled(badgeMin, badgeMax, ToU32(Color.FromArgb(120, Mix(accent, Color.Black, 0.36f))), 11f);
        dl.AddRect(badgeMin, badgeMax, ToU32(Color.FromArgb(160, Mix(accent, Color.White, 0.22f))), 11f, ImDrawFlags.None, 1f);
        dl.AddText(new Vector2(badgeMin.X + 12f, badgeMin.Y + 4f), ToU32(Color.FromArgb(236, 236, 242, 246)), badge);
        dl.AddText(new Vector2(min.X + 16f, min.Y + 64f), ToU32(Color.FromArgb(196, 206, 214, 224)), locationLabel);
    }

    private List<CraftRecipeView> BuildCraftRecipes(Player player)
    {
        return new List<CraftRecipeView>
        {
            new CraftRecipeView(player.GetWeaponState("SMG")?.Unlocked == true ? "SMG crafted" : "SMG frame", "4 scrap + 40 cr", "Unlock the SMG and make it appear in the inventory and hotbar pool.", player.GetWeaponState("SMG")?.Unlocked == true ? "Already crafted" : "Craft SMG", Color.FromArgb(76, 164, 148), InventorySlotKind.Weapon),
            new CraftRecipeView(player.GetWeaponState("SHOTGUN")?.Unlocked == true ? "Shotgun crafted" : "Shotgun frame", "6 scrap + 80 cr", "Unlock the shotgun and add it to the bag for dragging or hotbar use.", player.GetWeaponState("SHOTGUN")?.Unlocked == true ? "Already crafted" : "Craft shotgun", Color.FromArgb(182, 124, 72), InventorySlotKind.Weapon),
            new CraftRecipeView(player.GetWeaponState("CARBINE")?.Unlocked == true ? "Carbine crafted" : "Carbine frame", "8 scrap + 110 cr", "Unlock the carbine and drop it into your inventory roster.", player.GetWeaponState("CARBINE")?.Unlocked == true ? "Already crafted" : "Craft carbine", Color.FromArgb(132, 98, 184), InventorySlotKind.Weapon),
            new CraftRecipeView("Barricade kit", $"{_scrapCraftCost} scrap", "Build one barricade kit you can place straight from the hotbar.", "Craft barricade kit", Color.FromArgb(150, 106, 70), InventorySlotKind.Barricade),
            new CraftRecipeView("Ammo cache", "3 scrap", "Top up reserve ammo across your unlocked weapons.", "Pack ammo cache", Color.FromArgb(206, 156, 74), InventorySlotKind.Weapon),
            new CraftRecipeView("Armor plate", "2 scrap + 8 cr", "Patch 24 armor instantly. Good when a wave is about to slap you.", "Patch armor", Color.FromArgb(88, 118, 200), InventorySlotKind.Armor),
            new CraftRecipeView("Med patch", "2 scrap + 12 cr", "Restore 32 HP right away.", "Apply med patch", Color.FromArgb(164, 88, 94), InventorySlotKind.Health),
            new CraftRecipeView("Turret battery", "6 scrap + 22 cr", "Restore one turret charge if you have room for it.", "Build turret battery", Color.FromArgb(76, 144, 182), InventorySlotKind.Turret),
            new CraftRecipeView("Overdrive cell", "5 scrap + 18 cr", "Charge 45 percent of your overdrive meter.", "Charge overdrive", Color.FromArgb(214, 134, 72), InventorySlotKind.Overdrive)
        };
    }

    private void DrawCraftMenuPanel(Player player, List<CraftRecipeView> recipes)
    {
        float listWidth = Math.Min(620f, ImGui.GetContentRegionAvail().X * 0.56f);
        ImGui.BeginChild("craft_list", new Vector2(listWidth, 0f), ImGuiChildFlags.Borders);
        ImGui.TextDisabled("Recipes");
        ImGui.SameLine();
        ImGui.TextDisabled("separate from the bag so it stays readable");
        ImGui.Spacing();

        const int craftColumns = 2;
        float spacing = 10f;
        float tileWidth = MathF.Floor((ImGui.GetContentRegionAvail().X - spacing * (craftColumns - 1)) / craftColumns);
        for (int i = 0; i < recipes.Count; i++)
        {
            if (i % craftColumns != 0)
            {
                ImGui.SameLine();
            }

            if (DrawCraftRecipeTile($"craft_{i}", recipes[i], new Vector2(tileWidth, 88f), _craftRecipeIndex == i))
            {
                _craftRecipeIndex = i;
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("craft_detail", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
        CraftRecipeView selected = recipes[Math.Clamp(_craftRecipeIndex, 0, recipes.Count - 1)];
        ImGui.TextColored(ToVec4(selected.Accent), selected.Title);
        ImGui.SameLine();
        ImGui.TextDisabled(selected.Cost);
        ImGui.Separator();
        ImGui.TextWrapped(selected.Description);
        ImGui.Spacing();
        ImGui.Text($"Scrap {player.Scrap}");
        ImGui.Text($"Credits {player.Credits}");
        ImGui.Text($"Barricades {player.BarricadeKits}   Turrets {player.TurretCharges}/{player.MaxTurretCharges}");
        ImGui.Text($"HP {player.Health}/{player.MaxHealth}   Armor {player.Armor}/100   OVR {(int)player.Adrenaline}%");
        ImGui.Spacing();
        if (ImGui.Button(selected.ActionText, new Vector2(-1f, 0f)))
        {
            TryCraftRecipe(player, _craftRecipeIndex);
        }
        ImGui.Spacing();
        ImGui.TextDisabled("Crafting lives in its own tab now, so the inventory stays about items and dragging instead of turning into soup.");
        ImGui.Separator();
        ImGui.TextDisabled("Shortcut: press C anytime in a run.");
        ImGui.EndChild();
    }

    private void DrawUpgradeMenuPanel(Player player)
    {
        List<UpgradeWorkbenchView> entries = BuildUpgradeWorkbenchEntries(player);
        _upgradeWorkbenchIndex = Math.Clamp(_upgradeWorkbenchIndex, 0, entries.Count - 1);
        UpgradeWorkbenchView selected = entries[_upgradeWorkbenchIndex];

        float listWidth = Math.Min(420f, ImGui.GetContentRegionAvail().X * 0.38f);
        ImGui.BeginChild("upgrade_list", new Vector2(listWidth, 0f), ImGuiChildFlags.Borders);
        ImGui.TextDisabled("Support workshop");
        ImGui.Spacing();
        ImGui.TextWrapped("Pick what you want to improve first. Left side is the gear category, right side is the nasty upgrade stuff.");
        ImGui.Spacing();

        const int upgradeColumns = 3;
        float spacing = 10f;
        float tileWidth = MathF.Floor((ImGui.GetContentRegionAvail().X - spacing * (upgradeColumns - 1)) / upgradeColumns);
        for (int i = 0; i < entries.Count; i++)
        {
            if (i % upgradeColumns != 0)
            {
                ImGui.SameLine();
            }

            if (DrawUpgradeWorkbenchTile($"upgrade_tile_{i}", entries[i], new Vector2(tileWidth, 64f), _upgradeWorkbenchIndex == i))
            {
                _upgradeWorkbenchIndex = i;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("Resources");
        ImGui.Text($"Credits {player.Credits}");
        ImGui.Text($"Scrap {player.Scrap}");
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("upgrade_detail", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
        ImGui.TextColored(ToVec4(selected.Accent), selected.Title);
        ImGui.SameLine();
        ImGui.TextDisabled(selected.Subtitle);
        ImGui.Separator();
        ImGui.TextWrapped(selected.Description);
        ImGui.Spacing();
        DrawSelectedUpgradeWorkbenchDetail(player, _upgradeWorkbenchIndex);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("Craft tab makes the item. Upgrade tab makes it meaner.");
        ImGui.EndChild();
    }

    private void DrawInventoryLineAccent(ImDrawListPtr dl, Vector2 min, Vector2 max, Color accent, float rounding, bool emphasized)
    {
        float width = max.X - min.X;
        if (width <= 20f)
        {
            return;
        }

        float y = min.Y + 8f;
        float pad = 9f;
        float segment = MathF.Max(16f, MathF.Min(width * 0.24f, 42f));
        float thickness = emphasized ? 2.4f : 1.7f;
        uint lineColor = ToU32(Color.FromArgb(emphasized ? 232 : 188, accent));
        uint ghostColor = ToU32(Color.FromArgb(72, Mix(accent, Color.White, 0.24f)));

        dl.AddLine(new Vector2(min.X + pad, y), new Vector2(min.X + pad + segment, y), ghostColor, thickness + 1f);
        dl.AddLine(new Vector2(max.X - pad - segment, y), new Vector2(max.X - pad, y), ghostColor, thickness + 1f);
        dl.AddLine(new Vector2(min.X + pad, y), new Vector2(min.X + pad + segment, y), lineColor, thickness);
        dl.AddLine(new Vector2(max.X - pad - segment, y), new Vector2(max.X - pad, y), lineColor, thickness);

        if (emphasized)
        {
            dl.AddCircleFilled(new Vector2(min.X + pad + segment + 7f, y), 2.2f, ToU32(Color.FromArgb(228, accent)), 12);
        }

        dl.AddLine(new Vector2(min.X + rounding * 0.55f, min.Y + 1.5f), new Vector2(max.X - rounding * 0.55f, min.Y + 1.5f), ToU32(Color.FromArgb(44, accent)), 1f);
    }

    private bool DrawInventoryModeButton(string id, string label, InventorySlotKind kind, Color accent, bool active)
    {
        Vector2 size = new Vector2(Math.Max(48f, ImGui.GetContentRegionAvail().X), 56f);
        ImGui.PushID(id);
        bool clicked = ImGui.InvisibleButton("##mode", size);
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Color panel = active ? Color.FromArgb(188, Mix(accent, Color.Black, 0.34f)) : Color.FromArgb(118, 32, 38, 46);
        dl.AddRectFilled(min, max, ToU32(panel), 12f);
        dl.AddRect(min, max, ToU32(Color.FromArgb(active ? 220 : 120, active ? Mix(accent, Color.White, 0.48f) : Color.FromArgb(92, 108, 124))), 12f, ImDrawFlags.None, active ? 1.7f : 1.1f);
        InventoryViewSlot iconSlot = new InventoryViewSlot(label, string.Empty, string.Empty, accent, kind);
        DrawInventoryIcon(dl, new RectangleF(min.X + 10f, min.Y + 16f, 20f, 20f), iconSlot);
        dl.AddText(new Vector2(min.X + 38f, min.Y + 18f), ToU32(Color.FromArgb(236, 236, 242, 246)), label);
        ImGui.PopID();
        return clicked;
    }

    private List<UpgradeWorkbenchView> BuildUpgradeWorkbenchEntries(Player player)
    {
        return new List<UpgradeWorkbenchView>
        {
            new UpgradeWorkbenchView("Barricade", $"Tech {_barricadeTechLevel}/3", "Cheaper barricade crafting and one free barricade kit every time you improve the engineering line.", Color.FromArgb(150, 106, 70), InventorySlotKind.Barricade),
            new UpgradeWorkbenchView("Turret", $"Rank {player.TurretUpgradeRank}", "More turret power and better charge utility for when the wave gets rude.", Color.FromArgb(76, 144, 182), InventorySlotKind.Turret),
            new UpgradeWorkbenchView("Grenade", $"Rank {player.GrenadeUpgradeRank}", "Push grenade handling harder so the cooldown hurts less and the boom matters more.", Color.FromArgb(190, 126, 68), InventorySlotKind.Grenade),
            new UpgradeWorkbenchView("Overdrive", $"Rank {player.OverdriveUpgradeRank}", "Stretch your overdrive duration and make the panic button feel like a proper steroid shot.", Color.FromArgb(214, 134, 72), InventorySlotKind.Overdrive),
            new UpgradeWorkbenchView("Weapons", $"DMG {player.DamageUpgradeRank} · FR {player.FireRateUpgradeRank}", "Core weapon handling: more damage, faster fire, quicker reload. Pure murder math.", Color.FromArgb(120, 96, 176), InventorySlotKind.Weapon),
            new UpgradeWorkbenchView("Mobility", $"Move {player.MoveSpeed:0}", "Movement and survival upgrades so you keep your ass attached during bad waves.", Color.FromArgb(86, 128, 200), InventorySlotKind.Health)
        };
    }

    private bool DrawUpgradeWorkbenchTile(string id, UpgradeWorkbenchView entry, Vector2 size, bool selected)
    {
        ImGui.PushID(id);
        bool clicked = ImGui.InvisibleButton("##upgradetile", size);
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Color panel = Color.FromArgb(selected ? 148 : 108, Mix(entry.Accent, Color.Black, 0.46f));
        dl.AddRectFilled(min, max, ToU32(panel), 12f);
        dl.AddRect(min, max, ToU32(Color.FromArgb(selected ? 214 : 128, selected ? Mix(entry.Accent, Color.White, 0.5f) : Mix(entry.Accent, Color.White, 0.16f))), 12f, ImDrawFlags.None, selected ? 1.6f : 1.1f);
        InventoryViewSlot iconSlot = new InventoryViewSlot(entry.Title, entry.Subtitle, entry.Description, entry.Accent, entry.IconKind);
        DrawInventoryIcon(dl, new RectangleF(min.X + 12f, min.Y + 12f, 18f, 18f), iconSlot);
        dl.AddText(new Vector2(min.X + 38f, min.Y + 10f), ToU32(Color.FromArgb(238, 238, 243, 246)), entry.Title);
        dl.AddText(new Vector2(min.X + 12f, min.Y + 36f), ToU32(Color.FromArgb(214, 214, 221, 230)), entry.Subtitle);
        ImGui.PopID();
        return clicked;
    }

    private void DrawSelectedUpgradeWorkbenchDetail(Player player, int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                {
                    int barricadeTechCost = GetBarricadeTechCost();
                    bool canBuyBarricadeTech = player.Credits >= barricadeTechCost && _barricadeTechLevel < 3;
                    if (!canBuyBarricadeTech) ImGui.BeginDisabled();
                    if (ImGui.Button($"Barricade engineering ({_barricadeTechLevel}/3) - {barricadeTechCost} cr", new Vector2(-1f, 0f)))
                    {
                        TryBuyBarricadeTech(player);
                    }
                    if (!canBuyBarricadeTech) ImGui.EndDisabled();
                    ImGui.Text($"Current barricade craft cost: {_scrapCraftCost} scrap");
                    ImGui.Text($"Kits in inventory: {player.BarricadeKits}");
                    ImGui.TextWrapped("Every rank drops one free barricade kit into the inventory and lowers future craft cost.");
                    break;
                }
            case 1:
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Turrets, $"Turret systems ({player.TurretUpgradeRank})");
                ImGui.Text($"Charges {player.TurretCharges}/{player.MaxTurretCharges}");
                ImGui.TextWrapped("This line is for stronger turret support and charge economy.");
                break;
            case 2:
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Grenades, $"Grenade systems ({player.GrenadeUpgradeRank})");
                ImGui.Text($"Cooldown {player.GrenadeCooldownDuration:0.0}s");
                ImGui.TextWrapped("Grenades get nastier and more usable instead of feeling like decorative pocket rocks.");
                break;
            case 3:
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Overdrive, $"Overdrive systems ({player.OverdriveUpgradeRank})");
                ImGui.Text($"Duration {player.OverdriveDuration:0.0}s");
                ImGui.Text($"Meter {(int)player.Adrenaline}%");
                ImGui.TextWrapped("Longer overdrive means more time to bully the wave before it bullies you.");
                break;
            case 4:
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Damage, $"Damage boost ({player.DamageUpgradeRank})");
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.FireRate, $"Fire rate boost ({player.FireRateUpgradeRank})");
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Reload, $"Reload boost ({player.ReloadUpgradeRank})");
                ImGui.Text($"Damage x{player.DamageMultiplier:0.00}   Fire x{player.FireRateMultiplier:0.00}");
                ImGui.Text($"Reload x{player.ReloadSpeedMultiplier:0.00}");
                break;
            default:
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Mobility, $"Mobility boost ({player.MobilityUpgradeRank})");
                DrawPlayerUpgradeButton(player, PlayerUpgradeKind.Vitality, $"Vitality boost ({player.VitalityUpgradeRank})");
                ImGui.Text($"Move {player.MoveSpeed:0}");
                ImGui.Text($"HP {player.Health}/{player.MaxHealth}   Armor {player.Armor}/100");
                break;
        }
    }

    private bool DrawCraftRecipeTile(string id, CraftRecipeView recipe, Vector2 size, bool selected)
    {
        ImGui.PushID(id);
        bool clicked = ImGui.InvisibleButton("##crafttile", size);
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Color panel = Color.FromArgb(selected ? 148 : 108, Mix(recipe.Accent, Color.Black, 0.46f));
        dl.AddRectFilled(min, max, ToU32(panel), 12f);
        dl.AddRect(min, max, ToU32(Color.FromArgb(selected ? 214 : 128, selected ? Mix(recipe.Accent, Color.White, 0.5f) : Mix(recipe.Accent, Color.White, 0.16f))), 12f, ImDrawFlags.None, selected ? 1.6f : 1.1f);
        InventoryViewSlot iconSlot = new InventoryViewSlot(recipe.Title, recipe.Cost, recipe.Description, recipe.Accent, recipe.IconKind);
        DrawInventoryIcon(dl, new RectangleF(min.X + 12f, min.Y + 14f, 22f, 22f), iconSlot);
        dl.AddText(new Vector2(min.X + 42f, min.Y + 12f), ToU32(Color.FromArgb(238, 238, 243, 246)), recipe.Title);
        dl.AddText(new Vector2(min.X + 42f, min.Y + 36f), ToU32(Color.FromArgb(220, 216, 224, 232)), recipe.Cost);
        dl.AddText(new Vector2(min.X + 12f, min.Y + 60f), ToU32(Color.FromArgb(196, 202, 210, 220)), recipe.ActionText);
        ImGui.PopID();
        return clicked;
    }

    private void HandleInventorySlotInput(int displayIndex, int rawIndex, InventoryViewSlot slot, bool clicked)
    {
        if (clicked)
        {
            _inventorySelectedIndex = rawIndex;
            if (slot.WeaponIndex >= 0)
            {
                _inventoryUpgradeWeaponIndex = slot.WeaponIndex;
            }
        }

        if (_inventoryDragRawIndex < 0 && !slot.IsEmpty && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 4f))
        {
            _inventoryDragRawIndex = displayIndex;
            _inventoryDragHoverRawIndex = displayIndex;
            _inventorySelectedIndex = rawIndex;
        }

        if (_inventoryDragRawIndex >= 0)
        {
            Vector2 min = ImGui.GetItemRectMin();
            Vector2 max = ImGui.GetItemRectMax();
            if (ImGui.IsMouseHoveringRect(min, max, false))
            {
                _inventoryDragHoverRawIndex = displayIndex;
            }
        }
    }

    private bool DrawInventoryTile(string id, InventoryViewSlot slot, Vector2 size, string caption, bool selected, bool dragging, bool compact, bool dropTarget)
    {
        ImGui.PushID(id);
        bool clicked = ImGui.InvisibleButton("##tile", size);
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Color accent = slot.IsEmpty ? Color.FromArgb(92, 100, 110) : slot.Accent;
        Color panel = slot.IsEmpty ? Color.FromArgb(88, 30, 36, 44) : Color.FromArgb(compact ? 122 : 108, Mix(accent, Color.Black, 0.48f));
        if (selected)
        {
            panel = Mix(panel, Color.White, 0.08f);
        }
        if (dragging)
        {
            panel = Mix(accent, Color.White, 0.24f);
        }

        float rounding = compact ? 12f : 14f;
        dl.AddRectFilled(min + new Vector2(0f, 3f), max + new Vector2(0f, 3f), ToU32(Color.FromArgb(42, 0, 0, 0)), rounding);
        if (selected)
        {
            dl.AddRectFilled(min + new Vector2(-1f, -1f), max + new Vector2(1f, 1f), ToU32(Color.FromArgb(36, accent)), rounding + 1f);
        }
        dl.AddRectFilled(min, max, ToU32(panel), rounding);
        dl.AddRect(min, max, ToU32(Color.FromArgb(selected ? 214 : 130, selected ? Mix(accent, Color.White, 0.56f) : Mix(accent, Color.White, 0.14f))), rounding, ImDrawFlags.None, selected ? 1.7f : 1.1f);
        if (!slot.IsEmpty)
        {
            DrawInventoryLineAccent(dl, min, max, accent, rounding, selected || dragging);
        }
        if (dropTarget)
        {
            dl.AddRect(min + new Vector2(2f, 2f), max - new Vector2(2f, 2f), ToU32(Color.FromArgb(220, 132, 216, 255)), rounding - 2f, ImDrawFlags.None, 2f);
        }

        Vector2 badgeTextPos = new Vector2(min.X + 10f, min.Y + 7f);
        dl.AddText(badgeTextPos, ToU32(Color.FromArgb(218, 198, 206, 214)), caption);

        RectangleF iconRect = compact
            ? new RectangleF(min.X + 10f, min.Y + 28f, 18f, 18f)
            : new RectangleF(min.X + 10f, min.Y + 28f, 20f, 20f);
        if (!slot.IsEmpty)
        {
            DrawInventoryIcon(dl, iconRect, slot);
        }

        string title = slot.IsEmpty ? string.Empty : (compact ? GetCompactSlotTitle(slot) : slot.Title);
        string value = slot.IsEmpty ? string.Empty : (compact ? GetCompactSlotValue(slot) : slot.Value);
        float textX = slot.IsEmpty ? min.X + 10f : iconRect.Right + 8f;
        if (!string.IsNullOrWhiteSpace(title))
        {
            dl.AddText(new Vector2(textX, min.Y + 28f), ToU32(Color.FromArgb(236, 236, 242, 246)), title);
        }
        if (!string.IsNullOrWhiteSpace(value))
        {
            dl.AddText(new Vector2(textX, max.Y - 24f), ToU32(Color.FromArgb(214, 214, 221, 230)), value);
        }

        if (slot.Highlighted)
        {
            dl.AddCircleFilled(new Vector2(max.X - 12f, min.Y + 14f), 3.5f, ToU32(Color.FromArgb(228, 236, 198, 96)), 12);
        }


        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextColored(ToVec4(accent), slot.Title);
            if (!string.IsNullOrWhiteSpace(value))
            {
                ImGui.TextDisabled(value);
            }
            ImGui.PushTextWrapPos(340f);
            ImGui.TextUnformatted(slot.Description);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        ImGui.PopID();
        return clicked;
    }

    private bool DrawHotbarTile(string id, InventoryViewSlot slot, Vector2 size, int displayIndex, bool active)
    {
        ImGui.PushID(id);
        bool clicked = ImGui.InvisibleButton("##hotbar_tile", size);
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Color accent = slot.IsEmpty ? Color.FromArgb(92, 100, 110) : slot.Accent;
        Color panel = slot.IsEmpty ? Color.FromArgb(96, 28, 34, 42) : Color.FromArgb(active ? 156 : 126, Mix(accent, Color.Black, active ? 0.32f : 0.46f));

        dl.AddRectFilled(min + new Vector2(0f, 4f), max + new Vector2(0f, 4f), ToU32(Color.FromArgb(42, 0, 0, 0)), 14f);
        dl.AddRectFilled(min, max, ToU32(panel), 14f);
        if (active && !slot.IsEmpty)
        {
            DrawInventoryLineAccent(dl, min, max, accent, 14f, true);
        }
        dl.AddRect(min, max, ToU32(Color.FromArgb(active ? 228 : 138, active ? Mix(accent, Color.White, 0.56f) : Mix(accent, Color.White, 0.16f))), 14f, ImDrawFlags.None, active ? 1.8f : 1.1f);

        string badgeText = (displayIndex + 1).ToString();
        Vector2 badgeTextPos = new Vector2(min.X + 10f, min.Y + 7f);
        dl.AddText(badgeTextPos, ToU32(Color.FromArgb(224, 204, 212, 220)), badgeText);

        RectangleF iconRect = new RectangleF(min.X + 10f, min.Y + 28f, 18f, 18f);
        if (!slot.IsEmpty)
        {
            DrawInventoryIcon(dl, iconRect, slot);
        }

        string title = slot.IsEmpty ? string.Empty : GetCompactSlotTitle(slot);
        if (!string.IsNullOrWhiteSpace(title))
        {
            Vector2 titleSize = ImGui.CalcTextSize(title);
            float titleX = MathF.Max(min.X + 8f, MathF.Min(max.X - titleSize.X - 8f, iconRect.Right + 6f));
            dl.AddText(new Vector2(titleX, min.Y + 28f), ToU32(Color.FromArgb(238, 238, 244, 248)), title);
        }

        string value = slot.IsEmpty ? string.Empty : GetCompactSlotValue(slot);
        if (!string.IsNullOrWhiteSpace(value))
        {
            Vector2 valueSize = ImGui.CalcTextSize(value);
            dl.AddText(new Vector2(max.X - valueSize.X - 8f, max.Y - 22f), ToU32(Color.FromArgb(216, 214, 222, 232)), value);
        }

        if (active)
        {
            Vector2 activeMin = new Vector2(min.X + 8f, max.Y - 22f);
            Vector2 activeMax = new Vector2(min.X + 48f, max.Y - 6f);
            dl.AddRectFilled(activeMin, activeMax, ToU32(Color.FromArgb(120, Mix(accent, Color.Black, 0.18f))), 8f);
            dl.AddText(new Vector2(activeMin.X + 7f, activeMin.Y + 1f), ToU32(Color.FromArgb(238, 238, 244, 248)), "ARM");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextColored(ToVec4(accent), GetCompactSlotTitle(slot));
            if (!string.IsNullOrWhiteSpace(value))
            {
                ImGui.TextDisabled(value);
            }
            ImGui.PushTextWrapPos(300f);
            ImGui.TextUnformatted(slot.Description);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        ImGui.PopID();
        return clicked;
    }

    private void DrawInventoryDragGhost(ImDrawListPtr dl, InventoryViewSlot slot, Vector2 pos)
    {
        Vector2 size = new Vector2(154f, 58f);
        Vector2 max = pos + size;
        Color accent = slot.IsEmpty ? Color.FromArgb(92, 100, 110) : slot.Accent;
        dl.AddRectFilled(pos, max, ToU32(Color.FromArgb(198, Mix(accent, Color.Black, 0.44f))), 12f);
        dl.AddRect(pos, max, ToU32(Color.FromArgb(220, Mix(accent, Color.White, 0.22f))), 12f, ImDrawFlags.None, 1.5f);
        RectangleF iconRect = new RectangleF(pos.X + 10f, pos.Y + 10f, 18f, 18f);
        DrawInventoryIcon(dl, iconRect, slot);
        dl.AddText(new Vector2(iconRect.Right + 8f, pos.Y + 10f), ToU32(Color.FromArgb(240, 238, 242, 246)), slot.Title);
        dl.AddText(new Vector2(iconRect.Right + 8f, pos.Y + 32f), ToU32(Color.FromArgb(224, 214, 221, 230)), slot.Value);
    }

    private void DrawInventoryIcon(ImDrawListPtr dl, RectangleF rect, InventoryViewSlot slot)
    {
        Vector2 a = new Vector2(rect.Left, rect.Top);
        Vector2 b = new Vector2(rect.Right, rect.Bottom);
        Vector2 c = new Vector2((rect.Left + rect.Right) * 0.5f, (rect.Top + rect.Bottom) * 0.5f);
        Color accent = slot.IsEmpty ? Color.FromArgb(118, 126, 138) : slot.Accent;
        uint fill = ToU32(Color.FromArgb(220, accent));
        uint line = ToU32(Color.FromArgb(240, Mix(accent, Color.White, 0.32f)));

        switch (slot.Kind)
        {
            case InventorySlotKind.Weapon:
                if (slot.WeaponIndex == 2)
                {
                    dl.AddRectFilled(new Vector2(a.X + 1f, c.Y - 2f), new Vector2(b.X - 3f, c.Y + 2f), fill, 2f);
                    dl.AddRectFilled(new Vector2(c.X - 1f, c.Y - 1f), new Vector2(c.X + 3f, b.Y - 1f), fill, 2f);
                }
                else
                {
                    dl.AddRectFilled(new Vector2(a.X + 1f, c.Y - 2f), new Vector2(b.X - 5f, c.Y + 2f), fill, 2f);
                    dl.AddRectFilled(new Vector2(a.X + 6f, c.Y + 1f), new Vector2(a.X + 10f, b.Y - 2f), fill, 2f);
                    if (slot.WeaponIndex == 3)
                    {
                        dl.AddLine(new Vector2(b.X - 5f, c.Y - 2f), new Vector2(b.X - 1f, a.Y + 3f), line, 2f);
                    }
                    if (slot.WeaponIndex == 1)
                    {
                        dl.AddLine(new Vector2(a.X + 4f, a.Y + 4f), new Vector2(b.X - 5f, a.Y + 4f), line, 1.2f);
                    }
                }
                break;
            case InventorySlotKind.Scrap:
                dl.AddRectFilled(new Vector2(a.X + 2f, a.Y + 4f), new Vector2(a.X + 9f, a.Y + 11f), fill, 2f);
                dl.AddRectFilled(new Vector2(a.X + 7f, a.Y + 8f), new Vector2(a.X + 15f, a.Y + 15f), fill, 2f);
                dl.AddRectFilled(new Vector2(a.X + 11f, a.Y + 3f), new Vector2(b.X - 1f, a.Y + 9f), fill, 2f);
                break;
            case InventorySlotKind.Credits:
                dl.AddCircleFilled(c, rect.Width * 0.34f, fill, 18);
                dl.AddCircle(c, rect.Width * 0.34f, line, 18, 1.4f);
                break;
            case InventorySlotKind.Barricade:
                dl.AddRectFilled(new Vector2(a.X + 2f, c.Y + 1f), new Vector2(b.X - 2f, b.Y - 2f), fill, 2f);
                dl.AddLine(new Vector2(a.X + 6f, c.Y + 1f), new Vector2(a.X + 6f, b.Y - 2f), line, 1.2f);
                dl.AddLine(new Vector2(c.X, c.Y + 1f), new Vector2(c.X, b.Y - 2f), line, 1.2f);
                break;
            case InventorySlotKind.Grenade:
                dl.AddCircleFilled(new Vector2(c.X, c.Y + 2f), rect.Width * 0.28f, fill, 18);
                dl.AddLine(new Vector2(c.X, a.Y + 2f), new Vector2(c.X, a.Y + 7f), line, 1.4f);
                dl.AddCircle(new Vector2(c.X + 4f, a.Y + 3f), 3f, line, 12, 1.2f);
                break;
            case InventorySlotKind.Turret:
                dl.AddCircleFilled(new Vector2(c.X, c.Y + 4f), rect.Width * 0.24f, fill, 18);
                dl.AddLine(new Vector2(c.X, c.Y + 4f), new Vector2(b.X - 2f, c.Y - 1f), line, 2.6f);
                dl.AddLine(new Vector2(c.X - 4f, b.Y - 2f), new Vector2(c.X - 1f, c.Y + 8f), line, 1.4f);
                dl.AddLine(new Vector2(c.X + 4f, b.Y - 2f), new Vector2(c.X + 1f, c.Y + 8f), line, 1.4f);
                break;
            case InventorySlotKind.Overdrive:
                dl.AddLine(new Vector2(c.X - 3f, a.Y + 2f), new Vector2(c.X - 1f, c.Y - 1f), line, 2f);
                dl.AddLine(new Vector2(c.X - 1f, c.Y - 1f), new Vector2(c.X + 2f, c.Y - 1f), line, 2f);
                dl.AddLine(new Vector2(c.X + 2f, c.Y - 1f), new Vector2(c.X - 1f, b.Y - 2f), line, 2f);
                dl.AddLine(new Vector2(c.X - 1f, b.Y - 2f), new Vector2(c.X + 5f, c.Y + 1f), line, 2f);
                break;
            case InventorySlotKind.Armor:
                dl.AddRectFilled(new Vector2(a.X + 4f, a.Y + 3f), new Vector2(b.X - 4f, b.Y - 3f), fill, 3f);
                dl.AddLine(new Vector2(c.X, a.Y + 3f), new Vector2(c.X, b.Y - 3f), line, 1.2f);
                dl.AddLine(new Vector2(a.X + 4f, c.Y), new Vector2(b.X - 4f, c.Y), line, 1.2f);
                break;
            case InventorySlotKind.Health:
                dl.AddRectFilled(new Vector2(c.X - 3f, a.Y + 3f), new Vector2(c.X + 3f, b.Y - 3f), fill, 2f);
                dl.AddRectFilled(new Vector2(a.X + 3f, c.Y - 3f), new Vector2(b.X - 3f, c.Y + 3f), fill, 2f);
                break;
            default:
                dl.AddRect(new Vector2(a.X + 3f, a.Y + 3f), new Vector2(b.X - 3f, b.Y - 3f), ToU32(Color.FromArgb(116, 116, 124, 136)), 3f, ImDrawFlags.None, 1f);
                break;
        }
    }

    private string GetCompactSlotTitle(InventoryViewSlot slot)
    {
        return slot.Kind switch
        {
            InventorySlotKind.Weapon => slot.WeaponIndex >= 0 ? GetWeaponShortName(Player.Arsenal[slot.WeaponIndex].Definition) : slot.Title,
            InventorySlotKind.Credits => "CR",
            InventorySlotKind.Barricade => "BARR",
            InventorySlotKind.Grenade => "GRN",
            InventorySlotKind.Turret => "TUR",
            InventorySlotKind.Overdrive => "OVR",
            InventorySlotKind.Armor => "ARM",
            InventorySlotKind.Health => "MED",
            _ => slot.Title
        };
    }

    private static string GetCompactSlotValue(InventoryViewSlot slot)
    {
        return slot.Kind == InventorySlotKind.Empty ? string.Empty : slot.Value;
    }

    private void DrawWeaponUpgradeButton(WeaponState state, WeaponUpgradeKind kind, string label, bool canSpend)
    {
        ImGui.PushID(label);
        if (!canSpend) ImGui.BeginDisabled();
        if (ImGui.Button(label, new Vector2(-1f, 0f)))
        {
            if (state.TrySpendPoint(kind))
            {
                SetAnnouncement($"{state.Definition.Name}: {kind} upgraded", 0.8f);
            }
        }
        if (!canSpend) ImGui.EndDisabled();
        ImGui.PopID();
    }

    private void DrawPlayerUpgradeButton(Player player, PlayerUpgradeKind kind, string label)
    {
        int cost = player.GetUpgradeCost(kind);
        bool afford = player.Credits >= cost;
        ImGui.PushID(label);
        if (!afford) ImGui.BeginDisabled();
        if (ImGui.Button($"{label} - {cost} cr", new Vector2(-1f, 0f)))
        {
            if (player.TryBuyUpgrade(kind))
            {
                SetAnnouncement($"{label} purchased", 0.9f);
            }
        }
        if (!afford) ImGui.EndDisabled();
        ImGui.PopID();
    }

    private void BeginModalWindow(string title, Vector2 screen, float width, float height)
    {
        BeginModalWindow(title, screen, width, height, 1f);
    }

    private void BeginModalWindow(string title, Vector2 screen, float width, float height, float uiScale)
    {
        ImGui.SetNextWindowPos(new Vector2((screen.X - width) * 0.5f, (screen.Y - height) * 0.5f), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
        ImGui.Begin(title, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings);
        ImGui.SetWindowFontScale(Math.Clamp(0.92f * uiScale, 0.82f, 1.16f));
    }

    private void BeginOverlayWindow(string id, Vector2 pos, Vector2 size, float alpha)
    {
        BeginOverlayWindow(id, pos, size, alpha, 1f);
    }

    private void BeginOverlayWindow(string id, Vector2 pos, Vector2 size, float alpha, float uiScale)
    {
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(alpha);
        ImGui.Begin(id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
        ImGui.SetWindowFontScale(Math.Clamp(0.92f * uiScale, 0.80f, 1.16f));
    }

    private void DrawImGuiChatOverlay(Vector2 screen, float hudScale, bool allowInput)
    {
        RectangleF rect = GetChatPanelRect(screen, hudScale);
        BeginOverlayWindow("##chat_panel", new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), 0.86f, hudScale);
        ImGui.TextColored(ToVec4(Color.FromArgb(124, 196, 255)), "Chat");
        ImGui.SameLine();
        ImGui.TextDisabled(allowInput ? (_chatOpen ? "Enter = send · Esc = close" : "Enter or click the field") : "Read only right now");

        float footerReserve = allowInput ? 34f * hudScale : 0f;
        float historyHeight = Math.Max(48f * hudScale, rect.Height - 46f * hudScale - footerReserve);
        ImGui.BeginChild("##chat_log", new Vector2(0f, historyHeight), ImGuiChildFlags.Borders);
        if (_chatEntries.Count == 0)
        {
            ImGui.TextDisabled("No messages yet.");
        }
        else
        {
            for (int i = 0; i < _chatEntries.Count; i++)
            {
                ChatEntry entry = _chatEntries[i];
                ImGui.PushTextWrapPos(0f);
                if (entry.IsSystem)
                {
                    ImGui.TextColored(ToVec4(Color.FromArgb(186, 214, 226, 238)), $"> {entry.Message}");
                }
                else
                {
                    ImGui.TextColored(ToVec4(entry.Accent), entry.Sender + ":");
                    ImGui.SameLine(0f, 4f);
                    ImGui.TextWrapped(entry.Message);
                }
                ImGui.PopTextWrapPos();
            }

            if (_chatScrollToBottom)
            {
                ImGui.SetScrollHereY(1f);
                _chatScrollToBottom = false;
            }
        }
        ImGui.EndChild();

        if (allowInput)
        {
            ImGui.TextDisabled(_chatOpen ? "Type and hit Enter." : "Click here or press Enter to write.");
            if (_chatFocusInputRequested)
            {
                ImGui.SetKeyboardFocusHere();
                _chatFocusInputRequested = false;
            }

            ImGui.SetNextItemWidth(-1f);
            bool submitted = ImGui.InputText("##chat_input", ref _chatInputBuffer, MaxChatMessageLength, ImGuiInputTextFlags.EnterReturnsTrue);
            bool itemActive = ImGui.IsItemActive() || ImGui.IsItemFocused();
            _chatOpen = submitted ? false : (itemActive || !string.IsNullOrWhiteSpace(_chatInputBuffer));
            if (submitted)
            {
                SubmitChatMessage();
            }
        }

        ImGui.End();
    }

    private float[] BuildFrameGraphOrdered()
    {
        float[] result = new float[_frameGraph.Length];
        for (int i = 0; i < _frameGraph.Length; i++)
        {
            result[i] = _frameGraph[(_frameGraphHead + i) % _frameGraph.Length];
        }
        return result;
    }

    private static uint ToU32(Color color)
    {
        return ImGui.ColorConvertFloat4ToU32(new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f));
    }

    private static Vector4 ToVec4(Color color)
    {
        return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }

    private Vector2 WorldToScreen(Vector2 world)
    {
        return new Vector2(world.X - Camera.X, world.Y - Camera.Y);
    }

    private void DrawSimpleBar(ImDrawListPtr draw, RectangleF rect, float value, Color fillColor)
    {
        value = Math.Clamp(value, 0f, 1f);
        Vector2 min = new Vector2(rect.Left, rect.Top);
        Vector2 max = new Vector2(rect.Right, rect.Bottom);
        draw.AddRectFilled(min, max, ToU32(Color.FromArgb(180, 16, 18, 22)), 3f);
        draw.AddRect(min, max, ToU32(Color.FromArgb(120, 255, 255, 255)), 3f);
        if (value <= 0f)
        {
            return;
        }

        Vector2 fillMax = new Vector2(rect.Left + rect.Width * value, rect.Bottom);
        draw.AddRectFilled(min, fillMax, ToU32(fillColor), 3f);
    }

}
