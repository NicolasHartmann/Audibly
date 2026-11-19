# Book Details from the Internet (Windows) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** From a book's context menu on Windows, search Audible, pick a hit, and apply its description, narrator, series and part, genres and release date field by field.

**Architecture:** Platform-neutral search, parsing and field comparison in `Audibly.Core/Metadata` (usable by Android later). Five new nullable columns on `Audiobook` via one EF migration, written by a narrow repository method. A two-step `ContentDialog` in `Audibly.App` ties it together.

**Tech Stack:** .NET 8, System.Text.Json, HttpClient, EF Core 9.0.7 + SQLite, xunit, WinUI 3 (Windows App SDK).

**Spec:** `docs/superpowers/specs/2026-11-19-online-metadata-design.md`

## Global Constraints

- Search: `https://api.audible.de/1.0/catalog/products` and `https://api.audible.com/1.0/catalog/products` with `keywords` (one search text), `num_results=10`, `products_sort_by=Relevance`, `response_groups=contributors,series,product_attrs,media`; both in parallel; .de hits first; duplicates (same ASIN) dropped; hits without `runtime_length_min` (not audiobooks) dropped.
- Details: `https://api.audnex.us/books/{asin}?region=de` for .de hits, `region=us` for .com hits.
- HttpClient timeout 15 seconds. No API keys.
- `Composer` is never written by this feature (it is part of import duplicate detection).
- Nothing is saved without the user pressing Apply; only checked fields change; an empty value from Audible is never selectable and never clears an existing value.
- Network or parsing failures are shown as text in the dialog; they never throw out of the dialog and never touch the database.
- New columns are nullable; existing books keep all data after the migration.
- Windows only. Android gets the columns through the shared migration but no UI.
- UI texts are English.

## Review Focus

- One of the two Audible regions fails or times out while the other answers: the hits from the working region are shown, no error. (Test in Task 1.)
- Audnexus answers without `seriesPrimary`, `narrators` or `genres`, or with 404: missing fields come back empty / an error text is shown, no crash. (Tests in Task 1.)
- `releaseDate` `"2010-11-04T00:00:00.000Z"` is stored and shown as 4 Nov 2010 on a machine in UTC+1, not shifted to another day. (Test in Task 1.)
- Description HTML with `<p>`, `<br>`, `<b>` and entities like `&amp;` / `&quot;` becomes readable plain text with paragraph breaks. (Test in Task 1.)
- The book's current title is a chapter name or the author is empty: the search box is editable, an empty search does not hit the network. (Test in Task 1; box in Task 4.)
- The catalog also returns entries without a runtime (likely not audiobooks; seen for ASIN `3838795679`; note that ISBN-style ASINs *with* a runtime are real audiobooks and stay): they are not listed. (Test in Task 1.)

---

## File Structure

| File | Responsibility |
|---|---|
| `Audibly.Core/Metadata/AudibleHit.cs` | records `AudibleHit`, `BookDetails` |
| `Audibly.Core/Metadata/AudibleJson.cs` | pure parsing of Audible search and Audnexus JSON, HTML → text, region merge |
| `Audibly.Core/Metadata/AudibleMetadataClient.cs` | HTTP calls, parallel regions |
| `Audibly.Core/Metadata/MetadataComparison.cs` | old/new rows, which can be applied, merge into `BookMetadata` |
| `Audibly.Models/Audiobook.cs` | five new properties |
| `Audibly.Models/BookMetadata.cs` | record with the seven fields this feature writes |
| `Audibly.Repository/Migrations/*_AddBookDetails.cs` | generated migration |
| `Audibly.Repository/{Interfaces,Sql}/…AudiobookRepository.cs` | `SaveMetadataAsync` |
| `Audibly.App/Views/ContentDialogs/LookUpOnlineDialog.xaml(.cs)` | two-step dialog |
| `Audibly.App/Services/DialogService.cs` | `ShowLookUpOnlineDialogAsync` |
| `Audibly.App/UserControls/AudiobookTile.xaml(.cs)` | menu entry |
| `Audibly.App/ViewModels/AudiobookViewModel.cs`, `Views/ControlPages/MoreInfoDialogContent.xaml` | show new fields |
| `Audibly.Audio.Tests/Metadata/*` | tests + JSON fixtures |

