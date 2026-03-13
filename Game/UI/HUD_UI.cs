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

    private enum InventoryViewMode
    {
        Inventory,
        Craft
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

    private bool UseNativeImGuiUi() => true;

    public void DrawImGui(Vector2 displaySize)
    {
        Vector2 screen = displaySize;
        ImDrawListPtr bg = ImGui.GetBackgroundDrawList();
        ImDrawListPtr fg = ImGui.GetForegroundDrawList();

        DrawImGuiWorld(bg, screen);
        DrawImGuiDayNightOverlay(bg, screen);
        DrawImGuiWorldGlow(bg, screen);

        bool gameplayOverlay = _phase == GamePhase.Playing || _phase == GamePhase.Paused || _phase == GamePhase.GameOver;
        if (gameplayOverlay)
        {
            DrawImGuiCrosshair(fg);
        }

        switch (_phase)
        {
            case GamePhase.Title:
                DrawImGuiTint(fg, screen, 124);
                DrawImGuiTitle(fg, screen);
                break;
            case GamePhase.Settings:
                DrawImGuiTint(fg, screen, 146);
                DrawImGuiSettings(fg, screen);
                break;
            case GamePhase.MultiplayerMenu:
                DrawImGuiTint(fg, screen, 146);
                DrawImGuiMultiplayerMenu(fg, screen);
                break;
            case GamePhase.LanBrowser:
                DrawImGuiTint(fg, screen, 146);
                DrawImGuiLanBrowser(fg, screen);
                break;
            case GamePhase.HostSetup:
                DrawImGuiTint(fg, screen, 146);
                DrawImGuiHostSetup(fg, screen);
                break;
            case GamePhase.JoinSetup:
                DrawImGuiTint(fg, screen, 146);
                DrawImGuiJoinSetup(fg, screen);
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
    }

    private void DrawImGuiFloor(ImDrawListPtr draw, Vector2 screen)
    {
        float darkness = _dayNight.Darkness;
        uint top = ToU32(Mix(Color.FromArgb(26, 36, 48), Color.FromArgb(8, 12, 20), darkness));
        uint bottom = ToU32(Mix(Color.FromArgb(10, 14, 22), Color.FromArgb(2, 4, 8), darkness));
        draw.AddRectFilledMultiColor(Vector2.Zero, screen, top, top, bottom, bottom);

        int grid = TileMap.TileSize;
        uint gridColor = ToU32(Mix(Color.FromArgb(28, 78, 102, 122), Color.FromArgb(18, 62, 86, 122), darkness));
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

            Color baseColor = value == 1 ? Color.FromArgb(74, 84, 96) : Color.FromArgb(132, 96, 60);
            Color topColor = Mix(baseColor, Color.White, 0.12f);
            Color bottomColor = Mix(baseColor, Color.Black, 0.22f);
            Vector2 min = WorldToScreen(new Vector2(rect.Left, rect.Top));
            Vector2 max = WorldToScreen(new Vector2(rect.Right, rect.Bottom));
            draw.AddRectFilledMultiColor(min, max, ToU32(topColor), ToU32(topColor), ToU32(bottomColor), ToU32(bottomColor));
            draw.AddRect(min, max, ToU32(Color.FromArgb(160, Mix(baseColor, Color.Black, 0.35f))), 0f, ImDrawFlags.None, 1f);
        }
    }

    private void DrawImGuiLootCrates(ImDrawListPtr draw)
    {
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
            Color wood = Mix(Color.FromArgb(152, 100, 58), Color.FromArgb(235, 196, 158), flash * 0.35f);
            Color trim = Mix(Color.FromArgb(84, 56, 30), Color.White, flash * 0.2f);

            draw.AddRectFilled(min + new Vector2(2f, 4f), max + new Vector2(2f, 4f), ToU32(Color.FromArgb(38, 0, 0, 0)), 4f);
            draw.AddRectFilled(min, max, ToU32(wood), 4f);
            draw.AddRect(min, max, ToU32(Color.FromArgb(220, trim)), 4f, ImDrawFlags.None, 1.4f);
            draw.AddLine(new Vector2(min.X + 4f, p.Y), new Vector2(max.X - 4f, p.Y), ToU32(Color.FromArgb(180, 208, 170, 126)), 2f);
            draw.AddLine(new Vector2(p.X, min.Y + 4f), new Vector2(p.X, max.Y - 4f), ToU32(Color.FromArgb(180, 208, 170, 126)), 2f);

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
            draw.AddCircleFilled(p, radius, ToU32(Color.FromArgb(230, player.Accent)), 24);
            draw.AddCircle(p, radius, ToU32(Color.FromArgb(124, 255, 255, 255)), 24, 1.2f);

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
            draw.AddCircleFilled(p, r, ToU32(Color.FromArgb(214, remote.Accent)), 24);
            draw.AddCircle(p, r, ToU32(Color.FromArgb(104, 255, 255, 255)), 24, 1.1f);

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
        BeginModalWindow("Main Menu", screen, 520f, 420f);
        ImGui.Text("Survival 2D Alpha");
        ImGui.Separator();
        ImGui.TextWrapped("Testing create game...");
        ImGui.Spacing();

        if (ImGui.Button("Solo run", new Vector2(-1f, 0f))) ActivateTitleButton("solo");
        if (ImGui.Button("Multiplayer", new Vector2(-1f, 0f))) ActivateTitleButton("multi");
        if (ImGui.Button("Settings", new Vector2(-1f, 0f))) ActivateTitleButton("settings");
        if (ImGui.Button(_shuffleArenaOnStart ? "Arena shuffle: ON" : "Arena shuffle: OFF", new Vector2(-1f, 0f))) ActivateTitleButton("shuffle");

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
        BeginModalWindow("Settings", screen, 540f, 470f);
        ImGui.Text("Game settings");
        ImGui.Separator();

        if (ImGui.Button($"Difficulty: {GetDifficultyName()}", new Vector2(-1f, 0f))) ActivateSettingsButton("difficulty");
        if (ImGui.Button($"HUD scale: {GetUiScaleName()}", new Vector2(-1f, 0f))) ActivateSettingsButton("uiscale");
        if (ImGui.Button(_showHints ? "Hints: ON" : "Hints: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("hints");
        if (ImGui.Button(_sound.Enabled ? "Audio: ON" : "Audio: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("audio");
        if (ImGui.Button(_showNetworkDebug ? "Net debug: ON" : "Net debug: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("netdebug");
        if (ImGui.Button(_showFpsHud ? "FPS HUD: ON" : "FPS HUD: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("fpshud");
        if (ImGui.Button(_showPerfHud ? "Perf HUD: ON" : "Perf HUD: OFF", new Vector2(-1f, 0f))) ActivateSettingsButton("perfhud");
        if (ImGui.Button("Back", new Vector2(-1f, 0f))) ActivateSettingsButton("back");

        ImGui.End();
    }

    private void DrawImGuiHostSetup(ImDrawListPtr draw, Vector2 screen)
    {
        BeginModalWindow("Host Setup", screen, 540f, 360f);
        ImGui.Text("Host session");
        ImGui.Separator();
        ImGui.Text($"Port: {_joinPort}");
        ImGui.Text($"Slots: {_maxPlayers}");
        ImGui.Spacing();

        if (ImGui.Button($"Port -   {_joinPort}", new Vector2(-1f, 0f))) ActivateHostButton("portminus");
        if (ImGui.Button($"Port +   {_joinPort}", new Vector2(-1f, 0f))) ActivateHostButton("portplus");
        if (ImGui.Button($"Slots -  {_maxPlayers}", new Vector2(-1f, 0f))) ActivateHostButton("maxminus");
        if (ImGui.Button($"Slots +  {_maxPlayers}", new Vector2(-1f, 0f))) ActivateHostButton("maxplus");
        if (ImGui.Button("Start host match", new Vector2(-1f, 0f))) ActivateHostButton("hoststart");
        if (ImGui.Button("Back", new Vector2(-1f, 0f))) ActivateHostButton("back");

        ImGui.End();
    }

    private void DrawImGuiJoinSetup(ImDrawListPtr draw, Vector2 screen)
    {
        BeginModalWindow("Join Setup", screen, 560f, 380f);
        ImGui.Text("Join session");
        ImGui.Separator();
        ImGui.TextWrapped($"Target: {_joinAddress}:{_joinPort}");
        ImGui.Spacing();

        if (ImGui.Button($"Paste address ({_joinAddress})", new Vector2(-1f, 0f))) ActivateJoinButton("paste");
        if (ImGui.Button("Use localhost", new Vector2(-1f, 0f))) ActivateJoinButton("localhost");
        if (ImGui.Button($"Port -   {_joinPort}", new Vector2(-1f, 0f))) ActivateJoinButton("portminus");
        if (ImGui.Button($"Port +   {_joinPort}", new Vector2(-1f, 0f))) ActivateJoinButton("portplus");
        if (ImGui.Button("Connect and start", new Vector2(-1f, 0f))) ActivateJoinButton("joinstart");
        if (ImGui.Button("Back", new Vector2(-1f, 0f))) ActivateJoinButton("back");

        ImGui.End();
    }

    private void DrawImGuiPause(ImDrawListPtr draw, Vector2 screen)
    {
        BeginModalWindow("Paused", screen, 400f, 240f);
        ImGui.Text("Game paused");
        ImGui.Separator();
        if (ImGui.Button("Resume", new Vector2(-1f, 0f))) ActivatePausedButton("resume");
        if (ImGui.Button("Settings", new Vector2(-1f, 0f))) ActivatePausedButton("settings");
        if (ImGui.Button("Return to title", new Vector2(-1f, 0f))) ActivatePausedButton("title");
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
        ImGui.SetNextWindowPos(new Vector2((screen.X - 420f) * 0.5f, 16f), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(420f, 58f), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.92f);
        ImGui.Begin("##announcement", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        ImGui.TextColored(ToVec4(Player.Accent), "NOTICE");
        ImGui.SameLine();
        ImGui.TextWrapped(_announcement);
        ImGui.End();
    }

    private void DrawImGuiHud(ImDrawListPtr draw, Vector2 screen)
    {
        Player player = Player;
        List<InventoryViewSlot> inventorySlots = BuildInventorySlots(player);
        float leftW = 292f;
        float rightW = 292f;

        BeginOverlayWindow("##phase_panel", new Vector2((screen.X - 340f) * 0.5f, 14f), new Vector2(340f, 60f), 0.88f);
        ImGui.TextColored(ToVec4(_dayNight.IsNight ? Color.FromArgb(255, 194, 118) : Color.FromArgb(104, 220, 255)), _dayNight.IsNight ? $"Night wave {_waveNumber}" : "Day phase");
        ImGui.SameLine();
        ImGui.TextDisabled(FormatPhaseTimer(_dayNight.RemainingTime));
        ImGui.Separator();
        ImGui.TextDisabled(_nightWaveStarted ? "Combat live · keep breathing" : "Scavenge · repair · reload");
        ImGui.SameLine();
        ImGui.TextDisabled($"Players {_network.ConnectedPlayers}");
        ImGui.End();

        BeginOverlayWindow("##status_panel", new Vector2(14f, 14f), new Vector2(leftW, 166f), 0.88f);
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

        BeginOverlayWindow("##weapon_panel", new Vector2(screen.X - rightW - 14f, 14f), new Vector2(rightW, 178f), 0.88f);
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

        float slotWidth = Math.Clamp((screen.X - 236f) / 9f - 6f, 78f, 92f);
        float hotbarW = slotWidth * 9f + 8f * 8f + 18f;
        BeginOverlayWindow("##hotbar_panel", new Vector2((screen.X - hotbarW) * 0.5f, screen.Y - 116f), new Vector2(hotbarW, 100f), 0.93f);
        for (int displayIndex = 0; displayIndex < 9; displayIndex++)
        {
            if (displayIndex > 0)
            {
                ImGui.SameLine();
            }

            int rawIndex = _inventoryLayout[displayIndex];
            InventoryViewSlot slot = inventorySlots[Math.Clamp(rawIndex, 0, inventorySlots.Count - 1)];
            bool active = displayIndex == GetSelectedHotbarDisplayIndex();
            if (DrawInventoryTile($"hud_hotbar_{displayIndex}", slot, new Vector2(slotWidth, 60f), (displayIndex + 1).ToString(), active, false, true, false))
            {
                ActivateHotbarSlot(player, displayIndex, new Size((int)screen.X, (int)screen.Y));
            }
        }
        ImGui.Spacing();
        ImGui.TextDisabled("Hold LMB on a tile to drag · Tab bag · C craft · top strip mirrors the hotbar.");
        ImGui.End();

        if (_remotePlayers.Count > 0)
        {
            float remoteHeight = Math.Min(186f, 42f + _remotePlayers.Count * 34f);
            BeginOverlayWindow("##squad_panel", new Vector2(14f, screen.Y - remoteHeight - 126f), new Vector2(280f, remoteHeight), 0.84f);
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
            BeginOverlayWindow("##perf_panel", new Vector2(screen.X - 282f, screen.Y - 220f), new Vector2(268f, 188f), 0.82f);
            ImGui.Text($"FPS {_renderFps:0}  UPS {_updateFps:0}");
            ImGui.TextDisabled(_rendererLabel);
            ImGui.Text($"Update {_updateMs:0.00} ms  Render {_renderMs:0.00} ms");
            ImGui.PlotLines("##framems", ref graph[0], graph.Length, 0, null, 0f, 40f, new Vector2(0f, 82f));
            ImGui.End();
        }

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
            string value = state.Unlocked ? $"{state.AmmoInClip}/{state.AmmoReserve}" : $"LOCK {state.Definition.UnlockCost}cr";
            string desc = state.Unlocked
                ? $"{state.Definition.Name} ready. Put it on the hotbar strip or keep it in the backpack. Drag with held left mouse."
                : $"{state.Definition.Name} is locked. Buy it for {state.Definition.UnlockCost} credits when you can afford it.";
            slots.Add(new InventoryViewSlot(state.Definition.Name.ToUpper(), value, desc, GetWeaponColor(state.Definition), InventorySlotKind.Weapon, i, player.SelectedWeaponIndex == i));
        }

        slots.Add(new InventoryViewSlot("SCRAP", player.Scrap.ToString(), $"Core crafting material. Open the Craft menu with C to turn it into gear and support tools.", Color.FromArgb(118, 126, 138), InventorySlotKind.Scrap));
        slots.Add(new InventoryViewSlot("CREDITS", player.Credits.ToString(), "Spend credits on unlocks, upgrades and advanced crafting recipes.", Color.FromArgb(176, 130, 78), InventorySlotKind.Credits));
        slots.Add(new InventoryViewSlot("BARRICADE", player.BarricadeKits.ToString(), "Portable cover. Drop it in front of a lane to slow the dead down.", Color.FromArgb(150, 106, 70), InventorySlotKind.Barricade));
        slots.Add(new InventoryViewSlot("GRENADE", player.GrenadeCooldownTimer <= 0f ? "READY" : $"{player.GrenadeCooldownTimer:0.0}s", "Throwable explosive. Right mouse still works, but you can also bind or activate it from the hotbar.", Color.FromArgb(184, 128, 78), InventorySlotKind.Grenade));
        slots.Add(new InventoryViewSlot("TURRET", $"{player.TurretCharges}/{player.MaxTurretCharges}", "Deployable auto-gun. Keep at least one charge if you want emergency cover fire.", Color.FromArgb(76, 144, 182), InventorySlotKind.Turret));
        slots.Add(new InventoryViewSlot("OVERDRIVE", player.IsOverdriveActive ? "LIVE" : $"{(int)player.Adrenaline}%", "Combat burst mode. Charge it up, then shove it into a hotbar slot you can hit fast.", Color.FromArgb(214, 134, 72), InventorySlotKind.Overdrive, -1, player.IsOverdriveActive));

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

        Vector2 size = new Vector2(Math.Min(1140f, screen.X - 56f), Math.Min(680f, screen.Y - 54f));
        Vector2 pos = new Vector2((screen.X - size.X) * 0.5f, (screen.Y - size.Y) * 0.5f);
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.965f);
        ImGui.Begin("Inventory", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings);
        ImGui.Text(_inventoryViewMode == InventoryViewMode.Inventory ? "Inventory" : "Craft menu");
        ImGui.SameLine();
        ImGui.TextDisabled(_inventoryViewMode == InventoryViewMode.Inventory ? "clean slots · hold LMB to drag" : "recipes · quick support crafting");
        ImGui.Separator();

        float navWidth = 124f;
        ImGui.BeginChild("inv_nav", new Vector2(navWidth, 0f), ImGuiChildFlags.Borders);
        if (DrawInventoryModeButton("mode_inventory", "Inventory", InventorySlotKind.Weapon, Color.FromArgb(86, 128, 200), _inventoryViewMode == InventoryViewMode.Inventory))
        {
            _inventoryViewMode = InventoryViewMode.Inventory;
        }

        ImGui.Dummy(new Vector2(0f, 6f));

        if (DrawInventoryModeButton("mode_craft", "Craft", InventorySlotKind.Scrap, Color.FromArgb(164, 112, 72), _inventoryViewMode == InventoryViewMode.Craft))
        {
            _inventoryViewMode = InventoryViewMode.Craft;
        }

        ImGui.Separator();
        ImGui.TextDisabled("Resources");
        ImGui.Text($"Scrap {player.Scrap}");
        ImGui.Text($"Credits {player.Credits}");
        ImGui.Text($"Kits {player.BarricadeKits}");
        ImGui.Text($"Turrets {player.TurretCharges}/{player.MaxTurretCharges}");
        ImGui.Text($"Overdrive {(int)player.Adrenaline}%");
        ImGui.Separator();
        ImGui.TextDisabled("Keys");
        ImGui.TextDisabled("Tab = bag");
        ImGui.TextDisabled("C = craft");
        ImGui.TextDisabled("1-9 = hotbar");
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("inv_main", new Vector2(0f, 0f), ImGuiChildFlags.None);
        if (_inventoryViewMode == InventoryViewMode.Inventory)
        {
            DrawInventoryLoadoutPanel(screen, player, slots);
        }
        else
        {
            DrawCraftMenuPanel(player, recipes);
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
        float gridWidth = Math.Min(752f, ImGui.GetContentRegionAvail().X * 0.64f);
        ImGui.BeginChild("inv_grid", new Vector2(gridWidth, 0f), ImGuiChildFlags.Borders);
        ImGui.TextDisabled("Quick access");
        ImGui.SameLine();
        ImGui.TextDisabled("1-9 mirror the slot bar at the bottom.");
        ImGui.Spacing();

        float quickSpacing = 6f;
        float quickWidth = MathF.Floor((ImGui.GetContentRegionAvail().X - quickSpacing * 8f) / 9f);
        float quickHeight = 64f;
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

        ImGui.Dummy(new Vector2(0f, 10f));
        ImGui.Separator();
        ImGui.TextDisabled("Backpack");
        ImGui.SameLine();
        ImGui.TextDisabled("drag anything here, then drag it back whenever you want");
        ImGui.Spacing();

        const int backpackColumns = 6;
        float packSpacing = 8f;
        float packWidth = MathF.Floor((ImGui.GetContentRegionAvail().X - packSpacing * (backpackColumns - 1)) / backpackColumns);
        float packHeight = 76f;
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

        if (_inventoryDragRawIndex >= 0)
        {
            ImGui.Dummy(new Vector2(0f, 8f));
            int fromDisplay = _inventoryDragRawIndex + 1;
            string hoverText = _inventoryDragHoverRawIndex >= 0 ? $" -> {_inventoryDragHoverRawIndex + 1}" : string.Empty;
            ImGui.TextDisabled($"Dragging slot {fromDisplay}{hoverText}");
            ImGui.TextDisabled("Release over another tile to swap it. Empty slots work too.");
        }
        else
        {
            ImGui.Dummy(new Vector2(0f, 8f));
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
        int displaySlot = FindInventoryDisplayIndex(selectedRawIndex) + 1;

        ImGui.TextColored(ToVec4(selectedSlot.Accent), selectedSlot.Title);
        ImGui.SameLine();
        ImGui.TextDisabled(selectedSlot.Value);
        ImGui.SameLine();
        ImGui.TextDisabled($"Slot {displaySlot}");
        ImGui.Separator();
        ImGui.TextWrapped(selectedSlot.Description);
        ImGui.Spacing();

        if (selectedSlot.WeaponIndex >= 0)
        {
            WeaponState selectedWeapon = player.Arsenal[selectedSlot.WeaponIndex];
            ImGui.TextWrapped(selectedWeapon.Definition.Description);
            ImGui.Spacing();
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
                    ImGui.TextDisabled("Drop another tile here to keep the quickbar tidy.");
                    break;
                default:
                    ImGui.TextDisabled("This slot is passive. Move it wherever it feels clean.");
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
            }
        }

        ImGui.Separator();
        ImGui.TextDisabled("Tab = inventory · C = craft · 1-9 = hotbar · RMB still throws grenade.");
        ImGui.EndChild();
    }

    private List<CraftRecipeView> BuildCraftRecipes(Player player)
    {
        return new List<CraftRecipeView>
        {
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
        Color panel = slot.IsEmpty ? Color.FromArgb(86, 34, 40, 48) : Color.FromArgb(compact ? 118 : 102, Mix(accent, Color.Black, 0.42f));
        if (selected)
        {
            panel = Mix(panel, Color.White, 0.08f);
        }
        if (dragging)
        {
            panel = Mix(accent, Color.White, 0.24f);
        }

        float rounding = compact ? 10f : 12f;
        dl.AddRectFilled(min, max, ToU32(panel), rounding);
        dl.AddRect(min, max, ToU32(Color.FromArgb(selected ? 210 : 132, selected ? Mix(accent, Color.White, 0.55f) : Mix(accent, Color.White, 0.12f))), rounding, ImDrawFlags.None, selected ? 1.6f : 1.1f);
        if (dropTarget)
        {
            dl.AddRect(min + new Vector2(2f, 2f), max - new Vector2(2f, 2f), ToU32(Color.FromArgb(220, 132, 216, 255)), rounding - 2f, ImDrawFlags.None, 2f);
        }

        RectangleF iconRect = new RectangleF(min.X + 8f, min.Y + 8f, compact ? 18f : 20f, compact ? 18f : 20f);
        DrawInventoryIcon(dl, iconRect, slot);
        float textX = iconRect.Right + 8f;
        dl.AddText(new Vector2(min.X + 8f, min.Y + 4f), ToU32(Color.FromArgb(210, 180, 188, 196)), caption);
        dl.AddText(new Vector2(textX, min.Y + 8f), ToU32(Color.FromArgb(236, 236, 242, 246)), compact ? GetCompactSlotTitle(slot) : slot.Title);
        dl.AddText(new Vector2(textX, compact ? min.Y + 31f : min.Y + size.Y - 22f), ToU32(Color.FromArgb(214, 214, 221, 230)), compact ? GetCompactSlotValue(slot) : slot.Value);
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
        ImGui.SetNextWindowPos(new Vector2((screen.X - width) * 0.5f, (screen.Y - height) * 0.5f), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
        ImGui.Begin(title, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings);
    }

    private void BeginOverlayWindow(string id, Vector2 pos, Vector2 size, float alpha)
    {
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(alpha);
        ImGui.Begin(id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
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
