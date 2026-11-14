# Listening Stats & Trophies Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Audibly zeichnet auf, wie lange gehört wird, zeigt das auf einer Statistikseite und schaltet daraus Trophäen mit Stufen frei.

**Architecture:** Eine neue Klassenbibliothek `Audibly.Stats` (ohne WinUI) enthält die gesamte Logik: Mitzählen aus den Positions-Ticks des Players, Speichern in einer eigenen SQLite-Datei `stats.db`, Auswertung und Trophäenkatalog. Die App hängt sich nur an drei Stellen in `PlayerViewModel` ein und bekommt zwei neue Seiten. Die Bibliotheksdatenbank `Audibly.db` wird nicht angefasst, es gibt keine EF-Migration.

**Tech Stack:** .NET 8, WinUI 3 (Windows App SDK 1.7), `Microsoft.Data.Sqlite` 9.0.7, xunit 2.9.2.

**Spec:** Es gibt kein separates Spec-Dokument. Der Abschnitt „Festgelegte Entscheidungen" unten ist die Spezifikation; er fasst die Anforderungen und die Antworten des Nutzers vom 2026-11-14 zusammen.

## Festgelegte Entscheidungen

Vom Nutzer entschieden:

- **Hördauer = echte Zeit.** 1 h Hören bei 2× zählt als 1 h Hördauer und 1 h eingesparte Zeit. Unter 1× ist die Ersparnis 0, nie negativ.
- **Zwei Menüpunkte** in der linken Navigation: „Statistics" und „Trophies", unter „Now Playing".
- **Englische Oberfläche**, wie der Rest der App.
- **Trophäen haben drei Stufen** (Bronze, Silver, Gold) mit Fortschrittsanzeige, und beim Freischalten erscheint ein Hinweis in der App.
- **Statistikwerte:** aktueller Titel (Hördauer, eingesparte Zeit), gesamt (Hördauer, eingesparte Zeit), Hördauer pro Tag für die letzten 5 Tage, an denen gehört wurde, pro Monat für jeden Monat mit Daten, pro Jahr für jedes Jahr, Anzahl der Titel in der Bibliothek.
- **Genres** sind nicht Teil dieses Plans. Sie brauchen nichts vorab: Die Aufzeichnung speichert die Hörbuch-ID, darüber lässt sich später nach Genre gruppieren.

Von mir festgelegt (bitte beim Review prüfen):

- **Start bei null.** Die App hat bisher keine Hörhistorie gespeichert. Statistik und Trophäen beginnen mit der Installation dieser Funktion.
- **Layout nach den Screenshots, eigenes Aussehen.** Übernommen werden: Reiter über einem Balkendiagramm mit großer Durchschnittszahl und gestrichelter Durchschnittslinie, Karten für Kennzahlen, ein Raster runder Abzeichen mit ausgegrauten gesperrten Trophäen und ein Dialog mit Ziel und Fortschritt. Farben, Symbole und Namen sind eigene (App-Akzentfarbe, Segoe-Fluent-Glyphen), keine Audible-Grafiken oder -Namen.
- **Kein „Teilen"-Knopf** und kein Diagramm zum Bibliothekswachstum; verlangt ist nur die Anzahl der Titel.
- **„Beendet" zählt nur durch Hören** (Fortschritt erreicht 99,9 %), nicht durch manuelles Markieren als gehört.
- **Tages- und Stundengrenzen** in lokaler Zeit.
- **Arbeitsbranch:** `feature/listening-stats`, abgezweigt von `local/read-along`. Nichts wird gepusht.

## Global Constraints

- Die Store-Installation von Audibly und ihre Bibliothek werden nie angefasst. Getestet wird nur in „Audibly (Dev)" (Identität `Audibly.DevTest`), neu bereitgestellt über das Scratchpad-Skript `redeploy-dev.ps1`.
- Kein Push, kein PR, kein Kommentar ohne ausdrückliche Zustimmung des Nutzers.
- `Audibly.db`, `AudiblyContext` und die Migrationen bleiben unverändert.
- Ein Fehler in der Statistik darf die Wiedergabe nie stören: Jeder Aufruf aus `PlayerViewModel` in die Statistik ist gegen Ausnahmen abgesichert und schreibt höchstens ins Log.
- Aus dem Positions-Tick heraus wird nie auf die Datenbank geschrieben; geschrieben wird gesammelt alle 30 Sekunden und bei Pause, Buchwechsel und Beenden.
- Alle sichtbaren Texte englisch. Zahlen- und Datumsformate über `CultureInfo.InvariantCulture`.
- Commit-Nachrichten enden mit `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`.

## Review Focus

1. **Springen ist kein Hören.** Kapitelwechsel, Skip-Tasten, Klick in die Fortschrittsleiste oder auf ein Read-Along-Wort dürfen keine Hördauer erzeugen. (Test in Task 1)
2. **Dateiwechsel bei Büchern aus mehreren Dateien.** Die Position springt auf 0 zurück; das darf weder negativ zählen noch die Zählung dauerhaft stoppen. (Test in Task 1)
3. **Hören über Mitternacht und über die volle Stunde.** Die Zeit landet anteilig im richtigen Tag und in der richtigen Stunde. (Test in Task 1)
4. **`stats.db` fehlt, ist gesperrt oder beschädigt.** Die App startet und spielt trotzdem; die Statistikseite zeigt den leeren Zustand. (Tests in Task 2 und Task 5)
5. **Gelöschtes Hörbuch.** Seine Hörzeit bleibt in Gesamt-, Tages-, Monats- und Jahreswerten erhalten; ein erneut beendetes Buch zählt nicht doppelt innerhalb von 24 Stunden. (Tests in Task 2 und Task 3)

