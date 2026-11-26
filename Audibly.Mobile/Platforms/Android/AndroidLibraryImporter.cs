using Android.Content;
using Android.OS;
using AndroidX.DocumentFile.Provider;
using Audibly.Core.Import;
using Audibly.Mobile.Services;
using Microsoft.Win32.SafeHandles;
using Uri = Android.Net.Uri;

namespace Audibly.Mobile;

/// <summary>
///     Imports from a folder picked in Android's own folder dialog. The app keeps the permission
///     for that folder and stores a content URI for every file instead of a path.
/// </summary>
public sealed class AndroidLibraryImporter(AppData data) : ILibraryImporter
{
    private const string FolderKey = "LibraryFolderUri";
    private const string FolderNameKey = "LibraryFolderName";
    private const string IgnoredKey = "LibraryIgnored";
    private const ActivityFlags ReadWrite = ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission;
    internal const int PickFolderRequest = 4711;

    private static TaskCompletionSource<Uri?>? _pick;
    private readonly Context _context = Android.App.Application.Context;

    public string? FolderName => Preferences.Default.Get<string?>(FolderNameKey, null);

    public bool HasFolderAccess
    {
        get
        {
            var stored = Preferences.Default.Get<string?>(FolderKey, null);
            if (stored == null) return true;
            try
            {
                return _context.ContentResolver!.PersistedUriPermissions
                    .Any(permission => permission.IsReadPermission && permission.Uri?.ToString() == stored);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public async Task<bool> PickFolderAsync()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null) return false;

        _pick = new TaskCompletionSource<Uri?>();
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        // write as well as read: removing a book can also delete its files
        intent.AddFlags(ReadWrite | ActivityFlags.GrantPersistableUriPermission);
        activity.StartActivityForResult(intent, PickFolderRequest);
        var uri = await _pick.Task;
        if (uri == null) return false;

        // without this the permission ends when the app is closed
        _context.ContentResolver!.TakePersistableUriPermission(uri, ReadWrite);
        Preferences.Default.Set(FolderKey, uri.ToString());
        Preferences.Default.Set(FolderNameKey, DocumentFile.FromTreeUri(_context, uri)?.Name ?? "Audiobooks");
        return true;
    }

    /// <summary>Called by the activity with the result of the folder dialog.</summary>
    internal static void OnFolderPicked(Uri? uri) => _pick?.TrySetResult(uri);

    public Task<ImportSummary> ScanAsync(Action<string>? progress = null) => Task.Run(async () =>
    {
        var stored = Preferences.Default.Get<string?>(FolderKey, null);
        var root = stored == null ? null : DocumentFile.FromTreeUri(_context, Uri.Parse(stored)!);
        if (root == null || !root.Exists()) return new ImportSummary(0, 0, 0);

        var entries = new List<(string Folder, string FileName, DocumentFile File)>();
        Collect(root, "root", entries);
        var books = AudiobookImporter.GroupIntoBooks(entries, "root");

        int added = 0, known = 0, failed = 0;
        var ignored = IgnoredLocations();
        foreach (var files in books)
            try
            {
                var first = files[0].Uri.ToString()!;
                if (ignored.Contains(first)) continue;
                if (await data.Repository.Audiobooks.GetByFilePathAsync(first) != null)
                {
                    known++;
                    continue;
                }

                progress?.Invoke(files[0].Name ?? string.Empty);
                var imported = AudiobookImporter.Create(files
                    .Select(file => new AudioFileSource(file.Uri.ToString()!, file.Name ?? "audio", () => Open(file.Uri)))
                    .ToList());
                var book = imported.Audiobook;
                if (await data.Repository.Audiobooks.GetByTitleAuthorComposerAsync(book.Title, book.Author,
                        book.Composer) != null)
                {
                    known++;
                    continue;
                }

                book.CoverImagePath = book.ThumbnailPath = SaveCover(book.Id, imported.CoverImage);
                await data.Repository.Audiobooks.UpsertAsync(book);
                // the repository silently keeps an existing book with the same title and author
                if (await data.Repository.Audiobooks.GetByFilePathAsync(first) != null) added++;
                else failed++;
            }
            catch (Exception e)
            {
                failed++;
                System.Diagnostics.Debug.WriteLine("[Import] " + e);
            }

        return new ImportSummary(added, known, failed);
    });

    public bool CanDeleteFiles
    {
        get
        {
            var stored = Preferences.Default.Get<string?>(FolderKey, null);
            if (stored == null) return false;
            try
            {
                return _context.ContentResolver!.PersistedUriPermissions.Any(permission =>
                    permission.IsWritePermission && permission.Uri?.ToString() == stored);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public Task<int> DeleteFilesAsync(IReadOnlyList<string> locations) => Task.Run(() =>
    {
        var failed = 0;
        DocumentFile? parent = null;
        foreach (var location in locations)
            try
            {
                var uri = Uri.Parse(location)!;
                var file = DocumentFile.FromSingleUri(_context, uri);
                if (file == null || !file.Exists()) continue;
                parent ??= ParentOf(location);
                if (!Android.Provider.DocumentsContract.DeleteDocument(_context.ContentResolver!, uri)) failed++;
            }
            catch (Exception e)
            {
                failed++;
                System.Diagnostics.Debug.WriteLine("[Delete] " + e);
            }

        // a book of several files lives in its own folder; an empty one is removed too, never the chosen folder
        try
        {
            if (parent != null && parent.Exists() && parent.ListFiles().Length == 0 && !IsLibraryRoot(parent))
                parent.Delete();
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine("[Delete] " + e);
        }

        return failed;
    });

    public void Ignore(string firstLocation)
    {
        var ignored = IgnoredLocations();
        ignored.Add(firstLocation);
        Preferences.Default.Set(IgnoredKey, string.Join("\n", ignored));
    }

    public int IgnoredCount => IgnoredLocations().Count;

    public void ClearIgnored() => Preferences.Default.Remove(IgnoredKey);

    private static HashSet<string> IgnoredLocations() =>
        Preferences.Default.Get(IgnoredKey, string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

    public Task<byte[]?> ReadFolderCoverAsync(string firstLocation) => Task.Run(() =>
    {
        try
        {
            // only a book in its own folder: an image in the chosen folder itself belongs to no single book
            var folder = ParentOf(firstLocation);
            if (folder == null) return null;
            var images = folder.ListFiles().Where(file => file.IsFile && file.Name != null).ToList();
            var name = FolderCover.Pick(images.Select(file => file.Name!));
            var image = images.FirstOrDefault(file => file.Name == name);
            if (image == null) return null;

            using var stream = _context.ContentResolver!.OpenInputStream(image.Uri)
                               ?? throw new IOException("The image could not be opened.");
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine("[Cover] " + e);
            return (byte[]?)null;
        }
    });

    private DocumentFile? ParentOf(string location)
    {
        // walked down from the chosen folder: only a folder found that way can be listed and deleted
        var stored = Preferences.Default.Get<string?>(FolderKey, null);
        if (stored == null) return null;
        var tree = Uri.Parse(stored)!;
        var rootId = Android.Provider.DocumentsContract.GetTreeDocumentId(tree);
        var documentId = Android.Provider.DocumentsContract.GetDocumentId(Uri.Parse(location)!);
        if (rootId == null || documentId == null || !documentId.StartsWith(rootId + "/", StringComparison.Ordinal))
            return null;

        var relative = documentId[(rootId.Length + 1)..];
        var slash = relative.LastIndexOf('/');
        if (slash <= 0) return null;

        var folder = DocumentFile.FromTreeUri(_context, tree);
        foreach (var name in relative[..slash].Split('/'))
        {
            folder = folder?.FindFile(name);
            if (folder is not { IsDirectory: true }) return null;
        }

        return folder;
    }

    private bool IsLibraryRoot(DocumentFile folder)
    {
        var stored = Preferences.Default.Get<string?>(FolderKey, null);
        if (stored == null) return true;
        var rootId = Android.Provider.DocumentsContract.GetTreeDocumentId(Uri.Parse(stored)!);
        var folderId = Android.Provider.DocumentsContract.GetDocumentId(folder.Uri);
        return rootId == folderId;
    }

    public bool IsAvailable(string location)
    {
        try
        {
            return DocumentFile.FromSingleUri(_context, Uri.Parse(location)!)?.Exists() ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void Collect(DocumentFile folder, string path, List<(string, string, DocumentFile)> entries)
    {
        foreach (var child in folder.ListFiles())
            if (child.IsDirectory) Collect(child, path + "/" + child.Name, entries);
            else if (child.Name is { } name) entries.Add((path, name, child));
    }

    /// <summary>A seekable stream for a content URI; the tag reader needs to jump around in the file.</summary>
    private Stream Open(Uri uri)
    {
        var descriptor = _context.ContentResolver!.OpenFileDescriptor(uri, "r")
                         ?? throw new IOException("The file could not be opened: " + uri);
        return new DescriptorStream(descriptor);
    }

    private static string SaveCover(Guid bookId, byte[]? image)
    {
        if (image == null || image.Length == 0) return string.Empty;
        var folder = Path.Combine(FileSystem.AppDataDirectory, "covers");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, bookId.ToString("N") + ".img");
        File.WriteAllBytes(path, image);
        return path;
    }

    private sealed class DescriptorStream(ParcelFileDescriptor descriptor)
        : FileStream(new SafeFileHandle(descriptor.Fd, false), FileAccess.Read)
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) descriptor.Close();
        }
    }
}
