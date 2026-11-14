# Fahrplan: Abgleich über OneDrive und Audibly für Android

Stand: 14.11.2026. Das ist ein Fahrplan mit Entscheidungen und Reihenfolge, noch kein Umsetzungsplan mit einzelnen Schritten. Vor dem Bauen jedes Teils entsteht ein eigener Entwurf und Umsetzungsplan.

## Ziel

Auf dem PC hören, auf dem Handy an derselben Stelle weiterhören und umgekehrt. Die Hörstatistik und die Trophäen zählen über beide Geräte zusammen.

## Das sind zwei Projekte

1. **Abgleich über OneDrive** (Position und Statistik), zuerst nur zwischen Windows-Geräten nutzbar.
2. **Audibly für Android**, das denselben Abgleich mitbringt.

Reihenfolge: erst der Abgleich. Das Android-Projekt braucht sein Dateiformat, und er lässt sich mit zwei Windows-Installationen (Store-Version und Dev-Version oder zweiter PC) testen, bevor es eine Android-App gibt.

---

## Projekt 1: Abgleich über OneDrive

### Grundidee

Die App schreibt kleine Dateien in einen Ordner, den der Nutzer auswählt. Liegt dieser Ordner in OneDrive, überträgt der OneDrive-Client sie. Die Windows-App braucht dafür keine Anmeldung und keinen eigenen Zugang zu OneDrive.

### Drei Dinge, die der Entwurf lösen muss

**1. Dasselbe Buch auf zwei Geräten erkennen.** Heute bekommt jedes Buch beim Import eine zufällige Kennung, die auf jedem Gerät anders ist. Der Abgleich braucht eine Kennung, die sich aus dem Buch selbst ergibt.

- Vorschlag: Titel + Autor + Gesamtlänge in Sekunden, daraus ein Fingerabdruck.
- Offen: Dasselbe Buch kann auf einem Gerät aus 19 MP3-Dateien und auf dem anderen aus einer M4B-Datei bestehen. Darum wird die Position als Stelle im ganzen Buch übertragen (Sekunden ab Anfang), nicht als Datei + Stelle. Die Umrechnung gibt es seit der Fortschrittsanzeige über den ganzen Titel (`PlayerProgress.Locate`).

**2. Keine Konflikte in OneDrive.** Schreiben zwei Geräte dieselbe Datei, legt OneDrive Konfliktkopien an. Darum schreibt jedes Gerät nur in seine eigenen Dateien und liest die der anderen.

```
Audibly-Sync/
  devices/<gerätekennung>/
    device.json       Name des Geräts, App-Version, Formatversion
    positions.json    je Buch: Stelle im Buch, Geschwindigkeit, fertig ja/nein, Zeitstempel
    stats.json        Hörzeit je Tag, Stunde und Buch; Abschlüsse
```

**3. Zusammenführen ohne doppelt zu zählen.**

- **Position:** Der neueste Zeitstempel gewinnt. Liegt die fremde Stelle weiter vorn als die eigene, fragt die App nach ("Auf dem Handy bist du bei 4:20:23. Dorthin springen?"), statt still zurückzuspringen.
- **Statistik:** Jedes Gerät meldet nur, was auf ihm gehört wurde. Die Anzeige addiert alle Geräte. So wird nichts doppelt gezählt, egal wie oft abgeglichen wird.
- **Trophäen:** Sie werden aus der Summe neu berechnet; einmal erreichte Stufen bleiben.

### Wann abgeglichen wird

- Schreiben: bei Pause, Buchwechsel, Beenden und alle paar Minuten während der Wiedergabe.
- Lesen: beim Start, beim Öffnen eines Buchs und wenn sich Dateien im Ordner ändern.

### Was sich in der Windows-App ändert

- Option "Sync folder" mit Ordnerauswahl und Anzeige des letzten Abgleichs.
- Neue Bibliothek `Audibly.Sync` (plattformneutral, mit Tests): Dateiformat, Fingerabdruck, Zusammenführen.
- `Audibly.Stats` liest zusätzlich die Statistik fremder Geräte.
- Hinweis beim Öffnen eines Buchs, wenn ein anderes Gerät weiter ist.

### Nicht enthalten

- Die Hörbuchdateien selbst werden nicht abgeglichen.
- Lesezeichen, Read-Along-Daten und Einstellungen kommen später, falls gewünscht.

### Risiken

- OneDrive überträgt verzögert. Der Abgleich ist darum "irgendwann gleich", nicht sofort.
- Stimmen Titel oder Länge auf zwei Geräten nicht überein (andere Ausgabe, anders getaggt), wird das Buch nicht erkannt. Dafür braucht es eine Möglichkeit, zwei Bücher von Hand zu verbinden.

---

## Projekt 2: Audibly für Android

### Was sich wiederverwenden lässt