## Dateien

Neu:

- `Audibly.Stats/Audibly.Stats.csproj` — net8.0, `Nullable` und `ImplicitUsings` an, Paket `Microsoft.Data.Sqlite` 9.0.7
- `Audibly.Stats/ListeningSlice.cs` — Datensätze `ListeningSlice`, `Completion`, `StatsData`
- `Audibly.Stats/ListeningTracker.cs` — zählt Hörzeit aus Positions-Ticks
- `Audibly.Stats/StatsStore.cs` — SQLite-Zugriff auf `stats.db`
- `Audibly.Stats/StatsSummary.cs` — Summen, Tage, Monate, Jahre
- `Audibly.Stats/DurationFormat.cs`, `Audibly.Stats/ChartAxis.cs` — Anzeigehelfer
- `Audibly.Stats/TrophyCatalog.cs` — Definitionen und Auswertung
- `Audibly.Stats/ListeningStatsRecorder.cs` — verbindet Tracker, Store und Trophäen
- `Audibly.Stats.Tests/` — xunit-Projekt, eine Testdatei je Klasse
- `Audibly.App/Services/ListeningStatsService.cs` — App-Hülle mit 30-Sekunden-Timer und Hinweis
- `Audibly.App/UserControls/StatsBarChart.xaml(.cs)` — Balkendiagramm
- `Audibly.App/UserControls/TrophyBadge.xaml(.cs)` — rundes Abzeichen
- `Audibly.App/Views/StatsPage.xaml(.cs)`, `Audibly.App/Views/TrophiesPage.xaml(.cs)`

Geändert:

- `Audibly.sln` — beide neuen Projekte
- `Audibly.App/Audibly.App.csproj` — Projektverweis auf `Audibly.Stats`
- `Audibly.App/App.xaml.cs` — `App.ListeningStats`, Flush beim Schließen
- `Audibly.App/ViewModels/PlayerViewModel.cs` — drei Einhängepunkte
- `Audibly.App/AppShell.xaml(.cs)` — zwei Menüpunkte

---

### Task 1: Projekt `Audibly.Stats` und `ListeningTracker`

**Files:**
- Create: `Audibly.Stats/Audibly.Stats.csproj`, `Audibly.Stats/ListeningSlice.cs`, `Audibly.Stats/ListeningTracker.cs`
- Create: `Audibly.Stats.Tests/Audibly.Stats.Tests.csproj`, `Audibly.Stats.Tests/ListeningTrackerTests.cs`
- Modify: `Audibly.sln`

**Interfaces:**
- Produces:

```csharp
namespace Audibly.Stats;

public sealed record ListeningSlice(DateOnly Day, int Hour, Guid AudiobookId, double ListenedSeconds, double SavedSeconds);
public sealed record Completion(Guid AudiobookId, DateTime CompletedAt);
public sealed record StatsData(IReadOnlyList<ListeningSlice> Slices, IReadOnlyList<Completion> Completions)
{
    public static readonly StatsData Empty = new([], []);
}

public sealed class ListeningTracker
{
    public const double MaxTickRealSeconds = 3.0;
    public void Observe(Guid audiobookId, int sourceIndex, double positionSeconds, double speed, DateTime localNow);
    public void Break();
    public IReadOnlyList<ListeningSlice> Drain();
}
```

Regel für `Observe`: `delta = positionSeconds − letzte Position`. Gezählt wird nur, wenn Buch und `sourceIndex` gleich dem letzten Tick sind, `speed` endlich und > 0 ist und `0 < delta / speed ≤ MaxTickRealSeconds` gilt. Dann `listened = delta / speed`, `saved = max(0, delta − listened)`, addiert auf den Slice für `(DateOnly.FromDateTime(localNow), localNow.Hour, audiobookId)`. Die letzte Position wird bei jedem Aufruf aktualisiert, auch wenn nicht gezählt wurde. `Break()` vergisst die letzte Position. `Drain()` liefert die gesammelten Slices und leert sie; die letzte Position bleibt.

- [ ] **Step 1: Projekte anlegen.** `dotnet new classlib -n Audibly.Stats -f net8.0`, `dotnet new xunit -n Audibly.Stats.Tests -f net8.0`, Projektverweis vom Testprojekt auf `Audibly.Stats`, beide mit `dotnet sln Audibly.sln add` aufnehmen, die generierten `Class1.cs` und `UnitTest1.cs` löschen. xunit auf 2.9.2 festlegen wie in `Audibly.Audio.Tests`.

- [ ] **Step 2: Failing Tests schreiben** in `ListeningTrackerTests.cs`. `Book` ist eine feste Guid, `T(h, m, s)` ergibt `new DateTime(2026, 11, 14, h, m, s)`.

```csharp
[Fact] public void CountsRealTimeAndSavedTimeAtDoubleSpeed()
{
    var t = new ListeningTracker();
    t.Observe(Book, 0, 100, 2.0, T(10, 0, 0));
    t.Observe(Book, 0, 102, 2.0, T(10, 0, 1));
    var s = Assert.Single(t.Drain());
    Assert.Equal(1.0, s.ListenedSeconds, 3);
    Assert.Equal(1.0, s.SavedSeconds, 3);
    Assert.Equal(new DateOnly(2026, 11, 14), s.Day);
    Assert.Equal(10, s.Hour);
}

[Fact] public void SlowerThanNormalSavesNothing()        // 100 -> 100.5 bei 0.5x: Listened 1.0, Saved 0
[Fact] public void SeekForwardIsNotListening()           // 100 -> 400 bei 1x: Drain leer; danach 400 -> 401 zählt 1.0
[Fact] public void SeekBackwardIsNotListening()          // 400 -> 100: Drain leer
[Fact] public void NextFileRestartingAtZeroKeepsCounting() // (source 0, 599) -> (source 1, 0.2) zählt nichts; (source 1, 1.2) zählt 1.0
[Fact] public void OtherBookDoesNotCountTheFirstTick()
[Fact] public void BreakForgetsTheLastPosition()         // 100, Break(), 101: Drain leer
[Fact] public void SplitsAcrossMidnightAndHours()        // Tick um 23:59:59 und um 00:00:01 des Folgetags -> zwei Slices mit Day 14 / Hour 23 und Day 15 / Hour 0
[Fact] public void IgnoresInvalidSpeed()                 // speed 0, negativ, NaN: Drain leer, keine Ausnahme
[Fact] public void DrainEmptiesButKeepsPosition()        // nach Drain zählt der nächste Tick weiter
```

