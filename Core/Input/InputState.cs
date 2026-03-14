using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlayInspiredPrototype;

public sealed class InputState
{
    private readonly object _sync = new();
    private readonly HashSet<Keys> _keysDown = new();
    private readonly HashSet<Keys> _keysPressed = new();
    private readonly HashSet<Keys> _keysReleased = new();
    private readonly List<int> _textInputCodepoints = new();
    private Point _mouseScreen;
    private bool _leftMouseDown;
    private bool _leftMousePressed;
    private bool _rightMouseDown;
    private bool _rightMousePressed;
    private int _mouseWheelDelta;

    public Point MouseScreen
    {
        get
        {
            lock (_sync)
            {
                return _mouseScreen;
            }
        }
        set
        {
            lock (_sync)
            {
                _mouseScreen = value;
            }
        }
    }

    public bool LeftMouseDown
    {
        get
        {
            lock (_sync)
            {
                return _leftMouseDown;
            }
        }
    }

    public bool LeftMousePressed
    {
        get
        {
            lock (_sync)
            {
                return _leftMousePressed;
            }
        }
    }

    public bool RightMouseDown
    {
        get
        {
            lock (_sync)
            {
                return _rightMouseDown;
            }
        }
    }

    public bool RightMousePressed
    {
        get
        {
            lock (_sync)
            {
                return _rightMousePressed;
            }
        }
    }

    public int MouseWheelDelta
    {
        get
        {
            lock (_sync)
            {
                return _mouseWheelDelta;
            }
        }
    }

    public IReadOnlyList<int> TextInputCodepoints
    {
        get
        {
            lock (_sync)
            {
                return _textInputCodepoints.ToArray();
            }
        }
    }

    public bool IsDown(Keys key)
    {
        lock (_sync)
        {
            return _keysDown.Contains(key);
        }
    }

    public bool WasPressed(Keys key)
    {
        lock (_sync)
        {
            return _keysPressed.Contains(key);
        }
    }

    public bool WasReleased(Keys key)
    {
        lock (_sync)
        {
            return _keysReleased.Contains(key);
        }
    }

    public void SetKey(Keys key, bool pressed)
    {
        lock (_sync)
        {
            if (pressed)
            {
                if (_keysDown.Add(key))
                {
                    _keysPressed.Add(key);
                }
            }
            else if (_keysDown.Remove(key))
            {
                _keysReleased.Add(key);
            }
        }
    }

    public void AddTextInput(int unicode)
    {
        if (unicode <= 0)
        {
            return;
        }

        lock (_sync)
        {
            _textInputCodepoints.Add(unicode);
        }
    }

    public void SetMouseButton(MouseButtons button, bool pressed)
    {
        lock (_sync)
        {
            if (button == MouseButtons.Left)
            {
                if (pressed && !_leftMouseDown)
                {
                    _leftMousePressed = true;
                }

                _leftMouseDown = pressed;
            }
            else if (button == MouseButtons.Right)
            {
                if (pressed && !_rightMouseDown)
                {
                    _rightMousePressed = true;
                }

                _rightMouseDown = pressed;
            }
        }
    }

    public void AddMouseWheel(int delta)
    {
        lock (_sync)
        {
            _mouseWheelDelta += delta;
        }
    }

    public InputState Snapshot()
    {
        lock (_sync)
        {
            InputState snapshot = new InputState();
            snapshot._keysDown.UnionWith(_keysDown);
            snapshot._keysPressed.UnionWith(_keysPressed);
            snapshot._keysReleased.UnionWith(_keysReleased);
            snapshot._textInputCodepoints.AddRange(_textInputCodepoints);
            snapshot._mouseScreen = _mouseScreen;
            snapshot._leftMouseDown = _leftMouseDown;
            snapshot._leftMousePressed = _leftMousePressed;
            snapshot._rightMouseDown = _rightMouseDown;
            snapshot._rightMousePressed = _rightMousePressed;
            snapshot._mouseWheelDelta = _mouseWheelDelta;
            return snapshot;
        }
    }

    public InputState SnapshotAndEndFrame()
    {
        lock (_sync)
        {
            InputState snapshot = new InputState();
            snapshot._keysDown.UnionWith(_keysDown);
            snapshot._keysPressed.UnionWith(_keysPressed);
            snapshot._keysReleased.UnionWith(_keysReleased);
            snapshot._textInputCodepoints.AddRange(_textInputCodepoints);
            snapshot._mouseScreen = _mouseScreen;
            snapshot._leftMouseDown = _leftMouseDown;
            snapshot._leftMousePressed = _leftMousePressed;
            snapshot._rightMouseDown = _rightMouseDown;
            snapshot._rightMousePressed = _rightMousePressed;
            snapshot._mouseWheelDelta = _mouseWheelDelta;

            _keysPressed.Clear();
            _keysReleased.Clear();
            _textInputCodepoints.Clear();
            _leftMousePressed = false;
            _rightMousePressed = false;
            _mouseWheelDelta = 0;

            return snapshot;
        }
    }

    public void EndFrame()
    {
        lock (_sync)
        {
            _keysPressed.Clear();
            _keysReleased.Clear();
            _textInputCodepoints.Clear();
            _leftMousePressed = false;
            _rightMousePressed = false;
            _mouseWheelDelta = 0;
        }
    }
}
