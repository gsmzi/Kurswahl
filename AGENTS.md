# Dauerhafte Entwicklungsanweisungen

- Zuerst `docs/GSM_Kurswahl_Anforderungen_Codex.md` vollständig lesen. M1 bis M5 in dieser Reihenfolge bearbeiten; M1 abgenommen, M2 technisch vorhanden mit dokumentierten fachlichen Freigabegrenzen; aktueller Umfang: M5 funktional nach ausdrücklicher Nutzerfreigabe vom 28.10.2026; konkreter ASV-Adapter erst nach Musterdatei.
- C#/.NET 10 LTS, Avalonia. Domain ohne UI/Dateisystem; Rules nur auf Domain; Storage und ImportExport nur auf Domain; Application auf Domain/Rules. Beide Desktopprogramme nutzen diese gemeinsamen Komponenten. Keine Rechtsregeln in UI oder Importadaptern.
- Ausschließlich synthetische Personen und Accounts in Code, Tests, Samples und Protokollen. Keine Signierschlüssel, Echtdaten oder Diagnosen einchecken. Die Schulnetzpfade des GSM (Austeilordner, Programmordner) dürfen als Schulvorgaben eingecheckt werden (Nutzerentscheidung 02.11.2026: Ablauf soll auf das GSM passen); Personen- und Kontonamen bleiben synthetisch.
- Unbekannt ist nicht falsch und nicht erfüllt: explizit `needs_review`. M1 darf niemals verbindlich grün oder eingereicht melden. Vorbildung aus Wahl-JSON ist keine Vertrauensquelle. Profilprüfung folgt in M2, Profilerzeugung/-verteilung (ohne Zertifikats-/SID-Pflicht gemäß Nutzerentscheidung 27.10.2026) in M3.
- Jede Regel mit stabiler ID, Quelle, Version, Geltungsbereich und Schweregrad dokumentieren und testen. Keine unbestätigten rechtlichen Sonderregeln implementieren. Offene Punkte in `docs/OFFENE_PUNKTE.md` ergänzen.
- Fach, Kursart, Variante, Halbjahre, Angebot und spätere Unterrichtsgruppe getrennt modellieren. Planungsszenarien dürfen aktive Wahlen nicht verändern.
- JSON-Schemaänderungen explizit versionieren. Ab 02.11.2026 gilt der Kompatibilitätsvertrag in `docs/DATEIFORMAT.md`; für die Lehrerseite (Verwaltungsdatenbank, Stammdaten, `workspace.json`, `.gsm-transfer`) beginnt er mit Format 1.0 (Nutzerentscheidung 20.11.2026, Entwicklungsstände davor werden abgewiesen): alle veröffentlichten Versionen bleiben lesbar (Umwandlung nur im Speicher), geschrieben wird nur die aktuelle Version, neuere Versionen werden abgelehnt; jede Strukturänderung braucht neue Version, Umwandlung, Schemadatei und eingefrorene Testdateien (`CompatibilityTests`). Niemals stillschweigend migrieren oder defekte Dateien überschreiben. Austausch über temporäre Datei im Zielverzeichnis; Konkurrenzänderungen erkennen.
- Lehrerrevisionen sind unabhängig von Schülerdateien, append-only mit Herkunft und vollständigem Snapshot. Wiederherstellung erzeugt neue Revision. Import benötigt Vorschau und bestätigte Übernahme; ASV-Adapter erst anhand anonymisierter Musterdatei.
- Nach jedem abgeschlossenen Entwicklungsschritt verfügbare Tests ausführen und tatsächliche Ergebnisse inklusive Fehlern in `docs/TESTPROTOKOLL.md` dokumentieren. Abschließend Restore, Release-Build, gesamte Testsuite und verfügbaren UI-Smoke-Test ausführen. Keine erfolgreichen Tests behaupten, die nicht liefen.
- Programmversion: Haupt-/Nebenversion in `version.txt` nur bewusst ändern; die dritte Stelle zählt Commits automatisch (`build/Version.targets`). Programmpakete über `scripts/Publish-Release.ps1`, Aktualisierung über `Aktualisieren.cmd` (`docs/BETRIEB.md`).
- Pakete explizit pinnen, Lockdateien behalten, Lizenz-/Sicherheitsstand in `docs/ABHAENGIGKEITEN.md` pflegen. Keine automatischen Onlinezugriffe der Desktopanwendungen.
- Änderungen im bestehenden Arbeitsverzeichnis erhalten. Keine ungefragten Deployments; M5 funktional freigegeben (Nutzerentscheidung 28.10.2026); Schulnetz-Pilot und Produktivfreigabe nicht fingieren. M3 umfasst die getrennte AdministrationStorage-Komponente (nur Domain-Abhängigkeit), die nicht vom Schülerprogramm referenziert werden darf.
- Änderungen an Regeln (`GsmKurswahl.Rules`), Schülerablauf/Darstellung (`StudentSession`, `MainWindow`) oder dem Demo-Fachangebot (`samples/fachangebot-demo.json`) immer auch in der Browser-Testversion (`web/Kurswahltest/`) nachziehen, über `./scripts/Publish-Kurswahltest.ps1` aktualisieren und mit `KurswahltestWebParityTests` gegen C# verifizieren.
- Nach jeder wesentlichen, geprüften Änderung (Code, Regeln, UI, Dokumentation) einen sauberen Git-Commit erstellen und nach `origin/main` pushen (Nutzerwunsch vom 19.11.2026); ZIP-Pakete, Sicherungen, Laufzeitdaten, Schlüssel und Echtdaten bleiben dabei stets außerhalb von Git.

## Testpakete