- [ ] **Step 3: Tests ausführen und Fehlschlag bestätigen.** Run: `dotnet test Audibly.Stats.Tests` — Expected: Kompilierfehler, `ListeningTracker` existiert nicht.

- [ ] **Step 4: `ListeningSlice.cs` und `ListeningTracker.cs` implementieren** nach den Signaturen und der Regel oben. Slices in einem `Dictionary<(DateOnly, int, Guid), (double, double)>` sammeln. Die Klasse wird nur vom UI-Thread benutzt, keine Sperren nötig.

- [ ] **Step 5: Tests ausführen.** Run: `dotnet test Audibly.Stats.Tests` — Expected: 10 bestanden.

- [ ] **Step 6: Commit** — `Stats: count listening time from player position ticks`

---

### Task 2: `StatsStore` (SQLite)

**Files:**
- Create: `Audibly.Stats/StatsStore.cs`
- Test: `Audibly.Stats.Tests/StatsStoreTests.cs`

**Interfaces:**
- Consumes: `ListeningSlice`, `Completion`, `StatsData` aus Task 1.
- Produces:

```csharp
public sealed class StatsStore
{
    public StatsStore(string databasePath);
    public Task AddAsync(IReadOnlyList<ListeningSlice> slices);
    public Task<bool> RecordCompletionAsync(Guid audiobookId, DateTime localCompletedAt);
    public Task<StatsData> LoadAsync();
    public Task<IReadOnlyDictionary<string, int>> LoadTrophyTiersAsync();
    public Task SaveTrophyTierAsync(string trophyId, int tier, DateTime localUnlockedAt);
}
```

Schema, bei jedem Öffnen mit `CREATE TABLE IF NOT EXISTS` angelegt:

```sql
listening(day TEXT NOT NULL, hour INTEGER NOT NULL, book TEXT NOT NULL,
          listened REAL NOT NULL, saved REAL NOT NULL, PRIMARY KEY(day, hour, book));
completions(book TEXT NOT NULL, completed_at TEXT NOT NULL);
trophies(id TEXT PRIMARY KEY, tier INTEGER NOT NULL, unlocked_at TEXT NOT NULL);
```

`day` als `yyyy-MM-dd`, Zeitstempel als ISO-8601 (`"s"`), beides invariant. `AddAsync` addiert per `INSERT … ON CONFLICT(day, hour, book) DO UPDATE SET listened = listened + excluded.listened, saved = saved + excluded.saved` in einer Transaktion. `RecordCompletionAsync` gibt `false` zurück und schreibt nichts, wenn für dasselbe Buch eine Fertigstellung in den 24 Stunden davor existiert. `SaveTrophyTierAsync` senkt eine gespeicherte Stufe nie ab. Jede Methode öffnet ihre eigene Verbindung (`Pooling=False`), damit die Datei zwischen den Aufrufen nicht gesperrt bleibt. Keine Fremdschlüssel: Zeilen überleben das Löschen eines Hörbuchs.

- [ ] **Step 1: Failing Tests schreiben.** Jeder Test nutzt eine eigene Datei unter `Path.GetTempPath()` und löscht sie am Ende.

```csharp
[Fact] public async Task AddingTheSameSliceTwiceSumsIt()          // 2x (Day, 10, Book, 60, 30) -> LoadAsync: ein Slice mit 120 / 60
[Fact] public async Task KeepsSlicesOfDifferentHoursAndBooksApart()
[Fact] public async Task DataSurvivesANewStoreInstance()          // zweiter StatsStore auf derselben Datei sieht die Daten
[Fact] public async Task EmptyDatabaseLoadsAsEmpty()              // neue Datei: Slices und Completions leer
[Fact] public async Task CompletionWithin24HoursIsNotRecordedTwice() // 1. true, +23 h false, +25 h true -> 2 Completions
[Fact] public async Task TrophyTierNeverGoesDown()                // Save("marathoner", 2), Save("marathoner", 1) -> Load ergibt 2
[Fact] public async Task CorruptFileThrowsSqliteException()       // Datei mit Text "not a database": LoadAsync wirft SqliteException (der Recorder fängt sie, Task 5)
```

- [ ] **Step 2: Fehlschlag bestätigen.** Run: `dotnet test Audibly.Stats.Tests --filter StatsStoreTests` — Expected: Kompilierfehler.

- [ ] **Step 3: `StatsStore` implementieren** mit `Microsoft.Data.Sqlite` und rohem SQL, Signaturen und Regeln wie oben.

- [ ] **Step 4: Tests ausführen.** Expected: 7 bestanden, Task-1-Tests weiter grün.

- [ ] **Step 5: Commit** — `Stats: store listening time, completions and trophy tiers in stats.db`

---

### Task 3: Auswertung `StatsSummary`, `DurationFormat`, `ChartAxis`