Tests live in `Audibly.Audio.Tests` (it already references `Audibly.Core`; Task 2 adds `Audibly.Repository`). Run tests with `dotnet test Audibly.Audio.Tests -p:Platform=x64` (same as the existing tests).

---

### Task 1: Search, details and parsing (`Audibly.Core/Metadata`)

**Files:**
- Create: `Audibly.Core/Metadata/AudibleHit.cs`, `AudibleJson.cs`, `AudibleMetadataClient.cs`
- Create fixtures: `Audibly.Audio.Tests/Metadata/Fixtures/search-de.json`, `audnexus-panem2.json`, `audnexus-lotr.json` (copied to output: `<None Update="Metadata\Fixtures\*.json" CopyToOutputDirectory="PreserveNewest" />`)
- Test: `Audibly.Audio.Tests/Metadata/AudibleJsonTests.cs`, `AudibleMetadataClientTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record AudibleHit(string Asin, string Region, string Title, string Author, string Narrators, string? Series, string? SeriesPart, int? RuntimeMinutes, DateTime? ReleaseDate, string? Language, string? ImageUrl);` — `Region` is `"de"` or `"us"`; `Author`/`Narrators` joined with `", "`; `Series`/`SeriesPart` from `series[0].title` / `series[0].sequence`; `ImageUrl` from `product_images["500"]`.
  - `public sealed record BookDetails(string Asin, string? Description, string? Narrator, string? Series, string? SeriesPart, IReadOnlyList<string> Genres, DateTime? ReleaseDate);`
  - `public static class AudibleJson` with `IReadOnlyList<AudibleHit> ParseSearch(string json, string region)`, `BookDetails ParseDetails(string json)`, `string HtmlToText(string html)`, `IReadOnlyList<AudibleHit> Merge(IReadOnlyList<AudibleHit> de, IReadOnlyList<AudibleHit> us)`.
  - `public sealed class AudibleMetadataClient(HttpClient http)` with `Task<IReadOnlyList<AudibleHit>> SearchAsync(string keywords, CancellationToken ct)` and `Task<BookDetails> GetDetailsAsync(AudibleHit hit, CancellationToken ct)`. Errors surface as `MetadataLookupException(string message)` (also in this file).

- [ ] **Step 1: Save fixtures from the live APIs**

```bash
curl -s "https://api.audible.de/1.0/catalog/products?keywords=Die%20Tribute%20von%20Panem%20Collins&num_results=10&products_sort_by=Relevance&response_groups=contributors,series,product_attrs,media" > Audibly.Audio.Tests/Metadata/Fixtures/search-de.json
curl -s "https://api.audnex.us/books/B008CND4RS?region=de" > Audibly.Audio.Tests/Metadata/Fixtures/audnexus-panem2.json
curl -s "https://api.audnex.us/books/B004IZ4WFW?region=de" > Audibly.Audio.Tests/Metadata/Fixtures/audnexus-lotr.json
```

- [ ] **Step 2: Write the failing tests** (`AudibleJsonTests`)

```csharp
[Fact] void ParseSearch_reads_hit()        // search-de.json, region "de": hit with Asin "B008CND4RS" has Title "Gefährliche Liebe", Author "Suzanne Collins", Narrators "Maria Koschny", Series "Die Tribute von Panem", SeriesPart "2", RuntimeMinutes 576, ReleaseDate 2012-06-19, Language "german", Region "de", ImageUrl ends with ".jpg"
[Fact] void ParseDetails_reads_series_and_genres() // audnexus-panem2.json: Series "Die Tribute von Panem", SeriesPart "2", Narrator "Maria Koschny", ReleaseDate.Value.Date == new DateTime(2012,6,19)
[Fact] void ParseDetails_lists_genres_before_tags() // audnexus-lotr.json: Genres starts with ["Literatur & Belletristik","Science Fiction & Fantasy"] and contains "Epic Fantasy"; Narrator "Manfred Steffen, Rufus Beck, Hans Peter Hallwachs"; Series null
[Fact] void ParseDetails_without_optional_fields() // "{\"asin\":\"X\"}": Description/Narrator/Series/SeriesPart/ReleaseDate null, Genres empty
[Fact] void ParseDetails_keeps_release_day()       // releaseDate "2010-11-04T00:00:00.000Z" → ReleaseDate.Value.Date == new DateTime(2010,11,4) and Kind != Local-shifted (assert Year/Month/Day)
[Fact] void HtmlToText_paragraphs_and_entities()   // "<p>A &amp; B</p><p>C<br>D</p><b>E</b>" → "A & B\n\nC\nD\n\nE" (trimmed)
[Fact] void Merge_de_first_and_dedupes()           // de [a,b], us [b',c] (b' same Asin as b) → [a,b,c]
[Fact] void ParseSearch_skips_products_without_runtime() // {"products":[{"asin":"3838795679","title":"X"}]} → empty
```

