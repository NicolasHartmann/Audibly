using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Audibly.App.Helpers;
using Audibly.App.ViewModels;
using Audibly.Core;
using Audibly.Stats;
using Audibly.Sync;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;

namespace Audibly.App.Services;

/// <summary>
///     Connects the app to the sync: this device's file is written to the sync folder and the
///     other devices' files are read from it. With no folder chosen every call does nothing, and
///     no call ever throws: sync must not get in the way of playback.
/// </summary>
public sealed class SyncHost
{
    private static readonly TimeSpan ShutdownPushLimit = TimeSpan.FromSeconds(2);

    private readonly LocalSettingsStore _settings = new();
    private readonly DispatcherQueue? _createdOn = DispatcherQueue.GetForCurrentThread();
    private SyncSession? _session;
    private string? _loggedError;

    public SyncHost()
    {
        try
        {
            if (UserSettings.SyncFolderPath is { } path) _session = CreateSession(path);
        }
        catch (Exception e)
        {
            // runs while the app starts; a broken setting must not keep it from starting
            System.Diagnostics.Debug.WriteLine("[Sync] " + e);
        }
    }

    public bool IsEnabled => _session != null;

    /// <summary>The chosen sync folder, or null when sync is off.</summary>
    public string? FolderPath { get; private set; } = SafeFolderPath();

    public IReadOnlyList<DeviceState> Others => _session?.Others ?? [];
    public DateTime? LastSuccessUtc => _session?.LastSuccessUtc;
    public string? LastError => _session?.LastError;

    /// <summary>Raised on the UI thread when the folder, the last sync or the devices found changed.</summary>
    public event Action? Changed;

    /// <summary>Turns sync on for this folder. Nothing is read or written yet; see <see cref="SyncNowAsync" />.</summary>
    public void SetFolder(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            _session = CreateSession(path);
            UserSettings.SyncFolderPath = path;
            FolderPath = path;
            _loggedError = null;
        }
        catch (Exception e)
        {
            Log(e);
        }

