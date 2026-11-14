using Audibly.Core;
using Audibly.Models;
using Audibly.Stats;

namespace Audibly.Mobile.Services;

/// <summary>
///     The book that is playing: keeps its position, chapter and progress up to date, saves them and
///     records listening time. Lives as long as the process, independent of any page, so playback
///     with the screen off is tracked as well.
/// </summary>
public sealed class PlaybackCoordinator
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(10);

    private readonly AppData _data;
    private readonly IPlaybackService _player;
    private readonly ListeningStatsRecorder _stats;
    private IDispatcherTimer? _timer;
    private DateTime _lastSave = DateTime.MinValue;
    private DateTime _lastStatsFlush = DateTime.MinValue;
    private bool _saving;

    public PlaybackCoordinator(AppData data, IPlaybackService player)
    {
        _data = data;
        _player = player;
        _stats = new ListeningStatsRecorder(data.Stats, () => DateTime.Now, () => LibraryCount,
            e => System.Diagnostics.Debug.WriteLine("[Stats] " + e));
        _player.StateChanged += OnPlayerStateChanged;
        _player.Ended += OnPlayerEnded;
        _player.Failed += message => Failed?.Invoke(message);
    }

    /// <summary>Titles in the library; the library page keeps it current for the Collector trophy.</summary>
    public int LibraryCount { get; set; }

    public Audiobook? Book { get; private set; }
    public bool IsPlaying => _player.IsPlaying;

    /// <summary>Raised on the main thread whenever position, chapter, state or book changed.</summary>
    public event Action? Changed;

    public event Action<string>? Failed;

    public ChapterInfo? CurrentChapter => Book is { } book
        ? book.Chapters.FirstOrDefault(chapter => chapter.Index == book.CurrentChapterIndex) ?? book.Chapters.FirstOrDefault()
        : null;

    private double CurrentFileMs => Book is { } book && book.SourcePaths.Count > 0
        ? book.SourcePaths[Math.Clamp(book.CurrentSourceFileIndex, 0, book.SourcePaths.Count - 1)].CurrentTimeMs
        : 0;

    private List<double> FileDurations => Book?.SourcePaths.Select(file => (double)file.Duration).ToList() ?? [];

    /// <summary>Position in the whole book, in audio milliseconds.</summary>
    public double BookPositionMs => Book is { } book
        ? PlayerProgress.BookPositionMs(FileDurations, book.CurrentSourceFileIndex, CurrentFileMs)
        : 0;

    public double BookDurationMs => (Book?.Duration ?? 0) * 1000.0;

    /// <summary>Position inside the current chapter, in audio milliseconds.</summary>
    public double ChapterPositionMs => CurrentChapter is { } chapter ? Math.Max(0, CurrentFileMs - chapter.StartTime) : 0;

    public double ChapterDurationMs => CurrentChapter is { } chapter && chapter.EndTime > chapter.StartTime
        ? chapter.EndTime - chapter.StartTime
        : 0;

    /// <summary>When the sleep timer pauses playback, or null when it is off.</summary>
    public DateTime? SleepTimerEnd { get; private set; }

    public void SetSleepTimer(TimeSpan? duration)
    {
        SleepTimerEnd = duration is { } time ? DateTime.Now + time : null;
        Changed?.Invoke();
    }

    public async Task OpenAsync(Audiobook book, bool play)
    {
        if (Book != null && Book.Id != book.Id)
        {
            _stats.OnInterrupted();
            Book.IsNowPlaying = false;
            await SaveAsync();
        }

        Book = book;
        book.IsNowPlaying = true;
        book.DateLastPlayed = DateTime.Now;
        if (book.PlaybackSpeed <= 0) book.PlaybackSpeed = 1;

        var index = Math.Clamp(book.CurrentSourceFileIndex, 0, Math.Max(0, book.SourcePaths.Count - 1));
        book.CurrentSourceFileIndex = index;
        var items = book.SourcePaths
            .Select(file => new PlaybackItem(file.FilePath, book.Title ?? string.Empty, book.Author ?? string.Empty,
                book.CoverImagePath))
            .ToList();
        await _player.OpenAsync(items, index, CurrentFileMs, book.PlaybackSpeed, play);
        EnsureTimer();
        await SaveAsync();
        Changed?.Invoke();
    }

    public void PlayPause()
    {
        if (Book == null) return;
        if (_player.IsPlaying) _player.Pause();
        else _player.Play();
    }

    public void Pause() => _player.Pause();

    /// <summary>Skips forward or back in the book, across files, stopping at its start and end.</summary>
    public void Skip(double seconds)
    {
        if (Book is not { } book) return;
        if (PlaybackPosition.Skip(FileDurations, book.CurrentSourceFileIndex, CurrentFileMs, seconds) is { } target)
            SeekTo(target.SourceIndex, target.PositionMs);
    }

    public void SeekBook(double bookPositionMs)
    {
        if (Book == null) return;
        var (sourceIndex, positionMs) = PlayerProgress.Locate(FileDurations, bookPositionMs);
        SeekTo(sourceIndex, positionMs);
    }

    /// <summary>Seeks to a position inside the current chapter.</summary>
    public void SeekChapter(double chapterPositionMs)
    {
        if (CurrentChapter is { } chapter)
            SeekTo(chapter.ParentSourceFileIndex, chapter.StartTime + Math.Max(0, chapterPositionMs));
    }

    public void GoToChapter(ChapterInfo chapter) => SeekTo(chapter.ParentSourceFileIndex, chapter.StartTime);

    public void NextChapter()
    {
        if (Book is { } book && PlaybackPosition.Neighbor(book.Chapters, book.CurrentChapterIndex ?? 0, 1) is { } next)
            GoToChapter(next);
    }

    /// <summary>Back to the start of the chapter, or to the previous chapter when already near its start.</summary>
    public void PreviousChapter()
    {
        if (Book is not { } book || CurrentChapter is not { } chapter) return;
        var intoChapterMs = CurrentFileMs - chapter.StartTime;
        var target = intoChapterMs > 3000
            ? chapter
            : PlaybackPosition.Neighbor(book.Chapters, chapter.Index, -1) ?? chapter;
        GoToChapter(target);
    }

    public void SetSpeed(double speed)
    {
        if (Book is not { } book) return;
        book.PlaybackSpeed = speed;
        _player.SetSpeed(speed);
        _ = SaveAsync();
        Changed?.Invoke();
    }

    /// <summary>Call when the app goes to the background or is about to close.</summary>
    public async Task FlushAsync()
    {
        ReadPosition();
        await SaveAsync();
        await _stats.FlushAsync();
    }

    private void SeekTo(int sourceIndex, double positionMs)
    {
        if (Book is not { } book) return;
        // a jump is not listening time
        _stats.OnInterrupted();
        _player.Seek(sourceIndex, positionMs);
        Apply(book, sourceIndex, positionMs);
        Changed?.Invoke();
    }

    private void EnsureTimer()
    {
        if (_timer != null) return;
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TickInterval;
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();
    }

    private void OnTick()
    {
        if (Book is not { } book) return;
        if (!_player.IsPlaying) return;

        var wasCompleted = book.IsCompleted;
        ReadPosition();
        _stats.OnPosition(book.Id, book.CurrentSourceFileIndex, CurrentFileMs / 1000, book.PlaybackSpeed);
        // only reaching the end while listening counts as finishing, not a jump to the end
        if (!wasCompleted && book.IsCompleted) _ = _stats.OnCompletedAsync(book.Id);

        if (SleepTimerEnd is { } end && DateTime.Now >= end)
        {
            SleepTimerEnd = null;
            _player.Pause();
        }

        var now = DateTime.UtcNow;
        if (now - _lastSave >= SaveInterval) _ = SaveAsync();
        if (now - _lastStatsFlush >= TimeSpan.FromSeconds(30))
        {
            _lastStatsFlush = now;
            _ = _stats.FlushAsync();
        }

        Changed?.Invoke();
    }

    private void ReadPosition()
    {
        if (Book is { } book && book.SourcePaths.Count > 0) Apply(book, _player.SourceIndex, _player.PositionMs);
    }

    private static void Apply(Audiobook book, int sourceIndex, double positionMs)
    {
        sourceIndex = Math.Clamp(sourceIndex, 0, book.SourcePaths.Count - 1);
        book.CurrentSourceFileIndex = sourceIndex;
        book.SourcePaths[sourceIndex].CurrentTimeMs = (int)Math.Max(0, positionMs);
        if (PlaybackPosition.ChapterAt(book.Chapters, sourceIndex, positionMs) is { } chapter)
            book.CurrentChapterIndex = chapter.Index;

        if (book.Duration > 0)
        {
            var bookMs = PlayerProgress.BookPositionMs(book.SourcePaths.Select(file => (double)file.Duration),
                sourceIndex, positionMs);
            book.Progress = Math.Ceiling(bookMs / (book.Duration * 1000.0) * 100);
            // same rule as on Windows; jumping back into a finished book makes it unfinished again
            book.IsCompleted = book.Progress >= 99.9;
        }
    }

    private void OnPlayerStateChanged()
    {
        if (Book == null) return;
        ReadPosition();
        if (!_player.IsPlaying)
        {
            // pausing is the moment a listener may put the phone away: save now
            _stats.OnInterrupted();
            _ = SaveAsync();
            _ = _stats.FlushAsync();
        }

        Changed?.Invoke();
    }

    private async void OnPlayerEnded()
    {
        if (Book is not { } book) return;
        // the last tick before the end normally recorded the completion already
        var wasCompleted = book.IsCompleted;
        book.Progress = 100;
        book.IsCompleted = true;
        if (!wasCompleted) await _stats.OnCompletedAsync(book.Id);

        await SaveAsync();
        await _stats.FlushAsync();
        Changed?.Invoke();
    }

    private async Task SaveAsync()
    {
        if (Book is not { } book || _saving) return;
        _saving = true;
        _lastSave = DateTime.UtcNow;
        try
        {
            await _data.Repository.Audiobooks.UpsertAsync(book);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine("[Playback] save failed: " + e);
        }
        finally
        {
            _saving = false;
        }
    }
}