- [ ] **Step 3: Run, expect compile failure** — `dotnet test Audibly.Audio.Tests -p:Platform=x64 --filter AudibleJsonTests` → FAIL (types missing).

- [ ] **Step 4: Implement `AudibleHit.cs` and `AudibleJson.cs`**

Parse with `JsonDocument`; every property optional (`TryGetProperty`). Description: `summary` through `HtmlToText`, fallback `description`. Genres: names of `type=="genre"` in order, then `type=="tag"`, distinct. Dates: parse the `yyyy-MM-dd` part only, `DateTimeKind.Unspecified`. `HtmlToText`: `</p>` and `<br>` → line breaks (paragraph = blank line), strip remaining tags with a regex, `WebUtility.HtmlDecode`, collapse 3+ newlines to 2, trim.

- [ ] **Step 5: Run** `AudibleJsonTests` → PASS.

- [ ] **Step 6: Write failing client tests** (`AudibleMetadataClientTests`, fake `HttpMessageHandler` keyed by host)

```csharp
[Fact] Task Search_queries_both_regions_de_first()      // de → search-de.json, com → "{\"products\":[]}" → first hit Region "de"; both hosts were requested; query contains "keywords=" and "num_results=10"
[Fact] Task Search_survives_one_region_failing()        // com → 500 (or handler throws TaskCanceledException) → de hits returned, no exception
[Fact] Task Search_throws_when_both_fail()              // both 500 → MetadataLookupException
[Fact] Task Search_with_blank_keywords_does_not_call_network() // "  " → empty list, handler call count 0
[Fact] Task Details_uses_region_of_hit()                // hit Region "us" → requested URL is "https://api.audnex.us/books/{asin}?region=us"
[Fact] Task Details_404_throws_lookup_exception()       // 404 → MetadataLookupException
```

- [ ] **Step 7: Implement `AudibleMetadataClient`** — hosts `api.audible.de` → `"de"`, `api.audible.com` → `"us"`; `Uri.EscapeDataString` for query values; region calls via `Task.WhenAll` with each wrapped so a failure yields `null`; both `null` → `MetadataLookupException("Audible could not be reached.")`. Non-success on details → `MetadataLookupException("No details found for this book.")`. Cancellation by the caller's token propagates as `OperationCanceledException`.

- [ ] **Step 8: Run** `dotnet test Audibly.Audio.Tests -p:Platform=x64 --filter "AudibleJsonTests|AudibleMetadataClientTests"` → PASS.

- [ ] **Step 9: Commit** — `git add Audibly.Core/Metadata Audibly.Audio.Tests` · `git commit -m "Core: search Audible and read book details from Audnexus"`

---

### Task 2: New columns, migration, `SaveMetadataAsync`

**Files:**
- Modify: `Audibly.Models/Audiobook.cs`
- Create: `Audibly.Models/BookMetadata.cs`
- Create (generated): `Audibly.Repository/Migrations/<timestamp>_AddBookDetails.cs`, `.Designer.cs`; Modify: `AudiblyContextModelSnapshot.cs`
- Modify: `Audibly.Repository/Interfaces/IAudiobookRepository.cs`, `Audibly.Repository/Sql/SqlAudiobookRepository.cs`
- Modify: `Audibly.Audio.Tests/Audibly.Audio.Tests.csproj` (add `ProjectReference` to `Audibly.Repository`)
- Test: `Audibly.Audio.Tests/Metadata/SaveMetadataTests.cs`

**Interfaces:**
- Produces:
  - `Audiobook`: `string? Narrator`, `string? Series`, `string? SeriesPart`, `string? Genres` (one genre per line, `\n`), `string? Asin`.
  - `public sealed record BookMetadata(string? Description, string? Narrator, string? Series, string? SeriesPart, string? Genres, DateTime? ReleaseDate, string? Asin);` with `static BookMetadata From(Audiobook book)`.
  - `IAudiobookRepository.Task SaveMetadataAsync(Guid audiobookId, BookMetadata metadata);`