        RaiseChanged();
    }

    /// <summary>Stops syncing. The files in the folder stay where they are.</summary>
    public void TurnOff()
    {
        try
        {
            _session = null;
            FolderPath = null;
            UserSettings.SyncFolderPath = null;
        }
        catch (Exception e)
        {
            Log(e);
        }

        RaiseChanged();
    }

    /// <summary>Reads the other devices and writes this device's state once, for the settings page.</summary>
    public async Task SyncNowAsync()
    {
        if (_session is not { } session) return;
        try
        {
            await session.PullAsync();
            if (LibraryIsLoaded()) await session.PushAsync(true);
        }
        catch (Exception e)
        {
            Log(e);
        }

        AfterSync(session, null);
    }

    /// <summary>Writes this device's state: unforced while playing, forced on pause and book change.</summary>
    public async Task PushAsync(bool force)
    {
        if (_session is not { } session) return;
        try
        {
            if (!LibraryIsLoaded()) return;
            var before = State(session);
            await session.PushAsync(force);
            AfterSync(session, before);
        }
        catch (Exception e)
        {
            Log(e);
        }
    }

    /// <summary>
    ///     Writes this device's state before the app closes; gives up after two seconds. Call from
    ///     the UI thread: the library is read right here, so waiting cannot lock up on it.
    /// </summary>
    public void PushBeforeExit()
    {
        if (_session is not { } session) return;
        try
        {
            if (!LibraryIsLoaded()) return;
            // the session directly, not PushAsync above, whose continuation would need this thread
            session.PushAsync(true).Wait(ShutdownPushLimit);
        }
        catch (Exception e)
        {
            Log(e);
        }
    }

    /// <summary>What to ask the listener when they open this book, or null.</summary>
    public async Task<PositionOffer?> OfferForAsync(Guid bookId)
    {
        if (_session is not { } session) return null;
        try
        {
            var before = State(session);
            var offer = await session.OfferForAsync(bookId);
            AfterSync(session, before);
            return offer;
        }
        catch (Exception e)
        {
            Log(e);
            return null;
        }
    }

    /// <summary>The listener chose to stay where they are; this state is not offered again.</summary>
    public void Decline(PositionOffer offer)
    {
        try
        {
            _session?.Decline(offer);
        }
        catch (Exception e)
        {
            Log(e);
        }
    }

    /// <summary>The statistics of all devices together when sync is on, this device's own otherwise.</summary>
    public async Task<StatsData> LoadStatsAsync()
    {
        try
        {
            if (_session is { } session)
            {
                var before = State(session);
                var combined = await session.CombinedStatsAsync();
                AfterSync(session, before);
                // the session answers a failure with the shared empty instance; own statistics are better than none
                if (!ReferenceEquals(combined, StatsData.Empty)) return combined;
            }

            return await App.ListeningStats.LoadAsync();
        }
        catch (Exception e)
        {
            Log(e);
            return StatsData.Empty;
        }
    }

    /// <summary>"today 21:10", "yesterday 21:10" or a short date with the time, in local time.</summary>
    public static string When(DateTime utc, DateTime localNow)
    {
        var local = (utc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : utc)
            .ToLocalTime();
        var time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (local.Date == localNow.Date) return "today " + time;
        if (local.Date == localNow.Date.AddDays(-1)) return "yesterday " + time;
        return local.ToString("d", CultureInfo.CurrentCulture) + " " + time;
    }

    private SyncSession CreateSession(string path) =>
        new(new SyncService(new DirectorySyncFolder(path), SyncDevice.GetOrCreateId(_settings),
                Environment.MachineName, () => DateTime.UtcNow),
            BooksAsync, () => App.ListeningStats.LoadAsync(), _settings, () => DateTime.UtcNow);

    private static string? SafeFolderPath()
    {
        try
        {
            return UserSettings.SyncFolderPath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private DispatcherQueue? UiQueue => _createdOn ?? App.Window?.DispatcherQueue;

    /// <summary>
    ///     False while the library has not been read yet (or is empty). Writing then would replace
    ///     this device's file with one that reports no books and no listening time.
    /// </summary>
    private bool LibraryIsLoaded() =>
        UiQueue is not { HasThreadAccess: true } || App.ViewModel.AudiobooksForFilter.Count > 0;

    /// <summary>The library belongs to the UI thread, so it is always read there.</summary>
    private Task<IReadOnlyList<LocalBook>> BooksAsync()
    {
        var queue = UiQueue ?? throw new InvalidOperationException("The library cannot be read yet.");
        return queue.HasThreadAccess ? Task.FromResult(Snapshot()) : queue.EnqueueAsync(Snapshot);
    }

    private static IReadOnlyList<LocalBook> Snapshot()
    {
        // a reloaded library holds new objects; the one that is playing has the current position
        var playing = App.PlayerViewModel.NowPlaying;
        var books = new List<LocalBook>();
        foreach (var listed in App.ViewModel.AudiobooksForFilter)
            try
            {
                var book = playing != null && playing.Id == listed.Id ? playing : listed;
                if (book.SourcePaths.Count == 0) continue;
                var index = Math.Clamp(book.CurrentSourceFileIndex, 0, book.SourcePaths.Count - 1);
                var positionMs = PlayerProgress.BookPositionMs(book.SourcePaths.Select(file => (double)file.Duration),
                    index, book.SourcePaths[index].CurrentTimeMs);
                books.Add(new LocalBook(book.Id, book.Title ?? string.Empty, book.Author ?? string.Empty,
                    book.Duration, positionMs / 1000, book.PlaybackSpeed, book.IsCompleted,
                    book.DateLastPlayed?.ToUniversalTime()));
            }
            catch (Exception e)
            {
                // one damaged entry must not keep the others from syncing
                System.Diagnostics.Debug.WriteLine("[Sync] " + e);
            }

        return books;
    }

    private static (DateTime? Success, string? Error, IReadOnlyList<DeviceState> Others) State(SyncSession session) =>
        (session.LastSuccessUtc, session.LastError, session.Others);

    /// <summary>Logs a new failure once and tells the settings page when something it shows changed.</summary>
    private void AfterSync(SyncSession session,
        (DateTime? Success, string? Error, IReadOnlyList<DeviceState> Others)? before)
    {
        try
        {
            if (!ReferenceEquals(session, _session)) return;
            var now = State(session);
            if (now.Error != _loggedError)
            {
                _loggedError = now.Error;
                if (now.Error != null) App.ViewModel.LoggingService.Log("[Sync] " + now.Error);
            }

            if (before is { } was && was.Success == now.Success && was.Error == now.Error &&
                ReferenceEquals(was.Others, now.Others)) return;
            RaiseChanged();
        }
        catch (Exception e)
        {
            Log(e);
        }
    }

    private void RaiseChanged()
    {
        try
        {
            if (Changed is not { } handler) return;

            void Invoke()
            {
                try
                {
                    handler();
                }
                catch (Exception e)
                {
                    Log(e);
                }
            }

            if (UiQueue is { HasThreadAccess: false } queue) queue.TryEnqueue(Invoke);
            else Invoke();
        }
        catch (Exception e)
        {
            Log(e);
        }
    }

    private static void Log(Exception e)
    {
        try
        {
            App.ViewModel.LoggingService.LogError(e);
        }
        catch
        {
            // nothing is left to tell
        }
    }

    /// <summary>The sync's own values, kept with the app's other settings under their own prefix.</summary>
    private sealed class LocalSettingsStore : ISyncSettings
    {
        private const string Prefix = "Sync.";

        public string? Get(string key) => ApplicationData.Current.LocalSettings.Values[Prefix + key]?.ToString();

        public void Set(string key, string? value)
        {
            if (value == null) ApplicationData.Current.LocalSettings.Values.Remove(Prefix + key);
            else ApplicationData.Current.LocalSettings.Values[Prefix + key] = value;
        }
    }
}
