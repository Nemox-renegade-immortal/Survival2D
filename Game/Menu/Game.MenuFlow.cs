using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace SlayInspiredPrototype;

public sealed partial class Game
{
    private GamePhase _settingsReturnPhase = GamePhase.Title;

    private void UpdateTitle(InputState input, Size clientSize)
    {
        BuildTitleMenu(clientSize);
        if (UseNativeImGuiUi())
        {
            return;
        }

        HandleMenuInput(input, ActivateTitleButton, "solo");
    }

    private void UpdateMultiplayerMenu(InputState input, Size clientSize)
    {
        BuildMultiplayerMenu(clientSize);

        if (input.WasPressed(Keys.Escape))
        {
            _phase = GamePhase.Title;
            _sound.PlayUi(UiSound.Back);
            return;
        }

        if (UseNativeImGuiUi())
        {
            return;
        }

        HandleMenuInput(input, ActivateMultiplayerButton, "host");
    }

    private void UpdateLanBrowser(InputState input, Size clientSize)
    {
        BuildLanBrowserMenu(clientSize);

        if (input.WasPressed(Keys.Escape))
        {
            _phase = GamePhase.MultiplayerMenu;
            _sound.PlayUi(UiSound.Back);
            return;
        }

        if (UseNativeImGuiUi())
        {
            return;
        }

        HandleMenuInput(input, ActivateLanButton, "refresh");
    }

    private void UpdateSettings(InputState input, Size clientSize)
    {
        BuildSettingsMenu(clientSize);

        if (input.WasPressed(Keys.Escape))
        {
            ReturnFromSettings();
            return;
        }

        if (UseNativeImGuiUi())
        {
            return;
        }

        HandleMenuInput(input, ActivateSettingsButton, GetDefaultSettingsButtonId());
    }

    private void UpdateHostSetup(InputState input, Size clientSize)
    {
        BuildHostMenu(clientSize);

        if (input.WasPressed(Keys.Escape))
        {
            _phase = GamePhase.MultiplayerMenu;
            _sound.PlayUi(UiSound.Back);
            return;
        }

        if (UseNativeImGuiUi())
        {
            return;
        }

        HandleMenuInput(input, ActivateHostButton, "hoststart");
    }

    private void UpdateJoinSetup(InputState input, Size clientSize)
    {
        BuildJoinMenu(clientSize);

        if (input.WasPressed(Keys.Escape))
        {
            _phase = GamePhase.MultiplayerMenu;
            _sound.PlayUi(UiSound.Back);
            return;
        }

        if (UseNativeImGuiUi())
        {
            return;
        }

        HandleMenuInput(input, ActivateJoinButton, "joinstart");
    }

    private void UpdatePaused(InputState input, Size clientSize)
    {
        BuildPauseMenu(clientSize);

        if (input.WasPressed(Keys.Escape))
        {
            _phase = GamePhase.Playing;
            _sound.PlayUi(UiSound.Back);
            return;
        }

        if (UseNativeImGuiUi())
        {
            return;
        }

        HandleMenuInput(input, ActivatePausedButton, "resume");
    }

    private void UpdateGameOver(InputState input, Size clientSize)
    {
        BuildGameOverMenu(clientSize);

        if (input.WasPressed(Keys.Escape))
        {
            ResetToTitle();
            _sound.PlayUi(UiSound.Back);
            return;
        }

        if (input.WasPressed(Keys.Enter))
        {
            RetryCurrentMode();
            return;
        }

        if (UseNativeImGuiUi())
        {
            return;
        }

        HandleMenuInput(input, ActivateGameOverButton, null, false);
    }

    private void BuildTitleMenu(Size clientSize)
    {
        CreateMenuButtons(
            ("solo", "Solo run", true),
            ("multi", "Multiplayer", true),
            ("settings", "Settings", true),
            ("shuffle", _shuffleArenaOnStart ? "Arena shuffle: ON" : "Arena shuffle: OFF", true),
            ("exit", "Exit", true));

        Vector2 screen = new Vector2(clientSize.Width, clientSize.Height);
        float s = GetMenuScale(screen);
        LayoutMenuButtons(screen, 166f * s, 474f * s, 58f * s, 12f * s, 20f * s);
    }

    private void BuildMultiplayerMenu(Size clientSize)
    {
        CreateMenuButtons(
            ("host", "Host server", true),
            ("join", "Join by IP", true),
            ("lan", "LAN servers", true),
            ("back", "Back", true));

        float s = GetMenuScale(new Vector2(clientSize.Width, clientSize.Height));
        LayoutMenuButtons(new Vector2(clientSize.Width, clientSize.Height), 166f * s, 474f * s, 58f * s, 12f * s, 20f * s);
    }