Der Code der App teilt sich so auf (grob 14.700 Zeilen C#, 4.400 Zeilen Oberfläche):

| Teil | Auf Android nutzbar? |
| --- | --- |
| `Audibly.Models`, `Audibly.Repository` (Bibliothek, Datenbank) | Ja, plattformneutral |
| `Audibly.Stats` (Statistik, Trophäen) | Ja |
| `Audibly.Sync` (aus Projekt 1) | Ja, dafür wird es so gebaut |
| `Audibly.ReadAlong` (Textabgleich) | Die Logik ja; das Erkennen der Sprache auf dem Handy ist fraglich (siehe unten) |
| Rechenhelfer der App (Restzeit, Fortschritt, Kapitel) | Ja, nach Umzug in eine gemeinsame Bibliothek |
| Oberfläche (WinUI) | Nein, wird neu gebaut |
| Wiedergabe (Windows MediaPlayer, eigener Tempo-Player) | Nein, wird neu gebaut |
| Fenster, Mini-Player, Dateidialoge | Nein |

Die Oberfläche und die Wiedergabe sind der größte Teil der Arbeit. Ein Handy braucht ohnehin eine andere Aufteilung als ein Desktop-Fenster.

### Technik: drei Wege

1. **.NET MAUI (empfohlen).** Die Android-App wird in C# geschrieben und nutzt die gemeinsamen Bibliotheken direkt. Oberfläche neu in MAUI. Wiedergabe über den Android-eigenen Player, der schnelleres Abspielen mit gleicher Tonhöhe schon mitbringt.
2. **Uno Platform.** Führt WinUI-Oberflächen auf Android aus, könnte also mehr von der bestehenden Oberfläche übernehmen. Die App hängt aber an vielen Windows-eigenen Teilen, und das Desktop-Layout passt nicht aufs Handy; der Gewinn ist kleiner, als er klingt.
3. **Eigene App in Kotlin.** Beste Android-Anbindung, aber nichts ist wiederverwendbar: Statistik, Trophäen und Abgleich müssten ein zweites Mal geschrieben und dauerhaft doppelt gepflegt werden.

Vor der Entscheidung prüfe ich den aktuellen Stand der Werkzeuge (unterstützte .NET-Version, Android-Player-Anbindung); meine Empfehlung beruht auf dem Stand, den ich kenne.

### Was auf Android anders gelöst werden muss

- **Wiedergabe im Hintergrund:** läuft weiter bei ausgeschaltetem Bildschirm, mit Steuerung im Sperrbildschirm, über Kopfhörertasten und Bluetooth.
- **Dateien:** Android gibt Apps nur Zugriff auf Ordner, die der Nutzer freigibt.
- **Abgleich:** Auf dem Handy gibt es keinen OneDrive-Ordner im Dateisystem. Zwei Wege:
  - Über die Android-Dateiauswahl auf den OneDrive-Ordner zugreifen (die OneDrive-App stellt ihn dort bereit). Keine eigene Anmeldung nötig. Wie zuverlässig das Schreiben darüber ist, muss ein kurzer Versuch zeigen.
  - Direkt über die Microsoft-Schnittstelle mit Anmeldung in der App. Zuverlässiger, braucht aber eine einmalige kostenlose Registrierung der App bei Microsoft durch dich.
- **Read Along:** Das Aufbereiten eines Buchs (Spracherkennung) ist auf dem PC schon rechenintensiv. Vorschlag: auf dem PC aufbereiten und die fertigen Daten über den Sync-Ordner aufs Handy bringen.

### Stufen

1. **Gerüst und Wiedergabe:** Projekt anlegen, gemeinsame Bibliotheken einbinden, ein Buch aus einem Ordner importieren und abspielen, auch im Hintergrund. Geschwindigkeit, Kapitel, Sprungtasten, Schlaf-Timer.
2. **Bibliothek:** Übersicht mit Covern, Suche, Fortschritt je Buch.
3. **Abgleich:** Position und Statistik mit dem OneDrive-Ordner. Ab hier ist das eigentliche Ziel erreicht.
4. **Statistik und Trophäen:** die beiden Seiten fürs Handy.
5. **Später:** Read Along, Lesezeichen, Hörbücher direkt aus OneDrive laden.

### Voraussetzungen bei dir

- Android-Entwicklungswerkzeuge auf dem PC (kostenlos, mehrere GB).
- Dein Handy mit eingeschaltetem USB-Debugging zum Testen. Die App wird direkt aufs Handy gespielt; ein Eintrag im Play Store ist dafür nicht nötig.
- Ich kann auf dem Handy nichts sehen oder bedienen. Getestet wird in einem Android-Emulator auf dem PC, soweit möglich, und von dir auf dem echten Gerät.

### Verhältnis zum ursprünglichen Projekt

Die Android-App ist deutlich mehr als ein Beitrag zu rstewa/Audibly. Vorschlag: Sie lebt als eigenes Projekt in deinem Fork. Der Abgleich aus Projekt 1 kann dem Hauptprojekt als PR angeboten werden.

---

## Entscheidungen, die bei dir liegen

1. **Hörbuchdateien auf dem Handy:** kopierst du sie aufs Handy (Vorschlag für den Anfang), oder sollen sie aus OneDrive geladen werden?
2. **Technik für Android:** .NET MAUI wie empfohlen?
3. **Abgleich auf dem Handy:** erst der Weg ohne Anmeldung, und nur wenn er sich als unzuverlässig erweist die Microsoft-Anmeldung?
4. **Rückwärtssprung:** nachfragen, wenn das andere Gerät weiter vorn ist (Vorschlag), oder immer automatisch übernehmen?
5. **Umfang der ersten Android-Version:** Stufen 1 bis 3, also Abspielen, Bibliothek und Abgleich?
