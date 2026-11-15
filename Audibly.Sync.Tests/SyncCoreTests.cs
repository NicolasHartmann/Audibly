using Audibly.Stats;
using Audibly.Sync;

namespace Audibly.Sync.Tests;

public class SyncCoreTests
{
    private static readonly DateTime Noon = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid BookA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid BookB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static LocalBook Local(Guid id, string title = "The Book", string author = "An Author",
        double duration = 36000, double position = 1000, DateTime? lastPlayedUtc = null) =>
        new(id, title, author, duration, position, 1.0, false, lastPlayedUtc ?? Noon);

    private static SyncedPosition Remote(double position, DateTime updatedUtc, string title = "The Book",
        string author = "An Author", double duration = 36000, double speed = 1.5) =>
        new(title, author, duration, position, speed, false, updatedUtc);

    private static DeviceState Device(string name, params SyncedPosition[] positions) =>
        new(DeviceStateFile.FormatVersion, name + "-id", name, Noon, positions, [], []);

    // --- recognising the same book

    [Theory]
    [InlineData("The Book", "An Author", "the  book", "AN AUTHOR")]
    [InlineData("The Book: A Tale", "J. R. Author", "The Book - A Tale", "J R Author")]
    [InlineData("  The Book ", "An Author", "The Book", "An Author")]
    public void SameTitleAndAuthorIsTheSameBook(string titleA, string authorA, string titleB, string authorB) =>
        Assert.True(BookMatch.Same(titleA, authorA, 100, titleB, authorB, 99999));

    [Fact]
    public void DifferentTitlesMatchWhenAuthorAndLengthAgree()
    {
        // one device read the title from the album, the other from the first chapter
        Assert.True(BookMatch.Same("The Book", "An Author", 36000, "Chapter 1", "An Author", 36001));
        Assert.False(BookMatch.Same("The Book", "An Author", 36000, "Chapter 1", "An Author", 36010));
        Assert.False(BookMatch.Same("The Book", "An Author", 36000, "Chapter 1", "Someone Else", 36000));
    }

    [Fact]
    public void DifferentBooksOfOneAuthorDoNotMatch() =>
        Assert.False(BookMatch.Same("Part One", "An Author", 30000, "Part Two", "An Author", 40000));

    [Fact]
    public void AnEmptyTitleNeverMatchesByTitle() =>
        Assert.False(BookMatch.Same("", "An Author", 100, "", "An Author", 900));

    // --- the device file

    [Fact]
    public void ADeviceStateSurvivesWritingAndReading()
    {
        var state = new DeviceState(DeviceStateFile.FormatVersion, "id-1", "Desk PC", Noon,
            [Remote(4321.5, Noon.AddMinutes(-5))],
            [new SyncedSlice("2026-11-14", 21, "The Book", "An Author", 36000, 600.25, 120.5)],
            [new SyncedCompletion("The Book", "An Author", 36000, new DateTime(2026, 11, 14, 22, 0, 0))]);

        var read = DeviceStateFile.TryParse(DeviceStateFile.Serialize(state));

        Assert.NotNull(read);
        Assert.Equal("Desk PC", read.DeviceName);
        Assert.Equal(Noon, read.WrittenUtc);
        Assert.Equal(state.Positions, read.Positions);
        Assert.Equal(state.Slices, read.Slices);
        Assert.Equal(state.Completions, read.Completions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"formatVersion\":99,\"deviceId\":\"x\",\"deviceName\":\"y\"}")]
    [InlineData("[1,2,3]")]
    public void ABrokenOrNewerFileIsIgnoredNotFatal(string content) =>
        Assert.Null(DeviceStateFile.TryParse(content));

    [Fact]
    public void FileNamesAreSafeAndRecognisable()
    {
        Assert.Equal("abc-123.json", DeviceStateFile.FileName("abc-123"));
        Assert.True(DeviceStateFile.IsDeviceFile("abc-123.json"));
        Assert.False(DeviceStateFile.IsDeviceFile("abc-123 (conflicted copy).json.tmp"));
        Assert.False(DeviceStateFile.IsDeviceFile("notes.txt"));
    }

