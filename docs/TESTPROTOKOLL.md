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

## Schritt 8 – Aufgabenmodell und geführte Oberfläche (26.10.2026)

- Aufgabenmodell `ChoiceWorkflow.Tasks` (Regeln `PLAN-AREA-ART`, `PLAN-AREA-RELIGION`, `PLAN-AREA-SCIENCE`, `PLAN-MIN-COURSES`), `UsefulOralCandidates`, `StudentSession.Plan`/`ChooseTaskOption`/`GoToStep`; Schülerprogramm auf fünf Schritte mit dauerhaftem Planraster umgebaut.
- Zwischenläufe Application-Tests: erster Lauf 20/24, vier Fehler durch Testannahmen (Vergleich Array gegen `ImmutableArray`, fehlendes PH-Angebot bzw. PH-Fach im Testkatalog). Tests präzisiert, danach 24/24.
- Erster UI-Testlauf 1/2: Test erwartete nach der Antwort auf „Kunst oder Musik“ weiterhin beide Optionen; die Frage klappt aber wie vorgesehen zu. Test nutzt jetzt „Ändern“; danach 2/2.
- Sichtprüfung über headless gerenderte Skia-Screenshots (temporäres Hilfsprogramm außerhalb des Repositorys, Demo-Katalog, alle fünf Schritte). Dabei deaktivierte, grau wirkende Buttons für nicht anklickbare Planzeilen und künftige Schritte gefunden und durch einfache Beschriftungen ersetzt. Kein nativer Fenster-, DPI- oder Screenreader-Test.

### Abschluss

- Restore `--locked-mode --ignore-failed-sources -p:NuGetAudit=false` aus dem lokalen Paketcache: alle Projekte wiederhergestellt, keine Lockdatei-Änderung.
- Release-Build: **0 Warnungen, 0 Fehler**.
- Gesamte Testsuite: **94/94 bestanden**, keine übersprungen: Domain 8, Rules 33, Storage 26, ImportExport 1, Application 24, StudentApp 2.
- UI-Smoke-Test (Avalonia headless) bestanden: LF-Wahl, gesetzte D/M-Prüfung als Information, Pflichtbereiche als Einzelfragen mit Radio-Ersetzung und „Ändern“, Pflicht-Halbjahr gesperrt, zusätzliches Halbjahr und Kurs ergänzt, Speichern/Laden mit echtem JSON-Store; eingeschränktes LF nur in eingeklappter Liste mit Begründung.
- Schemaprüfung: `Schema OK` für `schueler-demo.json` und `fachangebot-demo.json`. `git diff --check` ohne Fehler.
- Nicht ausgeführt: Publish, Online-Sicherheits-Audit, Test auf Schulgeräten.

## Schritt 9 – Anzeige der persönlichen Voraussetzungen (26.10.2026)

- `StudentSession.Prerequisites`/`ProfileStatusText` und schreibgeschützter Bereich „Deine Voraussetzungen“ im Schülerprogramm (Informatik, Italienisch, Bili, Religion/Ethik Klasse 10, Sportbefreiung, jeweils mit betroffenen Angeboten).
- Erster Lauf: Build-Fehler CS0246 im UI-Test (Typ `InputElement` nicht importiert); die dabei ausgegebenen Testergebnisse stammten noch vom alten Build und wurden nicht gewertet. Zweiter Lauf: 94/95, UI-Test erwartete „D LF bilingual“, der Testkatalog heißt das Fach aber „Deutsch“. Testerwartung korrigiert.
- Abschlusslauf: Release-Build **0 Warnungen, 0 Fehler**; **95/95 bestanden** (Domain 8, Rules 33, Storage 26, ImportExport 1, Application 25, StudentApp 2). Headless-Screenshot von Schritt 1 mit Demoangebot gesichtet. Kein Restore-Neulauf (Pakete unverändert), keine Schemaänderung.

## Schritt 10 – Korrekturen nach erneuter Prüfung (26.10.2026)

- Änderungen nach Review der Commits `7c1ea99`, `8a31233` und `2cfdf85`:
  - `CatalogRules`: `CATALOG-PSY` verlangt exakt HJ 1–2; `CATALOG-GEO-BF` erlaubt Geographie außerhalb LF nur als BF mit exakt HJ 3–4; `CATALOG-I-PROFILE` verlangt BF/LF über HJ 1–4 zusätzlich zur Profilvoraussetzung.
  - `ChoiceWorkflow`: `ProfileContext` wird in `OralCandidates`, `LfError`, `OralError` und `UsefulOralCandidates` durchgereicht; `Tasks` (`Area`) übernimmt höchstens die tatsächlich für die Aufgabe benötigte Anzahl (`choose`) in `ChosenOfferIds` (priorisiert Pflicht-/Prüfungsfächer sowie `BIO/CH/PH` vor `INF`), sodass zusätzliche Kurse aus Schritt 4 nicht als Pflichtbereich gesperrt werden; `PLAN-AREA-SCIENCE` zählt ein gewähltes `INF-BF` bereits als 1 von 2 Teilfächern (`Open` sinkt von 2 auf 1, mindestens eine Naturwissenschaft bleibt Pflicht).
  - `ChoiceValidator`: `OFFER-SELECTION` prüft auch für `CourseType.Seminar` (SIA) die vollständige Belegung aller angebotenen Halbjahre; nichtleere `PlannedOralSubjects` werden gegen `ChoiceWorkflow.OralCandidates` und `ChoiceWorkflow.OralError` geprüft und bei Verstoß als `PLAN-ORAL` (`invalid`) gemeldet.
  - `StudentSession`: Ungeprüfte Profile (`Profile.Status != ProfileStatus.Verified`) geben in `Prerequisites` für alle Felder strikt `"ungeprüft"` aus; `ChooseTaskOption` begrenzt Mehrfachauswahl auf `task.Choose`; `NextStep` markiert unveränderte geladene Entwürfe bei reiner Reihenfolgenormierung von `ForcedOral` nicht als `IsDirty`; `SetSelection` erzwingt bei Seminarkursen alle Halbjahre und entfernt ein Fach aus `PlannedOralSubjects`, sobald es als LF gewählt wird.
  - `StudentSettings` / `App`: `GSM_KURSWAHL_SETTINGS` verlangt einen vollqualifizierten Dateipfad (leere Umgebungsvariable fällt auf Standardpfad zurück); `ResolveChoicePath` weist Pfadsegmente mit führenden/abschließenden Leerzeichen oder abschließenden Punkten (`".. "`, `"Kurswahl."`) sowie Dateinamen ohne Stamm (`".json"`) ab.
  - `MainWindow`: `Edit` stoppt den Autosave-Timer vor `edit()`; Fokuswiederherstellung durchsucht auch `plan`; `TaskCard` nutzt `CheckBox` statt `RadioButton`, wenn eine exklusive Aufgabe bereits ein gesperrtes Pflicht-/Prüfungsfach enthält; Halbjahres-Checkboxen für `CourseType.Seminar` sind in Schritt 4 deaktiviert.
- Test- und Buildläufe:
  - Versuch `pwsh -NoProfile -File scripts/Test.ps1 -LockedMode` in der Standard-Sandbox fehlgeschlagen (`Das System kann auf die Datei nicht zugreifen` beim Start des Windows-AppAlias `pwsh.exe`).
  - Mehrprozess-Builds (`dotnet build GSM-Kurswahl.sln -c Release --no-restore`) in der Standard-Sandbox brachen nach `0 Warnung(en), 0 Fehler` beim IPC-Named-Pipe-Abbau von MSBuild/VBCSCompiler mit `UnauthorizedAccessException` (Exit-Code 1) ab.
  - Einzelprozess-Build in der Standard-Sandbox (`dotnet build GSM-Kurswahl.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false --disable-build-servers`): **0 Warnungen, 0 Fehler** (Exit-Code 0).
  - `dotnet test` in der Standard-Sandbox scheiterte an blockierter Loopback-IPC zwischen `vstest.console` und `testhost` nach 90 Sekunden.
  - Offline-Restore in der Standard-Sandbox (`dotnet restore GSM-Kurswahl.sln --locked-mode -m:1 -p:NuGetAudit=false --disable-build-servers`): alle 13 Projekte erfolgreich wiederhergestellt (ohne `-p:NuGetAudit=false` meldete NuGet wegen gesperrtem Netzwerk `NU1900`).
  - Gesamte Testsuite außerhalb der Sandbox (`dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --logger trx --results-directory artifacts/test-results/final`): **106/106 Tests bestanden**, keine übersprungen (Domain 8, Rules 38, Storage 30, ImportExport 1, Application 27, StudentApp 2 inklusive Avalonia-Headless-UI-Smoke-Test).
  - JSON-Schemaprüfung (`pwsh -NoProfile -File scripts/Test-Schemas.ps1`, nach erfolgter `Test-Json`-Fehlermeldung unter Windows PowerShell 5.1 mit PowerShell 7 ausgeführt): `Schema OK: samples/schueler-demo.json` und `Schema OK: samples/fachangebot-demo.json`.
  - `git diff --check`: ohne Fehler.

