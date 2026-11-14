using ATL;
using Audibly.Core.Import;
using Xunit;

namespace Audibly.Audio.Tests;

public sealed class AudiobookImporterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "audibly-import-" + Guid.NewGuid().ToString("N"));

    public AudiobookImporterTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
            // a leftover temp folder is not worth failing a test for
        }
    }

    /// <summary>Writes a silent WAV file and, when given, its tags.</summary>
    private AudioFileSource Wav(string name, int seconds, string? title = null, string? artist = null)
    {
        const int sampleRate = 8000;
        var path = Path.Combine(_folder, name);
        var dataBytes = sampleRate * 2 * seconds;
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataBytes);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataBytes);
            writer.Write(new byte[dataBytes]);
        }

        if (title != null || artist != null)
        {
            var track = new Track(path);
            if (title != null) track.Title = title;
            if (artist != null) track.Artist = artist;
            Assert.True(track.Save());
        }

        // a location that is not a path, as on Android, proves the importer only uses the stream
        return new AudioFileSource("content://books/" + name, name, () => File.OpenRead(path));
    }

    [Fact]
    public void ASingleFileBecomesABookWithOneChapter()
    {
        var imported = AudiobookImporter.Create([Wav("book.wav", 3, "The Title", "The Author")]);

        var book = imported.Audiobook;
        Assert.Equal("The Title", book.Title);
        Assert.Equal("The Author", book.Author);
        Assert.Equal(3, book.Duration);
        Assert.Equal(1.0, book.PlaybackSpeed);
        Assert.Equal(1.0, book.Volume);
        Assert.Equal(0, book.CurrentSourceFileIndex);
        Assert.Equal(0, book.CurrentChapterIndex);
        var file = Assert.Single(book.SourcePaths);
        Assert.Equal("content://books/book.wav", file.FilePath);
        Assert.Equal(3, file.Duration);
        var chapter = Assert.Single(book.Chapters);
        Assert.Equal(0u, chapter.StartTime);
        Assert.Equal(3000u, chapter.EndTime);
        Assert.Equal("The Title", chapter.Title);
    }

    [Fact]
    public void FilesThatShareATitleGetTheirFileNamesAsChapterTitles()
    {
        var imported = AudiobookImporter.Create(
        [
            Wav("Story - 01.wav", 2, "Story", "Author"),
            Wav("Story - 02.wav", 3, "Story", "Author"),
            Wav("Story - 03.wav", 2, "Story", "Author")
        ]);

        var book = imported.Audiobook;
        Assert.Equal("Story", book.Title);
        Assert.Equal(7, book.Duration);
        Assert.Equal([0, 1, 2], book.SourcePaths.Select(file => file.Index));
        Assert.Equal(["Story - 01", "Story - 02", "Story - 03"], book.Chapters.Select(chapter => chapter.Title));
        Assert.Equal([0, 1, 2], book.Chapters.Select(chapter => chapter.Index));
        Assert.Equal([0, 1, 2], book.Chapters.Select(chapter => chapter.ParentSourceFileIndex));
        Assert.Equal(3000u, book.Chapters[1].EndTime);
    }

    [Fact]
    public void AFileWithoutTagsUsesItsFileNameAsTitle()
    {
        var book = AudiobookImporter.Create([Wav("Untagged Book.wav", 2)]).Audiobook;

        Assert.Equal("Untagged Book", book.Title);
        Assert.Equal("Untagged Book", Assert.Single(book.Chapters).Title);
        Assert.Null(AudiobookImporter.Create([Wav("other.wav", 2)]).CoverImage);
    }

    [Fact]
    public void CreateNeedsAtLeastOneFile() =>
        Assert.Throws<ArgumentException>(() => AudiobookImporter.Create([]));

    [Fact]
    public void ASubfolderIsOneBookAndARootFileIsItsOwnBook()
    {
        var books = AudiobookImporter.GroupIntoBooks(
        [
            ("root", "Single.m4b", "single"),
            ("root/Series", "Part 2.mp3", "p2"),
            ("root/Series", "Part 10.mp3", "p10"),
            ("root/Series", "Part 1.mp3", "p1"),
            ("root", "Another.mp3", "another"),
            ("root/Series", "cover.jpg", "cover"),
            ("root", "notes.txt", "notes")
        ], "root");

        Assert.Equal(3, books.Count);
        Assert.Contains(books, book => book.SequenceEqual(["single"]));
        Assert.Contains(books, book => book.SequenceEqual(["another"]));
        // natural order: 2 before 10
        Assert.Contains(books, book => book.SequenceEqual(["p1", "p2", "p10"]));
    }

    [Fact]
    public void GroupingIgnoresCaseOfExtensionsAndEmptyFolders()
    {
        var books = AudiobookImporter.GroupIntoBooks(
            [("root/A", "One.MP3", "one"), ("root/B", "readme.txt", "readme"), ("root/C/Deep", "x.M4A", "deep")], "root");

        Assert.Equal(2, books.Count);
        Assert.Contains(books, book => book.SequenceEqual(["one"]));
        Assert.Contains(books, book => book.SequenceEqual(["deep"]));
    }
}
