using System;
using System.IO;
using System.Windows.Forms;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Matrix4 = OpenTK.Mathematics.Matrix4;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace SlayInspiredPrototype;

public sealed unsafe class ImGuiController : IDisposable
{
    private const string VertexSource = "#version 330 core\n" +
                                        "layout (location = 0) in vec2 Position;\n" +
                                        "layout (location = 1) in vec2 UV;\n" +
                                        "layout (location = 2) in vec4 Color;\n" +
                                        "uniform mat4 projection_matrix;\n" +
                                        "out vec2 Frag_UV;\n" +
                                        "out vec4 Frag_Color;\n" +
                                        "void main()\n" +
                                        "{\n" +
                                        "    Frag_UV = UV;\n" +
                                        "    Frag_Color = Color;\n" +
                                        "    gl_Position = projection_matrix * vec4(Position.xy, 0, 1);\n" +
                                        "}";

    private const string FragmentSource = "#version 330 core\n" +
                                          "in vec2 Frag_UV;\n" +
                                          "in vec4 Frag_Color;\n" +
                                          "uniform sampler2D in_fontTexture;\n" +
                                          "layout (location = 0) out vec4 OutputColor;\n" +
                                          "void main()\n" +
                                          "{\n" +
                                          "    OutputColor = Frag_Color * texture(in_fontTexture, Frag_UV.st);\n" +
                                          "}";

    private readonly int _shader;
    private readonly int _vertexShader;
    private readonly int _fragmentShader;
    private readonly int _projectionLocation;
    private readonly int _textureLocation;
    private readonly int _vertexArray;
    private readonly int _vertexBuffer;
    private readonly int _indexBuffer;
    private readonly int _fontTexture;
    private int _vertexBufferSize;
    private int _indexBufferSize;
    private bool _frameBegun;

