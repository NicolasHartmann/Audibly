using System;
using System.Collections.Generic;
using System.Linq;
using Audibly.Core.Tags;
using Audibly.Models;

namespace Audibly.Core.Metadata;

/// <summary>The fields "Look up online" can change, in the order the dialog lists them.</summary>
public enum MetadataField
{
    Title,

    /// <summary>The cover image; its found value is the image's address, downloaded by the app on Apply.</summary>
    Cover,
    Narrator,
    Series,
    SeriesPart,

    /// <summary>The book's own tags; found genres are added to them, never replace them.</summary>
    Tags,
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
    /// <param name="hasOwnCover">
    ///     False while the book shows the default cover: then a found cover is ticked by default. An own
    ///     cover is only replaced when ticked by hand.
    /// </param>
    public static IReadOnlyList<FieldChange> Compare(BookMetadata current, BookDetails found, bool hasOwnCover = true)
    {
        var all = Enum.GetValues<MetadataField>();
        var foundOnly = Apply(new BookMetadata(null, null, null, null, null, null, null), found, all.ToHashSet());
        return all.Select(field =>
        {
            if (field == MetadataField.Cover)
            {
                var image = found.ImageUrl ?? string.Empty;
                var hasImage = !string.IsNullOrWhiteSpace(image);
                return new FieldChange(field, string.Empty, image, hasImage, hasImage && !hasOwnCover);
            }

            if (field == MetadataField.Tags)
            {
                var own = TagList.Parse(current.Tags);
                var merged = TagList.Add(own, found.Genres, []);
                return new FieldChange(field, string.Join(", ", own), string.Join(", ", merged),
                    found.Genres.Count > 0, merged.Count > own.Count);
            }

            var old = Display(current, field);
            var @new = Display(foundOnly, field);
            var canApply = !string.IsNullOrWhiteSpace(@new);
            // a new title is only taken when ticked by hand: Audible often adds "Das Hörspiel" or similar
            var different = old.Trim() != @new.Trim();
            return new FieldChange(field, old, @new, canApply, canApply && different && field != MetadataField.Title);
        }).ToList();
    }

    public static BookMetadata Apply(BookMetadata current, BookDetails found, IReadOnlySet<MetadataField> chosen)
    {
        string? Pick(MetadataField field, string? old, string? @new) =>
            chosen.Contains(field) && !string.IsNullOrWhiteSpace(@new) ? @new : old;

        var takeTags = chosen.Contains(MetadataField.Tags) && found.Genres.Count > 0;
        return new BookMetadata(
            Pick(MetadataField.Description, current.Description, found.Description),
            Pick(MetadataField.Narrator, current.Narrator, found.Narrator),
            Pick(MetadataField.Series, current.Series, found.Series),
            Pick(MetadataField.SeriesPart, current.SeriesPart, found.SeriesPart),
            takeTags ? string.Join("\n", found.Genres) : current.Genres,
            chosen.Contains(MetadataField.ReleaseDate) && found.ReleaseDate != null ? found.ReleaseDate : current.ReleaseDate,
            string.IsNullOrWhiteSpace(found.Asin) ? current.Asin : found.Asin,
            Pick(MetadataField.Title, current.Title, found.Title),
            takeTags ? TagList.Format(TagList.Add(TagList.Parse(current.Tags), found.Genres, [])) : current.Tags);
    }

    private static string Display(BookMetadata book, MetadataField field) => field switch
    {
        MetadataField.Title => book.Title ?? string.Empty,
        MetadataField.Cover => string.Empty,
        MetadataField.Narrator => book.Narrator ?? string.Empty,
        MetadataField.Series => book.Series ?? string.Empty,
        MetadataField.SeriesPart => book.SeriesPart ?? string.Empty,
        MetadataField.ReleaseDate => book.ReleaseDate?.ToShortDateString() ?? string.Empty,
        MetadataField.Description => book.Description ?? string.Empty,
        _ => string.Empty
    };
}
