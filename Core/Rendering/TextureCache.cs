using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using GLPixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace SlayInspiredPrototype;

internal static class TextureCache
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, IntPtr> Cache = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static IntPtr GetOrLoad(string relativePath)
    {
        string? assetPath = AssetLocator.Find(relativePath);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return IntPtr.Zero;
        }

        lock (Sync)
        {
            if (Cache.TryGetValue(assetPath, out IntPtr existing))
            {
                return existing;
            }

            if (Failed.Contains(assetPath))
            {
                return IntPtr.Zero;
            }

            try
            {
                using Bitmap source = new Bitmap(assetPath);
                using Bitmap bitmap = new Bitmap(source.Width, source.Height, DrawingPixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.DrawImage(source, 0, 0, source.Width, source.Height);
                }

                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppArgb);
                try
                {
                    int texture = GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, texture);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bitmap.Width, bitmap.Height, 0, GLPixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
                    Cache[assetPath] = (IntPtr)texture;
                    return (IntPtr)texture;
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }
            catch
            {
                Failed.Add(assetPath);
                return IntPtr.Zero;
            }
        }
    }
}