**Files:**
- Create: `Audibly.Stats/StatsSummary.cs`, `Audibly.Stats/DurationFormat.cs`, `Audibly.Stats/ChartAxis.cs`
- Test: `Audibly.Stats.Tests/StatsSummaryTests.cs`, `DurationFormatTests.cs`, `ChartAxisTests.cs`

**Interfaces:**
- Consumes: `StatsData`.
- Produces:

```csharp
public sealed record Totals(double ListenedSeconds, double SavedSeconds);
public sealed record PeriodTotal(string Label, DateOnly Start, double ListenedSeconds);

public static class StatsSummary
{
    public static Totals Total(StatsData data);
    public static Totals ForBook(StatsData data, Guid audiobookId);
    public static IReadOnlyList<PeriodTotal> LastListenedDays(StatsData data, int count = 5);
    public static IReadOnlyList<PeriodTotal> Months(StatsData data);
    public static IReadOnlyList<PeriodTotal> Years(StatsData data);
    public static double Average(IReadOnlyList<PeriodTotal> periods);
}

public static class DurationFormat { public static string Short(double seconds); }

public static class ChartAxis
{
    public static (double MaxSeconds, IReadOnlyList<double> TickSeconds) For(double largestValueSeconds);
}
```

Regeln:
- `LastListenedDays`: die letzten `count` Tage mit mehr als 0 Sekunden, älteste zuerst, Tage ohne Hören werden übersprungen. Label `ddd d`, zum Beispiel `Sat 14`.
- `Months`: jeder Monat vom ersten bis zum letzten Monat mit Daten, Lücken als 0. Label `MMM` (`Nov`), bei Daten über mehr als ein Kalenderjahr `MMM yy` (`Nov 26`).
- `Years`: jedes Jahr vom ersten bis zum letzten mit Daten, Label `2026`.
- `Average`: Mittel über alle Einträge der Liste, 0 bei leerer Liste.
- `DurationFormat.Short`: `5h 4m`, unter einer Stunde `43m`, unter einer Minute `0m`; abgerundet auf ganze Minuten; negative und nicht endliche Werte ergeben `0m`.
- `ChartAxis.For`: wählt die kleinste Schrittweite aus 5 min, 10 min, 15 min, 30 min, 1 h, 2 h, 3 h, 6 h, 12 h, 24 h, 50 h, 100 h, 250 h, 500 h, 1000 h, für die `4 × Schritt ≥ largestValueSeconds` gilt; liefert `MaxSeconds = 4 × Schritt` und fünf Ticks von 0 bis Max. Bei 0 oder ungültigem Wert Schritt 15 min.

- [ ] **Step 1: Failing Tests schreiben.**

```csharp
// StatsSummaryTests
[Fact] public void TotalSumsListenedAndSavedOverAllBooks()
[Fact] public void ForBookOnlyCountsThatBook()
[Fact] public void ForUnknownBookIsZero()
[Fact] public void LastListenedDaysSkipsDaysWithoutListening()
// Daten am 1., 3., 4., 9., 10., 14. Nov -> Labels "Tue 3", "Wed 4", "Mon 9", "Tue 10", "Sat 14"
[Fact] public void LastListenedDaysWithFewerDaysReturnsWhatExists()
[Fact] public void MonthsFillGapsWithZero()          // Juni und Sept 2026 -> Jun, Jul(0), Aug(0), Sep
[Fact] public void MonthsAcrossYearsCarryTheYear()   // Dez 2025 und Jan 2026 -> "Dec 25", "Jan 26"
[Fact] public void YearsListEveryYearWithData()
[Fact] public void DeletedBookStillCountsInTotals()  // Slices einer Guid, die es in der Bibliothek nicht gibt, zählen in Total, Months, Years
[Fact] public void EmptyDataGivesEmptyListsAndZeroTotals()

// DurationFormatTests
[Theory]
[InlineData(18240, "5h 4m")] [InlineData(2580, "43m")] [InlineData(59, "0m")]
[InlineData(3600, "1h 0m")] [InlineData(-5, "0m")] [InlineData(double.NaN, "0m")]

// ChartAxisTests
[Fact] public void ElevenAndAHalfHoursGetsATwelveHourAxis() // Max 43200, Ticks 0, 10800, 21600, 32400, 43200
[Fact] public void FortyMinutesGetsAOneHourAxis()           // Schritt 15 min
[Fact] public void ZeroGetsTheSmallestUsefulAxis()          // Max 3600
```

- [ ] **Step 2: Fehlschlag bestätigen**, **Step 3: implementieren**, **Step 4: Tests ausführen** (`dotnet test Audibly.Stats.Tests`, alle grün).

- [ ] **Step 5: Commit** — `Stats: totals and per-day, per-month, per-year summaries`

---

### Task 4: Trophäenkatalog

**Files:**
- Create: `Audibly.Stats/TrophyCatalog.cs`
- Test: `Audibly.Stats.Tests/TrophyCatalogTests.cs`

**Interfaces:**
- Consumes: `StatsData`.
- Produces:

```csharp
public sealed record TrophyDefinition(string Id, string Name, string Glyph, double[] Thresholds, string Goal, string Progress);
public sealed record TrophyInput(StatsData Data, int LibraryCount);
public sealed record TrophyState(TrophyDefinition Definition, double Value, int Tier)
{
    public double? NextThreshold { get; }   // null bei Gold
    public string GoalText { get; }         // Goal mit dem nächsten Schwellwert, bei Gold: "All tiers unlocked."
    public string ProgressText { get; }     // Progress mit dem aktuellen Wert
}

public static class TrophyCatalog
{
    public static IReadOnlyList<TrophyDefinition> All { get; }
    public static IReadOnlyList<TrophyState> Evaluate(TrophyInput input);
    public static string TierName(int tier);   // 0 "Locked", 1 "Bronze", 2 "Silver", 3 "Gold"
}
```

