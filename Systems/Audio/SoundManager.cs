using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace SlayInspiredPrototype;

public enum UiSound
{
    Hover,
    Click,
    Back,
    Start,
    Error
}

public enum WorldSound
{
    RifleShot,
    SmgShot,
    ShotgunShot,
    CarbineShot,
    Explosion,
    Pickup,
    Hit,
    Barricade,
    Zombie,
    CrateHit,
    CrateBreak
}

public sealed class SoundManager
{
    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern int mciSendString(string command, StringBuilder? buffer, int bufferSize, IntPtr hwndCallback);

    private readonly object _sync = new object();
    private readonly List<(MemoryStream stream, DateTime utc)> _activeStreams = new List<(MemoryStream stream, DateTime utc)>();
    private readonly List<(string alias, DateTime utc)> _activeAliases = new List<(string alias, DateTime utc)>();
    private readonly Dictionary<string, DateTime> _cooldowns = new Dictionary<string, DateTime>();

    public bool Enabled { get; set; } = true;
    public string LastMixInfo { get; private set; } = "AUDIO stereo procedural";

    public void PlayUi(UiSound cue)
    {
        if (!Enabled)
        {
            return;
        }

        switch (cue)
        {
            case UiSound.Hover:
                PlayTone(650f, 0.05f, 0f, 0.16f, "ui_hover", 50);
                break;
            case UiSound.Click:
                PlayTone(860f, 0.08f, 0f, 0.22f, "ui_click", 40);
                break;
            case UiSound.Back:
                PlayTone(420f, 0.08f, 0f, 0.2f, "ui_back", 80);
                break;
            case UiSound.Start:
                PlayTone(980f, 0.12f, 0f, 0.28f, "ui_start", 120);
                break;
            default:
                PlayTone(220f, 0.12f, 0f, 0.24f, "ui_error", 120);
                break;
        }
    }

    public void PlayWorld(WorldSound cue, Vector2 listener, Vector2 source, float maxDistance, bool use3D)
    {
        if (!Enabled)
        {
            return;
        }

        Vector2 delta = source - listener;
        float dist = delta.Length();
        float attenuation = Math.Clamp(1f - dist / Math.Max(1f, maxDistance), 0.04f, 1f);
        float pan = use3D ? Math.Clamp(delta.X / Math.Max(1f, maxDistance), -1f, 1f) : 0f;

        float freq;
        float duration;
        string key;
        int cooldownMs;

        switch (cue)
        {
            case WorldSound.RifleShot:
                freq = 540f;
                duration = 0.04f;
                key = "rifle";
                cooldownMs = 18;
                break;
            case WorldSound.SmgShot:
                freq = 720f;
                duration = 0.025f;
                key = "smg";
                cooldownMs = 10;
                break;
            case WorldSound.ShotgunShot:
                freq = 220f;
                duration = 0.08f;
                key = "shotgun";
                cooldownMs = 90;
                break;
            case WorldSound.CarbineShot:
                freq = 470f;
                duration = 0.05f;
                key = "carbine";
                cooldownMs = 40;
                break;
            case WorldSound.Explosion:
                freq = 120f;
                duration = 0.18f;
                key = "explosion";
                cooldownMs = 100;
                break;
            case WorldSound.Pickup:
                freq = 980f;
                duration = 0.03f;
                key = "pickup";
                cooldownMs = 20;
                break;
            case WorldSound.Barricade:
                freq = 160f;
                duration = 0.05f;
                key = "barricade";
                cooldownMs = 25;
                break;
            case WorldSound.Zombie:
                freq = 280f;
                duration = 0.08f;
                key = "zombie";
                cooldownMs = 140;
                break;
            case WorldSound.CrateHit:
                freq = 210f;
                duration = 0.045f;
                key = "crate_hit";
                cooldownMs = 16;
                break;
            case WorldSound.CrateBreak:
                freq = 130f;
                duration = 0.18f;
                key = "crate_break";
                cooldownMs = 80;
                break;
            default:
                freq = 820f;
                duration = 0.02f;
                key = "hit";
                cooldownMs = 8;
                break;
        }

        LastMixInfo = $"AUDIO {(use3D ? "3D" : "2D")} pan {pan:0.00} attn {attenuation:0.00}";

        if (cue == WorldSound.CrateHit)
        {
            string? file = AssetLocator.Find("Assets/audio/crate_bullet.mp3");
            if (TryPlayFile(file, key, cooldownMs, attenuation * 0.9f, pan))
            {
                return;
            }
        }
        else if (cue == WorldSound.CrateBreak)
        {
            string? file = AssetLocator.Find("Assets/audio/crate_break.mp3");
            if (TryPlayFile(file, key, cooldownMs, attenuation, pan))
            {
                return;
            }
        }

        PlayTone(freq * (0.78f + attenuation * 0.35f), duration, pan, attenuation * 0.3f, key, cooldownMs);
    }

    private void PlayTone(float frequency, float durationSeconds, float pan, float volume, string key, int cooldownMs)
    {
        if (!CanPlay(key, cooldownMs))
        {
            return;
        }

        byte[] wavBytes = CreateStereoSineWave(frequency, durationSeconds, pan, volume);
        MemoryStream stream = new MemoryStream(wavBytes, writable: false);
        SoundPlayer player = new SoundPlayer(stream);

        lock (_sync)
        {
            _activeStreams.Add((stream, DateTime.UtcNow));
            CleanupStreamsUnsafe();
            CleanupAliasesUnsafe();
        }

        try
        {
            player.Play();
        }
        catch
        {
            try { stream.Dispose(); } catch { }
        }
    }

