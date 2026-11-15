using Audibly.Stats;

namespace Audibly.Sync;

/// <summary>
///     The place the device files live in. Windows uses a plain directory; Android reaches the
///     folder through the system's folder access. File names are plain names without a path.
/// </summary>
public interface ISyncFolder
{
    Task<IReadOnlyList<string>> ListAsync();

    /// <summary>The file's text, or null when it does not exist.</summary>
    Task<string?> ReadAsync(string fileName);

    Task WriteAsync(string fileName, string content);
}

/// <summary>
///     Writes this device's file and reads those of the other devices. Nothing here throws: a
///     missing, full or broken sync folder must never get in the way of playback, so a failure
///     only shows up in <see cref="LastError" />.
/// </summary>
public sealed class SyncService(ISyncFolder folder, string deviceId, string deviceName, Func<DateTime> utcNow)
{
    private readonly SemaphoreSlim _busy = new(1, 1);
    private string? _lastWritten;

    public string DeviceId { get; } = deviceId;

    /// <summary>The other devices as of the last successful read.</summary>
    public IReadOnlyList<DeviceState> Others { get; private set; } = [];

    public DateTime? LastSuccessUtc { get; private set; }

    /// <summary>What went wrong the last time, or null when the last push or pull worked.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    ///     Writes this device's state. Returns false when it could not be written. An unchanged
    ///     state is not written again, so the cloud service only uploads when something changed.
    /// </summary>
    public async Task<bool> PushAsync(IEnumerable<LocalBook> books, StatsData ownStats)
    {
        await _busy.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = utcNow();
            var state = SyncMerge.BuildState(DeviceId, deviceName, now, books, ownStats);
            // compared without the time of writing, which changes every time
            var fingerprint = DeviceStateFile.Serialize(state with { WrittenUtc = default });
            if (fingerprint != _lastWritten)
            {
                await folder.WriteAsync(DeviceStateFile.FileName(DeviceId), DeviceStateFile.Serialize(state))
                    .ConfigureAwait(false);
                _lastWritten = fingerprint;
            }

            LastSuccessUtc = now;
            LastError = null;
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            return false;
        }
        finally
        {
            _busy.Release();
        }
    }

    /// <summary>
    ///     Reads the other devices' files. When the folder cannot be read, the devices from the
    ///     last successful read are returned.
    /// </summary>
    public async Task<IReadOnlyList<DeviceState>> PullAsync()
    {
        await _busy.WaitAsync().ConfigureAwait(false);
        try
        {
            var others = new List<DeviceState>();
            foreach (var name in await folder.ListAsync().ConfigureAwait(false))
            {
                if (!DeviceStateFile.IsDeviceFile(name)) continue;
                var state = DeviceStateFile.TryParse(await folder.ReadAsync(name).ConfigureAwait(false));
                if (state != null && state.DeviceId != DeviceId) others.Add(state);
            }

            Others = others;
            LastSuccessUtc = utcNow();
            LastError = null;
        }
        catch (Exception e)
        {
            LastError = e.Message;
        }
        finally
        {
            _busy.Release();
        }

        return Others;
    }
}

/// <summary>A sync folder in the file system, for example a folder that OneDrive keeps in sync.</summary>
public sealed class DirectorySyncFolder(string root) : ISyncFolder
{
    private string Folder => Path.Combine(root, DeviceStateFile.Folder);

    public Task<IReadOnlyList<string>> ListAsync() => Task.Run<IReadOnlyList<string>>(() =>
        Directory.Exists(Folder)
            ? Directory.GetFiles(Folder).Select(Path.GetFileName).OfType<string>().ToList()
            : []);

    public Task<string?> ReadAsync(string fileName) => Task.Run(() =>
    {
        var path = Path.Combine(Folder, Path.GetFileName(fileName));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    });

    public Task WriteAsync(string fileName, string content) => Task.Run(() =>
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, Path.GetFileName(fileName));
        // written beside the target and moved into place, so a reader never sees half a file
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, true);
    });
}
