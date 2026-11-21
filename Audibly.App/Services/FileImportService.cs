// Author: rstewa · https://github.com/rstewa
// Created: 04/15/2024
// Updated: 10/17/2024

using Audibly.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using ATL;
using Audibly.App.Extensions;
using Audibly.App.Helpers;
using Audibly.App.Services.Interfaces;
using Audibly.App.ViewModels;
using Audibly.Core.Import;
using Audibly.Models;
using AutoMapper;
using Microsoft.UI.Xaml.Controls;
using Sharpener.Extensions;
using ChapterInfo = Audibly.Models.ChapterInfo;

namespace Audibly.App.Services;

public class FileImportService : IImportFiles
{
    private static IMapper _mapper;

    public FileImportService()
    {
        _mapper = new MapperConfiguration(cfg => { cfg.CreateMap<ATL.ChapterInfo, ChapterInfo>(); }).CreateMapper();
    }

    #region IImportFiles Members

    public event IImportFiles.ImportCompletedHandler? ImportCompleted;

    public async Task ImportDirectoryAsync(string path, CancellationToken cancellationToken,
        Func<int, int, string, BookImportStatus, Task> progressCallback)
    {
        var books = await Task.Run(() => AudiobookImporter.ScanDirectory(path, cancellationToken), cancellationToken);
        for (var index = 0; index < books.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files = books[index];
            var folderName = files.Count > 1 ? Path.GetFileName(Path.GetDirectoryName(files[0])) : null;
            var (title, status) = await ImportBookAsync(files, cancellationToken, folderName);

            await progressCallback(index + 1, books.Count, title, status);
        }

        ImportCompleted?.Invoke();
    }

    public async Task ImportFromJsonAsync(StorageFile file, CancellationToken cancellationToken,
        Func<int, int, string, bool, Task> progressCallback)
    {
        // read the json string from the file
        var json = FileIO.ReadTextAsync(file).AsTask().Result;

        if (string.IsNullOrEmpty(json))
        {
            // log the error
            App.ViewModel.LoggingService.LogError(new Exception("Failed to read the json file"), true);
            ImportCompleted?.Invoke();
            return;
        }

        // deserialize the json string to a list of audiobooks
        var importedAudiobooks = JsonSerializer.Deserialize<List<ImportedAudiobook>>(json);

        if (importedAudiobooks == null)
        {
            // log the error
            App.ViewModel.LoggingService.LogError(new Exception("Failed to deserialize the json file"), true);
            return;
        }

        var didFail = false;
        var numberOfFiles = importedAudiobooks.Count;

        foreach (var importedAudiobook in importedAudiobooks)
        {
            // Check if cancellation was requested
            cancellationToken.ThrowIfCancellationRequested();

            // verify that the audiobook file exists
            if (!File.Exists(importedAudiobook.FilePath))
            {
                // log the error
                App.ViewModel.LoggingService.LogError(new Exception("Audiobook file does not exist"));
                App.ViewModel.EnqueueNotification(new Notification
                {
                    Message = $"Audiobook file was moved or deleted: {importedAudiobook.FilePath}",
                    Severity = InfoBarSeverity.Warning
                });

                didFail = true;
                continue;
            }

            var audiobook = await CreateAudiobook(importedAudiobook.FilePath, importedAudiobook);

            if (audiobook == null)
            {
                didFail = true;
            }
            else
            {
                // insert the audiobook into the database
                var result = await App.Repository.Audiobooks.UpsertAsync(audiobook);
                if (result == null) didFail = true;
            }

            var title = audiobook?.Title ?? Path.GetFileNameWithoutExtension(importedAudiobook.FilePath);

            // report progress
            await progressCallback(importedAudiobooks.IndexOf(importedAudiobook), numberOfFiles, title, didFail);

            didFail = false;
        }

        ImportCompleted?.Invoke();
    }

    public async Task ImportFromMultipleFilesAsync(string[] paths, CancellationToken cancellationToken,
        Func<int, int, string, bool, Task> progressCallback)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (title, status) = await ImportBookAsync(paths, cancellationToken);
        if (status == BookImportStatus.AlreadyInLibrary)
            App.ViewModel.EnqueueNotification(new Notification
            {
                Message = $"Audiobook is already in the library: {title}",
                Severity = InfoBarSeverity.Warning
            });

