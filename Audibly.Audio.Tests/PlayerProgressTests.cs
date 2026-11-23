using Audibly.Core;
using Xunit;

namespace Audibly.Audio.Tests;

public class PlayerProgressTests
{
    private const double HourMs = 3_600_000;

    [Theory]
    [InlineData(0, "0:00:00")]
    [InlineData(61_000, "0:01:01")]
    [InlineData(4 * HourMs, "4:00:00")]
    [InlineData(-5, "0:00:00")]
    [InlineData(double.NaN, "0:00:00")]
    public void ElapsedIsHoursMinutesSeconds(double positionMs, string expected) =>
        Assert.Equal(expected, PlayerProgress.Elapsed(positionMs));

    [Fact]
    public void TotalIgnoresTheSpeed() =>
        Assert.Equal("10:00:00", PlayerProgress.End(PlayerTimeDisplay.ElapsedAndTotal, 10 * HourMs, 4 * HourMs, 2));

    [Fact]
    public void RemainingIsDividedByTheSpeed()
    {
        Assert.Equal("-3:00:00", PlayerProgress.End(PlayerTimeDisplay.Remaining, 10 * HourMs, 4 * HourMs, 2));
        Assert.Equal("-6:00:00", PlayerProgress.End(PlayerTimeDisplay.Remaining, 10 * HourMs, 4 * HourMs, 1));
        Assert.Equal("-0:00", PlayerProgress.End(PlayerTimeDisplay.Remaining, 10 * HourMs, 11 * HourMs, 1));
    }

    [Theory]
    [InlineData(10 * HourMs, 4 * HourMs, "40 %")]
    [InlineData(1000, 999, "99 %")]
    [InlineData(1000, 1000, "100 %")]
    [InlineData(1000, 5000, "100 %")]
    [InlineData(1000, -1, "0 %")]
    [InlineData(0, 0, "0 %")]
    [InlineData(double.NaN, 5, "0 %")]
    public void PercentIsRoundedDownAndStaysInRange(double durationMs, double positionMs, string expected) =>
        Assert.Equal(expected, PlayerProgress.End(PlayerTimeDisplay.Percent, durationMs, positionMs, 2));

    [Fact]
    public void NextCyclesThroughAllDisplays()
    {
        Assert.Equal(PlayerTimeDisplay.Remaining, PlayerProgress.Next(PlayerTimeDisplay.ElapsedAndTotal));
        Assert.Equal(PlayerTimeDisplay.Percent, PlayerProgress.Next(PlayerTimeDisplay.Remaining));
        Assert.Equal(PlayerTimeDisplay.ElapsedAndTotal, PlayerProgress.Next(PlayerTimeDisplay.Percent));
    }

    [Fact]
    public void BookPositionAddsThePreviousFiles()
    {
        double[] files = [100, 200, 300];
        Assert.Equal(5_000, PlayerProgress.BookPositionMs(files, 0, 5_000));
        Assert.Equal(305_000, PlayerProgress.BookPositionMs(files, 2, 5_000));
        Assert.Equal(300_000, PlayerProgress.BookPositionMs(files, 2, -5));
    }

    [Fact]
    public void LocateFindsTheFileAndTheOffsetInIt()
    {
        double[] files = [100, 200, 300];
        Assert.Equal((0, 5_000d), PlayerProgress.Locate(files, 5_000));
        Assert.Equal((1, 50_000d), PlayerProgress.Locate(files, 150_000));
        Assert.Equal((2, 299_000d), PlayerProgress.Locate(files, 599_000));
    }

    [Fact]
    public void LocatePutsABoundaryAtTheStartOfTheNextFile() =>
        Assert.Equal((1, 0d), PlayerProgress.Locate([100, 200, 300], 100_000));

    [Fact]
    public void LocateClampsToTheBook()
    {
        double[] files = [100, 200, 300];
        Assert.Equal((2, 300_000d), PlayerProgress.Locate(files, 600_000));
        Assert.Equal((2, 300_000d), PlayerProgress.Locate(files, 9_000_000));
        Assert.Equal((0, 0d), PlayerProgress.Locate(files, -1));
        Assert.Equal((0, 0d), PlayerProgress.Locate(files, double.NaN));
        Assert.Equal((0, 0d), PlayerProgress.Locate([], 5_000));
    }

    [Fact]
    public void LocateIsTheInverseOfBookPosition()
    {
        double[] files = [100, 200, 300];
        var (index, positionMs) = PlayerProgress.Locate(files, 250_000);
        Assert.Equal(250_000, PlayerProgress.BookPositionMs(files, index, positionMs));
    }

    [Fact]
    public void OtherScopeSwaps()
    {
        Assert.Equal(PlayerProgressScope.Book, PlayerProgress.Other(PlayerProgressScope.Chapter));
        Assert.Equal(PlayerProgressScope.Chapter, PlayerProgress.Other(PlayerProgressScope.Book));
    }

    [Fact]
    public void SecondLineElapsedAndTotal() =>
        Assert.Equal("Book  4:00:00 · 10:00:00",
            PlayerProgress.SecondLine(PlayerProgressScope.Book, PlayerTimeDisplay.ElapsedAndTotal, 10 * HourMs,
                4 * HourMs, 2));

    [Fact]
    public void SecondLineRemainingUsesSpeed() =>
        Assert.Equal("Book  4:00:00 · 3:00:00 left",
            PlayerProgress.SecondLine(PlayerProgressScope.Book, PlayerTimeDisplay.Remaining, 10 * HourMs,
                4 * HourMs, 2));

    [Fact]
    public void SecondLinePercent() =>
        Assert.Equal("Chapter  50 %",
            PlayerProgress.SecondLine(PlayerProgressScope.Chapter, PlayerTimeDisplay.Percent, HourMs, HourMs / 2, 1));

    [Theory]
    [InlineData(10 * HourMs, HourMs, true)]
    [InlineData(10 * HourMs, 10 * HourMs, false)] // one file, no chapters: chapter = whole book
    [InlineData(10 * HourMs, 10 * HourMs - 500, false)] // rounding differences do not count
    [InlineData(0, 0, false)]
    [InlineData(double.NaN, HourMs, false)]
    public void SecondLineOnlyWhenChapterIsShorterThanBook(double bookMs, double chapterMs, bool expected) =>
        Assert.Equal(expected, PlayerProgress.ShowSecondLine(bookMs, chapterMs));

    [Fact]
    public void ParseReadsStoredNumbersAndFallsBack()
    {
        Assert.Equal(PlayerProgressScope.Book, PlayerProgress.Parse(1, PlayerProgressScope.Chapter));
        Assert.Equal(PlayerTimeDisplay.Percent, PlayerProgress.Parse("2", PlayerTimeDisplay.ElapsedAndTotal));
        Assert.Equal(PlayerTimeDisplay.ElapsedAndTotal, PlayerProgress.Parse(7, PlayerTimeDisplay.ElapsedAndTotal));
        Assert.Equal(PlayerProgressScope.Chapter, PlayerProgress.Parse(null, PlayerProgressScope.Chapter));
        Assert.Equal(PlayerProgressScope.Chapter, PlayerProgress.Parse("x", PlayerProgressScope.Chapter));
    }
}
