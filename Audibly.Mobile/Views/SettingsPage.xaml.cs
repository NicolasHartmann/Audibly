using Audibly.Core;
using Audibly.Mobile.Services;

namespace Audibly.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ILibraryImporter _importer;
    private readonly SyncHost _sync;
    private bool _loading;
    private bool _picking;

    public SettingsPage(ILibraryImporter importer, SyncHost sync)
    {
        InitializeComponent();
        _importer = importer;
        _sync = sync;
    }

    protected override void OnDisappearing()
    {
        _sync.Changed -= OnSyncChanged;
        base.OnDisappearing();
    }

    private void OnSyncChanged() => MainThread.BeginInvokeOnMainThread(ShowSync);

    private void ShowSync()
    {
        SyncOffButton.IsEnabled = _sync.IsEnabled;
        SyncFolderLabel.Text = _sync.IsEnabled ? "Folder: " + _sync.FolderName : "Folder: not set";
        SyncDevicesLabel.IsVisible = _sync.IsEnabled;
        if (!_sync.IsEnabled)
        {
            SyncStatusLabel.Text = "Off";
            return;
        }

        SyncStatusLabel.Text = !_sync.HasFolderAccess
            ? "Audibly can no longer use this folder. Choose it again."
            : _sync.LastError is { } error ? "Last sync failed: " + error
            : _sync.LastSuccessUtc is { } success ? "Last sync: " + success.ToLocalTime().ToString("d MMM yyyy HH:mm",
                System.Globalization.CultureInfo.InvariantCulture)
            : "Not synced yet";
        SyncDevicesLabel.Text = _sync.Others.Count == 0
            ? "No other devices found yet"
            : "Other devices: " + string.Join(", ", _sync.Others.Select(device => device.DeviceName));
    }

    private async void SyncChoose_Clicked(object? sender, EventArgs e)
    {
        if (_picking) return;
        _picking = true;
        try
        {
            await _sync.PickFolderAsync();
        }
        finally
        {
            _picking = false;
            ShowSync();
        }
    }

    private void SyncOff_Clicked(object? sender, EventArgs e)
    {
        _sync.TurnOff();
        ShowSync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _sync.Changed += OnSyncChanged;
        ShowSync();
        // the list of other devices is only as fresh as the last read
        _ = _sync.RefreshAsync();
        _loading = true;
        FolderLabel.Text = _importer.FolderName ?? "No folder chosen yet. Choose one on the Library page.";
        ScopeChapter.IsChecked = PlayerSettings.ProgressScope == PlayerProgressScope.Chapter;
        ScopeBook.IsChecked = PlayerSettings.ProgressScope == PlayerProgressScope.Book;
        DisplayTotal.IsChecked = PlayerSettings.TimeDisplay == PlayerTimeDisplay.ElapsedAndTotal;
        DisplayRemaining.IsChecked = PlayerSettings.TimeDisplay == PlayerTimeDisplay.Remaining;
        DisplayPercent.IsChecked = PlayerSettings.TimeDisplay == PlayerTimeDisplay.Percent;
        _loading = false;
    }

    private void Scope_Changed(object? sender, CheckedChangedEventArgs e)
    {
        if (_loading || !e.Value) return;
        PlayerSettings.ProgressScope = ScopeBook.IsChecked ? PlayerProgressScope.Book : PlayerProgressScope.Chapter;
    }

    private void Display_Changed(object? sender, CheckedChangedEventArgs e)
    {
        if (_loading || !e.Value) return;
        PlayerSettings.TimeDisplay = DisplayRemaining.IsChecked ? PlayerTimeDisplay.Remaining
            : DisplayPercent.IsChecked ? PlayerTimeDisplay.Percent
            : PlayerTimeDisplay.ElapsedAndTotal;
    }
}