        await progressCallback(1, 1, title, status != BookImportStatus.Imported);
        ImportCompleted?.Invoke();
    }

    public async Task ImportFileAsync(string path, CancellationToken cancellationToken,
        Func<int, int, string, bool, Task> progressCallback)
    {
        // Check if cancellation was requested
        cancellationToken.ThrowIfCancellationRequested();

        var didFail = false;
        var audiobook = await CreateAudiobook(path);

        if (audiobook == null) didFail = true;

        // insert the audiobook into the database
        if (audiobook != null)
        {
            var result = await App.Repository.Audiobooks.UpsertAsync(audiobook);
            if (result == null) didFail = true;
        }

        var title = audiobook?.Title ?? Path.GetFileNameWithoutExtension(path);

        // report progress
        // NOTE: keeping this bc this function will be used in the future to import 1-to-many files
        await progressCallback(1, 1, title, didFail);

        ImportCompleted?.Invoke();
    }

    #endregion

    private static async Task<(string Title, BookImportStatus Status)> ImportBookAsync(
        IReadOnlyList<string> paths, CancellationToken cancellationToken, string? folderName = null)
    {
        var fallbackTitle = folderName ?? Path.GetFileNameWithoutExtension(paths[0]);
        var repository = App.Repository.Audiobooks;

        // Check the path first because "Look up online" can change the book's title after import.
        var existing = await repository.GetByFilePathAsync(paths[0]);
        if (existing != null) return (existing.Title, BookImportStatus.AlreadyInLibrary);

        ImportedBook imported;
        try
        {
            imported = await Task.Run(() => ReadAudiobook(paths, folderName, cancellationToken), cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            App.ViewModel.LoggingService.LogError(e, true);
            return (fallbackTitle, BookImportStatus.Failed);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var audiobook = imported.Audiobook;
        existing = await repository.GetByTitleAuthorComposerAsync(audiobook.Title, audiobook.Author, audiobook.Composer);
        if (existing != null) return (existing.Title, BookImportStatus.AlreadyInLibrary);

        var hash = $"{audiobook.Title}{audiobook.Author}{audiobook.Composer}".GetSha256Hash();
        (audiobook.CoverImagePath, audiobook.ThumbnailPath) =
            await App.ViewModel.AppDataService.WriteCoverImageAsync(hash, imported.CoverImage);
        cancellationToken.ThrowIfCancellationRequested();

        return (audiobook.Title, await SaveImportedBookAsync(audiobook)
            ? BookImportStatus.Imported : BookImportStatus.Failed);
    }

    private static async Task<bool> SaveImportedBookAsync(Audiobook audiobook)
    {
        var repository = App.Repository.Audiobooks;
        var saved = await repository.UpsertAsync(audiobook);
        // Upsert can also return the input when an existing book has the same title and author.
        return saved != null && (await repository.GetByFilePathAsync(audiobook.SourcePaths[0].FilePath))?.Id == audiobook.Id;
    }

    private static ImportedBook ReadAudiobook(IReadOnlyList<string> paths, string? folderName,
        CancellationToken cancellationToken)
    {
        var audiobook = new Audiobook
        {
            CurrentSourceFileIndex = 0,
            SourcePaths = [],
            PlaybackSpeed = 1.0,
            Volume = 1.0,
            IsCompleted = false
        };

        byte[]? imageBytes = null;
        var sourceFileIndex = 0;
        var chapterIndex = 0;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var track = new Track(path);
            var trackTitle = string.IsNullOrWhiteSpace(track.Title) ? Path.GetFileNameWithoutExtension(path) : track.Title;

            // check if this is the 1st file
            if (audiobook.SourcePaths.Count == 0)
            {
                // in a book of several files the track title is usually the first chapter's
                // ("Kapitel 1"); the album names the book
                audiobook.Title = paths.Count > 1 && !string.IsNullOrWhiteSpace(track.Album)
                    ? track.Album
                    : folderName ?? trackTitle;
                audiobook.Composer = track.Composer;
                audiobook.Author = track.Artist;
                audiobook.Description =
                    track.Description.IsNullOrEmpty()
                        ? track.Comment.IsNullOrEmpty()
                            ? track.AdditionalFields.TryGetValue("\u00A9des", out var value) ? value : track.Comment
                            : track.Comment
                        : track.Description;
                audiobook.ReleaseDate = track.Date;

                // Read the cover from the first file.
                imageBytes = track.EmbeddedPictures.FirstOrDefault()?.PictureData;
            }

            var sourceFile = new SourceFile
            {
                Index = sourceFileIndex++,
                FilePath = path,
                Duration = track.Duration,
                CurrentTimeMs = 0
            };

            audiobook.SourcePaths.Add(sourceFile);

            // read in the chapters
            foreach (var ch in track.Chapters)
            {
                var tmp = _mapper.Map<ChapterInfo>(ch);
                tmp.Index = chapterIndex++;
                tmp.ParentSourceFileIndex = sourceFile.Index;
                audiobook.Chapters.Add(tmp);
            }

            if (track.Chapters.Count == 0)
                // create a single chapter for the entire book
                audiobook.Chapters.Add(new ChapterInfo
                {
                    StartTime = 0,
                    EndTime = Convert.ToUInt32(audiobook.SourcePaths[sourceFileIndex - 1].Duration * 1000),
                    StartOffset = 0,
                    EndOffset = 0,
                    UseOffset = false,
                    Title = trackTitle,
                    Index = chapterIndex++,
                    ParentSourceFileIndex = sourceFile.Index
                });
        }

        // files that share one title tag would all show the same chapter name
        var chapterTitles = FileChapterTitles.Resolve(
            audiobook.Chapters.Select(chapter => (chapter.ParentSourceFileIndex, (string?)chapter.Title)).ToList(),
            paths);
        for (var i = 0; i < audiobook.Chapters.Count; i++) audiobook.Chapters[i].Title = chapterTitles[i];

        // get duration of the entire audiobook
        audiobook.Duration = audiobook.SourcePaths.Sum(x => x.Duration);

        // combine the chapters from all the files
        // audiobook.Chapters = audiobook.SourcePaths.SelectMany(x => x.Chapters).ToList();
        audiobook.CurrentChapterIndex = 0;

        return new ImportedBook(audiobook, imageBytes);
    }

    private static async Task<Audiobook?> CreateAudiobook(string path, ImportedAudiobook? importedAudiobook = null, bool notifyUser = true)
    {
        try
        {
            var track = new Track(path);

            // by file first: a book renamed with "Look up online" no longer matches its file tags
            var existingAudioBook =
                await App.Repository.Audiobooks.GetByFilePathAsync(path) ??
                await App.Repository.Audiobooks.GetByTitleAuthorComposerAsync(track.Title, track.Artist,
                    track.Composer);
            if (existingAudioBook != null)
            {
                // log the error
                App.ViewModel.LoggingService.LogError(new Exception("Audiobook already exists in the database"));
                if (notifyUser)
                {
                    App.ViewModel.EnqueueNotification(new Notification
                    {
                        Message = "Audiobook is already in the library.",
                        Severity = InfoBarSeverity.Warning
                    });
                }
                return null;
            }

            var sourceFile = new SourceFile
            {
                Index = 0,
                FilePath = path,
                Duration = track.Duration,
                CurrentTimeMs = importedAudiobook?.CurrentTimeMs ?? 0
                // CurrentChapterIndex = 0,
                // Chapters = []
            };

            var audiobook = new Audiobook
            {
                CurrentSourceFileIndex = 0,
                Title = track.Title,
                Composer = track.Composer,
                CurrentChapterIndex = importedAudiobook?.CurrentChapterIndex ?? 0,
                Duration = track.Duration,
                Author = track.Artist,
                Description =
                    track.Description.IsNullOrEmpty()
                        ? track.Comment.IsNullOrEmpty()
                            ? track.AdditionalFields.TryGetValue("\u00A9des", out var value) ? value : track.Comment
                            : track.Comment
                        : track.Description,
                PlaybackSpeed = 1.0,
                Progress = importedAudiobook?.Progress ?? 0,
                ReleaseDate = track.Date,
                Volume = 1.0,
                IsCompleted = importedAudiobook?.IsCompleted ?? false,
                IsNowPlaying = importedAudiobook?.IsNowPlaying ?? false,
                SourcePaths =
                [
                    sourceFile
                ]
            };

            // TODO: check if the audiobook already exists in the database

            // save the cover image somewhere
            var imageBytes = track.EmbeddedPictures.FirstOrDefault()?.PictureData;

            // generate hash from title, author, and composer
            var hash = $"{audiobook.Title}{audiobook.Author}{audiobook.Composer}".GetSha256Hash();

            // write the metadata to a json file
            // todo: is this killing the import time?
            // await App.ViewModel.AppDataService.WriteMetadataAsync(hash, track);

            (audiobook.CoverImagePath, audiobook.ThumbnailPath) =
                await App.ViewModel.AppDataService.WriteCoverImageAsync(hash, imageBytes);

            // var chapters = audiobook.SourcePaths.First().Chapters;

            // read in the chapters
            var chapterIndex = 0;
            foreach (var ch in track.Chapters)
            {
                var tmp = _mapper.Map<ChapterInfo>(ch);
                tmp.Index = chapterIndex++;
                tmp.ParentSourceFileIndex = sourceFile.Index;
                audiobook.Chapters.Add(tmp);
            }

            if (audiobook.Chapters.Count == 0)
                // create a single chapter for the entire book
                audiobook.Chapters.Add(new ChapterInfo
                {
                    StartTime = 0,
                    EndTime = Convert.ToUInt32(audiobook.SourcePaths.First().Duration * 1000),
                    StartOffset = 0,
                    EndOffset = 0,
                    UseOffset = false,
                    Title = audiobook.Title,
                    Index = 0,
                    ParentSourceFileIndex = sourceFile.Index
                });

            return audiobook;
        }
        catch (Exception e)
        {
            // log the error
            App.ViewModel.LoggingService.LogError(e, true);
            return null;
        }
    }
}
