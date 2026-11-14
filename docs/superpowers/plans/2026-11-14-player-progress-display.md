# Player Progress Display Implementation Plan

**Goal:** The player's progress bar can cover the chapter or the whole title, and its time display can show elapsed/total, time remaining or percent.

**Architecture:** A pure helper (`PlayerProgress`) does all calculation and formatting and is tested in `Audibly.Audio.Tests` via a linked file. `PlayerViewModel` exposes four display properties that both players bind to; the two choices live in `UserSettings`.

**Spec:** `docs/superpowers/specs/2026-11-14-player-progress-display-design.md`

## Global Constraints

- Defaults reproduce today's player: chapter scope, elapsed and total.
- Elapsed and total are audio time; only remaining time divides by the playback speed.
- UI texts are English.
- `Views/Legacy` is not touched.

## Review Focus

- Dragging the whole-title bar in a multi-file book lands in the right file at the right offset, including exactly on a file boundary and at the very end.
- A book with zero duration or no files does not divide by zero or throw.
- Changing speed updates the remaining time at once, also while paused.
- A stored setting value outside the enum falls back to the default.
- While the user drags the bar, position updates do not move the thumb.

---

### Task 1: `PlayerProgress` helper

**Files:** Create `Audibly.App/Helpers/PlayerProgress.cs`; test `Audibly.Audio.Tests/PlayerProgressTests.cs`; link the helper in `Audibly.Audio.Tests/Audibly.Audio.Tests.csproj`.

**Produces:**

- `enum PlayerProgressScope { Chapter = 0, Book = 1 }`, `enum PlayerTimeDisplay { ElapsedAndTotal = 0, Remaining = 1, Percent = 2 }`
- `string Elapsed(double positionMs)` — `h:mm:ss`, negative or non-finite is `0:00:00`
- `string End(PlayerTimeDisplay display, double durationMs, double positionMs, double speed)` — total as `h:mm:ss`; remaining as `-` + `PlaybackTimeRemaining.Format`; percent as whole number rounded down, `0 %` to `100 %`
- `PlayerTimeDisplay Next(PlayerTimeDisplay display)` — cycles in enum order
- `double BookPositionMs(IEnumerable<double> sourceDurationsSeconds, int currentSourceIndex, double currentPositionMs)`
- `(int SourceIndex, double PositionMs) Locate(IReadOnlyList<double> sourceDurationsSeconds, double bookPositionMs)` — a boundary belongs to the following file; past the end clamps to the end of the last file; no files gives `(0, 0)`
- `T Parse<T>(object? stored, T fallback) where T : struct, Enum` — unknown or missing values give the fallback

Steps: tests first (values from the spec: 10 h book, hour 4, 2× gives `4:00:00`, `10:00:00`, `-3:00:00`, `40 %`), watch them fail, implement, `dotnet test Audibly.Audio.Tests`, commit.

### Task 2: Settings and view model

**Files:** Modify `Audibly.App/Helpers/UserSettings.cs`, `Audibly.App/ViewModels/PlayerViewModel.cs`.

**Produces on `PlayerViewModel`:** `PlayerProgressScope ProgressScope`, `PlayerTimeDisplay TimeDisplay` (both persist to `UserSettings`), `double ProgressMaximumMs`, `double ProgressValueMs`, `string ProgressElapsedText`, `string ProgressEndText`, `void CycleTimeDisplay()`. `SeekToPositionAsync(double sliderValue)` interprets the value in the current scope and uses the existing cross-file open for another file.

All four display properties are raised together whenever chapter position, chapter duration, book position, speed, the playing book or one of the two settings changes.

### Task 3: Player bar, mini player, settings page

**Files:** Modify `Audibly.App/UserControls/NowPlayingBar.xaml(.cs)`, `Audibly.App/Views/NewMiniPlayerPage.xaml`, `Audibly.App/Views/SettingsPage.xaml(.cs)`.

- Both sliders bind to `ProgressMaximumMs` / `ProgressValueMs`.
- The bar's left text binds to `ProgressElapsedText`, the right to `ProgressEndText`; tapping the right text calls `CycleTimeDisplay()`.
- Settings page: expander "Player" with `RadioButtons` "Progress bar" (Chapter / Full title) and "Time display" (Elapsed and total / Time remaining / Percent).

Verify in "Audibly (Dev)": each of the six combinations, a drag across a file boundary in a multi-file book, a speed change while paused, restart keeps the choice.
