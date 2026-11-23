# Next Fork To-dos Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** PayPal link, multi-folder import, second progress line in the player, chapter dropdown + progress in the read-along reader (Windows app).

**Architecture:** Pure logic goes into `Audibly.Core` (tested in `Audibly.Audio.Tests`, xUnit) and `Audibly.ReadAlong` (tested in `Audibly.ReadAlong.Tests`, console `Check(...)` style). WinUI code stays thin and is checked by hand in the running app.

**Tech Stack:** .NET 8, WinUI 3 (Windows App SDK 1.7), CsWin32 0.3.183, xUnit 2.9, System.IO.Compression + System.Xml.Linq.

**Spec:** `docs/superpowers/specs/2026-11-23-next-todos-design.md`

## Global Constraints

- Windows app only; no Android changes.
- PayPal URL exactly `https://paypal.me/hartmannn`.
- Folder import: one-time import, existing per-folder rules (`AudiobookImporter.ScanDirectory`), duplicates skipped as today.
- Second progress line: three modes elapsed/total, remaining, percent (`PlayerTimeDisplay`); remaining time honours playback speed; mode saved as its own setting.
- Reader chapters from EPUB 3 `nav.xhtml` or EPUB 2 `toc.ncx`, fallback one chapter per `Href`; choosing a chapter scrolls the text and seeks audio to the first aligned cue of that chapter; progress by text position in characters.
- UI text in English (the app is English).
- Update `todo.md` (tick items) and `README.md` changelog in each feature task's commit, like earlier fork work.

## Review Focus

1. Parent and child folder both picked in the dialog → each book imported once, not twice (test in Task 2).
2. TOC entry with a fragment pointing into the middle of a file → chapter starts at the passage containing that point, not at the file start (test in Task 4).
3. TOC hrefs relative to `nav.xhtml` in a different folder than the content files, URL-encoded (`Chapter%201.xhtml`) → still matched (test in Task 4).
4. Chapter with no aligned cue before the next chapter starts → no audio seek (test in Task 4, `FirstCueIndex` returns null).
5. Book with one audio file and no chapters → second progress line hidden (test in Task 3).

---

### Task 1: PayPal link

**Files:**
- Modify: `Audibly.App/Views/SettingsPage.xaml.cs:215`
- Modify: `todo.md` (tick the PayPal item)

- [ ] **Step 1:** Replace `https://paypal.me/rstewa35` with `https://paypal.me/hartmannn`.
- [ ] **Step 2:** Build: `dotnet build Audibly.App -c Debug -p:Platform=x64` → `Build succeeded`.
- [ ] **Step 3:** Commit `Windows: PayPal link points to the fork author`.

### Task 2: Import several folders at once

**Files:**
- Modify: `Audibly.Core/Import/AudiobookImporter.cs` (add `ScanDirectories`)
- Test: `Audibly.Audio.Tests/AudiobookImporterTests.cs`
- Create: `Audibly.App/Helpers/FolderDialog.cs`
- Modify: `Audibly.App/NativeMethods.txt`
- Modify: `Audibly.App/Services/FileImportService.cs`, `Audibly.App/Services/Interfaces/IImportFiles.cs`
- Modify: `Audibly.App/ViewModels/MainViewModel.cs:1254-1310`

**Interfaces:**
- Produces: `AudiobookImporter.ScanDirectories(IEnumerable<string> paths, CancellationToken token) : IReadOnlyList<IReadOnlyList<string>>`
- Produces: `FolderDialog.PickFolders(IntPtr owner) : IReadOnlyList<string>` (empty list on cancel)
- Produces: `IImportFiles.ImportDirectoriesAsync(IReadOnlyList<string> paths, CancellationToken, Func<int,int,string,BookImportStatus,Task>)`

- [ ] **Step 1: Failing tests** in `AudiobookImporterTests` (use the existing temp-folder helpers there):
  - `ScanDirectoriesMergesSeveralFolders`: two folders, each with one book → 2 books, order = folder order.
  - `ScanDirectoriesSkipsBooksFoundTwice`: pick `library` and `library/BookA` → `BookA` appears once (compare by first file path, case-insensitive).
  - `ScanDirectoriesWithNoPathsIsEmpty`: `[]` → empty.
