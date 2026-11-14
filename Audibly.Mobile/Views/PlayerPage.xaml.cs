using Audibly.Core;
using Audibly.Mobile.Services;

namespace Audibly.Mobile.Views;

public partial class PlayerPage : ContentPage
{
    private static readonly int[] TimerMinutes = [5, 10, 15, 30, 45, 60];

    private readonly PlaybackCoordinator _playback;
    private bool _dragging;
    private Guid? _shownBook;

    public PlayerPage(PlaybackCoordinator playback)
    {
        InitializeComponent();
        _playback = playback;
    }

    private bool CoversBook => PlayerSettings.ProgressScope == PlayerProgressScope.Book && _playback.BookDurationMs > 0;
    private double RangeMs => CoversBook ? _playback.BookDurationMs : _playback.ChapterDurationMs;
    private double ValueMs => CoversBook ? _playback.BookPositionMs : _playback.ChapterPositionMs;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _playback.Changed += Update;
        _playback.Failed += ShowFailure;
        _shownBook = null;
        Update();
    }

    protected override void OnDisappearing()
    {
        _playback.Changed -= Update;
        _playback.Failed -= ShowFailure;
        base.OnDisappearing();
    }

    private async void ShowFailure(string message)
    {
        try
        {
            await DisplayAlertAsync("Playback failed",
                "This part of the audiobook could not be played. The file may have been moved or deleted.\n\n" +
                message, "OK");
        }
        catch (Exception)
        {
            // the page went away while the message was on its way
        }
    }

    private void Update()
    {
        var book = _playback.Book;
        if (book == null) return;

        if (_shownBook != book.Id)
        {
            _shownBook = book.Id;
            TitleLabel.Text = book.Title;
            AuthorLabel.Text = book.Author;
            CoverImage.Source = !string.IsNullOrEmpty(book.CoverImagePath) && File.Exists(book.CoverImagePath)
                ? ImageSource.FromFile(book.CoverImagePath)
                : null;
        }

        ChapterButton.Text = _playback.CurrentChapter?.Title ?? string.Empty;
        PlayPauseButton.Text = _playback.WantsToPlay ? "Pause" : "Play";
        SpeedButton.Text = "Speed " + PlaybackSpeedPresets.Format(book.PlaybackSpeed);
        TimerButton.Text = _playback.SleepTimerEnd is { } end
            ? "Sleep in " + Math.Max(1, (int)Math.Ceiling((end - DateTime.Now).TotalMinutes)) + " min"
            : "Sleep timer";

        var range = Math.Max(1, RangeMs);
        var value = Math.Clamp(ValueMs, 0, range);
        if (!_dragging)
        {
            // the maximum first: a slider clamps a value that is above its current maximum
            ProgressSlider.Maximum = range;
            ProgressSlider.Value = value;
        }

        ElapsedLabel.Text = PlayerProgress.Elapsed(value);
        EndButton.Text = PlayerProgress.End(PlayerSettings.TimeDisplay, range, value, book.PlaybackSpeed);
    }

    private void Slider_DragStarted(object? sender, EventArgs e) => _dragging = true;

    private void Slider_DragCompleted(object? sender, EventArgs e)
    {
        _dragging = false;
        if (CoversBook) _playback.SeekBook(ProgressSlider.Value);
        else _playback.SeekChapter(ProgressSlider.Value);
    }

    private void End_Clicked(object? sender, EventArgs e)
    {
        PlayerSettings.TimeDisplay = PlayerProgress.Next(PlayerSettings.TimeDisplay);
        Update();
    }

    private void PlayPause_Clicked(object? sender, EventArgs e) => _playback.PlayPause();
    private void Back_Clicked(object? sender, EventArgs e) => _playback.Skip(-10);
    private void Forward_Clicked(object? sender, EventArgs e) => _playback.Skip(30);
    private void PreviousChapter_Clicked(object? sender, EventArgs e) => _playback.PreviousChapter();
    private void NextChapter_Clicked(object? sender, EventArgs e) => _playback.NextChapter();

    private async void Chapter_Clicked(object? sender, EventArgs e)
    {
        if (_playback.Book is not { } book || book.Chapters.Count == 0) return;
        var titles = book.Chapters.Select((chapter, index) => $"{index + 1}. {chapter.Title}").ToArray();
        var choice = await DisplayActionSheetAsync("Chapters", "Cancel", null, titles);
        var picked = Array.IndexOf(titles, choice);
        if (picked >= 0) _playback.GoToChapter(book.Chapters[picked]);
    }

    private async void Speed_Clicked(object? sender, EventArgs e)
    {
        var options = PlaybackSpeedPresets.QuickPicks.Select(PlaybackSpeedPresets.Format).ToArray();
        var choice = await DisplayActionSheetAsync("Playback speed", "Cancel", null, options);
        var picked = Array.IndexOf(options, choice);
        if (picked >= 0) _playback.SetSpeed(PlaybackSpeedPresets.QuickPicks[picked]);
    }

    private async void Timer_Clicked(object? sender, EventArgs e)
    {
        var options = TimerMinutes.Select(minutes => minutes + " minutes").ToArray();
        var choice = await DisplayActionSheetAsync("Sleep timer", "Cancel",
            _playback.SleepTimerEnd != null ? "Turn off" : null, options);
        if (choice == "Turn off") _playback.SetSleepTimer(null);
        var picked = Array.IndexOf(options, choice);
        if (picked >= 0) _playback.SetSleepTimer(TimeSpan.FromMinutes(TimerMinutes[picked]));
    }
}