`Tier` ist die Anzahl erreichter Schwellwerte (0 bis 3). `Goal` und `Progress` enthalten `{0}` für die Zahl; Stundenwerte werden mit höchstens einer Nachkommastelle formatiert, Zählwerte ohne.

Katalog (Reihenfolge = Anzeige):

| Id | Name | Glyph | Messwert | Bronze / Silver / Gold | Goal | Progress |
|---|---|---|---|---|---|---|
| `marathoner` | Marathoner | `` | Hördauer gesamt in Stunden | 10 / 100 / 500 | Listen for {0} hours in total. | You have listened for {0} hours. |
| `time-bender` | Time Bender | `` | Eingesparte Zeit gesamt in Stunden | 1 / 24 / 100 | Save {0} hours by listening faster. | You have saved {0} hours. |
| `finisher` | Finisher | `` | Anzahl Completions | 1 / 10 / 50 | Finish {0} audiobooks. | You have finished {0} audiobooks. |
| `collector` | Collector | `` | `LibraryCount` | 10 / 50 / 200 | Grow your library to {0} titles. | Your library has {0} titles. |
| `daily-habit` | Daily Habit | `` | Längste Serie aufeinanderfolgender Tage mit Hören | 3 / 7 / 30 | Listen {0} days in a row. | Your longest streak is {0} days. |
| `night-owl` | Night Owl | `` | Stunden in den Tagesstunden 23, 0, 1, 2, 3 | 1 / 10 / 50 | Listen for {0} hours between 11 pm and 4 am. | You have listened {0} hours at night. |
| `early-bird` | Early Bird | `` | Stunden in den Tagesstunden 4, 5, 6 | 1 / 10 / 50 | Listen for {0} hours between 4 am and 7 am. | You have listened {0} hours in the early morning. |
| `weekend-warrior` | Weekend Warrior | `` | Meiste Stunden an einem Wochenende (Sa + So zusammen) | 2 / 5 / 10 | Listen for {0} hours on a single weekend. | Your best weekend is {0} hours. |
| `binge-day` | Binge Day | `` | Meiste Stunden an einem Tag | 2 / 5 / 8 | Listen for {0} hours in a single day. | Your best day is {0} hours. |
| `speed-reader` | Speed Reader | `` | Stunden in Slices mit `SavedSeconds ≥ ListenedSeconds` (im Schnitt mindestens 2×) | 1 / 10 / 50 | Listen for {0} hours at double speed or faster. | You have listened {0} hours at 2× or faster. |
| `sprint` | Sprint | `` | Meiste Completions in einem Fenster von 7 Tagen | 2 / 3 / 5 | Finish {0} audiobooks within 7 days. | Your best week is {0} audiobooks. |
| `juggler` | Juggler | `` | Meiste verschiedene Hörbücher an einem Tag (je mindestens 60 s) | 2 / 3 / 5 | Listen to {0} different audiobooks on one day. | Your best is {0} audiobooks on one day. |
| `regular` | Regular | `` | Meiste Tage mit Hören in einem Kalendermonat | 10 / 20 / 28 | Listen on {0} days of one month. | Your best month has {0} listening days. |
| `around-the-clock` | Around the Clock | `` | Anzahl verschiedener Tagesstunden, in denen je gehört wurde (je mindestens 60 s) | 12 / 18 / 24 | Listen during {0} different hours of the day. | You have listened during {0} different hours. |

Die Glyph-Codes sind Vorschläge aus Segoe Fluent Icons; in Task 7 wird jeder am Bildschirm geprüft und bei einem leeren oder unpassenden Zeichen ersetzt.

- [ ] **Step 1: Failing Tests schreiben.**

```csharp
[Fact] public void CatalogHasFourteenTrophiesWithUniqueIdsAndThreeAscendingThresholds()
[Fact] public void EmptyDataLeavesEverythingLocked()                    // alle Tier 0, Value 0
[Fact] public void TierIsTheNumberOfThresholdsReached()                 // 100 h gesamt -> marathoner Tier 2, NextThreshold 500
[Fact] public void GoldHasNoNextThreshold()                             // 500 h -> Tier 3, NextThreshold null, GoalText "All tiers unlocked."
[Fact] public void CollectorUsesTheLibraryCount()                       // LibraryCount 50 -> Tier 2
[Fact] public void DailyHabitNeedsConsecutiveDays()                     // 1., 2., 3., 5., 6. Nov -> Value 3
[Fact] public void NightOwlOnlyCountsNightHours()                       // 1 h um 23 Uhr + 1 h um 12 Uhr -> Value 1
[Fact] public void WeekendWarriorAddsSaturdayAndSunday()                // Sa 14. 1,5 h + So 15. 1 h -> Value 2,5
[Fact] public void SpeedReaderCountsSlicesAtDoubleSpeedOrFaster()       // (3600, 3600) zählt, (3600, 1800) nicht
[Fact] public void SprintUsesASlidingSevenDayWindow()                   // Completions am 1., 5., 7., 20. -> Value 3
[Fact] public void JugglerIgnoresBooksUnderAMinute()
[Fact] public void AroundTheClockCountsDistinctHours()
[Fact] public void TextsContainTheNumbers()                             // marathoner bei 12,5 h: GoalText "Listen for 100 hours in total.", ProgressText "You have listened for 12.5 hours."
```

- [ ] **Step 2: Fehlschlag bestätigen**, **Step 3: implementieren**, **Step 4: Tests ausführen** (alle grün).

