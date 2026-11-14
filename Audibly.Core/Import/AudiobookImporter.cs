using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ATL;
using Audibly.Models;
using ChapterInfo = Audibly.Models.ChapterInfo;

namespace Audibly.Core.Import;

/// <summary>
///     One audio file of a book. <paramref name="Location" /> is whatever the platform plays from
///     (a path on Windows, a content URI on Android); tags are only ever read through the stream.
/// </summary>
public sealed record AudioFileSource(string Location, string FileName, Func<Stream> Open);

/// <summary>An audiobook read from its files, not saved yet, with the cover found in the first file.</summary>
public sealed record ImportedBook(Audiobook Audiobook, byte[]? CoverImage);

/// <summary>Builds audiobooks from audio files without touching a database or the file system.</summary>
public static class AudiobookImporter
{
    private static readonly string[] AudioExtensions = [".m4b", ".mp3", ".m4a"];

    /// <summary>
    ///     Reads the files in the given order into one audiobook. Book details come from the first
    ///     file; every file contributes its chapters, or one chapter when it has none.
    /// </summary>
    public static ImportedBook Create(IReadOnlyList<AudioFileSource> files)
    {
        if (files.Count == 0) throw new ArgumentException("An audiobook needs at least one file.", nameof(files));

        var audiobook = new Audiobook
        {
            CurrentSourceFileIndex = 0,
            SourcePaths = [],
            PlaybackSpeed = 1.0,
            Volume = 1.0,
            IsCompleted = false
        };
        byte[]? cover = null;
        var chapterIndex = 0;

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            using var stream = file.Open();
            var track = new Track(stream, Path.GetExtension(file.FileName));
            var fallbackTitle = Path.GetFileNameWithoutExtension(file.FileName);
            var trackTitle = string.IsNullOrWhiteSpace(track.Title) ? fallbackTitle : track.Title;

            if (index == 0)
            {
                audiobook.Title = trackTitle;
                audiobook.Author = track.Artist ?? string.Empty;
                audiobook.Composer = track.Composer ?? string.Empty;
                audiobook.Description = Description(track);
                audiobook.ReleaseDate = track.Date;
                cover = track.EmbeddedPictures.FirstOrDefault()?.PictureData;
            }

            audiobook.SourcePaths.Add(new SourceFile
            {
                Index = index,
                FilePath = file.Location,
                Duration = track.Duration,
                CurrentTimeMs = 0
            });

            foreach (var chapter in track.Chapters)
                audiobook.Chapters.Add(new ChapterInfo
                {
                    StartTime = chapter.StartTime,
                    EndTime = chapter.EndTime,
                    StartOffset = chapter.StartOffset,
                    EndOffset = chapter.EndOffset,
                    UseOffset = chapter.UseOffset,
                    Title = chapter.Title,
                    UniqueID = chapter.UniqueID,
                    Subtitle = chapter.Subtitle,
                    Index = chapterIndex++,
                    ParentSourceFileIndex = index
                });

            if (track.Chapters.Count == 0)
                audiobook.Chapters.Add(new ChapterInfo
                {
                    StartTime = 0,
                    EndTime = Convert.ToUInt32(track.Duration * 1000),
                    Title = trackTitle,
                    UniqueID = string.Empty,
                    Subtitle = string.Empty,
                    Index = chapterIndex++,
                    ParentSourceFileIndex = index
                });
        }

        // files that share one title tag would all show the same chapter name
        var titles = FileChapterTitles.Resolve(
            audiobook.Chapters.Select(chapter => (chapter.ParentSourceFileIndex, (string?)chapter.Title)).ToList(),
            files.Select(file => file.FileName).ToList());
        for (var i = 0; i < audiobook.Chapters.Count; i++) audiobook.Chapters[i].Title = titles[i];

        audiobook.Duration = audiobook.SourcePaths.Sum(file => file.Duration);
        audiobook.CurrentChapterIndex = 0;
        return new ImportedBook(audiobook, cover);
    }

    /// <summary>
    ///     Splits the audio files of a folder tree into books: every folder below the root that
    ///     contains audio files is one book, in natural file name order ("2" before "10"); every
    ///     audio file directly in the root is a book of its own.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<T>> GroupIntoBooks<T>(
        IEnumerable<(string Folder, string FileName, T File)> entries, string rootFolder)
    {
        var audio = entries
            .Where(entry => AudioExtensions.Contains(Path.GetExtension(entry.FileName), StringComparer.OrdinalIgnoreCase))
            .ToList();
        var books = new List<IReadOnlyList<T>>();

        foreach (var entry in audio.Where(entry => SameFolder(entry.Folder, rootFolder))
                     .OrderBy(entry => entry.FileName, NaturalOrder.Instance))
            books.Add([entry.File]);

        foreach (var folder in audio.Where(entry => !SameFolder(entry.Folder, rootFolder))
                     .GroupBy(entry => entry.Folder, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, NaturalOrder.Instance))
            books.Add(folder.OrderBy(entry => entry.FileName, NaturalOrder.Instance)
                .Select(entry => entry.File).ToList());

        return books;
    }

    private static bool SameFolder(string a, string b) =>
        string.Equals(a.TrimEnd('/', '\\'), b.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase);

    private static string Description(Track track)
    {
        if (!string.IsNullOrEmpty(track.Description)) return track.Description;
        if (!string.IsNullOrEmpty(track.Comment)) return track.Comment;
        return track.AdditionalFields.TryGetValue("©des", out var value) ? value : string.Empty;
    }

    /// <summary>Compares names so that numbers inside them sort by value.</summary>
    private sealed class NaturalOrder : IComparer<string>
    {
        public static readonly NaturalOrder Instance = new();

        public int Compare(string? x, string? y)
        {
            x ??= string.Empty;
            y ??= string.Empty;
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    var startI = i;
                    var startJ = j;
                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;
                    var a = x[startI..i].TrimStart('0');
                    var b = y[startJ..j].TrimStart('0');
                    var byValue = a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
                    if (byValue != 0) return byValue;
                    continue;
                }

                var byChar = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (byChar != 0) return byChar;
                i++;
                j++;
            }

            return (x.Length - i).CompareTo(y.Length - j);
        }
    }
}
