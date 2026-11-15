# Abgleich zwischen Windows und Android

Gehört zum Fahrplan `docs/superpowers/plans/2026-11-14-sync-and-android-roadmap.md`. Die dort am 14.11.2026 getroffenen Entscheidungen gelten: Dateien in einem Ordner, den ein Cloud-Dienst wie OneDrive überträgt; auf dem Handy zuerst ohne eigene Anmeldung; vor dem Übernehmen einer fremden Position wird gefragt.

## Absicht

Auf dem PC hören und auf dem Handy an derselben Stelle weiterhören, und umgekehrt. Hörzeit und Trophäen zählen über alle Geräte zusammen.

## Ziele

- In beiden Apps lässt sich ein Abgleich-Ordner wählen. Ohne gewählten Ordner verhält sich die App wie bisher.
- Jedes Gerät schreibt seinen Stand in eigene Dateien in diesem Ordner und liest die Dateien der anderen Geräte.
- Beim Öffnen eines Buchs: Hat ein anderes Gerät dasselbe Buch später gehört und steht an einer anderen Stelle, fragt die App, ob sie dorthin springen soll, und nennt Gerät, Stelle und Zeitpunkt.
- Statistik und Trophäen zeigen die Summe aller Geräte, ohne etwas doppelt zu zählen.
- Dasselbe Buch wird auf zwei Geräten erkannt, auch wenn es auf einem aus mehreren Dateien und auf dem anderen aus einer Datei besteht.

## Nicht-Ziele

- Die Hörbuchdateien selbst, Cover, Lesezeichen, Read-Along-Daten und Einstellungen werden nicht abgeglichen.
- Kein eigener Zugang zu OneDrive mit Anmeldung in dieser Stufe.
- Kein Abgleich in Echtzeit: Der Stand kommt so schnell an, wie der Cloud-Dienst die Dateien überträgt.

## Was auf keinen Fall passieren darf

- Die Position springt ohne Nachfrage.
- Hörzeit wird doppelt gezählt, egal wie oft abgeglichen wird.
- Ein fehlender, voller oder kaputter Abgleich-Ordner stört die Wiedergabe oder beschädigt die Bibliothek.
- Zwei Geräte überschreiben sich gegenseitig Dateien, sodass der Cloud-Dienst Konfliktkopien anlegt.

## Ansätze

1. **Eigene Dateien je Gerät in einem Ordner (gewählt, im Fahrplan bestätigt).** Kein Server, keine Anmeldung auf Windows, keine Konflikte, weil jedes Gerät nur seine eigenen Dateien schreibt.
2. **Eine gemeinsame Datei für alle Geräte.** Einfacher zu lesen, aber zwei Geräte, die gleichzeitig schreiben, erzeugen Konfliktkopien.
3. **Direkter Zugriff auf OneDrive mit Anmeldung.** Unabhängig vom Ordnerzugriff des Handys, verlangt aber eine Registrierung der App bei Microsoft und bindet an einen Dienst. Bleibt der Ausweg, falls der Ordnerzugriff auf dem Handy nicht zuverlässig ist.

## Aufbau

Neue plattformneutrale Bibliothek `Audibly.Sync` (net8.0, mit Tests) für Dateiformat, Bucherkennung und Zusammenführen. Sie greift nie selbst auf Dateien zu, sondern über eine kleine Schnittstelle, die Windows mit einem normalen Ordner und Android mit dem Ordnerzugriff des Systems erfüllt.

### Ordner

```
<Abgleich-Ordner>/
  audibly-sync/
    <gerätekennung>.json      alles, was dieses Gerät meldet
```

Eine Datei je Gerät. Sie enthält Formatversion, Gerätename, Zeitpunkt des Schreibens, die Positionen und die Hörzeit dieses Geräts. Die Gerätekennung wird beim ersten Abgleich zufällig erzeugt und bleibt.

### Dasselbe Buch erkennen

