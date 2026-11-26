using System.Collections.ObjectModel;
using Audibly.Core.Metadata;
using Audibly.Mobile.Services;

namespace Audibly.Mobile.Views;

/// <summary>One book in the library list.</summary>
public sealed class BookItem(LibraryEntry book, bool available, bool isOpen)
{
    public LibraryEntry Book { get; } = book;
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
    // kept while the app runs: the page is created anew each time it opens, the list should not be
    private static List<LibraryEntry>? _cache;
    private static readonly Dictionary<Guid, bool> Availability = [];

    private readonly AppData _data;
    private readonly ILibraryImporter _importer;
    private readonly PlaybackCoordinator _playback;
    private readonly ObservableCollection<BookItem> _books = [];
    private readonly View _emptyView;
    private bool _busy;
    private bool _opening;

    public LibraryPage(AppData data, ILibraryImporter importer, PlaybackCoordinator playback)
    {
        InitializeComponent();
        _data = data;
        _importer = importer;
        _playback = playback;
        BookList.ItemsSource = _books;

        // "no audiobooks yet" only once it is certain; while loading the list shows nothing
        _emptyView = (View)BookList.EmptyView;
        BookList.EmptyView = null;
        if (_cache != null) Show(_cache);
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
        LoadingIndicator.IsRunning = LoadingIndicator.IsVisible = _cache == null;
        try
        {
            var books = await _data.LoadLibraryAsync();
            _cache = books;
            _playback.LibraryCount = books.Count;
            Show(books);
            BookList.EmptyView = _emptyView;
            if (!_importer.HasFolderAccess) ShowStatus(NoAccessMessage);

            // whether the files are still there touches storage for every book; the list is shown first
            var changed = await Task.Run(() =>
            {
                var any = false;
                foreach (var book in books)
                {
                    var available = book.FirstFile != null && _importer.IsAvailable(book.FirstFile);
                    lock (Availability)
                    {
                        if (Availability.TryGetValue(book.Id, out var known) && known == available) continue;
                        Availability[book.Id] = available;
                        any = true;
                    }
                }

                return any;
            });
            if (changed && ReferenceEquals(_cache, books)) Show(books);
        }
        catch (Exception e)
        {
            ShowStatus("The library could not be opened: " + e.Message);
        }
        finally
        {
            LoadingIndicator.IsRunning = LoadingIndicator.IsVisible = false;
        }
    }

