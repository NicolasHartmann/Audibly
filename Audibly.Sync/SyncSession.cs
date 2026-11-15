using System.Globalization;
using Audibly.Stats;

namespace Audibly.Sync;

/// <summary>A few values the sync keeps on the device; each app stores them where it keeps its settings.</summary>
public interface ISyncSettings
{
    string? Get(string key);
    void Set(string key, string? value);
}

public static class SyncDevice
{
    private const string IdKey = "SyncDeviceId";

    /// <summary>This device's id for its file in the sync folder; created once and then kept.</summary>
    public static string GetOrCreateId(ISyncSettings settings)
    {
        var id = settings.Get(IdKey);
        if (!string.IsNullOrWhiteSpace(id)) return id;
        id = Guid.NewGuid().ToString("N");
        settings.Set(IdKey, id);
        return id;
    }
}

/// <summary>
///     What both apps do with the sync, in one place: when this device's state is written, what is
///     offered when a book is opened, which offers were declined, and the statistics of all
///     devices together. Nothing here throws; a failure shows up in <see cref="LastError" />.
/// </summary>
public sealed class SyncSession(SyncService service, Func<Task<IReadOnlyList<LocalBook>>> books,
    Func<Task<StatsData>> ownStats, ISyncSettings settings, Func<DateTime> utcNow)
{
    /// <summary>While a book plays, its position is written at most this often.</summary>
    public static readonly TimeSpan PlayingPushInterval = TimeSpan.FromMinutes(1);

    private const string DeclinedPrefix = "SyncDeclined.";
    private DateTime _lastPush = DateTime.MinValue;
    private string? _error;

    public IReadOnlyList<DeviceState> Others => service.Others;
    public DateTime? LastSuccessUtc => service.LastSuccessUtc;
    public string? LastError => _error ?? service.LastError;

    /// <summary>
    ///     Writes this device's state. While playing pass <c>force: false</c> so it happens at most
    ///     once a minute; on pause, book change and exit pass <c>force: true</c>.
    /// </summary>
    public async Task PushAsync(bool force)
    {
        var now = utcNow();
        if (!force && now - _lastPush < PlayingPushInterval) return;
        _lastPush = now;
        try
        {
            var library = await books().ConfigureAwait(false);
            var stats = await ownStats().ConfigureAwait(false);
            _error = null;
            await service.PushAsync(library, stats).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _error = e.Message;
        }
    }

    public Task<IReadOnlyList<DeviceState>> PullAsync() => service.PullAsync();

    /// <summary>
    ///     Reads the other devices and returns what to ask the listener when they open this book,
    ///     or null when there is nothing to ask.
    /// </summary>
    public async Task<PositionOffer?> OfferForAsync(Guid bookId)
    {
        try
        {
            var others = await service.PullAsync().ConfigureAwait(false);
            var library = await books().ConfigureAwait(false);
            _error = null;
            var book = library.FirstOrDefault(candidate => candidate.Id == bookId);
            return book == null ? null : SyncMerge.Offer(book, others, Declined(bookId));
        }
        catch (Exception e)
        {
            _error = e.Message;
            return null;
        }
    }

    /// <summary>The listener chose to stay where they are; this state of the other device is not offered again.</summary>
    public void Decline(PositionOffer offer) =>
        settings.Set(DeclinedPrefix + offer.BookId.ToString("N"),
            offer.UpdatedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    /// <summary>This device's statistics plus those of the other devices, for the statistics and trophies pages.</summary>
    public async Task<StatsData> CombinedStatsAsync()
    {
        try
        {
            var own = await ownStats().ConfigureAwait(false);
            var library = await books().ConfigureAwait(false);
            var others = await service.PullAsync().ConfigureAwait(false);
            _error = null;
            return SyncMerge.Combine(own, library, others);
        }
        catch (Exception e)
        {
            _error = e.Message;
            return StatsData.Empty;
        }
    }

    private DateTime? Declined(Guid bookId) =>
        DateTime.TryParse(settings.Get(DeclinedPrefix + bookId.ToString("N")), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var declined)
            ? declined.ToUniversalTime()
            : null;
}
