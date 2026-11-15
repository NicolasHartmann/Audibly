using Android.Content;
using AndroidX.DocumentFile.Provider;
using Audibly.Mobile.Services;
using Audibly.Sync;
using Uri = Android.Net.Uri;

namespace Audibly.Mobile;

/// <summary>
///     The sync folder on Android: any folder the system's folder dialog offers, for example one
///     that a cloud app keeps in sync. The app keeps read and write permission for it.
/// </summary>
public sealed class AndroidSyncFolderAccess : ISyncFolderAccess
{
    private const string FolderKey = "SyncFolderUri";
    private const string FolderNameKey = "SyncFolderName";
    internal const int PickFolderRequest = 4712;
    private const ActivityFlags ReadWrite = ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission;

    private static TaskCompletionSource<Uri?>? _pick;
    private readonly Context _context = Android.App.Application.Context;

    public string? FolderName => Preferences.Default.Get<string?>(FolderNameKey, null);

    public bool HasAccess
    {
        get
        {
            var stored = Preferences.Default.Get<string?>(FolderKey, null);
            if (stored == null) return true;
            try
            {
                return _context.ContentResolver!.PersistedUriPermissions.Any(permission =>
                    permission.IsReadPermission && permission.IsWritePermission && permission.Uri?.ToString() == stored);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public async Task<bool> PickAsync()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null) return false;

        _pick = new TaskCompletionSource<Uri?>();
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ReadWrite | ActivityFlags.GrantPersistableUriPermission);
        activity.StartActivityForResult(intent, PickFolderRequest);
        var uri = await _pick.Task;
        if (uri == null) return false;

        // without this the permission ends when the app is closed
        _context.ContentResolver!.TakePersistableUriPermission(uri, ReadWrite);
        Preferences.Default.Set(FolderKey, uri.ToString());
        Preferences.Default.Set(FolderNameKey, DocumentFile.FromTreeUri(_context, uri)?.Name ?? "Sync folder");
        return true;
    }

    /// <summary>Called by the activity with the result of the folder dialog.</summary>
    internal static void OnFolderPicked(Uri? uri) => _pick?.TrySetResult(uri);

    public ISyncFolder? Open()
    {
        var stored = Preferences.Default.Get<string?>(FolderKey, null);
        return stored == null ? null : new TreeFolder(_context, Uri.Parse(stored)!);
    }

    public void Forget()
    {
        var stored = Preferences.Default.Get<string?>(FolderKey, null);
        if (stored != null)
            try
            {
                _context.ContentResolver!.ReleasePersistableUriPermission(Uri.Parse(stored)!, ReadWrite);
            }
            catch (Exception)
            {
                // the permission was already gone
            }

        Preferences.Default.Remove(FolderKey);
        Preferences.Default.Remove(FolderNameKey);
    }

    /// <summary>The device files inside the picked folder, in the subfolder all devices share.</summary>
    private sealed class TreeFolder(Context context, Uri tree) : ISyncFolder
    {
        public Task<IReadOnlyList<string>> ListAsync() => Task.Run<IReadOnlyList<string>>(() =>
            Folder(create: false)?.ListFiles()
                .Where(file => file.IsFile && file.Name != null)
                .Select(file => file.Name!)
                .ToList() ?? []);

        public Task<string?> ReadAsync(string fileName) => Task.Run(() =>
        {
            var file = Folder(create: false)?.FindFile(fileName);
            if (file == null || !file.IsFile) return null;
            using var input = context.ContentResolver!.OpenInputStream(file.Uri)
                              ?? throw new IOException("The sync file could not be opened: " + fileName);
            using var reader = new StreamReader(input);
            return reader.ReadToEnd();
        });

        public Task WriteAsync(string fileName, string content) => Task.Run(() =>
        {
            var folder = Folder(create: true) ?? throw new IOException("The sync folder is not available.");
            // the type is deliberately generic: with a specific one some providers append their own extension
            var file = folder.FindFile(fileName) ?? folder.CreateFile("application/octet-stream", fileName)
                ?? throw new IOException("The sync file could not be created: " + fileName);

            // "wt" replaces the old content instead of writing over its beginning
            using var output = context.ContentResolver!.OpenOutputStream(file.Uri, "wt")
                               ?? throw new IOException("The sync file could not be written: " + fileName);
            using var writer = new StreamWriter(output);
            writer.Write(content);
        });

        private DocumentFile? Folder(bool create)
        {
            var root = DocumentFile.FromTreeUri(context, tree);
            if (root == null || !root.Exists()) throw new IOException("The sync folder is not available.");
            var folder = root.FindFile(DeviceStateFile.Folder);
            if (folder is { IsDirectory: true }) return folder;
            return create ? root.CreateDirectory(DeviceStateFile.Folder) : null;
        }
    }
}
