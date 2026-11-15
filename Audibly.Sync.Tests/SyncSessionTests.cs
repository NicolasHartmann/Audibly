using Audibly.Stats;
using Audibly.Sync;

namespace Audibly.Sync.Tests;

public class SyncSessionTests
{
    private static readonly DateTime Noon = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid BookA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private sealed class MemorySettings : ISyncSettings
    {
        public Dictionary<string, string> Values { get; } = [];
        public string? Get(string key) => Values.GetValueOrDefault(key);

        public void Set(string key, string? value)
        {
            if (value == null) Values.Remove(key);
            else Values[key] = value;
        }
    }

    private sealed class MemoryFolder : ISyncFolder
    {
        public Dictionary<string, string> Files { get; } = [];
        public int Writes { get; private set; }
        public Task<IReadOnlyList<string>> ListAsync() => Task.FromResult<IReadOnlyList<string>>(Files.Keys.ToList());
        public Task<string?> ReadAsync(string fileName) => Task.FromResult(Files.GetValueOrDefault(fileName));

        public Task WriteAsync(string fileName, string content)
        {
            Files[fileName] = content;
            Writes++;
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture
    {
        public MemoryFolder Folder { get; } = new();
        public MemorySettings Settings { get; } = new();
        public DateTime Now { get; set; } = Noon;
        public LocalBook Book { get; set; } = new(BookA, "The Book", "An Author", 36000, 1000, 1.0, false, Noon);
        public StatsData Stats { get; set; } = StatsData.Empty;
        public SyncSession Session { get; }

        public Fixture()
        {
            var id = SyncDevice.GetOrCreateId(Settings);
            Session = new SyncSession(new SyncService(Folder, id, "Desk PC", () => Now),
                () => Task.FromResult<IReadOnlyList<LocalBook>>([Book]), () => Task.FromResult(Stats), Settings,
                () => Now);
        }

        public void PhoneAt(double position, DateTime updatedUtc) =>
            Folder.Files["phone.json"] = DeviceStateFile.Serialize(new DeviceState(DeviceStateFile.FormatVersion,
                "phone", "Phone", updatedUtc,
                [new SyncedPosition("The Book", "An Author", 36000, position, 1.5, false, updatedUtc)],
                [new SyncedSlice("2026-11-15", 8, "The Book", "An Author", 36000, 300, 30)], []));
    }

    [Fact]
    public void TheDeviceIdIsCreatedOnceAndKept()
    {
        var settings = new MemorySettings();
        var id = SyncDevice.GetOrCreateId(settings);

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Equal(id, SyncDevice.GetOrCreateId(settings));
        Assert.NotEqual(id, SyncDevice.GetOrCreateId(new MemorySettings()));
        // the id becomes a file name
        Assert.True(DeviceStateFile.IsDeviceFile(DeviceStateFile.FileName(id)));
    }

    [Fact]
    public async Task PushesWhilePlayingAreSpacedAMinuteApartButAForcedPushIsNot()
    {
        var f = new Fixture();

        await f.Session.PushAsync(force: false);
        f.Book = f.Book with { PositionSeconds = 1010 };
        f.Now = Noon.AddSeconds(30);
        await f.Session.PushAsync(force: false);
        Assert.Equal(1, f.Folder.Writes);

        // pause, book change and exit must not wait for the minute to pass
        await f.Session.PushAsync(force: true);
        Assert.Equal(2, f.Folder.Writes);

        f.Book = f.Book with { PositionSeconds = 1100 };
        f.Now = Noon.AddSeconds(95);
        await f.Session.PushAsync(force: false);
        Assert.Equal(3, f.Folder.Writes);
    }

    [Fact]
    public async Task OpeningABookOffersTheOtherDevicesPosition()
    {
        var f = new Fixture();
        f.PhoneAt(5000, Noon.AddHours(1));

        var offer = await f.Session.OfferForAsync(BookA);

        Assert.Equal(("Phone", 5000d), (offer!.DeviceName, offer.PositionSeconds));
        Assert.Null(await f.Session.OfferForAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ADeclinedOfferStaysDeclinedAcrossRestarts()
    {
        var f = new Fixture();
        f.PhoneAt(5000, Noon.AddHours(1));
        f.Session.Decline((await f.Session.OfferForAsync(BookA))!);

        Assert.Null(await f.Session.OfferForAsync(BookA));

        // a new session with the same settings: the app was restarted
        var restarted = new SyncSession(new SyncService(f.Folder, SyncDevice.GetOrCreateId(f.Settings), "Desk PC",
                () => f.Now), () => Task.FromResult<IReadOnlyList<LocalBook>>([f.Book]),
            () => Task.FromResult(f.Stats), f.Settings, () => f.Now);
        Assert.Null(await restarted.OfferForAsync(BookA));

        // the phone moved on: that is a new state and is offered again
        f.PhoneAt(6000, Noon.AddHours(2));
        Assert.Equal(6000, (await restarted.OfferForAsync(BookA))!.PositionSeconds);
    }

    [Fact]
    public async Task CombinedStatisticsIncludeTheOtherDevices()
    {
        var f = new Fixture
        {
            Stats = new StatsData([new ListeningSlice(new DateOnly(2026, 11, 14), 21, BookA, 600, 120)], [])
        };
        f.PhoneAt(5000, Noon.AddHours(1));

        var all = await f.Session.CombinedStatsAsync();

        Assert.Equal(900, all.Slices.Sum(slice => slice.ListenedSeconds));
        Assert.All(all.Slices, slice => Assert.Equal(BookA, slice.AudiobookId));
        Assert.Equal("Phone", Assert.Single(f.Session.Others).DeviceName);
    }

    [Fact]
    public async Task FailingCallbacksNeverThrow()
    {
        var folder = new MemoryFolder();
        var settings = new MemorySettings();
        var session = new SyncSession(new SyncService(folder, "pc", "Desk PC", () => Noon),
            () => throw new InvalidOperationException("library is busy"),
            () => throw new InvalidOperationException("stats are busy"), settings, () => Noon);

        await session.PushAsync(force: true);
        Assert.Null(await session.OfferForAsync(BookA));
        Assert.Empty((await session.CombinedStatsAsync()).Slices);
        Assert.Contains("busy", session.LastError);
    }
}