    private bool CanPlay(string key, int cooldownMs)
    {
        DateTime now = DateTime.UtcNow;
        lock (_sync)
        {
            if (_cooldowns.TryGetValue(key, out DateTime until) && until > now)
            {
                return false;
            }

            _cooldowns[key] = now.AddMilliseconds(cooldownMs);
            return true;
        }
    }

    private bool TryPlayFile(string? assetPath, string key, int cooldownMs, float volumeScale, float pan)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath) || !CanPlay(key, cooldownMs))
        {
            return false;
        }

        string alias = $"snd_{key}_{Environment.TickCount64}_{_activeAliases.Count}";
        string escapedPath = assetPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        float clampedVolume = Math.Clamp(volumeScale, 0.04f, 1f);
        float clampedPan = Math.Clamp(pan, -1f, 1f);
        float leftGain = clampedVolume * (clampedPan <= 0f ? 1f : 1f - clampedPan);
        float rightGain = clampedVolume * (clampedPan >= 0f ? 1f : 1f + clampedPan);
        int masterVolume = Math.Clamp((int)(clampedVolume * 1000f), 40, 1000);
        int leftVolume = Math.Clamp((int)(leftGain * 1000f), 0, 1000);
        int rightVolume = Math.Clamp((int)(rightGain * 1000f), 0, 1000);

        lock (_sync)
        {
            CleanupAliasesUnsafe();
            if (mciSendString($"open \"{escapedPath}\" type mpegvideo alias {alias}", null, 0, IntPtr.Zero) != 0)
            {
                return false;
            }

            mciSendString($"setaudio {alias} volume to {masterVolume}", null, 0, IntPtr.Zero);
            mciSendString($"setaudio {alias} left volume to {leftVolume}", null, 0, IntPtr.Zero);
            mciSendString($"setaudio {alias} right volume to {rightVolume}", null, 0, IntPtr.Zero);
            if (mciSendString($"play {alias} from 0", null, 0, IntPtr.Zero) != 0)
            {
                mciSendString($"close {alias}", null, 0, IntPtr.Zero);
                return false;
            }

            _activeAliases.Add((alias, DateTime.UtcNow));
            return true;
        }
    }

    private void CleanupAliasesUnsafe()
    {
        for (int i = _activeAliases.Count - 1; i >= 0; i--)
        {
            if ((DateTime.UtcNow - _activeAliases[i].utc).TotalSeconds > 8)
            {
                try { mciSendString($"close {_activeAliases[i].alias}", null, 0, IntPtr.Zero); } catch { }
                _activeAliases.RemoveAt(i);
            }
        }
    }

    private void CleanupStreamsUnsafe()
    {
        for (int i = _activeStreams.Count - 1; i >= 0; i--)
        {
            if ((DateTime.UtcNow - _activeStreams[i].utc).TotalSeconds > 3)
            {
                try { _activeStreams[i].stream.Dispose(); } catch { }
                _activeStreams.RemoveAt(i);
            }
        }
    }

    private static byte[] CreateStereoSineWave(float frequency, float durationSeconds, float pan, float volume)
    {
        const int sampleRate = 22050;
        int sampleCount = Math.Max(1, (int)(sampleRate * durationSeconds));
        short channels = 2;
        short bitsPerSample = 16;
        int blockAlign = channels * bitsPerSample / 8;
        int byteRate = sampleRate * blockAlign;
        int dataSize = sampleCount * blockAlign;
        byte[] bytes = new byte[44 + dataSize];

        WriteAscii(bytes, 0, "RIFF");
        WriteInt32(bytes, 4, 36 + dataSize);
        WriteAscii(bytes, 8, "WAVE");
        WriteAscii(bytes, 12, "fmt ");
        WriteInt32(bytes, 16, 16);
        WriteInt16(bytes, 20, 1);
        WriteInt16(bytes, 22, channels);
        WriteInt32(bytes, 24, sampleRate);
        WriteInt32(bytes, 28, byteRate);
        WriteInt16(bytes, 32, (short)blockAlign);
        WriteInt16(bytes, 34, bitsPerSample);
        WriteAscii(bytes, 36, "data");
        WriteInt32(bytes, 40, dataSize);

        float leftGain = volume * (pan <= 0f ? 1f : 1f - pan);
        float rightGain = volume * (pan >= 0f ? 1f : 1f + pan);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float env = 1f - i / (float)sampleCount;
            float sample = MathF.Sin(2f * MathF.PI * frequency * t) * env;

            short left = (short)(sample * short.MaxValue * leftGain);
            short right = (short)(sample * short.MaxValue * rightGain);

            int offset = 44 + i * blockAlign;
            WriteInt16(bytes, offset, left);
            WriteInt16(bytes, offset + 2, right);
        }

        return bytes;
    }

    private static void WriteAscii(byte[] data, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            data[offset + i] = (byte)value[i];
        }
    }

    private static void WriteInt16(byte[] data, int offset, short value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteInt32(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
        data[offset + 2] = (byte)((value >> 16) & 0xFF);
        data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
