using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using TkKey = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using TkMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;
using WinKey = System.Windows.Forms.Keys;
using WinMouseButtons = System.Windows.Forms.MouseButtons;

namespace SlayInspiredPrototype;

public sealed class ImGuiGameWindow : GameWindow
{
    private readonly InputState _input = new();
    private readonly Game _game = new();
    private readonly Stopwatch _titleClock = Stopwatch.StartNew();
    private readonly (TkKey tk, WinKey win)[] _keyMap =
    {
        (TkKey.W, WinKey.W),
        (TkKey.A, WinKey.A),
        (TkKey.S, WinKey.S),
        (TkKey.D, WinKey.D),
        (TkKey.Q, WinKey.Q),
        (TkKey.E, WinKey.E),
        (TkKey.R, WinKey.R),
        (TkKey.F, WinKey.F),
        (TkKey.G, WinKey.G),
        (TkKey.B, WinKey.B),
        (TkKey.C, WinKey.C),
        (TkKey.T, WinKey.T),
        (TkKey.Tab, WinKey.Tab),
        (TkKey.Left, WinKey.Left),
        (TkKey.Right, WinKey.Right),
        (TkKey.Space, WinKey.Space),
        (TkKey.Escape, WinKey.Escape),
        (TkKey.Enter, WinKey.Enter),
        (TkKey.D1, WinKey.D1),
        (TkKey.D2, WinKey.D2),
        (TkKey.D3, WinKey.D3),
        (TkKey.D4, WinKey.D4),
        (TkKey.D5, WinKey.D5),
        (TkKey.D6, WinKey.D6),
        (TkKey.D7, WinKey.D7),
        (TkKey.D8, WinKey.D8),
        (TkKey.D9, WinKey.D9)
    };

    private ImGuiController? _imgui;
    private bool _fullscreen;
    private Vector2i _windowedSize;
    private Vector2i _windowedLocation;
    private OpenTK.Windowing.Common.WindowState _windowedState;
    private float _updateFps;
    private float _renderFps;
    private float _lastUpdateMs;
    private float _lastRenderMs;
    private float _lastFrameMs;
    private double _statsTimer;
    private int _updateCounter;
    private int _renderCounter;
    private bool _previousF11Down;
    private InputState _imguiFrameInput = new();

    public ImGuiGameWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    {
        VSync = VSyncMode.Off;
        IsEventDriven = false;
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.ClearColor(0.04f, 0.05f, 0.07f, 1f);
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);
        _imgui = new ImGuiController(ClientSize.X, ClientSize.Y);
        _windowedSize = ClientSize;
        _windowedLocation = Location;
        _windowedState = WindowState;
    }

    protected override void OnUnload()
    {
        _imgui?.Dispose();
        base.OnUnload();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        if (ClientSize.X > 0 && ClientSize.Y > 0)
        {
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        }
    }

    protected override void OnFocusedChanged(FocusedChangedEventArgs e)
    {
        base.OnFocusedChanged(e);
        if (!IsFocused)
        {
            _input.EndFrame();
        }
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (_imgui is null || IsExiting)
        {
            return;
        }

        if (WindowState == OpenTK.Windowing.Common.WindowState.Minimized)
        {
            return;
        }

        float dt = (float)Math.Clamp(args.Time, 0.0005, 1.0 / 30.0);
        if (!IsFocused)
        {
            dt = MathF.Min(dt, 1f / 60f);
        }

        bool f11Down = KeyboardState.IsKeyDown(TkKey.F11);
        if (f11Down && !_previousF11Down)
        {
            ToggleFullscreen();
        }
        _previousF11Down = f11Down;

        PollInput();

        InputState frameInput = _input.SnapshotAndEndFrame();
        _imguiFrameInput = frameInput;

        Stopwatch updateWatch = Stopwatch.StartNew();
        _game.Update(dt, frameInput, new Size(ClientSize.X, ClientSize.Y));
        updateWatch.Stop();

        _lastUpdateMs = (float)updateWatch.Elapsed.TotalMilliseconds;
        _lastFrameMs = dt * 1000f;
        _updateCounter++;

        _statsTimer += dt;
        if (_statsTimer >= 0.25)
        {
            _updateFps = _updateCounter / (float)_statsTimer;
            _renderFps = _renderCounter / (float)_statsTimer;
            _updateCounter = 0;
            _renderCounter = 0;
            _statsTimer = 0.0;
        }

        _game.SetRuntimeStats(_updateFps, _renderFps, _lastUpdateMs, _lastRenderMs, _lastFrameMs, "OpenGL + ImGui renderer");

        if (_titleClock.Elapsed.TotalSeconds >= 0.35)
        {
            Title = $"Survival  |  FPS {_renderFps:0}  |  UPS {_updateFps:0}  |  {_lastFrameMs:0.00} ms";
            _titleClock.Restart();
        }
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        if (_imgui is null || IsExiting)
        {
            return;
        }

        Stopwatch renderWatch = Stopwatch.StartNew();

        GL.Clear(ClearBufferMask.ColorBufferBit);
        _imgui.Update(ClientSize.X, ClientSize.Y, (float)Math.Max(args.Time, 1.0 / 240.0), _imguiFrameInput);
        _game.DrawImGui(new System.Numerics.Vector2(ClientSize.X, ClientSize.Y));
        _imgui.Render();
        SwapBuffers();

        renderWatch.Stop();
        _lastRenderMs = (float)renderWatch.Elapsed.TotalMilliseconds;
        _renderCounter++;
    }

    private void PollInput()
    {
        var mouse = MousePosition;
        _input.MouseScreen = new Point((int)mouse.X, (int)mouse.Y);
        _input.SetMouseButton(WinMouseButtons.Left, MouseState.IsButtonDown(TkMouseButton.Left));
        _input.SetMouseButton(WinMouseButtons.Right, MouseState.IsButtonDown(TkMouseButton.Right));

        float wheel = MouseState.ScrollDelta.Y;
        if (MathF.Abs(wheel) > 0.001f)
        {
            _input.AddMouseWheel((int)(wheel * 120f));
        }

        for (int i = 0; i < _keyMap.Length; i++)
        {
            _input.SetKey(_keyMap[i].win, KeyboardState.IsKeyDown(_keyMap[i].tk));
        }
    }

    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _windowedSize = ClientSize;
            _windowedLocation = Location;
            _windowedState = WindowState;
            WindowState = OpenTK.Windowing.Common.WindowState.Normal;
            WindowBorder = WindowBorder.Hidden;
            WindowState = OpenTK.Windowing.Common.WindowState.Fullscreen;
            _fullscreen = true;
        }
        else
        {
            WindowState = OpenTK.Windowing.Common.WindowState.Normal;
            WindowBorder = WindowBorder.Resizable;
            ClientSize = _windowedSize;
            Location = _windowedLocation;
            WindowState = _windowedState;
            _fullscreen = false;
        }
    }
}