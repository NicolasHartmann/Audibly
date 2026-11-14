using Audibly.App.Helpers;
using Xunit;

namespace Audibly.Audio.Tests;

public class FileChapterTitlesTests
{
    private static readonly string[] Files =
    [
        @"D:\Books\HP\Harry Potter (Full-Cast Edition) - 01.mp3",
        @"D:\Books\HP\Harry Potter (Full-Cast Edition) - 02.mp3",
        @"D:\Books\HP\Harry Potter (Full-Cast Edition) - 03.mp3"
    ];

    [Fact]
    public void FilesSharingOneTagTitleAreNamedAfterTheirFiles()
    {
        var titles = FileChapterTitles.Resolve(
            [(0, "Harry Potter (Full-Cast Edition)"), (1, "Harry Potter (Full-Cast Edition)"), (2, "Harry Potter (Full-Cast Edition)")],
            Files);

        Assert.Equal(
            ["Harry Potter (Full-Cast Edition) - 01", "Harry Potter (Full-Cast Edition) - 02", "Harry Potter (Full-Cast Edition) - 03"],
            titles);
    }

    [Fact]
    public void DistinctTagTitlesAreKept()
    {
        var titles = FileChapterTitles.Resolve([(0, "Vorspann"), (1, "Kapitel 1"), (2, "Kapitel 2")], Files);

        Assert.Equal(["Vorspann", "Kapitel 1", "Kapitel 2"], titles);
    }

    [Fact]
    public void MissingTagTitlesFallBackToFileNames()
    {
        var titles = FileChapterTitles.Resolve([(0, "Intro"), (1, ""), (2, null)], Files);

        Assert.Equal(
            ["Harry Potter (Full-Cast Edition) - 01", "Harry Potter (Full-Cast Edition) - 02", "Harry Potter (Full-Cast Edition) - 03"],
            titles);
    }

    [Fact]
    public void EmbeddedChaptersOfAFileAreNeverRenamed()
    {
        // File 0 carries two real chapter marks; files 1 and 2 only have the shared tag title.
        var titles = FileChapterTitles.Resolve(
            [(0, "Book"), (0, "Book"), (1, "Book"), (2, "Book")],
            Files);

        Assert.Equal(
            ["Book", "Book", "Harry Potter (Full-Cast Edition) - 02", "Harry Potter (Full-Cast Edition) - 03"],
            titles);
    }

    [Fact]
    public void SingleFileBookIsLeftAlone()
    {
        var titles = FileChapterTitles.Resolve([(0, "Book")], [Files[0]]);

        Assert.Equal(["Book"], titles);
    }

    [Fact]
    public void ChapterPointingAtAnUnknownFileIsLeftAlone()
    {
        var titles = FileChapterTitles.Resolve([(0, "Book"), (1, "Book"), (7, "Book")], Files);

        Assert.Equal(
            ["Harry Potter (Full-Cast Edition) - 01", "Harry Potter (Full-Cast Edition) - 02", "Book"],
            titles);
    }
}
