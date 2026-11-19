# Buchinfos aus dem Internet (Windows, Testphase)

Gehört zum To-do-Punkt "Fill in a book's metadata from the internet". Am 19.11.2026 mit dem Nutzer festgelegt: Weg 2 (Audible suchen, Audnexus liefert die Daten), erst nur Windows, Buch für Buch mit Auswahl, Übernahme mit Häkchen pro Feld.

## Absicht

Bücher bekommen verlässliche Angaben zu Beschreibung, Genres, Serie und Teil, Sprecher und Erscheinungsdatum. Die Datei-Tags sind oft leer oder falsch, vor allem bei gerippten CDs. Die Genres sind später der Ausgangspunkt für eigene Tags (nächster To-do-Punkt).

Die Bücher sind meist deutsch oder englisch und stammen von Audible oder von gerippten CDs. Audible.de führt auch viele CD-Hörbücher.

## Ziele

- Im Kontextmenü eines Buchs (Bibliothek, Windows) gibt es den Eintrag "Look up online…".
- Die App sucht bei Audible nach Titel und Autor und zeigt eine Trefferliste. Der Nutzer wählt einen Treffer oder ändert die Suche.
- Danach zeigt sie pro Feld den alten und den neuen Wert, jeweils mit Häkchen. Übernommen wird nur, was angehakt ist.
- Neue Felder: Sprecher, Serie, Teil der Serie, Genres. Beschreibung und Erscheinungsdatum gibt es schon.
- "More Info" zeigt die neuen Felder an.

## Nicht-Ziele

- Keine automatische Suche für die ganze Bibliothek. Das kommt erst später, nach der Testphase.
- Kein Android in dieser Stufe. Die Datenbankfelder gelten aber auf beiden Plattformen, weil beide dieselben Migrationen nutzen.
- Kein Cover-Wechsel. Das ist ein eigener To-do-Punkt, auch wenn Audnexus ein Bild liefert.
- Keine eigenen Tags und keine Filter nach Genre. Das gehört zum nächsten To-do-Punkt.
- Der Abgleich zwischen Geräten trägt keine Buchinfos.

## Was auf keinen Fall passieren darf

- Etwas wird ohne Bestätigung überschrieben.
- Ein leerer Wert von Audible löscht einen vorhandenen Wert.
- Die Dubletten-Erkennung beim Import ändert sich. Sie prüft Titel, Autor und `Composer`, deshalb bleibt `Composer` unangetastet (siehe Sprecher).
- Kein Netz, ein Fehler bei Audible oder Audnexus oder eine geänderte Antwort bringt die App zum Absturz oder beschädigt das Buch. Stattdessen erscheint eine Meldung im Dialog.

## Ansätze

1. **Nur der Audible-Katalog.** Eine Quelle, aber die Genres kommen dort als verschachtelte Leitern.
2. **Audible zum Suchen, Audnexus für die Daten (gewählt).** Audnexus (`api.audnex.us`, der Dienst hinter Audiobookshelf) liefert ein sauberes Buchobjekt mit Genres und feineren Tags, Serie, Sprechern und Beschreibung. Dafür hängt die App an einem zweiten fremden Dienst.
3. **Die Audible-Webseite auslesen.** Verworfen, weil das bricht, sobald Amazon die Seite umbaut.

Beide Schnittstellen sind inoffiziell und brauchen keinen Schlüssel. Ändern sie sich, fällt nur die Suche aus.

## Aufbau

### Suche (`Audibly.Core/Metadata`, plattformneutral, mit Tests)

