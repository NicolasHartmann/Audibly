using System.Collections.ObjectModel;
using Audibly.Mobile.Services;
using Audibly.Models;

namespace Audibly.Mobile.Views;

/// <summary>One book in the library list.</summary>
public sealed class BookItem(Audiobook book, bool available, bool isOpen)
{
    public Audiobook Book { get; } = book;
    public string Title { get; } = string.IsNullOrWhiteSpace(book.Title) ? "Untitled" : book.Title;
    public string Initial => Title[..1].ToUpperInvariant();
    public string Subtitle { get; } = available ? book.Author ?? string.Empty : "File not found";
    public ImageSource? Cover { get; } = !string.IsNullOrEmpty(book.CoverImagePath) && File.Exists(book.CoverImagePath)
        ? ImageSource.FromFile(book.CoverImagePath)
        : null;
    public double Progress { get; } = Math.Clamp(book.Progress / 100.0, 0, 1);
    public string Percent { get; } = $"{Math.Clamp(Math.Floor(book.Progress), 0, 100):0}%";

    /// <summary>Length of the whole book as hours and minutes, e.g. "14:11".</summary>
    public string Length { get; } = $"{book.Duration / 3600}:{book.Duration / 60 % 60:00}";

    public bool IsOpen { get; } = isOpen;
    public FontAttributes TitleAttributes => IsOpen ? FontAttributes.Bold : FontAttributes.None;
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
        await LoadAsync();
    }

    private string NoAccessMessage =>
        $"Audibly can no longer read the folder \"{_importer.FolderName}\". Tap Folder and choose it again; your audiobooks and positions are kept.";

    private async Task LoadAsync()
    {
        try
        {
            var books = await _data.LoadBooksAsync();
            _playback.LibraryCount = books.Count;
            var open = _playback.Book?.Id;
            // checking the files touches storage; keep it off the UI thread
            var items = await Task.Run(() => books
                // the book that was listened to last comes first, then the others by title
                .OrderByDescending(book => book.Id == open)
                .ThenByDescending(book => book.DateLastPlayed ?? DateTime.MinValue)
                .ThenBy(book => book.Title)
                .Select(book => new BookItem(book,
                    book.SourcePaths.Count > 0 && _importer.IsAvailable(book.SourcePaths[0].FilePath), book.Id == open))
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

    /// <summary>Opens the book, paused, and goes straight back to the player.</summary>
    private async void Book_Tapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not BookItem item) return;
        if (!item.Available)
        {
            ShowStatus("The files of this audiobook are no longer in the folder.");
            return;
        }

        // a double tap would open the book twice
        if (_opening) return;
        _opening = true;
        try
        {
            // Android 13 and later only show the playback notification with this permission
            await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (_playback.Book?.Id != item.Book.Id) await _playback.OpenAsync(item.Book, false);
            await Shell.Current.GoToAsync("..");
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

    private void ShowStatus(string text)
    {
        StatusLabel.Text = text;
        StatusLabel.IsVisible = true;
    }
}