- [ ] **Step 5: Commit** — `Stats: trophy catalog with bronze, silver and gold tiers`

---

### Task 5: `ListeningStatsRecorder`

**Files:**
- Create: `Audibly.Stats/ListeningStatsRecorder.cs`
- Test: `Audibly.Stats.Tests/ListeningStatsRecorderTests.cs`

**Interfaces:**
- Consumes: `ListeningTracker`, `StatsStore`, `TrophyCatalog`.
- Produces:

```csharp
public sealed class ListeningStatsRecorder
{
    public ListeningStatsRecorder(StatsStore store, Func<DateTime> localNow, Func<int> libraryCount, Action<Exception>? onError = null);
    public void OnPosition(Guid audiobookId, int sourceIndex, double positionSeconds, double speed);
    public void OnInterrupted();
    public Task OnCompletedAsync(Guid audiobookId);
    public Task<IReadOnlyList<TrophyState>> FlushAsync();
    public Task<StatsData> LoadAsync();
    public Task<IReadOnlyList<TrophyState>> LoadTrophiesAsync();
}
```

`FlushAsync` schreibt die gesammelten Slices, wertet die Trophäen aus, vergleicht mit den gespeicherten Stufen, speichert jede höhere Stufe und gibt nur die Trophäen zurück, deren Stufe gestiegen ist (je Trophäe ein Eintrag mit der neuen Stufe). Hat sich seit dem letzten Flush nichts angesammelt und wurde keine Fertigstellung gemeldet, kehrt es ohne Datenbankzugriff mit einer leeren Liste zurück; der allererste Aufruf wertet immer aus. `LoadTrophiesAsync` liefert den aktuellen Stand, wobei eine gespeicherte Stufe eine niedrigere berechnete überschreibt (zum Beispiel nach dem Löschen von Büchern). Keine Methode wirft: Ausnahmen gehen an `onError`, Rückgabe ist dann leer bzw. `StatsData.Empty`. Schlägt das Schreiben fehl, bleiben die Slices für den nächsten Flush erhalten.

- [ ] **Step 1: Failing Tests schreiben** (echter `StatsStore` auf Temp-Datei, Uhr als veränderbare Variable).

```csharp
[Fact] public async Task FlushPersistsWhatWasObserved()
[Fact] public async Task FlushReturnsOnlyNewlyUnlockedTiers()        // LibraryCount 10 -> erster Flush liefert collector Tier 1, zweiter Flush leer
[Fact] public async Task RaisingATierIsReportedOnce()                // LibraryCount 10 -> 50: collector Tier 2 genau einmal
[Fact] public async Task CompletionUnlocksFinisher()                 // OnCompletedAsync, FlushAsync -> finisher Tier 1
[Fact] public async Task StoredTierSurvivesShrinkingLibrary()        // LibraryCount 50, Flush, dann 5 -> LoadTrophiesAsync: collector Tier 2
[Fact] public async Task BrokenDatabaseNeverThrows()                 // Pfad auf beschädigte Datei: OnPosition, FlushAsync, LoadAsync, LoadTrophiesAsync werfen nicht, onError wurde aufgerufen, LoadAsync ergibt StatsData.Empty
[Fact] public async Task FailedWriteKeepsSlicesForTheNextFlush()     // Datei während des ersten Flush exklusiv geöffnet, danach freigegeben -> zweiter Flush speichert die Zeit
[Fact] public async Task NothingNewMeansNoDatabaseAccess()           // nach erfolgreichem Flush Datei löschen, FlushAsync erneut: Datei wird nicht neu angelegt
```

- [ ] **Step 2: Fehlschlag bestätigen**, **Step 3: implementieren**, **Step 4: Tests ausführen** (`dotnet test Audibly.Stats.Tests`, alle grün).

- [ ] **Step 5: Commit** — `Stats: recorder that flushes listening time and reports unlocked trophies`

---

### Task 6: In den Player einhängen

**Files:**
- Create: `Audibly.App/Services/ListeningStatsService.cs`
- Modify: `Audibly.App/Audibly.App.csproj` (Projektverweis), `Audibly.App/App.xaml.cs`, `Audibly.App/ViewModels/PlayerViewModel.cs`

**Interfaces:**
- Consumes: `ListeningStatsRecorder` aus Task 5.
- Produces:

```csharp
namespace Audibly.App.Services;

public sealed class ListeningStatsService
{
    public ListeningStatsService(string databasePath);
    public void OnPosition(Guid audiobookId, int sourceIndex, double positionSeconds, double speed);
    public void OnInterrupted();
    public void OnCompleted(Guid audiobookId);
    public Task FlushAsync();
    public Task<StatsData> LoadAsync();
    public Task<IReadOnlyList<TrophyState>> LoadTrophiesAsync();
    public event Action? Updated;   // nach jedem Flush, der etwas geschrieben hat
}
```

und `public static ListeningStatsService ListeningStats { get; }` in `App`.

- [ ] **Step 1: `ListeningStatsService` schreiben.** Datenbankpfad `ApplicationData.Current.LocalFolder.Path + @"\stats.db"`. `libraryCount` ist `App.ViewModel.Audiobooks.Count`, `onError` ruft `App.ViewModel.LoggingService.LogError`. Ein `DispatcherQueueTimer` mit 30 Sekunden ruft `FlushAsync`. Für jede von `FlushAsync` gemeldete Trophäe: `App.ViewModel.EnqueueNotification(new Notification { Message = $"Trophy unlocked: {state.Definition.Name} ({TrophyCatalog.TierName(state.Tier)})", Severity = InfoBarSeverity.Success })`. `OnInterrupted` und `OnCompleted` stoßen zusätzlich einen Flush an, ohne darauf zu warten.

