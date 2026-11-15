using Audibly.Core;
using Audibly.Models;
using Audibly.Stats;
using Audibly.Sync;

namespace Audibly.Mobile.Services;

/// <summary>
///     Connects the app to the sync: this device's file is written to the sync folder and the
///     other devices' files are read from it. With no folder picked every call does nothing, and
///     no call ever throws: sync must not get in the way of playback.
/// </summary>
public sealed class SyncHost
{
    private readonly AppData _data;
    private readonly ISyncFolderAccess _folder;
    private readonly PreferenceSettings _settings = new();
    private SyncSession? _session;

    public SyncHost(AppData data, ISyncFolderAccess folder)
    {
        _data = data;
        _folder = folder;
        Start();
    }

    /// <summary>
    ///     The book that is playing, with its position as of this moment. The library database is
    ///     only saved every few seconds, so without this the written position would lag behind.
    /// </summary>
    public Func<Audiobook?> PlayingBook { get; set; } = () => null;

    public bool IsEnabled => _session != null;
    public string? FolderName => _folder.FolderName;
    public bool HasFolderAccess => _folder.HasAccess;
    public IReadOnlyList<DeviceState> Others => _session?.Others ?? [];
    public DateTime? LastSuccessUtc => _session?.LastSuccessUtc;
    public string? LastError => _session?.LastError;

    /// <summary>Raised after a push or pull, on whatever thread it finished on.</summary>
    public event Action? Changed;

    /// <summary>Lets the listener pick the sync folder and syncs once. False when they cancelled.</summary>
    public async Task<bool> PickFolderAsync()
    {
        try
        {
            if (!await _folder.PickAsync()) return false;
            Start();
            if (_session != null)
            {
                await _session.PullAsync();
                await _session.PushAsync(force: true);
            }

            Changed?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine("[Sync] " + e);
            return false;
        }
    }

    public void TurnOff()
    {
        _folder.Forget();
        _session = null;
        Changed?.Invoke();
    }

    /// <summary>Writes this device's state: unforced while playing, forced on pause, book change and exit.</summary>
    public async Task PushAsync(bool force)
    {
        if (_session is not { } session) return;
        await session.PushAsync(force);
        Changed?.Invoke();
    }

    /// <summary>What to ask the listener when they open this book, or null.</summary>
    public async Task<PositionOffer?> OfferForAsync(Guid bookId)
    {
        if (_session is not { } session) return null;
        var offer = await session.OfferForAsync(bookId);
        Changed?.Invoke();
        return offer;
    }

    public void Decline(PositionOffer offer) => _session?.Decline(offer);

    /// <summary>Reads the other devices again, for the settings page.</summary>
    public async Task RefreshAsync()
    {
        if (_session is not { } session) return;
        await session.PullAsync();
        Changed?.Invoke();
    }

    private void Start()
    {
        try
        {
            var folder = _folder.Open();
            _session = folder == null
                ? null
                : new SyncSession(
                    new SyncService(folder, SyncDevice.GetOrCreateId(_settings), DeviceName(), () => DateTime.UtcNow),
                    LoadBooksAsync, () => _data.Stats.LoadAsync(), _settings, () => DateTime.UtcNow);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine("[Sync] " + e);
            _session = null;
        }
    }

    private static string DeviceName()
    {
        var model = DeviceInfo.Current.Model;
        return string.IsNullOrWhiteSpace(model) ? "Android phone" : model;
    }

    private async Task<IReadOnlyList<LocalBook>> LoadBooksAsync()
    {
        var playing = PlayingBook();
        var books = await _data.LoadBooksAsync();
        return books.Select(book => ToLocal(playing != null && playing.Id == book.Id ? playing : book)).ToList();
    }

    private static LocalBook ToLocal(Audiobook book)
    {
        var index = Math.Clamp(book.CurrentSourceFileIndex, 0, Math.Max(0, book.SourcePaths.Count - 1));
        var positionMs = book.SourcePaths.Count == 0
            ? 0
            : PlayerProgress.BookPositionMs(book.SourcePaths.Select(file => (double)file.Duration), index,
                book.SourcePaths[index].CurrentTimeMs);
        return new LocalBook(book.Id, book.Title ?? string.Empty, book.Author ?? string.Empty, book.Duration,
            positionMs / 1000, book.PlaybackSpeed, book.IsCompleted, book.DateLastPlayed?.ToUniversalTime());
    }

    /// <summary>The sync's own values, kept with the app's other settings.</summary>
    private sealed class PreferenceSettings : ISyncSettings
    {
        public string? Get(string key) => Preferences.Default.Get<string?>(key, null);

        public void Set(string key, string? value)
        {
            if (value == null) Preferences.Default.Remove(key);
            else Preferences.Default.Set(key, value);
        }
    }
}
