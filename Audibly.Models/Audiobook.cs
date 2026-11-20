// Author: rstewa · https://github.com/rstewa
// Created: 04/15/2024
// Updated: 10/11/2024

namespace Audibly.Models;

/// <summary>
///     Represents an audiobook.
/// </summary>
public class Audiobook : DbObject, IEquatable<Audiobook>
{
    public string Author { get; set; }
    public string Composer { get; set; }
    public int CurrentSourceFileIndex { get; set; }
    public DateTime? DateLastPlayed { get; set; }
    public string Description { get; set; }
    public long Duration { get; set; } // *

    // public int CurrentTimeMs { get; set; } // *
    public string CoverImagePath { get; set; }

    public string ThumbnailPath { get; set; }

    public List<SourceFile> SourcePaths { get; set; }
    public bool IsNowPlaying { get; set; }
    public double PlaybackSpeed { get; set; }
    public double Progress { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string Title { get; set; }
    public double Volume { get; set; }
    public int? CurrentChapterIndex { get; set; }
    public bool IsCompleted { get; set; }

    public List<ChapterInfo> Chapters { get; set; } = [];

    // filled in from the internet ("Look up online"); Composer stays as the file tags say, because
    // it is part of the duplicate check on import
    public string? Narrator { get; set; }
    public string? Series { get; set; }

    /// <summary>Text, because Audible also numbers parts like "1-3" or "2.5".</summary>
    public string? SeriesPart { get; set; }

    /// <summary>One genre per line; broad genres first, then finer ones.</summary>
    public string? Genres { get; set; }

    /// <summary>The user's own tags, one per line (see Audibly.Core.Tags.TagList).</summary>
    public string? Tags { get; set; }

    /// <summary>Audible's id of the book the details came from.</summary>
    public string? Asin { get; set; }

    public bool Equals(Audiobook? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Author, other.Author, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Title, other.Title, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((Audiobook)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Author, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(Title, StringComparer.OrdinalIgnoreCase);
        return hashCode.ToHashCode();
    }
}