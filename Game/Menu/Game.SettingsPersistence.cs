using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SlayInspiredPrototype;

public sealed partial class Game
{
    private void LoadPersistentSettings()
    {
        ApplyPersistentSettings(GameSettingsStorage.Load());
    }

    private void SavePersistentSettings()
    {
        try
        {
            GameSettingsStorage.Save(CapturePersistentSettings());
        }
        catch
        {
            SetAnnouncement("Settings save failed", 1.1f);
        }
    }

    private GameSettingsData CapturePersistentSettings()
    {
        return new GameSettingsData
        {
            Nickname = _preferredCallsign,
            Difficulty = GetDifficultyName(),
            UiScale = GetUiScaleName(),
            ShowHints = _showHints,
            ShuffleArenaOnStart = _shuffleArenaOnStart,
            ShowFpsHud = _showFpsHud,
            ShowPerfHud = _showPerfHud,
            MenuArtworkEnabled = _menuArtworkEnabled,
            SplashLogoEnabled = _splashLogoEnabled,
            ScreenShaderEnabled = _screenShaderEnabled,
            WorldParticlesEnabled = _worldParticlesEnabled,
            ScreenShakeEnabled = _screenShakeEnabled,
            AudioEnabled = _sound.Enabled,
            MasterVolumePercent = _sound.MasterVolumePercent,
            UiVolumePercent = _sound.UiVolumePercent,
            WorldVolumePercent = _sound.WorldVolumePercent,
            SpatialAudioEnabled = _sound.SpatialAudioEnabled,
            ShowNetworkDebug = _showNetworkDebug,
            JoinAddress = _joinAddress,
            JoinPort = _joinPort,
            MaxPlayers = _maxPlayers
        };
    }

    private void ApplyPersistentSettings(GameSettingsData data)
    {
        _preferredCallsign = NormalizeCallsign(data.Nickname, "P1");
        _difficulty = ParseDifficulty(data.Difficulty);
        _uiScale = ParseUiScale(data.UiScale);
        _showHints = data.ShowHints;
        _shuffleArenaOnStart = data.ShuffleArenaOnStart;
        _showFpsHud = data.ShowFpsHud;
        _showPerfHud = data.ShowPerfHud;
        if (_showPerfHud)
        {
            _showFpsHud = true;
        }

        _menuArtworkEnabled = data.MenuArtworkEnabled;
        _splashLogoEnabled = data.SplashLogoEnabled;
        _screenShaderEnabled = data.ScreenShaderEnabled;
        _worldParticlesEnabled = data.WorldParticlesEnabled;
        _screenShakeEnabled = data.ScreenShakeEnabled;
        _showNetworkDebug = data.ShowNetworkDebug;
        _joinAddress = string.IsNullOrWhiteSpace(data.JoinAddress) ? "127.0.0.1" : data.JoinAddress.Trim();
        _joinPort = Math.Clamp(data.JoinPort, 1, 65535);
        _maxPlayers = Math.Clamp(data.MaxPlayers, 1, 64);

        _sound.Enabled = data.AudioEnabled;
        _sound.MasterVolumePercent = ClampPercent(data.MasterVolumePercent);
        _sound.UiVolumePercent = ClampPercent(data.UiVolumePercent);
        _sound.WorldVolumePercent = ClampPercent(data.WorldVolumePercent);
        _sound.SpatialAudioEnabled = data.SpatialAudioEnabled;

        InvalidateMenuArtworkCache();
        RefreshPreferredPlayerPreview();
        SyncMenuInputBuffers();
    }

    private static int ClampPercent(int value) => Math.Clamp(value, 0, 100);

    private static string NormalizeCallsign(string? raw, string fallback = "P1")
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        string filtered = new string(raw.Trim().Where(ch => !char.IsControl(ch)).ToArray());
        if (filtered.Length > 16)
        {
            filtered = filtered.Substring(0, 16);
        }

        return string.IsNullOrWhiteSpace(filtered) ? fallback : filtered;
    }

    private static DifficultyMode ParseDifficulty(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "casual" => DifficultyMode.Casual,
            "nightmare" => DifficultyMode.Nightmare,
            _ => DifficultyMode.Survival
        };
    }

    private static UiScaleMode ParseUiScale(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "compact" => UiScaleMode.Tiny,
            "large" => UiScaleMode.Compact,
            "huge" => UiScaleMode.Normal,
            "normal" => UiScaleMode.Normal,
            _ => UiScaleMode.Compact
        };
    }

    private void InvalidateMenuArtworkCache()
    {
        _menuBackgroundResolved = false;
        _logoResolved = false;
        try { _menuBackgroundBitmap?.Dispose(); } catch { }
        try { _logoBitmap?.Dispose(); } catch { }
        _menuBackgroundBitmap = null;
        _logoBitmap = null;
    }

    private void RefreshPreferredPlayerPreview()
    {
        if (_phase == GamePhase.Playing || _phase == GamePhase.Paused || _phase == GamePhase.GameOver || _network.Mode != NetMode.None)
        {
            return;
        }

        if (_players.Count > 0)
        {
            SetupPlayers(_preferredCallsign, _players[0].Accent);
        }
    }

    private void CyclePercentSetting(Action<int> setter, int currentValue, int step = 10)
    {
        int next = currentValue >= 100 ? 0 : Math.Min(100, currentValue + step);
        setter(next);
        _sound.PlayUi(UiSound.Click);
        SavePersistentSettings();
    }

    private void SetMasterVolumePercent(int value)
    {
        _sound.MasterVolumePercent = ClampPercent(value);
    }

    private void SetUiVolumePercent(int value)
    {
        _sound.UiVolumePercent = ClampPercent(value);
    }

    private void SetWorldVolumePercent(int value)
    {
        _sound.WorldVolumePercent = ClampPercent(value);
    }

    private void TryPasteNickname()
    {
        try
        {
            string text = Clipboard.GetText()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                _sound.PlayUi(UiSound.Error);
                SetAnnouncement("Clipboard nickname is empty", 1f);
                return;
            }

            _preferredCallsign = NormalizeCallsign(text, _preferredCallsign);
            RefreshPreferredPlayerPreview();
            _nicknameInputBuffer = _preferredCallsign;
            _sound.PlayUi(UiSound.Click);
            SavePersistentSettings();
            SetAnnouncement($"Nickname: {_preferredCallsign}", 1.1f);
        }
        catch
        {
            _sound.PlayUi(UiSound.Error);
            SetAnnouncement("Nickname paste failed", 1.2f);
        }
    }

    private void ResetNickname()
    {
        _preferredCallsign = "P1";
        RefreshPreferredPlayerPreview();
        _nicknameInputBuffer = _preferredCallsign;
        _sound.PlayUi(UiSound.Click);
        SavePersistentSettings();
        SetAnnouncement("Nickname reset to P1", 1f);
    }
}
