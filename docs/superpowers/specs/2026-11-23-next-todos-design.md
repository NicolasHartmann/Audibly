# Next fork todos — design (2026-11-23)

Four small, independent changes to the Windows app (Audibly.App). Built in this order.

## 1. PayPal link

- Settings → donate opens `https://paypal.me/hartmannn` instead of `paypal.me/rstewa35`
  (`Audibly.App/Views/SettingsPage.xaml.cs`).
- Approaches: only one.

## 2. Import several audiobook folders at once

**Why:** importing a large library folder by folder is slow.

- The "import folder" action opens the Windows folder dialog (`IFileOpenDialog` with
  `FOS_PICKFOLDERS | FOS_ALLOWMULTISELECT`) instead of the WinUI `FolderPicker`, which can only pick one folder.
- Each picked folder goes through the existing import: a folder with audio files is one book; otherwise every
  subfolder with audio files is one book (same rules as today, natural file order).
- One-time import, not a watched folder. Duplicates are skipped as today.
- Replaces the current single-folder button; picking one folder still works.

**Approaches:** WinUI `FolderPicker` (single folder only, ruled out) · a "collect folders, then import" list
(more UI, no gain) · **Win32 dialog with multi-select (chosen by user)**.

**Left to the builder:** COM interop details (CsWin32 or hand-written), access to the picked paths (the app has
broad file access; check `FutureAccessList` is not needed), progress/cancel reuse.

## 3. Book and chapter progress at the same time

**Why:** user wants to see both at once in the player.

- The big progress bar stays as now (chapter or book, per Settings → Player progress scope).
- Below it, a small text line shows the *other* scope, e.g. `Book  3:12:05 · 7:28:00 left`.
- Clicking that line cycles its own mode: elapsed/total, time remaining, percent. Saved as its own setting
  (like `PlayerTimeDisplay`). Remaining time honours playback speed, as today.
- Line hidden when there is nothing to show (no book, or a book with a single chapter where both are the same —
  builder's call).

**Approaches:** **A: big bar + small text line (chosen)** · B: two full bars (two sliders confuse dragging,
more space).

**Existing code:** `PlayerViewModel` progress region (`ProgressScope`, `TimeDisplay`, `PlayerProgress.*`),
`BookRemainingText` / `ChapterRemainingText`.

## 4. Read-along reader: chapter selection and progress

**Why:** when the user has read ahead in the ebook, jumping the audiobook to that place should be easy.

- A chapter dropdown at the top of the read-along panel.
- Chapters come from the EPUB table of contents (EPUB 3 `nav.xhtml`, EPUB 2 `toc.ncx`), read from the attached
  EPUB. Each entry maps to the first passage whose `Href` (and `Fragment`, if given) matches.
  Fallback when there is no usable TOC: one chapter per EPUB file (`Href`), titled by its first passage text.
- Choosing a chapter: the text scrolls there (follow-narration stops) **and** the audiobook seeks to the first
  aligned cue at or after that passage (same path as word click: `PlayerViewModel.SeekReadAlongAsync`).
  Chapters with no aligned cue: text scrolls, audio stays, short note.
- Progress line in the panel: `Book 42 % · Chapter 3/24 · 61 %`, by **text position** (top visible passage,
  counted in characters), so it reflects where the user reads, not only where the audio is. (My call; the user
  did not object.)
- The dropdown follows the current chapter while reading/scrolling.

**Approaches:** **A: EPUB table of contents, file fallback (chosen)** · B: audiobook chapters (titles like
"Track 07", do not match the text) · C: EPUB files only (files often ≠ chapters).

**Left to the builder:** where the TOC parsing lives (`Audibly.ReadAlong`, testable in `Audibly.ReadAlong.Tests`),
caching the parsed TOC, href normalisation (relative paths, URL-encoding), nested TOC entries (flatten with
indent).

## Non-goals

- Android changes.
- Watched library folders (already exist, unchanged).
- Reading the EPUB without a prepared alignment.