    // --- offering another device's position

    [Fact]
    public void ANewerDifferentPositionIsOffered()
    {
        var offer = SyncMerge.Offer(Local(BookA), [Device("Phone", Remote(5000, Noon.AddHours(1)))]);

        Assert.NotNull(offer);
        Assert.Equal(BookA, offer.BookId);
        Assert.Equal("Phone", offer.DeviceName);
        Assert.Equal(5000, offer.PositionSeconds);
        Assert.Equal(Noon.AddHours(1), offer.UpdatedUtc);
    }

    [Fact]
    public void AnOlderPositionIsNotOffered() =>
        Assert.Null(SyncMerge.Offer(Local(BookA), [Device("Phone", Remote(5000, Noon.AddHours(-1)))]));

    [Fact]
    public void APositionWithinThirtySecondsIsNotOffered()
    {
        Assert.Null(SyncMerge.Offer(Local(BookA, position: 1000), [Device("Phone", Remote(1030, Noon.AddHours(1)))]));
        Assert.NotNull(SyncMerge.Offer(Local(BookA, position: 1000), [Device("Phone", Remote(1031, Noon.AddHours(1)))]));
        // also backwards: the listener may have rewound on the other device
        Assert.NotNull(SyncMerge.Offer(Local(BookA, position: 1000), [Device("Phone", Remote(900, Noon.AddHours(1)))]));
    }

    [Fact]
    public void TheNewestOfSeveralDevicesIsOffered()
    {
        var offer = SyncMerge.Offer(Local(BookA),
        [
            Device("Phone", Remote(5000, Noon.AddHours(1))),
            Device("Tablet", Remote(7000, Noon.AddHours(3))),
            Device("Laptop", Remote(6000, Noon.AddHours(2)))
        ]);

        Assert.Equal("Tablet", offer!.DeviceName);
        Assert.Equal(7000, offer.PositionSeconds);
    }

    [Fact]
    public void ADeclinedOfferIsNotRepeatedButANewerOneIs()
    {
        var devices = new[] { Device("Phone", Remote(5000, Noon.AddHours(1))) };

        Assert.Null(SyncMerge.Offer(Local(BookA), devices, declinedUtc: Noon.AddHours(1)));
        Assert.NotNull(SyncMerge.Offer(Local(BookA), devices, declinedUtc: Noon.AddMinutes(30)));
    }

    [Fact]
    public void ABookThatWasNeverPlayedHereTakesAnyRemotePosition() =>
        Assert.NotNull(SyncMerge.Offer(Local(BookA, position: 0) with { LastPlayedUtc = null },
            [Device("Phone", Remote(5000, Noon.AddDays(-30)))]));

    [Fact]
    public void OtherBooksAndPositionsPastTheEndAreNotOffered()
    {
        Assert.Null(SyncMerge.Offer(Local(BookA),
            [Device("Phone", Remote(5000, Noon.AddHours(1), title: "Another Book", duration: 100))]));
        // a position beyond this device's copy of the book is clamped to its end, not refused
        var offer = SyncMerge.Offer(Local(BookA, duration: 36000),
            [Device("Phone", Remote(36001, Noon.AddHours(1), duration: 36001))]);
        Assert.Equal(36000, offer!.PositionSeconds);
    }

    // --- what this device reports

