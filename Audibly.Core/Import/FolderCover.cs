using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Audibly.Core.Import;

/// <summary>Finds the cover image that sits next to an audiobook's files.</summary>
public static class FolderCover
{
    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];

    private static readonly string[] CoverNames = ["cover", "folder", "front", "albumart"];

    /// <summary>
    ///     Picks the cover among a folder's file names: a well-known name (cover, folder, front,
    ///     albumart) wins, otherwise the only image in the folder. Returns null when it is unclear.
    /// </summary>
    public static string? Pick(IEnumerable<string> fileNames)
    {
        var images = fileNames.Where(IsImage).ToList();
        foreach (var name in CoverNames)
        {
            var match = images.FirstOrDefault(file =>
                string.Equals(Path.GetFileNameWithoutExtension(file), name, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        return images.Count == 1 ? images[0] : null;
    }

    /// <summary>Full path of the cover in a folder on disk, or null.</summary>
    public static string? Find(string? folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
        try
        {
            var name = Pick(Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>());
            return name == null ? null : Path.Combine(folder, name);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static bool IsImage(string fileName) =>
        ImageExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
}
