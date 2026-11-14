# Audibly for Android (Playback and Library) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** An Android app that imports audiobooks from a folder on the phone, shows them in a library and plays them, also in the background.

**Architecture:** Platform-neutral logic moves into a new library `Audibly.Core` that the Windows app and the Android app share. The Android app (`Audibly.Mobile`, .NET MAUI) owns only UI, file access and an Android playback service around ExoPlayer.

**Tech Stack:** .NET 8 libraries (`Audibly.Core`, `Audibly.Models`, `Audibly.Repository`, `Audibly.Stats`), .NET 10 MAUI for Android, AndroidX Media3 (ExoPlayer, MediaSessionService), z440.atl.core for tags, xunit.

**Spec:** `docs/superpowers/specs/2026-11-14-android-app-design.md`

## Global Constraints

- `Audibly.sln` keeps building with the .NET 8 SDK; the MAUI project lives in its own `Audibly.Mobile.sln`.
- The Windows app's behavior does not change in this plan.
- UI texts are English.
- Minimum Android version: API 26.
- Tools are installed under `D:\AndroidDev` only; the system-wide .NET install is not modified.
- Elapsed and total times are audio time; only remaining time divides by the speed (same rule as on Windows).

## Review Focus

- Playback continues with the screen off and after the app's UI is closed; the position is saved when Android kills the process.
- A multi-file book crosses file boundaries without a gap and keeps the right chapter title.
- The folder permission is lost (folder deleted, storage unmounted): the library shows the book as unavailable instead of crashing.
- A file with no tags, no cover or no chapters still imports with the file name as title and one chapter.
- Audio focus: a phone call or another player pauses playback, and it does not resume on its own at full volume.

---

### Task 1: `Audibly.Core` with the shared helpers

**Files:** Create `Audibly.Core/Audibly.Core.csproj` (net8.0). Move `PlaybackTimeRemaining.cs`, `PlayerProgress.cs`, `PlaybackSpeedPresets.cs`, `ChapterSleepTimer.cs`, `FileChapterTitles.cs` from `Audibly.App/Helpers/` to `Audibly.Core/`, namespace `Audibly.Core`. Modify `Audibly.App/Audibly.App.csproj`, `Audibly.Audio.Tests/Audibly.Audio.Tests.csproj` (project reference instead of linked files), `Audibly.sln`, and the `using` lines of every file that uses the helpers.

**Produces:** the five helpers under namespace `Audibly.Core`, signatures unchanged.

Verify: `dotnet test Audibly.Audio.Tests` (67 passed), the Windows app compiles.

### Task 2: Importer on streams

**Files:** Create `Audibly.Core/Import/AudioFileSource.cs`, `Audibly.Core/Import/AudiobookImporter.cs`; test `Audibly.Audio.Tests/AudiobookImporterTests.cs`.

**Produces:**

- `record AudioFileSource(string Location, string FileName, Func<Stream> Open)`
- `record ImportedBook(Audiobook Audiobook, byte[]? CoverImage)`
- `static ImportedBook AudiobookImporter.Create(IReadOnlyList<AudioFileSource> files)` — files in the given order; title, author, composer, description and release date from the first file; one `SourceFile` per file with `FilePath = Location`; chapters from tags, or one chapter per file without them; titles through `FileChapterTitles.Resolve`; a missing title falls back to the file name without extension; `PlaybackSpeed = 1`, `Volume = 1`, `CurrentChapterIndex = 0`.
- `static IReadOnlyList<IReadOnlyList<T>> AudiobookImporter.GroupIntoBooks<T>(IEnumerable<(string Folder, string FileName, T File)> entries, string rootFolder)` — a subfolder with audio files is one book (files sorted by natural name order); each audio file directly in the root is its own book; extensions `.m4b`, `.mp3`, `.m4a`.

Tests generate small silent WAV files in a temp folder and write their tags with ATL. They cover: single file, three files with identical titles, no tags (file name becomes the title, one chapter), grouping rules, natural sort (`2` before `10`).

### Task 3: Position logic shared by both players

**Files:** Create `Audibly.Core/PlaybackPosition.cs`; test `Audibly.Audio.Tests/PlaybackPositionTests.cs`.

**Produces:**

- `static ChapterInfo? ChapterAt(IReadOnlyList<ChapterInfo> chapters, int sourceIndex, double positionMs)`
- `static (int SourceIndex, double PositionMs)? Skip(IReadOnlyList<double> sourceDurationsSeconds, int sourceIndex, double positionMs, double deltaSeconds)` — clamps to the start and the end of the book and crosses files
- `static ChapterInfo? Neighbor(IReadOnlyList<ChapterInfo> chapters, int currentIndex, int direction)`

### Task 4: Tools

Install into `D:\AndroidDev`: .NET 10 SDK (`dotnet-install`), workload `maui-android`, Microsoft OpenJDK 17, Android SDK (platform, build-tools, platform-tools, emulator, one x86_64 system image) through `-t:InstallAndroidDependencies`. Needs the user's consent because it accepts the Android SDK license and downloads several GB.

Verify: `dotnet new maui` builds an APK and starts in the emulator.

### Task 5: Playback spike

A throwaway MAUI app with a `MediaSessionService` hosting ExoPlayer that plays one bundled file, keeps playing with the screen off, shows the media notification and changes speed. Settles the Media3 binding packages and the service wiring before the real app is written. What it teaches goes into Task 7.

### Task 6: App skeleton and data

**Files:** Create `Audibly.Mobile/` (MAUI single project, Android only), `Audibly.Mobile.sln`. Database and `stats.db` in the app's private storage, `Audibly.Repository` migrations applied at start, shell with the three pages.

### Task 7: Playback service

`IPlaybackService` (open book at position, play, pause, seek, speed, sleep timer, events for position, state and completion) implemented by the Android service from the spike. Multi-file books as a playlist. Saves the position on pause, on a timer and when the task is removed. Feeds `ListeningStatsRecorder`.

### Task 8: Folder import and library page

Folder picker (Storage Access Framework), persisted permission, scan through `AudiobookImporter`, covers written to private storage. Library grid with cover, title, author, progress; rescan; unavailable books marked.

### Task 9: Player page, mini bar and settings

Player page with all controls from the spec, progress bar and time display through `PlayerProgress`, chapter list, speed sheet with `PlaybackSpeedPresets`, sleep timer. Mini bar on the library page. Settings page.

### Task 10: Device check

Emulator run of every goal in the spec; APK for the user's phone with install instructions.
