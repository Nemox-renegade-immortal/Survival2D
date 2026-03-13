using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace SlayInspiredPrototype;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        GameWindowSettings game = new GameWindowSettings
        {
            UpdateFrequency = 120
        };

        NativeWindowSettings native = new NativeWindowSettings
        {
            Title = "Survival 2D",
            ClientSize = new Vector2i(1280, 720),
            MinimumClientSize = new Vector2i(960, 600),
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            WindowBorder = WindowBorder.Resizable,
            StartVisible = true,
            StartFocused = true,
            NumberOfSamples = 0
        };

        using ImGuiGameWindow window = new ImGuiGameWindow(game, native);
        window.Run();
    }
}