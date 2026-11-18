// Author: rstewa · https://github.com/rstewa
// Updated: 06/09/2025

using Audibly.App.Helpers;
using Audibly.App.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Audibly.App.Services;
using Audibly.App.ViewModels;
using Audibly.Models;
using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ColorHelper = CommunityToolkit.WinUI.Helpers.ColorHelper;

namespace Audibly.App.UserControls;

public sealed partial class AudiobookTile : UserControl
{
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    public AudiobookTile()
    {
        InitializeComponent();

        // Two lines are always reserved for the title, so the progress bars of a row line up
        // whether a title takes one line or two. The zoom setting changes the font size.
        TitleText.RegisterPropertyChangedCallback(TextBlock.FontSizeProperty, (_, _) => ReserveTwoTitleLines());
        ReserveTwoTitleLines();
    }

    private void ReserveTwoTitleLines() => TitleText.MinHeight = Math.Ceiling(TitleText.FontSize * 1.43) * 2;

    /// <summary>
    ///     Gets the app-wide PlayerViewModel instance.
    /// </summary>
    private static PlayerViewModel PlayerViewModel => App.PlayerViewModel;

    /// <summary>
    ///     Gets the app-wide ViewModel instance.
    /// </summary>
    private MainViewModel ViewModel => App.ViewModel;