- [ ] **Step 2:** `dotnet test Audibly.Audio.Tests --filter ScanDirectories` → FAIL (method missing).
- [ ] **Step 3:** Implement `ScanDirectories`: call `ScanDirectory` per path, keep a book only if none of its files was already taken (`HashSet<string>(StringComparer.OrdinalIgnoreCase)`).
- [ ] **Step 4:** Same command → PASS.
- [ ] **Step 5:** `FolderDialog.PickFolders`: add `FileOpenDialog`, `IFileOpenDialog`, `IShellItemArray`, `SIGDN`, `FILEOPENDIALOGOPTIONS` to `NativeMethods.txt`; create the dialog, `SetOptions(current | FOS_PICKFOLDERS | FOS_ALLOWMULTISELECT | FOS_FORCEFILESYSTEM)`, `Show(owner)`; on `HRESULT_FROM_WIN32(ERROR_CANCELLED)` (0x800704C7) return empty; else `GetResults` → each item `GetDisplayName(SIGDN_FILESYSPATH)`.
- [ ] **Step 6:** `FileImportService.ImportDirectoriesAsync`: like `ImportDirectoryAsync` but scans with `ScanDirectories`; progress total across all folders. `ImportDirectoryAsync(path, …)` becomes `ImportDirectoriesAsync([path], …)`.
- [ ] **Step 7:** `MainViewModel.ImportAudiobooksFromDirectoryAsync`: replace `FolderPicker` + `FutureAccessList` with `FolderDialog.PickFolders(hWnd)`; return on empty list; call `ImportDirectoriesAsync`. Progress dialog prefix `"Scanning folders…"` when more than one.
- [ ] **Step 8:** Build + run app; import two book folders at once and a library folder together with one of its subfolders → both books imported, the duplicate reported as skipped. Cancel the dialog → nothing happens.
- [ ] **Step 9:** Tick the todo item, add README changelog line, commit `Windows: import several audiobook folders at once`.

### Task 3: Second progress line in the player

**Files:**
- Modify: `Audibly.Core/PlayerProgress.cs`
- Test: `Audibly.Audio.Tests/PlayerProgressTests.cs`
- Modify: `Audibly.App/Helpers/UserSettings.cs` (near line 236)
- Modify: `Audibly.App/ViewModels/PlayerViewModel.cs` (progress region, lines ~212-285)
- Modify: `Audibly.App/UserControls/NowPlayingBar.xaml` (+ `.xaml.cs`)

**Interfaces:**
- Produces: `PlayerProgress.Other(PlayerProgressScope scope) : PlayerProgressScope`
- Produces: `PlayerProgress.SecondLine(PlayerProgressScope scope, PlayerTimeDisplay display, double durationMs, double positionMs, double speed) : string` — format `"{Book|Chapter}  {elapsed} · {end}"`, where elapsed = `Elapsed(positionMs)` and end = `End(display, …)`; for `Remaining` the end part is `"{time} left"` without the minus sign; for `Percent` only `"{Book|Chapter}  {n} %"`.
- Produces (ViewModel): `SecondTimeDisplay` (`PlayerTimeDisplay`, saved as `UserSettings.PlayerSecondTimeDisplay`, default `Remaining`), `SecondProgressText` (string), `ShowSecondProgress` (bool), `CycleSecondTimeDisplay()`.

- [ ] **Step 1: Failing tests:**
  - `OtherScopeSwaps`: `Other(Chapter) == Book`, `Other(Book) == Chapter`.
  - `SecondLineElapsedAndTotal`: `SecondLine(Book, ElapsedAndTotal, 10h, 4h, 2) == "Book  4:00:00 · 10:00:00"`.
  - `SecondLineRemainingUsesSpeed`: `SecondLine(Book, Remaining, 10h, 4h, 2) == "Book  4:00:00 · 3:00:00 left"`.
  - `SecondLinePercent`: `SecondLine(Chapter, Percent, 1h, 0.5h, 1) == "Chapter  50 %"`.
