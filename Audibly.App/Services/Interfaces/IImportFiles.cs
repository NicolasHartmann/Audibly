// Author: rstewa · https://github.com/rstewa
// Created: 04/15/2024
// Updated: 10/03/2024

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Audibly.App.Services.Interfaces;

public enum BookImportStatus
{
    Imported,
    AlreadyInLibrary,
    Failed
}

public interface IImportFiles
{
    public delegate void ImportCompletedHandler();

    public event ImportCompletedHandler ImportCompleted;

    Task ImportDirectoryAsync(string path, CancellationToken cancellationToken,
        Func<int, int, string, BookImportStatus, Task> progressCallback);

    /// <summary>Imports several picked folders at once (see AudiobookImporter.ScanDirectories).</summary>
    Task ImportDirectoriesAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken,
        Func<int, int, string, BookImportStatus, Task> progressCallback);

    Task ImportFileAsync(string path, CancellationToken cancellationToken,
        Func<int, int, string, bool, Task> progressCallback);

    Task ImportFromMultipleFilesAsync(string[] paths, CancellationToken cancellationToken,
        Func<int, int, string, bool, Task> progressCallback);

    Task ImportFromJsonAsync(StorageFile file, CancellationToken cancellationToken,
        Func<int, int, string, bool, Task> progressCallback);
}
