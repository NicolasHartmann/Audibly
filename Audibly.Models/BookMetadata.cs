namespace Audibly.Models;

/// <summary>The details of a book that "Look up online" can change, saved together.</summary>
public sealed record BookMetadata(
    string? Description,
    string? Narrator,
    string? Series,
    string? SeriesPart,
    string? Genres,
    DateTime? ReleaseDate,
    string? Asin,
    string? Title = null)
{
    public static BookMetadata From(Audiobook book) =>
        new(book.Description, book.Narrator, book.Series, book.SeriesPart, book.Genres, book.ReleaseDate, book.Asin, book.Title);
}