    private void BuildSettingsMenu(Size clientSize)
    {
        List<(string id, string label, bool enabled)> items = new List<(string id, string label, bool enabled)>();
        items.AddRange(GetSettingsTabButtons());
        items.AddRange(GetSettingsActionButtons());
        items.Add(("back", "Back", true));
        CreateMenuButtons(items.ToArray());
        LayoutSettingsButtons(new Vector2(clientSize.Width, clientSize.Height));
    }

    private void BuildHostMenu(Size clientSize)
    {
        CreateMenuButtons(
            ("portminus", $"Port -   {_joinPort}", _joinPort > 1024),
            ("portplus", $"Port +   {_joinPort}", _joinPort < 65535),
            ("maxminus", $"Slots -  {_maxPlayers}", _maxPlayers > 1),
            ("maxplus", $"Slots +  {_maxPlayers}", _maxPlayers < 64),
            ("hoststart", "Start host match", true),
            ("back", "Back", true));

        float s = GetMenuScale(new Vector2(clientSize.Width, clientSize.Height));
        LayoutMenuButtons(new Vector2(clientSize.Width, clientSize.Height), 166f * s, 500f * s, 56f * s, 10f * s, 20f * s);
    }

    private void BuildLanBrowserMenu(Size clientSize)
    {
        IReadOnlyList<LanServerInfo> servers = _network.GetLanServers();
        List<(string Id, string Label, bool Enabled)> buttons = new List<(string Id, string Label, bool Enabled)>();

        if (servers.Count == 0)
        {
            buttons.Add(("refresh", "Refresh LAN scan", true));
        }
        else
        {
            foreach (LanServerInfo server in servers.Take(6))
            {
                string label = $"{server.ServerName}  {server.PlayerCount}/{server.MaxPlayers}  {server.Address}:{server.Port}";
                buttons.Add(($"lan:{server.Address}:{server.Port}", label, true));
            }

            buttons.Add(("refresh", "Refresh LAN scan", true));
        }

        buttons.Add(("back", "Back", true));
        CreateMenuButtons(buttons.ToArray());

        float s = GetMenuScale(new Vector2(clientSize.Width, clientSize.Height));
        LayoutMenuButtons(new Vector2(clientSize.Width, clientSize.Height), 152f * s, 560f * s, 54f * s, 10f * s, 18f * s);
    }

    private void BuildJoinMenu(Size clientSize)
    {
        string joinTarget = string.IsNullOrWhiteSpace(_joinAddress) ? "127.0.0.1" : _joinAddress;
        string shortJoinTarget = joinTarget.Length > 24 ? joinTarget.Substring(0, 21) + "..." : joinTarget;
        CreateMenuButtons(
            ("paste", $"Paste address  ({shortJoinTarget})", true),
            ("localhost", "Use localhost", true),
            ("portminus", $"Port -   {_joinPort}", _joinPort > 1),
            ("portplus", $"Port +   {_joinPort}", _joinPort < 65535),
            ("joinstart", "Connect and start", true),
            ("back", "Back", true));

        float s = GetMenuScale(new Vector2(clientSize.Width, clientSize.Height));
        LayoutMenuButtons(new Vector2(clientSize.Width, clientSize.Height), 166f * s, 500f * s, 56f * s, 10f * s, 20f * s);
    }

    private void BuildPauseMenu(Size clientSize)
    {
        CreateMenuButtons(
            ("resume", "Resume", true),
            ("settings", "Settings", true),
            (GetPauseSessionActionId(), GetPauseSessionActionLabel(), true),
            ("exit", "Exit", true));

        float s = GetMenuScale(new Vector2(clientSize.Width, clientSize.Height));
        LayoutMenuButtons(new Vector2(clientSize.Width, clientSize.Height), 166f * s, 430f * s, 56f * s, 10f * s, 20f * s);
    }

    private void BuildGameOverMenu(Size clientSize)
    {
        CreateMenuButtons(
            ("retry", "Retry", true),
            ("title", "Return to title", true));

        float s = GetMenuScale(new Vector2(clientSize.Width, clientSize.Height));
        float statsBottom = 138f * s + 24f * s + 78f * s;
        LayoutMenuButtons(new Vector2(clientSize.Width, clientSize.Height), statsBottom + 24f * s, 430f * s, 56f * s, 10f * s, 20f * s);
    }

