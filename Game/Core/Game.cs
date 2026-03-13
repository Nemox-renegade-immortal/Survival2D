using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace SlayInspiredPrototype;

public sealed partial class Game
{
    private enum GamePhase
    {
        Title,
        MultiplayerMenu,
        LanBrowser,
        Settings,
        HostSetup,
        JoinSetup,
        Playing,
        Paused,
        GameOver
    }

    private enum DifficultyMode
    {
        Casual,
        Survival,
        Nightmare
    }

    private enum UiScaleMode
    {
        Tiny,
        Compact,
        Normal
    }

    private sealed class RemoteActionTracker
    {
        public int ShotSequence;
        public int GrenadeSequence;
        public int TurretSequence;
        public int BarricadeSequence;
        public int OverdriveSequence;
        public int PickupSequence;
    }

    private bool _useFastUi = true;
    private float _smoothedRenderFps = 60f;
    private float _qualitySwitchCooldown;
    private Bitmap? _miniMapCache;
    private Size _miniMapCacheSize;
    private float _miniMapCacheTimer;
    private bool _inventoryOpen;
    private int _inventorySelectedIndex;
    private int _selectedHotbarIndex;
    private int _equippedSupportRawIndex = -1;
    private readonly int[] _inventoryLayout = new int[32];
    private Random _rng = new Random();
    private readonly Dictionary<int, RemoteActionTracker> _remoteActionTrackers = new Dictionary<int, RemoteActionTracker>();
    private readonly List<Player> _players = new List<Player>();
    private readonly List<RemotePlayerView> _remotePlayers = new List<RemotePlayerView>();
    private readonly List<Zombie> _zombies = new List<Zombie>();
    private int _nextZombieNetworkId = 1;
    private readonly List<Bullet> _bullets = new List<Bullet>();
    private readonly List<Bullet> _clientPredictedBullets = new List<Bullet>();
    private readonly List<Grenade> _grenades = new List<Grenade>();
    private readonly List<Explosion> _explosions = new List<Explosion>();
    private readonly List<Pickup> _pickups = new List<Pickup>();
    private readonly List<Turret> _turrets = new List<Turret>();
    private readonly List<ScrapPile> _scrapPiles = new List<ScrapPile>();
    private readonly List<LootCrate> _lootCrates = new List<LootCrate>();
    private readonly List<MenuButton> _menuButtons = new List<MenuButton>();
    private readonly SoundManager _sound = new SoundManager();
    private readonly NetworkManager _network = new NetworkManager();
    private readonly DayNightCycle _dayNight = new DayNightCycle();
    private RectangleF _worldDrawBounds;
    private readonly Font _titleFont = new Font("Segoe UI", 24f, FontStyle.Bold);
    private readonly Font _menuFont = new Font("Segoe UI", 13f, FontStyle.Bold);
    private readonly Font _hudFont = new Font("Segoe UI", 9f, FontStyle.Bold);
    private readonly Font _smallFont = new Font("Segoe UI", 8f, FontStyle.Regular);
    private readonly Font _tinyFont = new Font("Consolas", 7.5f, FontStyle.Regular);
    private bool _showFpsHud = false;
    private bool _showPerfHud = false;
    private float _updateFps;
    private float _renderFps;
    private float _updateMs;
    private float _renderMs;
    private float _frameMs;
    private string _rendererLabel = "GDI+ CPU renderer";
    private readonly float[] _frameGraph = new float[100];
    private int _frameGraphHead;
    private GamePhase _phase = GamePhase.Title;
    private DifficultyMode _difficulty = DifficultyMode.Survival;
    private UiScaleMode _uiScale = UiScaleMode.Normal;

    private int _score;
    private int _bestScore;
    private int _waveNumber;
    private bool _waveLive;
    private int _waveSpawned;
    private int _waveTarget;
    private float _waveSpawnTimer;
    private float _waveIntermission = 1.5f;
    private float _survivalTime;
    private int _hoverButtonIndex = -1;
    private string _announcement = "Ready.";
    private float _announcementTimer;
    private string _joinAddress = "127.0.0.1";
    private int _joinPort = 7777;
    private int _maxPlayers = 64;
    private bool _showHints = true;
    private bool _shuffleArenaOnStart = true;
    private bool _showNetworkDebug = false;
    private int _activeMapSeed;
    private float _backgroundPulse;
    private int _scrapCraftCost = 4;
    private bool _nightWaveStarted;
    private bool _nightRewardGranted;
    private float _crateRespawnTimer;
    private float _screenShakeTimer;
    private float _screenShakePower;
    private float _bloodOverlay;
    private float _damagePulse;
    private Vector2 _damageKick;

    private static Color Mix(Color a, Color b, float t)
    {
        t = MathF.Max(0f, MathF.Min(1f, t));
        int aa = (int)(a.A + (b.A - a.A) * t);
        int rr = (int)(a.R + (b.R - a.R) * t);
        int gg = (int)(a.G + (b.G - a.G) * t);
        int bb = (int)(a.B + (b.B - a.B) * t);
        return Color.FromArgb(aa, rr, gg, bb);
    }

    private void ResetInventoryLayout()
    {
        int[] defaults = new[] { 0, 10, 11, 12, 4, 6, 7, 8, 9, 1, 2, 3, 5 };
        for (int i = 0; i < _inventoryLayout.Length; i++)
        {
            _inventoryLayout[i] = i < defaults.Length ? defaults[i] : i;
        }
    }

    private int FindInventoryDisplayIndex(int rawIndex)
    {
        for (int i = 0; i < _inventoryLayout.Length; i++)
        {
            if (_inventoryLayout[i] == rawIndex)
            {
                return i;
            }
        }

        return Math.Clamp(rawIndex, 0, _inventoryLayout.Length - 1);
    }

    private int GetHotbarRawIndex(int slotIndex)
    {
        int displayIndex = Math.Clamp(slotIndex, 0, Math.Min(8, _inventoryLayout.Length - 1));
        return _inventoryLayout[displayIndex];
    }

    private void SetSelectedHotbarIndex(int slotIndex)
    {
        _selectedHotbarIndex = Math.Clamp(slotIndex, 0, 8);
        _inventorySelectedIndex = Math.Clamp(GetHotbarRawIndex(_selectedHotbarIndex), 0, _inventoryLayout.Length - 1);
    }

    private void SyncSelectedHotbarToWeapon(Player player)
    {
        _equippedSupportRawIndex = -1;
        int displayIndex = FindInventoryDisplayIndex(player.SelectedWeaponIndex);
        if (displayIndex >= 0 && displayIndex < 9)
        {
            SetSelectedHotbarIndex(displayIndex);
        }
    }

    private static bool IsSupportRawIndex(int rawIndex)
    {
        return rawIndex >= 6 && rawIndex <= 9;
    }

    private static bool IsWeaponRawIndex(int rawIndex)
    {
        return rawIndex >= 0 && rawIndex < Player.ActiveWeaponSlots;
    }

    private int GetSelectedHotbarDisplayIndex()
    {
        return Math.Clamp(_selectedHotbarIndex, 0, 8);
    }

    private int GetSelectedHotbarRawIndex()
    {
        return GetHotbarRawIndex(GetSelectedHotbarDisplayIndex());
    }

    private string GetHotbarSlotTitle(Player player, int rawIndex)
    {
        return rawIndex switch
        {
            >= 0 and < 4 => player.Arsenal[rawIndex].Definition.Name,
            4 => "Scrap pack",
            5 => "Credits",
            6 => "Barricade kit",
            7 => "Grenade",
            8 => "Turret",
            9 => player.IsOverdriveActive ? "Overdrive LIVE" : "Overdrive cell",
            _ => "Empty slot"
        };
    }

    private string GetHotbarSlotShortTitle(Player player, int rawIndex)
    {
        return rawIndex switch
        {
            >= 0 and < 4 => GetWeaponShortName(player.Arsenal[rawIndex].Definition),
            4 => "SCR",
            5 => "CR",
            6 => "BAR",
            7 => "GRN",
            8 => "TUR",
            9 => "OVR",
            _ => "---"
        };
    }

    private string GetHotbarSlotValue(Player player, int rawIndex)
    {
        return rawIndex switch
        {
            >= 0 and < 4 => player.Arsenal[rawIndex].Unlocked ? $"{player.Arsenal[rawIndex].AmmoInClip}/{player.Arsenal[rawIndex].AmmoReserve}" : "LOCK",
            4 => player.Scrap.ToString(),
            5 => player.Credits.ToString(),
            6 => player.BarricadeKits.ToString(),
            7 => player.GrenadeCooldownTimer <= 0f ? "READY" : $"{MathF.Ceiling(player.GrenadeCooldownTimer)}s",
            8 => $"{player.TurretCharges}/{player.MaxTurretCharges}",
            9 => player.IsOverdriveActive ? "LIVE" : $"{(int)player.Adrenaline}%",
            _ => string.Empty
        };
    }

    private Color GetHotbarSlotAccent(Player player, int rawIndex)
    {
        return rawIndex switch
        {
            >= 0 and < 4 => GetWeaponColor(player.Arsenal[rawIndex].Definition),
            4 => Color.FromArgb(92, 100, 110),
            5 => Color.FromArgb(176, 130, 78),
            6 => Color.FromArgb(106, 76, 48),
            7 => Color.FromArgb(134, 80, 48),
            8 => Color.FromArgb(50, 96, 112),
            9 => Color.FromArgb(214, 134, 72),
            _ => Color.FromArgb(84, 92, 102)
        };
    }

    private bool IsSupportEquipped()
    {
        return IsSupportRawIndex(_equippedSupportRawIndex);
    }

    private bool TryUseEquippedSupport(Player player, Size clientSize)
    {
        if (!IsSupportEquipped())
        {
            return false;
        }

        return ActivateInventoryRawSlot(player, _equippedSupportRawIndex, clientSize);
    }

    private bool SelectHotbarSlot(Player player, int slotIndex, Size clientSize, bool activateSupport)
    {
        int displayIndex = Math.Clamp(slotIndex, 0, Math.Min(8, _inventoryLayout.Length - 1));
        SetSelectedHotbarIndex(displayIndex);
        int rawIndex = GetHotbarRawIndex(displayIndex);
        if (rawIndex >= 0 && rawIndex < 4)
        {
            _equippedSupportRawIndex = -1;
            if (player.TrySelectWeapon(rawIndex))
            {
                _inventoryUpgradeWeaponIndex = rawIndex;
                return true;
            }

            SetAnnouncement("Weapon locked", 0.7f);
            return false;
        }

        if (IsSupportRawIndex(rawIndex))
        {
            _equippedSupportRawIndex = rawIndex;
            player.UseAnimation = MathF.Max(player.UseAnimation, 0.2f);
            return activateSupport ? ActivateInventoryRawSlot(player, rawIndex, clientSize) : true;
        }

        _equippedSupportRawIndex = -1;
        return activateSupport && ActivateInventoryRawSlot(player, rawIndex, clientSize);
    }

    private void StepHotbarSelection(Player player, int direction, Size clientSize)
    {
        int step = Math.Sign(direction);
        if (step == 0)
        {
            return;
        }

        int next = (_selectedHotbarIndex + step + 9) % 9;
        SelectHotbarSlot(player, next, clientSize, false);
    }

    private void SwapInventorySlots(int rawA, int rawB)
    {
        if (rawA == rawB)
        {
            return;
        }

        int indexA = FindInventoryDisplayIndex(rawA);
        int indexB = FindInventoryDisplayIndex(rawB);
        (_inventoryLayout[indexA], _inventoryLayout[indexB]) = (_inventoryLayout[indexB], _inventoryLayout[indexA]);
    }