    private void Show(List<LibraryEntry> books)
    {
        var open = _playback.Book?.Id;
        var items = books
            // the open book comes first, then the ones listened to most recently, then the others by title
            .OrderByDescending(book => book.Id == open)
            .ThenByDescending(book => book.DateLastPlayed ?? DateTime.MinValue)
            .ThenBy(book => book.Title)
            .Select(book =>
            {
                bool available;
                lock (Availability) available = Availability.GetValueOrDefault(book.Id, true);
                return new BookItem(book, available, book.Id == open);
            })
            .ToList();

        _books.Clear();
        foreach (var item in items) _books.Add(item);
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
            lock (Availability) Availability.Clear();
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
            if (_playback.Book?.Id != item.Book.Id)
            {
                // the list holds no chapters; the book is read in full only when it is opened
                var book = await _data.LoadBookAsync(item.Book.Id);
                if (book == null)
                {
                    ShowStatus("The audiobook could not be found in the library any more.");
                    return;
                }

                await _playback.OpenAsync(book, false);
            }

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

    private const string ChangeCover = "Change cover";
    private const string RemoveOnly = "Remove from library";
    private const string RemoveAndDelete = "Remove and delete files from phone";

    /// <summary>
    ///     Removes a book from the library and, when asked to, deletes its files from the phone.
    ///     Deleting files is asked once more, because it cannot be undone.
    /// </summary>
    private async void BookMenu_Clicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not BookItem item || _busy) return;
        try
        {
            var choice = await DisplayActionSheetAsync(item.Title, "Cancel", null, ChangeCover, RemoveOnly, RemoveAndDelete);
            if (choice == ChangeCover)
            {
                await ChangeCoverAsync(item);
                return;
            }

            if (choice is not (RemoveOnly or RemoveAndDelete)) return;
            var deleteFiles = choice == RemoveAndDelete;

            if (deleteFiles && !_importer.CanDeleteFiles)
            {
                await DisplayAlertAsync("Choose the folder again",
                    "To delete files, Audibly needs permission to change the audiobook folder. Tap Folder, choose " +
                    "the same folder again and allow access. Your audiobooks and positions are kept.", "OK");
                return;
            }

            if (deleteFiles && !await DisplayAlertAsync("Delete files?",
                    $"\"{item.Title}\" is removed from the library and its files are deleted from this phone. " +
                    "This cannot be undone.", "Delete", "Cancel"))
                return;

            _busy = true;
            var book = await _data.LoadBookAsync(item.Book.Id);
            var locations = book?.SourcePaths.OrderBy(file => file.Index).Select(file => file.FilePath).ToList() ?? [];

            _playback.Close(item.Book.Id);
            await _data.DeleteBookAsync(item.Book.Id);
            if (!string.IsNullOrEmpty(item.Book.CoverImagePath))
                try
                {
                    File.Delete(item.Book.CoverImagePath);
                }
                catch (Exception)
                {
                    // a leftover cover image only takes a little space
                }

            var failed = 0;
            if (deleteFiles && locations.Count > 0) failed = await _importer.DeleteFilesAsync(locations);
            // files that stay in the folder must not come back with the next scan
            if (locations.Count > 0 && (!deleteFiles || failed > 0)) _importer.Ignore(locations[0]);

            ShowStatus(!deleteFiles ? $"\"{item.Title}\" was removed from the library."
                : failed == 0 ? $"\"{item.Title}\" was removed and its files were deleted."
                : $"\"{item.Title}\" was removed, but {failed} of its files could not be deleted.");
            await LoadAsync();
        }
        catch (Exception exception)
        {
            ShowStatus("The audiobook could not be removed: " + exception.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private const string CoverFromPhone = "Choose an image";
    private const string CoverFromFolder = "Image from the book's folder";
    private const string CoverOnline = "Search online";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly AudibleMetadataClient Metadata = new(Http);

    /// <summary>Makes an image the book's cover: one the user picks, the one in its folder, or one found online.</summary>
    private async Task ChangeCoverAsync(BookItem item)
    {
        var choice = await DisplayActionSheetAsync("Change cover", "Cancel", null,
            CoverFromPhone, CoverFromFolder, CoverOnline);
        try
        {
            _busy = true;
            var image = choice switch
            {
                CoverFromPhone => await PickCoverAsync(),
                CoverFromFolder => await FolderCoverAsync(item),
                CoverOnline => await OnlineCoverAsync(item),
                _ => null
            };
            if (image == null) return;

            await _data.SetCoverAsync(item.Book.Id, image, item.Book.CoverImagePath);
            ShowStatus($"The cover of \"{item.Title}\" was changed.");
            await LoadAsync();
        }
        catch (Exception exception)
        {
            ShowStatus("The cover could not be changed: " + exception.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private static async Task<byte[]?> PickCoverAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose a cover",
            FileTypes = FilePickerFileType.Images
        });
        if (file == null) return null;

        await using var stream = await file.OpenReadAsync();
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        return bytes.ToArray();
    }

    private async Task<byte[]?> FolderCoverAsync(BookItem item)
    {
        var image = item.Book.FirstFile is { } first ? await _importer.ReadFolderCoverAsync(first) : null;
        if (image == null)
            ShowStatus($"No cover image (like cover.jpg or folder.jpg) was found in the folder of \"{item.Title}\".");
        return image;
    }

    /// <summary>Searches Audible by title and author and downloads the cover of the edition the user picks.</summary>
    private async Task<byte[]?> OnlineCoverAsync(BookItem item)
    {
        var keywords = await DisplayPromptAsync("Search online", "Title and author", "Search", "Cancel",
            initialValue: $"{item.Book.Title} {item.Book.Author}".Trim());
        if (string.IsNullOrWhiteSpace(keywords)) return null;

        ShowStatus("Searching Audible…");
        IReadOnlyList<AudibleHit> hits;
        try
        {
            hits = (await Metadata.SearchAsync(keywords, CancellationToken.None))
                .Where(hit => !string.IsNullOrWhiteSpace(hit.ImageUrl)).ToList();
        }
        catch (MetadataLookupException e)
        {
            ShowStatus(e.Message);
            return null;
        }

        if (hits.Count == 0)
        {
            ShowStatus("No covers found. Try other words.");
            return null;
        }

        StatusLabel.IsVisible = false;
        // numbered, so two editions with the same text stay apart
        var labels = hits.Select((hit, index) => $"{index + 1}. " + string.Join(" · ", new[]
        {
            hit.Title, hit.Author, hit.Narrators, hit.ReleaseDate?.Year.ToString(), hit.Region.ToUpperInvariant()
        }.Where(part => !string.IsNullOrWhiteSpace(part)))).ToArray();
        var picked = Array.IndexOf(labels, await DisplayActionSheetAsync("Choose the edition", "Cancel", null, labels));
        if (picked < 0) return null;

        ShowStatus("Downloading the cover…");
        return await Http.GetByteArrayAsync(hits[picked].ImageUrl);
    }

    private void ShowStatus(string text)
    {
        StatusLabel.Text = text;
        StatusLabel.IsVisible = true;
    }
}