- [ ] **Step 2: `PlayerViewModel` einhängen**, jeweils in `try { … } catch (Exception e) { Debug.WriteLine(e); }`:
  - In `PlaybackSession_PositionChanged`, direkt nach `if (IsUserSeeking) return;`: Ist `MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing`, dann `App.ListeningStats.OnPosition(audiobook.Id, audiobook.CurrentSourceFileIndex, position.TotalSeconds, PlaybackSpeed)`, sonst `OnInterrupted()`. Zusätzlich `OnInterrupted()` vor dem `return` der Zeile `if (IsUserSeeking) return;`.
  - Ebenda: `var wasCompleted = audiobook.IsCompleted;` vor der Zuweisung von `IsCompleted`; nach der Zuweisung bei `!wasCompleted && audiobook.IsCompleted` → `App.ListeningStats.OnCompleted(audiobook.Id)`.
  - In `PlaybackSession_PlaybackStateChanged`, Fall `Paused`: `App.ListeningStats.OnInterrupted()`.
  - Dort, wo ein Hörbuch geöffnet wird (bei `AudioPlayer.Close();` in der Öffnen-Methode um Zeile 533): `App.ListeningStats.OnInterrupted()`.

- [ ] **Step 3: Flush beim Beenden.** In `App.xaml.cs` im `Closed`-Handler des Fensters vor `PlayerViewModel.Dispose()`: `await ListeningStats.FlushAsync();`.

- [ ] **Step 4: Bauen und Tests.** Run: `dotnet build Audibly.App -c Debug -p:Platform=x64` und `dotnet test Audibly.Stats.Tests` und `dotnet test Audibly.Audio.Tests` — Expected: Build ohne Fehler (gesperrte Dateien der laufenden Dev-App zählen nicht), alle Tests grün.

- [ ] **Step 5: In der Dev-App prüfen.** `redeploy-dev.ps1` ausführen, ein Hörbuch 2 Minuten bei 2× abspielen, einmal in der Fortschrittsleiste weit springen, pausieren. Dann `stats.db` unter `%LOCALAPPDATA%\Packages\Audibly.DevTest_8hz582d7yec5r\LocalState` nur lesend öffnen. Expected: Summe `listened` zwischen 110 und 125 Sekunden, Summe `saved` ungefähr gleich groß, der Sprung hat keine Minuten hinzugefügt.

- [ ] **Step 6: Commit** — `Stats: record listening time while a book plays`

---

### Task 7: Statistikseite

**Files:**
- Create: `Audibly.App/UserControls/StatsBarChart.xaml(.cs)`, `Audibly.App/Views/StatsPage.xaml(.cs)`
- Modify: `Audibly.App/AppShell.xaml`, `Audibly.App/AppShell.xaml.cs`

**Interfaces:**
- Consumes: `App.ListeningStats.LoadAsync()`, `Updated`, `StatsSummary`, `DurationFormat`, `ChartAxis`, `PeriodTotal`.
- Produces: `StatsBarChart` mit den Dependency Properties `IReadOnlyList<PeriodTotal> Periods` und `double AverageSeconds`; `StatsPage`; Menüpunkt `StatsMenuItem`.

Aufbau der Seite von oben nach unten, in einem `ScrollViewer`, Inhalt maximal 900 Pixel breit und zentriert:

1. Überschrift `Listening statistics`.
2. Zwei Karten nebeneinander (unter 700 Pixel Breite untereinander):
   - `Current title` mit dem Titel des geladenen Hörbuchs, darunter `Listened` und `Time saved` als große Zahlen. Ohne geladenes Hörbuch: `No audiobook open`.
   - `All time` mit `Listened` und `Time saved`.
3. `SelectorBar` mit `Daily`, `Monthly`, `Yearly`; Start auf `Daily`.
4. Darunter mittig wie im Screenshot: kleine Überschrift in Großbuchstaben (`DAILY AVERAGE`, `MONTHLY AVERAGE`, `YEARLY AVERAGE`), darunter groß der Durchschnitt (`DurationFormat.Short`), darunter der Zeitraum (`3 Nov – 14 Nov 2026`, `Jun 2026 – Nov 2026`, `2025 – 2026`).
5. `StatsBarChart`: Balken mit abgerundeten oberen Ecken in der Akzentfarbe, Achsenbeschriftung rechts aus `ChartAxis.For`, gestrichelte waagerechte Linie beim Durchschnitt, Label unter jedem Balken, Tooltip mit `DurationFormat.Short` je Balken. Höhe 260 Pixel. Passen die Balken nicht in die Breite (viele Monate), scrollt das Diagramm waagerecht und zeigt beim Öffnen das rechte Ende.
6. Mittig: `IN MY LIBRARY` und groß `{n} titles` (bei 1: `1 title`).

Leerer Zustand statt 3 bis 5: `No listening recorded yet. Play an audiobook and your statistics will appear here.`

`Daily` zeigt `StatsSummary.LastListenedDays(data, 5)`, `Monthly` zeigt `Months`, `Yearly` zeigt `Years`. Die Seite lädt beim Öffnen und bei `Updated` neu (auf dem UI-Thread) und meldet sich beim Verlassen von `Updated` und `PlayerViewModel.PropertyChanged` ab. Farben nur aus Theme-Ressourcen, damit Hell und Dunkel funktionieren.