## Schritt 11 – Voraussetzungen kompakt in der Fußzeile (26.10.2026)

- Aktuellen Fünf-Schritt-Ablauf, Aufgaben-/Prüfungsplanung und Profilanzeige gesichtet. Änderung auf Darstellung begrenzt: Voraussetzungen aus dem rechten Plan entfernt, kleine Statuszeile in die Fußzeile verschoben, Details standardmäßig eingeklappt. Details enthalten nur Merkmal und Wert sowie Kontaktverweis; keine farbigen Einzel-Badges und wiederholten Wirkungstexte. Die angebotsspezifischen Sperrhinweise bleiben bei der Auswahl erreichbar. Bestehende weitere Arbeitsverzeichnisänderungen erhalten.
- Bestehenden UI-Smoke-Test auf die kompakte, eingeklappte und weiterhin schreibgeschützte Information angepasst; README aktualisiert.
- Restore mit `--locked-mode --ignore-failed-sources -p:NuGetAudit=false` und lokalem Paketcache erfolgreich. Keine neuen Pakete/Lockdateiänderungen, kein Online-Audit.
- Erster Release-Build im Standard-Ausgabeverzeichnis scheiterte an durch eine laufende Schüler-App gesperrten DLLs (MSB3026/MSB3027/MSB3021; 40 Warnungen, 8 Fehler). Keine Tests dieses Laufs gewertet; laufende App nicht beendet.
- Release-Build mit `-m:1 -p:UseSharedCompilation=false --disable-build-servers -p:OutputPath=bin/CompactReview/`: **0 Warnungen, 0 Fehler**.
- Gesamte Testsuite mit demselben OutputPath: **106/106 bestanden**, keine übersprungen (Domain 8, Rules 38, Storage 30, ImportExport 1, Application 27, StudentApp 2 inklusive Avalonia-Headless-UI-Smoke-Test).
- Beide JSON-Schemaprüfungen bestanden; `git diff --check` ohne Fehler. Kein nativer Sicht-/DPI-Test und kein Deployment durchgeführt.

## Schritt 12 – Full-HD-Darstellung (26.10.2026)

- Schülerfenster startet maximiert, Wiederherstellungsgröße 1440 × 900, Inhalt maximal 1760 logische Pixel breit. Kopfzeile passt sich unter 1450 logischen Pixeln an; Dateiaktionen wechseln in eine eigene Zeile. Schrittleiste und Planbeschriftungen umbrechbar, größere HJ-Spalten und Fachbeschriftung im Plan. Lange Dateiangaben einzeilig mit vollständigem Tooltip. Workflow und kompakte Voraussetzungen bleiben erhalten.
- Restore `--locked-mode --ignore-failed-sources -p:NuGetAudit=false` aus lokalem Paketcache erfolgreich; keine neuen Pakete oder Lockdateiänderungen, kein Online-Audit.
- Release-Build mit `-m:1 -p:UseSharedCompilation=false --disable-build-servers -p:OutputPath=bin/FullHd/`: **0 Warnungen, 0 Fehler**. Separater Ausgabeordner vermeidet Konflikte mit geöffneten App-Instanzen.
- Gesamte Testsuite mit gleichem OutputPath: **109/109 bestanden**, keine übersprungen (Domain 8, Rules 38, Storage 30, ImportExport 1, Application 27, StudentApp 5).
- Drei neue Headless-Layoutfälle: logische Arbeitsflächen 1920 × 1000, 1536 × 780 und 1280 × 640 als Annäherung an Full HD mit 100/125/150 % Skalierung inklusive Platz für Taskleiste/Fensterrahmen. Prüfen überlappungsfreie Titel-/Dateiaktionen und sichtbare Navigation, Speichern/Laden, letzten Schritt sowie Voraussetzungen. Bestehender kompletter Workflow-Smoke-Test weiterhin bestanden.
- Beide JSON-Schemaprüfungen und `git diff --check` erfolgreich. Kein nativer DPI-/Monitorwechsel-/Sichttest; Headless-Layouttests sind kein Nachweis echten Windows-DPI-Renderings. Kein Deployment.

## Schritt 13 – Wochenstunden deutlicher anzeigen (26.10.2026)

- Im dauerhaft sichtbaren Plan stehen je Halbjahr Kursanzahl und Wochenstunden nun zusammen in gleicher Schriftgröße (14) und gleichem halbfetten Gewicht. Die bisherige kleine, graue Stundenzeile wurde entfernt. Die bestehende Aussage, dass Belegung und Stunden keine Zulassungsbestätigung sind, bleibt sichtbar.
- Restore mit `--locked-mode --ignore-failed-sources -p:NuGetAudit=false` aus dem lokalen Paketcache erfolgreich; keine Paket-/Lockdateiänderung, kein Online-Audit.
- Release-Build im getrennten Ausgabeordner `bin/FullHd/`: **0 Warnungen, 0 Fehler**.
- Gesamte Testsuite: **109/109 bestanden**, keine übersprungen; darin 5 StudentApp-Tests mit UI-Workflow und drei Layoutgrößen. Beide JSON-Schemaprüfungen bestanden. Kein nativer Windows-Sichttest.

## M2 Schritt 1 – Profilprüfung (26.10.2026)

`dotnet test tests/GsmKurswahl.Storage.Tests -c Release --no-restore`: 44 bestanden, 0 Fehler, 0 übersprungen. Enthält flüchtig erzeugte ECDSA-P256-Testschlüssel, Manipulation, falsche Identität/SID/Jahrgang/Schema/Revision, unbekannten Schlüssel, ungültigen Algorithmus und fehlende Dateien. Keine Testschlüssel gespeichert.

## M2 Zwischenprüfung – Regelkern (26.10.2026)

Erster Gesamtbuild ohne `AVALONIA_TELEMETRY_OPTOUT=1`: fehlgeschlagen (MSB4018/UnauthorizedAccessException im Avalonia-BuildServices-Logpfad). Anschließender Gesamttest mit Opt-out: Domain 8/8, ImportExport 1/1, UI-Smoke 5/5 bestanden; Rules 34/38, Storage 43/44, Application 25/27. Sieben Fehler: bisherige M1-Gesamtstatus-Erwartungen für unvollständige Wahlen, alter M1-COMPLETENESS-Marker, noch fehlende Regeldokumentation und bisher als geplant erwartetes Wirtschaft-LF. M2 erkennt fehlende Pflichtbelegungen nun als invalid; Tests und Dokumentation werden entsprechend aktualisiert. Keine fachlichen Prüfungen zur Behebung abgeschaltet.

## M2 Schritt 2 – Belegungsregeln und Oberfläche (26.10.2026)

Zweiter Gesamttest `dotnet test GSM-Kurswahl.sln -c Release --no-restore` mit Avalonia-Opt-out: **137/137 bestanden** (Domain 8, Rules 52, Storage 44, ImportExport 1, Application 27, UI-Smoke 5). Zuvor Restore zunächst an nicht lesbarer Benutzer-NuGet.Config gescheitert; wiederholt mit erweitertem Zugriff: erfolgreich. Neue interne Projektreferenz zu ImportExport, keine neuen Paketversionen.