Wenn der Nutzer ein „Testpaket“, „Testprojekt“ oder eine neue portable Windows-„Testversion“
anfordert, die Testversion beider Desktopprogramme aus dem aktuellen Arbeitsstand bauen
und versioniert im folgenden Basisordner bereitstellen, sofern kein anderes Ziel genannt wird:

`C:\Users\m-zim\116522 Gymnasium bei St. Michael\ESL - General\Schulorganisation\Softwareentwicklung\Kurswahl-Testversion`

- Für jede neue Bereitstellung im Basisordner einen neuen Unterordner mit Datum und
  Uhrzeit (`YYYY-MM-DD_HH-MM`, z. B. `2026-11-17_14-20`) anlegen. Bei belegtem Namen
  einen eindeutigen späteren Zeitstempel verwenden; keinen vorhandenen Stand überschreiben.
  Darin liegt das entpackte, startfähige Paket mit den vorhandenen `.cmd`-Startern und
  `LIESMICH.txt` direkt auf oberster Ebene sowie `_system` daneben (kein zusätzlicher Paket-Unterordner).
- Den vorhandenen Bauprozess `scripts/Publish-Testpaket.ps1` (bzw. `Testpaket-bauen.cmd`)
  verwenden: beide Programme für Windows x64 selbstenthaltend. Ausschließlich die
  synthetischen Stände aus `DemoStages` verwenden; sie werden beim Teststart frisch
  lokal erzeugt. Keine bestehenden Verwaltungsdatenbanken, Schülerdateien oder echten
  Schulvorgaben in das Paket übernehmen.
- Vor der Bereitstellung `scripts/Test.ps1 -LockedMode` für Restore, Release-Build,
  gesamte Testsuite einschließlich Headless-UI-Smoke und Schemaprüfung ausführen.
  Das entpackte Paket zusätzlich mit `scripts/Test-Testpaket.ps1 -PackageDirectory <Paketordner>`
  über seine eigenen selbstenthaltenden Programme prüfen; passende Tests bei
  Programmänderungen ebenfalls ausführen. Ergebnisse und Fehler tatsächlich in
  `docs/TESTPROTOKOLL.md` protokollieren. Headless-/Vorbereitungstests ersetzen keinen Schulnetztest.
- Das Paket vollständig mit Langpfad-Unterstützung in den neuen Zeitstempel-Ordner
  kopieren und dort Dateibestand und sämtliche SHA-256-Prüfsummen gegen den Bauordner
  prüfen. Erst nach erfolgreicher Prüfung gilt die Bereitstellung als abgeschlossen.
- Der unmittelbar vorherige Zeitstempel-Ordner bleibt als funktionsfähige Vorversion
  stehen, sodass zwei Versionen verfügbar bleiben. Ältere Zeitstempel-Ordner erst nach
  erfolgreicher Prüfung des neuen Ordners entfernen. Vorher dort etwaige ungesicherte
  Laufzeitdaten unter `artifacts/testpaket/sicherung/` mit eindeutigem Zeitstempel
  sichern und die Sicherung prüfen. Löschziele auf Zugehörigkeit zum Basisordner
  prüfen; unbekannte Ordner und eine etwaige ältere, direkt im Basisordner liegende
  Bereitstellung nicht automatisch löschen.
- Die vom Starter getrennt unter `%LOCALAPPDATA%\GSM-Kurswahl-Testpaket` erzeugten
  Testläufe bleiben erhalten; die Versionsbereinigung löscht keine lokalen Arbeitsdaten.
- ZIP-Pakete, Sicherungen und Laufzeitdaten bleiben außerhalb von Git. Für zugehörige wesentliche Änderungen im Repository (z. B. Code- oder Protokolländerungen) gilt die allgemeine Commit-und-Push-Regel oben.

Diese Zielvorgabe ist ein ausdrücklicher Nutzerwunsch vom 17.11.2026 und gilt auch
in späteren Chats zu diesem Projekt. Ein angefordertes Testpaket umfasst die oben
beschriebene Bereitstellung; dafür ist keine erneute inhaltliche Bestätigung nötig.
Technische Sandbox-Freigaben bei Bedarf über das vorgesehene Tool anfordern.
Die Browser-Testversion `web/Kurswahltest/` und eine Produktivverteilung werden dadurch
nicht automatisch veröffentlicht.

## Synchronisation zwischen Rechnern (Git)

An diesem Repository wird auf mehreren Rechnern gearbeitet. Damit die Stände nicht auseinanderlaufen, gilt für jeden KI-Agenten, egal von welchem Hersteller oder Modell:

- **Vor Arbeitsbeginn** den aktuellen Stand holen: `git pull --ff-only`. Liegen lokale, uncommittete Änderungen vor, die nicht von dir stammen, nichts überschreiben, sondern den Nutzer fragen.
- **Nach jeder abgeschlossenen Änderung** committen und pushen (`git add -A`, `git commit`, `git push`), direkt auf dem aktuellen Branch. Commit-Nachrichten auf Deutsch und aussagekräftig.
- Lehnt GitHub den Push ab, weil dort ein neuerer Stand liegt: `git pull --rebase`, danach erneut pushen. Bei Konflikten nichts erzwingen (kein `--force`, kein `reset --hard`), sondern den Nutzer informieren.
- Vor jedem Commit prüfen, dass keine echten Personendaten, Schlüssel, Tokens oder Passwörter und nichts aus per `.gitignore` ausgeschlossenen Ordnern enthalten ist.
- Im Schulnetz geht der Zugriff auf GitHub nur über den Proxy: `HTTPS_PROXY=http://145.254.22.10:8000` setzen, wenn `github.com` nicht erreichbar ist.
- In Claude Code erledigt das ein Hook (`~/.claude/hooks/git-sync.ps1`) automatisch; für alle anderen Werkzeuge gilt diese Regel trotzdem.
