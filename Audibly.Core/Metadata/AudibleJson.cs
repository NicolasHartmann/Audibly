using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Audibly.Core.Metadata;

/// <summary>
///     Reads the answers of Audible's catalog search and of Audnexus. Both are unofficial, so every
///     field is optional: a missing one comes back empty instead of failing the whole answer.
/// </summary>
public static partial class AudibleJson
{
    public static IReadOnlyList<AudibleHit> ParseSearch(string json, string region)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("products", out var products) ||
            products.ValueKind != JsonValueKind.Array)
            return [];

        var hits = new List<AudibleHit>();
        foreach (var product in products.EnumerateArray())
        {
            var asin = String(product, "asin");
            // entries without a running time are not audiobooks
            if (asin == null || !product.TryGetProperty("runtime_length_min", out var runtime) ||
                runtime.ValueKind != JsonValueKind.Number)
                continue;

            var series = First(product, "series");
            string? imageUrl = null;
            if (product.TryGetProperty("product_images", out var images) && images.ValueKind == JsonValueKind.Object)
                imageUrl = String(images, "500") ?? images.EnumerateObject().Select(p => p.Value.GetString()).FirstOrDefault();

            hits.Add(new AudibleHit(
                asin,
                region,
                String(product, "title") ?? string.Empty,
                Names(product, "authors") ?? string.Empty,
                Names(product, "narrators") ?? string.Empty,
                series is { } s ? String(s, "title") : null,
                series is { } p ? String(p, "sequence") : null,
                runtime.GetInt32(),
                Date(String(product, "release_date")),
                String(product, "language"),
                imageUrl));
        }

        return hits;
    }

    public static BookDetails ParseDetails(string json)
    {
        using var document = JsonDocument.Parse(json);
        var book = document.RootElement;

        var summary = String(book, "summary");
        var description = !string.IsNullOrWhiteSpace(summary) ? HtmlToText(summary) : String(book, "description");

        JsonElement? series = book.TryGetProperty("seriesPrimary", out var primary) &&
                              primary.ValueKind == JsonValueKind.Object
            ? primary
            : null;

        var genres = new List<string>();
        if (book.TryGetProperty("genres", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var type in new[] { "genre", "tag" })
                genres.AddRange(list.EnumerateArray()
                    .Where(genre => String(genre, "type") == type)
                    .Select(genre => String(genre, "name"))
                    .OfType<string>());

        return new BookDetails(
            String(book, "asin") ?? string.Empty,
            string.IsNullOrWhiteSpace(description) ? null : description,
            Names(book, "narrators"),
            series is { } a ? String(a, "name") : null,
            series is { } b ? String(b, "position") : null,
            genres.Distinct().ToList(),
            Date(String(book, "releaseDate")));
    }

    /// <summary>Turns Audible's description HTML into plain text with blank lines between paragraphs.</summary>
    public static string HtmlToText(string html)
    {
        var text = BreakTag().Replace(html, "\n");
        text = ParagraphEnd().Replace(text, "\n\n");
        text = BlockEnd().Replace(text, "\n\n");
        text = AnyTag().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text).Replace("\r", string.Empty);
        text = SpacesAroundBreak().Replace(text, "\n");
        text = ManyBreaks().Replace(text, "\n\n");
        return text.Trim();
    }

    /// <summary>German hits first; a book both shops list is shown once, as the German one.</summary>
    public static IReadOnlyList<AudibleHit> Merge(IReadOnlyList<AudibleHit> de, IReadOnlyList<AudibleHit> us)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return de.Concat(us).Where(hit => seen.Add(hit.Asin)).ToList();
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement? First(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array &&
        value.GetArrayLength() > 0
            ? value[0]
            : null;

    private static string? Names(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return null;
        var names = value.EnumerateArray().Select(person => String(person, "name"))
            .Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    // only the day counts: "2010-11-04T00:00:00.000Z" must not become 3 or 5 November in another time zone
    private static DateTime? Date(string? value) =>
        value is { Length: >= 10 } && DateTime.TryParseExact(value[..10], "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)
            ? date
            : null;

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTag();

    [GeneratedRegex(@"</p\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphEnd();

    [GeneratedRegex(@"</(b|i|strong|em|div|li|ul|h\d)\s*>(?=\s*<|$)", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[ \t]*\n[ \t]*")]
    private static partial Regex SpacesAroundBreak();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyBreaks();
}