## M2 Abschlussprüfung – Korrekturlauf (26.10.2026)

Locked-Restore erfolgreich, Release-Build erfolgreich (0 Warnungen/0 Fehler). Drei Beispieldateien bestehen die JSON-Schemaprüfung. NuGet-Audit mit `--vulnerable --include-transitive --no-restore` meldet für alle Projekte keine bekannten anfälligen Pakete aus den abgefragten Quellen.

Erweiterter Testlauf: Domain 8, Rules 54, Storage 44, ImportExport 2, Application 28 bestanden. Neuer UI-Test blieb beim Schließen seines ungespeicherten Testentwurfs im Schutzdialog hängen; Lauf abgebrochen, kein Gesamt-Erfolg behauptet. Test verwendet jetzt einen speicherfähigen In-Memory-Store und speichert vor dem Schließen. `git diff --check` meldete zusätzliche Leerzeilen am Dateiende; diese bereinigt. Wiederholung folgt.

## M2 vor der PDF-/Sprachenkorrektur (26.10.2026)

Abschließendes `scripts/Test.ps1 -LockedMode` erfolgreich: Restore, Release-Build mit 0 Warnungen/Fehlern, **142 Tests bestanden** (Domain 8, Rules 54, Storage 44, ImportExport 2, Application 28, UI-Smoke 6), drei JSON-Schemaprüfungen erfolgreich. Danach Nutzerkorrektur: ausschließlich PDF statt HTML/Browserdruck; Französisch/Latein ergänzen und Fremdsprachenpflicht plus Sprach-/NW-Alternative immer sichtbar machen. Folgende Prüfungen beziehen sich auf diesen neuen Schritt.

## PDF-/Sprachenkorrektur – erster Testlauf (26.10.2026)

Restore mit expliziten SkiaSharp-Referenzen erfolgreich. Erster Gesamttest: Domain 8, Rules 54, Storage 44, ImportExport 3, UI-Smoke 6 erfolgreich; Application 24 erfolgreich/4 fehlgeschlagen. Die vier Tests erwarteten noch die alte gemeinsame Zwei-NW-Aufgabe bzw. INF in der ersten NW-Aufgabe. Tests an die getrennten Pflicht-/Alternativaufgaben angepasst; sieben neue Sprachpfad-Regressionstests ergänzt. PDF-Stresstest mit langen Labels/18 Meldungen erzeugt gültige dreiseitige PDF; pypdf bestätigt Umlaute und letzte Meldung. Erste Sichtprüfung zeigte Meldungen über Seitengrenzen geteilt; Layout hält jetzt normale Meldungsblöcke zusammen. Erneute Prüfung folgt.

PDF-/Sprachen-Korrekturlauf 2: Application-Tests zunächst nicht kompilierbar (mehrdeutiges ImmutableArray.AddRange mit Collection-Ausdruck und nur lokal in anderer Testklasse vorhandener MemoryStore). Collection-Ausdrücke direkt verwendet und eigenständigen Teststore ergänzt. Andere Projekte: Domain 8, Rules 54, Storage 44, ImportExport 3 und UI 6 bestanden. Kein Gesamt-Erfolg dieses Laufs.

PDF-/Sprachen-Korrekturlauf 3: Application **35/35 bestanden**, einschließlich sieben neuen Sprachpfadtests. Layout der dreiseitigen Stress-PDF nach Blockumbruch erneut vollständig visuell geprüft: keine Überlagerungen/Abschneidungen, Umlaute sichtbar, Seitenzahlen vorhanden. pdfplumber bestätigt sämtliche Textgrenzen innerhalb der A4-Seite; pypdf zuvor Vollständigkeit bis Prüfhinweis 18. Ein zusätzlicher UI-Smoke prüft nun die tatsächlich ausgelieferten F-/L-/CH-Optionen aus dem Samplekatalog.

## M2 PDF-/Sprachenstand – Abschluss (26.10.2026)

`scripts/Test.ps1 -LockedMode` vollständig erfolgreich: Locked-Restore; Release-Build **0 Warnungen, 0 Fehler**; gesamte Testsuite **151 bestanden, 0 fehlgeschlagen, 0 übersprungen** (Domain 8, Rules 54, Storage 44, ImportExport 3, Application 35, StudentApp 7); drei Beispieldatei-Schemas erfolgreich. UI-Smoke umfasst jetzt direkte PDF-Aktion ohne Druckbutton sowie tatsächlich sichtbare, wählbare F-/L-/CH-Alternativen aus dem ausgelieferten Katalog.

Aktueller PDF-Stresstest: 3 A4-Seiten aus dem Produktionsrenderer, Text mit Umlauten/ß, letzter Prüfhinweis 18 und Regelstand m2-2 nachweislich enthalten; alle Textbegrenzungen innerhalb der Seitenränder. Mit Poppler gerendert, visuell geprüft, normale Meldungsblöcke ungeteilt. Keine native Druckfunktion, kein HTML-/Browserexport. Der Stressbogen ist ausschließlich ein lokales Testartefakt, kein administrativ freigegebener Schulbogen.

Paket-Audit `dotnet list GSM-Kurswahl.sln package --vulnerable --include-transitive --no-restore`: erfolgreich, keine bekannten anfälligen Pakete für alle Projekte laut NuGet.org. `git diff --check`: erfolgreich. Keine Tests auf Schuldomäne/SMB, kein nativer macOS-/Linux-PDF-Sichttest und keine verbindliche Rechtsfreigabe für 2029 behauptet.

## M2 Review-Korrekturen und Abgleich mit AGVO / KOE 2026 (26.10.2026)

- Fachliche und technische Korrekturen nach Abgleich mit `docs/Rechtsgrundlage AGVO BW GymAbiPrV_BW_2018.pdf` (Stand 04.02.2025) und `Kursstufen-Organisationserlass (KOE) 2026` (Stand 05.02.2026):
  - **Sportbefreiung & Sportersatz (`ChoiceWorkflow`, `AcademicRules`):** Bei teilweiser oder dauerhafter Sportbefreiung ist Sport nicht als mündliches Prüfungsfach wählbar (`AGVO § 21 Abs. 2 Nr. 6`). Jeder zusätzliche Kurs in anderen Basisfächern aus Pflicht- oder Wahlbereich (`CourseType.Bf` oder `CourseType.Elective`) ersetzt Sport; für die Erfüllung sind mindestens 42 Kurse ohne SIA sowie die entsprechende Anzahl zusätzlicher Kurse maßgeblich (`KOE 2026 Kap. 3.4 S. 7`). Einzelhalbjahreskurse (`AGVO-SEMESTERS`) sind nur im Umfang der befreiten Sport-Halbjahre als Sportersatz zulässig.
  - **Religion / Ethik (`ChoiceWorkflow`, `AcademicRules`, `ChoiceValidator`):** Als mündliches Prüfungsfach ist regulär nur das bereits in Klasse 10 besuchte Fach (`ReligionEthicsGrade10`) wählbar (`AGVO § 21 Abs. 2 Nr. 4/5`, `KOE 2026 Kap. 8.6.3`). Bei einem Wechsel der Belegung gegenüber Klasse 10 sowie bei abweichender Prüfungsplanung wird über `AGVO-RELIGION` bzw. `PLAN-ORAL` explizit auf die erforderliche Überprüfung/Feststellungsprüfung zu Beginn von HJ 1 hingewiesen (bzw. auf den Ausschluss bei Abmeldung Rel -> Ethik nach `KOE 2026 Kap. 3.3`).
  - **Mindestanrechnung (`AcademicRules.MinimumCredits`):** Auch bei noch unvollständiger Wahl der ersten Naturwissenschaft oder der zweiten Fremdsprache/Naturwissenschaft werden die 12 Pflichtanrechnungskurse dieses Bereichs (`AGVO § 17 Abs. 1`) in der Mindest-Anrechnung berücksichtigt.
  - **Wahlassistent Schritt 3 (`ChoiceWorkflow.Tasks`):** `INF-BF` bleibt in `PLAN-AREA-BREADTH` auch dann als gewählt (`ChosenOfferIds`) markiert, wenn die erste Naturwissenschaft (`PLAN-AREA-SCIENCE`) noch offen ist; `firstLanguage` und `firstScience` werden konsistent aus den oberen Teilaufgaben bestimmt.
  - **Oberfläche & Build (`MainWindow`, `StudentSession`, `GsmKurswahl.StudentApp.csproj`):** Hinweistext in Schritt 1 nennt allgemein fortgeführte Fremdsprachen statt nur Englisch; `Prerequisites` zeigt bestätigte Sprachlaufbahnen, Belegungs-/Prüfungsstatus in Religion/Ethik und dauerhafte Sportbefreiung; `appsettings.json`, `profile-trust.json` und `samples/fachangebot-demo.json` werden in das Ausgabeverzeichnis kopiert.
