// Author: rstewa · https://github.com/rstewa
// Created: 10/22/2024
// Updated: 10/24/2024

using Audibly.Models;
using Audibly.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Audibly.Repository.Sql;

public class SqlAudiobookRepository(AudiblyContext db) : IAudiobookRepository
{
    #region IAudiobookRepository Members

    public async Task<IEnumerable<Audiobook>> GetAsync()
    {
        return await db.Audiobooks
            .Include(x => x.SourcePaths.OrderBy(source => source.Index))
            .Include(x => x.Chapters.OrderBy(chapter => chapter.Index))
            .OrderBy(audiobook => audiobook.Title)
            // .AsNoTracking()  // todo: testing this out
            .ToListAsync();
    }

    public Task<Audiobook?> GetNowPlayingAsync()
    {
        // return now playing audiobook
        return db.Audiobooks
            .Include(x => x.SourcePaths.OrderBy(source => source.Index))
            .Include(x => x.Chapters.OrderBy(chapter => chapter.Index))
            .AsNoTracking()
            .FirstOrDefaultAsync(audiobook => audiobook.IsNowPlaying);
    }

    public Task<Audiobook?> GetByTitleAuthorComposerAsync(string title, string author, string composer)
    {
        return db.Audiobooks
            .Include(x => x.SourcePaths.OrderBy(source => source.Index))
            .Include(x => x.Chapters.OrderBy(chapter => chapter.Index))
            .AsNoTracking()
            .FirstOrDefaultAsync(audiobook =>
                audiobook.Title == title &&
                audiobook.Author == author &&
                audiobook.Composer == composer);
    }

    // get async using filepath
    public async Task<Audiobook?> GetByFilePathAsync(string filePath)
    {
        return await db.Audiobooks
            .Include(x => x.SourcePaths.OrderBy(source => source.Index))
            .Include(x => x.Chapters.OrderBy(chapter => chapter.Index))
            .AsNoTracking()
            .FirstOrDefaultAsync(audiobook =>
                audiobook.SourcePaths.Any(source => source.FilePath == filePath));
    }

    public async Task<Audiobook?> GetAsync(Guid id)
    {
        return await db.Audiobooks
            .Include(x => x.SourcePaths.OrderBy(source => source.Index))
            .Include(x => x.Chapters.OrderBy(chapter => chapter.Index))
            .AsNoTracking()
            .FirstOrDefaultAsync(audiobook => audiobook.Id == id);
    }

