using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Audibly.Stats;

namespace Audibly.Sync;

/// <summary>A book of this device's library, reduced to what the sync needs.</summary>
public sealed record LocalBook(Guid Id, string Title, string Author, double DurationSeconds, double PositionSeconds,
    double Speed, bool Completed, DateTime? LastPlayedUtc);

/// <summary>Another device is at a different place in a book and listened to it more recently.</summary>
public sealed record PositionOffer(Guid BookId, string DeviceName, double PositionSeconds, double Speed,
    DateTime UpdatedUtc);

/// <summary>
///     Brings the states of several devices together. Nothing here writes into a device's own
///     data: positions are only offered, and statistics are only added up for display, so syncing
///     any number of times never counts listening time twice.
/// </summary>
public static class SyncMerge
{
    /// <summary>Positions closer than this are treated as the same place.</summary>
    public const double SamePlaceSeconds = 30;

    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>
    ///     The most recent position another device has for this book, when it is newer than this
    ///     device's own listening and at a different place. Null when there is nothing to offer or
    ///     the listener already declined that state.
    /// </summary>
    public static PositionOffer? Offer(LocalBook local, IEnumerable<DeviceState> others, DateTime? declinedUtc = null)
    {
        PositionOffer? best = null;
        foreach (var device in Newest(others))
        foreach (var remote in device.Positions)
        {
            if (!BookMatch.Same(local.Title, local.Author, local.DurationSeconds, remote.Title, remote.Author,
                    remote.DurationSeconds)) continue;
            if (local.LastPlayedUtc is { } own && remote.UpdatedUtc <= own) continue;
            if (declinedUtc is { } declined && remote.UpdatedUtc <= declined) continue;
            if (best != null && remote.UpdatedUtc <= best.UpdatedUtc) continue;

            // the other device's copy may be a little longer; never offer a place past this copy's end
            var position = Math.Clamp(remote.PositionSeconds, 0, Math.Max(0, local.DurationSeconds));
            best = new PositionOffer(local.Id, device.DeviceName, position, remote.Speed, remote.UpdatedUtc);
        }

        return best != null && Math.Abs(best.PositionSeconds - local.PositionSeconds) > SamePlaceSeconds ? best : null;
    }

    /// <summary>What this device reports: where it is in its books and what was listened to on it.</summary>
    public static DeviceState BuildState(string deviceId, string deviceName, DateTime nowUtc,
        IEnumerable<LocalBook> books, StatsData ownStats)
    {
        var byId = new Dictionary<Guid, LocalBook>();
        foreach (var book in books) byId[book.Id] = book;

        var positions = byId.Values
            .Where(book => book.LastPlayedUtc != null)
            .Select(book => new SyncedPosition(book.Title, book.Author, book.DurationSeconds, book.PositionSeconds,
                book.Speed, book.Completed, book.LastPlayedUtc!.Value))
            .ToList();

        // listening time is reported by what the book is; a book that left the library cannot be named
        var slices = ownStats.Slices
            .Where(slice => byId.ContainsKey(slice.AudiobookId))
            .Select(slice =>
            {
                var book = byId[slice.AudiobookId];
                return new SyncedSlice(slice.Day.ToString(DayFormat, CultureInfo.InvariantCulture), slice.Hour,
                    book.Title, book.Author, book.DurationSeconds, slice.ListenedSeconds, slice.SavedSeconds);
            })
            .ToList();

        var completions = ownStats.Completions
            .Where(completion => byId.ContainsKey(completion.AudiobookId))
            .Select(completion =>
            {
                var book = byId[completion.AudiobookId];
                return new SyncedCompletion(book.Title, book.Author, book.DurationSeconds, completion.CompletedAt);
            })
            .ToList();

        return new DeviceState(DeviceStateFile.FormatVersion, deviceId, deviceName, nowUtc, positions, slices,
            completions);
    }

    /// <summary>
    ///     This device's statistics plus those of the other devices, for display. Books the other
    ///     devices share with this one are counted under the local book; books that only exist
    ///     elsewhere get a stable id of their own.
    /// </summary>
    public static StatsData Combine(StatsData local, IEnumerable<LocalBook> books, IEnumerable<DeviceState> others)
    {
        var library = books.ToList();
        var slices = new List<ListeningSlice>(local.Slices);
        var completions = new List<Completion>(local.Completions);

        foreach (var device in Newest(others))
        {
            foreach (var slice in device.Slices)
            {
                if (slice.Hour is < 0 or > 23 || slice.Listened < 0 ||
                    !DateOnly.TryParseExact(slice.Day, DayFormat, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var day)) continue;
                slices.Add(new ListeningSlice(day, slice.Hour,
                    IdFor(library, slice.Title, slice.Author, slice.DurationSeconds), slice.Listened,
                    Math.Max(0, slice.Saved)));
            }

            foreach (var completion in device.Completions)
                completions.Add(new Completion(
                    IdFor(library, completion.Title, completion.Author, completion.DurationSeconds),
                    completion.CompletedAt));
        }

        return new StatsData(slices, completions.OrderBy(completion => completion.CompletedAt).ToList());
    }

    /// <summary>One state per device: a cloud service may briefly show an old and a new copy of a file.</summary>
    private static IEnumerable<DeviceState> Newest(IEnumerable<DeviceState> devices) =>
        devices.GroupBy(device => device.DeviceId)
            .Select(group => group.OrderByDescending(device => device.WrittenUtc).First());

    private static Guid IdFor(List<LocalBook> library, string title, string author, double durationSeconds)
    {
        var local = library.FirstOrDefault(book =>
            BookMatch.Same(book.Title, book.Author, book.DurationSeconds, title, author, durationSeconds));
        if (local != null) return local.Id;

        // the same foreign book must get the same id every time, or "different books" would grow with each sync
        var key = BookMatch.Normalize(title) + "\n" + BookMatch.Normalize(author);
        return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(key)));
    }
}