- Test- und Buildläufe:
  - Offline-Restore in der Standard-Sandbox (`dotnet restore GSM-Kurswahl.sln --locked-mode -m:1 -p:NuGetAudit=false --disable-build-servers`): alle 13 Projekte erfolgreich wiederhergestellt.
  - Erster Release-Build (`dotnet build GSM-Kurswahl.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false --disable-build-servers`): **0 Warnungen, 0 Fehler**.
  - Erster Testlauf in der Standard-Sandbox scheiterte erneut an blockierter Loopback-IPC von `vstest.console` (Exit-Code 1 ohne Testausführung).
  - Erster Testlauf außerhalb der Sandbox: **154/155 bestanden, 1 fehlgeschlagen** (`IncompleteBreadthSelectionDoesNotUnderstateMinimumCredits` erwartete 30 statt 32 Kurse, weil GK als mündliches Prüfungsfach 4 statt 2 Pflichtkurse einbringt). Testerwartung auf 32 korrigiert.
  - Zweiter Release-Build: **0 Warnungen, 0 Fehler**.
  - Zweiter Testlauf außerhalb der Sandbox (`dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --logger trx --results-directory artifacts/test-results/final`): **155/155 Tests bestanden**, keine übersprungen (Domain 8, Rules 57, Storage 44, ImportExport 3, Application 36, StudentApp 7 inklusive Avalonia-Headless-UI-Smoke-Tests).
  - JSON-Schemaprüfung (`pwsh -NoProfile -File scripts/Test-Schemas.ps1`): `Schema OK` für alle drei Beispieldateien (`samples/schueler-demo.json`, `samples/fachangebot-demo.json`, `samples/profil-demo-unsigned.json`).
  - `git diff --check`: erfolgreich.
- Nachtrag (Entfernung „Mündliche Fächer später festlegen“ auf Nutzerwunsch):
  - `DeferOralButton` (`MainWindow`) und `DeferOralPlanning` (`StudentSession`) entfernt; `ChoiceValidator` verlangt nun stets zwei geplante mündliche Prüfungsfächer (`PLAN-ORAL`, `invalid` bei leerer oder unvollständiger Planung).
  - Zwei Zwischen-Testläufe in `LanguageChoiceTests` angepasst (Hilfsmethode `Create` wählt in Schritt 2 zwei mündliche Fächer außerhalb der Sprachen/Naturwissenschaften, damit die Sprach-/NW-Aufgaben in Schritt 3 unvorbelastet geprüft werden).
  - Abschließender Release-Build (**0 Warnungen, 0 Fehler**), gesamte Testsuite (**155/155 Tests bestanden**: Domain 8, Rules 57, Storage 44, ImportExport 3, Application 36, StudentApp 7), JSON-Schemaprüfung und `git diff --check` erfolgreich.
- Nachtrag (Tooltips für `klären`- und `Fehler`-Badges im Plan):
  - `PlanRow` um `IssueText` ergänzt; `MainWindow.BuildPlan` setzt den vollständigen Regel- und Abhilfetext (`Rule.Id: Explanation\nRemedy`) als Tooltip sowohl auf das `klären`-/`Fehler`-Badge als auch auf die Planzeile bzw. den Sprung-Button.
  - Release-Build (**0 Warnungen, 0 Fehler**), gesamte Testsuite (**155/155 Tests bestanden**, inkl. UI-Prüfung auf den Tooltip in `WindowTests`) und `git diff --check` erfolgreich.

## M3 – erster integrierter Entwicklungsschritt (26.10.2026)

- SQLite-Bestand, versionierte Stammdaten, append-only Wahlhistorie, Importvorschau CSV/XLSX, Signierung/Verteilung und Lehreroberfläche implementiert.
- Erstes `dotnet add ... Microsoft.Data.Sqlite --version 10.0.0` in Sandbox: Zugriff auf Benutzer-NuGet.Config verweigert. Mit erweitertem Zugriff Paketauflösung möglich, aber Restore wegen NU1903 für SQLitePCLRaw.lib.e_sqlite3 2.1.11 fehlgeschlagen (GHSA-2m69-gcr7-jv3q). Auf Microsoft.Data.Sqlite 10.0.14 / transitive SQLitePCLRaw 2.1.13 aktualisiert; Restore erfolgreich. Keine Audit-Unterdrückung.
- Erster Release-Build: CS8602 in TeacherWindow, nullable Importherkunft korrigiert. Zweiter Release-Build: 0 Warnungen, 0 Fehler.
- Gesamte Testsuite mit lokal freigegebenem Testhost: 165 bestanden, 0 fehlgeschlagen, 0 übersprungen (Domain 8, Rules 57, Storage 50, ImportExport 6, Application 36, UI 8). Artefakte unter artifacts/test-results/m3-first.
- Neue Tests: Einzelwriter-Sperre, unbekannte/defekte DB, stale Importvorschau, Lehreränderung/Altdatei, append-only Wiederherstellung, Vorbildungskorrektur, Signatur-Roundtrip/Profilrevision, beschädigtes Profil, 100 Einzelprofile mit genau einem gesperrten Ziel (99 erfolgreich), unveränderte Wahldateien, CSV-Mapping/Dubletten/unbekannte Angaben, XLSX/Formelablehnung sowie startende Lehreroberfläche.
- Anschließende Architekturprüfung: administrative Signierung/SQLite werden in ein eigenes, nur vom Lehrerprogramm verwendetes Assembly verlegt; Abschlussprüfung folgt.

## M3 – Architektur und Angebotsvorbereitung (27.10.2026)

- AdministrationStorage referenziert ausschließlich Domain; StudentApp hat weder diese Projektreferenz noch SQLite im neu ausgewerteten Lockgraph. Gemeinsamer JSON-Vertrag ohne Datei-/UI-Zugriff in Domain. Zertifikat bleibt ausschließlich administrativ; öffentlicher Schlüsselexport ergänzt.
- Zwischen-Build meldete CS0407 bei SelectMany mit ImmutableArray-Methodengruppe in CatalogConfiguration. Durch expliziten Lambdaausdruck korrigiert. Anschließender Release-Build: 0 Warnungen, 0 Fehler.
- Zweiter vollständiger Testlauf: **167 bestanden, 0 fehlgeschlagen, 0 übersprungen** (Domain 8, Rules 57, Storage 51, ImportExport 6, Application 37, UI 8), artifacts/test-results/m3-second. Neu: Reimport erhält manuelle Stammdatenkorrekturen/Wahlhistorie und Angebotsvorbereitung verändert den Ursprungskatalog nicht.
- Review-Nacharbeiten: kanonische Home-Pfade, stabile interne IDs, unbekannte Stammdatenschemata abweisen, Nachwahl-Zeitstempel, striktes CSV-Quoting, begrenztes Wahl-Einsammeln über bestehenden JsonChoiceStore, offene Vorschauen beim Bestandswechsel verwerfen. Demo-Importsample mit Regressionstest ergänzt. Erneuter Release-Build: 0 Warnungen, 0 Fehler. Abschlusslauf folgt.

- Erster M3-Abschlusslauf am 27.10.2026: Locked-Restore und Release-Build erfolgreich; 167/168 Tests bestanden. Neuer Demo-Sampletest fand eine verschobene CSV-Spalte (unknown stand in Religion statt Sport). Sample anhand aller 18 Spalten korrigiert. Skript brach vor Schemaprüfung ab; diese wird im Wiederholungslauf ausgeführt.

