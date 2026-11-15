namespace Audibly.Mobile.Services;

/// <summary>What a scan of the audiobook folder did.</summary>
public sealed record ImportSummary(int Added, int AlreadyInLibrary, int Failed);

/// <summary>Finds audiobooks in the folder the listener picked and adds them to the library.</summary>
public interface ILibraryImporter
{
    /// <summary>Name of the picked folder, or null when none was picked yet.</summary>
    string? FolderName { get; }

    /// <summary>
    ///     False when a folder was picked but Android no longer lets the app read it. Choosing the
    ///     same folder again restores access to the books that are already in the library.
    /// </summary>
    bool HasFolderAccess { get; }

    /// <summary>Lets the listener pick the folder. False when they cancelled.</summary>
    Task<bool> PickFolderAsync();

    /// <summary>Adds every audiobook in the folder that is not in the library yet.</summary>
    Task<ImportSummary> ScanAsync(Action<string>? progress = null);

    /// <summary>True when the file behind a stored location can still be opened.</summary>
    bool IsAvailable(string location);

    /// <summary>
    ///     True when the app may delete files in the audiobook folder. Folders chosen before
    ///     deleting existed were granted for reading only and have to be chosen again.
    /// </summary>
    bool CanDeleteFiles { get; }

    /// <summary>
    ///     Deletes a book's files from the phone, and its folder when nothing else is left in it.
    ///     Returns how many files could not be deleted.
    /// </summary>
    Task<int> DeleteFilesAsync(IReadOnlyList<string> locations);

    /// <summary>A removed book is not added again by the next scan while its files are still there.</summary>
    void Ignore(string firstLocation);

    /// <summary>How many removed books the scan currently leaves out.</summary>
    int IgnoredCount { get; }

    /// <summary>Lets the next scan add removed books again whose files are still in the folder.</summary>
    void ClearIgnored();
}
