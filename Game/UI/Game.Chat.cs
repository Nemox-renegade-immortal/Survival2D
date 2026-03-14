using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;

namespace SlayInspiredPrototype;

public sealed partial class Game
{
    private sealed class ChatEntry
    {
        public string Sender = string.Empty;
        public string Message = string.Empty;
        public Color Accent = Color.Gainsboro;
        public bool IsSystem;
    }

    private readonly List<ChatEntry> _chatEntries = new List<ChatEntry>();
    private string _chatInputBuffer = string.Empty;
    private bool _chatOpen;
    private bool _chatFocusInputRequested;
    private bool _chatScrollToBottom;

    private const int MaxChatEntries = 48;
    private const int MaxChatMessageLength = 160;

    private float GetAdaptiveImGuiHudScale(Vector2 screen)
    {
        float baseScale = MathF.Min(screen.X / 1600f, screen.Y / 900f);
        float autoScale = baseScale < 1f
            ? Math.Clamp(baseScale, 0.72f, 1f)
            : Math.Clamp(1f + (baseScale - 1f) * 0.12f, 1f, 1.08f);

        float presetScale = _uiScale switch
        {
            UiScaleMode.Tiny => 0.90f,
            UiScaleMode.Normal => 1.12f,
            _ => 1.00f
        };

        return Math.Clamp(autoScale * presetScale, 0.74f, 1.18f);
    }

    private Vector2 GetChatPanelSize(Vector2 screen, float hudScale)
    {
        float width = Math.Clamp(screen.X * 0.38f, 300f * hudScale, 520f * hudScale);
        float height = Math.Clamp(screen.Y * 0.22f, 124f * hudScale, 208f * hudScale);
        return new Vector2(width, height);
    }

    private RectangleF GetChatPanelRect(Vector2 screen, float hudScale)
    {
        float margin = 14f * hudScale;
        float hotbarHeight = 134f * hudScale;
        Vector2 size = GetChatPanelSize(screen, hudScale);
        float y = screen.Y - hotbarHeight - margin - size.Y - 12f * hudScale;
        return new RectangleF(margin, y, size.X, size.Y);
    }

    private float GetChatReservedHeight(Vector2 screen, float hudScale)
    {
        return GetChatPanelSize(screen, hudScale).Y + 12f * hudScale;
    }

    private static string SanitizeChatText(string? raw, int maxLength = MaxChatMessageLength)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        List<char> chars = new List<char>(Math.Min(maxLength, raw.Length));
        foreach (char ch in raw.Trim())
        {
            if (char.IsControl(ch) && ch != ' ')
            {
                continue;
            }

            chars.Add(ch);
            if (chars.Count >= maxLength)
            {
                break;
            }
        }