- `GET https://api.audible.de/1.0/catalog/products?title=…&author=…&num_results=10&products_sort_by=Relevance&response_groups=contributors,series,product_attrs,media` und dasselbe für `api.audible.com`. Beide Anfragen laufen gleichzeitig. Die Treffer von .de kommen zuerst, Dubletten (gleiche ASIN) fallen weg.
- Ein Treffer in der Liste zeigt: Cover-Vorschau, Titel, Autor, Sprecher, Serie und Teil, Länge, Jahr, Sprache und Land (DE/US). An Länge und Sprache erkennt man die richtige Ausgabe.
- Nach der Auswahl: `GET https://api.audnex.us/books/{asin}?region=de` bzw. `region=us`, je nach Land des Treffers.
- Die Antworten werden in ein eigenes Ergebnis-Objekt übersetzt. HTML in der Beschreibung (`summary`) wird zu reinem Text mit Absätzen. Die Tests prüfen das Übersetzen an gespeicherten Beispielantworten, nicht am echten Netz.
- `HttpClient` mit Zeitlimit von etwa 15 Sekunden.

### Felder (`Audiobook`, eine EF-Migration)

| Feld | Herkunft (Audnexus) | Neu? |
|---|---|---|
| `Description` | `summary` als Text, sonst `description` | vorhanden |
| `ReleaseDate` | `releaseDate` | vorhanden |
| `Narrator` (Text) | `narrators`, mit ", " verbunden | neu |
| `Series` (Text) | `seriesPrimary.name` | neu |
| `SeriesPart` (Text) | `seriesPrimary.position` | neu, Text, weil Audible auch "1-3" liefert |
| `Genres` (Text) | `genres`, Namen zeilenweise; erst `type=genre`, dann `type=tag` | neu |
| `Asin` (Text) | gewählter Treffer | neu, damit sich das Buch später ohne Suche erneut abfragen lässt |

Alle neuen Spalten dürfen leer sein. Bestehende Bücher behalten nach der Migration alles.

**Sprecher:** "More Info" zeigt `Narrator`, falls gesetzt, sonst wie bisher `Composer`.

### Speichern

Neue Repository-Methode `SaveMetadataAsync(Guid id, …)`, die nur diese Spalten schreibt. Sie folgt dem Muster von `RenameAsync`. Das ganze Buch zu speichern würde jede Datei und jedes Kapitel neu schreiben. Danach wird das `AudiobookViewModel` aktualisiert.

### Oberfläche (Windows)

Ein `ContentDialog` mit zwei Schritten:

```
Look up online
[ Der Herr der Ringe  J.R.R. Tolkien      ] [Search]
┌──────────────────────────────────────────────────────┐
│ [img] Der Herr der Ringe. Das Hörspiel                │
│       Tolkien · Steffen, Beck, … · 11h 26m · 2010 · DE│
│ [img] The Lord of the Rings …           · EN · US     │
└──────────────────────────────────────────────────────┘
                                  [Cancel]  [Next]
```

```
Apply details              old                 new
[x] Narrator               —                   Manfred Steffen, …
[x] Series                 —                   —            (leer → nicht wählbar)
[ ] Description            Wir schreiben …     Wir schreiben …
[x] Genres                 —                   Fantasy, Epic Fantasy, …
[x] Release date           2010                04.11.2010
                                  [Back]  [Apply]
```

- Häkchen sind gesetzt, wo der neue Wert nicht leer ist und vom alten abweicht. Leere neue Werte sind nicht wählbar.
- Während der Suche dreht sich ein Ladering. Fehler und "keine Treffer" stehen als Text im Dialog.
- Die Texte der Oberfläche sind englisch, wie der Rest der App.

## Entscheidungen des Umsetzers (nicht vom Nutzer festgelegt)

- Eigenes Feld `Narrator` statt `Composer` überschreiben (Grund: Dubletten-Erkennung beim Import).
- Genres vorerst als ein Textfeld, eine Zeile pro Genre. Der Tag-Punkt baut seine eigene Struktur und übernimmt diese Liste als Startwert. So entsteht jetzt keine Tabelle, deren Form erst die Tags festlegen.
- `Asin` wird mitgespeichert.
- .de und .com werden immer beide abgefragt, nicht nur bei wenigen Treffern.

## Offen für den Umsetzer

- Wo genau der Menüeintrag im Kontextmenü steht und wie die Felder in "More Info" angeordnet sind.
- Wie lang die Beschreibung im Vergleich gekürzt angezeigt wird.
