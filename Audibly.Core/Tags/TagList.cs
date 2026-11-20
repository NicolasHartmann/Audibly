using System;
using System.Collections.Generic;
using System.Linq;

namespace Audibly.Core.Tags;

/// <summary>
///     A book's own tags, stored as one tag per line. Tags compare without regard to case; the spelling
///     already present wins, so "Fantasy" and "fantasy" never become two tags.
/// </summary>
public static class TagList
{
    private static readonly StringComparer Same = StringComparer.CurrentCultureIgnoreCase;

    public static IReadOnlyList<string> Parse(string? stored) =>
        Clean((stored ?? string.Empty).Split('\n'));

    public static string? Format(IEnumerable<string> tags)
    {
        var list = Clean(tags);
        return list.Count == 0 ? null : string.Join("\n", list);
    }

    /// <param name="known">Tags elsewhere in the library, whose spelling a new tag takes.</param>
    public static IReadOnlyList<string> Add(IReadOnlyList<string> current, IEnumerable<string> add,
        IEnumerable<string> known)
    {
        var spelling = new Dictionary<string, string>(Same);
        foreach (var tag in Clean(known)) spelling.TryAdd(tag, tag);
        return Clean(current.Concat(add.Select(tag => spelling.TryGetValue(tag.Trim(), out var s) ? s : tag)));
    }

    public static IReadOnlyList<string> Remove(IReadOnlyList<string> current, IEnumerable<string> remove)
    {
        var gone = new HashSet<string>(remove.Select(tag => tag.Trim()), Same);
        return Clean(current.Where(tag => !gone.Contains(tag)));
    }

    public static IReadOnlyList<string> AllTags(IEnumerable<string?> storedPerBook) =>
        Clean(storedPerBook.SelectMany(Parse)).OrderBy(tag => tag, Same).ToList();

    /// <summary>True when nothing is ticked or the book has at least one of the ticked tags.</summary>
    public static bool Matches(IReadOnlyList<string> bookTags, IReadOnlyCollection<string> ticked) =>
        ticked.Count == 0 || bookTags.Any(tag => ticked.Contains(tag, Same));

    private static List<string> Clean(IEnumerable<string> tags)
    {
        var seen = new HashSet<string>(Same);
        return tags.Select(tag => tag.Trim()).Where(tag => tag.Length > 0 && seen.Add(tag)).ToList();
    }
}
