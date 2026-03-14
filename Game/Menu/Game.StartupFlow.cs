using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Windows.Forms;
using ImGuiNET;

namespace SlayInspiredPrototype;

public sealed partial class Game
{
    private enum PendingLaunchMode
    {
        None,
        Solo,
        Host,
        Join
    }

    private const float SplashDuration = 1.55f;
    private const float MinimumLoadingDisplay = 0.12f;

    private PendingLaunchMode _pendingLaunchMode;
    private GamePhase _loadingFailurePhase = GamePhase.Title;
    private string _loadingFailureMessage = "Launch failed";
    private string _loadingStatus = "Loading";
    private float _splashTimer;
    private float _loadingTimer;
    private bool _menuBackgroundResolved;
    private bool _logoResolved;
    private Bitmap? _menuBackgroundBitmap;
    private Bitmap? _logoBitmap;

    private void BeginStartupSplash()
    {
        _pendingLaunchMode = PendingLaunchMode.None;
        _splashTimer = 0f;
        _loadingTimer = 0f;
        _loadingStatus = "Booting";
        _phase = GamePhase.Splash;
        _announcementTimer = 0f;
    }

    private void UpdateSplash(float dt, InputState input, Size clientSize)
    {
        _splashTimer += dt;
        bool skip = input.LeftMousePressed || input.WasPressed(Keys.Enter) || input.WasPressed(Keys.Space) || input.WasPressed(Keys.Escape);
        if (_splashTimer >= SplashDuration || skip)
        {
            ResetToTitle();
        }
    }

    private void QueueSoloLaunch()
    {
        _network.Stop();
        BeginLoading(PendingLaunchMode.Solo, GamePhase.Title, "Preparing solo run", "Solo start failed");
    }

    private void QueueHostLaunch()
    {
        BeginLoading(PendingLaunchMode.Host, GamePhase.HostSetup, "Starting host session", "Host startup failed");
    }

    private void QueueJoinLaunch(GamePhase failurePhase, string status, string failureMessage)
    {
        BeginLoading(PendingLaunchMode.Join, failurePhase, status, failureMessage);
    }

    private void BeginLoading(PendingLaunchMode mode, GamePhase failurePhase, string status, string failureMessage)
    {
        _pendingLaunchMode = mode;
        _loadingFailurePhase = failurePhase;
        _loadingFailureMessage = failureMessage;
        _loadingStatus = status;
        _loadingTimer = 0f;
        _phase = GamePhase.Loading;
        _sound.PlayUi(UiSound.Click);
    }

    private void UpdateLoading(float dt, InputState input, Size clientSize)
    {
        _loadingTimer += dt;
        if (_loadingTimer < MinimumLoadingDisplay || _pendingLaunchMode == PendingLaunchMode.None)
        {
            return;
        }

        PendingLaunchMode mode = _pendingLaunchMode;
        _pendingLaunchMode = PendingLaunchMode.None;

        try
        {
            switch (mode)
            {
                case PendingLaunchMode.Solo:
                    _network.Stop();
                    RestartRun(_preferredCallsign, Color.FromArgb(92, 220, 255));
                    return;
                case PendingLaunchMode.Host:
                    _network.StartHost(_joinPort, _maxPlayers);
                    RestartRun(_preferredCallsign, Color.FromArgb(92, 220, 255));
                    return;
                case PendingLaunchMode.Join:
                    string callsign = _preferredCallsign;
                    _network.Join(_joinAddress, _joinPort, callsign);
                    RestartRun(callsign, Color.FromArgb(92, 220, 255));
                    return;
                default:
                    ResetToTitle();
                    return;
            }
        }
        catch
        {
            _network.Stop();
            _phase = _loadingFailurePhase;
            _sound.PlayUi(UiSound.Error);
            SetAnnouncement(_loadingFailureMessage, 1.4f);
        }
    }

    private Bitmap? GetMenuBackgroundBitmap()
    {
        if (!_menuArtworkEnabled)
        {
            return null;
        }

        if (!_menuBackgroundResolved)
        {
            _menuBackgroundResolved = true;
            string[] candidates =
            {
                "Assets/ui/menu_background.png",
                "Assets/ui/menu_background.jpg",
                "Assets/ui/menu_background.jpeg",
                "Assets/ui/main_menu.png",
                "Assets/ui/mainmenu.png",
                "Assets/ui/menu_bg.png",
                "Assets/backgrounds/menu.png",
                "Assets/backgrounds/main_menu.png",
                "Assets/menu_background.png"
            };

            foreach (string candidate in candidates)
            {
                string? path = AssetLocator.Find(candidate);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                try
                {
                    _menuBackgroundBitmap = new Bitmap(path);
                    break;
                }
                catch
                {
                }
            }
        }

        return _menuBackgroundBitmap;
    }