## M3 – erfolgreicher Abschlusslauf (27.10.2026)

- `pwsh -NoProfile -File scripts/Test.ps1 -LockedMode`: **erfolgreich**, Locked-Restore aller 14 Projekte, Release-Build **0 Warnungen / 0 Fehler**, gesamte Testsuite **168 bestanden / 0 fehlgeschlagen / 0 übersprungen**. Aufteilung: Domain 8, Rules 57, Storage 51, ImportExport 7, Application 37, UI 8. Die UI-Suite enthält den Avalonia-Headless-Smoke für Schüler- und Lehrerprogramm.
- Alle drei JSON-Sample-Schemas erfolgreich geprüft. Das neue CSV-Importsample wird zusätzlich durch den Importtest geprüft.
- `dotnet list GSM-Kurswahl.sln package --vulnerable --include-transitive --no-restore`: erfolgreich; laut NuGet.org für sämtliche 14 Projekte keine bekannten anfälligen Pakete.
- `git diff --check`: erfolgreich (nur Git-Hinweise zur automatischen LF/CRLF-Konvertierung).
- Keine privaten Schlüssel, lokalen Datenbanken oder echten Personendaten erstellt/eingecheckt. Synthetische Signiertests erzeugen Schlüssel nur im Arbeitsspeicher und löschen ihre temporären Dateien. Kein Deployment und keine Arbeit an M4/M5.
- Nicht ausgeführt: realer Windows-Zertifikatsspeicher-/Schuldomänen-/SMB-Test, native DPI-/Screenreader-Abnahme, selbstenthaltender Release/Schulnetzpilot. Die UI-Smoke-Tests ersetzen diese Prüfungen nicht. Fachliche Freigabe 2029 und verbindliche Endwahl bleiben offen.

## M3 – Dateiorientierung und verständliche Importe (27.10.2026)

- Nutzerwunsch: Dateitypen, Importwege und technische Begriffe verständlicher machen. Neue Startseite „Start & Dateien“ erklärt Ablauf, Dateirollen, Testdateien und Einrichtungsdateien. Sichtbare Hinweise und aufklappbare Erklärungen je Arbeitsbereich; ergänzende Tooltips an Aktionen, Kontofeldern und Spaltenauswahl. Zertifikateinrichtung zusammengefasst im aufklappbaren Schul-IT-Bereich.
- Technische Texteingabe für das Spaltenmapping durch deutsch beschriftete Auswahllisten ersetzt, Pflichtfelder markiert. Dateiwechsel verwirft alte Zuordnungen. Passende Filter und klarere Titel für Dateidialoge; Schülerlistenimport weist andere Endungen ausdrücklich ab. Fachregeln, Schemata und bestätigte Übernahmen unverändert; keine neuen Pakete.
- Erster Release-Build erfolgreich: 0 Warnungen, 0 Fehler. Abschluss `scripts/Test.ps1 -LockedMode`: Restore, Release-Build (0 Warnungen/0 Fehler), **169/169 Tests bestanden**, keine übersprungen, alle drei JSON-Sample-Schemas erfolgreich. Aufteilung: Domain 8, Rules 57, Storage 51, ImportExport 7, Application 37, UI 9.
- Avalonia-Headless-Smoke prüft die neue Startseite und alle sechs Reiter. Zusätzlicher UI-Test prüft Zuordnung tatsächlich ausgewählter Spalten, unbekannte optionale Felder, Tooltip und Zurücksetzen beim Dateiwechsel. `git diff --check` erfolgreich. Keine native Schulgeräte-/Screenreader-Abnahme behauptet.

## Jahrgangsoberfläche – erster Testlauf (27.10.2026)

- Neuer automatischer Einstieg über gespeicherten aktiven Jahrgang, fortsetzbare Einrichtung, Aufgabenübersicht aus aktuellen Verwaltungsdaten, expliziter Jahrgangswechsel, lokale Speicherortwahl und einmalige Einbindung vorhandener Bestände.
- Zwei Release-Builds erfolgreich, jeweils 0 Warnungen / 0 Fehler. Erster vollständiger Testlauf: **177 bestanden, 0 fehlgeschlagen, 0 übersprungen** (Domain 8, Rules 57, Storage 58, ImportExport 7, Application 37, UI 10). Ergebnisse unter artifacts/test-results/workspace-first.
- Neue Prüfungen: Einrichtungsfortsetzung, explizite aktive Jahrgangswahl, fehlender Bestand wird nicht neu erzeugt, defektes/unbekanntes Arbeitsstandschema, konkurrierende Änderungen, Einbindung bestehender Datenbank, gemerkter/fehlender Speicherort; UI durchläuft Jahrgang und Angebot bis zum Import und öffnet fertigen Jahrgang direkt mit passender nächster Aufgabe.
- Nacharbeiten: aktuelle Katalogversion gezielt exportieren, Fachauswahl bei bestehenden Wahlen schützen, Statusbereich in der Höhe begrenzen. Zusätzlicher Regressionstest für überholte Profile und fehlgeschlagene Zustellung in der Aufgabenübersicht. Abschlussprüfung folgt.

## Jahrgangsoberfläche – Abschlussprüfung (27.10.2026)

- `scripts/Test.ps1 -LockedMode` vollständig erfolgreich: Locked-Restore, Release-Build **0 Warnungen / 0 Fehler**, **178 Tests bestanden / 0 fehlgeschlagen / 0 übersprungen**, drei bestehende JSON-Sample-Schemas erfolgreich. Aufteilung: Domain 8, Rules 57, Storage 59, ImportExport 7, Application 37, UI 10.
- Der UI-Test durchläuft nun auch Schülerliste → Zuordnung → Abschluss und öffnet danach in einem neuen Fenster automatisch die Aufgabenübersicht. Zusätzlicher Dashboardtest bestätigt, dass neuere Zustellfehler und Vorbildungskorrekturen frühere erfolgreiche Zustellungen nicht als aktuell erscheinen lassen.
- `git diff --check` erfolgreich. Keine Paketänderungen; kein neuer Paket-Audit erforderlich oder behauptet. Keine Echtdaten, keine privaten Schlüssel, kein Deployment. Native Schulgeräte-/DPI-/Screenreader-Abnahme weiterhin offen; verfügbarer Avalonia-Headless-Smoke ausgeführt.

## Vereinfachung und Navigationskorrektur – erster Lauf (27.10.2026)

Release-Build erfolgreich, 0 Warnungen/Fehler. Vollständiger Lauf `scripts/Test.ps1 -LockedMode`: Restore/Build erfolgreich; 184 von 185 Tests bestanden, 1 fehlgeschlagen (Storage: Konkurrenztest schrieb nach Änderung des initialen Schritts denselben Inhalt und erwartete fälschlich eine Konkurrenzabweichung). Test schreibt jetzt tatsächlich einen anderen Schritt. UI-Navigation und Sammelimport bestanden. Schemaprüfung wegen Testfehler noch nicht gelaufen. Weitere Regressionen für automatische Sammlung und relative Reimporte ergänzt; Wiederholung folgt.

Zweiter Abschlussversuch: Restore erfolgreich; Build mit CS8602 in StudentImportService abgebrochen (Nullable-Fluss nach Normalisierung des Kandidaten). Importherkunft jetzt vor der Normalisierung als geprüfte lokale Variable gespeichert. In diesem Versuch keine Tests/Schemaprüfung gelaufen.

Dritter Lauf erfolgreich: Restore, Release-Build (0 Warnungen/Fehler), 187/187 Tests (Domain 8, Rules 57, Storage 62, ImportExport 10, Application 37, UI 13), drei Sample-Schemas. Automatische Sammlung mit Wiederholungsschutz und offenem Lehrerkonflikt sowie normalisierte Nachimporte getestet. Abschließende Ergänzung: doppelte SID/Ordner bereits in Importvorschau abweisen, vorhandenes gemeinsames Verteilungsverzeichnis vorbelegen; abschließender Lauf folgt.

