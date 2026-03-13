using System;
using System.IO;

namespace SlayInspiredPrototype;

internal static class AssetLocator
{
    public static string? Find(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        string normalized = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        string[] roots =
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetDirectoryName(typeof(AssetLocator).Assembly.Location) ?? string.Empty
        };

        foreach (string root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string? current = Path.GetFullPath(root);
            for (int depth = 0; depth < 7 && !string.IsNullOrWhiteSpace(current); depth++)
            {
                string candidate = Path.Combine(current, normalized);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                DirectoryInfo? parent = Directory.GetParent(current);
                current = parent?.FullName;
            }
        }

        return null;
    }
}
