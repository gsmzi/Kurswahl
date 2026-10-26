# Testprotokoll M1

Umgebung: Windows x64, .NET SDK 10.0.400; ausschließlich synthetische Daten. TRX-Ergebnisse unter `artifacts/test-results` (nicht einchecken).

## Schritt 1 – Solution und Domäne

- Erster Versuch `dotnet test tests/GsmKurswahl.Domain.Tests`: Restore durch fehlenden Zugriff auf Benutzer-NuGet.Config blockiert; keine Tests ausgeführt. Abhilfe: projektlokale NuGet.Config und expliziter Restore.
- Auch explizite Konfiguration benötigte in dieser Sandbox erhöhten Lesezugriff auf die NuGet-Umgebung; autorisierter Restore erfolgreich. Anschließend **11/11 Domänentests bestanden**.

## Schritt 2 – Demoangebot und Regelkern

- Domäne **11/11**, Rules **14/14** bestanden (25 gesamt).
- Geprüft: kein M1-Grünstatus, explizite unbekannte Voraussetzungen, kein Vertrauen in ungeprüfte Profilwerte, INF-Zugangswege, gesperrte geplante Angebote, Versionsabweichungen, unbekannte Angebote, Doppelfachwahl und SIA-Halbjahreszählung. Rechtsquelle jeweils Pflichtenheft; keine vollständige AGVO-Akzeptanz behauptet.

## Schritt 3 – JSON und Importgrenze

- Domäne **11/11**, Rules **14/14**, Storage **19/19**, ImportExport **1/1** bestanden (45 gesamt).
- JSON-Roundtrip, Sicherung der Vorgängerversion, simulierter Abbruch vor Austausch, Cancellation, Dateisperre, konkurrierender veralteter Schreibstand, defektes JSON, unbekanntes Schema, Pflicht-/Fremdfelder, Identitätswechsel und Pfadtraversierung geprüft.
- Importtest betrifft nur den Vorschauvertrag. Implementierte Importadapter folgen in M3/M5.

## Schritt 4 – Schüler- und Lehreroberfläche

- Application **7/7** bestanden: Auswahl, Speichern/Laden, unverändertes Speichern, Doppelfachschutz, Lade-/Schreibfehler, Verwerfbestätigung, Bili-Sperre und Demo-Grenze.
- Erster Gesamtbuild scheiterte an Avalonia-Build-Telemetriezugriff außerhalb der Sandbox. Mit dokumentiertem Opt-out erneut gebaut: **0 Warnungen, 0 Fehler**.
- Gesamtsuite anschließend **54/54** bestanden, davon **2 Avalonia-Headless-UI-Tests**: tatsächliche Checkbox-Auswahl, Speichern per Button über die JSON-Dateischicht, erneutes Laden und sichtbarer Vorläufigkeitsstatus; deaktiviertes eingeschränktes Angebot mit Begründung.
- Beide Beispiel-JSONs mittels PowerShell `Test-Json` gegen die JSON-Schemata 2020-12 geprüft: bestanden.

## Schritt 5 – Abschlussprüfung

- `scripts/Test.ps1 -LockedMode`: Restore erfolgreich, Release-Build **0 Warnungen / 0 Fehler**.
- **55/55 Tests bestanden**, keine übersprungen: Domain 11, Rules 14, Storage 20, ImportExport 1, Application 7, StudentApp 2. Der zusätzliche Storage-Test liest die tatsächlich ausgelieferten synthetischen JSON-Beispiele und prüft ihre Konsistenz.
- Beide JSON-Schemaprüfungen erneut bestanden.
- Paket-Audit `dotnet list ... package --vulnerable --include-transitive --no-restore` erfolgreich: alle 13 Projekte ohne gemeldete bekannte Paket-Schwachstellen gemäß NuGet.org zum Prüfzeitpunkt. Wartungsstatus von xUnit v2 separat dokumentiert.
- Selbstenthaltender `win-x64`-Publish: Online-Restore ohne Fortschritt abgebrochen; Wiederholung ausschließlich mit lokalem Paketcache meldet NU1102 für .NET-10.0.11-Runtimepakete. Kein Standalone-Erfolg behauptet. Frameworkabhängiger Publish wird als M1-Alternative erstellt; Standalone-/Schulnetz-Prüfung bleibt offen.
- Frameworkabhängiger Release-Publish für **beide Programme erfolgreich**: `artifacts/publish/student-framework-dependent` und `artifacts/publish/teacher-framework-dependent`. .NET-10-Laufzeit auf dem Zielrechner erforderlich. Schüler-Konfiguration und Fachangebot werden mitkopiert.
- Erneuter Solution-Restore mit `--locked-mode` nach den Publish-Versuchen erfolgreich. Kein nativer Windows-Sichttest und kein Test auf einem Rechner ohne SDK durchgeführt; UI-Funktion automatisiert headless geprüft.

