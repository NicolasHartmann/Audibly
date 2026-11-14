using Audibly.Core;
using Audibly.Models;
using Xunit;

namespace Audibly.Audio.Tests;

public class PlaybackPositionTests
{
    private static ChapterInfo Chapter(int index, int source, uint startMs, uint endMs) => new()
    {
        Index = index, ParentSourceFileIndex = source, StartTime = startMs, EndTime = endMs, Title = $"Chapter {index}"
    };

    // file 0: two chapters of 60 s, file 1: one chapter of 90 s
    private static readonly ChapterInfo[] Chapters =
    [
        Chapter(0, 0, 0, 60_000), Chapter(1, 0, 60_000, 120_000), Chapter(2, 1, 0, 90_000)
    ];

    private static readonly double[] Files = [120, 90];

    [Fact]
    public void ChapterAtFindsTheChapterOfTheFile()
    {
        Assert.Equal(0, PlaybackPosition.ChapterAt(Chapters, 0, 59_999)!.Index);
        Assert.Equal(1, PlaybackPosition.ChapterAt(Chapters, 0, 90_000)!.Index);
        Assert.Equal(2, PlaybackPosition.ChapterAt(Chapters, 1, 10_000)!.Index);
    }

    [Fact]
    public void ChapterAtPutsABoundaryInTheFollowingChapter() =>
        Assert.Equal(1, PlaybackPosition.ChapterAt(Chapters, 0, 60_000)!.Index);

    [Fact]
    public void ChapterAtPastTheLastChapterOfAFileGivesThatLastChapter()
    {
        Assert.Equal(1, PlaybackPosition.ChapterAt(Chapters, 0, 120_000)!.Index);
        Assert.Equal(2, PlaybackPosition.ChapterAt(Chapters, 1, 500_000)!.Index);
    }

    [Fact]
    public void ChapterAtIsNullForAFileWithoutChapters()
    {
        Assert.Null(PlaybackPosition.ChapterAt(Chapters, 5, 0));
        Assert.Null(PlaybackPosition.ChapterAt([], 0, 0));
    }

    [Fact]
    public void SkipMovesInsideAFile()
    {
        Assert.Equal((0, 40_000d), PlaybackPosition.Skip(Files, 0, 10_000, 30));
        Assert.Equal((0, 0d), PlaybackPosition.Skip(Files, 0, 10_000, -10));
    }

    [Fact]
    public void SkipCrossesFiles()
    {
        Assert.Equal((1, 20_000d), PlaybackPosition.Skip(Files, 0, 110_000, 30));
        Assert.Equal((0, 115_000d), PlaybackPosition.Skip(Files, 1, 5_000, -10));
    }

    [Fact]
    public void SkipStopsAtTheStartAndTheEndOfTheBook()
    {
        Assert.Equal((0, 0d), PlaybackPosition.Skip(Files, 0, 5_000, -10));
        Assert.Equal((1, 90_000d), PlaybackPosition.Skip(Files, 1, 80_000, 30));
    }

    [Fact]
    public void SkipIsNullWithoutFiles() => Assert.Null(PlaybackPosition.Skip([], 0, 0, 30));

    [Fact]
    public void NeighborGivesTheNextAndThePreviousChapter()
    {
        Assert.Equal(2, PlaybackPosition.Neighbor(Chapters, 1, 1)!.Index);
        Assert.Equal(0, PlaybackPosition.Neighbor(Chapters, 1, -1)!.Index);
    }

    [Fact]
    public void NeighborIsNullBeyondTheEnds()
    {
        Assert.Null(PlaybackPosition.Neighbor(Chapters, 0, -1));
        Assert.Null(PlaybackPosition.Neighbor(Chapters, 2, 1));
    }
}