    private void AudiobookTile_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        BlackOverlayGrid.Visibility = Visibility.Visible;
        ButtonTile.Background =
            new SolidColorBrush(ColorHelper.ToColor("#393939")); // Change background to indicate hover
    }

    private void AudiobookTile_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (MenuFlyout.IsOpen) return;
        BlackOverlayGrid.Visibility = Visibility.Collapsed;
        ButtonTile.Background = new SolidColorBrush(Colors.Transparent); // Revert background to original
    }

    private void MenuFlyout_Closed(object sender, object e)
    {
        BlackOverlayGrid.Visibility = Visibility.Collapsed;
        ButtonTile.Background = new SolidColorBrush(Colors.Transparent); // Revert background to original
    }

    /// <summary>A click on the book opens it in the large player; its back arrow returns to the library.</summary>
    private async void ButtonTile_Click(object sender, RoutedEventArgs e)
    {
        var audiobook = ViewModel.Audiobooks.FirstOrDefault(a => a.Id == Id);
        if (audiobook == null) return;
        try
        {
            if (PlayerViewModel.NowPlaying?.Id != audiobook.Id) await PlayerViewModel.OpenAudiobook(audiobook);
            if (PlayerViewModel.NowPlaying?.Id != audiobook.Id) return;

            PlayerViewModel.IsPlayerFullScreen = true;
            PlayerViewModel.MaximizeMinimizeGlyph = Helpers.Constants.MinimizeGlyph;
            PlayerViewModel.MaximizeMinimizeTooltip = Helpers.Constants.MinimizeTooltip;
            if (App.RootFrame?.Content is not PlayerPage) App.RootFrame?.Navigate(typeof(PlayerPage));
        }
        catch (Exception exception)
        {
            ViewModel.LoggingService.LogError(exception, true);
        }
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        var audiobook = ViewModel.Audiobooks.FirstOrDefault(a => a.Id == Id);
        if (audiobook == null) return;
        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            await PlayerViewModel.OpenAudiobook(audiobook);

            // todo: this really breaks shit
            // PlayerViewModel.MediaPlayer.Play();
        });
    }

    private void ShowInFileExplorer_OnClick(object sender, RoutedEventArgs e)
    {
        Process p = new();
        p.StartInfo.FileName = "explorer.exe";
        p.StartInfo.Arguments = $"/select, \"{FilePath}\"";
        p.Start();
    }

    private async void DeleteAudiobook_OnClick(object sender, RoutedEventArgs e)
    {
        var audiobook = ViewModel.Audiobooks.FirstOrDefault(a => a.Id == Id);
        if (audiobook == null) return;
        ViewModel.SelectedAudiobook = audiobook;

        await ViewModel.DeleteAudiobookAsync();
    }

    private void ButtonTile_OnRightTapped(object sender, RightTappedRoutedEventArgs? e)
    {
        if (e is null) return;
        var myOption = new FlyoutShowOptions
        {
            ShowMode = FlyoutShowMode.Transient
        };
        MenuFlyout.ShowAt(ButtonTile, myOption);
    }

    private void OpenInAppFolder_OnClick(object sender, RoutedEventArgs e)
    {
        var audiobook = ViewModel.Audiobooks.FirstOrDefault(a => a.Id == Id);
        if (audiobook == null) return;
        var dir = Path.GetDirectoryName(audiobook.CoverImagePath);
        if (dir == null) return;
        Process p = new();
        p.StartInfo.FileName = "explorer.exe";
        p.StartInfo.Arguments = $"/open, \"{dir}\"";
        p.Start();
    }

    private async void MoreInfo_OnClick(object sender, RoutedEventArgs e)
    {
        var audiobook = ViewModel.Audiobooks.FirstOrDefault(a => a.Id == Id);
        if (audiobook == null) return;

        MenuFlyout.Hide();

        // note: content dialog
        await DialogService.ShowMoreInfoDialogAsync(audiobook);
    }

    private async void MarkAsCompleted_OnClick(object sender, RoutedEventArgs e)
    {
        var audiobook = ViewModel.Audiobooks.FirstOrDefault(a => a.Id == Id);
        if (audiobook == null) return;
        audiobook.IsCompleted = true;
        await audiobook.SavePlaybackStateAsync();
    }

    private async void MarkAsIncomplete_OnClick(object sender, RoutedEventArgs e)
    {
        var audiobook = ViewModel.Audiobooks.FirstOrDefault(a => a.Id == Id);
        if (audiobook == null) return;
        audiobook.IsCompleted = false;
        await audiobook.SavePlaybackStateAsync();
    }

    private void ExportMetadataToJson_OnClick(object sender, RoutedEventArgs e)
    {
        var audiobook = ViewModel.Audiobooks.FirstOrDefault(a => a.Id == Id);
        if (audiobook == null) return;

        ViewModel.AppDataService.ExportMetadataAsync(audiobook.SourcePaths)
            .ContinueWith(task =>
            {
                if (task.IsFaulted) App.ViewModel.LoggingService.LogError(task.Exception, true);
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    #region dependency properties

    public Guid Id
    {
        get => (Guid)GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    public static readonly DependencyProperty IdProperty =
        DependencyProperty.Register(nameof(Id), typeof(Guid), typeof(AudiobookTile),
            new PropertyMetadata(Guid.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(AudiobookTile), new PropertyMetadata(null));

    public string Author
    {
        get => (string)GetValue(AuthorProperty);
        set => SetValue(AuthorProperty, value);
    }

    public static readonly DependencyProperty AuthorProperty =
        DependencyProperty.Register(nameof(Author), typeof(string), typeof(AudiobookTile), new PropertyMetadata(null));

    public object Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(object), typeof(AudiobookTile), new PropertyMetadata(null));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(AudiobookTile),
            new PropertyMetadata(0.0, ProgressPropertyChangedCallback));

    private static void ProgressPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudiobookTile audiobookTile) audiobookTile.ProgressText = $"{Math.Clamp(Math.Floor((double)e.NewValue), 0, 100):0}%";
    }

    /// <summary>"57%"; the bar next to it shows the same.</summary>
    public string ProgressText
    {
        get => (string)GetValue(ProgressTextProperty);
        private set => SetValue(ProgressTextProperty, value);
    }

    public static readonly DependencyProperty ProgressTextProperty =
        DependencyProperty.Register(nameof(ProgressText), typeof(string), typeof(AudiobookTile), new PropertyMetadata("0%"));

    /// <summary>Length of the whole book at its playback speed, e.g. "11h 54m · 2.8×".</summary>
    public string LengthText
    {
        get => (string)GetValue(LengthTextProperty);
        set => SetValue(LengthTextProperty, value);
    }

    public static readonly DependencyProperty LengthTextProperty =
        DependencyProperty.Register(nameof(LengthText), typeof(string), typeof(AudiobookTile), new PropertyMetadata(string.Empty));

    public bool IsCompleted
    {
        get => (bool)GetValue(IsCompletedProperty);
        set => SetValue(IsCompletedProperty, value);
    }

    public static readonly DependencyProperty IsCompletedProperty =
        DependencyProperty.Register(nameof(IsCompleted), typeof(bool), typeof(AudiobookTile),
            new PropertyMetadata(false, IsCompletedPropertyChangedCallback));

    private static void IsCompletedPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not AudiobookTile audiobookTile) return;
        var isCompleted = (bool)e.NewValue;
        // a finished book shows a full bar with a check mark instead of the percentage
        audiobookTile.ProgressTextBlock.Visibility = isCompleted ? Visibility.Collapsed : Visibility.Visible;
        audiobookTile.CompletedGrid.Visibility = isCompleted ? Visibility.Visible : Visibility.Collapsed;
        audiobookTile.MarkAsCompletedButton.Visibility = isCompleted ? Visibility.Collapsed : Visibility.Visible;
        audiobookTile.MarkAsIncompleteButton.Visibility = isCompleted ? Visibility.Visible : Visibility.Collapsed;
    }

    public int SourcePathsCount
    {
        get => (int)GetValue(SourcePathsCountProperty);
        set => SetValue(SourcePathsCountProperty, value);
    }

    public static readonly DependencyProperty SourcePathsCountProperty =
        DependencyProperty.Register(nameof(SourcePathsCount), typeof(int), typeof(AudiobookTile),
            new PropertyMetadata(0));

    public List<SourceFile> SourcePaths
    {
        get => (List<SourceFile>)GetValue(SourcePathsProperty);
        set => SetValue(SourcePathsProperty, value);
    }

    public static readonly DependencyProperty SourcePathsProperty =
        DependencyProperty.Register(nameof(SourcePaths), typeof(List<SourceFile>), typeof(AudiobookTile),
            new PropertyMetadata(null));

    public string FilePath
    {
        get => (string)GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    public static readonly DependencyProperty FilePathProperty =
        DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(AudiobookTile),
            new PropertyMetadata(null));

    #endregion
}