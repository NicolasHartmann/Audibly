# Player: Fortschritt über den ganzen Titel und wählbare Zeitanzeige

## Absicht

Der Player zeigt heute nur das laufende Kapitel: links die Position im Kapitel, rechts die Kapitellänge, und der Regler reicht nur über das Kapitel. Der Nutzer will sehen, wie lange es bis zum Ende des ganzen Hörbuchs noch dauert, und selbst wählen, was der Player anzeigt.

## Ziele

- Zwei Einstellungen, beide dauerhaft gespeichert:
  - **Fortschrittsleiste:** Kapitel (wie bisher) oder vollständiger Titel.
  - **Zeitanzeige:** verbleibende Zeit, gehörte Zeit / Gesamtzeit (wie bisher) oder Prozent.
- Die verbleibende Zeit rechnet die aktuelle Wiedergabegeschwindigkeit ein und ändert sich sofort, wenn die Geschwindigkeit geändert wird.
- Gehörte Zeit und Gesamtzeit sind Zeiten des Hörbuchs selbst und hängen nicht von der Geschwindigkeit ab (vom Nutzer so entschieden: bei 2×, Stunde 4 von 10 steht dort 4:00 / 10:00, verbleibend 3:00).
- Ohne Änderung der Einstellungen sieht der Player aus wie heute.

## Nicht-Ziele

- Keine Änderung an der Kapitelauswahl, an den Sprungtasten oder an der Statistik.
- Die alte Player-Seite (`Views/Legacy`) bleibt, wie sie ist.

## Was auf keinen Fall passieren darf

- Ziehen am Regler über den ganzen Titel springt an eine falsche Stelle oder bleibt bei Büchern aus mehreren Dateien in der aktuellen Datei hängen.
- Die Anzeige springt oder flackert beim Wechsel von Kapitel, Datei oder Geschwindigkeit.

## Anzeige

Links steht immer die gehörte Zeit des gewählten Bereichs (Kapitel oder Titel). Rechts steht je nach Zeitanzeige:

| Zeitanzeige | Rechts | Beispiel (Titel, 2×, Stunde 4 von 10) |
| --- | --- | --- |
| Gehörte Zeit / Gesamtzeit | Gesamtlänge des Bereichs | `4:00:00` … `10:00:00` |
| Verbleibende Zeit | Restzeit bei aktueller Geschwindigkeit, mit Minus | `4:00:00` … `-3:00:00` |
| Prozent | Anteil des Bereichs, ganze Zahl | `4:00:00` … `40 %` |

Der Regler und sein Tooltip beim Ziehen folgen dem gewählten Bereich.

## Bedienung

- **Optionen:** neuer Abschnitt "Player" mit zwei Auswahlgruppen (Radio-Buttons): "Progress bar" (Chapter / Full title) und "Time display" (Time remaining / Elapsed and total / Percent). Texte englisch wie der Rest der App.
- **Im Player:** Ein Klick auf die rechte Zeit schaltet die Zeitanzeige zur nächsten Variante weiter. Die Fortschrittsleiste wird nur in den Optionen umgestellt.
- Gilt für die Leiste im Hauptfenster und für den Regler im Mini-Player. Die Zeilen "Chapter …" und "Book …" im Mini-Player bleiben.

## Ansätze

1. **Anzeige-Eigenschaften im PlayerViewModel (gewählt).** Das ViewModel bekommt drei Eigenschaften für links, rechts und den Regler (Position und Maximum), die je nach Einstellung Kapitel- oder Titelwerte liefern. Die Berechnung liegt in einer reinen Hilfsklasse mit Tests. Beide Player binden an dieselben Eigenschaften und bleiben dadurch gleich.
2. **Umschalten in jeder Ansicht.** Jede Ansicht entscheidet selbst, welche Werte sie bindet. Das dupliziert die Logik in Hauptfenster und Mini-Player und läuft leicht auseinander.
3. **Zweiter Regler für den Titel zusätzlich zum Kapitelregler.** Zeigt beides gleichzeitig, braucht aber mehr Platz und ist nicht das, was gewünscht war (entweder – oder).

## Entscheidungen des Umsetzers (nicht vom Nutzer festgelegt)

- Standard: Kapitel + gehörte Zeit / Gesamtzeit, also das heutige Verhalten.
- Sprung über Dateigrenzen nutzt den vorhandenen Weg im `PlayerViewModel` (Datei öffnen und an Position starten), den auch Read Along verwendet.
- Die Restzeit-Berechnung nutzt die vorhandene Klasse `PlaybackTimeRemaining`.
- Einstellungen liegen in `UserSettings` wie Lautstärke und Geschwindigkeit.

## Dem Umsetzer überlassen

- Namen der Eigenschaften und der Hilfsklasse, genaue Gestaltung des Abschnitts in den Optionen.