## Schritt 6 – Korrekturen nach Review (25.10.2026)

Umgebung: Windows 11 x64, .NET SDK 10.0.400, Bash-Aufrufe der Einzelbefehle aus dem README. `scripts/Test.ps1` konnte nicht laufen, weil auf diesem Rechner kein PowerShell 7 (`pwsh`, für `Test-Json`) installiert ist; die JSON-Schemaprüfung wurde daher in diesem Schritt **nicht** ausgeführt (die Schemata wurden nicht geändert).

- Ausgangsstand vor den Änderungen: Restore `--locked-mode`, Release-Build 0 Warnungen / 0 Fehler, **55/55 Tests bestanden**.
- Änderungen: GSM-Angebotsgrenzen aus `ModelGuard` als Regeln mit ID nach `GsmKurswahl.Rules/CatalogRules` verlagert (sperren das betroffene Angebot statt den ganzen Katalog); Regelkatalog in `RECHTSQUELLEN.md`; `JsonChoiceStore` meldet ein fehlendes Home/Laufwerk als „Speicherort nicht erreichbar“ und legt das Home nie an; `StudentSession` behält bei verschwundener Datei den Entwurf und speichert danach nur anlegend statt dauerhaft gesperrt.
- Normaler Restore aktualisierte `tests/GsmKurswahl.Storage.Tests/packages.lock.json` (neuer Projektverweis auf Rules, keine neuen Pakete). Danach Restore `--locked-mode` erfolgreich.
- Erster Build: **1 Fehler** CS0407 (Methodengruppe `Check` in `SelectMany` mehrdeutig). Der anschließende `dotnet test` lief nur gegen veraltete Binärdateien und wird nicht gewertet. Korrigiert durch Lambda.
- Zweiter Release-Build: **0 Warnungen, 0 Fehler**.
- **78/78 Tests bestanden**, keine übersprungen: Domain 8, Rules 33, Storage 24, ImportExport 1, Application 10, StudentApp 2 (Avalonia headless).
  - Domain: Test „verbotene LF im ModelGuard“ (5 Fälle) entfernt, da Regel verlagert; neu: Strukturprüfung lässt Schulregeln an Rules, unbekannte Fachkennung wird abgewiesen.
  - Rules: verbotene LF mit Regel-ID (5), je ein Negativfall für alle `CATALOG-*`-Regeln inkl. Sperre und `invalid` in `Validate` (9), gültiges Angebot ohne Verstoß, `PROFILE-TRUST`/`PREREQUISITE-UNKNOWN`, `PREREQUISITE-NO`, gestrichenes Angebot → `OFFER-SELECTION`, jede Regel-ID in `RECHTSQUELLEN.md` dokumentiert.
  - Storage: fehlender Speicherort beim Laden und Speichern (Home wird nicht angelegt), fehlender Unterordner unter vorhandenem Home = neuer Entwurf, Ziel außerhalb des Speicherorts abgewiesen; Beispielkatalog ohne `CATALOG-*`-Verstoß und Beispielwahl `needs_review`.
  - Application: verschwundene Datei erhält Entwurf, nächstes Speichern ohne Ersetzungs-Token; Laden mit Verwerfen ohne Datei sperrt nicht; Katalogverstoß sperrt Angebot mit Regel-ID.
- Nicht ausgeführt: JSON-Schemaprüfung (kein `pwsh`), manueller Sichttest der Schüler-App, Publish, Paket-Audit.
- Nachtrag nach Installation von PowerShell 7.6.8: `pwsh -NoProfile -File scripts/Test.ps1 -LockedMode` vom Anwender ausgeführt, Ausgabe gesichtet. Restore `--locked-mode` und Release-Build erfolgreich, **78/78 Tests bestanden** (Testhost .NET 10.0.14), beide Schemaprüfungen bestanden (`Schema OK` für `schueler-demo.json` und `fachangebot-demo.json`). Weiterhin nicht ausgeführt: manueller Sichttest, Publish, Paket-Audit.

