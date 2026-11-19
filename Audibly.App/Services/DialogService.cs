// Author: rstewa · https://github.com/rstewa
// Updated: 07/29/2025

using System;
using System.Threading;
using System.Threading.Tasks;
using Audibly.App.Extensions;
using Audibly.App.Helpers;
using Audibly.App.ViewModels;
using Audibly.App.Views.ContentDialogs;
using Audibly.App.Views.ControlPages;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Audibly.App.Services;

public static class DialogService
{
    private static readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    private static ContentDialog? _progressDialog;

    /// <summary>
    ///     Gets the app-wide ViewModel instance.
    /// </summary>
    public static MainViewModel ViewModel => App.ViewModel;

    private static XamlRoot? GetXamlRoot()
    {
        try
        {
            return App.Window?.Content?.XamlRoot;
        }
        catch (Exception e)
        {
            App.ViewModel.LoggingService.LogError(e, true);
            return null;
        }
    }

    // confirmation dialog with custom title and content
    internal static async Task<ContentDialogResult> ShowConfirmationDialogAsync(string title, string content,
        string primaryButtonText = "Yes", string closeButtonText = "No")
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return ContentDialogResult.None;

        var result = ContentDialogResult.None;
        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            var confirmationDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = closeButtonText,
                XamlRoot = App.Window.Content.XamlRoot,
                RequestedTheme = ThemeHelper.ActualTheme
            };

            result = await confirmationDialog.ShowOneAtATimeAsync();
        });

        return result;
    }

    /// <summary>
    ///     A question with two answers. True for the primary button, false for the close button or a
    ///     dismissed dialog, and null when the dialog could not be shown at all, so that "not asked"
    ///     is never mistaken for "no".
    /// </summary>
    internal static async Task<bool?> ShowChoiceDialogAsync(string title, string content,
        string primaryButtonText, string closeButtonText)
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return null;

        bool? choice = null;
        try
        {
            await _dispatcherQueue.EnqueueAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = primaryButtonText,
                    CloseButtonText = closeButtonText,
                    // a key pressed just as the dialog appears must not pick the answer that changes something
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = App.Window.Content.XamlRoot,
                    RequestedTheme = ThemeHelper.ActualTheme
                };

                // not ShowOneAtATimeAsync: it answers a failure to show with the same result as a closed
                // dialog, and it waits up to an hour behind another dialog (seen after an import, whose
                // progress dialog kept the queue). Waiting only briefly lets the caller ask again later.
                var (shown, result) = await DialogManager.TryOneAtATimeAsync(async () => await dialog.ShowAsync(),
                    TimeSpan.FromSeconds(5));
                if (shown) choice = result == ContentDialogResult.Primary;
            });
        }
        catch (Exception e)
        {
            App.ViewModel.LoggingService.LogError(e);
            return null;
        }

        return choice;
    }

    internal static async Task ShowErrorDialogAsync(string title, string content)
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return;

        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            var errorDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                XamlRoot = App.Window.Content.XamlRoot,
                RequestedTheme = ThemeHelper.ActualTheme
            }.SetPrimaryButton("OK");
            await errorDialog.ShowOneAtATimeAsync();
        });
    }

    internal static async Task ShowOkDialogAsync(string title, string content)
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return;

        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                RequestedTheme = ThemeHelper.ActualTheme,
                XamlRoot = App.Window.Content.XamlRoot
            }.SetPrimaryButton("OK");
            await dialog.ShowOneAtATimeAsync();
        });
    }

    internal static async Task ShowOnboardingDialogAsync()
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return;

        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            // show onboarding dialog
            var dialog = new ContentDialog
            {
                Title = "Welcome to Audibly!",
                Content = "We're glad you're here. Let's get started by adding your first audiobook.",
                CloseButtonText = "Ok",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = ThemeHelper.ActualTheme,
                XamlRoot = App.Window.Content.XamlRoot
            };
            await dialog.ShowOneAtATimeAsync();
        });
    }

    internal static async Task ShowChangelogDialogAsync()
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return;

        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            // show changelog dialog
            var dialog = new ChangelogContentDialog
            {
                XamlRoot = App.Window.Content.XamlRoot,
                RequestedTheme = ThemeHelper.ActualTheme
            };

            await dialog.ShowOneAtATimeAsync();
        });
    }

    internal static async Task<ContentDialogResult> ShowSelectFilesDialogAsync()
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return ContentDialogResult.None;

        var result = ContentDialogResult.None;
        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            var selectFilesDialog = new SelectFilesDialog();

            var contentDialog = new ContentDialog
            {
                Title = "Put Selected Files in Order (Drag and Drop)",
                Content = selectFilesDialog,
                PrimaryButtonText = "OK",
                CloseButtonText = "Cancel",
                XamlRoot = App.Window.Content.XamlRoot,
                MinWidth = selectFilesDialog.ActualWidth,
                RequestedTheme = ThemeHelper.ActualTheme
            };

            result = await contentDialog.ShowOneAtATimeAsync();
        });

        return result;
    }

    internal static async Task ShowDataMigrationRequiredDialogAsync()
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return;

        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = "Data Migration Required",
                Content =
                    "To ensure compatibility with the latest update, we need to migrate your data to the new database " +
                    "format. This process may take a few minutes depending on the size of your library. Do not close the app " +
                    "during this process.",
                DefaultButton = ContentDialogButton.Primary,
                PrimaryButtonText = "Migrate Data",
                XamlRoot = App.Window.Content.XamlRoot,
                RequestedTheme = ThemeHelper.ActualTheme
            }.SetPrimaryButton("Migrate Data", async (_, _) => await ViewModel.MigrateDatabase());
            await dialog.ShowOneAtATimeAsync();
        });
    }

    internal static async Task ShowDataMigrationFailedDialogAsync()
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return;

        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = "Data Migration Failed",
                Content =
                    "We were unable to migrate your data from the previous version of Audibly. Please contact support for assistance.",
                CloseButtonText = "Ok",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = ThemeHelper.ActualTheme,
                XamlRoot = App.Window.Content.XamlRoot
            };
            await dialog.ShowOneAtATimeAsync();
        });
    }

    internal static async Task ShowMoreInfoDialogAsync(AudiobookViewModel audiobookViewModel)
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return;

        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            var moreInfoDialog = new MoreInfoDialogContent(audiobookViewModel);

            var contentDialog = new ContentDialog
            {
                Title = "More Info",
                Content = moreInfoDialog,
                CloseButtonText = "Close",
                XamlRoot = App.Window.Content.XamlRoot,
                RequestedTheme = ThemeHelper.ActualTheme,
                MinWidth = moreInfoDialog.ActualWidth
            };

            // todo: decide if I want to use this
            // contentDialog.Background = (Brush)Application.Current.Resources["AcrylicBackgroundFillColorBaseBrush"];

            await contentDialog.ShowOneAtATimeAsync();
        });
    }

    /// <summary>
    ///     Looks a book up on Audible and saves the details the user chose. Nothing changes when the
    ///     dialog is cancelled or saving fails.
    /// </summary>
    internal static async Task ShowLookUpOnlineDialogAsync(AudiobookViewModel book)
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return;

        await _dispatcherQueue.EnqueueAsync(async () =>
        {
            var dialog = new LookUpOnlineDialog(book)
            {
                XamlRoot = xamlRoot,
                RequestedTheme = ThemeHelper.ActualTheme
            };
            await dialog.ShowOneAtATimeAsync();
            if (dialog.Result is not { } metadata) return;

            try
            {
                await Task.Run(() => App.Repository.Audiobooks.SaveMetadataAsync(book.Id, metadata));
                book.ApplyMetadata(metadata);
            }
            catch (Exception e)
            {
                ViewModel.LoggingService.LogError(e);
                ViewModel.EnqueueNotification(new Notification
                {
                    Message = "The book details could not be saved.",
                    Severity = InfoBarSeverity.Error
                });
            }
        });
    }

    internal static async Task ShowProgressDialogAsync(string title, CancellationTokenSource? cts,
        bool showCancelButton = true)
    {
        var xamlRoot = GetXamlRoot();
        if (xamlRoot == null) return;

        // yes, I'm intentionally not awaiting this
        _dispatcherQueue.EnqueueAsync(async () =>
        {
            _progressDialog = new ProgressContentDialog(cts)
            {
                Title = title,
                RequestedTheme = ThemeHelper.ActualTheme,
                XamlRoot = App.Window.Content.XamlRoot
            };

            if (showCancelButton)
            {
                _progressDialog.DefaultButton = ContentDialogButton.Close;
                _progressDialog.SetCloseButton("Cancel");
            }

            // todo: should i pass cts to ShowOneAtATimeAsync?
            await _progressDialog.ShowOneAtATimeAsync();
        });
    }

    internal static async Task CloseProgressDialogAsync()
    {
        await _dispatcherQueue.EnqueueAsync(() =>
        {
            if (_progressDialog == null) return;
            _progressDialog.Hide();
            _progressDialog = null;
        });
    }
}