## Vereinfachte Lehrerverwaltung – Abschluss (27.10.2026)

- Nutzerpräzisierung: Fachauswahl bleibt vor dem Import. Weniger Einzelschritte, tabellarische Sammelübernahme, gemeinsame Verteilung nach Grundverzeichnis und automatisches Einsammeln bleiben umgesetzt. Einstellungen/Hilfe haben einen dauerhaften Rückweg; wiederverwendete Ansichten besitzen jeweils nur einen Scroll-Container.
- Letzter vollständiger Lauf `scripts/Test.ps1 -LockedMode` erfolgreich: Locked-Restore, Release-Build mit **0 Warnungen / 0 Fehlern**, **189 Tests bestanden / 0 fehlgeschlagen / 0 übersprungen**. Domain 8, Rules 57, Storage 62, ImportExport 12, Application 37, UI 13. Drei Sample-Schemas erfolgreich. Auch der unmittelbar vorherige Stand bestand 189 Tests; nach der Nutzerpräzisierung vollständig erneut geprüft.
- Neue Regressionen: wiederholter Wechsel Einstellungen/Hilfe/Zurück vor Einrichtung; Sammelimport zweier vorausgewählter Zeilen; Standard-/Ausnahmeordner und unzulässige Ordnernamen; doppelte SID/Ziele; Wurzelbegrenzung und fehlende Verzeichnisse; unveränderte/relative Nachimporte erhalten bestätigte Zuordnungen; automatische Sammlung erzeugt keine Dubletten und überschreibt Lehrerentscheidungen nicht. Verfügbarer Avalonia-Headless-UI-Smoke ausgeführt.
- Keine neuen Pakete, keine privaten Schlüssel oder Echtdaten. Reale Schulnetz-/Windows-Zertifikatverteilung und native Bildschirm-/DPI-/Screenreader-Abnahme weiterhin nicht ausgeführt. Automatische Sammlung läuft nur bei geöffneter App; Kontodaten und Signierkonfiguration benötigen weiterhin einmalige administrative Vorbereitung.

## Optische Überarbeitung – erster Build (27.10.2026)

Lehreroberfläche: helle Flächen, grüne Hauptaktionen, Fachkacheln, Kennzahlenkarten und grafischer Dreischritt. Erster Release-Build kompilierte die Änderungen, scheiterte beim Kopieren der von einer laufenden Lehrerapp gesperrten EXE (MSB3027/MSB3021, 10 Wiederholungswarnungen). Kein Prozess beendet. Weitere Builds/Tests in separatem `artifacts/visual-review`-Ausgabeverzeichnis.

## Optische Überarbeitung – Abschluss (27.10.2026)

- Separater Locked-Restore mit `dotnet restore GSM-Kurswahl.sln --configfile NuGet.Config --locked-mode --artifacts-path artifacts/visual-review` erfolgreich. Release-Build mit demselben Artifacts-Pfad erfolgreich, **0 Warnungen / 0 Fehler**. Die laufende Lehrerapp blieb geöffnet.
- Vollständige Suite: `dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --artifacts-path artifacts/visual-review --logger trx --results-directory artifacts/test-results/visual-final`: **190 bestanden / 0 fehlgeschlagen / 0 übersprungen**. Domain 8, Rules 57, Storage 62, ImportExport 12, Application 37, UI 14. `scripts/Test-Schemas.ps1`: drei Sample-Schemas erfolgreich. `git diff --check` erfolgreich.
- UI-Test durchläuft Fachauswahl, Import, Verteilung und Unterbrechung. Prüft ausdrücklich, dass „Später verteilen“ weiterhin offene Zustellungen zeigt; Navigation Einstellungen/Hilfe/Übersicht und Breite der Schrittkarten bei 900 × 600 geprüft.
- Mit `GSM_UI_SCREENSHOTS` auf `artifacts/ui-preview` verwendet die Testsuite den vorhandenen Skia-Renderer. Fachauswahl, Import, Verteilung, Übersicht und kompakte Ansicht wurden als PNG gerendert und visuell geprüft. Nach erster Sichtprüfung Hauptaktion in der Übersicht nach oben verlegt und anschließend erneut gebaut/getestet/gerendert. Screenshots enthalten ausschließlich synthetische Daten. Kein neuer Paketbezug.
- Startbare geprüfte Lehrerfassung: `artifacts/visual-review/bin/GsmKurswahl.TeacherApp/release/GsmKurswahl.TeacherApp.exe`. Kein Deployment, kein Beenden der geöffneten App, keine Echtdatenänderungen. Native Schulgeräte-/DPI-/Screenreader-Abnahme bleibt offen.

## M3 Review-Korrekturen und anklickbare Verlaufsleiste (27.10.2026)

- Behebung der im M3-Review festgestellten Punkte sowie Umsetzung der direkt anklickbaren Verlaufsleiste (`WorkflowStep0..2` ohne zusätzliche Buttons darunter) und der Bearbeitung von `PriorEducation`/`DepartureStatus` in der Schülerdetailansicht (`ShowStudent`):
  - **ImportExport (`StudentTableImport`, `ImportColumnPicker`):** XLSX-Pfadnormalisierung bei `xl/`-Präfix, Groß-/Kleinschreibung in Zellreferenzen, Bereichsprüfung für Shared-Strings, 100-Spalten-Grenze in `Table`, Pflichtfeldprüfung für `name` bzw. `firstName`+`lastName`, case-insensitive Erkennung von `sport` (`unbekannt`/`keine`, sortierte Halbjahre), `religion` (`ToUpperInvariant`), `departure` (inkl. `bestätigt`) und `ExternalStudentId`-Dubletten; Pflichtfeldmarkierung (`*`) im Spaltenwähler.
  - **Storage & AdministrationStorage (`SignedProfileReader`, `ProfileDistribution`, `SqliteAdministrationStore`, `TeacherWorkspace`):** strikte Prüfung auf nichtleere Domänen-/Kontosegmente in `DOMÄNE\Konto`; Pfadseparator-Normalisierung bei der `HomeDirectory`-Eindeutigkeitsprüfung in SQLite; Jahrgangsprüfung vorhandener Datensätze beim Einbinden (`Attach`) einer bestehenden Datenbank.
  - **Application (`StudentImportService`, `TeacherDashboard`, `TeacherSession`):** Erhalt bestehender `AccountId` (SID) und bestätigter `HomeDirectory` bei leeren optionalen Spalten im Nachimport; Prüfung von `expectedKeyId` und Vergleich gegen die letzte profilrelevante Stammdatenänderung (`last.At >= profile.IssuedAt`) im Dashboard; `ActorRole.Student` bei `StudentSubmission`; sortierte Halbjahre im Revisions-Diff; Aktualisierung von `OfferVersion`/`RuleVersion`/`UpdatedAt` bei `Restore`.
  - **TeacherApp (`TeacherVisuals`, `TeacherWorkspaceShell`, `TeacherSimplifiedWorkflow`, `TeacherWindow`):** Schrittkarten `WorkflowStep0..2` direkt anklickbar, redundante `setupHeader`-Buttons entfernt; Beibehaltung der Katalogversion beim Anlegen des gleichen Jahrgangs; Schutz des Einrichtungsstatus bei „Fachauswahl erneut bearbeiten“ mit vorhandenen Schülern; Anzeige offener Sammelkonflikte auf dem Dashboard und Einzel-Auswahl/Verwerfen von Konflikten in `BuildCollection`; Abgleich von `WindowsUsername`/`AccountId`/`HomeDirectory` gegen Bestandsdaten in `ShowImportPreview`; Speicherung von Zertifikats-Thumbprint und `KeyId` in `PrepareDelivery`; Erhalt relativer Ordnerpfade bei unbestätigter Kontozuordnung sowie Verzeichnis-/ReparsePoint-Prüfung bei bestätigter Zuordnung; vollständige Bearbeitungsmöglichkeit für `PriorEducation` und `DepartureStatus` in `ShowStudent`; Aktualisierung von `OfferVersion`/`RuleVersion`/`Revision` bei `TeacherEdit`, Anzeige von `PlannedOralSubjects` in der Historie und `Note` bei Nachwahlen sowie automatisches Aktualisieren einer geöffneten `ShowStudent`-Ansicht bei `Refresh()`.
  - **Bereinigung & Schemata (`scripts/Test-Schemas.ps1`, `schemas/teacher-workspace.v1.schema.json`, `README.md`):** Temporäre Arbeitsdateien `samples/verwaltung.db`, `samples/verwaltung.db.lock` und `samples/klasse10-demo.csv.lock` entfernt; alle 6 JSON-Schemata in `scripts/Test-Schemas.ps1` geprüft (`catalog`-Schema in `teacher-workspace.v1.schema.json` für Offline-`Test-Json` eingebettet).
