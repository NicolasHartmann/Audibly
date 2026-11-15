using Audibly.Sync;

namespace Audibly.Mobile.Services;

/// <summary>The folder the listener picked for syncing, as the platform gives access to it.</summary>
public interface ISyncFolderAccess
{
    /// <summary>Name of the picked folder, or null when sync is off.</summary>
    string? FolderName { get; }

    /// <summary>False when a folder was picked but the system no longer lets the app use it.</summary>
    bool HasAccess { get; }

    /// <summary>Lets the listener pick the folder. False when they cancelled.</summary>
    Task<bool> PickAsync();

    /// <summary>The picked folder, or null when none is picked.</summary>
    ISyncFolder? Open();

    /// <summary>Turns sync off. Files in the folder are left alone.</summary>
    void Forget();
}
