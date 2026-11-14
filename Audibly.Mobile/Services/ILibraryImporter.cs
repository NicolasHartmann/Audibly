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
}