- Test- und Buildläufe:
  - Offline-Restore in der Standard-Sandbox (`dotnet restore GSM-Kurswahl.sln --configfile NuGet.Config --locked-mode -m:1 -p:NuGetAudit=false --disable-build-servers`): alle 14 Projekte erfolgreich wiederhergestellt.
  - Erster Release-Build in der Standard-Sandbox (`dotnet build GSM-Kurswahl.sln -c Release --no-restore --no-dependencies -m:1 -p:UseSharedCompilation=false --disable-build-servers -p:AvaloniaPreviewerTelemetryOptOut=true`): **1 Fehler** (`CS8602` in `tests/GsmKurswahl.ImportExport.Tests/StudentTableImportTests.cs`, Zeile 89 nach `Assert.Null`). Durch Null-Forgiving-Operator (`rows[0].Candidate!`) behoben.
  - Zweiter Release-Build in der Standard-Sandbox: **0 Warnungen, 0 Fehler**.
  - Erster Testlauf außerhalb der Sandbox (`dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --logger trx --results-directory artifacts/test-results/m3-review-fixes`): **191/192 bestanden, 1 fehlgeschlagen** (`StudentDetailsAllowPrerequisiteEditingAndPreserveRelativeHomeDirectory` wegen xUnit-Sammlungsvergleich `string[]` gegen `ImmutableArray<string>`). Durch `.ToArray()` im Test korrigiert.
  - Dritter Release-Build in der Standard-Sandbox: **0 Warnungen, 0 Fehler**.
  - Abschließender Testlauf außerhalb der Sandbox (`dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --logger trx --results-directory artifacts/test-results/final`): **192/192 Tests bestanden**, 0 fehlgeschlagen, 0 übersprungen (Domain 8, Rules 57, Storage 62, ImportExport 13, Application 37, StudentApp/UI 15 inklusive Avalonia-Headless-UI-Smoke-Tests für Schüler- und Lehrerprogramm).
  - JSON-Schemaprüfung: Erster Lauf in der Standard-Sandbox wegen blockiertem AppExecutionAlias `pwsh.exe` nicht ausführbar; zweiter Lauf außerhalb der Sandbox meldete bei `schemas/teacher-workspace.v1.schema.json` eine nicht aufgelöste externe `$ref`-Referenz in `Test-Json`. Nach Einbettung des Katalog-Teilschemas in `schemas/teacher-workspace.v1.schema.json` dritter Lauf (`pwsh -NoProfile -File scripts/Test-Schemas.ps1`): **alle 6 Schemaprüfungen bestanden** (`samples/schueler-demo.json`, `samples/fachangebot-demo.json`, `samples/profil-demo-unsigned.json`, `schemas/profile-signature.v1.schema.json`, `schemas/teacher-location.v1.schema.json`, `schemas/teacher-workspace.v1.schema.json`).
  - `git diff --check`: ohne Fehler.

## Streichung der Zertifikats- und SID-Pflicht (27.10.2026)

- Auf ausdrücklichen Nutzerwunsch wurde die Zertifikats-/Signaturpflicht (`profil.sig`, `profile-trust.json`, `X509Store`, `ECDsa`, `CertificateThumbprint`, `KeyId`) sowie die Pflicht zur Zuordnung von Windows-SID (`AccountId`) und Windows-Benutzername (`DOMÄNE\Konto`) aus Code, Schemas und Dokumentation gestrichen:
  - **Storage (`SignedProfileReader`):** liest und prüft `profil.json` direkt ohne `profil.sig`, ohne ECDSA-Schlüssel und ohne Windows-SID-Abgleich; `WindowsUsername`, `AccountId` und `KeyId` dürfen leer sein (`""`).
  - **AdministrationStorage (`ProfileDistribution`):** `Issue(StudentRecord)` und `Distribute(StudentRecord, IssuedProfile)` stellen `profil.json` direkt in den bestätigten Schülerordner zu, ohne privaten Schlüssel, `KeyId`, `AccountId` oder `profil.sig`.
  - **Application & ImportExport (`TeacherDashboard`, `TeacherSession`, `StudentTableImport`):** keine Pflichtprüfung auf `expectedKeyId`, `WindowsUsername` oder `AccountId` im Dashboard; `WindowsUsername`-Abgleich in `Preview` nur noch, wenn beide Seiten gesetzt sind; `username` im Tabellenimport optional (`studentId`, `class`, `year` und `name` bzw. `firstName`+`lastName` genügen).
  - **StudentApp & TeacherApp (`App`, `ImportColumnPicker`, `TeacherSimplifiedWorkflow`, `TeacherWindow`, `TeacherWorkspaceShell`, `TeacherVisuals`):** `SignedProfileReader` ohne `profile-trust.json` geladen; Spalte `accountId` (SID) aus `ImportColumnPicker` entfernt; Zertifikats-Thumbprint- und `KeyId`-Eingaben sowie SID-Pflichtprüfungen aus Verteilung, Schülerdetailansicht, Einstellungen und Verlaufsleiste entfernt.
  - **Schema & Dokumentation (`schemas/student-profile.v1.schema.json`, `docs/GSM_Kurswahl_Anforderungen_Codex.md`, `docs/PROFILE.md`, `docs/DATEIFORMAT.md`, `docs/LEHRERVERWALTUNG.md`, `docs/OFFENE_PUNKTE.md`, `README.md`, `AGENTS.md`):** leere Zeichenketten für `windowsUsername`, `accountId` und `keyId` erlaubt und Dokumentation konsistent angepasst.
- Test- und Buildläufe:
  - Offline-Restore in der Standard-Sandbox (`dotnet restore GSM-Kurswahl.sln --configfile NuGet.Config --locked-mode -m:1 -p:NuGetAudit=false --disable-build-servers`): alle 14 Projekte erfolgreich wiederhergestellt.
  - Erster Release-Build in der Standard-Sandbox mit `--no-dependencies` (`dotnet build GSM-Kurswahl.sln -c Release --no-restore --no-dependencies -m:1 -p:UseSharedCompilation=false --disable-build-servers -p:AvaloniaPreviewerTelemetryOptOut=true`): **fehlgeschlagen** mit `CS0103` in `src/GsmKurswahl.TeacherApp/TeacherVisuals.cs(82,134)` (verbliebener Aufrufparameter `keyId.Text?.Trim()` an `TeacherDashboardBuilder.Build`) sowie Folge-Fehlern `CS7036` wegen `--no-dependencies`-Buildreihenfolge vor Neubau von `GsmKurswahl.AdministrationStorage`. In `TeacherVisuals.cs` behoben.
  - Zweiter Release-Build in der Standard-Sandbox ohne `--no-dependencies` (`dotnet build GSM-Kurswahl.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false --disable-build-servers -p:AvaloniaPreviewerTelemetryOptOut=true`): **0 Warnungen, 0 Fehler**.
  - Testlauf: In der Standard-Sandbox wegen blockiertem Loopback-IPC (`vstest.console`) mit Exit-Code 1 abgebrochen; anschließend außerhalb der Sandbox (`dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --logger trx --results-directory artifacts/test-results/final`): **189/189 Tests bestanden**, 0 fehlgeschlagen, 0 übersprungen (Domain 8, Rules 57, Storage 58, ImportExport 13, Application 37, StudentApp/UI 16 inklusive neuem Test `SimpleDistributionDeliversProfilesWithoutCertificateOrSid` sowie Avalonia-Headless-UI-Smoke-Tests).
  - JSON-Schemaprüfung (`pwsh -NoProfile -File scripts/Test-Schemas.ps1`, außerhalb der Sandbox wegen `pwsh.exe`-AppExecutionAlias): **alle 6 Schemaprüfungen bestanden**.
  - `git diff --check`: ohne Fehler.