- [ ] **Step 1: Write the failing test** — SQLite in memory (`new SqliteConnection("DataSource=:memory:")`, opened, `UseSqlite(connection)`), `context.Database.Migrate()`, insert an `Audiobook` with Title "T", Author "A", Composer "C", Description "old", one `SourceFile` and one `ChapterInfo`.

```csharp
[Fact] Task SaveMetadata_writes_only_book_details()
// SaveMetadataAsync(id, new BookMetadata("new", "N", "S", "2", "Fantasy\nEpic Fantasy", new DateTime(2010,11,4), "B004IZ4WFW"))
// fresh context: Description "new", Narrator "N", Series "S", SeriesPart "2", Genres "Fantasy\nEpic Fantasy", ReleaseDate 2010-11-04, Asin "B004IZ4WFW",
// Composer still "C", Title "T", SourcePaths.Count 1, Chapters.Count 1
[Fact] Task Migration_keeps_existing_books() // book inserted before SaveMetadata: new columns null, Composer/Description unchanged
```

- [ ] **Step 2: Run** `dotnet test Audibly.Audio.Tests -p:Platform=x64 --filter SaveMetadataTests` → FAIL (compile).

- [ ] **Step 3: Add the properties and `BookMetadata`.**

- [ ] **Step 4: Generate the migration**

```bash
dotnet tool install --global dotnet-ef --version 9.0.7
dotnet ef migrations add AddBookDetails --project Audibly.Repository --startup-project Audibly.Repository
```
Expected: migration adds five nullable `TEXT` columns to `Audiobooks` and nothing else (inspect `Up`; no drops, no renames).

- [ ] **Step 5: Implement `SaveMetadataAsync`** with `ExecuteUpdateAsync` setting exactly the seven columns, same pattern as `RenameAsync` (`SqlAudiobookRepository.cs:106`).

- [ ] **Step 6: Run** `SaveMetadataTests` → PASS. Also `dotnet build Audibly.Mobile.slnx -f net8.0-android` is not required; `dotnet build Audibly.sln -p:Platform=x64` → 0 errors.

- [ ] **Step 7: Commit** — `git commit -m "Library: columns for narrator, series, genres and ASIN"`

---

### Task 3: Old/new comparison (`MetadataComparison`)

**Files:**
- Create: `Audibly.Core/Metadata/MetadataComparison.cs`
- Test: `Audibly.Audio.Tests/Metadata/MetadataComparisonTests.cs`

**Interfaces:**
- Consumes: `BookMetadata` (Task 2), `BookDetails` (Task 1).
- Produces:
  - `public enum MetadataField { Narrator, Series, SeriesPart, Genres, ReleaseDate, Description }` (this is the row order).
  - `public sealed record FieldChange(MetadataField Field, string Old, string New, bool CanApply, bool ApplyByDefault);` — `Old`/`New` are display text: genres joined with `", "`, dates `d.ToShortDateString()`, null → `""`.
  - `public static class MetadataComparison` with `IReadOnlyList<FieldChange> Compare(BookMetadata current, BookDetails found)` and `BookMetadata Apply(BookMetadata current, BookDetails found, IReadOnlySet<MetadataField> chosen)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] void Empty_new_value_cannot_be_applied()      // found.Series null, current.Series "X" → row Series: CanApply false, ApplyByDefault false
[Fact] void Different_value_is_checked_by_default()  // current Narrator null, found "N" → CanApply true, ApplyByDefault true
[Fact] void Equal_value_is_not_checked()             // both "N" → CanApply true, ApplyByDefault false
[Fact] void Release_date_compares_by_day()           // current 2010-11-04 00:00, found 2010-11-04 → ApplyByDefault false
[Fact] void Apply_changes_only_chosen_fields()       // chosen {Narrator}: result Narrator from found, Series/Description from current, Asin == found.Asin
[Fact] void Apply_ignores_chosen_empty_field()       // chosen {Series}, found.Series null, current "X" → result Series "X"
[Fact] void Apply_stores_genres_one_per_line()       // found Genres ["A","B"], chosen {Genres} → "A\nB"
```

- [ ] **Step 2: Run** → FAIL (compile). **Step 3: Implement.** **Step 4: Run** `MetadataComparisonTests` → PASS.

- [ ] **Step 5: Commit** — `git commit -m "Core: compare current and found book details field by field"`