    public async Task<IEnumerable<Audiobook>> GetAsync(string search)
    {
        var parameters = search.Split(' ');
        return await db.Audiobooks
            .Include(x => x.SourcePaths.OrderBy(source => source.Index))
            .Include(x => x.Chapters.OrderBy(chapter => chapter.Index))
            .Where(audiobook =>
                parameters.Any(parameter =>
                    audiobook.Author.StartsWith(parameter) ||
                    audiobook.Title.StartsWith(parameter) ||
                    audiobook.Description.StartsWith(parameter)))
            .OrderByDescending(audiobook =>
                parameters.Count(parameter =>
                    audiobook.Author.StartsWith(parameter) ||
                    audiobook.Title.StartsWith(parameter) ||
                    audiobook.Description.StartsWith(parameter)))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task SavePlaybackStateAsync(AudiobookPlaybackState state)
    {
        await db.Audiobooks
            .Where(audiobook => audiobook.Id == state.AudiobookId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(audiobook => audiobook.CurrentSourceFileIndex, state.CurrentSourceFileIndex)
                .SetProperty(audiobook => audiobook.CurrentChapterIndex, state.CurrentChapterIndex)
                .SetProperty(audiobook => audiobook.Progress, state.Progress)
                .SetProperty(audiobook => audiobook.IsCompleted, state.IsCompleted)
                .SetProperty(audiobook => audiobook.DateLastPlayed, state.DateLastPlayed)
                .SetProperty(audiobook => audiobook.PlaybackSpeed, state.PlaybackSpeed)
                .SetProperty(audiobook => audiobook.Volume, state.Volume)
                .SetProperty(audiobook => audiobook.IsNowPlaying, state.IsNowPlaying));

        await db.SourceFiles
            .Where(file => file.AudiobookId == state.AudiobookId && file.Index == state.CurrentSourceFileIndex)
            .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.CurrentTimeMs, state.CurrentTimeMs));
    }

    public async Task RenameAsync(Guid audiobookId, string title) =>
        await db.Audiobooks
            .Where(audiobook => audiobook.Id == audiobookId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(audiobook => audiobook.Title, title));

    public async Task SetTagsAsync(IReadOnlyDictionary<Guid, string?> tagsByBook)
    {
        foreach (var (id, tags) in tagsByBook)
            await db.Audiobooks
                .Where(audiobook => audiobook.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(audiobook => audiobook.Tags, tags));
    }

    public async Task SetCoverAsync(Guid audiobookId, string coverImagePath, string thumbnailPath) =>
        await db.Audiobooks
            .Where(audiobook => audiobook.Id == audiobookId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(audiobook => audiobook.CoverImagePath, coverImagePath)
                .SetProperty(audiobook => audiobook.ThumbnailPath, thumbnailPath));

    public async Task SaveMetadataAsync(Guid audiobookId, BookMetadata metadata) =>
        await db.Audiobooks
            .Where(audiobook => audiobook.Id == audiobookId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(audiobook => audiobook.Description, metadata.Description ?? string.Empty)
                .SetProperty(audiobook => audiobook.Narrator, metadata.Narrator)
                .SetProperty(audiobook => audiobook.Series, metadata.Series)
                .SetProperty(audiobook => audiobook.SeriesPart, metadata.SeriesPart)
                .SetProperty(audiobook => audiobook.Genres, metadata.Genres)
                .SetProperty(audiobook => audiobook.ReleaseDate, metadata.ReleaseDate)
                .SetProperty(audiobook => audiobook.Asin, metadata.Asin)
                .SetProperty(audiobook => audiobook.Title,
                    audiobook => string.IsNullOrWhiteSpace(metadata.Title) ? audiobook.Title : metadata.Title)
                .SetProperty(audiobook => audiobook.Tags,
                    audiobook => metadata.Tags == null ? audiobook.Tags : metadata.Tags));

    public async Task<Audiobook?> UpsertAsync(Audiobook audiobook)
    {
        var current = await db.Audiobooks
            .Include(x => x.SourcePaths.OrderBy(source => source.Index))
            .Include(x => x.Chapters.OrderBy(chapter => chapter.Index))
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Title == audiobook.Title && a.Author == audiobook.Author);

        // TODO: fix this bug
        if (current != null && current.Id != audiobook.Id)
            return audiobook;

        if (current == null)
            db.Audiobooks.Add(audiobook);
        else
            db.Audiobooks.Update(audiobook);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            return null;
        }

        return audiobook;
    }

    public async Task DeleteAsync(Guid audiobookId)
    {
        var audiobook = await db.Audiobooks
            .Include(x => x.SourcePaths)
            .Include(x => x.Chapters)
            .FirstOrDefaultAsync(a => a.Id == audiobookId);

        if (audiobook != null) db.Remove(audiobook);

        try
        {
            await db.SaveChangesAsync();
        }
        // this is for a really annoying edge case I was running into for some reason
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
                if (entry.Entity is Audiobook || entry.Entity is SourceFile || entry.Entity is ChapterInfo)
                {
                    var proposedValues = entry.CurrentValues;
                    var databaseValues = entry.GetDatabaseValues();

                    if (databaseValues == null)
                    {
                        // The entity was deleted by another process
                        db.Entry(entry.Entity).State = EntityState.Detached;
                    }
                    else
                    {
                        foreach (var property in proposedValues.Properties)
                        {
                            var proposedValue = proposedValues[property];
                            var databaseValue = databaseValues[property];

                            // Decide which value should be written to database
                            proposedValues[property] = proposedValue;
                        }

                        // Refresh original values to bypass next concurrency check
                        entry.OriginalValues.SetValues(databaseValues);
                    }
                }
                else
                {
                    throw new NotSupportedException(
                        "Don't know how to handle concurrency conflicts for "
                        + entry.Metadata.Name);
                }

            // Retry the save operation
            await db.SaveChangesAsync();
        }
    }

    public async Task DeleteAllAsync(Func<int, int, string, string, Task> progressCallback)
    {
        var audiobooks = db.Audiobooks
            .Include(x => x.SourcePaths)
            .Include(x => x.Chapters)
            .ToList();
        
        for (var i = 0; i < audiobooks.Count; i++) 
        {
            db.Remove(audiobooks[i]);
            await progressCallback(i, audiobooks.Count, audiobooks[i].Title, audiobooks[i].CoverImagePath);
        }

        await db.SaveChangesAsync();
    }

    #endregion
}