    private static Color WithAlpha(Color color, int alpha)
    {
        return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), color);
    }

    private RectangleF BuildWorldDrawBounds(Size clientSize, float margin = 96f)
    {
        return new RectangleF(
            Camera.X - margin,
            Camera.Y - margin,
            clientSize.Width + margin * 2f,
            clientSize.Height + margin * 2f);
    }

    private bool IsVisible(Vector2 position, float radius)
    {
        return position.X + radius >= _worldDrawBounds.Left &&
               position.X - radius <= _worldDrawBounds.Right &&
               position.Y + radius >= _worldDrawBounds.Top &&
               position.Y - radius <= _worldDrawBounds.Bottom;
    }

    private bool IsVisible(RectangleF rect)
    {
        return rect.Right >= _worldDrawBounds.Left &&
               rect.Left <= _worldDrawBounds.Right &&
               rect.Bottom >= _worldDrawBounds.Top &&
               rect.Top <= _worldDrawBounds.Bottom;
    }

    private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        if (radius <= 1f)
        {
            path.AddRectangle(rect);
            path.CloseFigure();
            return path;
        }

        float diameter = radius * 2f;
        RectangleF arc = new RectangleF(rect.X, rect.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private bool UseFastUiForRect(RectangleF rect)
    {
        return _useFastUi || rect.Width < 64f || rect.Height < 24f;
    }

    private void RebuildMiniMapCache(SizeF size)
    {
        int width = Math.Max(1, (int)MathF.Ceiling(size.Width));
        int height = Math.Max(1, (int)MathF.Ceiling(size.Height));

        if (_miniMapCache is not null && _miniMapCacheSize.Width == width && _miniMapCacheSize.Height == height && _miniMapCacheTimer > 0f)
        {
            return;
        }

        _miniMapCache?.Dispose();
        _miniMapCache = new Bitmap(width, height);
        _miniMapCacheSize = new Size(width, height);
        _miniMapCacheTimer = 0.2f;

        using Graphics mg = Graphics.FromImage(_miniMapCache);
        mg.SmoothingMode = SmoothingMode.None;
        mg.PixelOffsetMode = PixelOffsetMode.HighSpeed;
        mg.InterpolationMode = InterpolationMode.NearestNeighbor;
        mg.CompositingQuality = CompositingQuality.HighSpeed;
        mg.Clear(Color.FromArgb(10, 12, 16));

        using SolidBrush wallBrush = new SolidBrush(Color.FromArgb(150, 82, 90, 102));
        using SolidBrush barricadeBrush = new SolidBrush(Color.FromArgb(170, 110, 76, 46));

        float sx = width / (float)Map.Width;
        float sy = height / (float)Map.Height;

        for (int x = 0; x < Map.Width; x++)
        {
            for (int y = 0; y < Map.Height; y++)
            {
                int value = Map.GetTileValue(x, y);
                if (value == 0)
                {
                    continue;
                }

                mg.FillRectangle(value == 1 ? wallBrush : barricadeBrush, x * sx, y * sy, Math.Max(1f, sx), Math.Max(1f, sy));
            }
        }
    }

    private RectangleF GetMenuButtonsBounds(float padX = 18f, float padY = 18f)
    {
        if (_menuButtons.Count == 0)
        {
            return new RectangleF(0f, 0f, 0f, 0f);
        }

        RectangleF bounds = _menuButtons[0].Rect;
        for (int i = 1; i < _menuButtons.Count; i++)
        {
            bounds = RectangleF.Union(bounds, _menuButtons[i].Rect);
        }

        return new RectangleF(bounds.X - padX, bounds.Y - padY, bounds.Width + padX * 2f, bounds.Height + padY * 2f);
    }

    private Color GetMenuButtonAccent(MenuButton button, bool hovered)
    {
        Color baseColor = button.Id switch
        {
            "solo" or "hoststart" or "joinstart" or "resume" or "retry" => Player.Accent,
            "settings" or "difficulty" or "uiscale" or "hints" or "audio" or "netdebug" or "fpshud" or "perfhud" => Color.FromArgb(66, 116, 146),
            "shuffle" or "paste" or "localhost" => Color.FromArgb(72, 124, 94),
            "portminus" or "portplus" or "maxminus" or "maxplus" => Color.FromArgb(132, 96, 58),
            "title" or "back" => Color.FromArgb(84, 92, 108),
            _ => Color.FromArgb(76, 90, 108)
        };

        if (!button.Enabled)
        {
            baseColor = Mix(baseColor, Color.FromArgb(42, 46, 52), 0.55f);
        }

        return hovered ? Mix(baseColor, Color.White, 0.08f) : baseColor;
    }

    public void SetRuntimeStats(float updateFps, float renderFps, float updateMs, float renderMs, float frameMs, string rendererLabel)
    {
        _updateFps = MathF.Max(0f, updateFps);
        _renderFps = MathF.Max(0f, renderFps);
        _updateMs = MathF.Max(0f, updateMs);
        _renderMs = MathF.Max(0f, renderMs);
        _frameMs = MathF.Max(0f, frameMs);
        _rendererLabel = string.IsNullOrWhiteSpace(rendererLabel) ? "GDI+ CPU renderer" : rendererLabel;
        _frameGraph[_frameGraphHead] = _frameMs;
        _frameGraphHead = (_frameGraphHead + 1) % _frameGraph.Length;
        _useFastUi = true;
    }

    private void DrawMenuCard(Graphics g, RectangleF rect, Color accent, string badge)
    {
        DrawGlassPanel(g, rect, 22f, accent, 214);

        RectangleF topLine = new RectangleF(rect.X + 14f, rect.Y + 30f, rect.Width - 28f, 2f);
        using SolidBrush line = new SolidBrush(Color.FromArgb(120, accent));
        g.FillRectangle(line, topLine);

        if (!string.IsNullOrEmpty(badge))
        {
            DrawLabelPill(g, badge, _tinyFont, new RectangleF(rect.X + 14f, rect.Y + 8f, 90f, 16f), accent, Color.WhiteSmoke);
        }
    }

    private void DrawShadow(Graphics g, RectangleF rect, float radius, int alpha, float offsetY = 4f)
    {
        if (alpha <= 0 || rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        RectangleF shadowRect = new RectangleF(rect.X, rect.Y + offsetY, rect.Width, rect.Height);
        bool fast = UseFastUiForRect(shadowRect);

        using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(fast ? Math.Min(alpha, 22) : alpha, 0, 0, 0));

        if (fast)
        {
            g.FillRectangle(shadowBrush, shadowRect.X, shadowRect.Y, shadowRect.Width, shadowRect.Height);
            return;
        }

        using GraphicsPath shadowPath = CreateRoundedPath(shadowRect, radius);
        g.FillPath(shadowBrush, shadowPath);
    }

    private void DrawGlassPanel(Graphics g, RectangleF rect, float radius, Color accent, int alpha = 205)
    {
        bool fast = UseFastUiForRect(rect);

        DrawShadow(g, rect, radius, fast ? 18 : 40, fast ? 2f : 4f);

        using SolidBrush fill = new SolidBrush(Color.FromArgb(alpha, 18, 22, 28));
        using Pen border = new Pen(Color.FromArgb(fast ? 54 : 84, 255, 255, 255), 1f);
        RectangleF accentRect = new RectangleF(rect.X + 1f, rect.Y + 1f, rect.Width - 2f, MathF.Min(fast ? 6f : 10f, rect.Height * 0.16f));
        using SolidBrush accentBrush = new SolidBrush(Color.FromArgb(fast ? 118 : 150, Mix(accent, Color.White, 0.06f)));
        using Pen accentLine = new Pen(Color.FromArgb(fast ? 180 : 220, accent), fast ? 1f : 2f);

        if (fast)
        {
            g.FillRectangle(fill, rect.X, rect.Y, rect.Width, rect.Height);
            g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);

            if (accentRect.Width > 0f && accentRect.Height > 0f)
            {
                g.FillRectangle(accentBrush, accentRect.X, accentRect.Y, accentRect.Width, accentRect.Height);
            }

            g.DrawLine(accentLine, rect.X + 3f, rect.Y + 1.5f, rect.Right - 3f, rect.Y + 1.5f);
            return;
        }

        using GraphicsPath path = CreateRoundedPath(rect, radius);
        g.FillPath(fill, path);
        g.DrawPath(border, path);

        using GraphicsPath accentPath = CreateRoundedPath(accentRect, Math.Max(4f, radius - 2f));
        g.FillPath(accentBrush, accentPath);
        g.DrawLine(accentLine, rect.X + radius * 0.55f, rect.Y + 1.5f, rect.Right - radius * 0.55f, rect.Y + 1.5f);
    }

    private void DrawBar(Graphics g, RectangleF rect, float value, Color fillColor, string label, string valueText)
    {
        value = MathF.Max(0f, MathF.Min(1f, value));
        bool fast = _useFastUi || rect.Width < 96f || rect.Height < 14f;

        DrawShadow(g, rect, 7f, fast ? 10 : 24, 2f);

        using SolidBrush bg = new SolidBrush(Color.FromArgb(200, 26, 30, 36));
        using Pen border = new Pen(Color.FromArgb(80, 255, 255, 255));

        if (fast)
        {
            g.FillRectangle(bg, rect.X, rect.Y, rect.Width, rect.Height);
            g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);

            float fillWidth = Math.Max(0f, (rect.Width - 4f) * value);
            if (fillWidth > 0.5f)
            {
                using SolidBrush fill = new SolidBrush(Color.FromArgb(215, fillColor));
                g.FillRectangle(fill, rect.X + 2f, rect.Y + 2f, fillWidth, Math.Max(1f, rect.Height - 4f));
            }
        }
        else
        {
            using GraphicsPath bgPath = CreateRoundedPath(rect, 7f);
            g.FillPath(bg, bgPath);
            g.DrawPath(border, bgPath);

            float fillWidth = Math.Max(0f, (rect.Width - 4f) * value);
            if (fillWidth > 0.5f)
            {
                RectangleF fillRect = new RectangleF(rect.X + 2f, rect.Y + 2f, fillWidth, rect.Height - 4f);
                using GraphicsPath fillPath = CreateRoundedPath(fillRect, 5f);
                using SolidBrush fill = new SolidBrush(Color.FromArgb(228, fillColor));
                g.FillPath(fill, fillPath);
            }
        }

        if (!string.IsNullOrEmpty(label))
        {
            g.DrawString(label, _tinyFont, Brushes.WhiteSmoke, rect.X + 6f, rect.Y + 1f);
        }

        if (!string.IsNullOrEmpty(valueText))
        {
            SizeF valueSize = g.MeasureString(valueText, _tinyFont);
            g.DrawString(valueText, _tinyFont, Brushes.Gainsboro, rect.Right - valueSize.Width - 6f, rect.Y + 1f);
        }
    }

    private void DrawTagBadge(Graphics g, RectangleF rect, string text, Color accent)
    {
        using SolidBrush fill = new SolidBrush(Color.FromArgb(220, Mix(accent, Color.Black, 0.15f)));
        using Pen border = new Pen(Color.FromArgb(60, 255, 255, 255));

        if (UseFastUiForRect(rect))
        {
            g.FillRectangle(fill, rect.X, rect.Y, rect.Width, rect.Height);
            g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
        }
        else
        {
            using GraphicsPath path = CreateRoundedPath(rect, rect.Height / 2f);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        SizeF size = g.MeasureString(text, _tinyFont);
        g.DrawString(text, _tinyFont, Brushes.White, rect.X + (rect.Width - size.Width) / 2f, rect.Y + (rect.Height - size.Height) / 2f - 1f);
    }

    private void DrawLabelPill(Graphics g, string text, Font font, RectangleF rect, Color accent, Color textColor)
    {
        DrawGlassPanel(g, rect, rect.Height / 2f, accent, _useFastUi ? 188 : 210);
        SizeF size = g.MeasureString(text, font);
        using SolidBrush textBrush = new SolidBrush(textColor);
        g.DrawString(text, font, textBrush, rect.X + (rect.Width - size.Width) / 2f, rect.Y + (rect.Height - size.Height) / 2f - 1f);
    }

    private string GetWeaponShortName(Weapon weapon)
    {
        return weapon.Name switch
        {
            "Pistol" => "PST",
            "Shotgun" => "SG",
            "Carbine" => "CB",
            "SMG" => "SMG",
            _ => weapon.Name.Length <= 3 ? weapon.Name.ToUpper() : weapon.Name.Substring(0, 3).ToUpper()
        };
    }

    private Color GetWeaponColor(Weapon weapon)
    {
        return weapon.Name switch
        {
            "Pistol" => Color.FromArgb(126, 176, 255),
            "SMG" => Color.FromArgb(98, 230, 196),
            "Shotgun" => Color.FromArgb(255, 174, 84),
            "Carbine" => Color.FromArgb(202, 134, 255),
            _ => Color.SlateGray
        };
    }

    private Point _lastMouseScreen;

    public TileMap Map { get; }
    public Player Player => _players.Count > 0 ? _players[0] : new Player(new Vector2(160f, 160f), "P1", Color.Cyan);
    public Vector2 Camera { get; private set; }
    public int WaveNumber => _waveNumber;

    public Game()
    {
        _activeMapSeed = unchecked((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));
        if (_activeMapSeed == 0)
        {
            _activeMapSeed = 1;
        }

        _rng = new Random(_activeMapSeed);
        Map = new TileMap(42, 26, _rng);
        ResetToTitle();
    }

    private void RebuildArenaForCurrentMode()
    {
        if (_network.Mode != NetMode.None)
        {
            _activeMapSeed = _network.MapSeed != 0 ? _network.MapSeed : 1;
            _rng = new Random(_activeMapSeed);
            Map.Rebuild(_rng);
            return;
        }

        if (_shuffleArenaOnStart)
        {
            _activeMapSeed = unchecked((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));
            if (_activeMapSeed == 0)
            {
                _activeMapSeed = 1;
            }

            _rng = new Random(_activeMapSeed);
            Map.Rebuild(_rng);
        }
    }

    public void ResetToTitle()
    {
        _phase = GamePhase.Title;
        _score = 0;
        _waveNumber = 0;
        _inventoryOpen = false;
        _inventorySelectedIndex = 0;
        _selectedHotbarIndex = 0;
        _inventoryDragRawIndex = -1;
        _inventoryDragHoverRawIndex = -1;
        ResetInventoryLayout();
        _waveLive = false;
        _waveSpawned = 0;
        _waveTarget = 0;
        _waveSpawnTimer = 0f;
        _waveIntermission = 1.3f;
        _survivalTime = 0f;
        _announcementTimer = 0f;
        _hoverButtonIndex = -1;
        _nightWaveStarted = false;
        _nightRewardGranted = false;
        _dayNight.Reset(true);
        ClearRuntime();
        _network.Stop();
        SetupPlayers("P1", Color.FromArgb(92, 220, 255));
        RebuildArenaForCurrentMode();
        SeedScavenge();
        SetAnnouncement("Cursor-driven menu online", 1.8f);
    }

    private void RestartRun(string callsign, Color accent)
    {
        ClearRuntime();
        _score = 0;
        _waveNumber = 0;
        _waveLive = false;
        _inventoryOpen = false;
        _inventorySelectedIndex = 0;
        _selectedHotbarIndex = 0;
        _inventoryDragRawIndex = -1;
        _inventoryDragHoverRawIndex = -1;
        ResetInventoryLayout();
        _waveSpawned = 0;
        _waveTarget = 0;
        _waveSpawnTimer = 0f;
        _waveIntermission = 1.1f;
        _survivalTime = 0f;
        _nightWaveStarted = false;
        _nightRewardGranted = false;
        _dayNight.Reset(true);
        SetupPlayers(callsign, accent);
        RebuildArenaForCurrentMode();

        if (_network.Mode != NetMode.Client)
        {
            SeedScavenge();
        }

        _phase = GamePhase.Playing;
        SetAnnouncement(_network.Mode switch
        {
            NetMode.Host => $"Host simulation armed · seed {_activeMapSeed}",
            NetMode.Client => $"Client simulation armed · seed {_activeMapSeed}",
            _ => "Solo run armed"
        }, 1.8f);
        _sound.PlayUi(UiSound.Start);
    }

    private void SetupPlayers(string callsign, Color accent)
    {
        _players.Clear();
        _players.Add(new Player(new Vector2(160f, 160f), callsign, accent));
    }

    private int AllocateZombieNetworkId()
    {
        int id = Math.Max(1, _nextZombieNetworkId);
        _nextZombieNetworkId = id >= int.MaxValue - 1 ? 1 : id + 1;
        return id;
    }

    private Zombie CreateTrackedZombie(ZombieKind kind, Vector2 position, int wave)
    {
        Zombie zombie = Zombie.Create(kind, position, wave);
        zombie.NetworkId = AllocateZombieNetworkId();
        return zombie;
    }

    private void ClearRuntime()
    {
        _remotePlayers.Clear();
        _zombies.Clear();
        _bullets.Clear();
        _clientPredictedBullets.Clear();
        _grenades.Clear();
        _explosions.Clear();
        _pickups.Clear();
        _turrets.Clear();
        _scrapPiles.Clear();
        _lootCrates.Clear();
        _remoteActionTrackers.Clear();
        _nextZombieNetworkId = 1;
        _crateRespawnTimer = 10f;
        _screenShakeTimer = 0f;
        _screenShakePower = 0f;
        _bloodOverlay = 0f;
        _damagePulse = 0f;
        _damageKick = Vector2.Zero;
    }

    private void SeedScavenge()
    {
        for (int i = 0; i < 8; i++)
        {
            _scrapPiles.Add(new ScrapPile(Map.GetRandomFreePoint(_rng), 2 + _rng.Next(3)));
        }

        SpawnLootCrates(6);
        _crateRespawnTimer = 8f + (float)_rng.NextDouble() * 6f;

        for (int i = 0; i < 3; i++)
        {
            _pickups.Add(new Pickup(PickupType.Ammo, Map.GetRandomFreePoint(_rng), 1));
        }

        _pickups.Add(new Pickup(PickupType.Medkit, Map.GetRandomFreePoint(_rng), 24));
        _pickups.Add(new Pickup(PickupType.Armor, Map.GetRandomFreePoint(_rng), 18));
    }

    private void SpawnLootCrates(int count)
    {
        int spawned = 0;
        int attempts = Math.Max(12, count * 24);
        for (int i = 0; i < attempts && spawned < count; i++)
        {
            Vector2 position = Map.GetRandomFreePoint(_rng) + new Vector2(_rng.Next(-10, 11), _rng.Next(-10, 11));
            if (TryAddLootCrate(position, 34 + _rng.Next(16)))
            {
                spawned++;
            }
        }
    }

    private bool TryAddLootCrate(Vector2 position, int health)
    {
        const float radius = 18f;
        if (Map.CollidesCircle(position, radius))
        {
            return false;
        }

        foreach (Player player in _players)
        {
            if (Vector2.DistanceSquared(player.Position, position) < 110f * 110f)
            {
                return false;
            }
        }

        foreach (LootCrate crate in _lootCrates)
        {
            float rr = crate.Radius + radius + 22f;
            if (Vector2.DistanceSquared(crate.Position, position) < rr * rr)
            {
                return false;
            }
        }

        _lootCrates.Add(new LootCrate(position, health, radius));
        return true;
    }

    private void UpdateLootCrates(float dt)
    {
        for (int i = _lootCrates.Count - 1; i >= 0; i--)
        {
            LootCrate crate = _lootCrates[i];
            crate.Update(dt);
            if (crate.Destroyed && crate.HitFlash <= 0f)
            {
                _lootCrates.RemoveAt(i);
            }
        }
    }

    private void DamageLootCrate(LootCrate crate, int damage)
    {
        if (crate.Destroyed)
        {
            return;
        }

        bool destroyed = crate.ApplyDamage(damage);
        _sound.PlayWorld(destroyed ? WorldSound.Barricade : WorldSound.Hit, Player.Position, crate.Position, 480f, true);

        if (!destroyed)
        {
            return;
        }

        int scrapDrops = 2 + _rng.Next(3);
        for (int i = 0; i < scrapDrops; i++)
        {
            Vector2 dir = Phys.FromAngle((float)(_rng.NextDouble() * Math.PI * 2.0));
            Vector2 drop = crate.Position + dir * (8f + (float)_rng.NextDouble() * 16f);
            if (!Map.CollidesCircle(drop, 10f))
            {
                _scrapPiles.Add(new ScrapPile(drop, 1 + _rng.Next(2)));
            }
        }

        if (_rng.NextDouble() < 0.42)
        {
            _pickups.Add(new Pickup(PickupType.Credits, crate.Position + new Vector2(_rng.Next(-12, 13), _rng.Next(-12, 13)), 6 + _rng.Next(8)));
        }

        if (_rng.NextDouble() < 0.26)
        {
            _pickups.Add(new Pickup(PickupType.Ammo, crate.Position + new Vector2(_rng.Next(-10, 11), _rng.Next(-10, 11)), 1));
        }

        if (_rng.NextDouble() < 0.16)
        {
            _pickups.Add(new Pickup(PickupType.Medkit, crate.Position + new Vector2(_rng.Next(-8, 9), _rng.Next(-8, 9)), 12 + _rng.Next(8)));
        }
    }

    private void UpdateHitFeedback(float dt)
    {
        _crateRespawnTimer = Math.Max(0f, _crateRespawnTimer - dt);
        _screenShakeTimer = Math.Max(0f, _screenShakeTimer - dt);
        _screenShakePower = Math.Max(0f, _screenShakePower - dt * 26f);
        _bloodOverlay = Math.Max(0f, _bloodOverlay - dt * 0.22f);
        _damagePulse = Math.Max(0f, _damagePulse - dt * 1.8f);
        _damageKick *= MathF.Max(0f, 1f - dt * 9f);

        if ((_phase == GamePhase.Playing || _phase == GamePhase.Paused || _phase == GamePhase.GameOver) && !_dayNight.IsNight && _crateRespawnTimer <= 0f && _lootCrates.Count < 10)
        {
            SpawnLootCrates(1);
            _crateRespawnTimer = 12f + (float)_rng.NextDouble() * 8f;
        }
    }

    private void TriggerPlayerHitFeedback(int damage, Vector2 sourcePosition, bool heavy = false)
    {
        if (_players.Count == 0)
        {
            return;
        }

        float intensity = Math.Clamp(damage / 28f, 0.08f, heavy ? 0.75f : 0.45f);
        _screenShakeTimer = Math.Max(_screenShakeTimer, 0.12f + intensity * 0.22f);
        _screenShakePower = Math.Max(_screenShakePower, 4f + damage * 0.18f + (heavy ? 4f : 0f));
        _bloodOverlay = Math.Min(0.92f, _bloodOverlay + intensity * (heavy ? 0.82f : 0.48f));
        _damagePulse = Math.Max(_damagePulse, 0.2f + intensity * 0.45f);

        Vector2 dir = Phys.NormalizeSafe(Player.Position - sourcePosition);
        if (dir.LengthSquared() > 0.0001f)
        {
            _damageKick = dir * (10f + damage * 0.28f);
        }
    }

    private bool TryCraftBarricade(Player player)
    {
        if (player.TryCraftBarricadeKit(_scrapCraftCost))
        {
            player.NotifyPickup();
            SetAnnouncement("Barricade kit crafted", 0.9f);
            _sound.PlayWorld(WorldSound.Barricade, player.Position, player.Position, 64f, false);
            return true;
        }

        SetAnnouncement($"Need {_scrapCraftCost} scrap", 0.9f);
        _sound.PlayUi(UiSound.Error);
        return false;
    }

    private bool TrySpendCraftResources(Player player, int scrapCost, int creditCost, string failText)
    {
        if (player.Scrap < scrapCost)
        {
            SetAnnouncement($"Need {scrapCost} scrap", 0.9f);
            _sound.PlayUi(UiSound.Error);
            return false;
        }

        if (player.Credits < creditCost)
        {
            SetAnnouncement($"Need {creditCost} credits", 0.9f);
            _sound.PlayUi(UiSound.Error);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(failText))
        {
            SetAnnouncement(failText, 0.9f);
        }

        player.Scrap -= scrapCost;
        player.Credits -= creditCost;
        return true;
    }

    private bool TryCraftAmmoCache(Player player)
    {
        if (!TrySpendCraftResources(player, 3, 0, string.Empty))
        {
            return false;
        }

        player.GiveAmmoForAllWeapons();
        player.NotifyPickup();
        SetAnnouncement("Ammo cache packed", 0.9f);
        _sound.PlayWorld(WorldSound.Pickup, player.Position, player.Position, 80f, false);
        return true;
    }

    private bool TryCraftArmorPlate(Player player)
    {
        if (player.Armor >= 100)
        {
            SetAnnouncement("Armor already full", 0.8f);
            _sound.PlayUi(UiSound.Error);
            return false;
        }

        if (!TrySpendCraftResources(player, 2, 8, string.Empty))
        {
            return false;
        }

        player.GiveArmor(24);
        player.NotifyPickup();
        SetAnnouncement("Armor plate patched", 0.9f);
        _sound.PlayWorld(WorldSound.Pickup, player.Position, player.Position, 80f, false);
        return true;
    }

    private bool TryCraftMedPatch(Player player)
    {
        if (player.Health >= player.MaxHealth)
        {
            SetAnnouncement("Health already full", 0.8f);
            _sound.PlayUi(UiSound.Error);
            return false;
        }

        if (!TrySpendCraftResources(player, 2, 12, string.Empty))
        {
            return false;
        }

        player.Heal(32);
        player.NotifyPickup();
        SetAnnouncement("Med patch applied", 0.9f);
        _sound.PlayWorld(WorldSound.Pickup, player.Position, player.Position, 80f, false);
        return true;
    }

    private bool TryCraftTurretBattery(Player player)
    {
        if (player.TurretCharges >= player.MaxTurretCharges)
        {
            SetAnnouncement("Turret charge already full", 0.8f);
            _sound.PlayUi(UiSound.Error);
            return false;
        }

        if (!TrySpendCraftResources(player, 6, 22, string.Empty))
        {
            return false;
        }

        player.RestoreTurretCharge(1);
        player.NotifyPickup();
        SetAnnouncement("Turret battery built", 0.9f);
        _sound.PlayWorld(WorldSound.Pickup, player.Position, player.Position, 80f, false);
        return true;
    }

    private bool TryCraftOverdriveCell(Player player)
    {
        if (player.Adrenaline >= player.MaxAdrenaline)
        {
            SetAnnouncement("Overdrive already full", 0.8f);
            _sound.PlayUi(UiSound.Error);
            return false;
        }

        if (!TrySpendCraftResources(player, 5, 18, string.Empty))
        {
            return false;
        }

        player.AddAdrenaline(45f);
        player.NotifyPickup();
        SetAnnouncement("Overdrive cell charged", 0.9f);
        _sound.PlayWorld(WorldSound.Pickup, player.Position, player.Position, 80f, false);
        return true;
    }

    private bool TryCraftRecipe(Player player, int recipeIndex)
    {
        return recipeIndex switch
        {
            0 => TryCraftBarricade(player),
            1 => TryCraftAmmoCache(player),
            2 => TryCraftArmorPlate(player),
            3 => TryCraftMedPatch(player),
            4 => TryCraftTurretBattery(player),
            5 => TryCraftOverdriveCell(player),
            _ => false
        };
    }

    private bool TryPlaceBarricade(Player player, Vector2 mouseWorld)
    {
        if (!player.TryUseBarricadeKit())
        {
            SetAnnouncement("No barricade kits", 0.8f);
            return false;
        }

        if (TryGetBarricadePreview(mouseWorld, out Vector2 placed) && Map.TryPlaceBarricade(mouseWorld, out placed))
        {
            player.NotifyBarricade();
            _sound.PlayWorld(WorldSound.Barricade, player.Position, placed, 420f, true);
            return true;
        }

        player.BarricadeKits++;
        SetAnnouncement("Can't place barricade there", 0.9f);
        _sound.PlayUi(UiSound.Error);
        return false;
    }

    private bool TryDeployTurret(Player player)
    {
        if (!player.TryUseTurretCharge())
        {
            SetAnnouncement("Turret not ready", 0.8f);
            _sound.PlayUi(UiSound.Error);
            return false;
        }

        if (TryGetTurretPreview(player, out Vector2 position))
        {
            player.NotifyTurret();
            if (_network.Mode != NetMode.Client)
            {
                _turrets.Add(new Turret(position, 14f, 14f + player.TurretLifetimeBonus, 0.16f, 340f, 16 + player.TurretDamageBonus, 780f));
            }
            _sound.PlayWorld(WorldSound.Barricade, player.Position, position, 360f, true);
            return true;
        }

        player.RestoreTurretCharge(1);
        SetAnnouncement("Can't deploy turret there", 0.8f);
        _sound.PlayUi(UiSound.Error);
        return false;
    }

    private bool TryThrowGrenade(Player player)
    {
        if (!player.CanThrowGrenade())
        {
            SetAnnouncement("Grenade recharging", 0.7f);
            return false;
        }

        Vector2 dir = Phys.FromAngle(player.AimAngle);
        if (_network.Mode != NetMode.Client)
        {
            _grenades.Add(new Grenade(player.Position + dir * 20f, dir * 500f, 7f, 0.9f, player.GrenadeDamage, player.GrenadeRadius, 0, Color.Orange));
        }
        player.BeginGrenadeCooldown();
        player.NotifyGrenade();
        _sound.PlayWorld(WorldSound.Explosion, player.Position, player.Position + dir * 40f, 120f, false);
        return true;
    }

    private bool TryActivateOverdrive(Player player)
    {
        if (player.ActivateOverdrive())
        {
            player.NotifyOverdrive();
            SetAnnouncement("Overdrive ON", 0.9f);
            return true;
        }

        SetAnnouncement("Overdrive not charged", 0.7f);
        return false;
    }

    private bool ActivateInventoryRawSlot(Player player, int rawIndex, Size clientSize)
    {
        Vector2 mouseWorld = ScreenToWorld(_lastMouseScreen, clientSize);
        switch (rawIndex)
        {
            case 0:
            case 1:
            case 2:
            case 3:
                if (player.TrySelectWeapon(rawIndex))
                {
                    _inventoryUpgradeWeaponIndex = rawIndex;
                    return true;
                }
                SetAnnouncement("Weapon locked", 0.7f);
                return false;
            case 4:
                return TryCraftBarricade(player);
            case 6:
                return TryPlaceBarricade(player, mouseWorld);
            case 7:
                return TryThrowGrenade(player);
            case 8:
                return TryDeployTurret(player);
            case 9:
                return TryActivateOverdrive(player);
            default:
                return false;
        }
    }

    private bool ActivateHotbarSlot(Player player, int slotIndex, Size clientSize)
    {
        return SelectHotbarSlot(player, slotIndex, clientSize, false);
    }

    private bool TryGetBarricadePreview(Vector2 mouseWorld, out Vector2 placedCenter)
    {
        (int tx, int ty) = Map.ToTile(mouseWorld);
        placedCenter = Map.TileCenter(tx, ty);
        if (tx <= 1 || ty <= 1 || tx >= Map.Width - 2 || ty >= Map.Height - 2)
        {
            return false;
        }

        return !Map.IsBlocking(tx, ty);
    }

    private bool TryGetTurretPreview(Player player, out Vector2 position)
    {
        position = player.Position + Phys.FromAngle(player.AimAngle) * 42f;
        return !Map.CollidesCircle(position, 16f);
    }

    private void SetAnnouncement(string text, float duration)
    {
        _announcement = text;
        _announcementTimer = duration;
    }

    public void Update(float dt, InputState input, Size clientSize)
    {
        dt = Math.Min(0.033f, Math.Max(0.001f, dt));
        _backgroundPulse += dt;
        _lastMouseScreen = input.MouseScreen;
        _announcementTimer = Math.Max(0f, _announcementTimer - dt);
        _qualitySwitchCooldown = Math.Max(0f, _qualitySwitchCooldown - dt);
        _miniMapCacheTimer = Math.Max(0f, _miniMapCacheTimer - dt);
        UpdateHitFeedback(dt);
        switch (_phase)
        {
            case GamePhase.Title:
                UpdateTitle(input, clientSize);
                break;
            case GamePhase.MultiplayerMenu:
                UpdateMultiplayerMenu(input, clientSize);
                break;
            case GamePhase.LanBrowser:
                UpdateLanBrowser(input, clientSize);
                break;
            case GamePhase.Settings:
                UpdateSettings(input, clientSize);
                break;
            case GamePhase.HostSetup:
                UpdateHostSetup(input, clientSize);
                break;
            case GamePhase.JoinSetup:
                UpdateJoinSetup(input, clientSize);
                break;
            case GamePhase.Playing:
                UpdatePlaying(dt, input, clientSize);
                break;
            case GamePhase.Paused:
                UpdatePaused(input, clientSize);
                break;
            case GamePhase.GameOver:
                UpdateGameOver(input, clientSize);
                break;
        }

        UpdateCamera(clientSize);
    }

    private void CycleDifficulty()
    {
        _difficulty = _difficulty switch
        {
            DifficultyMode.Casual => DifficultyMode.Survival,
            DifficultyMode.Survival => DifficultyMode.Nightmare,
            _ => DifficultyMode.Casual
        };
        _sound.PlayUi(UiSound.Click);
        SetAnnouncement("Difficulty: " + GetDifficultyName(), 1f);
    }

    private void CycleUiScale()
    {
        _uiScale = _uiScale switch
        {
            UiScaleMode.Tiny => UiScaleMode.Compact,
            UiScaleMode.Compact => UiScaleMode.Normal,
            _ => UiScaleMode.Tiny
        };
        _sound.PlayUi(UiSound.Click);
        SetAnnouncement("UI scale: " + GetUiScaleName(), 1f);
    }

    private string GetDifficultyName()
    {
        return _difficulty switch
        {
            DifficultyMode.Casual => "Casual",
            DifficultyMode.Nightmare => "Nightmare",
            _ => "Survival"
        };
    }

    private string GetUiScaleName()
    {
        return _uiScale switch
        {
            UiScaleMode.Tiny => "Compact",
            UiScaleMode.Normal => "Huge",
            _ => "Large"
        };
    }

    private float GetUiScaleValue()
    {
        return _uiScale switch
        {
            UiScaleMode.Tiny => 1.02f,
            UiScaleMode.Normal => 1.34f,
            _ => 1.18f
        };
    }

    private float GetHealthMultiplier()
    {
        return _difficulty switch
        {
            DifficultyMode.Casual => 0.85f,
            DifficultyMode.Nightmare => 1.35f,
            _ => 1f
        };
    }

    private float GetDamageMultiplier()
    {
        return _difficulty switch
        {
            DifficultyMode.Casual => 0.82f,
            DifficultyMode.Nightmare => 1.35f,
            _ => 1f
        };
    }

    private int GetWaveTargetCount()
    {
        int baseCount = 8 + _waveNumber * 2;
        if (_difficulty == DifficultyMode.Casual) baseCount -= 2;
        if (_difficulty == DifficultyMode.Nightmare) baseCount += 4;
        if (_waveNumber % 5 == 0 && _waveNumber > 0) baseCount += 1;
        return Math.Max(6, baseCount);
    }

    private void HandleDayNightTransitions()
    {
        if (_dayNight.JustBecameNight)
        {
            StartNightWave();
            SetAnnouncement(_waveNumber % 5 == 0 ? $"Boss night {_waveNumber}" : $"Night {_waveNumber} started", 1.6f);
        }

        if (_dayNight.JustBecameDay)
        {
            _waveLive = false;
            _waveSpawnTimer = 0f;
            _nightWaveStarted = false;
            _nightRewardGranted = false;
            SpawnLootCrates(1 + (_waveNumber % 3 == 0 ? 1 : 0));
            _crateRespawnTimer = 10f + (float)_rng.NextDouble() * 6f;
            SetAnnouncement("Daytime. Zombies stop spawning.", 1.6f);
        }
    }

    private void StartNightWave()
    {
        _waveNumber++;
        _waveTarget = GetWaveTargetCount();
        _waveSpawned = 0;
        _waveSpawnTimer = 0.35f;
        _waveLive = true;
        _nightWaveStarted = true;
        _nightRewardGranted = false;
    }
    private void UpdateInventoryInput(InputState input)
    {
        if (!_inventoryOpen)
        {
            return;
        }

        if (input.WasPressed(Keys.Left) || input.WasPressed(Keys.A))
        {
            _inventorySelectedIndex--;
        }

        if (input.WasPressed(Keys.Right) || input.WasPressed(Keys.D))
        {
            _inventorySelectedIndex++;
        }

        if (input.WasPressed(Keys.Up) || input.WasPressed(Keys.W))
        {
            _inventorySelectedIndex -= 8;
        }

        if (input.WasPressed(Keys.Down) || input.WasPressed(Keys.S))
        {
            _inventorySelectedIndex += 8;
        }

        if (_inventorySelectedIndex < 0)
        {
            _inventorySelectedIndex = 31;
        }

        if (_inventorySelectedIndex > 31)
        {
            _inventorySelectedIndex = 0;
        }
    }
    private void UpdatePlaying(float dt, InputState input, Size clientSize)
    {
        if (input.WasPressed(Keys.Escape))
        {
            _phase = GamePhase.Paused;
            return;
        }

        _survivalTime += dt;
        _dayNight.Tick(dt);
        HandleDayNightTransitions();
        UpdateInventoryInput(input);
        UpdateLocalPlayer(dt, input, clientSize);
        UpdateNetworkGhosts();

        if (_network.Mode == NetMode.Client)
        {
            UpdateClientPredictedBullets(dt);
            SyncClientWorldFromNetwork(dt);
            SyncClientLocalPlayerStateFromNetwork();
            UpdatePickups();
            UpdateScrapPiles();
        }
        else
        {
            UpdateWaveDirector(dt);
            UpdateLootCrates(dt);
            UpdateTurrets(dt);
            UpdateGrenades(dt);
            UpdateBullets(dt);
            UpdateZombies(dt);
            UpdateExplosions(dt);
            UpdatePickups();
            UpdateScrapPiles();

            if (_network.Mode == NetMode.Host)
            {
                UpdateRemoteWorldInteractions();
                PushWorldSyncState();
            }
        }

        if (!_players.Any(p => p.IsAlive))
        {
            _phase = GamePhase.GameOver;
            _bestScore = Math.Max(_bestScore, _score);
            SetAnnouncement("Run ended", 99f);
        }
    }
    private void UpdateLocalPlayer(float dt, InputState input, Size clientSize)
    {
        if (_players.Count == 0)
        {
            return;
        }

        Player player = _players[0];
        if (!player.IsAlive)
        {
            return;
        }

        player.Tick(dt);

        Vector2 move = Vector2.Zero;
        if (input.IsDown(Keys.W)) move.Y -= 1f;
        if (input.IsDown(Keys.S)) move.Y += 1f;
        if (input.IsDown(Keys.A)) move.X -= 1f;
        if (input.IsDown(Keys.D)) move.X += 1f;
        move = Phys.NormalizeSafe(move);
        player.SetMoveBlend(move.LengthSquared() > 0.001f ? 1f : 0f, move);

        if (input.WasPressed(Keys.Space))
        {
            player.TryStartDash(move);
        }

        float speed = player.MoveSpeed * player.GetCurrentMoveSpeedMultiplier();
        Vector2 desired = player.Position + ((player.IsDashing ? player.DashDirection : move) * speed * dt);
        player.Position = Phys.ResolveCircleVsWorld(player.Position, desired, player.Radius, Map);

        Vector2 mouseWorld = ScreenToWorld(input.MouseScreen, clientSize);
        Vector2 aimVector = mouseWorld - player.Position;
        if (aimVector.LengthSquared() > 1f)
        {
            player.AimAngle = MathF.Atan2(aimVector.Y, aimVector.X);
        }

        if (input.WasPressed(Keys.D1)) ActivateHotbarSlot(player, 0, clientSize);
        if (input.WasPressed(Keys.D2)) ActivateHotbarSlot(player, 1, clientSize);
        if (input.WasPressed(Keys.D3)) ActivateHotbarSlot(player, 2, clientSize);
        if (input.WasPressed(Keys.D4)) ActivateHotbarSlot(player, 3, clientSize);
        if (input.WasPressed(Keys.D5)) ActivateHotbarSlot(player, 4, clientSize);
        if (input.WasPressed(Keys.D6)) ActivateHotbarSlot(player, 5, clientSize);
        if (input.WasPressed(Keys.D7)) ActivateHotbarSlot(player, 6, clientSize);
        if (input.WasPressed(Keys.D8)) ActivateHotbarSlot(player, 7, clientSize);
        if (input.WasPressed(Keys.D9)) ActivateHotbarSlot(player, 8, clientSize);
        if (input.WasPressed(Keys.Q))
        {
            player.CycleWeapon(-1);
            SyncSelectedHotbarToWeapon(player);
        }
        if (input.WasPressed(Keys.E))
        {
            if (!TryUseEquippedSupport(player, clientSize))
            {
                player.CycleWeapon(1);
                SyncSelectedHotbarToWeapon(player);
            }
        }
        if (!_inventoryOpen && input.MouseWheelDelta != 0)
        {
            StepHotbarSelection(player, input.MouseWheelDelta > 0 ? -1 : 1, clientSize);
        }

        if (input.WasPressed(Keys.R) && !IsSupportEquipped())
        {
            player.StartReload();
        }

        if (input.WasPressed(Keys.Tab))
        {
            if (!_inventoryOpen || _inventoryViewMode != InventoryViewMode.Inventory)
            {
                _inventoryOpen = true;
                _inventoryViewMode = InventoryViewMode.Inventory;
                SetAnnouncement("Inventory opened", 0.7f);
            }
            else
            {
                _inventoryOpen = false;
                SetAnnouncement("Inventory closed", 0.7f);
            }
        }

        if (input.WasPressed(Keys.C))
        {
            if (!_inventoryOpen || _inventoryViewMode != InventoryViewMode.Craft)
            {
                _inventoryOpen = true;
                _inventoryViewMode = InventoryViewMode.Craft;
                SetAnnouncement("Craft menu opened", 0.7f);
            }
            else
            {
                _inventoryOpen = false;
                SetAnnouncement("Craft menu closed", 0.7f);
            }
        }

        if (input.WasPressed(Keys.F))
        {
            TryActivateOverdrive(player);
        }

        if (input.WasPressed(Keys.B))
        {
            TryCraftBarricade(player);
        }

        if (input.WasPressed(Keys.T))
        {
            TryPlaceBarricade(player, mouseWorld);
        }

        if (input.WasPressed(Keys.G))
        {
            TryDeployTurret(player);
        }

        if (!_inventoryOpen && input.RightMousePressed)
        {
            TryThrowGrenade(player);
        }

        if (!_inventoryOpen && !IsSupportEquipped() && input.LeftMouseDown && player.CanShoot())
        {
            if (_network.Mode == NetMode.Client)
            {
                FireClientWeaponRequest(player);
            }
            else
            {
                FirePlayerWeapon(player, 0);
            }
        }

        _network.PushLocalState(
            player.Callsign,
            player.Accent,
            player.Position,
            player.AimAngle,
            player.Health,
            player.IsAlive,
            player.Armor,
            player.Weapon.Name,
            player.CurrentWeapon.Level,
            player.SelectedWeaponIndex,
            GetSelectedHotbarRawIndex(),
            player.LastMoveInput,
            player.MoveBlend,
            player.IsReloading,
            player.IsOverdriveActive,
            player.ShootAnimation,
            player.PickupAnimation,
            player.UseAnimation,
            player.ReloadAnimation,
            player.ShotSequence,
            player.GrenadeSequence,
            player.TurretSequence,
            player.BarricadeSequence,
            player.OverdriveSequence,
            player.PickupSequence);
    }

    private static Weapon GetWeaponDefinitionByName(string sourceWeaponName)
    {
        if (!string.IsNullOrWhiteSpace(sourceWeaponName))
        {
            Weapon? weapon = WeaponCatalog.FindByName(sourceWeaponName);
            if (weapon is not null)
            {
                return weapon;
            }
        }

        return WeaponCatalog.Rifle;
    }

    private void ApplyZombieImpact(Zombie enemy, Vector2 hitVelocity, string sourceWeaponName, float knockbackScale = 1f)
    {
        Weapon weapon = GetWeaponDefinitionByName(sourceWeaponName);
        float slowSeconds = Math.Max(0f, weapon.ZombieSlowSeconds);
        float slowMultiplier = Math.Clamp(weapon.ZombieSlowMultiplier, 0.35f, 1f);
        float knockback = Math.Max(0f, weapon.ZombieKnockback * knockbackScale);

        if (slowSeconds > 0f)
        {
            enemy.SlowTimer = MathF.Max(enemy.SlowTimer, slowSeconds);
            enemy.SlowMultiplier = MathF.Min(enemy.SlowMultiplier, slowMultiplier);
        }

        if (knockback > 0f && hitVelocity.LengthSquared() > 0.0001f)
        {
            Vector2 dir = Phys.NormalizeSafe(hitVelocity);
            float resistance = enemy.Kind switch
            {
                ZombieKind.Runner => 1.08f,
                ZombieKind.Tank => 0.48f,
                ZombieKind.Boss => 0.34f,
                ZombieKind.Exploder => 0.82f,
                _ => 0.9f
            };
            enemy.KnockbackVelocity += dir * knockback * resistance;
        }
    }

    private void SpawnWeaponBullets(List<Bullet> destination, Vector2 muzzle, float aimAngle, Weapon weapon, float spreadRadians, int damage, int ownerPlayerIndex, Color tint)
    {
        int pellets = Math.Max(1, weapon.Pellets);
        for (int pellet = 0; pellet < pellets; pellet++)
        {
            float spread = ((float)_rng.NextDouble() - 0.5f) * spreadRadians;
            Vector2 dir = Phys.FromAngle(aimAngle + spread);
            destination.Add(new Bullet(
                muzzle,
                dir * weapon.BulletSpeed,
                weapon.BulletRadius,
                weapon.BulletLifetime,
                damage,
                true,
                ownerPlayerIndex,
                tint,
                weapon.Name));
        }
    }

    private void PlayWeaponShotSound(Player player, Vector2 muzzle, Weapon weapon)
    {
        switch (weapon.Name)
        {
            case "SMG":
                _sound.PlayWorld(WorldSound.SmgShot, player.Position, muzzle, 760f, true);
                break;
            case "Shotgun":
                _sound.PlayWorld(WorldSound.ShotgunShot, player.Position, muzzle, 760f, true);
                break;
            case "Carbine":
                _sound.PlayWorld(WorldSound.CarbineShot, player.Position, muzzle, 760f, true);
                break;
            default:
                _sound.PlayWorld(WorldSound.RifleShot, player.Position, muzzle, 760f, true);
                break;
        }
    }

    private void FirePlayerWeapon(Player player, int ownerPlayerIndex)
    {
        Vector2 muzzle = player.Position + Phys.FromAngle(player.AimAngle) * 22f;
        Weapon weapon = player.Weapon;
        SpawnWeaponBullets(_bullets, muzzle, player.AimAngle, weapon, player.GetCurrentSpreadRadians(), player.GetShotDamage(), ownerPlayerIndex, player.Accent);
        player.NotifyShot();
        player.ConsumeShot();
        PlayWeaponShotSound(player, muzzle, weapon);
    }

    private void FireClientWeaponRequest(Player player)
    {
        Vector2 muzzle = player.Position + Phys.FromAngle(player.AimAngle) * 22f;
        Weapon weapon = player.Weapon;
        int ownerPlayerIndex = Math.Max(1, _network.LocalPlayerId);
        SpawnWeaponBullets(_clientPredictedBullets, muzzle, player.AimAngle, weapon, player.GetCurrentSpreadRadians(), player.GetShotDamage(), ownerPlayerIndex, Color.FromArgb(218, player.Accent));
        player.NotifyShot();
        player.ConsumeShot();
        PlayWeaponShotSound(player, muzzle, weapon);
    }

    private void UpdateClientPredictedBullets(float dt)
    {
        for (int i = _clientPredictedBullets.Count - 1; i >= 0; i--)
        {
            Bullet bullet = _clientPredictedBullets[i];
            bullet.Lifetime -= dt;
            bullet.Position += bullet.Velocity * dt;

            if (bullet.Lifetime <= 0f || Map.CollidesCircle(bullet.Position, bullet.Radius))
            {
                _clientPredictedBullets.RemoveAt(i);
            }
        }
    }

    private void ReconcileClientPredictedBullets()
    {
        if (_network.Mode != NetMode.Client || _clientPredictedBullets.Count == 0)
        {
            return;
        }

        int localId = Math.Max(1, _network.LocalPlayerId);

        for (int i = _clientPredictedBullets.Count - 1; i >= 0; i--)
        {
            Bullet predicted = _clientPredictedBullets[i];
            bool matched = false;

            for (int j = 0; j < _bullets.Count; j++)
            {
                Bullet authoritative = _bullets[j];
                if (!authoritative.FromPlayer || authoritative.OwnerPlayerIndex != localId)
                {
                    continue;
                }

                float radius = Math.Max(20f, authoritative.Radius + predicted.Radius + authoritative.Velocity.Length() * 0.05f);
                if (Vector2.DistanceSquared(predicted.Position, authoritative.Position) <= radius * radius)
                {
                    matched = true;
                    break;
                }
            }

            if (matched)
            {
                _clientPredictedBullets.RemoveAt(i);
            }
        }

        if (_clientPredictedBullets.Count > 64)
        {
            _clientPredictedBullets.RemoveRange(0, _clientPredictedBullets.Count - 64);
        }
    }

    private void UpdateNetworkGhosts()
    {
        _remotePlayers.Clear();
        HashSet<int> seen = new HashSet<int>();
        foreach (RemotePlayerView remote in _network.GetRemotePlayers())
        {
            if (remote.Position.X <= 0f && remote.Position.Y <= 0f)
            {
                continue;
            }

            seen.Add(remote.PlayerId);
            if (_network.Mode == NetMode.Host)
            {
                ApplyRemoteActions(remote);
            }
            else if (_network.Mode == NetMode.Client)
            {
                ApplyRemoteClientVisualActions(remote);
            }

            if (!_remotePlayerSmoothing.TryGetValue(remote.PlayerId, out Vector2 smoothed))
            {
                smoothed = remote.Position;
            }

            remote.TargetPosition = remote.Position;
            smoothed = Vector2.Lerp(smoothed, remote.Position, 0.28f);
            _remotePlayerSmoothing[remote.PlayerId] = smoothed;
            remote.Position = smoothed;
            _remotePlayers.Add(remote);
        }

        List<int> stale = new List<int>();
        foreach (int id in _remotePlayerSmoothing.Keys)
        {
            if (!seen.Contains(id))
            {
                stale.Add(id);
            }
        }

        for (int i = 0; i < stale.Count; i++)
        {
            _remotePlayerSmoothing.Remove(stale[i]);
            _remoteActionTrackers.Remove(stale[i]);
        }
    }

    private void ApplyRemoteActions(RemotePlayerView remote)
    {
        if (!_remoteActionTrackers.TryGetValue(remote.PlayerId, out RemoteActionTracker? tracker))
        {
            tracker = new RemoteActionTracker();
            _remoteActionTrackers[remote.PlayerId] = tracker;
        }

        int shotDelta = Math.Clamp(remote.ShotSequence - tracker.ShotSequence, 0, 4);
        for (int i = 0; i < shotDelta; i++)
        {
            FireRemoteWeapon(remote);
        }
        tracker.ShotSequence = remote.ShotSequence;

        int grenadeDelta = Math.Clamp(remote.GrenadeSequence - tracker.GrenadeSequence, 0, 2);
        for (int i = 0; i < grenadeDelta; i++)
        {
            SpawnRemoteGrenade(remote);
        }
        tracker.GrenadeSequence = remote.GrenadeSequence;

        int turretDelta = Math.Clamp(remote.TurretSequence - tracker.TurretSequence, 0, 2);
        for (int i = 0; i < turretDelta; i++)
        {
            SpawnRemoteTurret(remote);
        }
        tracker.TurretSequence = remote.TurretSequence;

        int barricadeDelta = Math.Clamp(remote.BarricadeSequence - tracker.BarricadeSequence, 0, 2);
        for (int i = 0; i < barricadeDelta; i++)
        {
            SpawnRemoteBarricade(remote);
        }
        tracker.BarricadeSequence = remote.BarricadeSequence;
        tracker.OverdriveSequence = remote.OverdriveSequence;
        tracker.PickupSequence = remote.PickupSequence;
    }

    private void FireRemoteWeapon(RemotePlayerView remote)
    {
        Weapon weapon = WeaponCatalog.FindByName(remote.WeaponName) ?? WeaponCatalog.Rifle;
        Vector2 muzzle = remote.TargetPosition + Phys.FromAngle(remote.AimAngle) * 22f;
        SpawnWeaponBullets(_bullets, muzzle, remote.AimAngle, weapon, weapon.SpreadRadians, Math.Max(1, weapon.Damage), remote.PlayerId, remote.Accent);
    }

    private void FireRemoteWeaponPredicted(RemotePlayerView remote)
    {
        Weapon weapon = WeaponCatalog.FindByName(remote.WeaponName) ?? WeaponCatalog.Rifle;
        Vector2 muzzle = remote.TargetPosition + Phys.FromAngle(remote.AimAngle) * 22f;
        SpawnWeaponBullets(_clientPredictedBullets, muzzle, remote.AimAngle, weapon, weapon.SpreadRadians, Math.Max(1, weapon.Damage), remote.PlayerId, Color.FromArgb(176, remote.Accent));
    }

    private void ApplyRemoteClientVisualActions(RemotePlayerView remote)
    {
        if (!_remoteActionTrackers.TryGetValue(remote.PlayerId, out RemoteActionTracker? tracker))
        {
            tracker = new RemoteActionTracker
            {
                ShotSequence = remote.ShotSequence,
                GrenadeSequence = remote.GrenadeSequence,
                TurretSequence = remote.TurretSequence,
                BarricadeSequence = remote.BarricadeSequence,
                OverdriveSequence = remote.OverdriveSequence,
                PickupSequence = remote.PickupSequence
            };
            _remoteActionTrackers[remote.PlayerId] = tracker;
            return;
        }

        int shotDelta = Math.Clamp(remote.ShotSequence - tracker.ShotSequence, 0, 4);
        for (int i = 0; i < shotDelta; i++)
        {
            FireRemoteWeaponPredicted(remote);
        }

        tracker.ShotSequence = remote.ShotSequence;
        tracker.GrenadeSequence = remote.GrenadeSequence;
        tracker.TurretSequence = remote.TurretSequence;
        tracker.BarricadeSequence = remote.BarricadeSequence;
        tracker.OverdriveSequence = remote.OverdriveSequence;
        tracker.PickupSequence = remote.PickupSequence;
    }

    private void SpawnRemoteGrenade(RemotePlayerView remote)
    {
        Vector2 dir = Phys.FromAngle(remote.AimAngle);
        _grenades.Add(new Grenade(remote.TargetPosition + dir * 20f, dir * 500f, 7f, 0.9f, 76, 120f, remote.PlayerId, Color.Orange));
    }

    private void SpawnRemoteTurret(RemotePlayerView remote)
    {
        Vector2 position = remote.TargetPosition + Phys.FromAngle(remote.AimAngle) * 42f;
        if (!Map.CollidesCircle(position, 16f))
        {
            _turrets.Add(new Turret(position, 14f, 14f, 0.16f, 340f, 16, 780f));
        }
    }

    private void SpawnRemoteBarricade(RemotePlayerView remote)
    {
        Vector2 placeAt = remote.TargetPosition + Phys.FromAngle(remote.AimAngle) * 56f;
        Map.TryPlaceBarricade(placeAt, out _);
    }

    private void SyncClientWorldFromNetwork(float dt)
    {
        NetWorldState world = _network.GetWorldState();

        _score = Math.Max(0, world.Score);
        _survivalTime = Math.Max(0f, world.SurvivalTime);
        _waveNumber = Math.Max(0, world.WaveNumber);
        _waveLive = world.WaveLive;
        _waveSpawned = Math.Max(0, world.WaveSpawned);
        _waveTarget = Math.Max(0, world.WaveTarget);
        _nightWaveStarted = world.NightWaveStarted;
        _nightRewardGranted = world.NightRewardGranted;
        _dayNight.ApplyNetworkState(world.IsNight, world.PhaseTimer, world.TotalTime, world.Blend);

        Dictionary<int, Zombie> zombiesById = new Dictionary<int, Zombie>(_zombies.Count);
        for (int i = 0; i < _zombies.Count; i++)
        {
            Zombie existing = _zombies[i];
            if (existing.NetworkId > 0)
            {
                zombiesById[existing.NetworkId] = existing;
            }
        }

        List<Zombie> syncedZombies = new List<Zombie>(world.Zombies.Count);
        float zombieLeadSeconds = Math.Clamp((_network.WorldSnapshotAgeMs + _network.PingMs * 0.35f) / 1000f, 0f, 0.45f);
        float zombieLerp = 1f - MathF.Exp(-Math.Max(0.001f, 10f * dt));

        foreach (NetZombieState state in world.Zombies)
        {
            int zombieId = state.Id <= 0 ? syncedZombies.Count + 1 : state.Id;
            Zombie zombie;
            if (!zombiesById.TryGetValue(zombieId, out zombie))
            {
                zombie = Zombie.Create((ZombieKind)Math.Clamp(state.KindId, 0, (int)ZombieKind.Boss), new Vector2(state.X, state.Y), Math.Max(1, _waveNumber));
                zombie.Position = new Vector2(state.X, state.Y);
                zombie.NetworkId = zombieId;
            }

            Vector2 authoritativePosition = new Vector2(state.X, state.Y);
            Vector2 velocity = new Vector2(state.VelocityX, state.VelocityY);
            Vector2 predictedPosition = authoritativePosition + velocity * zombieLeadSeconds;
            if (Map.CollidesCircle(predictedPosition, zombie.Radius))
            {
                predictedPosition = authoritativePosition;
            }

            float snapDistanceSq = Vector2.DistanceSquared(zombie.Position, authoritativePosition);
            if (snapDistanceSq > 240f * 240f)
            {
                zombie.Position = authoritativePosition;
            }
            else
            {
                zombie.Position = Vector2.Lerp(zombie.Position, predictedPosition, zombieLerp);
            }

            zombie.NetworkId = zombieId;
            zombie.Velocity = velocity;
            zombie.Health = state.IsAlive ? Math.Max(0, state.Health) : 0;
            zombie.MaxHealth = Math.Max(1, state.MaxHealth);
            syncedZombies.Add(zombie);
        }

        _zombies.Clear();
        _zombies.AddRange(syncedZombies);

        _pickups.Clear();
        foreach (NetPickupState state in world.Pickups)
        {
            _pickups.Add(new Pickup((PickupType)Math.Clamp(state.TypeId, 0, (int)PickupType.Adrenaline), new Vector2(state.X, state.Y), state.Value));
        }

        _scrapPiles.Clear();
        foreach (NetScrapState state in world.Scraps)
        {
            _scrapPiles.Add(new ScrapPile(new Vector2(state.X, state.Y), state.ScrapAmount));
        }

        _turrets.Clear();
        foreach (NetTurretState state in world.Turrets)
        {
            Turret turret = new Turret(new Vector2(state.X, state.Y), 14f, state.MaxLifetime, 0.16f, 340f, 16, 780f)
            {
                AimAngle = state.AimAngle,
                Lifetime = state.Lifetime,
                MaxLifetime = state.MaxLifetime
            };
            _turrets.Add(turret);
        }

        _lootCrates.Clear();
        foreach (NetLootCrateState state in world.Crates)
        {
            LootCrate crate = new LootCrate(new Vector2(state.X, state.Y), Math.Max(1, state.MaxHealth), state.Radius)
            {
                Health = Math.Clamp(state.Health, 0, Math.Max(1, state.MaxHealth)),
                MaxHealth = Math.Max(1, state.MaxHealth),
                HitFlash = state.HitFlash,
                Destroyed = state.Destroyed
            };
            _lootCrates.Add(crate);
        }

        float bulletLeadSeconds = Math.Clamp((_network.WorldSnapshotAgeMs + _network.PingMs * 0.5f) / 1000f, 0f, 0.35f);
        _bullets.Clear();
        foreach (NetBulletState state in world.Bullets)
        {
            Vector2 velocity = new Vector2(state.VelocityX, state.VelocityY);
            float lifetime = state.Lifetime - bulletLeadSeconds;
            if (lifetime <= 0f)
            {
                continue;
            }

            Vector2 position = new Vector2(state.X, state.Y) + velocity * bulletLeadSeconds;
            if (Map.CollidesCircle(position, state.Radius))
            {
                continue;
            }

            _bullets.Add(new Bullet(
                position,
                velocity,
                state.Radius,
                lifetime,
                state.Damage,
                state.FromPlayer,
                state.OwnerPlayerId,
                Color.FromArgb(state.TintArgb),
                state.SourceWeaponName));
        }

        ReconcileClientPredictedBullets();

        _grenades.Clear();
        foreach (NetGrenadeState state in world.Grenades)
        {
            _grenades.Add(new Grenade(
                new Vector2(state.X, state.Y),
                new Vector2(state.VelocityX, state.VelocityY),
                state.Radius,
                state.Lifetime,
                state.Damage,
                state.ExplosionRadius,
                state.OwnerPlayerId,
                Color.FromArgb(state.TintArgb)));
        }

        _explosions.Clear();
        foreach (NetExplosionState state in world.Explosions)
        {
            Explosion explosion = new Explosion(new Vector2(state.X, state.Y), state.Radius, Math.Max(0.05f, state.MaxLifetime), Color.FromArgb(state.TintArgb))
            {
                Lifetime = state.Lifetime,
                MaxLifetime = Math.Max(0.05f, state.MaxLifetime)
            };
            _explosions.Add(explosion);
        }

        Map.SetBarricades(world.Barricades.Select(b => (b.Tx, b.Ty, b.Health)));
    }

    private void SyncClientLocalPlayerStateFromNetwork()
    {
        if (_network.Mode != NetMode.Client || _players.Count == 0)
        {
            return;
        }

        NetPlayerState? state = _network.GetLocalPlayerState();
        if (state is null)
        {
            return;
        }

        Player player = Player;
        player.Health = Math.Max(0, state.Health);
        player.Armor = Math.Max(0, state.Armor);
        if (!state.IsAlive)
        {
            player.Health = 0;
        }
    }

    private void UpdateRemoteWorldInteractions()
    {
        if (_network.Mode != NetMode.Host || _remotePlayers.Count == 0)
        {
            return;
        }

        for (int r = 0; r < _remotePlayers.Count; r++)
        {
            RemotePlayerView remote = _remotePlayers[r];
            if (!remote.IsAlive)
            {
                continue;
            }

            Vector2 remotePos = remote.TargetPosition;

            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                Pickup pickup = _pickups[i];
                if (pickup.Collected)
                {
                    continue;
                }

                if (!Phys.CirclesOverlap(remotePos, 24f, pickup.Position, pickup.Radius))
                {
                    continue;
                }

                pickup.Collected = true;
                _network.ApplyRemotePlayerPickup(remote.PlayerId, pickup.Type, pickup.Value);
                _pickups.RemoveAt(i);
            }

            for (int i = _scrapPiles.Count - 1; i >= 0; i--)
            {
                ScrapPile scrap = _scrapPiles[i];
                if (scrap.Collected)
                {
                    continue;
                }

                if (!Phys.CirclesOverlap(remotePos, 26f, scrap.Position, scrap.Radius))
                {
                    continue;
                }

                scrap.Collected = true;
                _network.ApplyRemotePlayerPickup(remote.PlayerId, PickupType.Credits, scrap.ScrapAmount);
                _scrapPiles.RemoveAt(i);
            }
        }
    }

    private bool TryGetClosestAliveTarget(Vector2 from, out Vector2 targetPos, out float targetRadius, out int targetPlayerId)
    {
        targetPos = Vector2.Zero;
        targetRadius = 18f;
        targetPlayerId = 0;
        bool found = false;
        float bestDist = float.MaxValue;

        foreach (Player player in _players)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            float dist = Vector2.DistanceSquared(player.Position, from);
            if (dist < bestDist)
            {
                bestDist = dist;
                targetPos = player.Position;
                targetRadius = player.Radius;
                targetPlayerId = 0;
                found = true;
            }
        }

        if (_network.Mode == NetMode.Host)
        {
            foreach (RemotePlayerView remote in _remotePlayers)
            {
                if (!remote.IsAlive)
                {
                    continue;
                }

                float dist = Vector2.DistanceSquared(remote.TargetPosition, from);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    targetPos = remote.TargetPosition;
                    targetRadius = 18f;
                    targetPlayerId = remote.PlayerId;
                    found = true;
                }
            }
        }

        return found;
    }

    private void UpdateWaveDirector(float dt)
    {
        if (!_dayNight.IsNight)
        {
            return;
        }

        if (!_nightWaveStarted)
        {
            StartNightWave();
            SetAnnouncement(_waveNumber % 5 == 0 ? $"Boss night {_waveNumber}" : $"Night {_waveNumber} started", 1.6f);
        }

        if (!_waveLive)
        {
            return;
        }

        _waveSpawnTimer -= dt;
        if (_waveSpawned < _waveTarget && _waveSpawnTimer <= 0f)
        {
            _waveSpawnTimer = Math.Max(0.1f, 0.6f - _waveNumber * 0.018f);
            SpawnZombie();
            _waveSpawned++;
        }

        if (_waveSpawned >= _waveTarget && _zombies.Count == 0)
        {
            _waveLive = false;
            if (!_nightRewardGranted)
            {
                _nightRewardGranted = true;
                _scrapPiles.Add(new ScrapPile(Map.GetRandomFreePoint(_rng), 3 + _rng.Next(3)));
                _pickups.Add(new Pickup(PickupType.Credits, Map.GetRandomFreePoint(_rng), 12 + _waveNumber * 2));
                SetAnnouncement("Night clear", 1.4f);
            }
        }
    }

    private void SpawnZombie()
    {
        ZombieKind kind = PickZombieKind();
        Vector2 spawn = PickSpawnPoint();
        Zombie enemy = CreateTrackedZombie(kind, spawn, _waveNumber);

        enemy.MaxHealth = Math.Max(1, (int)MathF.Round(enemy.MaxHealth * GetHealthMultiplier()));
        enemy.Health = enemy.MaxHealth;
        enemy.ProjectileDamage = Math.Max(1, (int)MathF.Round(enemy.ProjectileDamage * GetDamageMultiplier()));
        enemy.ContactDamage = Math.Max(1, (int)MathF.Round(enemy.ContactDamage * GetDamageMultiplier()));
        enemy.ExplosionDamage = Math.Max(1, (int)MathF.Round(enemy.ExplosionDamage * GetDamageMultiplier()));

        _zombies.Add(enemy);
    }

    private ZombieKind PickZombieKind()
    {
        if (_waveNumber > 0 && _waveNumber % 5 == 0 && _waveSpawned == 0)
        {
            return ZombieKind.Boss;
        }

        int roll = _rng.Next(100);
        if (_waveNumber < 3)
        {
            return roll < 72 ? ZombieKind.Grunt : ZombieKind.Runner;
        }

        if (_waveNumber < 5)
        {
            if (roll < 40) return ZombieKind.Grunt;
            if (roll < 60) return ZombieKind.Runner;
            if (roll < 76) return ZombieKind.Spitter;
            if (roll < 90) return ZombieKind.Leaper;
            return ZombieKind.Exploder;
        }

        if (_waveNumber < 8)
        {
            if (roll < 26) return ZombieKind.Grunt;
            if (roll < 42) return ZombieKind.Runner;
            if (roll < 56) return ZombieKind.Spitter;
            if (roll < 70) return ZombieKind.Leaper;
            if (roll < 82) return ZombieKind.Exploder;
            if (roll < 92) return ZombieKind.Howler;
            return ZombieKind.Necromancer;
        }

        if (roll < 18) return ZombieKind.Grunt;
        if (roll < 32) return ZombieKind.Runner;
        if (roll < 45) return ZombieKind.Tank;
        if (roll < 58) return ZombieKind.Spitter;
        if (roll < 70) return ZombieKind.Leaper;
        if (roll < 81) return ZombieKind.Exploder;
        if (roll < 91) return ZombieKind.Howler;
        return ZombieKind.Necromancer;
    }

    private Vector2 PickSpawnPoint()
    {
        Player player = Player;
        for (int i = 0; i < 64; i++)
        {
            Vector2 p = Map.GetRandomFreePoint(_rng);
            if (Vector2.DistanceSquared(p, player.Position) > 340f * 340f)
            {
                return p;
            }
        }

        return Map.GetRandomFreePoint(_rng);
    }

    private void UpdateTurrets(float dt)
    {
        for (int i = _turrets.Count - 1; i >= 0; i--)
        {
            Turret turret = _turrets[i];
            turret.Lifetime -= dt;
            turret.FireCooldown -= dt;
            if (turret.Dead)
            {
                _turrets.RemoveAt(i);
                continue;
            }

            Zombie? target = _zombies
                .Where(e => e.IsAlive && Vector2.DistanceSquared(e.Position, turret.Position) <= turret.Range * turret.Range)
                .OrderBy(e => Vector2.DistanceSquared(e.Position, turret.Position))
                .FirstOrDefault();

            if (target is null)
            {
                continue;
            }

            Vector2 dir = Phys.NormalizeSafe(target.Position - turret.Position);
            turret.AimAngle = MathF.Atan2(dir.Y, dir.X);

            if (turret.FireCooldown <= 0f)
            {
                turret.FireCooldown = turret.FireInterval;
                _bullets.Add(new Bullet(turret.Position, dir * turret.BulletSpeed, 4f, 0.9f, turret.Damage, true, -2, Color.LightSeaGreen, "Turret"));
                _sound.PlayWorld(WorldSound.SmgShot, Player.Position, turret.Position, 680f, true);
            }
        }
    }

    private void UpdateGrenades(float dt)
    {
        for (int i = _grenades.Count - 1; i >= 0; i--)
        {
            Grenade grenade = _grenades[i];
            grenade.Lifetime -= dt;
            grenade.Position = Phys.ResolveCircleVsWorld(grenade.Position, grenade.Position + grenade.Velocity * dt, grenade.Radius, Map);
            grenade.Velocity *= 0.982f;

            if (grenade.Lifetime <= 0f)
            {
                Explode(grenade.Position, grenade.ExplosionRadius, grenade.Damage, true, Color.OrangeRed);
                _grenades.RemoveAt(i);
            }
        }
    }

    private void Explode(Vector2 position, float radius, int damage, bool fromPlayer, Color tint)
    {
        _explosions.Add(new Explosion(position, radius, 0.35f, tint));
        _sound.PlayWorld(WorldSound.Explosion, Player.Position, position, 900f, true);

        if (fromPlayer)
        {
            foreach (Zombie enemy in _zombies)
            {
                if (!enemy.IsAlive)
                {
                    continue;
                }

                float dist = Vector2.Distance(enemy.Position, position);
                if (dist <= radius)
                {
                    float falloff = 1f - dist / Math.Max(1f, radius);
                    int dealt = Math.Max(1, (int)(damage * falloff));
                    enemy.Health -= dealt;
                    Vector2 blast = enemy.Position - position;
                    ApplyZombieImpact(enemy, blast, "Shotgun", 0.9f + falloff * 0.7f);
                    if (enemy.Health <= 0)
                    {
                        OnZombieKilled(enemy, 0, "");
                    }
                }
            }

            Map.TryDamageBarricadeNear(position, damage / 4);
        }
        else
        {
            foreach (Player player in _players)
            {
                if (!player.IsAlive)
                {
                    continue;
                }

                float dist = Vector2.Distance(player.Position, position);
                if (dist <= radius)
                {
                    int dealt = Math.Max(1, (int)(damage * (1f - dist / Math.Max(1f, radius))));
                    player.TakeDamage(dealt);
                    TriggerPlayerHitFeedback(dealt, position, dealt > 18);
                }
            }

            if (_network.Mode == NetMode.Host)
            {
                foreach (RemotePlayerView remote in _remotePlayers)
                {
                    if (!remote.IsAlive)
                    {
                        continue;
                    }

                    float dist = Vector2.Distance(remote.TargetPosition, position);
                    if (dist <= radius)
                    {
                        int dealt = Math.Max(1, (int)(damage * (1f - dist / Math.Max(1f, radius))));
                        _network.ApplyRemotePlayerDamage(remote.PlayerId, dealt);
                    }
                }
            }

            Map.TryDamageBarricadeNear(position, damage / 2);
        }

        foreach (LootCrate crate in _lootCrates)
        {
            if (crate.Destroyed)
            {
                continue;
            }

            float dist = Vector2.Distance(crate.Position, position);
            if (dist <= radius)
            {
                int dealt = Math.Max(1, (int)(damage * (1f - dist / Math.Max(1f, radius)) * 0.75f));
                DamageLootCrate(crate, dealt);
            }
        }
    }
    private void UpdateBullets(float dt)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            Bullet bullet = _bullets[i];
            bullet.Lifetime -= dt;
            bullet.Position += bullet.Velocity * dt;

            if (bullet.Lifetime <= 0f || Map.CollidesCircle(bullet.Position, bullet.Radius))
            {
                _bullets.RemoveAt(i);
                continue;
            }

            if (bullet.FromPlayer)
            {
                bool hit = false;
                foreach (Zombie enemy in _zombies)
                {
                    if (!enemy.IsAlive)
                    {
                        continue;
                    }

                    if (Phys.CirclesOverlap(bullet.Position, bullet.Radius, enemy.Position, enemy.Radius))
                    {
                        enemy.Health -= bullet.Damage;
                        ApplyZombieImpact(enemy, bullet.Velocity, bullet.SourceWeaponName);
                        _sound.PlayWorld(WorldSound.Hit, Player.Position, enemy.Position, 500f, true);

                        if (bullet.OwnerPlayerIndex == 0 && !string.IsNullOrWhiteSpace(bullet.SourceWeaponName))
                        {
                            if (Player.AwardWeaponExperience(bullet.SourceWeaponName, 5))
                            {
                                WeaponState? state = Player.GetWeaponState(bullet.SourceWeaponName);
                                if (state is not null)
                                {
                                    SetAnnouncement($"{state.Definition.Name} level {state.Level}", 0.9f);
                                }
                            }
                        }

                        if (enemy.Health <= 0)
                        {
                            OnZombieKilled(enemy, bullet.OwnerPlayerIndex, bullet.SourceWeaponName);
                        }

                        hit = true;
                        break;
                    }
                }

                if (!hit)
                {
                    foreach (LootCrate crate in _lootCrates)
                    {
                        if (crate.Destroyed)
                        {
                            continue;
                        }

                        if (Phys.CirclesOverlap(bullet.Position, bullet.Radius, crate.Position, crate.Radius))
                        {
                            DamageLootCrate(crate, bullet.Damage);
                            hit = true;
                            break;
                        }
                    }
                }

                if (hit)
                {
                    _bullets.RemoveAt(i);
                }
            }
            else
            {
                bool hit = false;
                foreach (Player player in _players)
                {
                    if (!player.IsAlive)
                    {
                        continue;
                    }

                    if (Phys.CirclesOverlap(bullet.Position, bullet.Radius, player.Position, player.Radius))
                    {
                        player.TakeDamage(bullet.Damage);
                        TriggerPlayerHitFeedback(bullet.Damage, bullet.Position);
                        hit = true;
                        break;
                    }
                }

                if (!hit && _network.Mode == NetMode.Host)
                {
                    foreach (RemotePlayerView remote in _remotePlayers)
                    {
                        if (!remote.IsAlive)
                        {
                            continue;
                        }

                        if (Phys.CirclesOverlap(bullet.Position, bullet.Radius, remote.TargetPosition, 18f))
                        {
                            _network.ApplyRemotePlayerDamage(remote.PlayerId, bullet.Damage);
                            hit = true;
                            break;
                        }
                    }
                }

                if (hit)
                {
                    _bullets.RemoveAt(i);
                }
            }
        }
    }

    private void OnZombieKilled(Zombie enemy, int ownerPlayerIndex, string sourceWeaponName = "")
    {
        if (enemy.RewardScore < 0)
        {
            return;
        }

        enemy.Health = 0;
        int reward = enemy.RewardScore;
        enemy.RewardScore = -1;
        Player player = Player;
        player.Kills++;
        player.Credits += 2 + Math.Max(0, reward / 10);
        player.AddAdrenaline(8f + reward * 0.1f);
        _score += reward;

        if (ownerPlayerIndex == 0 && !string.IsNullOrWhiteSpace(sourceWeaponName))
        {
            if (player.AwardWeaponExperience(sourceWeaponName, 18 + Math.Max(0, reward / 6)))
            {
                WeaponState? state = player.GetWeaponState(sourceWeaponName);
                if (state is not null)
                {
                    SetAnnouncement($"{state.Definition.Name} level {state.Level}", 1.1f);
                }
            }
        }

        if (_rng.NextDouble() < 0.18)
        {
            _pickups.Add(new Pickup(PickupType.Ammo, enemy.Position, 1));
        }

        if (_rng.NextDouble() < 0.10)
        {
            _pickups.Add(new Pickup(PickupType.Medkit, enemy.Position, 16));
        }

        if (_rng.NextDouble() < 0.25)
        {
            _scrapPiles.Add(new ScrapPile(enemy.Position + new Vector2(_rng.Next(-8, 9), _rng.Next(-8, 9)), 1 + _rng.Next(2)));
        }

        if (enemy.Kind == ZombieKind.Exploder || enemy.Kind == ZombieKind.Boss)
        {
            Explode(enemy.Position, enemy.Kind == ZombieKind.Boss ? 150f : enemy.ExplosionRadius, enemy.Kind == ZombieKind.Boss ? enemy.ExplosionDamage + 10 : enemy.ExplosionDamage, false, Color.OrangeRed);
        }
    }

    private void UpdateZombies(float dt)
    {
        for (int i = _zombies.Count - 1; i >= 0; i--)
        {
            Zombie enemy = _zombies[i];
            if (!enemy.IsAlive)
            {
                _zombies.RemoveAt(i);
                continue;
            }

            enemy.AttackCooldown = Math.Max(0f, enemy.AttackCooldown - dt);
            enemy.SpecialCooldown = Math.Max(0f, enemy.SpecialCooldown - dt);
            enemy.BuffTimer = Math.Max(0f, enemy.BuffTimer - dt);
            enemy.PathRefreshTimer = Math.Max(0f, enemy.PathRefreshTimer - dt);
            enemy.SlowTimer = Math.Max(0f, enemy.SlowTimer - dt);
            if (enemy.SlowTimer <= 0f)
            {
                enemy.SlowMultiplier = 1f;
            }

            enemy.KnockbackVelocity *= MathF.Exp(-8.5f * dt);
            if (enemy.KnockbackVelocity.LengthSquared() < 4f)
            {
                enemy.KnockbackVelocity = Vector2.Zero;
            }

            if (!TryGetClosestAliveTarget(enemy.Position, out Vector2 targetPos, out float targetRadius, out int targetPlayerId))
            {
                continue;
            }

            bool hasLineOfSight = Map.HasLineOfSight(enemy.Position, targetPos);

            if (hasLineOfSight)
            {
                enemy.PathWaypoint = targetPos;
            }
            else if (enemy.PathRefreshTimer <= 0f || Vector2.DistanceSquared(enemy.Position, enemy.PathWaypoint) < 36f)
            {
                enemy.PathWaypoint = Map.GetNextStepToward(enemy.Position, targetPos);
                enemy.PathRefreshTimer = 0.42f + (float)_rng.NextDouble() * 0.28f;
            }

            Vector2 desiredDir = Phys.NormalizeSafe(enemy.PathWaypoint - enemy.Position);
            float desiredSpeed = enemy.EffectiveMoveSpeed;
            float distanceToTargetSq = Vector2.DistanceSquared(enemy.Position, targetPos);

            if (enemy.IsRanged)
            {
                float preferredRange = enemy.PreferredRange;
                float retreatRange = preferredRange * 0.75f;
                float holdRange = preferredRange * 1.2f;

                if (distanceToTargetSq < retreatRange * retreatRange)
                {
                    desiredDir = Phys.NormalizeSafe(enemy.Position - targetPos);
                }
                else if (distanceToTargetSq < holdRange * holdRange && hasLineOfSight)
                {
                    desiredDir = Vector2.Zero;
                }
            }

            Vector2 before = enemy.Position;
            Vector2 travel = desiredDir * desiredSpeed + enemy.KnockbackVelocity;
            Vector2 after = Phys.ResolveCircleVsWorld(enemy.Position, enemy.Position + travel * dt, enemy.Radius, Map);
            enemy.Position = after;

            bool hitBarricade = false;
            Vector2 barricadeHit = enemy.Position;

            if (!hasLineOfSight && Vector2.DistanceSquared(enemy.Position, targetPos) > (enemy.Radius + 22f) * (enemy.Radius + 22f))
            {
                hitBarricade = Map.TryDamageBlockingBarricade(enemy.Position, targetPos, Math.Max(4, enemy.BarricadeDamage), out barricadeHit);
            }

            if (!hitBarricade && Vector2.DistanceSquared(before, after) < 0.0004f)
            {
                hitBarricade = Map.TryDamageBlockingBarricade(enemy.Position, targetPos, Math.Max(4, enemy.BarricadeDamage), out barricadeHit);
            }

            if (hitBarricade)
            {
                _sound.PlayWorld(WorldSound.Barricade, Player.Position, barricadeHit, 500f, true);
            }

            distanceToTargetSq = Vector2.DistanceSquared(enemy.Position, targetPos);

            if (enemy.Kind == ZombieKind.Leaper && enemy.SpecialCooldown <= 0f && distanceToTargetSq < 220f * 220f)
            {
                enemy.SpecialCooldown = Math.Max(1.8f, enemy.SpecialInterval);
                Vector2 leapDir = Phys.NormalizeSafe(targetPos - enemy.Position);
                enemy.Position = Phys.ResolveCircleVsWorld(enemy.Position, enemy.Position + leapDir * enemy.LeapDistance, enemy.Radius, Map);
                _sound.PlayWorld(WorldSound.Zombie, Player.Position, enemy.Position, 650f, true);
            }

            if (enemy.Kind == ZombieKind.Howler && enemy.SpecialCooldown <= 0f)
            {
                enemy.SpecialCooldown = Math.Max(3.4f, enemy.SpecialInterval);
                foreach (Zombie other in _zombies)
                {
                    if (other == enemy || !other.IsAlive)
                    {
                        continue;
                    }

                    if (Vector2.DistanceSquared(other.Position, enemy.Position) <= 180f * 180f)
                    {
                        other.BuffTimer = Math.Max(other.BuffTimer, 3.2f);
                    }
                }
            }

            if (enemy.Kind == ZombieKind.Necromancer && enemy.SpecialCooldown <= 0f)
            {
                enemy.SpecialCooldown = Math.Max(4.5f, enemy.SpecialInterval);
                for (int s = 0; s < 2; s++)
                {
                    Vector2 spawn = enemy.Position + new Vector2(_rng.Next(-42, 43), _rng.Next(-42, 43));
                    if (!Map.CollidesCircle(spawn, 14f))
                    {
                        _zombies.Add(CreateTrackedZombie(ZombieKind.Runner, spawn, Math.Max(1, _waveNumber - 1)));
                    }
                }
            }

            if (enemy.Kind == ZombieKind.Boss && enemy.SpecialCooldown <= 0f)
            {
                enemy.SpecialCooldown = Math.Max(3.5f, enemy.SpecialInterval);
                for (int s = 0; s < 3; s++)
                {
                    Vector2 spawn = enemy.Position + new Vector2(_rng.Next(-56, 57), _rng.Next(-56, 57));
                    if (!Map.CollidesCircle(spawn, 16f))
                    {
                        _zombies.Add(CreateTrackedZombie(ZombieKind.Runner, spawn, _waveNumber));
                    }
                }
            }

            enemy.Velocity = dt > 0.0001f ? (enemy.Position - before) / dt : Vector2.Zero;

            float rangedAttackRange = enemy.PreferredRange * 1.25f;
            if (enemy.IsRanged && enemy.AttackCooldown <= 0f && hasLineOfSight && distanceToTargetSq <= rangedAttackRange * rangedAttackRange)
            {
                enemy.AttackCooldown = enemy.ContactInterval;
                FireZombieBurst(enemy, targetPos);
            }

            float contactRange = enemy.Radius + targetRadius + 2f;
            if (distanceToTargetSq <= contactRange * contactRange && enemy.AttackCooldown <= 0f)
            {
                enemy.AttackCooldown = enemy.ContactInterval;
                if (targetPlayerId == 0)
                {
                    Player target = GetClosestAlivePlayer(enemy.Position);
                    target.TakeDamage(enemy.ContactDamage);
                    TriggerPlayerHitFeedback(enemy.ContactDamage, enemy.Position, enemy.Kind == ZombieKind.Exploder || enemy.Kind == ZombieKind.Boss);
                }
                else
                {
                    _network.ApplyRemotePlayerDamage(targetPlayerId, enemy.ContactDamage);
                }

                if (enemy.Kind == ZombieKind.Exploder)
                {
                    Explode(enemy.Position, enemy.ExplosionRadius, enemy.ExplosionDamage, false, Color.OrangeRed);
                    enemy.Health = 0;
                }
            }
        }
    }

    private void FireZombieBurst(Zombie enemy, Vector2 targetPos)
    {
        Vector2 baseDir = Phys.NormalizeSafe(targetPos - enemy.Position);
        int projectiles = Math.Max(1, enemy.BurstProjectiles);

        for (int p = 0; p < projectiles; p++)
        {
            float t = projectiles == 1 ? 0f : (p / (float)(projectiles - 1) - 0.5f);
            float angle = MathF.Atan2(baseDir.Y, baseDir.X) + t * enemy.ProjectileSpread;
            Vector2 dir = Phys.FromAngle(angle);
            _bullets.Add(new Bullet(enemy.Position + dir * (enemy.Radius + 4f), dir * enemy.ProjectileSpeed, 5f, 1.4f, enemy.ProjectileDamage, false, -1, enemy.Tint, enemy.Kind.ToString()));
        }

        _sound.PlayWorld(WorldSound.Zombie, Player.Position, enemy.Position, 900f, true);
    }

    private Player GetClosestAlivePlayer(Vector2 from)
    {
        Player? best = null;
        float bestDist = float.MaxValue;
        foreach (Player player in _players)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            float d = Vector2.DistanceSquared(player.Position, from);
            if (d < bestDist)
            {
                bestDist = d;
                best = player;
            }
        }

        return best ?? Player;
    }

    private void UpdateExplosions(float dt)
    {
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            Explosion ex = _explosions[i];
            ex.Lifetime -= dt;
            if (ex.Lifetime <= 0f)
            {
                _explosions.RemoveAt(i);
            }
        }
    }

    private void UpdatePickups()
    {
        Player player = Player;

        for (int i = _pickups.Count - 1; i >= 0; i--)
        {
            Pickup pickup = _pickups[i];
            if (!pickup.Collected && Phys.CirclesOverlap(player.Position, player.Radius + 6f, pickup.Position, pickup.Radius))
            {
                pickup.Collected = true;
                switch (pickup.Type)
                {
                    case PickupType.Medkit:
                        player.Heal(pickup.Value);
                        break;
                    case PickupType.Ammo:
                        player.GiveAmmoForAllWeapons();
                        break;
                    case PickupType.Armor:
                        player.GiveArmor(pickup.Value);
                        break;
                    case PickupType.Credits:
                        player.Credits += pickup.Value;
                        break;
                    case PickupType.Adrenaline:
                        player.AddAdrenaline(pickup.Value);
                        break;
                }

                player.NotifyPickup();
                _sound.PlayWorld(WorldSound.Pickup, player.Position, pickup.Position, 320f, true);
            }

            if (pickup.Collected)
            {
                _pickups.RemoveAt(i);
            }
        }
    }

    private void UpdateScrapPiles()
    {
        Player player = Player;
        for (int i = _scrapPiles.Count - 1; i >= 0; i--)
        {
            ScrapPile scrap = _scrapPiles[i];
            if (!scrap.Collected && Phys.CirclesOverlap(player.Position, player.Radius + 8f, scrap.Position, scrap.Radius))
            {
                scrap.Collected = true;
                player.GiveScrap(scrap.ScrapAmount);
                player.NotifyPickup();
                _sound.PlayWorld(WorldSound.Pickup, player.Position, scrap.Position, 320f, true);
            }

            if (scrap.Collected)
            {
                _scrapPiles.RemoveAt(i);
            }
        }
    }

    private void UpdateCamera(Size clientSize)
    {
        Vector2 target = Player.Position;
        Vector2 shake = Vector2.Zero;
        if (_screenShakeTimer > 0f && _screenShakePower > 0.01f)
        {
            float kickFactor = MathF.Min(1f, _damagePulse * 1.4f);
            Vector2 randomShake = new Vector2(((float)_rng.NextDouble() * 2f - 1f), ((float)_rng.NextDouble() * 2f - 1f));
            shake = randomShake * _screenShakePower + _damageKick * 0.08f * kickFactor;
        }

        Camera = new Vector2(
            Math.Clamp(target.X - clientSize.Width / 2f + shake.X, 0f, Math.Max(0f, Map.PixelWidth - clientSize.Width)),
            Math.Clamp(target.Y - clientSize.Height / 2f + shake.Y, 0f, Math.Max(0f, Map.PixelHeight - clientSize.Height)));
    }

    private Vector2 ScreenToWorld(Point screen, Size clientSize)
    {
        return new Vector2(screen.X + Camera.X, screen.Y + Camera.Y);
    }

    public void Draw(Graphics g, Size clientSize, InputState input)
    {
        bool fast = _renderFps < 55f || _updateFps < 55f;

        g.Clear(Color.FromArgb(10, 12, 16));

        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.TextRenderingHint = fast ? TextRenderingHint.SingleBitPerPixelGridFit : TextRenderingHint.ClearTypeGridFit;

        DrawWorld(g, clientSize);

        if (!fast)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        DrawDayNightOverlay(g, clientSize);
        DrawScreenShader(g, clientSize, fast);
        DrawCrosshairOverlay(g);

        switch (_phase)
        {
            case GamePhase.Title:
                DrawTitle(g, clientSize);
                break;
            case GamePhase.MultiplayerMenu:
                DrawWorldTint(g, clientSize, 150);
                DrawMultiplayerMenu(g, clientSize);
                break;
            case GamePhase.LanBrowser:
                DrawWorldTint(g, clientSize, 150);
                DrawLanBrowser(g, clientSize);
                break;
            case GamePhase.Settings:
                DrawWorldTint(g, clientSize, 150);
                DrawSettings(g, clientSize);
                break;
            case GamePhase.HostSetup:
                DrawWorldTint(g, clientSize, 150);
                DrawHostSetup(g, clientSize);
                break;
            case GamePhase.JoinSetup:
                DrawWorldTint(g, clientSize, 150);
                DrawJoinSetup(g, clientSize);
                break;
            case GamePhase.Playing:
                DrawHud(g, clientSize);
                break;
            case GamePhase.Paused:
                DrawHud(g, clientSize);
                DrawWorldTint(g, clientSize, 145);
                DrawPauseMenu(g, clientSize);
                break;
            case GamePhase.GameOver:
                DrawHud(g, clientSize);
                DrawWorldTint(g, clientSize, 165);
                DrawGameOver(g, clientSize);
                break;
        }

        if (_announcementTimer > 0f)
        {
            DrawAnnouncement(g, clientSize);
        }
    }
    private void DrawWorld(Graphics g, Size clientSize)
    {
        _worldDrawBounds = BuildWorldDrawBounds(clientSize, 120f);

        g.TranslateTransform(-Camera.X, -Camera.Y);

        Rectangle view = new Rectangle((int)Camera.X, (int)Camera.Y, clientSize.Width, clientSize.Height);
        DrawFloor(g, view);
        DrawTiles(g, view);
        DrawScrap(g);
        DrawPickups(g);
        DrawTurrets(g);
        DrawBullets(g);
        DrawGrenades(g);
        DrawExplosions(g);
        DrawZombies(g);
        DrawSupportPlacementPreview(g, clientSize);
        DrawPlayers(g);
        DrawRemotePlayers(g);

        g.ResetTransform();
    }

    private void DrawFloor(Graphics g, Rectangle view)
    {
        float darkness = _dayNight.Darkness;
        Color top = Mix(Color.FromArgb(28, 38, 48), Color.FromArgb(10, 14, 22), darkness);
        Color bottom = Mix(Color.FromArgb(14, 18, 24), Color.FromArgb(4, 6, 10), darkness);
        using LinearGradientBrush backdrop = new LinearGradientBrush(view, top, bottom, LinearGradientMode.Vertical);
        g.FillRectangle(backdrop, view);

        int grid = TileMap.TileSize;
        Color gridColor = Mix(Color.FromArgb(36, 90, 112, 132), Color.FromArgb(26, 70, 90, 130), darkness);
        using Pen pen = new Pen(gridColor);
        for (int x = view.Left / grid * grid; x < view.Right + grid; x += grid)
        {
            g.DrawLine(pen, x, view.Top, x, view.Bottom);
        }

        for (int y = view.Top / grid * grid; y < view.Bottom + grid; y += grid)
        {
            g.DrawLine(pen, view.Left, y, view.Right, y);
        }

        int pulseAlpha = (int)(18f + (1f - darkness) * 10f + 10f * (0.5f + 0.5f * MathF.Sin(_backgroundPulse * 1.4f)));
        Color pulseColor = _dayNight.IsNight ? Color.FromArgb(pulseAlpha, 110, 126, 255) : Color.FromArgb(pulseAlpha, 80, 220, 255);
        using Pen pulse = new Pen(pulseColor, 1f);
        for (int y = view.Top - (view.Top % (grid * 4)); y < view.Bottom + grid * 4; y += grid * 4)
        {
            g.DrawLine(pulse, view.Left, y, view.Right, y);
        }
    }

    private void DrawTiles(Graphics g, Rectangle view)
    {
        foreach ((RectangleF rect, int value) in Map.GetDrawTiles(view))
        {
            if (value == 0)
            {
                continue;
            }

            Color baseColor = value == 1 ? Color.FromArgb(74, 84, 96) : Color.FromArgb(132, 96, 60);
            Color topColor = Mix(baseColor, Color.White, 0.12f);
            Color bottomColor = Mix(baseColor, Color.Black, 0.22f);
            using LinearGradientBrush fill = new LinearGradientBrush(rect, topColor, bottomColor, LinearGradientMode.Vertical);
            g.FillRectangle(fill, rect);
            using Pen border = new Pen(Color.FromArgb(160, Mix(baseColor, Color.Black, 0.35f)), 1f);
            g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
            using Pen bevel = new Pen(Color.FromArgb(70, Color.White));
            g.DrawLine(bevel, rect.Left + 2f, rect.Top + 2f, rect.Right - 2f, rect.Top + 2f);
            g.DrawLine(bevel, rect.Left + 2f, rect.Top + 2f, rect.Left + 2f, rect.Bottom - 2f);
        }
    }

    private void DrawScrap(Graphics g)
    {
        foreach (ScrapPile scrap in _scrapPiles)
        {
            if (!IsVisible(scrap.Position, scrap.Radius + 16f))
            {
                continue;
            }

            float r = scrap.Radius;
            using SolidBrush glowBrush = new SolidBrush(Color.FromArgb(42, 170, 190, 210));
            using SolidBrush fill = new SolidBrush(Color.FromArgb(190, 142, 154, 166));
            using Pen border = new Pen(Color.FromArgb(150, 220, 228, 236));
            g.FillEllipse(glowBrush, scrap.Position.X - r - 4f, scrap.Position.Y - r - 4f, (r + 4f) * 2f, (r + 4f) * 2f);
            g.FillEllipse(fill, scrap.Position.X - r, scrap.Position.Y - r, r * 2f, r * 2f);
            g.DrawEllipse(border, scrap.Position.X - r, scrap.Position.Y - r, r * 2f, r * 2f);
            DrawTagBadge(g, new RectangleF(scrap.Position.X - 12f, scrap.Position.Y - 7f, 24f, 14f), "SCR", Color.FromArgb(88, 102, 116));
        }
    }

    private void DrawPickups(Graphics g)
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
                _ => Color.Magenta
            };

            float r = pickup.Radius;
            using SolidBrush glow = new SolidBrush(Color.FromArgb(40, color));
            using SolidBrush fill = new SolidBrush(Color.FromArgb(220, color));
            using Pen border = new Pen(Color.FromArgb(100, 255, 255, 255));
            g.FillEllipse(glow, pickup.Position.X - r - 4f, pickup.Position.Y - r - 4f, (r + 4f) * 2f, (r + 4f) * 2f);
            g.FillEllipse(fill, pickup.Position.X - r, pickup.Position.Y - r, r * 2f, r * 2f);
            g.DrawEllipse(border, pickup.Position.X - r, pickup.Position.Y - r, r * 2f, r * 2f);
        }
    }

    private void DrawTurrets(Graphics g)
    {
        foreach (Turret turret in _turrets)
        {
            if (!IsVisible(turret.Position, turret.Radius + 24f))
            {
                continue;
            }

            float r = turret.Radius;
            RectangleF bodyRect = new RectangleF(turret.Position.X - r, turret.Position.Y - r, r * 2f, r * 2f);
            DrawShadow(g, new RectangleF(bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height * 0.72f), r * 0.55f, 34, 4f);

            using SolidBrush body = new SolidBrush(Color.FromArgb(205, 38, 108, 124));
            using Pen outline = new Pen(Color.FromArgb(160, 205, 230, 236), 1.2f);
            g.FillEllipse(body, bodyRect);
            g.DrawEllipse(outline, bodyRect);

            Vector2 tip = turret.Position + Phys.FromAngle(turret.AimAngle) * 18f;
            using Pen barrel = new Pen(Color.FromArgb(230, 225, 232, 238), 3.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(barrel, turret.Position.X, turret.Position.Y, tip.X, tip.Y);
        }
    }

    private void DrawBullets(Graphics g)
    {
        foreach (Bullet bullet in _bullets)
        {
            if (!IsVisible(bullet.Position, bullet.Radius + 8f))
            {
                continue;
            }

            float r = bullet.Radius;
            using SolidBrush glow = new SolidBrush(Color.FromArgb(52, bullet.Tint));
            using SolidBrush fill = new SolidBrush(Color.FromArgb(235, Mix(bullet.Tint, Color.White, 0.1f)));
            g.FillEllipse(glow, bullet.Position.X - r - 2f, bullet.Position.Y - r - 2f, (r + 2f) * 2f, (r + 2f) * 2f);
            g.FillEllipse(fill, bullet.Position.X - r, bullet.Position.Y - r, r * 2f, r * 2f);
        }

        foreach (Bullet bullet in _clientPredictedBullets)
        {
            if (!IsVisible(bullet.Position, bullet.Radius + 8f))
            {
                continue;
            }

            float r = bullet.Radius;
            using SolidBrush glow = new SolidBrush(Color.FromArgb(34, bullet.Tint));
            using SolidBrush fill = new SolidBrush(Color.FromArgb(150, Mix(bullet.Tint, Color.White, 0.18f)));
            g.FillEllipse(glow, bullet.Position.X - r - 2f, bullet.Position.Y - r - 2f, (r + 2f) * 2f, (r + 2f) * 2f);
            g.FillEllipse(fill, bullet.Position.X - r, bullet.Position.Y - r, r * 2f, r * 2f);
        }
    }

    private void DrawGrenades(Graphics g)
    {
        foreach (Grenade grenade in _grenades)
        {
            if (!IsVisible(grenade.Position, grenade.Radius + 14f))
            {
                continue;
            }

            float r = grenade.Radius;
            RectangleF rect = new RectangleF(grenade.Position.X - r, grenade.Position.Y - r, r * 2f, r * 2f);
            DrawShadow(g, new RectangleF(rect.X, rect.Y, rect.Width, rect.Height * 0.65f), r * 0.45f, 30, 4f);

            using SolidBrush fill = new SolidBrush(Color.FromArgb(220, 188, 88, 44));
            using Pen border = new Pen(Color.FromArgb(150, 255, 220, 196));
            g.FillEllipse(fill, rect);
            g.DrawEllipse(border, rect);
        }
    }

    private void DrawExplosions(Graphics g)
    {
        foreach (Explosion ex in _explosions)
        {
            if (!IsVisible(ex.Position, ex.Radius + 12f))
            {
                continue;
            }

            float alpha = ex.MaxLifetime <= 0f ? 0f : ex.Lifetime / ex.MaxLifetime;
            using SolidBrush glow = new SolidBrush(Color.FromArgb((int)(alpha * 58f), ex.Tint));
            using Pen pen = new Pen(Color.FromArgb((int)(alpha * 235), Mix(ex.Tint, Color.White, 0.08f)), 2.8f);
            g.FillEllipse(glow, ex.Position.X - ex.Radius, ex.Position.Y - ex.Radius, ex.Radius * 2f, ex.Radius * 2f);
            g.DrawEllipse(pen, ex.Position.X - ex.Radius, ex.Position.Y - ex.Radius, ex.Radius * 2f, ex.Radius * 2f);
        }
    }

    private void DrawZombies(Graphics g)
    {
        foreach (Zombie enemy in _zombies)
        {
            if (!IsVisible(enemy.Position, enemy.Radius + 22f))
            {
                continue;
            }

            float r = enemy.Radius;
            RectangleF bodyRect = new RectangleF(enemy.Position.X - r, enemy.Position.Y - r, r * 2f, r * 2f);
            DrawShadow(g, new RectangleF(bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height * 0.72f), r * 0.55f, 28, 4f);

            using SolidBrush body = new SolidBrush(Color.FromArgb(220, enemy.Tint));
            using Pen outline = new Pen(Color.FromArgb(105, 255, 255, 255), 1.1f);
            g.FillEllipse(body, bodyRect);
            g.DrawEllipse(outline, bodyRect);

            float eyeRadius = Math.Max(1.8f, r * 0.12f);
            g.FillEllipse(Brushes.WhiteSmoke, enemy.Position.X - r * 0.34f, enemy.Position.Y - r * 0.16f, eyeRadius * 2f, eyeRadius * 2f);
            g.FillEllipse(Brushes.WhiteSmoke, enemy.Position.X + r * 0.02f, enemy.Position.Y - r * 0.16f, eyeRadius * 2f, eyeRadius * 2f);

            float hp = enemy.Health / (float)Math.Max(1, enemy.MaxHealth);
            DrawBar(g, new RectangleF(enemy.Position.X - r, enemy.Position.Y - r - 10f, r * 2f, 6f), hp, Color.FromArgb(214, 68, 82), string.Empty, string.Empty);
        }
    }

    private void DrawSupportPlacementPreview(Graphics g, Size clientSize)
    {
        if (_players.Count == 0 || !IsSupportEquipped())
        {
            return;
        }

        Player player = _players[0];
        if (!player.IsAlive)
        {
            return;
        }

        Vector2 mouseWorld = ScreenToWorld(_lastMouseScreen, clientSize);
        switch (_equippedSupportRawIndex)
        {
            case 6:
                if (TryGetBarricadePreview(mouseWorld, out Vector2 barricadeCenter))
                {
                    using SolidBrush fill = new SolidBrush(Color.FromArgb(68, 110, 220, 140));
                    using Pen border = new Pen(Color.FromArgb(220, 170, 250, 190), 2f);
                    RectangleF rect = new RectangleF(barricadeCenter.X - TileMap.TileSize * 0.5f, barricadeCenter.Y - TileMap.TileSize * 0.5f, TileMap.TileSize, TileMap.TileSize);
                    g.FillRectangle(fill, rect);
                    g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
                }
                else
                {
                    (int tx, int ty) = Map.ToTile(mouseWorld);
                    Vector2 center = Map.TileCenter(tx, ty);
                    using SolidBrush fill = new SolidBrush(Color.FromArgb(58, 220, 86, 86));
                    using Pen border = new Pen(Color.FromArgb(220, 255, 140, 140), 2f);
                    RectangleF rect = new RectangleF(center.X - TileMap.TileSize * 0.5f, center.Y - TileMap.TileSize * 0.5f, TileMap.TileSize, TileMap.TileSize);
                    g.FillRectangle(fill, rect);
                    g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
                }
                break;
            case 8:
                if (TryGetTurretPreview(player, out Vector2 turretPos))
                {
                    using SolidBrush fill = new SolidBrush(Color.FromArgb(62, 92, 180, 210));
                    using Pen border = new Pen(Color.FromArgb(216, 170, 238, 255), 2f);
                    g.FillEllipse(fill, turretPos.X - 16f, turretPos.Y - 16f, 32f, 32f);
                    g.DrawEllipse(border, turretPos.X - 16f, turretPos.Y - 16f, 32f, 32f);
                }
                else
                {
                    Vector2 fallbackTurretPos = player.Position + Phys.FromAngle(player.AimAngle) * 42f;
                    using SolidBrush fill = new SolidBrush(Color.FromArgb(56, 220, 86, 86));
                    using Pen border = new Pen(Color.FromArgb(216, 255, 140, 140), 2f);
                    g.FillEllipse(fill, fallbackTurretPos.X - 16f, fallbackTurretPos.Y - 16f, 32f, 32f);
                    g.DrawEllipse(border, fallbackTurretPos.X - 16f, fallbackTurretPos.Y - 16f, 32f, 32f);
                }
                break;
        }
    }

    private void DrawHeldItem(Graphics g, Player player, Vector2 hand, Vector2 forward)
    {
        switch (GetSelectedHotbarRawIndex())
        {
            case 4:
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(220, 98, 106, 116)))
                using (Pen border = new Pen(Color.FromArgb(210, 226, 234, 242), 1.4f))
                {
                    RectangleF rect = new RectangleF(hand.X - 7f, hand.Y - 6f, 14f, 12f);
                    g.FillRectangle(fill, rect);
                    g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
                }
                break;
            case 5:
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(226, 192, 148, 86)))
                using (Pen border = new Pen(Color.FromArgb(220, 255, 238, 202), 1.4f))
                {
                    g.FillEllipse(fill, hand.X - 6f, hand.Y - 6f, 12f, 12f);
                    g.DrawEllipse(border, hand.X - 6f, hand.Y - 6f, 12f, 12f);
                }
                break;
            case 6:
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(220, 116, 84, 54)))
                using (Pen border = new Pen(Color.FromArgb(210, 238, 220, 200), 1.6f))
                {
                    RectangleF rect = new RectangleF(hand.X - 8f + forward.X * 2f, hand.Y - 6f + forward.Y * 2f, 16f, 12f);
                    g.FillRectangle(fill, rect);
                    g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
                }
                break;
            case 7:
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(226, 168, 104, 48)))
                using (Pen border = new Pen(Color.FromArgb(220, 250, 232, 190), 1.4f))
                {
                    g.FillEllipse(fill, hand.X - 6f, hand.Y - 6f, 12f, 12f);
                    g.DrawEllipse(border, hand.X - 6f, hand.Y - 6f, 12f, 12f);
                    g.DrawLine(border, hand.X + 2f, hand.Y - 2f, hand.X + 7f, hand.Y - 8f);
                }
                break;
            case 8:
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(226, 76, 122, 142)))
                using (Pen border = new Pen(Color.FromArgb(220, 216, 246, 252), 1.4f))
                {
                    RectangleF rect = new RectangleF(hand.X - 7f, hand.Y - 7f, 14f, 14f);
                    g.FillEllipse(fill, rect);
                    g.DrawEllipse(border, rect);
                    g.DrawLine(border, hand.X, hand.Y, hand.X + forward.X * 12f, hand.Y + forward.Y * 12f);
                }
                break;
            case 9:
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(226, 138, 88, 44)))
                using (Pen border = new Pen(Color.FromArgb(220, 255, 228, 182), 1.4f))
                {
                    RectangleF rect = new RectangleF(hand.X - 7f, hand.Y - 9f, 14f, 18f);
                    g.FillRectangle(fill, rect);
                    g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
                    g.DrawLine(border, hand.X - 3f, hand.Y - 12f, hand.X + 3f, hand.Y - 12f);
                }
                break;
        }
    }

    private void DrawPlayers(Graphics g)
    {
        foreach (Player player in _players)
        {
            if (!IsVisible(player.Position, player.Radius + 26f))
            {
                continue;
            }

            float r = player.Radius;
            RectangleF bodyRect = new RectangleF(player.Position.X - r, player.Position.Y - r, r * 2f, r * 2f);
            DrawShadow(g, new RectangleF(bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height * 0.7f), r * 0.6f, 34, 4f);

            if (player.IsOverdriveActive)
            {
                using SolidBrush aura = new SolidBrush(Color.FromArgb(34, 255, 160, 72));
                g.FillEllipse(aura, player.Position.X - r - 7f, player.Position.Y - r - 7f, (r + 7f) * 2f, (r + 7f) * 2f);
            }

            using SolidBrush body = new SolidBrush(Color.FromArgb(228, player.Accent));
            using Pen outline = new Pen(Color.FromArgb(120, 255, 255, 255), 1.2f);
            g.FillEllipse(body, bodyRect);
            g.DrawEllipse(outline, bodyRect);

            using SolidBrush visor = new SolidBrush(Color.FromArgb(210, 236, 242, 248));
            g.FillEllipse(visor, player.Position.X - r * 0.38f, player.Position.Y - r * 0.2f, r * 0.72f, r * 0.38f);

            Vector2 forward = Phys.FromAngle(player.AimAngle);
            Vector2 tip = player.Position + forward * 24f;
            Vector2 muzzleBase = player.Position + forward * (r * 0.45f);
            bool localPlayer = _players.Count > 0 && ReferenceEquals(player, _players[0]);
            int selectedRawIndex = localPlayer ? GetSelectedHotbarRawIndex() : player.SelectedWeaponIndex;
            bool drawWeapon = !localPlayer || (IsWeaponRawIndex(selectedRawIndex) && player.SelectedWeaponIndex == selectedRawIndex);
            if (drawWeapon)
            {
                using Pen pen = new Pen(player.IsOverdriveActive ? Color.FromArgb(255, 190, 92) : Color.FromArgb(236, 242, 252), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, muzzleBase.X, muzzleBase.Y, tip.X, tip.Y);
            }
            else
            {
                DrawHeldItem(g, player, tip, forward);
            }

            RectangleF nameRect = new RectangleF(player.Position.X - 30f, player.Position.Y - r - 22f, 60f, 16f);
            DrawGlassPanel(g, nameRect, 8f, player.Accent, 204);
            SizeF callsignSize = g.MeasureString(player.Callsign, _tinyFont);
            g.DrawString(player.Callsign, _tinyFont, Brushes.White, nameRect.X + (nameRect.Width - callsignSize.Width) / 2f, nameRect.Y + 1f);
        }
    }

    private void DrawRemotePlayers(Graphics g)
    {
        foreach (RemotePlayerView remote in _remotePlayers)
        {
            if (!IsVisible(remote.Position, 40f))
            {
                continue;
            }

            float r = 16f;
            RectangleF bodyRect = new RectangleF(remote.Position.X - r, remote.Position.Y - r, r * 2f, r * 2f);
            DrawShadow(g, new RectangleF(bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height * 0.7f), r * 0.5f, 24, 4f);

            using SolidBrush body = new SolidBrush(Color.FromArgb(210, remote.Accent));
            using Pen outline = new Pen(Color.FromArgb(100, 255, 255, 255), 1.1f);
            g.FillEllipse(body, bodyRect);
            g.DrawEllipse(outline, bodyRect);

            Vector2 tip = remote.Position + Phys.FromAngle(remote.AimAngle) * 20f;
            using Pen barrel = new Pen(Color.FromArgb(200, 236, 242, 250), 3.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(barrel, remote.Position.X, remote.Position.Y, tip.X, tip.Y);

            RectangleF nameRect = new RectangleF(remote.Position.X - 28f, remote.Position.Y - r - 20f, 56f, 14f);
            DrawGlassPanel(g, nameRect, 7f, remote.Accent, 196);
            SizeF nameSize = g.MeasureString(remote.Callsign, _tinyFont);
            g.DrawString(remote.Callsign, _tinyFont, Brushes.WhiteSmoke, nameRect.X + (nameRect.Width - nameSize.Width) / 2f, nameRect.Y + 1f);
        }
    }

    private void DrawCrosshairOverlay(Graphics g)
    {
        int x = _lastMouseScreen.X;
        int y = _lastMouseScreen.Y;
        Color accent = Player.Accent;

        using Pen outer = new Pen(Color.FromArgb(180, 6, 8, 10), 3f);
        using Pen inner = new Pen(Mix(accent, Color.White, 0.12f), 1.5f);
        g.DrawEllipse(outer, x - 8, y - 8, 16, 16);
        g.DrawEllipse(inner, x - 8, y - 8, 16, 16);
        g.DrawLine(outer, x - 14, y, x - 5, y);
        g.DrawLine(outer, x + 5, y, x + 14, y);
        g.DrawLine(outer, x, y - 14, x, y - 5);
        g.DrawLine(outer, x, y + 5, x, y + 14);
        g.DrawLine(inner, x - 14, y, x - 5, y);
        g.DrawLine(inner, x + 5, y, x + 14, y);
        g.DrawLine(inner, x, y - 14, x, y - 5);
        g.DrawLine(inner, x, y + 5, x, y + 14);

        using SolidBrush center = new SolidBrush(Color.FromArgb(228, Color.White));
        g.FillEllipse(center, x - 2f, y - 2f, 4f, 4f);
    }

    private string FormatPhaseTimer(float value)
    {
        int seconds = Math.Max(0, (int)MathF.Ceiling(value));
        int minutes = seconds / 60;
        int remain = seconds % 60;
        return $"{minutes:00}:{remain:00}";
    }


    private void DrawDayNightOverlay(Graphics g, Size clientSize)
    {
        if (_phase != GamePhase.Playing && _phase != GamePhase.Paused && _phase != GamePhase.GameOver)
        {
            return;
        }

        int alpha = (int)(18f + _dayNight.Darkness * 96f);
        using SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 10, 14, 22));
        g.FillRectangle(b, new Rectangle(0, 0, clientSize.Width, clientSize.Height));

        if (_players.Count == 0)
        {
            return;
        }

        Player player = _players[0];
        float screenX = player.Position.X - Camera.X;
        float screenY = player.Position.Y - Camera.Y;
        int glowLayers = _useFastUi ? 4 : 6;
        float maxRadius = _dayNight.IsNight ? 210f : 132f;
        Color glowColor = _dayNight.IsNight ? Color.FromArgb(92, 126, 210) : Color.FromArgb(255, 182, 92);

        for (int i = glowLayers; i >= 1; i--)
        {
            float t = i / (float)glowLayers;
            float radius = maxRadius * t;
            int glowAlpha = (int)((_dayNight.IsNight ? 20f : 10f) * t);
            using SolidBrush glow = new SolidBrush(Color.FromArgb(glowAlpha, glowColor));
            g.FillEllipse(glow, screenX - radius, screenY - radius, radius * 2f, radius * 2f);
        }
    }

    private void DrawScreenShader(Graphics g, Size clientSize, bool fast)
    {
        int scanAlpha = (_phase == GamePhase.Playing || _phase == GamePhase.Paused || _phase == GamePhase.GameOver)
            ? (int)(5f + _dayNight.Darkness * 8f)
            : 6;
        Color scanColor = (_phase == GamePhase.Playing || _phase == GamePhase.Paused || _phase == GamePhase.GameOver) && _dayNight.IsNight
            ? Color.FromArgb(scanAlpha, 70, 86, 122)
            : Color.FromArgb(scanAlpha, 62, 92, 112);
        int lineStep = fast ? 6 : 4;
        using (Pen scan = new Pen(scanColor, 1f))
        {
            for (int y = 0; y < clientSize.Height; y += lineStep)
            {
                g.DrawLine(scan, 0, y, clientSize.Width, y);
            }
        }

        int layers = fast ? 4 : 7;
        int thickness = fast ? 10 : 12;
        int extra = (_phase == GamePhase.Playing || _phase == GamePhase.Paused || _phase == GamePhase.GameOver)
            ? (int)(_dayNight.Darkness * 8f)
            : 0;

        for (int i = 0; i < layers; i++)
        {
            int inset = i * (thickness - 2);
            int width = clientSize.Width - inset * 2;
            int height = clientSize.Height - inset * 2;
            if (width <= 0 || height <= 0)
            {
                break;
            }

            int alpha = 8 + i * 3 + extra;
            using Pen vignette = new Pen(Color.FromArgb(alpha, 0, 0, 0), thickness);
            g.DrawRectangle(vignette, inset, inset, Math.Max(1, width - 1), Math.Max(1, height - 1));
        }
    }


    private void DrawCenterTimer(Graphics g, Size clientSize, float s)
    {
        float width = 252f * s;
        float height = 74f * s;
        RectangleF rect = new RectangleF((clientSize.Width - width) / 2f, 16f, width, height);
        Color accent = _dayNight.IsNight ? Color.FromArgb(88, 108, 206) : Color.FromArgb(210, 148, 66);
        DrawGlassPanel(g, rect, 16f, accent, 220);

        string phase = _dayNight.IsNight ? "NIGHT" : "DAY";
        string timer = FormatPhaseTimer(_dayNight.RemainingTime);
        string waveText = _dayNight.IsNight
            ? (_waveLive ? $"Wave {_waveNumber}   Zombies {_waveSpawned}/{_waveTarget}" : $"Wave {_waveNumber}   Clear")
            : "Safe period   No zombie spawns";

        g.DrawString(phase, _tinyFont, Brushes.Gainsboro, rect.X + 12f, rect.Y + 8f);

        SizeF timerSize = g.MeasureString(timer, _menuFont);
        g.DrawString(timer, _menuFont, Brushes.White, rect.X + (rect.Width - timerSize.Width) / 2f, rect.Y + 14f);

        SizeF waveSize = g.MeasureString(waveText, _tinyFont);
        g.DrawString(waveText, _tinyFont, Brushes.Gainsboro, rect.X + (rect.Width - waveSize.Width) / 2f, rect.Bottom - 22f);

        RectangleF barRect = new RectangleF(rect.X + 12f, rect.Bottom - 8f, rect.Width - 24f, 4f);
        using SolidBrush bg = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
        using SolidBrush fill = new SolidBrush(Color.FromArgb(220, accent));
        g.FillRectangle(bg, barRect);
        g.FillRectangle(fill, barRect.X, barRect.Y, barRect.Width * (1f - _dayNight.PhaseProgress), barRect.Height);
    }
    private void DrawPerformanceHud(Graphics g, Size clientSize, float s)
    {
        int width = (int)(184 * s);
        int height = (int)(118 * s);
        float top = 140f * s;
        RectangleF rect = new RectangleF(clientSize.Width - width - 12f, top, width, height);
        DrawGlassPanel(g, rect, 14f, Color.FromArgb(92, 110, 154), 210);
        g.DrawString("Performance", _smallFont, Brushes.White, rect.Left + 10f, rect.Top + 7f);

        if (_showFpsHud)
        {
            string fpsText = $"FPS {_renderFps:0}  |  UPS {_updateFps:0}";
            g.DrawString(fpsText, _tinyFont, Brushes.WhiteSmoke, rect.Left + 10f, rect.Top + 24f);
        }

        if (_showPerfHud)
        {
            string msText = $"Frame {_frameMs:0.00} ms  Update {_updateMs:0.00}  Draw {_renderMs:0.00}";
            g.DrawString(msText, _tinyFont, Brushes.Gainsboro, rect.Left + 10f, rect.Top + 38f);

            string counts = $"Z {_zombies.Count}  B {_bullets.Count}  G {_grenades.Count}  T {_turrets.Count}";
            g.DrawString(counts, _tinyFont, Brushes.Gainsboro, rect.Left + 10f, rect.Top + 52f);

            RectangleF graph = new RectangleF(rect.Left + 10f, rect.Top + 68f, rect.Width - 20f, rect.Height - 78f);
            using SolidBrush bg = new SolidBrush(Color.FromArgb(210, 12, 16, 22));
            using Pen guide16 = new Pen(Color.FromArgb(90, 214, 148, 66), 1f);
            using Pen guide8 = new Pen(Color.FromArgb(70, 80, 170, 255), 1f);
            using Pen line = new Pen(Color.FromArgb(220, Player.Accent), _useFastUi ? 1f : 1.6f);
            g.FillRectangle(bg, graph);

            if (!_useFastUi)
            {
                float y16 = graph.Bottom - MathF.Min(graph.Height - 2f, 16.67f / 22f * graph.Height);
                float y8 = graph.Bottom - MathF.Min(graph.Height - 2f, 8.33f / 22f * graph.Height);
                g.DrawLine(guide16, graph.Left, y16, graph.Right, y16);
                g.DrawLine(guide8, graph.Left, y8, graph.Right, y8);
            }

            int step = _useFastUi ? 2 : 1;
            PointF[] points = new PointF[(_frameGraph.Length + step - 1) / step];
            int point = 0;
            for (int i = 0; i < _frameGraph.Length; i += step)
            {
                int index = (_frameGraphHead + i) % _frameGraph.Length;
                float sample = MathF.Min(22f, _frameGraph[index]);
                float x = graph.Left + point * (graph.Width / Math.Max(1, points.Length - 1));
                float y = graph.Bottom - sample / 22f * graph.Height;
                points[point++] = new PointF(x, y);
            }

            if (points.Length > 1)
            {
                g.DrawLines(line, points);
            }
        }

        string renderer = _rendererLabel.Length > 28 ? _rendererLabel.Substring(0, 28) + "..." : _rendererLabel;
        g.DrawString(renderer, _tinyFont, Brushes.Silver, rect.Left + 10f, rect.Bottom - 14f);
    }

    private void DrawHud(Graphics g, Size clientSize)
    {
        float s = GetUiScaleValue();
        DrawSingleHud(g, clientSize, s);

        if (_inventoryOpen)
        {
            DrawInventoryOverlay(g, clientSize, s);
        }
    }
    private void DrawSingleHud(Graphics g, Size clientSize, float s)
    {
        Player player = Player;

        RectangleF rect = new RectangleF(12f, 12f, 340f * s, 132f * s);
        DrawGlassPanel(g, rect, 14f, player.Accent, 210);

        string phase = _dayNight.IsNight ? "NIGHT" : "DAY";
        string state = _dayNight.IsNight ? (_waveLive ? "LIVE" : "CLEAR") : "SAFE";
        string timer = FormatPhaseTimer(_dayNight.RemainingTime);

        g.DrawString($"{phase}  WAVE {_waveNumber}  {timer}", _hudFont, Brushes.White, rect.Left + 12f, rect.Top + 8f);
        DrawTagBadge(g, new RectangleF(rect.Right - 62f, rect.Top + 8f, 50f, 16f), state, player.Accent);

        DrawBar(
            g,
            new RectangleF(rect.Left + 12f, rect.Top + 34f, rect.Width - 24f, 14f),
            player.Health / (float)Math.Max(1, player.MaxHealth),
            Color.FromArgb(214, 68, 82),
            "HP",
            $"{player.Health}/{player.MaxHealth}"
        );

        DrawBar(
            g,
            new RectangleF(rect.Left + 12f, rect.Top + 54f, rect.Width - 24f, 14f),
            player.Armor / 100f,
            Color.FromArgb(72, 122, 214),
            "AR",
            player.Armor.ToString()
        );

        DrawBar(
            g,
            new RectangleF(rect.Left + 12f, rect.Top + 74f, rect.Width - 24f, 14f),
            player.Adrenaline / Math.Max(1f, player.MaxAdrenaline),
            Color.FromArgb(214, 148, 66),
            "OD",
            $"{(int)player.Adrenaline}%"
        );

        int selectedRawIndex = GetSelectedHotbarRawIndex();
        g.DrawString($"Held  {GetHotbarSlotTitle(player, selectedRawIndex)}", _tinyFont, Brushes.Gainsboro, rect.Left + 12f, rect.Top + 98f);
        g.DrawString($"State  {GetHotbarSlotValue(player, selectedRawIndex)}", _tinyFont, Brushes.Gainsboro, rect.Left + 126f, rect.Top + 98f);

        g.DrawString($"Score {_score}", _tinyFont, Brushes.Gainsboro, rect.Left + 12f, rect.Top + 113f);
        g.DrawString($"Best {_bestScore}", _tinyFont, Brushes.Gainsboro, rect.Left + 106f, rect.Top + 113f);
        g.DrawString($"Credits {player.Credits}", _tinyFont, Brushes.Gainsboro, rect.Left + 206f, rect.Top + 113f);
    }
    private void DrawInventoryOverlay(Graphics g, Size clientSize, float s)
    {
        Player player = Player;

        float width = 520f * s;
        float height = 300f * s;
        RectangleF panel = new RectangleF(
            (clientSize.Width - width) / 2f,
            (clientSize.Height - height) / 2f,
            width,
            height);

        DrawWorldTint(g, clientSize, 110);
        DrawGlassPanel(g, panel, 20f, player.Accent, 220);

        g.DrawString("Inventory", _menuFont, Brushes.White, panel.X + 18f, panel.Y + 14f);
        g.DrawString("Tab - close   F - overdrive   A/D or Left/Right - select", _smallFont, Brushes.Gainsboro, panel.X + 18f, panel.Y + 42f);

        float slotSize = 74f * s;
        float gap = 10f * s;
        float startX = panel.X + 18f;
        float startY = panel.Y + 74f;

        WeaponState rifle = player.Arsenal[0];
        WeaponState smg = player.Arsenal[1];
        WeaponState shotgun = player.Arsenal[2];
        WeaponState carbine = player.Arsenal[3];

        DrawInventoryBigSlot(g, new RectangleF(startX + (slotSize + gap) * 0, startY, slotSize, slotSize), "RIFLE", rifle.Unlocked ? $"{rifle.AmmoInClip}/{rifle.AmmoReserve}" : "LOCK", GetWeaponColor(rifle.Definition), _inventorySelectedIndex == 0);
        DrawInventoryBigSlot(g, new RectangleF(startX + (slotSize + gap) * 1, startY, slotSize, slotSize), "SMG", smg.Unlocked ? $"{smg.AmmoInClip}/{smg.AmmoReserve}" : "LOCK", GetWeaponColor(smg.Definition), _inventorySelectedIndex == 1);
        DrawInventoryBigSlot(g, new RectangleF(startX + (slotSize + gap) * 2, startY, slotSize, slotSize), "SHOTGUN", shotgun.Unlocked ? $"{shotgun.AmmoInClip}/{shotgun.AmmoReserve}" : "LOCK", GetWeaponColor(shotgun.Definition), _inventorySelectedIndex == 2);
        DrawInventoryBigSlot(g, new RectangleF(startX + (slotSize + gap) * 3, startY, slotSize, slotSize), "CARBINE", carbine.Unlocked ? $"{carbine.AmmoInClip}/{carbine.AmmoReserve}" : "LOCK", GetWeaponColor(carbine.Definition), _inventorySelectedIndex == 3);

        DrawInventoryBigSlot(g, new RectangleF(startX + (slotSize + gap) * 0, startY + slotSize + 16f, slotSize, slotSize), "SCRAP", player.Scrap.ToString(), Color.FromArgb(104, 112, 120), _inventorySelectedIndex == 4);
        DrawInventoryBigSlot(g, new RectangleF(startX + (slotSize + gap) * 1, startY + slotSize + 16f, slotSize, slotSize), "BARR", player.BarricadeKits.ToString(), Color.FromArgb(124, 88, 54), _inventorySelectedIndex == 5);
        DrawInventoryBigSlot(g, new RectangleF(startX + (slotSize + gap) * 2, startY + slotSize + 16f, slotSize, slotSize), "TURRET", $"{player.TurretCharges}/{player.MaxTurretCharges}", Color.FromArgb(64, 128, 146), _inventorySelectedIndex == 6);
        DrawInventoryBigSlot(g, new RectangleF(startX + (slotSize + gap) * 3, startY + slotSize + 16f, slotSize, slotSize), "OD", $"{(int)player.Adrenaline}%", Color.FromArgb(196, 118, 54), _inventorySelectedIndex == 7);

        RectangleF info = new RectangleF(panel.Right - 170f * s, panel.Y + 74f, 132f * s, 164f * s);
        DrawGlassPanel(g, info, 14f, player.Accent, 190);

        g.DrawString("Player", _smallFont, Brushes.WhiteSmoke, info.X + 10f, info.Y + 10f);
        g.DrawString($"HP: {player.Health}/{player.MaxHealth}", _smallFont, Brushes.Gainsboro, info.X + 10f, info.Y + 34f);
        g.DrawString($"Armor: {player.Armor}", _smallFont, Brushes.Gainsboro, info.X + 10f, info.Y + 54f);
        g.DrawString($"Credits: {player.Credits}", _smallFont, Brushes.Gainsboro, info.X + 10f, info.Y + 74f);
        g.DrawString($"Kills: {player.Kills}", _smallFont, Brushes.Gainsboro, info.X + 10f, info.Y + 94f);
        g.DrawString($"Wave: {_waveNumber}", _smallFont, Brushes.Gainsboro, info.X + 10f, info.Y + 114f);
        g.DrawString(_dayNight.IsNight ? "Phase: Night" : "Phase: Day", _smallFont, Brushes.Gainsboro, info.X + 10f, info.Y + 134f);

        string selectedName = GetInventorySelectedName();
        SizeF selectedSize = g.MeasureString(selectedName, _smallFont);
        g.DrawString(selectedName, _smallFont, Brushes.WhiteSmoke, panel.X + 18f, panel.Bottom - selectedSize.Height - 16f);
    }
    private string GetInventorySelectedName()
    {
        return _inventorySelectedIndex switch
        {
            0 => "Rifle slot",
            1 => "SMG slot",
            2 => "Shotgun slot",
            3 => "Carbine slot",
            4 => "Scrap resources",
            5 => "Barricade kits",
            6 => "Turret charges",
            7 => "Overdrive energy",
            _ => "Inventory"
        };
    }
    private void DrawInventoryBigSlot(Graphics g, RectangleF rect, string title, string value, Color accent, bool selected)
    {
        DrawGlassPanel(g, rect, 12f, selected ? Mix(accent, Color.White, 0.08f) : accent, selected ? 226 : 206);

        if (selected)
        {
            using Pen border = new Pen(Color.FromArgb(220, accent), 2f);
            g.DrawRectangle(border, rect.X + 1f, rect.Y + 1f, rect.Width - 2f, rect.Height - 2f);
        }

        SizeF titleSize = g.MeasureString(title, _tinyFont);
        SizeF valueSize = g.MeasureString(value, _tinyFont);

        g.DrawString(title, _tinyFont, Brushes.WhiteSmoke, rect.X + (rect.Width - titleSize.Width) / 2f, rect.Y + 12f);
        g.DrawString(value, _tinyFont, Brushes.Gainsboro, rect.X + (rect.Width - valueSize.Width) / 2f, rect.Bottom - valueSize.Height - 12f);
    }
    private void DrawTopLeftHud(Graphics g, Size clientSize, float s)
    {
        RectangleF rect = new RectangleF(12f, 12f, 314f * s, 144f * s);
        Player player = Player;
        DrawGlassPanel(g, rect, 16f, player.Accent, 220);

        string phaseText = _dayNight.IsNight ? "NIGHT" : "DAY";
        string combatText = _dayNight.IsNight ? (_waveLive ? "LIVE" : "CLEAR") : "SAFE";
        g.DrawString($"{phaseText} · Wave {_waveNumber} · {combatText}", _hudFont, Brushes.White, rect.Left + 12f, rect.Top + 8f);

        SizeF modeSize = g.MeasureString(GetDifficultyName(), _tinyFont);
        DrawTagBadge(g, new RectangleF(rect.Right - modeSize.Width - 24f, rect.Top + 8f, modeSize.Width + 16f, 15f), GetDifficultyName(), player.Accent);

        DrawBar(g, new RectangleF(rect.Left + 12f, rect.Top + 32f, rect.Width - 24f, 16f), player.Health / (float)Math.Max(1, player.MaxHealth), Color.FromArgb(214, 68, 82), "HP", $"{player.Health}/{player.MaxHealth}");
        DrawBar(g, new RectangleF(rect.Left + 12f, rect.Top + 54f, rect.Width - 24f, 16f), player.Armor / 100f, Color.FromArgb(72, 122, 214), "AR", player.Armor.ToString());
        DrawBar(g, new RectangleF(rect.Left + 12f, rect.Top + 76f, rect.Width - 24f, 16f), player.Adrenaline / Math.Max(1f, player.MaxAdrenaline), Color.FromArgb(214, 148, 66), "OD", $"{(int)player.Adrenaline}%");

        string weaponText = $"{player.Weapon.Name}  {player.CurrentWeapon.AmmoInClip}/{player.CurrentWeapon.AmmoReserve}";
        string scoreText = $"Score {_score}  |  Best {_bestScore}";
        g.DrawString(weaponText, _smallFont, Brushes.Gainsboro, rect.Left + 12f, rect.Top + 101f);

        SizeF scoreSize = g.MeasureString(scoreText, _smallFont);
        g.DrawString(scoreText, _smallFont, Brushes.Gainsboro, rect.Right - scoreSize.Width - 12f, rect.Top + 119f);
    }


    private void DrawMiniMap(Graphics g, Size clientSize, float s)
    {
        int width = (int)(184 * s);
        int height = (int)(120 * s);
        RectangleF rect = new RectangleF(clientSize.Width - width - 12, 12, width, height);
        DrawGlassPanel(g, rect, 14f, Player.Accent, 212);

        RectangleF mapRect = new RectangleF(rect.Left + 8f, rect.Top + 22f, rect.Width - 16f, rect.Height - 30f);
        g.DrawString("Arena", _tinyFont, Brushes.WhiteSmoke, rect.Left + 10f, rect.Top + 6f);

        RebuildMiniMapCache(mapRect.Size);
        if (_miniMapCache is not null)
        {
            g.DrawImageUnscaled(_miniMapCache, (int)mapRect.Left, (int)mapRect.Top);
        }

        float sx = mapRect.Width / Map.PixelWidth;
        float sy = mapRect.Height / Map.PixelHeight;

        foreach (Zombie enemy in _zombies)
        {
            g.FillEllipse(Brushes.IndianRed, mapRect.Left + enemy.Position.X * sx - 2f, mapRect.Top + enemy.Position.Y * sy - 2f, 4f, 4f);
        }

        foreach (RemotePlayerView remote in _remotePlayers)
        {
            using SolidBrush b = new SolidBrush(remote.Accent);
            g.FillEllipse(b, mapRect.Left + remote.Position.X * sx - 3f, mapRect.Top + remote.Position.Y * sy - 3f, 6f, 6f);
        }

        foreach (Player player in _players)
        {
            using SolidBrush b = new SolidBrush(player.Accent);
            g.FillEllipse(b, mapRect.Left + player.Position.X * sx - 3f, mapRect.Top + player.Position.Y * sy - 3f, 6f, 6f);
        }
    }

    private void DrawBottomInventory(Graphics g, Size clientSize, float s)
    {
        Player player = Player;
        int slotSize = (int)(48f * s);
        int slotGap = 6;
        int slots = 9;
        int barWidth = slots * slotSize + (slots - 1) * slotGap + 22;
        int barHeight = slotSize + 42;
        RectangleF area = new RectangleF((clientSize.Width - barWidth) / 2f, clientSize.Height - barHeight - 14f, barWidth, barHeight);

        DrawGlassPanel(g, area, 18f, player.Accent, 216);
        DrawLabelPill(g, $"Credits {player.Credits}", _tinyFont, new RectangleF(area.Left + 10f, area.Top + 8f, 92f, 16f), Color.FromArgb(92, 76, 58), Color.WhiteSmoke);
        DrawLabelPill(g, $"Overdrive {(int)player.Adrenaline}%", _tinyFont, new RectangleF(area.Right - 124f, area.Top + 8f, 114f, 16f), Color.FromArgb(122, 86, 44), Color.WhiteSmoke);

        float x = area.Left + 11f;
        float y = area.Top + 26f;

        for (int displayIndex = 0; displayIndex < 9; displayIndex++)
        {
            int rawIndex = _inventoryLayout[displayIndex];
            bool selected = displayIndex == GetSelectedHotbarDisplayIndex();
            Color slotColor = selected ? Mix(player.Accent, GetHotbarSlotAccent(player, rawIndex), 0.28f) : Mix(GetHotbarSlotAccent(player, rawIndex), Color.FromArgb(34, 38, 46), 0.72f);
            DrawItemSlot(g, Rectangle.Round(new RectangleF(x, y, slotSize, slotSize)), GetHotbarSlotShortTitle(player, rawIndex), GetHotbarSlotValue(player, rawIndex), slotColor, selected, (displayIndex + 1).ToString());
            x += slotSize + slotGap;
        }
    }

    private void DrawRemoteRoster(Graphics g, Size clientSize, float s)
    {
        if (_remotePlayers.Count == 0)
        {
            return;
        }

        int width = (int)(196 * s);
        int lineHeight = 24;
        int height = 34 + lineHeight * _remotePlayers.Count;
        RectangleF rect = new RectangleF(12f, clientSize.Height - height - 92f, width, height);
        DrawGlassPanel(g, rect, 14f, Color.FromArgb(84, 124, 168), 208);
        g.DrawString("Remote roster", _smallFont, Brushes.White, rect.Left + 10f, rect.Top + 8f);

        float y = rect.Top + 26f;
        foreach (RemotePlayerView remote in _remotePlayers)
        {
            using SolidBrush b = new SolidBrush(remote.Accent);
            g.FillEllipse(b, rect.Left + 10f, y + 3f, 12f, 12f);
            g.DrawString(remote.Callsign, _tinyFont, Brushes.WhiteSmoke, rect.Left + 28f, y + 1f);
            g.DrawString($"HP {remote.Health}", _tinyFont, Brushes.Gainsboro, rect.Right - 42f, y + 1f);
            y += lineHeight;
        }
    }

    private void DrawNetworkDebug(Graphics g, Size clientSize, float s)
    {
        string text = _network.StatusText + " | " + _sound.LastMixInfo;
        SizeF size = g.MeasureString(text, _tinyFont);
        RectangleF rect = new RectangleF(clientSize.Width - size.Width - 24f, clientSize.Height - size.Height - 18f, size.Width + 14f, size.Height + 8f);
        DrawGlassPanel(g, rect, 10f, Color.FromArgb(78, 96, 118), 196);
        g.DrawString(text, _tinyFont, Brushes.Gainsboro, rect.X + 7f, rect.Y + 3f);
    }

    private void DrawItemSlot(Graphics g, Rectangle slot, string caption, string value, Color fillColor, bool selected = false, string? hotkey = null)
    {
        RectangleF rect = slot;
        DrawGlassPanel(g, rect, 10f, selected ? Mix(fillColor, Color.White, 0.06f) : fillColor, selected ? 224 : 204);

        RectangleF accentStrip = new RectangleF(rect.X + 5f, rect.Y + 5f, rect.Width - 10f, 8f);
        using SolidBrush accentBrush = new SolidBrush(Color.FromArgb(selected ? 220 : 138, Mix(fillColor, Color.White, 0.04f)));
        g.FillRectangle(accentBrush, accentStrip);

        if (!string.IsNullOrEmpty(hotkey))
        {
            SizeF hotkeySize = g.MeasureString(hotkey, _tinyFont);
            float badgeWidth = Math.Max(16f, hotkeySize.Width + 6f);
            DrawTagBadge(g, new RectangleF(rect.Right - badgeWidth - 4f, rect.Y + 3f, badgeWidth, 11f), hotkey, selected ? Player.Accent : Color.FromArgb(78, 86, 98));
        }

        SizeF capSize = g.MeasureString(caption, _tinyFont);
        g.DrawString(caption, _tinyFont, Brushes.WhiteSmoke, rect.X + (rect.Width - capSize.Width) / 2f, rect.Y + 15f);

        SizeF valueSize = g.MeasureString(value, _tinyFont);
        g.DrawString(value, _tinyFont, Brushes.Gainsboro, rect.X + (rect.Width - valueSize.Width) / 2f, rect.Bottom - 15f);
    }

    private void DrawWorldTint(Graphics g, Size clientSize, int alpha)
    {
        using SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 8, 10, 14));
        g.FillRectangle(b, new Rectangle(0, 0, clientSize.Width, clientSize.Height));
    }

    private void DrawTitle(Graphics g, Size clientSize)
    {
        DrawMenuHeader(g, clientSize, "Slay-Inspired Overkill NET", "Sharper HUD, LAN browser, and less multiplayer jank");

        RectangleF panel = GetMenuButtonsBounds(18f, 18f);
        panel = new RectangleF(panel.X - 16f, panel.Y - 8f, panel.Width + 32f, panel.Height + 18f);
        DrawMenuCard(g, panel, Player.Accent, "MAIN");

        foreach (MenuButton button in _menuButtons)
        {
            DrawMenuButton(g, button, button.Contains(_lastMouseScreen));
        }

        float chipsY = panel.Bottom + 12f;
        DrawLabelPill(g, GetDifficultyName(), _tinyFont, new RectangleF(clientSize.Width / 2f - 156f, chipsY, 92f, 18f), Color.FromArgb(74, 118, 150), Color.WhiteSmoke);
        DrawLabelPill(g, $"{_maxPlayers} players", _tinyFont, new RectangleF(clientSize.Width / 2f - 48f, chipsY, 96f, 18f), Color.FromArgb(86, 96, 116), Color.WhiteSmoke);
        DrawLabelPill(g, _shuffleArenaOnStart ? "Shuffle ON" : "Shuffle OFF", _tinyFont, new RectangleF(clientSize.Width / 2f + 64f, chipsY, 108f, 18f), _shuffleArenaOnStart ? Color.FromArgb(70, 128, 98) : Color.FromArgb(108, 86, 86), Color.WhiteSmoke);

        DrawFooterText(g, clientSize, "Solo or multiplayer. LAN browser now lists local servers without clipboard gymnastics.");
    }

    private void DrawMultiplayerMenu(Graphics g, Size clientSize)
    {
        DrawMenuHeader(g, clientSize, "Multiplayer", "Host a server, join by IP, or jump into a LAN match");

        RectangleF panel = GetMenuButtonsBounds(18f, 18f);
        panel = new RectangleF(panel.X - 16f, panel.Y - 8f, panel.Width + 32f, panel.Height + 18f);
        DrawMenuCard(g, panel, Color.FromArgb(78, 122, 160), "NET");

        foreach (MenuButton button in _menuButtons)
        {
            DrawMenuButton(g, button, button.Contains(_lastMouseScreen));
        }

        float chipsY = panel.Bottom + 12f;
        DrawLabelPill(g, $"LAN {_network.GetLanServers().Count}", _tinyFont, new RectangleF(clientSize.Width / 2f - 138f, chipsY, 82f, 18f), Color.FromArgb(74, 118, 150), Color.WhiteSmoke);
        DrawLabelPill(g, $"tcp/{_joinPort}", _tinyFont, new RectangleF(clientSize.Width / 2f - 42f, chipsY, 84f, 18f), Color.FromArgb(86, 96, 116), Color.WhiteSmoke);
        DrawLabelPill(g, $"slots {_maxPlayers}", _tinyFont, new RectangleF(clientSize.Width / 2f + 56f, chipsY, 92f, 18f), Color.FromArgb(80, 120, 94), Color.WhiteSmoke);

        DrawFooterText(g, clientSize, "LAN list auto-refreshes via UDP beacon. Host is advertised on local network.");
    }

    private void DrawLanBrowser(Graphics g, Size clientSize)
    {
        IReadOnlyList<LanServerInfo> servers = _network.GetLanServers();
        DrawMenuHeader(g, clientSize, "LAN servers", servers.Count == 0 ? "No hosts seen yet on local network" : $"{servers.Count} server(s) discovered on local network");

        RectangleF panel = GetMenuButtonsBounds(18f, 18f);
        panel = new RectangleF(panel.X - 16f, panel.Y - 8f, panel.Width + 32f, panel.Height + 18f);
        DrawMenuCard(g, panel, Color.FromArgb(88, 124, 154), "LAN");

        foreach (MenuButton button in _menuButtons)
        {
            DrawMenuButton(g, button, button.Contains(_lastMouseScreen));
        }

        float chipsY = panel.Bottom + 12f;
        DrawLabelPill(g, $"found {servers.Count}", _tinyFont, new RectangleF(clientSize.Width / 2f - 118f, chipsY, 82f, 18f), Color.FromArgb(74, 118, 150), Color.WhiteSmoke);
        DrawLabelPill(g, "UDP beacon", _tinyFont, new RectangleF(clientSize.Width / 2f - 20f, chipsY, 86f, 18f), Color.FromArgb(86, 96, 116), Color.WhiteSmoke);
        DrawLabelPill(g, "click to join", _tinyFont, new RectangleF(clientSize.Width / 2f + 80f, chipsY, 94f, 18f), Color.FromArgb(80, 120, 94), Color.WhiteSmoke);

        DrawFooterText(g, clientSize, servers.Count == 0 ? "Start a host on this network and hit refresh. Discovery is LAN-only." : "Each entry shows server name, players, and endpoint. Click one to connect.");
    }

    private void DrawSettings(Graphics g, Size clientSize)
    {
        DrawMenuHeader(g, clientSize, "Settings", "UI, difficulty, audio and debug controls");

        RectangleF panel = GetMenuButtonsBounds(18f, 18f);
        panel = new RectangleF(panel.X - 14f, panel.Y - 8f, panel.Width + 28f, panel.Height + 18f);
        DrawMenuCard(g, panel, Color.FromArgb(76, 122, 150), "CONFIG");

        foreach (MenuButton button in _menuButtons)
        {
            DrawMenuButton(g, button, button.Contains(_lastMouseScreen));
        }

        float chipsY = panel.Bottom + 12f;
        DrawLabelPill(g, "HUD " + GetUiScaleName(), _tinyFont, new RectangleF(clientSize.Width / 2f - 150f, chipsY, 92f, 18f), Color.FromArgb(78, 102, 126), Color.WhiteSmoke);
        DrawLabelPill(g, _showHints ? "Hints ON" : "Hints OFF", _tinyFont, new RectangleF(clientSize.Width / 2f - 46f, chipsY, 92f, 18f), _showHints ? Color.FromArgb(74, 128, 96) : Color.FromArgb(104, 88, 88), Color.WhiteSmoke);
        DrawLabelPill(g, _sound.Enabled ? "Audio ON" : "Audio OFF", _tinyFont, new RectangleF(clientSize.Width / 2f + 58f, chipsY, 92f, 18f), _sound.Enabled ? Color.FromArgb(124, 90, 58) : Color.FromArgb(92, 92, 102), Color.WhiteSmoke);

        DrawFooterText(g, clientSize, "Compact menu, darker surfaces, cleaner contrast.");
    }

    private void DrawHostSetup(Graphics g, Size clientSize)
    {
        DrawMenuHeader(g, clientSize, "Host server", $"Port {_joinPort}  |  Max players {_maxPlayers}");

        RectangleF panel = GetMenuButtonsBounds(18f, 18f);
        panel = new RectangleF(panel.X - 14f, panel.Y - 8f, panel.Width + 28f, panel.Height + 18f);
        DrawMenuCard(g, panel, Color.FromArgb(74, 132, 158), "HOST");

        foreach (MenuButton button in _menuButtons)
        {
            DrawMenuButton(g, button, button.Contains(_lastMouseScreen));
        }

        float chipsY = panel.Bottom + 12f;
        DrawLabelPill(g, $"tcp/{_joinPort}", _tinyFont, new RectangleF(clientSize.Width / 2f - 112f, chipsY, 84f, 18f), Color.FromArgb(78, 110, 138), Color.WhiteSmoke);
        DrawLabelPill(g, $"slots {_maxPlayers}", _tinyFont, new RectangleF(clientSize.Width / 2f - 16f, chipsY, 92f, 18f), Color.FromArgb(90, 98, 118), Color.WhiteSmoke);
        DrawLabelPill(g, "LAN/WAN test", _tinyFont, new RectangleF(clientSize.Width / 2f + 88f, chipsY, 96f, 18f), Color.FromArgb(80, 120, 94), Color.WhiteSmoke);

        DrawFooterText(g, clientSize, "Prototype TCP host. Good for testing, not magic 64-player production voodoo.");
    }

    private void DrawJoinSetup(Graphics g, Size clientSize)
    {
        DrawMenuHeader(g, clientSize, "Join server", $"{_joinAddress}:{_joinPort}");

        RectangleF panel = GetMenuButtonsBounds(18f, 18f);
        panel = new RectangleF(panel.X - 14f, panel.Y - 8f, panel.Width + 28f, panel.Height + 18f);
        DrawMenuCard(g, panel, Color.FromArgb(84, 118, 154), "JOIN");

        foreach (MenuButton button in _menuButtons)
        {
            DrawMenuButton(g, button, button.Contains(_lastMouseScreen));
        }

        float chipsY = panel.Bottom + 12f;
        DrawLabelPill(g, _joinAddress, _tinyFont, new RectangleF(clientSize.Width / 2f - 160f, chipsY, 154f, 18f), Color.FromArgb(76, 102, 132), Color.WhiteSmoke);
        DrawLabelPill(g, $"port {_joinPort}", _tinyFont, new RectangleF(clientSize.Width / 2f + 8f, chipsY, 84f, 18f), Color.FromArgb(124, 90, 58), Color.WhiteSmoke);
        DrawLabelPill(g, "clipboard ready", _tinyFont, new RectangleF(clientSize.Width / 2f + 104f, chipsY, 110f, 18f), Color.FromArgb(74, 120, 98), Color.WhiteSmoke);

        DrawFooterText(g, clientSize, "Paste IP or hit localhost for same-machine testing.");
    }

    private void DrawPauseMenu(Graphics g, Size clientSize)
    {
        DrawMenuHeader(g, clientSize, "Paused", "Take a breath, then go back to murder");

        RectangleF panel = GetMenuButtonsBounds(18f, 18f);
        panel = new RectangleF(panel.X - 18f, panel.Y - 8f, panel.Width + 36f, panel.Height + 18f);
        DrawMenuCard(g, panel, Color.FromArgb(92, 108, 142), "PAUSE");

        foreach (MenuButton button in _menuButtons)
        {
            DrawMenuButton(g, button, button.Contains(_lastMouseScreen));
        }

        DrawFooterText(g, clientSize, "Esc resumes. Title throws you out of the current run.");
    }

    private void DrawGameOver(Graphics g, Size clientSize)
    {
        DrawMenuHeader(g, clientSize, "Run ended", $"Score {_score}  |  Best {_bestScore}  |  Time {_survivalTime:0.0}s");

        RectangleF statsRect = new RectangleF(clientSize.Width / 2f - 198f, 164f, 396f, 72f);
        DrawMenuCard(g, statsRect, Color.FromArgb(142, 78, 78), "RESULT");

        DrawLabelPill(g, $"Score {_score}", _tinyFont, new RectangleF(statsRect.X + 18f, statsRect.Y + 34f, 96f, 18f), Color.FromArgb(138, 82, 82), Color.WhiteSmoke);
        DrawLabelPill(g, $"Best {_bestScore}", _tinyFont, new RectangleF(statsRect.X + 126f, statsRect.Y + 34f, 96f, 18f), Color.FromArgb(92, 102, 122), Color.WhiteSmoke);
        DrawLabelPill(g, $"{_survivalTime:0.0}s", _tinyFont, new RectangleF(statsRect.X + 234f, statsRect.Y + 34f, 72f, 18f), Color.FromArgb(126, 94, 58), Color.WhiteSmoke);
        DrawLabelPill(g, $"Wave {_waveNumber}", _tinyFont, new RectangleF(statsRect.X + 318f, statsRect.Y + 34f, 60f, 18f), Color.FromArgb(78, 116, 144), Color.WhiteSmoke);

        RectangleF panel = GetMenuButtonsBounds(18f, 18f);
        panel = new RectangleF(panel.X - 18f, panel.Y - 8f, panel.Width + 36f, panel.Height + 18f);
        DrawMenuCard(g, panel, Color.FromArgb(132, 76, 76), "DEFEAT");

        foreach (MenuButton button in _menuButtons)
        {
            DrawMenuButton(g, button, button.Contains(_lastMouseScreen));
        }

        DrawFooterText(g, clientSize, "Enter restarts. Esc returns to title.");
    }

    private void DrawMenuHeader(Graphics g, Size clientSize, string title, string subtitle)
    {
        SizeF titleSize = g.MeasureString(title, _titleFont);
        RectangleF titleRect = new RectangleF((clientSize.Width - Math.Max(360f, titleSize.Width + 44f)) / 2f, 52f, Math.Max(360f, titleSize.Width + 44f), titleSize.Height + 16f);
        DrawGlassPanel(g, titleRect, 22f, Player.Accent, 220);

        using SolidBrush titleBrush = new SolidBrush(Color.White);
        g.DrawString(title, _titleFont, titleBrush, titleRect.X + 22f, titleRect.Y + 7f);

        RectangleF accentLine = new RectangleF(titleRect.X + 22f, titleRect.Bottom - 6f, titleRect.Width - 44f, 2f);
        using SolidBrush line = new SolidBrush(Color.FromArgb(170, Player.Accent));
        g.FillRectangle(line, accentLine);

        SizeF subSize = g.MeasureString(subtitle, _smallFont);
        RectangleF subRect = new RectangleF((clientSize.Width - Math.Max(320f, subSize.Width + 34f)) / 2f, titleRect.Bottom + 10f, Math.Max(320f, subSize.Width + 34f), subSize.Height + 10f);
        DrawGlassPanel(g, subRect, 12f, Color.FromArgb(78, 98, 122), 202);
        g.DrawString(subtitle, _smallFont, Brushes.Gainsboro, subRect.X + 16f, subRect.Y + 5f);
    }

    private void DrawMenuButton(Graphics g, MenuButton button, bool hovered)
    {
        RectangleF rect = button.Rect;
        Color accent = GetMenuButtonAccent(button, hovered);
        int alpha = button.Enabled ? (hovered ? 226 : 206) : 170;

        DrawGlassPanel(g, rect, 14f, accent, alpha);

        RectangleF strip = new RectangleF(rect.X + 6f, rect.Y + 6f, 8f, rect.Height - 12f);
        using SolidBrush stripBrush = new SolidBrush(Color.FromArgb(button.Enabled ? 220 : 100, accent));
        using GraphicsPath stripPath = CreateRoundedPath(strip, 4f);
        g.FillPath(stripBrush, stripPath);

        if (hovered && button.Enabled)
        {
            RectangleF hoverRect = new RectangleF(rect.X + 14f, rect.Y + 6f, rect.Width - 20f, rect.Height - 12f);
            using SolidBrush hoverBrush = new SolidBrush(Color.FromArgb(24, Color.White));
            using GraphicsPath hoverPath = CreateRoundedPath(hoverRect, 10f);
            g.FillPath(hoverBrush, hoverPath);
        }

        using SolidBrush textBrush = new SolidBrush(button.Enabled ? Color.WhiteSmoke : Color.FromArgb(160, 180, 184, 190));
        g.DrawString(button.Label, _menuFont, textBrush, rect.X + 24f, rect.Y + 8f);

        string arrow = hovered ? ">>" : ">";
        SizeF arrowSize = g.MeasureString(arrow, _menuFont);
        g.DrawString(arrow, _menuFont, textBrush, rect.Right - arrowSize.Width - 16f, rect.Y + 8f);
    }

    private void DrawFooterText(Graphics g, Size clientSize, string text)
    {
        SizeF size = g.MeasureString(text, _smallFont);
        RectangleF rect = new RectangleF((clientSize.Width - Math.Max(420f, size.Width + 28f)) / 2f, clientSize.Height - 46f, Math.Max(420f, size.Width + 28f), size.Height + 8f);
        DrawGlassPanel(g, rect, 12f, Color.FromArgb(76, 88, 106), 190);
        g.DrawString(text, _smallFont, Brushes.Gainsboro, rect.X + 14f, rect.Y + 4f);
    }

    private void DrawAnnouncement(Graphics g, Size clientSize)
    {
        SizeF size = g.MeasureString(_announcement, _menuFont);
        RectangleF rect = new RectangleF((clientSize.Width - Math.Max(220f, size.Width + 28f)) / 2f, 18f, Math.Max(220f, size.Width + 28f), size.Height + 12f);
        DrawGlassPanel(g, rect, 14f, Player.Accent, 224);
        DrawLabelPill(g, "NOTICE", _tinyFont, new RectangleF(rect.X + 8f, rect.Y + 8f, 54f, 16f), Player.Accent, Color.WhiteSmoke);
        g.DrawString(_announcement, _menuFont, Brushes.White, rect.X + 70f, rect.Y + 6f);
    }
}