    private void CreateMenuButtons(params (string id, string label, bool enabled)[] items)
    {
        _menuButtons.Clear();
        foreach ((string id, string label, bool enabled) in items)
        {
            MenuButton button = new MenuButton(id, label, Rectangle.Empty)
            {
                Enabled = enabled
            };
            _menuButtons.Add(button);
        }
    }

    private static float GetMenuScale(Vector2 screen)
    {
        float sx = screen.X / 1600f;
        float sy = screen.Y / 900f;
        return Math.Clamp(MathF.Min(sx, sy), 0.88f, 1.12f);
    }

    private void LayoutMenuButtons(Vector2 screen, float panelTop, float panelWidth, float buttonHeight, float gap, float pad)
    {
        panelWidth = MathF.Min(panelWidth, screen.X - 80f);
        float panelX = (screen.X - panelWidth) * 0.5f;
        if (_menuButtons.Count == 0)
        {
            return;
        }

        float buttonWidth = panelWidth - pad * 2f;
        float startY = panelTop + 34f * pad / 16f;
        for (int i = 0; i < _menuButtons.Count; i++)
        {
            float y = startY + i * (buttonHeight + gap);
            _menuButtons[i].Rect = Rectangle.Round(new RectangleF(panelX + pad, y, buttonWidth, buttonHeight));
        }
    }

    private void HandleMenuInput(InputState input, Action<string> onActivate, string? defaultButtonId = null, bool allowDefaultEnter = true)
    {
        int hovered = -1;
        for (int i = 0; i < _menuButtons.Count; i++)
        {
            if (_menuButtons[i].Contains(input.MouseScreen))
            {
                hovered = i;
                break;
            }
        }

        if (hovered != _hoverButtonIndex)
        {
            if (hovered >= 0)
            {
                _sound.PlayUi(UiSound.Hover);
            }
            _hoverButtonIndex = hovered;
        }

        if (hovered >= 0 && input.LeftMousePressed)
        {
            MenuButton button = _menuButtons[hovered];
            if (button.Enabled)
            {
                onActivate(button.Id);
                return;
            }
        }

        if (allowDefaultEnter && input.WasPressed(Keys.Enter))
        {
            MenuButton? target = hovered >= 0
                ? _menuButtons[hovered]
                : (!string.IsNullOrWhiteSpace(defaultButtonId)
                    ? _menuButtons.FirstOrDefault(b => b.Enabled && b.Id == defaultButtonId)
                    : _menuButtons.FirstOrDefault(b => b.Enabled));
            if (target is not null)
            {
                onActivate(target.Id);
            }
        }
    }