- [ ] **Step 2:** `dotnet test Audibly.Audio.Tests --filter PlayerProgress` → FAIL.
- [ ] **Step 3:** Implement both methods in `PlayerProgress`.
- [ ] **Step 4:** Same command → PASS.
- [ ] **Step 5:** ViewModel: compute book and chapter (max, value) the way `ProgressMaximumMs`/`ProgressValueMs` do, but for `Other(ProgressScope)`. `ShowSecondProgress` = book has `Duration > 0` and its chapter range differs from the whole book (chapter duration < book duration − 1000 ms). Raise `SecondProgressText`/`ShowSecondProgress` in `NotifyProgressChanged` and on `SecondTimeDisplay` / `ProgressScope` change.
- [ ] **Step 6:** XAML: add a second grid row under the slider spanning all columns: a transparent `Button` (same style as `ProgressEndButton`, `AutomationId="SecondProgressButton"`, tooltip "Click to switch between total, time remaining and percent") holding a `TextBlock` (`CaptionTextBlockStyle`, `FontSize="14"`, secondary text brush) bound to `SecondProgressText`; `Visibility` bound to `ShowSecondProgress`; click → `CycleSecondTimeDisplay()`.
- [ ] **Step 7:** Run app: multi-chapter book shows `Book …` under a chapter bar; switching Settings → progress scope to book shows `Chapter …`; click cycles three modes and survives restart; single-file book without chapters shows no line; speed 2× halves remaining.
- [ ] **Step 8:** Tick todo, README changelog, commit `Windows: book and chapter progress at the same time in the player`.

### Task 4: Reader chapters — table of contents and progress logic

**Files:**
- Create: `Audibly.ReadAlong/ReadingChapters.cs`
- Test: `Audibly.ReadAlong.Tests/Program.cs` (new checks, build a small EPUB zip in a temp file inside the test)

**Interfaces:**
- Produces:
  ```csharp
  public sealed record ReadingChapter(string Title, int Depth, int FirstPassage);
  public sealed record ReadingPosition(int Chapter, double BookFraction, double ChapterFraction);
  public static class ReadingChapters
  {
      // map = the reading layout (display passages); reads TOC from map.Epub.Path
      public static Task<IReadOnlyList<ReadingChapter>> CreateAsync(AlignmentMap map, CancellationToken token = default);
      public static ReadingPosition Locate(IReadOnlyList<Passage> passages, IReadOnlyList<ReadingChapter> chapters, int topPassage);
      public static int? FirstCueIndex(IReadOnlyList<Passage> passages, IReadOnlyList<ReadingChapter> chapters, int chapter); // passage index with a cue, or null
  }
  ```

- [ ] **Step 1: Failing checks** (EPUB built in the test: `META-INF/container.xml` → `OEBPS/content.opf`, nav at `OEBPS/nav/nav.xhtml`, content `OEBPS/text/Chapter 1.xhtml`, `OEBPS/text/ch2.xhtml`; passages with `Href` as spine hrefs `text/Chapter 1.xhtml`, `text/ch2.xhtml`):
  - "toc titles in order": nav with `Chapter One` → `../text/Chapter%201.xhtml`, `Chapter Two` → `../text/ch2.xhtml`, `Part 2.1` (nested) → `../text/ch2.xhtml#sec` → titles `[Chapter One, Chapter Two, Part 2.1]`, depths `[0,0,1]`.
  - "fragment starts mid-file": `#sec` element is the 3rd paragraph of ch2 → `FirstPassage` = index of the passage holding that paragraph's text.
  - "epub2 ncx": same book with `toc.ncx` (spine `toc="ncx"`), no nav → same titles.
  - "file fallback": no nav and no ncx → one chapter per distinct `Href` in passage order, title = first passage text cut to 60 chars.
  - "unmatched toc entries dropped": entry pointing to a file not in passages is skipped; chapters sorted by `FirstPassage`, duplicates (same `FirstPassage`) keep the first.
  - "locate": passages of lengths 100/100/200/100 with chapters at 0 and 2; `Locate(…, 2)` → `Chapter 1`, `BookFraction 0.4`, `ChapterFraction 0`; `Locate(…, 3)` → `ChapterFraction 2/3`.
  - "first cue": chapter whose passages have no cues until the next chapter → `null`; otherwise index of first passage with `Cues.Count > 0`.
