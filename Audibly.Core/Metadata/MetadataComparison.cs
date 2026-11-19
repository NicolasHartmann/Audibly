using System;
using System.Collections.Generic;
using System.Linq;
using Audibly.Models;

namespace Audibly.Core.Metadata;

/// <summary>The fields "Look up online" can change, in the order the dialog lists them.</summary>
public enum MetadataField
{
    Narrator,
    Series,
    SeriesPart,
    Genres,
    ReleaseDate,
    Description
}

/// <summary>One row of the dialog: the current and the found value as display text.</summary>
public sealed record FieldChange(MetadataField Field, string Old, string New, bool CanApply, bool ApplyByDefault);

/// <summary>
///     Sets current and found details side by side and applies the rows the user chose. An empty found
///     value can never be applied, so a lookup never clears what the book already has.
/// </summary>
public static class MetadataComparison
{
    public static IReadOnlyList<FieldChange> Compare(BookMetadata current, BookDetails found)
    {
        var all = Enum.GetValues<MetadataField>();
        var foundOnly = Apply(new BookMetadata(null, null, null, null, null, null, null), found, all.ToHashSet());
        return all.Select(field =>
        {
            var old = Display(current, field);
            var @new = Display(foundOnly, field);
            var canApply = !string.IsNullOrWhiteSpace(@new);
            return new FieldChange(field, old, @new, canApply, canApply && old.Trim() != @new.Trim());
        }).ToList();
    }

    public static BookMetadata Apply(BookMetadata current, BookDetails found, IReadOnlySet<MetadataField> chosen)
    {
        string? Pick(MetadataField field, string? old, string? @new) =>
            chosen.Contains(field) && !string.IsNullOrWhiteSpace(@new) ? @new : old;

        var genres = found.Genres.Count > 0 ? string.Join("\n", found.Genres) : null;
        return new BookMetadata(
            Pick(MetadataField.Description, current.Description, found.Description),
            Pick(MetadataField.Narrator, current.Narrator, found.Narrator),
            Pick(MetadataField.Series, current.Series, found.Series),
            Pick(MetadataField.SeriesPart, current.SeriesPart, found.SeriesPart),
            Pick(MetadataField.Genres, current.Genres, genres),
            chosen.Contains(MetadataField.ReleaseDate) && found.ReleaseDate != null ? found.ReleaseDate : current.ReleaseDate,
            string.IsNullOrWhiteSpace(found.Asin) ? current.Asin : found.Asin);
    }

    private static string Display(BookMetadata book, MetadataField field) => field switch
    {
        MetadataField.Narrator => book.Narrator ?? string.Empty,
        MetadataField.Series => book.Series ?? string.Empty,
        MetadataField.SeriesPart => book.SeriesPart ?? string.Empty,
        MetadataField.Genres => string.Join(", ",
            (book.Genres ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
        MetadataField.ReleaseDate => book.ReleaseDate?.ToShortDateString() ?? string.Empty,
        MetadataField.Description => book.Description ?? string.Empty,
        _ => string.Empty
    };
}
