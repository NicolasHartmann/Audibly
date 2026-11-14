using Audibly.Core;
using Audibly.Mobile.Services;

namespace Audibly.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ILibraryImporter _importer;
    private bool _loading;

    public SettingsPage(ILibraryImporter importer)
    {
        InitializeComponent();
        _importer = importer;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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