    public ImGuiController(int width, int height)
    {
        _vertexBufferSize = 10000;
        _indexBufferSize = 2000;

        ImGui.CreateContext();
        ImGui.StyleColorsDark();
        ApplyStyle();
        SetupKeyMap();

        ImGuiIOPtr io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.DisplaySize = new NumVector2(width, height);
        io.DisplayFramebufferScale = NumVector2.One;
        io.DeltaTime = 1f / 60f;
        BuildFonts(io, 20f);

        _vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(_vertexShader, VertexSource);
        GL.CompileShader(_vertexShader);
        CheckShader(_vertexShader, "vertex");

        _fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(_fragmentShader, FragmentSource);
        GL.CompileShader(_fragmentShader);
        CheckShader(_fragmentShader, "fragment");

        _shader = GL.CreateProgram();
        GL.AttachShader(_shader, _vertexShader);
        GL.AttachShader(_shader, _fragmentShader);
        GL.LinkProgram(_shader);
        CheckProgram(_shader);

        _projectionLocation = GL.GetUniformLocation(_shader, "projection_matrix");
        _textureLocation = GL.GetUniformLocation(_shader, "in_fontTexture");

        _vertexArray = GL.GenVertexArray();
        _vertexBuffer = GL.GenBuffer();
        _indexBuffer = GL.GenBuffer();

        GL.BindVertexArray(_vertexArray);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

        const int stride = 20;
        GL.EnableVertexAttribArray(0);
        GL.EnableVertexAttribArray(1);
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (IntPtr)0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (IntPtr)8);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, stride, (IntPtr)16);

        _fontTexture = CreateFontTexture();

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }

    private void BuildFonts(ImGuiIOPtr io, float fontSize)
    {
        io.Fonts.Clear();

        string windowsFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        string[] candidates =
        {
            Path.Combine(windowsFonts, "segoeui.ttf"),
            Path.Combine(windowsFonts, "tahoma.ttf"),
            Path.Combine(windowsFonts, "arial.ttf"),
            Path.Combine(windowsFonts, "verdana.ttf")
        };

        string? fontPath = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
            {
                fontPath = candidates[i];
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(fontPath))
        {
            io.Fonts.AddFontFromFileTTF(fontPath, fontSize, null, io.Fonts.GetGlyphRangesCyrillic());
        }
        else
        {
            io.Fonts.AddFontDefault();
        }

        io.FontGlobalScale = 1f;
    }

    private static void SetupKeyMap()
    {
    }

    public void Update(int width, int height, float deltaSeconds, InputState input)
    {
        if (_frameBegun)
        {
            ImGui.Render();
        }

        ImGuiIOPtr io = ImGui.GetIO();
        io.DisplaySize = new NumVector2(width, height);
        io.DisplayFramebufferScale = NumVector2.One;
        io.DeltaTime = MathF.Max(1f / 240f, deltaSeconds);
        ApplyInput(io, input);

        ImGui.NewFrame();
        _frameBegun = true;
    }

    private static void ApplyInput(ImGuiIOPtr io, InputState input)
    {
        io.AddMousePosEvent(input.MouseScreen.X, input.MouseScreen.Y);
        io.AddMouseButtonEvent(0, input.LeftMouseDown);
        io.AddMouseButtonEvent(1, input.RightMouseDown);
        io.AddMouseWheelEvent(0f, input.MouseWheelDelta / 120f);

        SetKey(io, ImGuiKey.Tab, input.IsDown(Keys.Tab));
        SetKey(io, ImGuiKey.LeftArrow, input.IsDown(Keys.Left));
        SetKey(io, ImGuiKey.RightArrow, input.IsDown(Keys.Right));
        SetKey(io, ImGuiKey.UpArrow, input.IsDown(Keys.Up));
        SetKey(io, ImGuiKey.DownArrow, input.IsDown(Keys.Down));
        SetKey(io, ImGuiKey.PageUp, input.IsDown(Keys.PageUp));
        SetKey(io, ImGuiKey.PageDown, input.IsDown(Keys.PageDown));
        SetKey(io, ImGuiKey.Home, input.IsDown(Keys.Home));
        SetKey(io, ImGuiKey.End, input.IsDown(Keys.End));
        SetKey(io, ImGuiKey.Insert, input.IsDown(Keys.Insert));
        SetKey(io, ImGuiKey.Delete, input.IsDown(Keys.Delete));
        SetKey(io, ImGuiKey.Backspace, input.IsDown(Keys.Back));
        SetKey(io, ImGuiKey.Space, input.IsDown(Keys.Space));
        SetKey(io, ImGuiKey.Enter, input.IsDown(Keys.Enter));
        SetKey(io, ImGuiKey.Escape, input.IsDown(Keys.Escape));
        SetKey(io, ImGuiKey.A, input.IsDown(Keys.A));
        SetKey(io, ImGuiKey.C, input.IsDown(Keys.C));
        SetKey(io, ImGuiKey.D, input.IsDown(Keys.D));
        SetKey(io, ImGuiKey.E, input.IsDown(Keys.E));
        SetKey(io, ImGuiKey.F, input.IsDown(Keys.F));
        SetKey(io, ImGuiKey.G, input.IsDown(Keys.G));
        SetKey(io, ImGuiKey.Q, input.IsDown(Keys.Q));
        SetKey(io, ImGuiKey.R, input.IsDown(Keys.R));
        SetKey(io, ImGuiKey.S, input.IsDown(Keys.S));
        SetKey(io, ImGuiKey.T, input.IsDown(Keys.T));
        SetKey(io, ImGuiKey.V, input.IsDown(Keys.V));
        SetKey(io, ImGuiKey.W, input.IsDown(Keys.W));
        SetKey(io, ImGuiKey.X, input.IsDown(Keys.X));
        SetKey(io, ImGuiKey.Y, input.IsDown(Keys.Y));
        SetKey(io, ImGuiKey.Z, input.IsDown(Keys.Z));
        SetKey(io, ImGuiKey._1, input.IsDown(Keys.D1));
        SetKey(io, ImGuiKey._2, input.IsDown(Keys.D2));
        SetKey(io, ImGuiKey._3, input.IsDown(Keys.D3));
        SetKey(io, ImGuiKey._4, input.IsDown(Keys.D4));

        bool ctrl = input.IsDown(Keys.ControlKey) || input.IsDown(Keys.LControlKey) || input.IsDown(Keys.RControlKey);
        bool shift = input.IsDown(Keys.ShiftKey) || input.IsDown(Keys.LShiftKey) || input.IsDown(Keys.RShiftKey);
        bool alt = input.IsDown(Keys.Menu) || input.IsDown(Keys.LMenu) || input.IsDown(Keys.RMenu);
        bool super = input.IsDown(Keys.LWin) || input.IsDown(Keys.RWin);
        SetKey(io, ImGuiKey.ModCtrl, ctrl);
        SetKey(io, ImGuiKey.ModShift, shift);
        SetKey(io, ImGuiKey.ModAlt, alt);
        SetKey(io, ImGuiKey.ModSuper, super);

        foreach (int codepoint in input.TextInputCodepoints)
        {
            io.AddInputCharacter((uint)codepoint);
        }
    }

    private static void SetKey(ImGuiIOPtr io, ImGuiKey key, bool down)
    {
        io.AddKeyEvent(key, down);
    }

    public void Render()
    {
        if (!_frameBegun)
        {
            return;
        }

        _frameBegun = false;
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }

    private void ApplyStyle()
    {
        ImGuiStylePtr style = ImGui.GetStyle();
        style.WindowRounding = 7f;
        style.FrameRounding = 5f;
        style.PopupRounding = 5f;
        style.ScrollbarRounding = 7f;
        style.GrabRounding = 4f;
        style.FrameBorderSize = 1f;
        style.WindowBorderSize = 1f;
        style.ItemSpacing = new NumVector2(8f, 6f);
        style.WindowPadding = new NumVector2(12f, 10f);
        style.FramePadding = new NumVector2(8f, 5f);

        var colors = style.Colors;
        colors[(int)ImGuiCol.WindowBg] = new NumVector4(0.08f, 0.09f, 0.11f, 0.94f);
        colors[(int)ImGuiCol.Border] = new NumVector4(0.33f, 0.37f, 0.43f, 0.45f);
        colors[(int)ImGuiCol.FrameBg] = new NumVector4(0.15f, 0.17f, 0.20f, 0.95f);
        colors[(int)ImGuiCol.FrameBgHovered] = new NumVector4(0.20f, 0.23f, 0.28f, 0.98f);
        colors[(int)ImGuiCol.FrameBgActive] = new NumVector4(0.23f, 0.27f, 0.33f, 1.00f);
        colors[(int)ImGuiCol.Button] = new NumVector4(0.18f, 0.21f, 0.26f, 0.97f);
        colors[(int)ImGuiCol.ButtonHovered] = new NumVector4(0.24f, 0.29f, 0.36f, 1.00f);
        colors[(int)ImGuiCol.ButtonActive] = new NumVector4(0.28f, 0.34f, 0.42f, 1.00f);
        colors[(int)ImGuiCol.TitleBg] = new NumVector4(0.09f, 0.10f, 0.12f, 1.00f);
        colors[(int)ImGuiCol.TitleBgActive] = new NumVector4(0.11f, 0.13f, 0.16f, 1.00f);
        colors[(int)ImGuiCol.Header] = new NumVector4(0.18f, 0.22f, 0.27f, 0.90f);
        colors[(int)ImGuiCol.HeaderHovered] = new NumVector4(0.23f, 0.28f, 0.35f, 0.98f);
        colors[(int)ImGuiCol.HeaderActive] = new NumVector4(0.28f, 0.33f, 0.41f, 1.00f);
        colors[(int)ImGuiCol.PlotHistogram] = new NumVector4(0.50f, 0.66f, 0.98f, 1.00f);
        colors[(int)ImGuiCol.PlotHistogramHovered] = new NumVector4(0.68f, 0.78f, 1.00f, 1.00f);
    }

    private int CreateFontTexture()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out _);

        int texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texture);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)pixels);

        io.Fonts.SetTexID((IntPtr)texture);
        io.Fonts.ClearTexData();
        return texture;
    }

    private void RenderDrawData(ImDrawDataPtr drawData)
    {
        int fbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        int fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (fbWidth <= 0 || fbHeight <= 0)
        {
            return;
        }

        drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

        GL.Enable(EnableCap.Blend);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.ScissorTest);
        GL.Viewport(0, 0, fbWidth, fbHeight);

        Matrix4 projection = Matrix4.CreateOrthographicOffCenter(0f, drawData.DisplaySize.X, drawData.DisplaySize.Y, 0f, -1f, 1f);

        GL.UseProgram(_shader);
        GL.Uniform1(_textureLocation, 0);
        GL.UniformMatrix4(_projectionLocation, false, ref projection);
        GL.BindVertexArray(_vertexArray);

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];
            int vertexSize = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
            if (vertexSize > _vertexBufferSize)
            {
                while (vertexSize > _vertexBufferSize)
                {
                    _vertexBufferSize *= 2;
                }

                GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
                GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            }

            int indexSize = cmdList.IdxBuffer.Size * sizeof(ushort);
            if (indexSize > _indexBufferSize)
            {
                while (indexSize > _indexBufferSize)
                {
                    _indexBufferSize *= 2;
                }

                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
                GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertexSize, (IntPtr)cmdList.VtxBuffer.Data);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
            GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, indexSize, (IntPtr)cmdList.IdxBuffer.Data);

            int indexOffset = 0;
            for (int cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
            {
                ImDrawCmdPtr pcmd = cmdList.CmdBuffer[cmdi];
                if (pcmd.UserCallback != IntPtr.Zero)
                {
                    indexOffset += (int)pcmd.ElemCount;
                    continue;
                }

                GL.BindTexture(TextureTarget.Texture2D, (int)pcmd.TextureId);
                var clip = pcmd.ClipRect;
                GL.Scissor((int)clip.X, (int)(fbHeight - clip.W), (int)(clip.Z - clip.X), (int)(clip.W - clip.Y));
                GL.DrawElementsBaseVertex(
                    PrimitiveType.Triangles,
                    (int)pcmd.ElemCount,
                    DrawElementsType.UnsignedShort,
                    (IntPtr)(indexOffset * sizeof(ushort)),
                    (int)pcmd.VtxOffset);

                indexOffset += (int)pcmd.ElemCount;
            }
        }

        GL.Disable(EnableCap.ScissorTest);
        GL.BindVertexArray(0);
        GL.UseProgram(0);
    }

    private static void CheckShader(int shader, string name)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
        {
            throw new InvalidOperationException($"ImGui {name} shader compile failed: {GL.GetShaderInfoLog(shader)}");
        }
    }

    private static void CheckProgram(int program)
    {
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
        if (status == 0)
        {
            throw new InvalidOperationException($"ImGui program link failed: {GL.GetProgramInfoLog(program)}");
        }
    }

    public void Dispose()
    {
        if (_frameBegun)
        {
            ImGui.Render();
            _frameBegun = false;
        }

        GL.DeleteTexture(_fontTexture);
        GL.DeleteBuffer(_vertexBuffer);
        GL.DeleteBuffer(_indexBuffer);
        GL.DeleteVertexArray(_vertexArray);
        GL.DetachShader(_shader, _vertexShader);
        GL.DetachShader(_shader, _fragmentShader);
        GL.DeleteShader(_vertexShader);
        GL.DeleteShader(_fragmentShader);
        GL.DeleteProgram(_shader);
        ImGui.DestroyContext();
    }
}