    private void ActivateTitleButton(string id)
    {
        switch (id)
        {
            case "solo":
                QueueSoloLaunch();
                break;
            case "multi":
                _phase = GamePhase.MultiplayerMenu;
                _sound.PlayUi(UiSound.Click);
                break;
            case "settings":
                _settingsReturnPhase = GamePhase.Title;
                SyncMenuInputBuffers();
                _phase = GamePhase.Settings;
                _sound.PlayUi(UiSound.Click);
                break;
            case "shuffle":
                _shuffleArenaOnStart = !_shuffleArenaOnStart;
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_shuffleArenaOnStart ? "Arena shuffle enabled" : "Arena shuffle disabled", 1f);
                break;
            case "exit":
                RequestExit();
                break;
        }
    }

    private void ActivateMultiplayerButton(string id)
    {
        switch (id)
        {
            case "host":
                SyncMenuInputBuffers();
                _phase = GamePhase.HostSetup;
                _sound.PlayUi(UiSound.Click);
                break;
            case "join":
                SyncMenuInputBuffers();
                _phase = GamePhase.JoinSetup;
                _sound.PlayUi(UiSound.Click);
                break;
            case "lan":
                _phase = GamePhase.LanBrowser;
                _sound.PlayUi(UiSound.Click);
                break;
            case "back":
                _phase = GamePhase.Title;
                _sound.PlayUi(UiSound.Back);
                break;
        }
    }

    private void ActivateLanButton(string id)
    {
        if (id == "refresh")
        {
            _sound.PlayUi(UiSound.Click);
            SetAnnouncement("Refreshing LAN servers", 0.8f);
            return;
        }

        if (id == "back")
        {
            _phase = GamePhase.MultiplayerMenu;
            _sound.PlayUi(UiSound.Back);
            return;
        }

        if (!id.StartsWith("lan:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string endpoint = id.Substring(4);
        int colonIndex = endpoint.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex >= endpoint.Length - 1)
        {
            return;
        }

        _joinAddress = endpoint.Substring(0, colonIndex);
        if (!int.TryParse(endpoint.Substring(colonIndex + 1), out int parsedPort))
        {
            return;
        }

        _joinPort = Math.Clamp(parsedPort, 1, 65535);
        SyncMenuInputBuffers();
        QueueJoinLaunch(GamePhase.LanBrowser, "Joining LAN server", "LAN join failed");
    }

    private void ActivateSettingsButton(string id)
    {
        if (TrySelectSettingsTab(id))
        {
            return;
        }

        switch (id)
        {
            case "difficulty":
                CycleDifficulty();
                break;
            case "uiscale":
                CycleUiScale();
                break;
            case "nicknamepaste":
                TryPasteNickname();
                break;
            case "nicknamereset":
                ResetNickname();
                break;
            case "hints":
                _showHints = !_showHints;
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_showHints ? "Hints enabled" : "Hints disabled", 1f);
                break;
            case "audio":
                _sound.Enabled = !_sound.Enabled;
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_sound.Enabled ? "Audio enabled" : "Audio disabled", 1f);
                break;
            case "audiomaster":
                CyclePercentSetting(SetMasterVolumePercent, _sound.MasterVolumePercent);
                SetAnnouncement($"Master volume: {_sound.MasterVolumePercent}%", 1f);
                break;
            case "audioui":
                CyclePercentSetting(SetUiVolumePercent, _sound.UiVolumePercent);
                SetAnnouncement($"UI volume: {_sound.UiVolumePercent}%", 1f);
                break;
            case "audioworld":
                CyclePercentSetting(SetWorldVolumePercent, _sound.WorldVolumePercent);
                SetAnnouncement($"World volume: {_sound.WorldVolumePercent}%", 1f);
                break;
            case "audiospatial":
                _sound.SpatialAudioEnabled = !_sound.SpatialAudioEnabled;
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_sound.SpatialAudioEnabled ? "3D audio enabled" : "3D audio disabled", 1f);
                break;
            case "menuart":
                _menuArtworkEnabled = !_menuArtworkEnabled;
                InvalidateMenuArtworkCache();
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_menuArtworkEnabled ? "Menu artwork enabled" : "Menu backdrop forced to black", 1f);
                break;
            case "splashlogo":
                _splashLogoEnabled = !_splashLogoEnabled;
                InvalidateMenuArtworkCache();
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_splashLogoEnabled ? "Splash logo enabled" : "Splash logo hidden", 1f);
                break;
            case "shader":
                _screenShaderEnabled = !_screenShaderEnabled;
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_screenShaderEnabled ? "Screen shader enabled" : "Screen shader disabled", 1f);
                break;
            case "particles":
                _worldParticlesEnabled = !_worldParticlesEnabled;
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_worldParticlesEnabled ? "World particles enabled" : "World particles disabled", 1f);
                break;
            case "screenshake":
                _screenShakeEnabled = !_screenShakeEnabled;
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_screenShakeEnabled ? "Screen shake enabled" : "Screen shake disabled", 1f);
                break;
            case "joinpaste":
                PasteJoinAddress();
                break;
            case "joinreset":
                _joinAddress = "127.0.0.1";
                _joinPort = 7777;
                SyncMenuInputBuffers();
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement($"Join target reset: {_joinAddress}:{_joinPort}", 1.1f);
                break;
            case "netdebug":
                _showNetworkDebug = !_showNetworkDebug;
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_showNetworkDebug ? "Net debug enabled" : "Net debug disabled", 1f);
                break;
            case "fpshud":
                _showFpsHud = !_showFpsHud;
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_showFpsHud ? "FPS HUD enabled" : "FPS HUD disabled", 1f);
                break;
            case "perfhud":
                _showPerfHud = !_showPerfHud;
                if (_showPerfHud)
                {
                    _showFpsHud = true;
                }
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement(_showPerfHud ? "Performance HUD enabled" : "Performance HUD disabled", 1f);
                break;
            case "back":
                ReturnFromSettings();
                break;
        }
    }

    private void ActivateHostButton(string id)
    {
        switch (id)
        {
            case "portminus":
                _joinPort = Math.Max(1024, _joinPort - 1);
                SyncMenuInputBuffers();
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                break;
            case "portplus":
                _joinPort = Math.Min(65535, _joinPort + 1);
                SyncMenuInputBuffers();
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                break;
            case "maxminus":
                _maxPlayers = Math.Max(1, _maxPlayers - 1);
                SyncMenuInputBuffers();
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                break;
            case "maxplus":
                _maxPlayers = Math.Min(64, _maxPlayers + 1);
                SyncMenuInputBuffers();
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                break;
            case "hoststart":
                CommitHostPortInput();
                CommitHostSlotsInput();
                QueueHostLaunch();
                break;
            case "back":
                CommitHostPortInput();
                CommitHostSlotsInput();
                _phase = GamePhase.MultiplayerMenu;
                _sound.PlayUi(UiSound.Back);
                break;
        }
    }

    private void ActivateJoinButton(string id)
    {
        switch (id)
        {
            case "paste":
                PasteJoinAddress();
                break;
            case "localhost":
                _joinAddress = "127.0.0.1";
                SyncMenuInputBuffers();
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                SetAnnouncement("Join target set to localhost", 1f);
                break;
            case "portminus":
                _joinPort = Math.Max(1, _joinPort - 1);
                SyncMenuInputBuffers();
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                break;
            case "portplus":
                _joinPort = Math.Min(65535, _joinPort + 1);
                SyncMenuInputBuffers();
                _sound.PlayUi(UiSound.Click);
                SavePersistentSettings();
                break;
            case "joinstart":
                CommitJoinAddressInput();
                CommitJoinPortInput();
                QueueJoinLaunch(GamePhase.JoinSetup, "Connecting to server", "Join failed");
                break;
            case "back":
                CommitJoinAddressInput();
                CommitJoinPortInput();
                _phase = GamePhase.MultiplayerMenu;
                _sound.PlayUi(UiSound.Back);
                break;
        }
    }

    private void ActivatePausedButton(string id)
    {
        switch (id)
        {
            case "resume":
                _phase = GamePhase.Playing;
                _sound.PlayUi(UiSound.Back);
                break;
            case "settings":
                _settingsReturnPhase = GamePhase.Paused;
                SyncMenuInputBuffers();
                _phase = GamePhase.Settings;
                _sound.PlayUi(UiSound.Click);
                break;
            case "title":
            case "disconnect":
            case "stopserver":
                LeaveSessionOrReturnToTitle();
                break;
            case "exit":
                RequestExit();
                break;
        }
    }

    private void ActivateGameOverButton(string id)
    {
        switch (id)
        {
            case "retry":
                RetryCurrentMode();
                break;
            case "title":
            case "disconnect":
            case "stopserver":
                LeaveSessionOrReturnToTitle();
                break;
            case "exit":
                RequestExit();
                break;
        }
    }

    private void ReturnFromSettings()
    {
        CommitNicknameInput(false);
        _phase = _settingsReturnPhase == GamePhase.Paused ? GamePhase.Paused : GamePhase.Title;
        _sound.PlayUi(UiSound.Back);
    }

    private void RetryCurrentMode()
    {
        Player player = Player;
        RestartRun(_preferredCallsign, player.Accent);
    }

    private void PasteJoinAddress()
    {
        try
        {
            string text = Clipboard.GetText()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                _sound.PlayUi(UiSound.Error);
                SetAnnouncement("Clipboard is empty", 1f);
                return;
            }

            ApplyJoinAddress(text);
            SyncMenuInputBuffers();
            _sound.PlayUi(UiSound.Click);
            SavePersistentSettings();
            SetAnnouncement($"Join target: {_joinAddress}:{_joinPort}", 1.2f);
        }
        catch
        {
            _sound.PlayUi(UiSound.Error);
            SetAnnouncement("Clipboard paste failed", 1.2f);
        }
    }

    private void ApplyJoinAddress(string raw)
    {
        string text = raw.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(7);
        }
        else if (text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(8);
        }

        text = text.Trim('/');

        int colonIndex = text.LastIndexOf(':');
        if (colonIndex > 0 && colonIndex < text.Length - 1 && int.TryParse(text.Substring(colonIndex + 1), out int parsedPort))
        {
            _joinAddress = text.Substring(0, colonIndex).Trim();
            _joinPort = Math.Clamp(parsedPort, 1, 65535);
        }
        else
        {
            _joinAddress = text;
        }

        if (string.IsNullOrWhiteSpace(_joinAddress))
        {
            _joinAddress = "127.0.0.1";
        }
    }
}
