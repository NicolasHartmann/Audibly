using Audibly.Models;
using Audibly.Repository.Sql;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Audibly.Audio.Tests.Metadata;

public sealed class SaveMetadataTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AudiblyContext> _options;
    private readonly Guid _bookId;

    public SaveMetadataTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AudiblyContext>().UseSqlite(_connection).Options;
        using var db = new AudiblyContext(_options);
        db.Database.Migrate();

        var book = new Audiobook
        {
            Title = "T", Author = "A", Composer = "C", Description = "old", CoverImagePath = "", ThumbnailPath = "",
            SourcePaths = [new SourceFile { FilePath = "f.mp3", Index = 0 }],
            Chapters = [new ChapterInfo { Title = "Ch 1", Index = 0 }]
        };
        db.Audiobooks.Add(book);
        db.SaveChanges();
        _bookId = book.Id;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task SaveMetadata_writes_only_book_details()
    {
        await using (var db = new AudiblyContext(_options))
            await new SqlAudiobookRepository(db).SaveMetadataAsync(_bookId, new BookMetadata(
                "new", "N", "S", "2", "Fantasy\nEpic Fantasy", new DateTime(2010, 11, 4), "B004IZ4WFW"));

        await using var check = new AudiblyContext(_options);
        var book = await check.Audiobooks.Include(b => b.SourcePaths).Include(b => b.Chapters).SingleAsync();
        Assert.Equal("new", book.Description);
        Assert.Equal("N", book.Narrator);
        Assert.Equal("S", book.Series);
        Assert.Equal("2", book.SeriesPart);
        Assert.Equal("Fantasy\nEpic Fantasy", book.Genres);
        Assert.Equal(new DateTime(2010, 11, 4), book.ReleaseDate);
        Assert.Equal("B004IZ4WFW", book.Asin);
        Assert.Equal("C", book.Composer);
        Assert.Equal("T", book.Title); // no title given: the title stays
        Assert.Single(book.SourcePaths);
        Assert.Single(book.Chapters);
    }

    [Fact]
    public async Task SaveMetadata_renames_when_title_given()
    {
        await using (var db = new AudiblyContext(_options))
            await new SqlAudiobookRepository(db).SaveMetadataAsync(_bookId,
                new BookMetadata("old", null, null, null, null, null, null, "New title"));

        await using var check = new AudiblyContext(_options);
        var book = await check.Audiobooks.SingleAsync();
        Assert.Equal("New title", book.Title);
        Assert.Equal("C", book.Composer);
    }

    [Fact]
    public async Task SetCover_writes_only_the_cover_paths()
    {
        await using (var db = new AudiblyContext(_options))
            await new SqlAudiobookRepository(db).SetCoverAsync(_bookId, @"C:\covers\CoverImage.png", @"C:\covers\Thumbnail.jpeg");

        await using var check = new AudiblyContext(_options);
        var book = await check.Audiobooks.SingleAsync();
        Assert.Equal(@"C:\covers\CoverImage.png", book.CoverImagePath);
        Assert.Equal(@"C:\covers\Thumbnail.jpeg", book.ThumbnailPath);
        Assert.Equal("T", book.Title);
        Assert.Equal("old", book.Description);
    }

    [Fact]
    public async Task SetTags_changes_only_given_books()
    {
        Guid other;
        await using (var db = new AudiblyContext(_options))
        {
            var book = new Audiobook
            {
                Title = "T2", Author = "A", Composer = "C", Description = "", CoverImagePath = "", ThumbnailPath = "",
                Tags = "Keep", SourcePaths = [], Chapters = []
            };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();
            other = book.Id;
        }

        await using (var db = new AudiblyContext(_options))
            await new SqlAudiobookRepository(db).SetTagsAsync(new Dictionary<Guid, string?> { [_bookId] = "Krimi" });

        await using var check = new AudiblyContext(_options);
        Assert.Equal("Krimi", (await check.Audiobooks.SingleAsync(b => b.Id == _bookId)).Tags);
        Assert.Equal("Keep", (await check.Audiobooks.SingleAsync(b => b.Id == other)).Tags);
    }

    [Fact]
    public async Task SaveMetadata_writes_tags()
    {
        await using (var db = new AudiblyContext(_options))
            await new SqlAudiobookRepository(db).SaveMetadataAsync(_bookId,
                new BookMetadata("old", null, null, null, null, null, null, Tags: "A\nB"));

        await using var check = new AudiblyContext(_options);
        Assert.Equal("A\nB", (await check.Audiobooks.SingleAsync()).Tags);
    }

    [Fact]
    public void Migration_copies_genres_to_tags()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AudiblyContext>().UseSqlite(connection).Options;
        using var db = new AudiblyContext(options);
        db.GetService<IMigrator>().Migrate("20261119174948_AddBookDetails");
        db.Database.ExecuteSqlRaw(
            "INSERT INTO Audiobooks (Id, Author, Composer, CurrentSourceFileIndex, Description, Duration, CoverImagePath, " +
            "ThumbnailPath, IsNowPlaying, PlaybackSpeed, Progress, Title, Volume, IsCompleted, Genres) VALUES " +
            "('8f9a5d1e-0000-0000-0000-000000000001', 'A', 'C', 0, '', 0, '', '', 0, 1, 0, 'T', 1, 0, 'Fantasy\nEpic')");

        db.Database.Migrate();

        Assert.Equal("Fantasy\nEpic", db.Audiobooks.AsNoTracking().Single().Tags);
    }

    [Fact]
    public async Task Migration_keeps_existing_books()
    {
        await using var check = new AudiblyContext(_options);
        var book = await check.Audiobooks.SingleAsync();

        Assert.Null(book.Narrator);
        Assert.Null(book.Series);
        Assert.Null(book.SeriesPart);
        Assert.Null(book.Genres);
        Assert.Null(book.Asin);
        Assert.Equal("C", book.Composer);
        Assert.Equal("old", book.Description);
    }
}
