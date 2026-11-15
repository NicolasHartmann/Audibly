using Audibly.Models;
using Audibly.Repository.Interfaces;
using Audibly.Repository.Sql;
using Audibly.Stats;
using Microsoft.EntityFrameworkCore;

namespace Audibly.Mobile.Services;

/// <summary>
///     The app's data in its private storage: the same library database and statistics file as on
///     Windows. Created once at start.
/// </summary>
public sealed class AppData
{
    private readonly DbContextOptions<AudiblyContext> _options;
    private readonly SemaphoreSlim _saving = new(1, 1);

    public AppData(string folder)
    {
        Directory.CreateDirectory(folder);
        DatabasePath = Path.Combine(folder, "Audibly.db");
        StatsPath = Path.Combine(folder, "stats.db");

        var options = new DbContextOptionsBuilder<AudiblyContext>()
            .UseSqlite("Data Source=" + DatabasePath)
            .Options;
        using (var context = new AudiblyContext(options))
        {
            // the migrations are the ones the Windows app applies, so both platforms share one schema
            context.Database.Migrate();
        }

        _options = options;
        Repository = new SqlAudiblyRepository(options);
        Stats = new StatsStore(StatsPath);
    }

    /// <summary>
    ///     The whole library, read on a background thread. The database calls are synchronous
    ///     underneath, so calling the repository directly from a page would freeze it for as long
    ///     as the query takes.
    /// </summary>
    public Task<List<Audiobook>> LoadBooksAsync() =>
        Task.Run(async () => (await Repository.Audiobooks.GetAsync()).ToList());

    /// <summary>
    ///     Saves where a book stands, on a background thread and without touching its chapters.
    ///     Saving the whole book wrote every chapter again each time, on the thread that draws the
    ///     app, which made it stutter every few seconds.
    /// </summary>
    public Task SavePositionAsync(BookPosition position) => Task.Run(async () =>
    {
        await _saving.WaitAsync();
        try
        {
            using var db = new AudiblyContext(_options);
            var book = db.Audiobooks.FirstOrDefault(candidate => candidate.Id == position.BookId);
            if (book == null) return;

            book.CurrentSourceFileIndex = position.SourceIndex;
            book.CurrentChapterIndex = position.ChapterIndex;
            book.Progress = position.Progress;
            book.IsCompleted = position.IsCompleted;
            book.PlaybackSpeed = position.Speed;
            book.DateLastPlayed = position.LastPlayed;
            book.IsNowPlaying = position.IsNowPlaying;
            foreach (var file in db.SourceFiles.Where(file => file.AudiobookId == position.BookId))
                if (position.FileTimesMs.TryGetValue(file.Index, out var timeMs))
                    file.CurrentTimeMs = timeMs;
            db.SaveChanges();
        }
        finally
        {
            _saving.Release();
        }
    });

    public string DatabasePath { get; }
    public string StatsPath { get; }
    public IAudiblyRepository Repository { get; }
    public StatsStore Stats { get; }
}

/// <summary>Where a book stands, copied out of the live object so it can be saved on another thread.</summary>
public sealed record BookPosition(Guid BookId, int SourceIndex, int? ChapterIndex, double Progress, bool IsCompleted,
    double Speed, DateTime? LastPlayed, bool IsNowPlaying, IReadOnlyDictionary<int, int> FileTimesMs)
{
    public static BookPosition Of(Audiobook book) => new(book.Id, book.CurrentSourceFileIndex,
        book.CurrentChapterIndex, book.Progress, book.IsCompleted, book.PlaybackSpeed, book.DateLastPlayed,
        book.IsNowPlaying, book.SourcePaths.GroupBy(file => file.Index).ToDictionary(g => g.Key, g => g.First().CurrentTimeMs));
}