---

### Task 4: Windows dialog, menu entry, More Info

**Files:**
- Create: `Audibly.App/Views/ContentDialogs/LookUpOnlineDialog.xaml`, `.xaml.cs`
- Modify: `Audibly.App/Services/DialogService.cs` (next to `ShowMoreInfoDialogAsync`, line 260)
- Modify: `Audibly.App/UserControls/AudiobookTile.xaml` (after "More Info") and `.xaml.cs`
- Modify: `Audibly.App/ViewModels/AudiobookViewModel.cs` (`Narrator` at line 325), `Audibly.App/Views/ControlPages/MoreInfoDialogContent.xaml`

**Interfaces:**
- Consumes: `AudibleMetadataClient`, `AudibleHit`, `BookDetails`, `MetadataLookupException` (Task 1); `BookMetadata`, `SaveMetadataAsync` (Task 2); `MetadataComparison`, `FieldChange`, `MetadataField` (Task 3).
- Produces: `DialogService.ShowLookUpOnlineDialogAsync(AudiobookViewModel book)`; `AudiobookViewModel.ApplyMetadata(BookMetadata metadata)`, which sets the model's seven fields and raises `PropertyChanged` for them.

- [ ] **Step 1: ViewModel and More Info**
  - `Narrator => string.IsNullOrWhiteSpace(Model.Narrator) ? Model.Composer : Model.Narrator`.
  - New read-only `Series` (`"{Series} {SeriesPart}"` trimmed, e.g. "Die Tribute von Panem 2") and `Genres` (`", "`-joined) properties; rows "Series:" and "Genres:" in `MoreInfoDialogContent.xaml`, styled like the Narrator row, collapsed when empty.

- [ ] **Step 2: Dialog**
  - One `HttpClient` for the app (static in `DialogService` or `App`) with `Timeout = TimeSpan.FromSeconds(15)`.
  - Step 1 view: a `TextBox` prefilled with `"{Title} {Author}"` and a Search button (Enter also searches; the search runs once when the dialog opens). `SearchAsync(box text)`. A `ProgressRing` while it runs. A `ListView` of hits showing a 48 px cover, title, then `Author · Narrators · Series SeriesPart · 11h 26m · 2012 · german · DE`. Text "No results." or the exception message shown in the dialog. Primary button "Next" is enabled when a hit is selected.
  - Step 2 view: `GetDetailsAsync`, then one row per `FieldChange` (CheckBox `IsEnabled=CanApply`, `IsChecked=ApplyByDefault`, field label, old text, new text; long texts trimmed to 3 lines with a tooltip). Buttons "Back" (secondary) and "Apply" (primary).
  - Apply: `MetadataComparison.Apply(BookMetadata.From(book.Model), details, chosen)`, then `await Task.Run(() => App.Repository.Audiobooks.SaveMetadataAsync(book.Id, result))`, then `book.ApplyMetadata(result)` on the UI thread. The dialog's `CancellationTokenSource` is cancelled on close, so a late answer does nothing.
  - Errors from save are logged via `App.ViewModel.LoggingService.LogError` and shown as an error notification. The book stays unchanged.

- [ ] **Step 3: Menu entry** — `MenuFlyoutItem Text="Look up online…"` with globe glyph `&#xE774;` after "More Info". The handler mirrors `MoreInfo_OnClick` (`AudiobookTile.xaml.cs:146`) and calls `ShowLookUpOnlineDialogAsync`.

- [ ] **Step 4: Build** `dotnet build Audibly.sln -p:Platform=x64` → 0 errors; `dotnet test Audibly.Audio.Tests -p:Platform=x64` → all pass.

- [ ] **Step 5: Manual check in the running app** (record results in the hand-off):
  1. Look up a German Audible book: hits from DE first, pick one, rows prechecked as specified, Apply, More Info shows narrator, series and genres.
  2. Run it again on the same book: nothing is prechecked (values equal).
  3. A ripped CD with an English title: edit the search box, find a US hit, apply.
  4. Disconnect the network: search shows "Audible could not be reached.", the book is unchanged.
  5. Restart the app: the values are still there. Re-import the same file: "already in the library" warning still appears (Composer unchanged).

- [ ] **Step 6: Commit** — `git commit -m "Windows: look up a book's details online"`; tick both to-do items' first line in `todo.md` only after the user has tested.