    private Bitmap? GetLogoBitmap()
    {
        if (!_splashLogoEnabled)
        {
            return null;
        }

        if (!_logoResolved)
        {
            _logoResolved = true;
            string[] candidates =
            {
                "Assets/ui/logo.png",
                "Assets/ui/game_logo.png",
                "Assets/ui/splash_logo.png",
                "Assets/logo.png",
                "Assets/splash_logo.png"
            };

            foreach (string candidate in candidates)
            {
                string? path = AssetLocator.Find(candidate);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                try
                {
                    _logoBitmap = new Bitmap(path);
                    break;
                }
                catch
                {
                }
            }
        }

        return _logoBitmap;
    }

    private void DrawMenuBackdrop(Graphics g, Size clientSize)
    {
        g.Clear(Color.Black);

        Bitmap? background = GetMenuBackgroundBitmap();
        if (background is not null)
        {
            DrawCoverImage(g, background, clientSize);
            using SolidBrush dim = new SolidBrush(Color.FromArgb(112, 5, 8, 12));
            g.FillRectangle(dim, 0, 0, clientSize.Width, clientSize.Height);
        }
        else
        {
            using LinearGradientBrush fill = new LinearGradientBrush(
                new Rectangle(0, 0, clientSize.Width, clientSize.Height),
                Color.FromArgb(5, 7, 10),
                Color.FromArgb(14, 18, 24),
                LinearGradientMode.Vertical);
            g.FillRectangle(fill, 0, 0, clientSize.Width, clientSize.Height);
        }

        DrawEnhancedScreenShaderDetails(g, clientSize, false);
        using LinearGradientBrush vignetteTop = new LinearGradientBrush(
            new Rectangle(0, 0, clientSize.Width, Math.Max(120, clientSize.Height / 3)),
            Color.FromArgb(86, 0, 0, 0),
            Color.FromArgb(0, 0, 0, 0),
            LinearGradientMode.Vertical);
        g.FillRectangle(vignetteTop, 0, 0, clientSize.Width, Math.Max(120, clientSize.Height / 3));
    }

    private static void DrawCoverImage(Graphics g, Bitmap image, Size clientSize)
    {
        float scale = Math.Max(clientSize.Width / (float)Math.Max(1, image.Width), clientSize.Height / (float)Math.Max(1, image.Height));
        float drawWidth = image.Width * scale;
        float drawHeight = image.Height * scale;
        float x = (clientSize.Width - drawWidth) * 0.5f;
        float y = (clientSize.Height - drawHeight) * 0.5f;
        g.DrawImage(image, x, y, drawWidth, drawHeight);
    }

