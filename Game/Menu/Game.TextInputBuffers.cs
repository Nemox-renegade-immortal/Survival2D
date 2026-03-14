using System;
using ImGuiNET;

namespace SlayInspiredPrototype;

public sealed partial class Game
{
    private string _nicknameInputBuffer = "P1";
    private string _hostPortInputBuffer = "7777";
    private string _hostSlotsInputBuffer = "64";
    private string _joinAddressInputBuffer = "127.0.0.1";
    private string _joinPortInputBuffer = "7777";

    private void SyncMenuInputBuffers()
    {
        _nicknameInputBuffer = NormalizeCallsign(_preferredCallsign, "P1");
        _hostPortInputBuffer = _joinPort.ToString();
        _hostSlotsInputBuffer = _maxPlayers.ToString();
        _joinAddressInputBuffer = string.IsNullOrWhiteSpace(_joinAddress) ? "127.0.0.1" : _joinAddress.Trim();
        _joinPortInputBuffer = _joinPort.ToString();
    }

    private static void CommitImGuiTextIfNeeded(bool submitted, Action commitAction)
    {
        if (submitted || ImGui.IsItemDeactivatedAfterEdit())
        {
            commitAction();
        }
    }

    private void CommitNicknameInput(bool announce = true)
    {
        string normalized = NormalizeCallsign(_nicknameInputBuffer, "P1");
        _nicknameInputBuffer = normalized;
        if (normalized == _preferredCallsign)
        {
            return;
        }

        _preferredCallsign = normalized;
        RefreshPreferredPlayerPreview();
        _sound.PlayUi(UiSound.Click);
        SavePersistentSettings();
        if (announce)
        {
            SetAnnouncement($"Nickname: {_preferredCallsign}", 1f);
        }
    }

    private void CommitHostPortInput(bool announce = false)
    {
        if (!TryParseIntField(_hostPortInputBuffer, out int parsed))
        {
            _hostPortInputBuffer = _joinPort.ToString();
            return;
        }

        int clamped = Math.Clamp(parsed, 1024, 65535);
        _hostPortInputBuffer = clamped.ToString();
        _joinPortInputBuffer = clamped.ToString();
        if (clamped == _joinPort)
        {
            return;
        }

        _joinPort = clamped;
        _sound.PlayUi(UiSound.Click);
        SavePersistentSettings();
        if (announce)
        {
            SetAnnouncement($"Host port: {_joinPort}", 1f);
        }
    }

    private void CommitHostSlotsInput(bool announce = false)
    {
        if (!TryParseIntField(_hostSlotsInputBuffer, out int parsed))
        {
            _hostSlotsInputBuffer = _maxPlayers.ToString();
            return;
        }

        int clamped = Math.Clamp(parsed, 1, 64);
        _hostSlotsInputBuffer = clamped.ToString();
        if (clamped == _maxPlayers)
        {
            return;
        }

        _maxPlayers = clamped;
        _sound.PlayUi(UiSound.Click);
        SavePersistentSettings();
        if (announce)
        {
            SetAnnouncement($"Slots: {_maxPlayers}", 1f);
        }
    }

    private void CommitJoinAddressInput(bool announce = false)
    {
        string raw = _joinAddressInputBuffer.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            _joinAddressInputBuffer = string.IsNullOrWhiteSpace(_joinAddress) ? "127.0.0.1" : _joinAddress;
            return;
        }

        string beforeAddress = _joinAddress;
        int beforePort = _joinPort;
        ApplyJoinAddress(raw);
        _joinAddressInputBuffer = _joinAddress;
        _joinPortInputBuffer = _joinPort.ToString();
        _hostPortInputBuffer = _joinPort.ToString();

        if (beforeAddress == _joinAddress && beforePort == _joinPort)
        {
            return;
        }

        _sound.PlayUi(UiSound.Click);
        SavePersistentSettings();
        if (announce)
        {
            SetAnnouncement($"Join target: {_joinAddress}:{_joinPort}", 1.1f);
        }
    }

    private void CommitJoinPortInput(bool announce = false)
    {
        if (!TryParseIntField(_joinPortInputBuffer, out int parsed))
        {
            _joinPortInputBuffer = _joinPort.ToString();
            return;
        }

        int clamped = Math.Clamp(parsed, 1, 65535);
        _joinPortInputBuffer = clamped.ToString();
        _hostPortInputBuffer = clamped.ToString();
        if (clamped == _joinPort)
        {
            return;
        }

        _joinPort = clamped;
        _sound.PlayUi(UiSound.Click);
        SavePersistentSettings();
        if (announce)
        {
            SetAnnouncement($"Join port: {_joinPort}", 1f);
        }
    }

    private static bool TryParseIntField(string raw, out int value)
    {
        return int.TryParse(raw.Trim(), out value);
    }
}
