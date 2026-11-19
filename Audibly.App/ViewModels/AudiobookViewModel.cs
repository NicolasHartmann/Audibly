// Author: rstewa · https://github.com/rstewa
// Updated: 02/14/2025

using Audibly.Stats;
using System.Threading;
using Audibly.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Audibly.App.Extensions;
using Audibly.App.Helpers;
using Audibly.Models;
using ChapterInfo = Audibly.Models.ChapterInfo;

namespace Audibly.App.ViewModels;

/// <summary>
///     Provides a bindable wrapper for the Customer model class, encapsulating various services for access by the UI.
/// </summary>
public class AudiobookViewModel : BindableBase
{
    private string _volumeGlyph;

    /// <summary>
    ///     Initializes a new instance of the CustomerViewModel class that wraps a Customer object.
    /// </summary>
    public AudiobookViewModel(Audiobook? model = null)
    {
        Model = model ?? new Audiobook();

        if (model == null) return;

        // Books imported before file names were used still carry one shared title per file.
        var sourcePaths = model.SourcePaths.OrderBy(source => source.Index).Select(source => source.FilePath).ToList();
        var chapterTitles = FileChapterTitles.Resolve(
            model.Chapters.Select(chapter => (chapter.ParentSourceFileIndex, (string?)chapter.Title)).ToList(),
            sourcePaths);
        for (var i = 0; i < model.Chapters.Count; i++) model.Chapters[i].Title = chapterTitles[i];

        Chapters.Clear();
        foreach (var chapter in model.Chapters) Chapters.Add(chapter);

        // CurrentTimeMs = model.SourcePaths[model.CurrentSourceFileIndex].CurrentTimeMs;
    }

    /// <summary>
    ///     Gets the chapters of the audiobook.
    /// </summary>
    public ObservableCollection<ChapterInfo> Chapters { get; set; } = [];

    /// <summary>
    ///     Gets or sets the volume glyph of the audiobook.
    /// </summary>
    public string VolumeGlyph
    {
        get => _volumeGlyph;
        set => Set(ref _volumeGlyph, value);
    }

    /// <summary>
    ///     Gets or sets a value that indicates whether the underlying model has been modified.
    /// </summary>
    /// <remarks>
    ///     Used when sync'ing with the server to reduce load and only upload the models that have changed.
    /// </remarks>
    public bool IsModified { get; set; }

    // private bool _isNewAudiobook;
    //
    // /// <summary>
    // ///     Gets or sets a value that indicates whether this is a new audiobook.
    // /// </summary>
    // public bool IsNewAudiobook
    // {
    //     get => _isNewAudiobook;
    //     set => Set(ref _isNewAudiobook, value);
    // }

    /// <summary>
    ///     Saves audiobook data that has been edited.
    /// </summary>
    private static readonly SemaphoreSlim PlaybackStateSaving = new(1, 1);

    /// <summary>
    ///     Saves where the book stands (file, position, chapter, progress, speed, volume) on a
    ///     background thread, writing only two rows. <see cref="SaveAsync" /> writes the whole book
    ///     with every chapter and file, which takes seconds for a book of several hundred files and
    ///     froze the app when it ran on every position update.
    /// </summary>
    public async Task SavePlaybackStateAsync()
    {
        // read here, on the thread that changes these values; written elsewhere
        var file = CurrentSourceFile;
        var state = new AudiobookPlaybackState(Model.Id, Model.CurrentSourceFileIndex, file?.CurrentTimeMs ?? 0,
            Model.CurrentChapterIndex, Model.Progress, Model.IsCompleted, Model.DateLastPlayed, Model.PlaybackSpeed,
            Model.Volume, Model.IsNowPlaying);

        // one at a time and in order, so an older state never overwrites a newer one
        await PlaybackStateSaving.WaitAsync();
        try
        {
            await Task.Run(() => App.Repository.Audiobooks.SavePlaybackStateAsync(state));
        }
        catch (Exception e)
        {
            App.ViewModel.LoggingService.LogError(e);
        }
        finally
        {
            PlaybackStateSaving.Release();
        }
    }