- [ ] **Step 2:** `dotnet run --project Audibly.ReadAlong.Tests` → fails at the first new check.
- [ ] **Step 3:** Implement. TOC location: `container.xml` rootfile → OPF; nav = manifest item with `properties` containing `nav`, use `<nav epub:type="toc">`, walk `ol/li` (depth = nesting), title = `a` text trimmed; else `spine/@toc` → ncx `navPoint` (`navLabel/text`, `content/@src`). Resolve every href to a full zip path (`posix` join with the TOC file's folder, `Uri.UnescapeDataString`, normalize `..`); resolve passage `Href` against the OPF folder the same way.
  Fragment → passage, since display passage fragments are Storyteller ids that the original file lacks:
  ```
  body text = concatenated text of <body> in the zip entry
  target   = text length before the element with id == fragment (document order)
  walk that file's passages in order, finding each passage.Text in body text with IndexOf from the last end;
  chapter starts at the last passage whose start <= target (first passage of file if none / text not found)
  ```
  `Locate`: prefix sums of `Text.Length`; chapter = last chapter with `FirstPassage <= topPassage`; fractions in [0,1]; empty chapter list → chapter 0, chapter fraction = book fraction.
- [ ] **Step 4:** Same command → all `PASS`.
- [ ] **Step 5:** Commit `Read along: chapters from the EPUB table of contents, reading position`.

### Task 5: Reader chapters — UI

**Files:**
- Modify: `Audibly.App/UserControls/ReadAlongPanel.xaml`
- Modify: `Audibly.App/UserControls/ReadAlongPanel.xaml.cs`

**Interfaces:**
- Consumes: `ReadingChapters.CreateAsync`, `Locate`, `FirstCueIndex` (Task 4); `App.PlayerViewModel.SeekReadAlongAsync(int sourceIndex, double seconds)`.

- [ ] **Step 1:** XAML: new row between the status row and the list: `ComboBox x:Name="ChapterChoice"` (`PlaceholderText="Chapter"`, `MinWidth="240"`, `AutomationProperties.Name="Chapter"`, items shown as title indented by `Depth * 16` px) and `TextBlock x:Name="ReadingProgressText"`. Both collapsed while `_map is null`.
- [ ] **Step 2:** Load: after `_map` is set in `LoadBookAsync` and `Generate_Click`, build chapters with `ReadingChapters.CreateAsync(_map, token)` inside the existing `Task.Run`; keep in `_chapters`; on failure use an empty list (no dropdown, progress still shown).
- [ ] **Step 3:** Choosing a chapter (user change only; guard with `_settingChapter` flag): `StopFollowing()`, `Passages.ScrollIntoView(passages[FirstPassage], Leading)`; if `FirstCueIndex` is not null → `SeekReadAlongAsync(cue.SourceIndex, cue.StartSeconds)` of that passage's first cue; else `ModeLabel.Text = "No narration found for this chapter yet"`. Errors → `DialogService.ShowErrorDialogAsync("Unable to seek", …)` like `ReadAlongPassage`.
- [ ] **Step 4:** Progress: on `_scroll.ViewChanged` and after following scrolls, read `(Passages.ItemsPanelRoot as ItemsStackPanel)?.FirstVisibleIndex`; `Locate`; set `ReadingProgressText = $"Book {floor(book*100)} % · Chapter {i+1}/{n} · {floor(chapter*100)} %"` (without the chapter part when there are no chapters) and set `ChapterChoice.SelectedIndex` under the guard.
- [ ] **Step 5:** Run app with a prepared read-along book: dropdown lists the EPUB's chapters; picking one scrolls text and audio jumps there; scrolling by hand updates dropdown and progress; follow narration keeps them in step; a book whose EPUB has no TOC shows file chapters.
- [ ] **Step 6:** Tick todo, README changelog, commit `Windows: chapter selection and reading progress in Read Along`.