## Unabhängiger M3-Review nach Wegfall der Zertifikatspflicht (27.10.2026)

Geprüfter Stand: `31cfda7`, einschließlich der M3-Vorgänger `ba3a75e` und `e8ae21c`. Die dokumentierte Entscheidung gegen Zertifikats-/SID-Pflicht wurde als beabsichtigter Austauschvertrag behandelt. `ProfileDistribution` erzeugt ohne Schlüssel ein Einzelprofil; der Schülerstart benötigt weder Signatur noch Trustdatei. `TeacherSession` validiert eingesammelte Wahlen weiterhin gegen die geschützten Stammdaten. Produktcode im Review nicht geändert.

Prüfung:
- `dotnet restore GSM-Kurswahl.sln --configfile NuGet.Config --locked-mode --artifacts-path artifacts/m3-review-build`: erfolgreich.
- `dotnet build GSM-Kurswahl.sln -c Release --no-restore --artifacts-path artifacts/m3-review-build`: erfolgreich, 0 Warnungen / 0 Fehler.
- `dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --artifacts-path artifacts/m3-review-build --logger trx --results-directory artifacts/test-results/m3-review-baseline`: 189/189 bestanden, 0 übersprungen (Domain 8, Rules 57, Storage 58, ImportExport 13, Application 37, UI 16 einschließlich Headless-Smoke).
- `scripts/Test-Schemas.ps1`: alle sechs bestehenden Prüfungen bestanden.
- Zusätzliches isoliertes Testprojekt `artifacts/m3-review/M3Review.csproj` mit ausschließlich synthetischen Daten und bereits vorhandenen Paketversionen. Lauf mit `dotnet test artifacts/m3-review/M3Review.csproj -c Release --no-restore --artifacts-path artifacts/m3-review-build --logger trx --results-directory artifacts/test-results/m3-review-reproductions`: 6 Tests ausgeführt, 6 fehlgeschlagen. Diese Tests formulieren das erwartete Verhalten; die Assertion-Abweichungen reproduzieren die unten genannten Fehler bzw. Integrationslücken. Kein Fehler beim Kompilieren des Testprojekts.

Befunde (noch nicht korrigiert):
1. P1: `TeacherWindow.Refresh`, Zeilen 106–107, baut die offene Personenbearbeitung aus gespeicherten Daten neu auf. Jeder automatische Sammellauf ruft Refresh auf, auch ohne neue Wahl. Ein ungespeichertes IMP=Yes wird dadurch reproduzierbar wieder Unknown. Auch andere Eingaben im Personeneditor sind betroffen.
2. P1: `TeacherSimplifiedWorkflow.RenderCollectionStatus`, Zeilen 151–156, verwirft die Konfliktauswahl und erzeugt jedes Kontrollkästchen erneut mit IsChecked=true. Nach dem nächsten Sammellauf sind bewusst ausgeschlossene Konflikte wieder ausgewählt. Die globale Bestätigung/Begründung bleibt dabei stehen; die folgende Sammelübernahme kann unerwünschte Konflikte zugunsten der Schülerdatei entscheiden. Reproduktion: Auswahl false, weiterer Sammellauf, tatsächlicher Wert true.
3. P2: `TeacherDashboard.Build`, Zeilen 21–22, bewertet den letzten erfolgreichen Bericht nur anhand Schüler-ID, Profilrevision und Profilzeit. Nach bestätigtem Wechsel in einen neuen leeren Ordner bleibt Delivered=1, obwohl dort kein Profil liegt. Der Bericht benötigt eine Bindung an den zugestellten Zielpfad bzw. die entsprechende Zuordnungsrevision; beliebige nicht profilrelevante Stammdatenänderungen dürfen dabei weiterhin unschädlich bleiben.
4. P2: `TeacherWorkspaceShell.ShowOfferSetup`, Zeilen 202–210, prüft im Speicherereignis nicht erneut, ob inzwischen eine Wahl vorliegt. Wenn die Fachauswahl offen bleibt und das automatische Einsammeln eine erste Wahl übernimmt, lässt sich anschließend weiter das Angebot ändern. Reproduziert durch Anlegen einer StudentSubmission bei offener Auswahl und anschließendes Umschalten eines Fachs; die Katalogversion wurde trotzdem geändert. Bereits eingelesene Wahlen erhalten dadurch OFFER-VERSION-Abweichungen.
5. P2 / bestehende M3-Integrationslücke: Die gemeinsame Verteilung stellt `profil.json` direkt im Schülerordner bereit, während die ausgelieferte Schülerkonfiguration einen zusätzlichen Unterordner `GSM-Kurswahl-Demo` benutzt. Selbst mit HomeDirectory auf den richtigen Schülerordner zeigt die Schülerprüfung daher Missing. Unabhängig davon verwendet `StudentApp/App.cs`, Zeilen 26–27, weiterhin die installationsweite DemoStudentId (ausgeliefert demo-001); ein im richtigen Ordner gelesenes Profil für demo-002 ergibt ProfileInvalid. Für den gewünschten gemeinsamen Schülerclient fehlt eine abgestimmte Übergabe von Zielverzeichnis und persönlicher ID; derzeit wären passende individuelle Einstellungen zusätzlich erforderlich. Diese beiden Lücken wurden getrennt reproduziert und sind nicht als Regression der bewusst entfernten Kryptografie zu verstehen.

Die Reproduktionsdateien und Ergebnisse liegen ausschließlich im ignorierten artifacts-Verzeichnis. Keine Echtdaten, Netzlaufwerksänderungen oder Zertifikatsänderungen. Es wurden keine Korrekturen, Commits oder Pushes durchgeführt.

### M3-Reviewfehler korrigiert (27.10.2026)

- Automatische Sammlung erhält den offenen Personeneditor und die Profilauswahl. Konfliktauswahl bleibt für unveränderte Datei-/Bestandsrevisionen erhalten; neue Konflikte setzen die pauschale Konfliktfreigabe zurück. Jahrgangswechsel leert Konfliktzustände.
- Angebotsänderungen prüfen auch im Änderungsereignis, ob inzwischen Wahlen vorliegen. Zustellstatus wird mit dem zum Verteilungszeitpunkt vorhandenen Ziel aus der append-only Stammdatenhistorie verglichen; keine Datenbankmigration nötig.
- Schülerkonfiguration v2 verwendet einen expliziten persönlichen Wahlordner und dessen Einzelprofil statt einer festen demo-001-ID. Der aktuelle Katalog wird mitverteilt. v1 bleibt unverändert lesbar; keine Zertifikats- oder SID-Pflicht eingeführt.
- Sechs Regressionstests dauerhaft in M3RegressionTests übernommen. Integrationstests prüfen nun den vorgesehenen v2-Einstieg einschließlich Katalogverteilung und demo-002; explizite ID-Abweichungen bleiben ungültig.
- Restore mit Lockdateien erfolgreich nach erneutem Aufruf mit erweitertem Zugriff (erster Versuch: Zugriff auf Benutzer-NuGet.Config verweigert). Release-Build: 0 Warnungen, 0 Fehler. Gesamtsuite: 195/195 bestanden (8 Domain, 57 Rules, 58 Storage, 13 ImportExport, 37 Application, 22 UI/Integration), keine übersprungen. UI-Smoke-Tests enthalten die Hintergrundsammlung und den Angebotseditor. Sieben Schema-Prüfungen bestanden. Diff-Prüfung fand zunächst eine zusätzliche Leerzeile am Dateiende; entfernt.
- Befehle: dotnet restore/build/test GSM-Kurswahl.sln mit --artifacts-path artifacts/m3-review-build, Release und --locked-mode beim Restore; Testberichte unter artifacts/test-results/m3-fixes-final. Zusätzlich pwsh -NoProfile -File scripts/Test-Schemas.ps1 und git diff --check.