## Grenzen der Prüfung

Kein Schulnetz/SMB-Ausfalltest, keine echten Profile, kein AGVO-Volltest, kein realer CSV-/XLSX-/ASV-Import. Headless-Tests ersetzen keine Sicht-/Tastatur-/DPI-Prüfung auf dem Schulgerät. GitHub-CI wurde vorbereitet, aber nicht remote ausgeführt. macOS wurde hier nicht ausgeführt.

## Schritt 7 – Wahlassistent, erster Zwischenlauf (25.10.2026)

- `dotnet test GSM-Kurswahl.sln -c Release --no-restore`: Domain 8/8, Rules 32/33, Storage 24/24, ImportExport 1/1, Application 10/10 bestanden. Regel-Dokumentationstest scheiterte an den drei noch nicht dokumentierten PLAN-IDs; Dokumentation anschließend ergänzt.
- StudentApp konnte in diesem Lauf wegen MSB4018 / fehlendem Schreibzugriff der AvaloniaStatsTask auf deren Log außerhalb des Arbeitsverzeichnisses nicht gebaut werden. UI-Tests liefen deshalb nicht. Für weitere Läufe wird wie im Testskript AVALONIA_TELEMETRY_OPTOUT=1 gesetzt.

- Zweiter Zwischenlauf mit deaktivierter Build-Telemetrie: **86/86 Tests bestanden**, keine übersprungen (Domain 8, Rules 33, Storage 24, ImportExport 1, Application 18, StudentApp 2). Der UI-Test durchläuft alle vier Schritte und prüft D/M-Automatik, Pflichtkurse, zusätzliche Wahl und JSON-Roundtrip. Anschließend explizite Schema-v1-Fehlermeldung, zwei Storage-Regressionstests und Regelstand m1-2 ergänzt.

- Abschließendes Standardskript zunächst beim Restore gescheitert: Zugriff auf benutzerweite NuGet.Config verweigert. Mit prozesslokal auf artifacts/build-appdata gesetztem APPDATA danach NU1301 (NuGet-Netzwerkzugriff gesperrt); dieser Lauf wurde beendet. Keine Tests dieses Skriptlaufs als Erfolg gewertet.
- Restore anschließend erfolgreich für alle 13 Projekte: vorhandener Paketcache über NUGET_PACKAGES, `--locked-mode --ignore-failed-sources -p:NuGetAudit=false`. Keine Lockdatei-/Paketänderung, kein neuer Online-Sicherheits-Audit.

- Release-Build: 0 Warnungen, 0 Fehler. Erster Abschluss-Testlauf: 85/88 bestanden, drei Storage-Tests fehlgeschlagen. Die neue vorgelagerte Schemaprüfung liefert bei v1 ArgumentException; beschädigtes JSON liefert die JsonException-Unterklasse JsonReaderException. Testannahmen entsprechend präzisiert; Test für fehlende Pflichtfelder auf aktuelle Schema-v2-Version aktualisiert. Dateischutz blieb wirksam. Schemaprüfung wurde wegen des Testabbruchs in diesem Lauf nicht ausgeführt.

### Abschluss (26.10.2026)

- Release-Build erfolgreich: **0 Warnungen, 0 Fehler**.
- Gesamte Testsuite: **88/88 bestanden**, keine übersprungen: Domain 8, Rules 33, Storage 26, ImportExport 1, Application 18, StudentApp 2.
- Verfügbarer UI-Smoke-Test (Avalonia headless) bestanden: alle vier Schritte, automatische mündliche D/M-Planung, Pflichtkurse, ergänzende Wahl, Speichern/Laden mit echtem JSON-Store; eingeschränktes LF deaktiviert.
- Beide JSON-Schemaprüfungen erfolgreich (Schülerwahl v2, Katalog v1).
- `git diff --check` ohne Fehler. Keine neuen Pakete, keine Änderungen an Lockdateien.
- Kein nativer Sicht-/DPI-Test, kein Publish/Deployment, kein Online-Sicherheits-Audit. Fachliche Vollprüfung und verbindliche Freigabe bleiben M2.
