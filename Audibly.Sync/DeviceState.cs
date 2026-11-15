using System.Text.Json;

namespace Audibly.Sync;

/// <summary>Where one device stands in a book. The position is audio time from the start of the whole book.</summary>
public sealed record SyncedPosition(string Title, string Author, double DurationSeconds, double PositionSeconds,
    double Speed, bool Completed, DateTime UpdatedUtc);

/// <summary>Listening time of one device for one book in one hour of a local day ("yyyy-MM-dd").</summary>
public sealed record SyncedSlice(string Day, int Hour, string Title, string Author, double DurationSeconds,
    double Listened, double Saved);

/// <summary>A book finished on one device, at that device's local time.</summary>
public sealed record SyncedCompletion(string Title, string Author, double DurationSeconds, DateTime CompletedAt);

/// <summary>
///     Everything one device reports. Each device writes only its own file, so two devices never
///     overwrite each other and the cloud service has no conflicts to resolve.
/// </summary>
public sealed record DeviceState(int FormatVersion, string DeviceId, string DeviceName, DateTime WrittenUtc,
    IReadOnlyList<SyncedPosition> Positions, IReadOnlyList<SyncedSlice> Slices,
    IReadOnlyList<SyncedCompletion> Completions);

/// <summary>Reads and writes a device's file. A file that cannot be understood is ignored, never fatal.</summary>
public static class DeviceStateFile
{
    public const int FormatVersion = 1;

    /// <summary>The subfolder of the sync folder that holds the device files.</summary>
    public const string Folder = "audibly-sync";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public static string FileName(string deviceId) => deviceId + ".json";

    /// <summary>True for a device file, false for anything else a cloud service may leave in the folder.</summary>
    public static bool IsDeviceFile(string fileName) =>
        fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
        !fileName.Contains(' ') && !fileName.Contains('(');

    public static string Serialize(DeviceState state) => JsonSerializer.Serialize(state, Options);

    /// <summary>Null when the content is empty, damaged, incomplete or written by a newer format.</summary>
    public static DeviceState? TryParse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        try
        {
            var state = JsonSerializer.Deserialize<DeviceState>(content, Options);
            if (state is null || state.FormatVersion != FormatVersion) return null;
            if (string.IsNullOrWhiteSpace(state.DeviceId) || state.DeviceName is null) return null;
            // lists that are missing in the file arrive as null
            return state with
            {
                Positions = state.Positions ?? [],
                Slices = state.Slices ?? [],
                Completions = state.Completions ?? []
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
