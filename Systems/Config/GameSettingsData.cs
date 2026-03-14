using System;
using System.IO;
using System.Text.Json;

namespace SlayInspiredPrototype;

public sealed class GameSettingsData
{
    public string Nickname { get; set; } = "P1";
    public string Difficulty { get; set; } = "Survival";
    public string UiScale { get; set; } = "Large";
    public bool ShowHints { get; set; } = true;
    public bool ShuffleArenaOnStart { get; set; } = true;
    public bool ShowFpsHud { get; set; }
    public bool ShowPerfHud { get; set; }

    public bool MenuArtworkEnabled { get; set; } = true;
    public bool SplashLogoEnabled { get; set; } = true;
    public bool ScreenShaderEnabled { get; set; } = true;
    public bool WorldParticlesEnabled { get; set; } = true;
    public bool ScreenShakeEnabled { get; set; } = true;

    public bool AudioEnabled { get; set; } = true;
    public int MasterVolumePercent { get; set; } = 100;
    public int UiVolumePercent { get; set; } = 100;
    public int WorldVolumePercent { get; set; } = 100;
    public bool SpatialAudioEnabled { get; set; } = true;

    public bool ShowNetworkDebug { get; set; }
    public string JoinAddress { get; set; } = "127.0.0.1";
    public int JoinPort { get; set; } = 7777;
    public int MaxPlayers { get; set; } = 64;
}

internal static class GameSettingsStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetSettingsFilePath()
    {
        string root = ResolveRoot();
        string directory = Path.Combine(root, "Assets", "Settings");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "game_settings.json");
    }

    public static GameSettingsData Load()
    {
        string path = GetSettingsFilePath();
        try
        {
            if (!File.Exists(path))
            {
                GameSettingsData defaults = new GameSettingsData();
                Save(defaults);
                return defaults;
            }

            string json = File.ReadAllText(path);
            GameSettingsData? loaded = JsonSerializer.Deserialize<GameSettingsData>(json, JsonOptions);
            if (loaded is null)
            {
                return new GameSettingsData();
            }

            return loaded;
        }
        catch
        {
            return new GameSettingsData();
        }
    }

    public static void Save(GameSettingsData data)
    {
        string path = GetSettingsFilePath();
        string json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string ResolveRoot()
    {
        string[] roots =
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetDirectoryName(typeof(GameSettingsStorage).Assembly.Location) ?? AppContext.BaseDirectory
        };

        foreach (string root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string? current = Path.GetFullPath(root);
            for (int depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(current); depth++)
            {
                if (File.Exists(Path.Combine(current, "Survival2D.csproj")) || Directory.Exists(Path.Combine(current, "Assets")))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName;
            }
        }

        return Path.GetFullPath(AppContext.BaseDirectory);
    }
}
