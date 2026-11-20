using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Audibly.App.ViewModels;
using Audibly.Core.Metadata;
using Audibly.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;

namespace Audibly.App.Views.ContentDialogs;

/// <summary>
///     Searches Audible for a book, lets the user pick the right edition and then the details to take
///     over. Saves nothing itself: <see cref="Result" /> is set when the user pressed Apply.
/// </summary>
public sealed partial class LookUpOnlineDialog : ContentDialog
{
    /// <summary>Also used to download the chosen cover after Apply.</summary>
    internal static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly AudibleMetadataClient Client = new(Http);

    private readonly BookMetadata _current;
    private readonly string _currentCover;
    private readonly CancellationTokenSource _closing = new();
    private BookDetails? _details;
    private int _search;

    public LookUpOnlineDialog(AudiobookViewModel book)
    {
        InitializeComponent();
        _current = BookMetadata.From(book.Model);
        _currentCover = book.ThumbnailPath;
        SearchBox.Text = $"{book.Title} {book.Author}".Trim();
        Opened += async (_, _) => await SearchAsync();
        Closed += (_, _) => _closing.Cancel();
    }

    /// <summary>The details to save, or null when the user cancelled.</summary>
    public BookMetadata? Result { get; private set; }

    /// <summary>The address of the cover to download and use, or null when the cover was not ticked.</summary>
    public string? CoverUrl { get; private set; }

    /// <summary>
    ///     True when the book has a cover of its own. A book without one shows the default cover, whose
    ///     path is the image in the app package.
    /// </summary>
    public static bool HasOwnCover(string? coverImagePath) =>
        !string.IsNullOrWhiteSpace(coverImagePath) &&
        !coverImagePath.EndsWith("DefaultCoverImage.png", StringComparison.OrdinalIgnoreCase);

    private async Task SearchAsync()
    {
        var search = ++_search;
        HitList.ItemsSource = null;
        IsPrimaryButtonEnabled = false;
        SetBusy(true, "Searching Audible…");
        try
        {
            var hits = await Client.SearchAsync(SearchBox.Text, _closing.Token);
            if (search != _search) return;
            HitList.ItemsSource = hits.Select(hit => new HitItem(hit)).ToList();
            SetBusy(false, hits.Count == 0 ? "No results." : string.Empty);
        }
        catch (MetadataLookupException e)
        {
            if (search == _search) SetBusy(false, e.Message);
        }
        catch (OperationCanceledException)
        {
            // the dialog was closed
        }
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_details != null)
        {
            // step 2: Apply closes the dialog
            var chosen = ((IEnumerable<FieldItem>)FieldList.ItemsSource)
                .Where(item => item.IsChecked == true).Select(item => item.Field).ToHashSet();
            Result = MetadataComparison.Apply(_current, _details, chosen);
            CoverUrl = chosen.Contains(MetadataField.Cover) ? _details.ImageUrl : null;
            return;
        }

        args.Cancel = true;
        if (HitList.SelectedItem is not HitItem selected) return;

        var deferral = args.GetDeferral();
        IsPrimaryButtonEnabled = false;
        SetBusy(true, "Loading details…");
        try
        {
            _details = await Client.GetDetailsAsync(selected.Hit, _closing.Token);
            ShowFields(MetadataComparison.Compare(_current, _details, HasOwnCover(_currentCover)));
        }
        catch (MetadataLookupException e)
        {
            SetBusy(false, e.Message);
            IsPrimaryButtonEnabled = true;
        }
        catch (OperationCanceledException)
        {
            // the dialog was closed
        }
        finally
        {
            deferral.Complete();
        }
    }

    // "Back" in step 2
    private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        _details = null;
        FieldPanel.Visibility = Visibility.Collapsed;
        HitList.Visibility = Visibility.Visible;
        SearchBar.Visibility = Visibility.Visible;
        SecondaryButtonText = string.Empty;
        PrimaryButtonText = "Next";
        IsPrimaryButtonEnabled = HitList.SelectedItem != null;
        SetBusy(false, string.Empty);
    }

    private void ShowFields(IReadOnlyList<FieldChange> changes)
    {
        FieldList.ItemsSource = changes.Select(change => new FieldItem(change, _currentCover)).ToList();
        SearchBar.Visibility = Visibility.Collapsed;
        HitList.Visibility = Visibility.Collapsed;
        FieldPanel.Visibility = Visibility.Visible;
        SecondaryButtonText = "Back";
        PrimaryButtonText = "Apply";
        IsPrimaryButtonEnabled = true;
        SetBusy(false, "Tick what to take over. Left: current, right: found online.");
    }

    private void SetBusy(bool busy, string status)
    {
        Busy.IsActive = busy;
        StatusText.Text = status;
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync();

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        await SearchAsync();
    }

    private void Hits_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        IsPrimaryButtonEnabled = HitList.SelectedItem != null;
}

/// <summary>One search hit as the list shows it.</summary>
public sealed class HitItem(AudibleHit hit)
{
    public AudibleHit Hit { get; } = hit;
    public string Title => Hit.Title;

    public BitmapImage? Image { get; } = Uri.TryCreate(hit.ImageUrl, UriKind.Absolute, out var uri)
        ? new BitmapImage(uri) { DecodePixelWidth = 96 }
        : null;

    public string Details => string.Join(" · ", new[]
    {
        Hit.Author,
        Hit.Narrators,
        $"{Hit.Series} {Hit.SeriesPart}".Trim(),
        Hit.RuntimeMinutes is { } minutes ? $"{minutes / 60}h {minutes % 60:00}m" : null,
        Hit.ReleaseDate?.Year.ToString(),
        Hit.Language,
        Hit.Region.ToUpperInvariant()
    }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

/// <summary>One field of step 2: current and found value and whether to take it over.</summary>
public sealed class FieldItem(FieldChange change, string currentCover)
{
    public bool IsCover => change.Field == MetadataField.Cover;
    public bool IsText => !IsCover;
    public BitmapImage? OldImage { get; } = change.Field == MetadataField.Cover ? Picture(currentCover) : null;
    public BitmapImage? NewImage { get; } = change.Field == MetadataField.Cover ? Picture(change.New) : null;

    public MetadataField Field => change.Field;
    public string Old => change.Old;
    public string New => change.New;
    public bool CanApply => change.CanApply;
    public bool? IsChecked { get; set; } = change.ApplyByDefault;

    public string Label => change.Field switch
    {
        MetadataField.SeriesPart => "Part",
        MetadataField.ReleaseDate => "Release date",
        _ => change.Field.ToString()
    };

    private static BitmapImage? Picture(string? source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) ? new BitmapImage(uri) { DecodePixelWidth = 192 } : null;
}
