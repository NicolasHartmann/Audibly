using Audibly.Stats;
using Audibly.Sync;

namespace Audibly.Sync.Tests;

public class SyncServiceTests
{
    private static readonly DateTime Noon = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid BookA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private sealed class MemoryFolder : ISyncFolder
    {
        public Dictionary<string, string> Files { get; } = [];
        public int Writes { get; private set; }
        public bool Broken { get; set; }

        public Task<IReadOnlyList<string>> ListAsync() =>
            Broken ? throw new IOException("folder is gone") : Task.FromResult<IReadOnlyList<string>>(Files.Keys.ToList());

        public Task<string?> ReadAsync(string fileName) =>
            Broken ? throw new IOException("folder is gone") : Task.FromResult(Files.GetValueOrDefault(fileName));

        public Task WriteAsync(string fileName, string content)
        {
            if (Broken) throw new IOException("folder is gone");
            Files[fileName] = content;
            Writes++;
            return Task.CompletedTask;
        }
    }

    private static LocalBook Book(double position = 1000) =>
        new(BookA, "The Book", "An Author", 36000, position, 1.0, false, Noon);

    private static string OtherDevice(string id, string name, double position = 5000) =>
        DeviceStateFile.Serialize(new DeviceState(DeviceStateFile.FormatVersion, id, name, Noon,
            [new SyncedPosition("The Book", "An Author", 36000, position, 1.5, false, Noon.AddHours(1))], [], []));

    [Fact]
    public async Task PushWritesOnlyThisDevicesFile()
    {
        var folder = new MemoryFolder();
        folder.Files["phone.json"] = OtherDevice("phone", "Phone");
        var now = Noon;
        var service = new SyncService(folder, "pc", "Desk PC", () => now);

        Assert.True(await service.PushAsync([Book()], StatsData.Empty));

        Assert.Equal(["pc.json", "phone.json"], folder.Files.Keys.Order());
        var written = DeviceStateFile.TryParse(folder.Files["pc.json"])!;
        Assert.Equal(("pc", "Desk PC", Noon), (written.DeviceId, written.DeviceName, written.WrittenUtc));
        Assert.Equal(1000, Assert.Single(written.Positions).PositionSeconds);
        Assert.Equal(Noon, service.LastSuccessUtc);
        Assert.Null(service.LastError);
    }

    [Fact]
    public async Task AnUnchangedStateIsNotWrittenAgain()
    {
        var folder = new MemoryFolder();
        var now = Noon;
        var service = new SyncService(folder, "pc", "Desk PC", () => now);

        await service.PushAsync([Book()], StatsData.Empty);
        now = Noon.AddMinutes(5);
        await service.PushAsync([Book()], StatsData.Empty);
        Assert.Equal(1, folder.Writes);

        // the cloud service should only have something to upload when something changed
        await service.PushAsync([Book(position: 1200)], StatsData.Empty);
        Assert.Equal(2, folder.Writes);
    }

    [Fact]
    public async Task PullReturnsTheOtherDevicesAndSkipsEverythingElse()
    {
        var folder = new MemoryFolder();
        folder.Files["pc.json"] = OtherDevice("pc", "Desk PC");
        folder.Files["phone.json"] = OtherDevice("phone", "Phone");
        folder.Files["tablet.json"] = "damaged {";
        folder.Files["phone (conflicted copy).json"] = OtherDevice("phone", "Phone", position: 1);
        folder.Files["readme.txt"] = "hello";
        var service = new SyncService(folder, "pc", "Desk PC", () => Noon);

        var others = await service.PullAsync();

        var phone = Assert.Single(others);
        Assert.Equal("Phone", phone.DeviceName);
        Assert.Equal(5000, phone.Positions[0].PositionSeconds);
        Assert.Same(others, service.Others);
    }

    [Fact]
    public async Task ABrokenFolderNeverThrowsAndKeepsTheLastKnownDevices()
    {
        var folder = new MemoryFolder();
        folder.Files["phone.json"] = OtherDevice("phone", "Phone");
        var service = new SyncService(folder, "pc", "Desk PC", () => Noon);
        await service.PullAsync();

        folder.Broken = true;
        Assert.False(await service.PushAsync([Book()], StatsData.Empty));
        var others = await service.PullAsync();

        Assert.Equal("Phone", Assert.Single(others).DeviceName);
        Assert.Contains("folder is gone", service.LastError);

        folder.Broken = false;
        Assert.True(await service.PushAsync([Book()], StatsData.Empty));
        Assert.Null(service.LastError);
    }

    [Fact]
    public async Task ADirectoryWorksAsSyncFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "audibly-sync-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pc = new SyncService(new DirectorySyncFolder(root), "pc", "Desk PC", () => Noon);
            var phone = new SyncService(new DirectorySyncFolder(root), "phone", "Phone", () => Noon.AddHours(2));

            Assert.Empty(await pc.PullAsync());
            Assert.True(await phone.PushAsync([Book(position: 7000) with { LastPlayedUtc = Noon.AddHours(2) }],
                StatsData.Empty));

            // the files live in their own subfolder, so the listener's folder stays tidy
            Assert.True(File.Exists(Path.Combine(root, DeviceStateFile.Folder, "phone.json")));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, DeviceStateFile.Folder), "*.tmp"));

            var offer = SyncMerge.Offer(Book(), await pc.PullAsync());
            Assert.Equal(("Phone", 7000d), (offer!.DeviceName, offer.PositionSeconds));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
