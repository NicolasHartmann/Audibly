using System.Collections.ObjectModel;
using Audibly.Mobile.Services;
using Audibly.Models;

namespace Audibly.Mobile.Views;

/// <summary>One book in the library grid.</summary>
public sealed class BookItem(Audiobook book, bool available)
{
    public Audiobook Book { get; } = book;
    public string Title { get; } = string.IsNullOrWhiteSpace(book.Title) ? "Untitled" : book.Title;
    public string Initial => Title[..1].ToUpperInvariant();
    public string Subtitle { get; } = available ? book.Author ?? string.Empty : "File not found";
    public ImageSource? Cover { get; } = !string.IsNullOrEmpty(book.CoverImagePath) && File.Exists(book.CoverImagePath)
        ? ImageSource.FromFile(book.CoverImagePath)
        : null;
    public double Progress { get; } = Math.Clamp(book.Progress / 100.0, 0, 1);
    public bool Available { get; } = available;
}

public partial class LibraryPage : ContentPage
{
    private readonly AppData _data;
    private readonly ILibraryImporter _importer;
    private readonly PlaybackCoordinator _playback;
    private readonly ObservableCollection<BookItem> _books = [];
    private bool _busy;
    private bool _opening;

    public LibraryPage(AppData data, ILibraryImporter importer, PlaybackCoordinator playback)
    {
        InitializeComponent();
        _data = data;
        _importer = importer;
        _playback = playback;
        BookList.ItemsSource = _books;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _playback.Changed += UpdateMiniBar;
        _playback.Failed += ShowFailure;
        UpdateMiniBar();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        _playback.Changed -= UpdateMiniBar;
        _playback.Failed -= ShowFailure;
        base.OnDisappearing();
    }

    private string NoAccessMessage =>
        $"Audibly can no longer read the folder \"{_importer.FolderName}\". Tap Folder and choose it again; your audiobooks and positions are kept.";

    private async Task LoadAsync()
    {
        try
        {
            var books = (await _data.Repository.Audiobooks.GetAsync()).ToList();
            _playback.LibraryCount = books.Count;
            // checking the files touches storage; keep it off the UI thread
            var items = await Task.Run(() => books
                .Select(book => new BookItem(book,
                    book.SourcePaths.Count > 0 && _importer.IsAvailable(book.SourcePaths[0].FilePath)))
                .ToList());
            _books.Clear();
            foreach (var item in items) _books.Add(item);
            if (!_importer.HasFolderAccess) ShowStatus(NoAccessMessage);
        }
        catch (Exception e)
        {
            ShowStatus("The library could not be opened: " + e.Message);
        }
    }

    private async void Folder_Clicked(object? sender, EventArgs e)
    {
        if (_busy) return;
        // set before the dialog opens, so a second tap cannot open a second dialog
        _busy = true;
        try
        {
            if (await _importer.PickFolderAsync()) await ScanAsync();
        }
        catch (Exception exception)
        {
            ShowStatus("The folder could not be opened: " + exception.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private async void Rescan_Clicked(object? sender, EventArgs e)
    {
        if (_busy) return;
        if (_importer.FolderName == null)
        {
            ShowStatus("Choose a folder first.");
            return;
        }

        if (!_importer.HasFolderAccess)
        {
            ShowStatus(NoAccessMessage);
            return;
        }

        await ScanAsync();
    }

    private async Task ScanAsync()
    {
        _busy = true;
        ShowStatus("Looking for audiobooks…");
        try
        {
            var summary = await _importer.ScanAsync(name =>
                MainThread.BeginInvokeOnMainThread(() => ShowStatus("Adding " + name)));
            ShowStatus(summary.Failed > 0
                ? $"{summary.Added} added, {summary.Failed} could not be read."
                : summary.Added == 0
                    ? "No new audiobooks found."
                    : summary.Added == 1 ? "1 audiobook added." : $"{summary.Added} audiobooks added.");
            await LoadAsync();
        }
        catch (Exception exception)
        {
            ShowStatus("The scan failed: " + exception.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private async void Book_Tapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not BookItem item) return;
        if (!item.Available)
        {
            ShowStatus("The files of this audiobook are no longer in the folder.");
            return;
        }

        // a double tap would open the player page twice
        if (_opening) return;
        _opening = true;
        try
        {
            // Android 13 and later only show the playback notification with this permission
            await Permissions.RequestAsync<Permissions.PostNotifications>();
            // opening only loads the book; it starts when the listener presses Play
            if (_playback.Book?.Id != item.Book.Id) await _playback.OpenAsync(item.Book, false);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }
        catch (Exception exception)
        {
            ShowStatus("The audiobook could not be opened: " + exception.Message);
        }
        finally
        {
            _opening = false;
        }
    }

    private async void MiniBar_Tapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(nameof(PlayerPage));

    private void MiniPlayPause_Clicked(object? sender, EventArgs e) => _playback.PlayPause();

    private void UpdateMiniBar()
    {
        var book = _playback.Book;
        MiniBar.IsVisible = book != null;
        if (book == null) return;
        MiniTitle.Text = book.Title;
        MiniChapter.Text = _playback.CurrentChapter?.Title ?? string.Empty;
        MiniPlayPauseIcon.Data = _playback.WantsToPlay ? PlayerIcons.Pause : PlayerIcons.Play;
        SemanticProperties.SetDescription(MiniPlayPause, _playback.WantsToPlay ? "Pause" : "Play");
    }

    private void ShowFailure(string message) => ShowStatus("Playback failed: " + message);

    private void ShowStatus(string text)
    {
        StatusLabel.Text = text;
        StatusLabel.IsVisible = true;
    }
}
