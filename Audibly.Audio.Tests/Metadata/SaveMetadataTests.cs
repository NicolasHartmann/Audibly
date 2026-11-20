using Audibly.Models;
using Audibly.Repository.Sql;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
