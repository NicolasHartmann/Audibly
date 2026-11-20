# Tags für Hörbücher (Windows)

To-do "Genres / tags for audiobooks". Festgelegt mit dem Nutzer am 20.11.2026: eigene Tags, Audible nur als Vorschlag (B); mehrere Bücher auf einmal taggen; Speicherung als Textfeld pro Buch (Weg 1).

## Absicht

Bibliothek nach Tags filtern und in der Statistik sehen, wie viel pro Tag gehört wird. Wer eine ganze Bibliothek einfügt, soll viele Bücher auf einmal taggen können.

## Ziele

- Jedes Buch hat eine Liste eigener Tags. Bearbeiten: Rechtsklick → "Edit tags…".
- Mehrfachauswahl in der Bibliothek; "Edit tags…" für alle markierten Bücher: Tags hinzufügen oder entfernen.
- Beim Tippen Vorschläge aus den Tags, die es schon gibt. Gleiche Tags ohne Rücksicht auf Groß-/Kleinschreibung gelten als ein Tag (die vorhandene Schreibweise gewinnt).
- "Look up online": Die Zeile "Genres" heißt "Tags" und fügt gefundene Genres zu den vorhandenen Tags hinzu; eigene Tags bleiben.
- Einmalig: Die schon gespeicherten Genres werden die Tags der Bücher.
- Filter-Menü: unter dem Status ein Abschnitt "Tags" mit Häkchen. Mehrere Häkchen = eins davon reicht. Status und Tags zusammen = beides muss passen.
- Statistik: Bereich "By tag" mit Hörzeit pro Tag für den gewählten Zeitraum. Ein Buch mit zwei Tags zählt bei beiden. Bücher ohne Tag unter "No tag".

## Nicht-Ziele

- Kein "Look up online" für die ganze Bibliothek (später).
- Kein Android (die Spalte gilt dort aber, weil beide dieselben Migrationen nutzen).
- Keine Tag-Farben, kein eigener Tag-Verwalter (umbenennen/zusammenlegen) in dieser Stufe.
- Der Abgleich zwischen Geräten trägt keine Tags.

## Was nicht passieren darf

- Tags gehen verloren, wenn "Look up online" läuft oder Bücher neu eingelesen werden.
- Bulk-Bearbeitung ändert Bücher, die nicht markiert waren.
- Speichern blockiert die Oberfläche bei vielen Büchern.

## Ansätze

1. **Textfeld `Tags` pro Buch, ein Tag pro Zeile (gewählt).** Eine Migration, Filter und Statistik auf der Bibliothek im Speicher, Android kann später leicht folgen. Keine Extras pro Tag.
2. Eigene Tabellen Tag und Buch↔Tag: sauberer, aber mehr Migration und Code für dieselbe Funktion.
3. Eigene Datei neben der Datenbank: verworfen, Daten getrennt vom Buch.

## Aufbau

- **Daten:** neue Spalte `Audiobook.Tags` (Text, darf leer sein). Die Migration kopiert `Genres` nach `Tags`. `Genres` bleibt als Rohdaten von Audible.
- **Logik in `Audibly.Core/Tags`** (plattformneutral, mit Tests): Tags lesen/schreiben (Zeilen, getrimmt, ohne Dubletten unabhängig von Groß-/Kleinschreibung), Tags hinzufügen/entfernen, Filter (Status UND eins der Tags), Hörzeit pro Tag aus den Statistik-Scheiben und den Tags der Bücher.
- **Speichern:** Repository-Methode, die nur `Tags` schreibt, für viele Bücher in einem Aufruf.
- **Look up online:** Feld `Genres` im Vergleich wird zu `Tags`; Übernehmen = vereinigen, nicht ersetzen. Vorab angehakt, wenn neue Tags dazukämen.
- **Oberfläche Windows:**
  - Bibliothek: Mehrfachauswahl (Strg/Umschalt-Klick); bei mehreren markierten Büchern zeigt das Kontextmenü "Edit tags… (N books)".
  - Dialog "Edit tags": Chips mit ✕, Eingabefeld mit Vorschlägen. Bei mehreren Büchern: Tags, die alle haben, normal; Tags, die nur manche haben, blass mit Hinweis; ✕ entfernt bei allen, Hinzufügen fügt bei allen hinzu.
  - Filter-Menü: Abschnitt "Tags", alphabetisch, mit Anzahl Bücher.
  - Statistik: Bereich "By tag" als Liste mit Balken, sortiert nach Hörzeit.

```
Edit tags – 20 books
[Fantasy ✕] [Hörspiel ✕] (Epic Fantasy ✕  · 3 of 20)
[ add tag…            ]
```

## Entscheidungen des Umsetzers

- `Genres` bleibt neben `Tags` bestehen (Rohdaten, nichts geht verloren).
- Bei gleichen Tags mit anderer Schreibweise gewinnt die zuerst vorhandene.
- "No tag" in der Statistik, damit die Summe stimmt.

## Offen für den Umsetzer

- Genaue Art der Mehrfachauswahl (GridView-Auswahlmodus) und wie das Kontextmenü bei Auswahl erscheint.
- Darstellung der Balken in "By tag".
