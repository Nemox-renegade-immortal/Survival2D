using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace SlayInspiredPrototype;

public sealed class Window : Form
{
    private readonly Stopwatch _stopwatch = new Stopwatch();
    private readonly InputState _input = new InputState();
    private readonly Game _game = new Game();
    private readonly object _gameSync = new object();
    private Thread? _loopThread;
    private volatile bool _running;
    private volatile bool _focused = true;
    private volatile int _paintQueued;
    private volatile int _clientWidth;
    private volatile int _clientHeight;
    private volatile float _updateFps;
    private volatile float _renderFps;
    private volatile float _lastUpdateMs;
    private volatile float _lastRenderMs;
    private volatile float _lastFrameMs;
    private double _lastTime;
    private double _statsTimer;
    private double _titleTimer;
    private int _updateCounter;
    private int _renderCounter;
    private bool _fullscreen;
    private Rectangle _windowedBounds;
    private FormWindowState _windowedState;
    private FormBorderStyle _windowedBorderStyle;
    private readonly string _rendererLabel = "GDI+ CPU renderer | OS compositor may use GPU";

    public Window()
    {
        Text = "Slay-Inspired Prototype Overkill NET";
        ClientSize = new Size(1280, 720);
        MinimumSize = new Size(960, 600);
        BackColor = Color.Black;
        DoubleBuffered = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;
        _clientWidth = ClientSize.Width;
        _clientHeight = ClientSize.Height;
        _windowedBounds = Bounds;
        _windowedState = WindowState;
        _windowedBorderStyle = FormBorderStyle;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Opaque |
            ControlStyles.ResizeRedraw,
            true);

        UpdateStyles();

        ThreadPool.SetMinThreads(
            Math.Max(4, Environment.ProcessorCount * 2),
            Math.Max(4, Environment.ProcessorCount * 2));

        KeyDown += HandleKeyDown;
        KeyUp += (_, e) => _input.SetKey(e.KeyCode, false);
        MouseDown += (_, e) => _input.SetMouseButton(e.Button, true);
        MouseUp += (_, e) => _input.SetMouseButton(e.Button, false);
        MouseMove += (_, e) => _input.MouseScreen = e.Location;
        MouseWheel += (_, e) => _input.AddMouseWheel(e.Delta);
        Resize += (_, _) =>
        {
            _clientWidth = ClientSize.Width;
            _clientHeight = ClientSize.Height;
            QueuePaint();
        };
        Activated += (_, _) => _focused = true;
        Deactivate += (_, _) => _focused = false;
        Shown += (_, _) => StartGameLoop();
        FormClosing += (_, _) => StopGameLoop();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        _input.SetKey(e.KeyCode, true);
    }

    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _windowedBounds = Bounds;
            _windowedState = WindowState;
            _windowedBorderStyle = FormBorderStyle;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            TopMost = true;
            _fullscreen = true;
        }
        else
        {
            TopMost = false;
            FormBorderStyle = _windowedBorderStyle;
            WindowState = FormWindowState.Normal;
            Bounds = _windowedBounds;
            WindowState = _windowedState;
            _fullscreen = false;
        }

        _clientWidth = ClientSize.Width;
        _clientHeight = ClientSize.Height;
        QueuePaint();
    }

    private void StartGameLoop()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        _stopwatch.Start();
        _lastTime = _stopwatch.Elapsed.TotalSeconds;
        _loopThread = new Thread(GameLoop)
        {
            IsBackground = true,
            Name = "GameLoop",
            Priority = ThreadPriority.AboveNormal
        };
        _loopThread.Start();
    }

    private void StopGameLoop()
    {
        _running = false;
        if (_loopThread is not null && _loopThread.IsAlive)
        {
            _loopThread.Join(250);
        }
    }

    private void GameLoop()
    {
        const double targetStep = 1.0 / 120.0;
        const double maxStep = 1.0 / 30.0;

        while (_running)
        {
            if (IsDisposed)
            {
                break;
            }

            if (!Visible || WindowState == FormWindowState.Minimized)
            {
                Thread.Sleep(16);
                _lastTime = _stopwatch.Elapsed.TotalSeconds;
                continue;
            }

            double frameStart = _stopwatch.Elapsed.TotalSeconds;
            double elapsed = frameStart - _lastTime;

            if (elapsed < targetStep * 0.85)
            {
                Thread.Sleep(0);
                continue;
            }

            _lastTime = frameStart;
            float dt = (float)Math.Clamp(elapsed, 0.0005, maxStep);
            if (!_focused)
            {
                dt = MathF.Min(dt, 1f / 60f);
            }

            InputState frameInput = _input.SnapshotAndEndFrame();
            Size size = new Size(_clientWidth, _clientHeight);

            Stopwatch updateWatch = Stopwatch.StartNew();
            lock (_gameSync)
            {
                _game.Update(dt, frameInput, size);
            }
            updateWatch.Stop();

            _lastUpdateMs = (float)updateWatch.Elapsed.TotalMilliseconds;
            _lastFrameMs = dt * 1000f;
            Interlocked.Increment(ref _updateCounter);

            _statsTimer += dt;
            _titleTimer += dt;
            if (_statsTimer >= 0.25)
            {
                int updates = Interlocked.Exchange(ref _updateCounter, 0);
                int renders = Interlocked.Exchange(ref _renderCounter, 0);
                _updateFps = updates / (float)_statsTimer;
                _renderFps = renders / (float)_statsTimer;
                _statsTimer = 0.0;
            }

            lock (_gameSync)
            {
                _game.SetRuntimeStats(_updateFps, _renderFps, _lastUpdateMs, _lastRenderMs, _lastFrameMs, _rendererLabel);
            }

            if (_titleTimer >= 0.4)
            {
                _titleTimer = 0.0;
                UpdateWindowTitle();
            }

            QueuePaint();

            double frameTime = _stopwatch.Elapsed.TotalSeconds - frameStart;
            double sleep = targetStep - frameTime;
            if (sleep > 0.0015)
            {
                int ms = Math.Max(1, (int)(sleep * 1000.0) - 1);
                Thread.Sleep(ms);
            }
            else if (sleep > 0.0)
            {
                Thread.Sleep(0);
            }
        }
    }

    private void UpdateWindowTitle()
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        string title = $"Slay-Inspired Prototype Overkill NET  |  FPS {_renderFps:0}  |  UPS {_updateFps:0}  |  {_lastFrameMs:0.00} ms";

        try
        {
            BeginInvoke((Action)(() =>
            {
                if (!IsDisposed)
                {
                    Text = title;
                }
            }));
        }
        catch
        {
        }
    }

    private void QueuePaint()
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        if (Interlocked.Exchange(ref _paintQueued, 1) != 0)
        {
            return;
        }

        try
        {
            BeginInvoke((Action)(() =>
            {
                Interlocked.Exchange(ref _paintQueued, 0);
                Invalidate();
            }));
        }
        catch
        {
            Interlocked.Exchange(ref _paintQueued, 0);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            return;
        }

        Stopwatch renderWatch = Stopwatch.StartNew();
        lock (_gameSync)
        {
            _game.Draw(e.Graphics, ClientSize, _input.Snapshot());
        }
        renderWatch.Stop();

        _lastRenderMs = (float)renderWatch.Elapsed.TotalMilliseconds;
        Interlocked.Increment(ref _renderCounter);
    }
}