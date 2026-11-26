using System.Globalization;
using Audibly.Core;
using Audibly.Mobile.Services;

namespace Audibly.Mobile.Views;

/// <summary>
///     The app's home: the book that is open, its progress and the transport controls. The app
///     starts here with the book that was listened to last.
/// </summary>
public partial class PlayerPage : ContentPage
{
    private static readonly int[] TimerMinutes = [5, 10, 15, 30, 45, 60];
    private static readonly double[] SpeedQuickPicks = [1.0, 1.25, 1.5, 1.75, 2.0];

    private readonly PlaybackCoordinator _playback;
    private bool _dragging;
    private bool _restored;
    private bool _restoreDone;
    private bool _settingSpeed;
    private Guid? _shownBook;

    public PlayerPage(PlaybackCoordinator playback)
    {
        InitializeComponent();
        _playback = playback;

        SpeedSlider.Maximum = PlaybackSpeedPresets.StepCount;
        foreach (var pick in SpeedQuickPicks)
        {
            var text = pick.ToString("0.00", CultureInfo.InvariantCulture);
            var button = new Button
            {
                Text = text,
                BackgroundColor = Colors.Transparent,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(8, 0),
                AutomationId = "SpeedPick" + text
            };
            button.SetAppThemeColor(Button.TextColorProperty, (Color)Application.Current!.Resources["Primary"],
                (Color)Application.Current.Resources["PrimaryDark"]);
            button.Clicked += (_, _) => SetSpeed(pick);
            SpeedPicks.Add(button);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _playback.Changed += Update;
        _playback.Failed += ShowFailure;
        _shownBook = null;
        Update();

        if (_restored) return;
        _restored = true;
        try
        {
            // the app opens where the listener left: in the player, with the last book loaded
            await _playback.RestoreAsync();
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine("[Player] restore failed: " + e);
        }

        _restoreDone = true;
        Update();
    }

    protected override void OnDisappearing()
    {
        _playback.Changed -= Update;
        _playback.Failed -= ShowFailure;
        base.OnDisappearing();
    }

    /// <summary>Android's back button closes the speed panel first, then leaves the app as usual.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!SpeedOverlay.IsVisible) return base.OnBackButtonPressed();
        SpeedOverlay.IsVisible = false;
        return true;
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
        PlayerLayout.IsVisible = book != null;
        // while the last book is still loading the page stays blank instead of saying "no audiobook"
        EmptyLayout.IsVisible = book == null && _restoreDone;
        if (book == null) return;

        if (_shownBook != book.Id)
        {
            _shownBook = book.Id;
            TitleLabel.Text = book.Title;
            AuthorLabel.Text = book.Author;
            var hasCover = !string.IsNullOrEmpty(book.CoverImagePath) && File.Exists(book.CoverImagePath);
            CoverImage.Source = hasCover ? ImageSource.FromFile(book.CoverImagePath) : null;
            CoverImage.IsVisible = hasCover;
        }

        var offer = _playback.PendingOffer;
        OfferCard.IsVisible = offer != null;
        if (offer != null)
            OfferLabel.Text = $"On {offer.DeviceName} you were at " +
                              $"{PlayerProgress.Elapsed(offer.PositionSeconds * 1000)} ({When(offer.UpdatedUtc)}). " +
                              "Continue there?";

        var speed = book.PlaybackSpeed;
        SpeedButton.Text = PlaybackSpeedPresets.Snap(speed).ToString("0.00", CultureInfo.InvariantCulture) + "×";
        PlayPauseIcon.Data = _playback.WantsToPlay ? PlayerIcons.Pause : PlayerIcons.Play;
        SemanticProperties.SetDescription(PlayPauseButton, _playback.WantsToPlay ? "Pause" : "Play");

        var timer = _playback.SleepTimerRemaining;
        TimerIcon.IsVisible = timer == null;
        TimerText.IsVisible = timer != null;
        if (timer is { } remaining)
            TimerText.Text = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)) + " min";

        // the whole book: elapsed audio time, percent, and what is left at the current speed
        var bookDuration = Math.Max(1, _playback.BookDurationMs);
        var bookPosition = Math.Clamp(_playback.BookPositionMs, 0, bookDuration);
        BookProgress.Progress = bookPosition / bookDuration;
        BookElapsedLabel.Text = PlayerProgress.Elapsed(bookPosition);
        BookPercentLabel.Text = (Math.Floor(bookPosition / bookDuration * 1000) / 10)
            .ToString("0.0", CultureInfo.InvariantCulture) + "%";
        BookRemainingLabel.Text = "-" + PlaybackTimeRemaining.Format(bookDuration, bookPosition, speed);

        // the chapter
        var chapterDuration = Math.Max(1, _playback.ChapterDurationMs);
        var chapterPosition = Math.Clamp(_playback.ChapterPositionMs, 0, chapterDuration);
        if (!_dragging)
        {
            // the maximum first: a slider clamps a value that is above its current maximum
            ProgressSlider.Maximum = chapterDuration;
            ProgressSlider.Value = chapterPosition;
        }

        ChapterElapsedLabel.Text = PlaybackTimeRemaining.Format(chapterPosition, 0, 1);
        ChapterRemainingLabel.Text = "-" + PlaybackTimeRemaining.Format(chapterDuration, chapterPosition, speed);
        var chapter = _playback.CurrentChapter;
        ChapterButton.Text = chapter?.Title ?? string.Empty;
        ChapterNumberLabel.Text = chapter == null ? string.Empty : $"{chapter.Index + 1}/{book.Chapters.Count}";
    }

    private static string When(DateTime utc)
    {
        var local = utc.ToLocalTime();
        var days = (DateTime.Now.Date - local.Date).Days;
        return days == 0 ? $"today {local:HH:mm}"
            : days == 1 ? $"yesterday {local:HH:mm}"
            : local.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    // --- quick links and menu

    private void Menu_Clicked(object? sender, EventArgs e) => Shell.Current.FlyoutIsPresented = true;

    private async void Library_Clicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(LibraryPage));

    private void OfferContinue_Clicked(object? sender, EventArgs e) => _playback.AcceptOffer();
    private void OfferStay_Clicked(object? sender, EventArgs e) => _playback.DeclineOffer();

    // --- transport

    private void Slider_DragStarted(object? sender, EventArgs e) => _dragging = true;

    private void Slider_DragCompleted(object? sender, EventArgs e)
    {
        _dragging = false;
        _playback.SeekChapter(ProgressSlider.Value);
    }

    private void PlayPause_Clicked(object? sender, EventArgs e) => _playback.PlayPause();
    private void Back_Clicked(object? sender, EventArgs e) => _playback.Skip(-10);
    private void BackFar_Clicked(object? sender, EventArgs e) => _playback.Skip(-60);
    private void Forward_Clicked(object? sender, EventArgs e) => _playback.Skip(30);
    private void ForwardFar_Clicked(object? sender, EventArgs e) => _playback.Skip(60);
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

    /// <summary>Opened from the quick link here and from the menu.</summary>
    public async Task ShowTimerAsync()
    {
        const string chapterEnd = "End of chapter";
        var options = TimerMinutes.Select(minutes => minutes + " minutes").ToArray();
        string[] shown = _playback.CurrentChapter != null ? [chapterEnd, .. options] : options;
        var choice = await DisplayActionSheetAsync("Sleep timer", "Cancel",
            _playback.IsSleepTimerOn ? "Turn off" : null, shown);
        if (choice == "Turn off") _playback.SetSleepTimer(null);
        if (choice == chapterEnd) _playback.SetSleepTimerToChapterEnd();
        var picked = Array.IndexOf(options, choice);
        if (picked >= 0) _playback.SetSleepTimer(TimeSpan.FromMinutes(TimerMinutes[picked]));
    }

    private async void Timer_Clicked(object? sender, EventArgs e) => await ShowTimerAsync();

    // --- playback speed

    /// <summary>Opened from the quick link here and from the menu.</summary>
    public void ShowSpeed()
    {
        if (_playback.Book is not { } book) return;
        ShowSpeedValue(book.PlaybackSpeed);
        _settingSpeed = true;
        SpeedDefaultCheck.IsChecked =
            Math.Abs(PlaybackCoordinator.DefaultSpeed - PlaybackSpeedPresets.Snap(book.PlaybackSpeed)) < 0.001;
        _settingSpeed = false;
        SpeedOverlay.IsVisible = true;
    }

    private void Speed_Clicked(object? sender, EventArgs e) => ShowSpeed();
    private void SpeedClose_Tapped(object? sender, EventArgs e) => SpeedOverlay.IsVisible = false;

    private void ShowSpeedValue(double speed)
    {
        _settingSpeed = true;
        SpeedSlider.Value = PlaybackSpeedPresets.ToIndex(speed);
        _settingSpeed = false;
        SpeedValueLabel.Text = PlaybackSpeedPresets.Snap(speed).ToString("0.00", CultureInfo.InvariantCulture) + "×";
    }

    private void SetSpeed(double speed)
    {
        speed = PlaybackSpeedPresets.Snap(speed);
        ShowSpeedValue(speed);
        _playback.SetSpeed(speed);
        // the box says "this speed is the default"; a new speed keeps that promise
        if (SpeedDefaultCheck.IsChecked) PlaybackCoordinator.DefaultSpeed = speed;
    }

    private void SpeedMinus_Clicked(object? sender, EventArgs e) =>
        SetSpeed((_playback.Book?.PlaybackSpeed ?? 1) - PlaybackSpeedPresets.Step);

    private void SpeedPlus_Clicked(object? sender, EventArgs e) =>
        SetSpeed((_playback.Book?.PlaybackSpeed ?? 1) + PlaybackSpeedPresets.Step);

    private void SpeedSlider_ValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (!_settingSpeed) SetSpeed(PlaybackSpeedPresets.FromIndex(e.NewValue));
    }

    private void SpeedDefault_Changed(object? sender, CheckedChangedEventArgs e)
    {
        if (_settingSpeed) return;
        PlaybackCoordinator.DefaultSpeed = e.Value ? PlaybackSpeedPresets.Snap(_playback.Book?.PlaybackSpeed ?? 1) : 1.0;
    }
}
