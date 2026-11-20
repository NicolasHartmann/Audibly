using System;
using System.Collections.Generic;
using System.Linq;
using Audibly.App.ViewModels;
using Audibly.Core.Tags;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Audibly.App.Views.ContentDialogs;

/// <summary>
///     Edits the tags of one or many books. Saves nothing itself: after Save, <see cref="Added" /> and
///     <see cref="Removed" /> say what to change on every book, so a tag only some books have stays on
///     the others unless it was removed here.
/// </summary>
public sealed partial class EditTagsDialog : ContentDialog
{
    private static readonly StringComparer Same = StringComparer.CurrentCultureIgnoreCase;

    private readonly IReadOnlyList<AudiobookViewModel> _books;
    private readonly IReadOnlyList<string> _known;
    private readonly List<string> _added = [];
    private readonly HashSet<string> _removed = new(Same);

    public EditTagsDialog(IReadOnlyList<AudiobookViewModel> books, IReadOnlyList<string> known)
    {
        InitializeComponent();
        _books = books;
        _known = known;
        Title = books.Count == 1 ? $"Edit tags – {books[0].Title}" : $"Edit tags – {books.Count} books";
        Refresh();
    }

    /// <summary>Tags to add to every book; null when the dialog was cancelled.</summary>
    public IReadOnlyList<string>? Added { get; private set; }

    /// <summary>Tags to remove from every book; null when the dialog was cancelled.</summary>
    public IReadOnlyList<string>? Removed { get; private set; }

    private void Refresh()
    {
        var rows = new List<TagRow>();
        foreach (var tag in TagList.AllTags(_books.Select(book => TagList.Format(book.Tags)).Append(TagList.Format(_added))))
        {
            if (_removed.Contains(tag)) continue;
            var count = _added.Contains(tag, Same) ? _books.Count : _books.Count(book => book.Tags.Contains(tag, Same));
            rows.Add(count == _books.Count
                ? new TagRow(tag, string.Empty, 1)
                : new TagRow(tag, $"{count} of {_books.Count}", 0.6));
        }

        TagRows.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TagBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var text = sender.Text.Trim();
        sender.ItemsSource = text.Length == 0
            ? null
            : _known.Where(tag => tag.Contains(text, StringComparison.CurrentCultureIgnoreCase)).Take(10).ToList();
    }

    private void TagBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var typed = (args.ChosenSuggestion as string ?? sender.Text).Trim();
        if (typed.Length == 0) return;

        var tag = TagList.Add([], [typed], _known.Concat(_books.SelectMany(book => book.Tags)))[0];
        _removed.Remove(tag);
        if (!_added.Contains(tag, Same)) _added.Add(tag);
        sender.Text = string.Empty;
        sender.ItemsSource = null;
        Refresh();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string tag) return;
        _added.RemoveAll(added => Same.Equals(added, tag));
        _removed.Add(tag);
        Refresh();
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // a tag typed but not yet confirmed with Enter still counts
        if (TagBox.Text.Trim().Length > 0)
            TagBox_QuerySubmitted(TagBox, new AutoSuggestBoxQuerySubmittedEventArgs());
        Added = _added.ToList();
        Removed = _removed.ToList();
    }
}

/// <summary>One tag in the dialog; <see cref="Info" /> says how many of the books have it.</summary>
public sealed class TagRow(string tag, string info, double opacity)
{
    public string Tag { get; } = tag;
    public string Info { get; } = info;
    public double Opacity { get; } = opacity;
}
