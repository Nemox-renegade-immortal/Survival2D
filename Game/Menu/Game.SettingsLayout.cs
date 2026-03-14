using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SlayInspiredPrototype;

public sealed partial class Game
{
    private enum SettingsTab
    {
        Gameplay,
        Interface,
        Graphic,
        Audio,
        Network
    }

    private static readonly SettingsTab[] SettingsTabOrder =
    {
        SettingsTab.Gameplay,
        SettingsTab.Interface,
        SettingsTab.Graphic,
        SettingsTab.Audio,
        SettingsTab.Network
    };

    private SettingsTab _settingsTab = SettingsTab.Gameplay;
    private bool _menuArtworkEnabled = true;
    private bool _splashLogoEnabled = true;
    private bool _exitRequested;

    public bool ConsumeExitRequest()
    {
        if (!_exitRequested)
        {
            return false;
        }

        _exitRequested = false;
        return true;
    }

    private void RequestExit()
    {
        _exitRequested = true;
        _sound.PlayUi(UiSound.Click);
        SetAnnouncement("Exiting game", 0.6f);
    }

    private IEnumerable<(string Id, string Label, bool Enabled)> GetSettingsTabButtons()
    {
        foreach (SettingsTab tab in SettingsTabOrder)
        {
            yield return ($"tab:{GetSettingsTabToken(tab)}", GetSettingsTabLabel(tab), true);
        }
    }

    private IEnumerable<(string Id, string Label, bool Enabled)> GetSettingsActionButtons()
    {
        return _settingsTab switch
        {
            SettingsTab.Gameplay => new[]
            {
                ("nicknamepaste", $"Nickname: {_preferredCallsign} · Paste from clipboard", true),
                ("nicknamereset", "Nickname reset: P1", true),
                ("difficulty", $"Difficulty: {GetDifficultyName()}", true),
                ("hints", _showHints ? "Hints: ON" : "Hints: OFF", true),
                ("shuffle", _shuffleArenaOnStart ? "Arena shuffle: ON" : "Arena shuffle: OFF", true)
            },
            SettingsTab.Interface => new[]
            {
                ("uiscale", $"HUD scale: {GetUiScaleName()}", true),
                ("fpshud", _showFpsHud ? "FPS HUD: ON" : "FPS HUD: OFF", true),
                ("perfhud", _showPerfHud ? "Perf HUD: ON" : "Perf HUD: OFF", true)
            },
            SettingsTab.Graphic => new[]
            {
                ("menuart", _menuArtworkEnabled ? "Menu backdrop: ARTWORK" : "Menu backdrop: BLACK", true),
                ("splashlogo", _splashLogoEnabled ? "Splash logo: ON" : "Splash logo: OFF", true),
                ("shader", _screenShaderEnabled ? "Screen shader: ON" : "Screen shader: OFF", true),
                ("particles", _worldParticlesEnabled ? "World particles: ON" : "World particles: OFF", true),
                ("screenshake", _screenShakeEnabled ? "Screen shake: ON" : "Screen shake: OFF", true)
            },
            SettingsTab.Audio => new[]
            {
                ("audio", _sound.Enabled ? "Audio: ON" : "Audio: OFF", true),
                ("audiomaster", $"Master volume: {_sound.MasterVolumePercent}%", true),
                ("audioui", $"UI volume: {_sound.UiVolumePercent}%", true),
                ("audioworld", $"World volume: {_sound.WorldVolumePercent}%", true),
                ("audiospatial", _sound.SpatialAudioEnabled ? "3D audio: ON" : "3D audio: OFF", true)
            },
            SettingsTab.Network => new[]
            {
                ("joinpaste", $"Join target: {_joinAddress}:{_joinPort} · Paste", true),
                ("joinreset", "Join target: reset localhost", true),
                ("netdebug", _showNetworkDebug ? "Net debug: ON" : "Net debug: OFF", true)
            },
            _ => Array.Empty<(string, string, bool)>()
        };
    }

    private string GetDefaultSettingsButtonId()
    {
        return _settingsTab switch
        {
            SettingsTab.Gameplay => "nicknamepaste",
            SettingsTab.Interface => "uiscale",
            SettingsTab.Graphic => "menuart",
            SettingsTab.Audio => "audio",
            SettingsTab.Network => "joinpaste",
            _ => "difficulty"
        };
    }

    private string GetSettingsTabLabel(SettingsTab tab)
    {
        string label = GetSettingsTabName(tab);
        return _settingsTab == tab ? $"> {label}" : label;
    }

    private static string GetSettingsTabToken(SettingsTab tab)
    {
        return tab switch
        {
            SettingsTab.Gameplay => "gameplay",
            SettingsTab.Interface => "interface",
            SettingsTab.Graphic => "graphic",
            SettingsTab.Audio => "audio",
            SettingsTab.Network => "network",
            _ => "gameplay"
        };
    }

    private static string GetSettingsTabName(SettingsTab tab)
    {
        return tab switch
        {
            SettingsTab.Gameplay => "GamePlay",
            SettingsTab.Interface => "Settings",
            SettingsTab.Graphic => "Graphic",
            SettingsTab.Audio => "Audio",
            SettingsTab.Network => "Network",
            _ => "GamePlay"
        };
    }