    private static GraphicsPath BuildRoundedRectPath(RectangleF rect, float radius)
    {
        float r = Math.Max(1f, Math.Min(radius, Math.Min(rect.Width, rect.Height) * 0.5f));
        float d = r * 2f;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, d, d, 180f, 90f);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270f, 90f);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    private static void FillRoundedRect(Graphics g, Brush brush, RectangleF rect, float radius)
    {
        using GraphicsPath path = BuildRoundedRectPath(rect, radius);
        g.FillPath(brush, path);
    }

    private static void DrawRoundedRect(Graphics g, Pen pen, RectangleF rect, float radius)
    {
        using GraphicsPath path = BuildRoundedRectPath(rect, radius);
        g.DrawPath(pen, path);
    }

    private void DrawSplashScreen(Graphics g, Size clientSize)
    {
        g.Clear(Color.Black);

        Bitmap? logo = GetLogoBitmap();
        if (logo is not null)
        {
            float maxWidth = clientSize.Width * 0.42f;
            float maxHeight = clientSize.Height * 0.24f;
            float scale = Math.Min(maxWidth / Math.Max(1f, logo.Width), maxHeight / Math.Max(1f, logo.Height));
            float width = logo.Width * scale;
            float height = logo.Height * scale;
            g.DrawImage(logo, (clientSize.Width - width) * 0.5f, clientSize.Height * 0.28f, width, height);
        }
        else
        {
            using Font titleFont = new Font("Segoe UI", 30f, FontStyle.Bold);
            using Font subFont = new Font("Segoe UI", 12f, FontStyle.Regular);
            using SolidBrush titleBrush = new SolidBrush(Color.FromArgb(236, 238, 244, 248));
            using SolidBrush subBrush = new SolidBrush(Color.FromArgb(182, 188, 198, 214));
            StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Survival 2D", titleFont, titleBrush, new RectangleF(0f, clientSize.Height * 0.28f, clientSize.Width, 54f), centered);
            g.DrawString("boot sequence", subFont, subBrush, new RectangleF(0f, clientSize.Height * 0.28f + 56f, clientSize.Width, 28f), centered);
        }

        DrawSplashStatus(g, clientSize, "Loading interface");
    }

    private void DrawLoadingScreen(Graphics g, Size clientSize)
    {
        DrawMenuBackdrop(g, clientSize);
        using SolidBrush shade = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
        g.FillRectangle(shade, 0, 0, clientSize.Width, clientSize.Height);
        DrawSplashStatus(g, clientSize, _loadingStatus);
    }

    private void DrawSplashStatus(Graphics g, Size clientSize, string status)
    {
        float panelWidth = Math.Min(480f, clientSize.Width - 120f);
        RectangleF panel = new RectangleF((clientSize.Width - panelWidth) * 0.5f, clientSize.Height * 0.64f, panelWidth, 80f);
        using SolidBrush panelFill = new SolidBrush(Color.FromArgb(168, 6, 8, 12));
        using Pen panelStroke = new Pen(Color.FromArgb(84, 120, 168, 220), 1.5f);
        using Font textFont = new Font("Segoe UI", 13f, FontStyle.Bold);
        using Font hintFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        using SolidBrush textBrush = new SolidBrush(Color.FromArgb(236, 236, 242, 248));
        using SolidBrush hintBrush = new SolidBrush(Color.FromArgb(174, 182, 194, 208));
        FillRoundedRect(g, panelFill, panel, 16f);
        DrawRoundedRect(g, panelStroke, panel, 16f);
        g.DrawString(status, textFont, textBrush, panel.Left + 18f, panel.Top + 14f);
        g.DrawString(_phase == GamePhase.Splash ? "Press Enter to skip" : "Preparing world after menu selection", hintFont, hintBrush, panel.Left + 18f, panel.Top + 38f);

        RectangleF bar = new RectangleF(panel.Left + 18f, panel.Bottom - 20f, panel.Width - 36f, 8f);
        using SolidBrush track = new SolidBrush(Color.FromArgb(54, 154, 182, 214));
        using SolidBrush fill = new SolidBrush(Color.FromArgb(220, 104, 182, 255));
        FillRoundedRect(g, track, bar, 8f);
        float t = _phase == GamePhase.Splash ? Math.Clamp(_splashTimer / SplashDuration, 0f, 1f) : Math.Clamp(_loadingTimer / MinimumLoadingDisplay, 0f, 1f);
        RectangleF progress = new RectangleF(bar.Left, bar.Top, Math.Max(10f, bar.Width * Math.Max(0.12f, t)), bar.Height);
        FillRoundedRect(g, fill, progress, 8f);
    }

    private void DrawImGuiMenuBackdrop(ImDrawListPtr draw, Vector2 screen)
    {
        draw.AddRectFilled(Vector2.Zero, screen, ToU32(Color.Black));

        Bitmap? background = GetMenuBackgroundBitmap();
        if (background is not null)
        {
            IntPtr texture = IntPtr.Zero;
            foreach (string candidate in new[]
            {
                "Assets/ui/menu_background.png",
                "Assets/ui/menu_background.jpg",
                "Assets/ui/menu_background.jpeg",
                "Assets/ui/main_menu.png",
                "Assets/ui/mainmenu.png",
                "Assets/ui/menu_bg.png",
                "Assets/backgrounds/menu.png",
                "Assets/backgrounds/main_menu.png",
                "Assets/menu_background.png"
            })
            {
                texture = TextureCache.GetOrLoad(candidate);
                if (texture != IntPtr.Zero)
                {
                    break;
                }
            }

            if (texture != IntPtr.Zero)
            {
                draw.AddImage(texture, Vector2.Zero, screen);
                draw.AddRectFilled(Vector2.Zero, screen, ToU32(Color.FromArgb(104, 5, 8, 12)));
            }
        }
        else
        {
            draw.AddRectFilledMultiColor(
                Vector2.Zero,
                screen,
                ToU32(Color.FromArgb(255, 5, 7, 10)),
                ToU32(Color.FromArgb(255, 5, 7, 10)),
                ToU32(Color.FromArgb(255, 14, 18, 24)),
                ToU32(Color.FromArgb(255, 14, 18, 24)));
        }

        DrawImGuiAtmosphereShaderDetails(draw, screen);
        draw.AddRectFilledMultiColor(Vector2.Zero, new Vector2(screen.X, screen.Y * 0.34f), ToU32(Color.FromArgb(90, 0, 0, 0)), ToU32(Color.FromArgb(90, 0, 0, 0)), ToU32(Color.FromArgb(0, 0, 0, 0)), ToU32(Color.FromArgb(0, 0, 0, 0)));
    }

    private void DrawImGuiSplashScreen(ImDrawListPtr draw, Vector2 screen)
    {
        DrawImGuiTint(draw, screen, 38);
        DrawImGuiCenteredCard(draw, screen, _phase == GamePhase.Splash ? "Launching" : _loadingStatus, _phase == GamePhase.Splash ? "Press Enter to skip" : "Preparing world after menu selection", _phase == GamePhase.Splash ? Math.Clamp(_splashTimer / SplashDuration, 0f, 1f) : Math.Clamp(_loadingTimer / MinimumLoadingDisplay, 0f, 1f));
    }

    private void DrawImGuiLoadingScreen(ImDrawListPtr draw, Vector2 screen)
    {
        DrawImGuiTint(draw, screen, 96);
        DrawImGuiCenteredCard(draw, screen, _loadingStatus, "Preparing world after menu selection", Math.Clamp(_loadingTimer / MinimumLoadingDisplay, 0f, 1f));
    }

    private void DrawImGuiCenteredCard(ImDrawListPtr draw, Vector2 screen, string title, string subtitle, float progress)
    {
        Vector2 size = new Vector2(MathF.Min(460f, screen.X - 80f), 110f);
        Vector2 min = new Vector2((screen.X - size.X) * 0.5f, screen.Y * 0.62f);
        Vector2 max = min + size;
        draw.AddRectFilled(min, max, ToU32(Color.FromArgb(176, 6, 8, 12)), 16f);
        draw.AddRect(min, max, ToU32(Color.FromArgb(88, 120, 168, 220)), 16f, ImDrawFlags.None, 1.5f);
        draw.AddText(new Vector2(min.X + 18f, min.Y + 14f), ToU32(Color.FromArgb(236, 236, 242, 248)), title);
        draw.AddText(new Vector2(min.X + 18f, min.Y + 36f), ToU32(Color.FromArgb(180, 182, 194, 208)), subtitle);
        Vector2 barMin = new Vector2(min.X + 18f, max.Y - 24f);
        Vector2 barMax = new Vector2(max.X - 18f, max.Y - 16f);
        draw.AddRectFilled(barMin, barMax, ToU32(Color.FromArgb(54, 154, 182, 214)), 5f);
        float fillWidth = MathF.Max(10f, (barMax.X - barMin.X) * MathF.Max(0.12f, progress));
        draw.AddRectFilled(barMin, new Vector2(barMin.X + fillWidth, barMax.Y), ToU32(Color.FromArgb(220, 104, 182, 255)), 5f);

        Bitmap? logo = GetLogoBitmap();
        if (logo is not null)
        {
            IntPtr texture = IntPtr.Zero;
            foreach (string candidate in new[]
            {
                "Assets/ui/logo.png",
                "Assets/ui/game_logo.png",
                "Assets/ui/splash_logo.png",
                "Assets/logo.png",
                "Assets/splash_logo.png"
            })
            {
                texture = TextureCache.GetOrLoad(candidate);
                if (texture != IntPtr.Zero)
                {
                    break;
                }
            }

            if (texture != IntPtr.Zero)
            {
                Vector2 logoSize = new Vector2(MathF.Min(340f, screen.X * 0.34f), MathF.Min(120f, screen.Y * 0.18f));
                Vector2 logoMin = new Vector2((screen.X - logoSize.X) * 0.5f, screen.Y * 0.26f);
                draw.AddImage(texture, logoMin, logoMin + logoSize);
            }
        }
        else
        {
            draw.AddText(new Vector2(screen.X * 0.5f - 54f, screen.Y * 0.30f), ToU32(Color.FromArgb(240, 236, 242, 248)), "Survival 2D");
        }
    }
}
