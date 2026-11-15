using System.Text;

namespace Audibly.Sync;

/// <summary>
///     Decides whether two devices mean the same audiobook. Every device gives a book its own
///     random id, so the book has to be recognised by what it is.
/// </summary>
public static class BookMatch
{
    /// <summary>How far the total lengths may differ when the titles do not agree.</summary>
    public const double DurationToleranceSeconds = 2;

    /// <summary>Lower case, letters and digits only, single spaces.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var result = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text.Trim())
            if (char.IsLetterOrDigit(c))
            {
                if (pendingSpace && result.Length > 0) result.Append(' ');
                pendingSpace = false;
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                pendingSpace = true;
            }

        return result.ToString();
    }

    /// <summary>
    ///     The same title and author is the same book. With different titles it is still the same
    ///     book when the author agrees and the total length is practically identical: one device
    ///     may have read the title from the album and the other from the first chapter.
    /// </summary>
    public static bool Same(string? titleA, string? authorA, double durationA, string? titleB, string? authorB,
        double durationB)
    {
        if (Normalize(authorA) != Normalize(authorB)) return false;

        var a = Normalize(titleA);
        var b = Normalize(titleB);
        if (a.Length > 0 && a == b) return true;

        return a.Length > 0 && b.Length > 0 && durationA > 0 && durationB > 0 &&
               Math.Abs(durationA - durationB) <= DurationToleranceSeconds;
    }
}