    private string GetSettingsPanelTitle()
    {
        return _settingsTab switch
        {
            SettingsTab.Gameplay => "Gameplay settings",
            SettingsTab.Interface => "Interface settings",
            SettingsTab.Graphic => "Graphic settings",
            SettingsTab.Audio => "Audio settings",
            SettingsTab.Network => "Network settings",
            _ => "Settings"
        };
    }

    private string GetSettingsPanelSubtitle()
    {
        return _settingsTab switch
        {
            SettingsTab.Gameplay => "Nickname, difficulty, hints, and run setup.",
            SettingsTab.Interface => "HUD scale and debug overlays.",
            SettingsTab.Graphic => "Backdrop, particles, shaders, and shake.",
            SettingsTab.Audio => "Master, UI, world mix, and 3D audio.",
            SettingsTab.Network => "Stored join target and diagnostics.",
            _ => string.Empty
        };
    }

    private void CycleSettingsTab(int direction)
    {
        int currentIndex = Array.IndexOf(SettingsTabOrder, _settingsTab);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        int nextIndex = (currentIndex + direction) % SettingsTabOrder.Length;
        if (nextIndex < 0)
        {
            nextIndex += SettingsTabOrder.Length;
        }

        _settingsTab = SettingsTabOrder[nextIndex];
        _sound.PlayUi(UiSound.Hover);
        SetAnnouncement($"Settings tab: {GetSettingsTabName(_settingsTab)}", 0.8f);
    }

    private bool TrySelectSettingsTab(string id)
    {
        if (!id.StartsWith("tab:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string token = id.Substring(4);
        SettingsTab next = token.ToLowerInvariant() switch
        {
            "gameplay" => SettingsTab.Gameplay,
            "interface" => SettingsTab.Interface,
            "graphic" => SettingsTab.Graphic,
            "audio" => SettingsTab.Audio,
            "network" => SettingsTab.Network,
            _ => _settingsTab
        };

        if (next != _settingsTab)
        {
            _settingsTab = next;
            _sound.PlayUi(UiSound.Click);
            SetAnnouncement($"Settings tab: {GetSettingsTabName(_settingsTab)}", 0.8f);
        }

        return true;
    }

    private void LayoutSettingsButtons(Vector2 screen)
    {
        float s = GetMenuScale(screen);
        float navWidth = 220f * s;
        float contentWidth = 520f * s;
        float gap = 12f * s;
        float totalWidth = navWidth + gap + contentWidth;
        float navX = (screen.X - totalWidth) * 0.5f;
        float contentX = navX + navWidth + gap;
        float navTop = 182f * s;
        float contentTop = 182f * s;
        float navPad = 10f * s;
        float contentPad = 14f * s;
        float navButtonHeight = 44f * s;
        float contentButtonHeight = 50f * s;
        float navGap = 8f * s;
        float contentGap = 10f * s;

        List<MenuButton> navButtons = _menuButtons.Where(b => b.Id.StartsWith("tab:", StringComparison.OrdinalIgnoreCase)).ToList();
        MenuButton? backButton = _menuButtons.FirstOrDefault(b => b.Id == "back");
        List<MenuButton> actionButtons = _menuButtons.Where(b => b.Id != "back" && !b.Id.StartsWith("tab:", StringComparison.OrdinalIgnoreCase)).ToList();

        float navButtonWidth = navWidth - navPad * 2f;
        for (int i = 0; i < navButtons.Count; i++)
        {
            float y = navTop + 24f * s + i * (navButtonHeight + navGap);
            navButtons[i].Rect = System.Drawing.Rectangle.Round(new System.Drawing.RectangleF(navX + navPad, y, navButtonWidth, navButtonHeight));
        }

        if (backButton is not null)
        {
            float backY = navTop + 322f * s;
            backButton.Rect = System.Drawing.Rectangle.Round(new System.Drawing.RectangleF(navX + navPad, backY, navButtonWidth, navButtonHeight));
        }

        float actionWidth = contentWidth - contentPad * 2f;
        for (int i = 0; i < actionButtons.Count; i++)
        {
            float y = contentTop + 62f * s + i * (contentButtonHeight + contentGap);
            actionButtons[i].Rect = System.Drawing.Rectangle.Round(new System.Drawing.RectangleF(contentX + contentPad, y, actionWidth, contentButtonHeight));
        }
    }

    private string GetPauseSessionActionId()
    {
        return _network.Mode switch
        {
            NetMode.Client => "disconnect",
            NetMode.Host => "stopserver",
            _ => "title"
        };
    }

    private string GetPauseSessionActionLabel()
    {
        return _network.Mode switch
        {
            NetMode.Client => "Disconnect",
            NetMode.Host => "Stop server",
            _ => "Return to title"
        };
    }

    private string GetPauseMenuSubtitle()
    {
        return _network.Mode switch
        {
            NetMode.Client => "Disconnect from the server or bail out completely.",
            NetMode.Host => "Stop the server cleanly or kill the game from here.",
            _ => "Take a breath, then go back to murder.",
        };
    }

    private void LeaveSessionOrReturnToTitle()
    {
        string announcement = _network.Mode switch
        {
            NetMode.Client => "Disconnected from server",
            NetMode.Host => "Server stopped",
            _ => "Returned to title",
        };

        ResetToTitle();
        _sound.PlayUi(UiSound.Back);
        SetAnnouncement(announcement, 1.2f);
    }
}