Ein Buch gilt als dasselbe, wenn Titel und Autor übereinstimmen (ohne Groß-/Kleinschreibung, Satzzeichen und doppelte Leerzeichen). Stimmen die Titel nicht überein, gilt es trotzdem als dasselbe, wenn der Autor übereinstimmt und die Gesamtlänge höchstens zwei Sekunden abweicht. Der zweite Weg fängt den Fall ab, dass ein Gerät den Titel aus dem Album und das andere aus dem ersten Kapitel gelesen hat.

### Position

Übertragen wird die Stelle im ganzen Buch in Sekunden, dazu Geschwindigkeit, "abgeschlossen" und der Zeitpunkt, zu dem das Gerät das Buch zuletzt gehört hat. Jedes Gerät rechnet sie in seine eigene Datei und Stelle um.

Gefragt wird, wenn der fremde Zeitpunkt neuer ist als der eigene und die Stellen mehr als 30 Sekunden auseinanderliegen. Lehnt der Nutzer ab, wird für diesen fremden Stand nicht noch einmal gefragt.

### Hörzeit

Jedes Gerät meldet nur, was auf ihm gehört wurde, je Tag, Stunde und Buch, dazu abgeschlossene Bücher. Die Anzeige addiert den eigenen Stand und die Dateien der anderen Geräte. Weil kein Gerät fremde Hörzeit in seinen eigenen Stand übernimmt, kann nichts doppelt gezählt werden.

Trophäen werden aus der Summe berechnet; einmal erreichte Stufen bleiben auf dem Gerät, das sie gesehen hat.

### Wann

- Schreiben: bei Pause, Buchwechsel, Beenden, wenn die App in den Hintergrund geht, und während der Wiedergabe höchstens einmal pro Minute.
- Lesen: beim Start, beim Öffnen eines Buchs und beim Öffnen der Statistik.

Schreiben und Lesen laufen im Hintergrund. Fehler werden protokolliert und in den Einstellungen als Zustand angezeigt ("Last sync failed: …"), sie unterbrechen nichts.

### Oberfläche

- Einstellungen in beiden Apps: "Sync folder" mit Auswahl, Anzeige des Ordners, des letzten erfolgreichen Abgleichs und der anderen gefundenen Geräte, sowie "Turn off".
- Beim Öffnen eines Buchs die Frage, zum Beispiel: "On Pixel 8 you were at 4:20:23 (today 21:10). Continue there?" mit "Continue there" und "Stay here".

## Entscheidungen des Umsetzers (nicht vom Nutzer festgelegt)

- Eine Datei je Gerät statt mehrerer; sie bleibt klein (einige hundert Kilobyte nach Jahren).
- Grenze von 30 Sekunden für die Nachfrage und zwei Sekunden für die Längenprüfung.
- Der Gerätename ist der Name des Rechners bzw. des Handymodells; er steht nur in der Datei im eigenen Abgleich-Ordner.
- Zeitpunkte stehen als UTC in der Datei, Tage und Stunden der Hörzeit in Ortszeit des Geräts, auf dem gehört wurde.
- Bücher, die es nur auf einem anderen Gerät gibt, zählen in der Statistik mit, erscheinen aber nicht in der Bibliothek.

## Offenes Risiko

Ob die OneDrive-App auf Android ihren Ordner über die Ordnerauswahl des Systems freigibt, lässt sich nur auf einem echten Handy mit OneDrive prüfen. Der Abgleich auf Android wird deshalb so gebaut, dass jeder Ordner funktioniert, den das System zur Auswahl anbietet. Bietet OneDrive keinen an, gibt es zwei Auswege: ein Ordner auf dem Handy, den eine Synchronisier-App mit OneDrive abgleicht, oder Ansatz 3.

## Dem Umsetzer überlassen

- Namen der Klassen und Felder, genaue Gestaltung der Einstellungen und der Nachfrage.
