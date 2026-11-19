﻿// Author: rstewa · https://github.com/rstewa
// Updated: 07/29/2025

using Audibly.Core;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Audibly.App.Helpers;
using Audibly.App.Services;
using Audibly.App.ViewModels;
using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Audibly.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnSettingsPageLoaded;
        Unloaded += OnSettingsPageUnloaded;
    }

    /// <summary>
    ///     Gets the app-wide ViewModel instance.
    /// </summary>
    public MainViewModel ViewModel => App.ViewModel;

    /// <summary>The quick-pick speeds under the default speed slider.</summary>
    public SpeedPick[] QuickPicks { get; } =
        PlaybackSpeedPresets.QuickPicks.Select(speed => new SpeedPick(speed, PlaybackSpeedPresets.Format(speed))).ToArray();

    private void DefaultSpeedSlower_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DefaultPlaybackSpeed -= PlaybackSpeedPresets.Step;

    private void DefaultSpeedFaster_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DefaultPlaybackSpeed += PlaybackSpeedPresets.Step;

    private void DefaultSpeedQuickPick_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SpeedPick pick) ViewModel.DefaultPlaybackSpeed = pick.Speed;
    }

    public static string Version => Constants.Version;

    private void OnSettingsPageLoaded(object sender, RoutedEventArgs e)
    {
        var currentTheme = ThemeHelper.RootTheme;
        switch (currentTheme)
        {
            case ElementTheme.Light:
                themeMode.SelectedIndex = 0;
                break;
            case ElementTheme.Dark:
                themeMode.SelectedIndex = 1;
                break;
            case ElementTheme.Default:
                themeMode.SelectedIndex = 2;
                break;
        }

        ProgressScopeChoice.SelectedIndex = (int)App.PlayerViewModel.ProgressScope;
        TimeDisplayChoice.SelectedIndex = (int)App.PlayerViewModel.TimeDisplay;

        // Loaded can come again for the same page; never subscribe twice
        App.Sync.Changed -= ShowSyncState;
        App.Sync.Changed += ShowSyncState;
        ShowSyncState();
        // nothing was read or written since the app started: do it now, so the status says something real
        if (App.Sync is { IsEnabled: true, LastSuccessUtc: null, LastError: null }) _ = App.Sync.SyncNowAsync();
    }

    private void OnSettingsPageUnloaded(object sender, RoutedEventArgs e) => App.Sync.Changed -= ShowSyncState;

    #region sync

    private void ShowSyncState()
    {
        var sync = App.Sync;
        SyncFolderText.Text = sync.FolderPath ?? "Not set";
        ToolTipService.SetToolTip(SyncFolderText, sync.FolderPath);
        TurnOffSyncButton.IsEnabled = sync.IsEnabled;

        if (!sync.IsEnabled)
            SyncStatusText.Text = "Off";
        else if (sync.LastError is { } error)
            SyncStatusText.Text = "Last sync failed: " + error;
        else if (sync.LastSuccessUtc is { } success)
            SyncStatusText.Text = "Last sync: " + success.ToLocalTime().ToString("g");
        else
            SyncStatusText.Text = "Not synced yet";

        var devices = sync.Others.Select(device => device.DeviceName)
            .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToList();
        SyncDevicesText.Visibility = sync.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        SyncDevicesText.Text = devices.Count == 0
            ? "No other devices found yet"
            : "Other devices: " + string.Join(", ", devices);
    }

    private async void ChooseSyncFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openPicker = new FolderPicker();
            var hWnd = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(openPicker, hWnd);
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.FileTypeFilter.Add("*");

            var folder = await openPicker.PickSingleFolderAsync();
            if (folder == null) return;
            if (string.IsNullOrWhiteSpace(folder.Path))
            {
                // libraries and other virtual places have no path in the file system
                ViewModel.EnqueueNotification(new Notification
                {
                    Message = "This location cannot be used as a sync folder. Please choose a regular folder.",
                    Severity = InfoBarSeverity.Warning
                });
                return;
            }

            App.Sync.SetFolder(folder.Path);
            ChooseSyncFolderButton.IsEnabled = false;
            try
            {
                // read the other devices and write this one right away, so the status below is real
                await App.Sync.SyncNowAsync();
            }
            finally
            {
                ChooseSyncFolderButton.IsEnabled = true;
            }

            ShowSyncState();
        }
        catch (Exception exception)
        {
            ViewModel.LoggingService.LogError(exception);
        }
    }

    private void TurnOffSync_Click(object sender, RoutedEventArgs e)
    {
        App.Sync.TurnOff();
        ShowSyncState();
    }

    #endregion

    private void ProgressScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProgressScopeChoice.SelectedIndex >= 0)
            App.PlayerViewModel.ProgressScope = (PlayerProgressScope)ProgressScopeChoice.SelectedIndex;
    }

    private void TimeDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimeDisplayChoice.SelectedIndex >= 0)
            App.PlayerViewModel.TimeDisplay = (PlayerTimeDisplay)TimeDisplayChoice.SelectedIndex;
    }

    private async void themeMode_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var currentTheme = ThemeHelper.RootTheme;
        var selectedTheme = ((ComboBoxItem)themeMode.SelectedItem)?.Tag?.ToString();
        var window = WindowHelper.GetWindowForElement(this);
        if (selectedTheme != null)
        {
            ThemeHelper.RootTheme = App.GetEnum<ElementTheme>(selectedTheme);
            if (selectedTheme == "Dark")
                TitleBarHelper.SetCaptionButtonColors(window, Colors.White);
            else if (selectedTheme == "Light" && currentTheme != ElementTheme.Light)
                TitleBarHelper.SetCaptionButtonColors(window, Colors.Black);
            else
                // todo: don't think i need the conditional here
                _ = TitleBarHelper.ApplySystemThemeToCaptionButtons(window) == Colors.White ? "Dark" : "Light";
        }
    }

    private async void bugRequestCard_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/rstewa/Audibly/issues/new/choose"));
    }

    private async void donateCard_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://paypal.me/rstewa35"));
    }

    private async void libationCard_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/rmcrackan/Libation"));
    }

    private async void openAudibleCard_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://openaudible.org/"));
    }

    private void contactCard_Click(object sender, RoutedEventArgs e)
    {
        // copy email to clipboard
        const string email = "help@audibly.info";
        var dataPackage = new DataPackage();
        dataPackage.SetText(email);
        Clipboard.SetContent(dataPackage);

        // change icon to checkmark
        DispatcherQueue.TryEnqueue(() => CopyIcon.Glyph = "\uE8FB");

        // wait 1 second and change back to copy icon
        Task.Delay(1000).ContinueWith(_ => DispatcherQueue.TryEnqueue(() => CopyIcon.Glyph = "\uE8C8"));
    }

    private void OpenAppStateFolderInFileExplorer_Click(object sender, RoutedEventArgs e)
    {
        var filePath = ApplicationData.Current.LocalFolder.Path;
        Process p = new();
        p.StartInfo.FileName = "explorer.exe";
        p.StartInfo.Arguments = $"/open, \"{filePath}\"";
        p.Start();
    }

    private async void ResetLibrary_Click(object sender, RoutedEventArgs e)
    {
        // show confirmation dialog
        var result = await DialogService.ShowConfirmationDialogAsync(
            "Reset Library",
            "Are you sure you want to reset your library? This will NOT delete your actual audiobook files but it WILL delete the records of them in Audibly's database. This action cannot be undone.",
            "Yes, Reset Library",
            "No, Cancel");
        if (result != ContentDialogResult.Primary) return;

        ViewModel.DeleteAudiobooksAsync();
    }

    private async void AddWatchedFolder_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.AddWatchedFolderAsync();
    }

    private void RemoveWatchedFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchedFolder folder)
            ViewModel.RemoveWatchedFolder(folder);
    }

    private void SyncLibrary_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(LibraryCardPage));
        _ = Task.Run(async () => await ViewModel.SyncLibraryAsync(showProgressDialog: true));
    }
}

/// <summary>One quick-pick button: the speed and how it is written.</summary>
public sealed class SpeedPick(double speed, string label)
{
    public double Speed { get; } = speed;
    public string Label { get; } = label;
}
