# Betrieb: Programmpaket, Versionen und Aktualisierung

Stand 20.11.2026. Beschreibt das mit `scripts/Publish-Release.ps1` erzeugte Programmpaket. Das Testpaket (`Publish-Testpaket.ps1`) bleibt davon getrennt. Kein Deployment und keine Produktivfreigabe durch dieses Dokument.

## Versionsnummer

- Haupt- und Nebenversion stehen in `version.txt` im Repository (aktuell `1.0`) und werden nur bewusst geändert, z. B. auf `1.1` bei größeren Neuerungen oder `2.0` bei einem neuen Datenformat.
- Die dritte Stelle zählt automatisch die Commits seit der letzten Änderung von `version.txt` (`build/Version.targets`): 1.0.0, 1.0.1, 1.0.2 … Nach einer Änderung von `version.txt` beginnt sie wieder bei 0.
- Die vollständige Kennung enthält zusätzlich den Commit, z. B. `1.0.4+8b762c1c`. `.geaendert` am Ende bedeutet: gebaut mit nicht committeten Änderungen (nur für Entwicklung).
- Anzeige: Titelleiste beider Programme (`GSM-Kurswahl 1.0.4 | …`), im Lehrerprogramm zusätzlich unter **Einstellungen** samt Programmordner; im Paket in `version.json`.

## Programmpaket

`./scripts/Publish-Release.ps1` (mit `-SelfContained` auch das Lehrerprogramm ohne installierte .NET-Laufzeit) baut aus einem sauber committeten Stand `artifacts/releases/GSM-Kurswahl-<Version>.zip`:

```
GSM-Kurswahl-<Version>\
├─ TeacherApp\            Lehrerprogramm, lehrerprogramm.sperre, schulvorgaben.json
├─ StudentApp\            Schülerprogramm (selbstenthaltend), Quelle für die Verteilung in die Schülerordner
├─ docs\, README.md       Dokumentation
├─ _Update\Update-Kurswahl.ps1
├─ Aktualisieren.cmd      spielt ein neues Paket ein
├─ LIESMICH.txt           Kurzanleitung
├─ version.json           Version, Commit, Bauzeit
└─ SHA256.json            Prüfsummen aller Dateien
```

Es enthält keine Verwaltungsdatenbank, keinen Arbeitsstand und keine Schülerdaten.

## Erstinstallation

ZIP entpacken, den Ordner `GSM-Kurswahl-<Version>` z. B. in `GSM-Kurswahl` umbenennen und an den Zielort legen (Stammordner). Das Lehrerprogramm startet über `TeacherApp\GsmKurswahl.TeacherApp.exe`. Der Stammordner darf auf einem Netzlaufwerk liegen; die Startsperre lässt nur ein Lehrerprogramm gleichzeitig zu.

## Aktualisierung

1. Alle Lehrerprogramme schließen (auch an anderen Rechnern).
2. Neues `GSM-Kurswahl-<Version>.zip` unverändert in den Ordner `Updates` im Stammordner legen (bei der ersten Aktualisierung anlegen).
3. `Aktualisieren.cmd` doppelklicken.

Das Skript wählt das Paket mit der höchsten Version, entpackt es in einen Prüfordner und prüft alle Prüfsummen. Danach verschiebt es `TeacherApp`, `StudentApp`, `docs` und die Paketdateien nach `Sicherung\<alte Version>_<Zeit>`, setzt die neuen ein und übernimmt die bisherige `TeacherApp\schulvorgaben.json`. Anschließend prüft es den eingesetzten Stand erneut. Bei laufendem Lehrerprogramm, manipuliertem Paket oder einem Fehler bleibt bzw. wird der vorherige Stand wiederhergestellt. Protokoll: `Updates\update-protokoll.txt`; eingespielte Pakete liegen danach in `Updates\eingespielt`; die letzten drei Sicherungen bleiben erhalten.

Rückkehr zu einer älteren Version: deren ZIP in `Updates` legen und `Aktualisieren.cmd -ErlaubeAeltereVersion` aufrufen. Eine ältere Version öffnet keinen Bestand, den eine neuere Version in einem neueren Datenformat gespeichert hat (Kompatibilitätsvertrag: neuere Formate werden abgewiesen).

Das Skript läuft mit Windows PowerShell 5.1 und benötigt keine Installation; es wird über `-ExecutionPolicy Bypass` nur für diesen Aufruf gestartet. Ob AppLocker Skripte im Stammordner erlaubt, ist im Schulnetz-Pilot zu prüfen.

## Was bei einer Aktualisierung mit den Daten geschieht

- **Verwaltungsbestand** (Verwaltungsnetz: `%LOCALAPPDATA%\GSM-Kurswahl\Lehrerverwaltung` oder der gewählte Ordner; pädagogisches Netz: `Arbeitsstand` im Paketordner auf U:) liegt außerhalb von `TeacherApp` und bleibt unverändert. Neue Programmversionen lesen alle Datenformate ab 1.0 (siehe [DATEIFORMAT.md](DATEIFORMAT.md)).
- **Schülerordner:** Bereits verteilte Schülerprogramme werden bei der nächsten Verteilung ersetzt, sofern sie unverändert sind; Wahlen bleiben erhalten.
- **Pädagogisches Netz:** Das Paket für das pädagogische Netz enthält immer das gerade laufende Lehrerprogramm. Nach einer Aktualisierung im Verwaltungsnetz einfach ein neues Paket erzeugen.

## Empfohlener Ablauf für Programmverbesserungen

1. Änderung umsetzen, `./scripts/Test.ps1 -LockedMode` ausführen (Build, alle Tests, Schemaprüfung, Update-Test), committen und pushen.
2. `./scripts/Publish-Release.ps1 -SelfContained` ausführen.
3. ZIP in den Ordner `Updates` des Stammordners kopieren und `Aktualisieren.cmd` starten.

Während einer laufenden Wahlphase nur Fehlerkorrekturen einspielen. Regeländerungen können Schülerwahlen anders bewerten und sollten nach dem Einsammeln folgen.