        return new string(chars.ToArray()).Trim();
    }

    private void ClearChatHistory()
    {
        _chatEntries.Clear();
        _chatInputBuffer = string.Empty;
        _chatOpen = false;
        _chatFocusInputRequested = false;
        _chatScrollToBottom = false;
    }

    private void SeedSessionChat()
    {
        ClearChatHistory();
        AddSystemChat(_network.Mode switch
        {
            NetMode.Host => $"Session chat live. Hosting on {_network.HostAddress}:{_network.Port}.",
            NetMode.Client => $"Session chat live. Connected to {_network.HostAddress}:{_network.Port}.",
            _ => "Local chat ready."
        });
    }

    private void AddSystemChat(string message)
    {
        AddChatEntry("SYSTEM", message, Color.FromArgb(180, 214, 232, 246), true);
    }

    private void AddChatEntry(string sender, string message, Color accent, bool isSystem = false)
    {
        string cleanMessage = SanitizeChatText(message);
        if (string.IsNullOrWhiteSpace(cleanMessage))
        {
            return;
        }

        _chatEntries.Add(new ChatEntry
        {
            Sender = string.IsNullOrWhiteSpace(sender) ? "P?" : sender.Trim(),
            Message = cleanMessage,
            Accent = accent,
            IsSystem = isSystem
        });

        if (_chatEntries.Count > MaxChatEntries)
        {
            _chatEntries.RemoveRange(0, _chatEntries.Count - MaxChatEntries);
        }

        _chatScrollToBottom = true;
    }

    private void PumpIncomingChatMessages()
    {
        IReadOnlyList<NetChatMessage> messages = _network.DrainIncomingChatMessages();
        for (int i = 0; i < messages.Count; i++)
        {
            NetChatMessage message = messages[i];
            if (string.IsNullOrWhiteSpace(message.Message))
            {
                continue;
            }

            if (message.IsSystem)
            {
                AddSystemChat(message.Message);
                continue;
            }

            string sender = string.IsNullOrWhiteSpace(message.Sender) ? $"P{Math.Max(1, message.SenderPlayerId)}" : message.Sender;
            AddChatEntry(sender, message.Message, Color.FromArgb(message.AccentArgb));
        }
    }

    private void OpenChatInput()
    {
        if (_phase != GamePhase.Playing && _phase != GamePhase.Paused)
        {
            return;
        }

        _chatOpen = true;
        _chatFocusInputRequested = true;
    }

    private void CloseChatInput(bool clearBuffer)
    {
        _chatOpen = false;
        _chatFocusInputRequested = false;
        if (clearBuffer)
        {
            _chatInputBuffer = string.Empty;
        }
    }

    private void SubmitChatMessage()
    {
        string text = SanitizeChatText(_chatInputBuffer);
        _chatInputBuffer = string.Empty;
        _chatFocusInputRequested = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            _chatOpen = false;
            return;
        }

        Player player = Player;
        if (_network.Mode == NetMode.Host)
        {
            AddChatEntry(player.Callsign, text, player.Accent);
            _network.SendChatMessage(player.Callsign, player.Accent, text);
        }
        else if (_network.Mode == NetMode.Client)
        {
            _network.SendChatMessage(player.Callsign, player.Accent, text);
        }
        else
        {
            AddChatEntry(player.Callsign, text, player.Accent);
        }

        _chatOpen = false;
        _chatScrollToBottom = true;
    }

    private bool HandleGameplayChatInput(InputState input, Size clientSize)
    {
        if (input.WasPressed(Keys.Enter) && !_chatOpen)
        {
            OpenChatInput();
            return true;
        }

        if (_chatOpen && input.WasPressed(Keys.Escape))
        {
            CloseChatInput(true);
            return true;
        }

        if (_chatOpen)
        {
            return true;
        }

        Vector2 screen = new Vector2(clientSize.Width, clientSize.Height);
        RectangleF chatRect = GetChatPanelRect(screen, GetAdaptiveImGuiHudScale(screen));
        if ((input.LeftMousePressed || input.RightMousePressed) && chatRect.Contains(input.MouseScreen.X, input.MouseScreen.Y))
        {
            return true;
        }

        return false;
    }

    private void PushLocalPlayerNetworkState(Player player)
    {
        _network.PushLocalState(
            player.Callsign,
            player.Accent,
            player.Position,
            player.AimAngle,
            player.Health,
            player.IsAlive,
            player.Armor,
            player.MaxHealth,
            player.Weapon.Name,
            player.CurrentWeapon.Level,
            player.SelectedWeaponIndex,
            GetSelectedHotbarRawIndex(),
            player.CurrentWeapon.AmmoInClip,
            player.CurrentWeapon.AmmoReserve,
            player.Credits,
            player.Scrap,
            player.BarricadeKits,
            player.Kills,
            player.TurretCharges,
            player.MaxTurretCharges,
            player.Adrenaline,
            player.MaxAdrenaline,
            player.LastMoveInput,
            player.MoveBlend,
            player.FireTimer,
            player.WeaponStateSequence,
            player.IsReloading,
            player.ReloadTimer,
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
}
