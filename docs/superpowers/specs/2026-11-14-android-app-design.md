# Audibly für Android, erste Version: Abspielen und Bibliothek

Gehört zum Fahrplan `docs/superpowers/plans/2026-11-14-sync-and-android-roadmap.md`. Die dort am 14.11.2026 getroffenen Entscheidungen gelten.

## Absicht

Der Nutzer will seine Hörbücher auch auf dem Android-Handy hören. Die erste Version spielt Bücher ab, die er selbst aufs Handy kopiert hat, und zeigt sie in einer Bibliothek. Der Abgleich mit dem PC kommt danach.

## Ziele

- Einen Ordner auf dem Handy auswählen; die Hörbücher darin erscheinen in der Bibliothek (Cover, Titel, Autor, Fortschritt).
- Ein Buch abspielen: Play/Pause, 10 s zurück, 30 s vor, Kapitel vor/zurück, Kapitelauswahl, Geschwindigkeit 0,5× bis 4× bei gleicher Tonhöhe, Schlaf-Timer.
- Die Wiedergabe läuft bei ausgeschaltetem Bildschirm weiter und lässt sich über Benachrichtigung, Sperrbildschirm, Kopfhörer und Bluetooth steuern.
- Die Position wird gespeichert; nach einem Neustart geht es an derselben Stelle weiter.
- Bücher aus einer Datei (M4B) und aus mehreren Dateien (MP3-Ordner) funktionieren, mit denselben Kapitelnamen wie unter Windows.
- Die Hörzeit wird ab dem ersten Tag aufgezeichnet (`Audibly.Stats`), auch wenn die Statistikseiten erst später kommen. So fehlt beim späteren Abgleich nichts.

## Nicht-Ziele der ersten Version

- Abgleich mit OneDrive, Statistik- und Trophäenseiten, Read Along, Lesezeichen.
- Laden von Hörbüchern aus OneDrive.
- iOS. Veröffentlichung im Play Store.

## Was auf keinen Fall passieren darf

- Die Wiedergabe bricht ab, wenn der Bildschirm ausgeht oder die App in den Hintergrund geht.
- Die Position geht verloren, wenn Android die App beendet.
- Die Windows-App verändert ihr Verhalten durch den Umzug gemeinsamer Teile.

## Ansätze

1. **.NET MAUI mit gemeinsamer Kernbibliothek (gewählt, vom Nutzer bestätigt).** Neue Bibliothek `Audibly.Core` (plattformneutral) mit der Rechenlogik, die heute in der Windows-App liegt, und einem Import, der mit Datenströmen statt Dateipfaden arbeitet. Die Android-App nutzt `Audibly.Core`, `Audibly.Models`, `Audibly.Repository` und `Audibly.Stats`.
2. **Uno Platform.** Mehr Oberfläche übernehmbar, aber die App hängt an Windows-eigenen Teilen und das Desktop-Layout passt nicht aufs Handy.
3. **Kotlin.** Nichts wiederverwendbar; Statistik und später Abgleich doppelt zu pflegen.

## Aufbau

- **`Audibly.Core`** (net8.0, neu): `PlaybackTimeRemaining`, `PlayerProgress`, `PlaybackSpeedPresets`, `ChapterSleepTimer`, `FileChapterTitles` ziehen aus der Windows-App hierher um. Neu: `AudiobookImporter`, der aus einer Liste von Audiodateien (Name + Datenstrom) ein `Audiobook` mit Kapiteln baut, und `PlaybackPosition` für die Schritte, die beide Apps brauchen (Kapitel zu einer Stelle finden, nächste/vorige Datei).
- **`Audibly.Mobile`** (net10.0-android, neu, eigene Projektmappe `Audibly.Mobile.sln`): Oberfläche in MAUI, Android-Dienst für die Wiedergabe.
- Die bestehende Projektmappe `Audibly.sln` bleibt mit .NET 8 baubar; sie bekommt nur `Audibly.Core` dazu.

### Wiedergabe

Ein Android-Dienst (`MediaSessionService` aus Media3) besitzt den Player (ExoPlayer) und die Mediensitzung. Die Oberfläche spricht nur über eine schmale Schnittstelle (`IPlaybackService`: Öffnen, Play, Pause, Springen, Geschwindigkeit, Positionsereignisse) mit ihm. ExoPlayer hält die Tonhöhe bei geänderter Geschwindigkeit selbst; der eigene Tempo-Player der Windows-App wird nicht gebraucht.

Bücher aus mehreren Dateien werden dem Player als Wiedergabeliste übergeben, sodass der Übergang zwischen Dateien ohne Lücke und ohne eigenes Umschalten läuft.

### Dateien

Android gibt nur Zugriff auf Ordner, die der Nutzer in der Systemauswahl freigibt. Die App merkt sich die Freigabe dauerhaft und speichert zu jeder Datei ihre Android-Adresse (Content-URI) statt eines Dateipfads. Tags und Cover werden über Datenströme gelesen.

Ein Unterordner mit mehreren Audiodateien ist ein Buch; eine einzelne M4B- oder MP3-Datei direkt im gewählten Ordner ist ebenfalls ein Buch.

### Daten

Dieselbe Bibliotheksdatenbank (`Audibly.Repository`, SQLite) und dieselbe Statistikdatei (`stats.db`) wie unter Windows, im privaten Speicher der App.

### Oberfläche

Drei Seiten: Bibliothek (Raster mit Covern), Player (Cover, Titel, Kapitel, Leiste, Tasten, Geschwindigkeit, Timer), Einstellungen (Ordner, Fortschrittsleiste Kapitel/Titel, Zeitanzeige). Am unteren Rand der Bibliothek eine kleine Leiste mit dem laufenden Buch. Texte englisch.

## Entscheidungen des Umsetzers (nicht vom Nutzer festgelegt)

- Start auf .NET 10; älteste Android-Version: Android 8 (API 26).
- Werkzeuge (NET 10 SDK, MAUI-Arbeitslast, Android SDK, JDK) werden in einen eigenen Ordner auf `D:` installiert, ohne die systemweite .NET-Installation zu ändern.
- Der Import der Windows-App bleibt in dieser Stufe unverändert; nur die fünf Rechenhelfer ziehen um. Das Zusammenlegen der Importe ist ein späterer Schritt.
- Die erste Geräteprüfung läuft im Android-Emulator; das echte Handy prüft der Nutzer.

## Dem Umsetzer überlassen

- Gestaltung der Seiten im Detail, Namen der Klassen, Aufteilung der ViewModels.