- [ ] **Step 1: `StatsBarChart` bauen.** Balken in `OnPeriodsChanged` als `Rectangle` in ein `Grid` mit einer Spalte je Periode; Balkenhöhe = `ListenedSeconds / ChartAxis.MaxSeconds × Zeichenhöhe`, Werte über 0 mindestens 2 Pixel hoch.
- [ ] **Step 2: `StatsPage` bauen** nach dem Aufbau oben.
- [ ] **Step 3: Menüpunkt.** In `AppShell.xaml` nach `NowPlayingMenuItem`: `StatsMenuItem`, Content `Statistics`, Glyph `&#xE9D2;`. In `NavigationView_ItemInvoked` navigiert er `AppAppShellFrame` zu `StatsPage`; die bestehende Methode, die die Auswahl nach einer Navigation nachzieht (Kommentar „Ensures the nav menu reflects reality"), bekommt den Fall `StatsPage`.
- [ ] **Step 4: In der Dev-App prüfen.** `redeploy-dev.ps1`, Seite öffnen, je einen Screenshot von `Daily`, `Monthly`, `Yearly` in Hell und Dunkel sowie vom leeren Zustand (dazu `stats.db` der Dev-App bei beendeter App umbenennen und danach zurückbenennen). Expected: Zahlen stimmen mit einer SQL-Summe über `stats.db` überein; die Summe aller Tagesbalken ist nie größer als `All time`; kein abgeschnittener Text bei 940 Pixel Fensterbreite (Mindestbreite der App).
- [ ] **Step 5: Commit** — `Stats: statistics page with daily, monthly and yearly charts`

---

### Task 8: Trophäenseite

**Files:**
- Create: `Audibly.App/UserControls/TrophyBadge.xaml(.cs)`, `Audibly.App/Views/TrophiesPage.xaml(.cs)`
- Modify: `Audibly.App/AppShell.xaml`, `Audibly.App/AppShell.xaml.cs`

**Interfaces:**
- Consumes: `App.ListeningStats.LoadTrophiesAsync()`, `Updated`, `TrophyState`, `TrophyCatalog.TierName`.
- Produces: `TrophyBadge` mit Dependency Property `TrophyState State`; `TrophiesPage`; Menüpunkt `TrophiesMenuItem`.

Aufbau:

- Überschrift `Trophies`, darunter `{unlocked} of 14 unlocked`.
- Raster aus `TrophyBadge` (`ItemsRepeater` mit `UniformGridLayout`, Zellen 150 × 170, zentriert): Kreis mit 110 Pixel Durchmesser, Glyph mittig, Name darunter.
  - Gesperrt (Tier 0): Kreis ohne Füllung, Rand und Glyph in `TextFillColorDisabledBrush`, Name gedämpft.
  - Freigeschaltet: Kreis mit Verlauf in der Akzentfarbe (dunkel nach hell), Glyph weiß, 5 Pixel breiter Ring in der Stufenfarbe: Bronze `#B0733A`, Silver `#A7B4C2`, Gold `#D9A520`.
- Klick auf ein Abzeichen öffnet einen `ContentDialog` wie im Screenshot: Titel = Name, darunter die Stufe (`Silver` oder `Locked`), `GoalText`, `ProgressText`, ein `ProgressBar` von 0 bis `NextThreshold` (bei Gold voll), Schaltfläche `Close`. Kein Teilen.
- Tastatur: Abzeichen sind fokussierbar und mit Enter oder Leertaste zu öffnen; `AutomationProperties.Name` = `"{Name}, {TierName}"`.

- [ ] **Step 1: `TrophyBadge` bauen.**
- [ ] **Step 2: `TrophiesPage` mit Dialog bauen**; lädt beim Öffnen und bei `Updated`.
- [ ] **Step 3: Menüpunkt** `TrophiesMenuItem` nach `StatsMenuItem`, Content `Trophies`, Glyph `&#xE7C1;`, Navigation wie in Task 7.
- [ ] **Step 4: Glyphen prüfen.** Screenshot des Rasters; jedes der 14 Symbole muss sichtbar und passend sein, sonst in `TrophyCatalog` ersetzen und `dotnet test Audibly.Stats.Tests` erneut ausführen.
- [ ] **Step 5: In der Dev-App prüfen.** Die Testbibliothek der Dev-App hat weniger als 10 Titel, daher für den Test in einer Kopie arbeiten: App beenden, `stats.db` sichern, per SQL 11 Stunden Hörzeit auf heute eintragen, App starten, 40 Sekunden abspielen. Expected: Hinweis `Trophy unlocked: Marathoner (Bronze)` erscheint einmal und nach einem Neustart nicht erneut; das Abzeichen hat einen Bronzering; der Dialog zeigt `Listen for 100 hours in total.` und den Fortschritt. Danach die gesicherte `stats.db` zurückspielen.
- [ ] **Step 6: Commit** — `Stats: trophies page with tiered badges and unlock hint`

---

### Task 9: Abschlussprüfung

- [ ] **Step 1:** `dotnet test Audibly.Stats.Tests`, `dotnet test Audibly.Audio.Tests`, `dotnet run --project Audibly.ReadAlong.Tests` — Expected: alles grün (Read Along: 82 Prüfungen).
- [ ] **Step 2:** In der Dev-App 10 Minuten normal benutzen: abspielen, Geschwindigkeit ändern, Kapitel wechseln, Mini-Player öffnen, Read Along an, App schließen und neu öffnen. Expected: Hördauer wächst nur während der Wiedergabe, bleibt über den Neustart erhalten, Read Along und Geschwindigkeitsregler verhalten sich wie vorher.
- [ ] **Step 3:** `stats.db` bei beendeter App durch eine Textdatei ersetzen und starten. Expected: App startet und spielt, Statistikseite zeigt den leeren Zustand, ein Eintrag steht in `Audibly.log`. Danach die echte Datei zurückspielen.
- [ ] **Step 4:** Ergebnis dem Nutzer berichten, mit den Screenshots aus Task 7 und 8. Kein Push ohne Zustimmung.
