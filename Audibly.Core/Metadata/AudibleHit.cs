using System;
using System.Collections.Generic;

namespace Audibly.Core.Metadata;

/// <summary>One book in Audible's search results. <see cref="Region" /> is "de" or "us".</summary>
public sealed record AudibleHit(
    string Asin,
    string Region,
    string Title,
    string Author,
    string Narrators,
    string? Series,
    string? SeriesPart,
    int? RuntimeMinutes,
    DateTime? ReleaseDate,
    string? Language,
    string? ImageUrl);

/// <summary>What Audnexus knows about one book. Genres come first, then the finer tags.</summary>
public sealed record BookDetails(
    string Asin,
    string? Description,
    string? Narrator,
    string? Series,
    string? SeriesPart,
    IReadOnlyList<string> Genres,
    DateTime? ReleaseDate);
