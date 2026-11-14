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
    internal const int PickFolderRequest = 4711;

    private static TaskCompletionSource<Uri?>? _pick;
    private readonly Context _context = Android.App.Application.Context;

    public string? FolderName => Preferences.Default.Get<string?>(FolderNameKey, null);

    public async Task<bool> PickFolderAsync()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null) return false;

        _pick = new TaskCompletionSource<Uri?>();
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
        activity.StartActivityForResult(intent, PickFolderRequest);
        var uri = await _pick.Task;
        if (uri == null) return false;

        // without this the permission ends when the app is closed
        _context.ContentResolver!.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
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
        foreach (var files in books)
            try
            {
                var first = files[0].Uri.ToString()!;
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