    public async Task SaveAsync()
    {
        // if (IsNewAudiobook)
        // {
        //     IsNewAudiobook = false;
        //     App.ViewModel.Audiobooks.Add(this);
        // }

        if (IsModified)
            await App.Repository.Audiobooks.UpsertAsync(Model);

        IsModified = false;
    }


    #region model database properties

    private Audiobook _model;

    /// <summary>
    ///     Gets or sets the underlying Audiobook object.
    /// </summary>
    public Audiobook Model
    {
        get => _model;
        set
        {
            if (_model != value)
            {
                _model = value;

                // Raise the PropertyChanged event for all properties.
                IsModified = true;
                OnPropertyChanged(string.Empty);
            }
        }
    }

    /// <summary>
    ///     Gets or sets the date the audiobook was last played.
    /// </summary>
    public DateTime? DateLastPlayed
    {
        get => Model.DateLastPlayed;
        set
        {
            if (value != Model.DateLastPlayed)
            {
                Model.DateLastPlayed = value;
                IsModified = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LengthAtSpeedText));
            }
        }
    }

    // private int _currentTimeMs;

    /// <summary>
    ///     Gets or sets the current time in milliseconds of the audiobook.
    /// </summary>
    public int CurrentTimeMs
    {
        // get => _currentTimeMs;
        // set => Set(ref _currentTimeMs, value);
        get => CurrentSourceFile.CurrentTimeMs;
        set
        {
            if (value != CurrentSourceFile.CurrentTimeMs)
            {
                CurrentSourceFile.CurrentTimeMs = value;
                IsModified = true;
                OnPropertyChanged();

                // Task.Run(SaveAsync); // todo: should this be done here?
            }
        }
    }

    /// <summary>
    ///     Gets or sets whether the audiobook is currently playing or not.
    /// </summary>
    public bool IsNowPlaying
    {
        get => Model.IsNowPlaying;
        set
        {
            if (value == Model.IsNowPlaying) return;

            Model.IsNowPlaying = value;
            IsModified = true;
            OnPropertyChanged();

            // Task.Run(SaveAsync);
        }
    }

    /// <summary>
    ///     Gets or sets the playback speed of the audiobook.
    /// </summary>
    public double PlaybackSpeed
    {
        get => Model.PlaybackSpeed;
        set
        {
            if (value.Equals(Model.PlaybackSpeed)) return;

            Model.PlaybackSpeed = value;
            IsModified = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LengthAtSpeedText));
        }
    }

    /// <summary>
    ///     Gets or sets the progress of the audiobook.
    /// </summary>
    public double Progress
    {
        get => Model.Progress;
        set
        {
            if (value.Equals(Model.Progress)) return;

            Model.Progress = value;
            IsModified = true;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets the volume of the audiobook.
    /// </summary>
    public double Volume
    {
        get => Model.Volume;
        set
        {
            if (value.Equals(Model.Volume)) return;

            Model.Volume = value;
            VolumeGlyph = value switch
            {
                0 => "\uE992",
                < 0.5 => "\uE993",
                _ => "\uE994"
            };
            IsModified = true;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets the current chapter index of the audiobook.
    /// </summary>
    public int? CurrentChapterIndex
    {
        get => Model.CurrentChapterIndex;
        set
        {
            Model.CurrentChapterIndex = value;
            IsModified = true;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets the current source file index of the audiobook.
    /// </summary>
    public int CurrentSourceFileIndex
    {
        get => Model.CurrentSourceFileIndex;
        set
        {
            Model.CurrentSourceFileIndex = value;
            IsModified = true;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets or sets a value that indicates whether the audiobook has been completed.
    /// </summary>
    public bool IsCompleted
    {
        get => Model.IsCompleted;
        set
        {
            Model.IsCompleted = value;
            IsModified = true;
            OnPropertyChanged();
        }
    }

    #region read-only

    /// <summary>
    ///     Gets the unique identifier for the audiobook.
    /// </summary>
    public Guid Id => Model.Id;

    /// <summary>
    ///     Gets or sets the author of the audiobook.
    /// </summary>
    public string Author => Model.Author;

    /// <summary>
    ///     The narrator found online, otherwise the composer from the file tags.
    /// </summary>
    public string Narrator => string.IsNullOrWhiteSpace(Model.Narrator) ? Model.Composer : Model.Narrator;

    /// <summary>The series and the book's part in it, e.g. "Die Tribute von Panem 2".</summary>
    public string Series => $"{Model.Series} {Model.SeriesPart}".Trim();

    /// <summary>The genres, comma-separated.</summary>
    public string Genres => string.Join(", ",
        (Model.Genres ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>Takes over details saved by "Look up online" and updates what shows them.</summary>
    public void ApplyMetadata(BookMetadata metadata)
    {
        Model.Description = metadata.Description ?? string.Empty;
        Model.Narrator = metadata.Narrator;
        Model.Series = metadata.Series;
        Model.SeriesPart = metadata.SeriesPart;
        Model.Genres = metadata.Genres;
        Model.ReleaseDate = metadata.ReleaseDate;
        Model.Asin = metadata.Asin;
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Narrator));
        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(Genres));
        OnPropertyChanged(nameof(ReleaseDate));
    }

    /// <summary>
    ///     Gets or sets the description of the audiobook.
    /// </summary>
    public string Description => Model.Description;

    /// <summary>
    ///     Gets or sets the duration of the audiobook (seconds).
    /// </summary>
    public long Duration => Model.Duration;

    /// <summary>
    ///     Gets the duration of the audiobook as a string in the format "hh:mm:ss".
    /// </summary>
    public string DurationStr => CurrentSourceFile.Duration.ToStr_s();

    /// <summary>
    ///     Gets or sets the cover image path of the audiobook.
    /// </summary>
    public string CoverImagePath => Model.CoverImagePath;

    /// <summary>
    ///     Gets or sets the thumbnail path of the audiobook.
    /// </summary>
    public string ThumbnailPath =>
        Model.ThumbnailPath.Equals(string.Empty) ? Model.CoverImagePath : Model.ThumbnailPath;

    /// <summary>
    ///     Gets the title of the audiobook.
    /// </summary>
    public string Title
    {
        get => Model.Title;
        set
        {
            if (value == Model.Title) return;
            Model.Title = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     The speed this book plays at: its own once it has been listened to, the default speed for
    ///     a book that was never started (the player applies that speed when it is first opened).
    /// </summary>
    public double ListeningSpeed => DateLastPlayed == null ? UserSettings.PlaybackSpeed : PlaybackSpeed;

    /// <summary>How long the whole book takes at <see cref="ListeningSpeed" />, e.g. "11h 54m · 2.8×".</summary>
    public string LengthAtSpeedText
    {
        get
        {
            var speed = ListeningSpeed > 0 ? ListeningSpeed : 1;
            return $"{DurationFormat.Short(Duration / speed)} · {PlaybackSpeedPresets.Format(speed)}";
        }
    }

    /// <summary>
    ///     Gets or sets the release date of the audiobook.
    /// </summary>
    public string ReleaseDate => Model.ReleaseDate?.ToShortDateString() ?? string.Empty;

    /// <summary>
    ///     Gets or sets the current chapter of the audiobook.
    /// </summary>
    public ChapterInfo CurrentChapter => Chapters[CurrentChapterIndex ?? 0];

    private string _currentChapterTitle;

    /// <summary>
    ///     Gets or sets the current chapter title of the audiobook.
    /// </summary>
    public string CurrentChapterTitle
    {
        get => _currentChapterTitle;
        set => Set(ref _currentChapterTitle, value);
    }

    /// <summary>
    ///     Gets the current source file of the audiobook.
    /// </summary>
    public SourceFile CurrentSourceFile => Model.SourcePaths[Model.CurrentSourceFileIndex];

    /// <summary>
    ///     Gets the source paths of the audiobook.
    /// </summary>
    public List<SourceFile> SourcePaths => Model.SourcePaths;

    #endregion

    #endregion
}