    [Fact]
    public void BuildStateReportsPositionsAndOwnListeningByBookIdentity()
    {
        var stats = new StatsData(
            [new ListeningSlice(new DateOnly(2026, 11, 14), 21, BookA, 600, 120),
             new ListeningSlice(new DateOnly(2026, 11, 14), 22, Guid.NewGuid(), 50, 0)],
            [new Completion(BookA, new DateTime(2026, 11, 14, 22, 30, 0))]);

        var state = SyncMerge.BuildState("id-1", "Desk PC", Noon,
            [Local(BookA, position: 1234), Local(BookB, title: "Unplayed") with { LastPlayedUtc = null }], stats);

        Assert.Equal("id-1", state.DeviceId);
        Assert.Equal(Noon, state.WrittenUtc);
        // a book that was never played here has nothing to report
        var position = Assert.Single(state.Positions);
        Assert.Equal(("The Book", 1234d, Noon), (position.Title, position.PositionSeconds, position.UpdatedUtc));
        // listening time of a book that is no longer in the library cannot be named and is left out
        var slice = Assert.Single(state.Slices);
        Assert.Equal(("2026-11-14", 21, "The Book", 600d, 120d),
            (slice.Day, slice.Hour, slice.Title, slice.Listened, slice.Saved));
        Assert.Equal("The Book", Assert.Single(state.Completions).Title);
    }

    // --- statistics of all devices together

    [Fact]
    public void CombineAddsOtherDevicesAndMapsTheirBooksToLocalOnes()
    {
        var local = new StatsData([new ListeningSlice(new DateOnly(2026, 11, 14), 21, BookA, 600, 120)], []);
        var phone = new DeviceState(DeviceStateFile.FormatVersion, "phone-id", "Phone", Noon, [],
            [new SyncedSlice("2026-11-14", 21, "the book", "An Author", 36000, 300, 30),
             new SyncedSlice("2026-11-15", 8, "Only On The Phone", "Other", 5000, 100, 0)],
            [new SyncedCompletion("The Book", "An Author", 36000, new DateTime(2026, 11, 15, 9, 0, 0))]);

        var all = SyncMerge.Combine(local, [Local(BookA)], [phone]);

        Assert.Equal(1000, all.Slices.Sum(slice => slice.ListenedSeconds));
        Assert.Equal(900, all.Slices.Where(slice => slice.AudiobookId == BookA).Sum(slice => slice.ListenedSeconds));
        // a book that only exists elsewhere still counts, under an id of its own
        var foreign = Assert.Single(all.Slices, slice => slice.AudiobookId != BookA);
        Assert.NotEqual(Guid.Empty, foreign.AudiobookId);
        Assert.Equal(BookA, Assert.Single(all.Completions).AudiobookId);
    }

    [Fact]
    public void CombineIsStableSoRepeatedSyncsNeverCountTwice()
    {
        var local = new StatsData([new ListeningSlice(new DateOnly(2026, 11, 14), 21, BookA, 600, 120)], []);
        var phone = new DeviceState(DeviceStateFile.FormatVersion, "phone-id", "Phone", Noon, [],
            [new SyncedSlice("2026-11-15", 8, "Only On The Phone", "Other", 5000, 100, 0)], []);

        var first = SyncMerge.Combine(local, [Local(BookA)], [phone]);
        var second = SyncMerge.Combine(local, [Local(BookA)], [phone, phone with { WrittenUtc = Noon.AddHours(1) }]);

        // the same device seen twice (an old and a new copy of its file) counts once, the newest copy
        Assert.Equal(first.Slices.Sum(slice => slice.ListenedSeconds), second.Slices.Sum(slice => slice.ListenedSeconds));
        Assert.Equal(first.Slices.Single(slice => slice.AudiobookId != BookA).AudiobookId,
            second.Slices.Single(slice => slice.AudiobookId != BookA).AudiobookId);
    }

    [Fact]
    public void CombineSkipsBrokenDaysInsteadOfFailing()
    {
        var phone = new DeviceState(DeviceStateFile.FormatVersion, "phone-id", "Phone", Noon, [],
            [new SyncedSlice("not a day", 8, "The Book", "An Author", 36000, 100, 0),
             new SyncedSlice("2026-11-15", 99, "The Book", "An Author", 36000, 100, 0),
             new SyncedSlice("2026-11-15", 8, "The Book", "An Author", 36000, 100, 0)], []);

        Assert.Equal(100, SyncMerge.Combine(StatsData.Empty, [Local(BookA)], [phone]).Slices.Sum(s => s.ListenedSeconds));
    }
}
