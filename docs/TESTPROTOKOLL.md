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

### M4-Kursplanung implementiert und geprüft (28.10.2026)

- Nachfrage je Angebot/Variante/Halbjahr, LF-Kombinationen und paarweise Überschneidungen; Abgängerfilter verändert ausschließlich Planungszahlen. Zielgruppengröße als gespeicherte Schätzung, keine Unterrichtsgruppenzuweisung.
- Separate Vorschau/Szenarien, unveränderliche vollständige Planungsstände und Vergleich mit aktuell. Exakte Angebotsvarianten streichen, Auswirkungen gegen geschützte Voraussetzungen prüfen, Ersatzangebote vorläufig anzeigen. Bestätigung und Grund erforderlich; neue administrative Revisionen erhalten Schülerhistorie.
- Datenbankschema 2 wird nur über die ausdrückliche Aktivierungsaktion eingerichtet. Angebot, Revisionen, Basisstand und Nachwahlvermerke werden atomar gespeichert. Geänderte Schülerdaten, Wahlen oder Nachwahlvermerke verwerfen eine veraltete Vorschau. Angebot aus früherem Stand als neue Version wiederherstellbar; keine automatische Wahl im Namen der Schüler.
- Angebotsentscheidungen in SQLite sind nach Unterbrechungen maßgeblich; workspace-Kopie wird beim Öffnen synchronisiert. Zustellstatus fordert nach Angebotsänderung erneute Katalogverteilung. Keine Zertifikats-/SID-Pflicht und keine neuen Paketabhängigkeiten.
- UI in drei Reiter gegliedert: Auswertung, gespeicherte Stände, Angebot ändern. Angebote ohne Nachfrage separat aufklappbar. Renderdatei artifacts/ui-m4-final/teacher-planning.png erstellt und visuell geprüft.

Prüfverlauf und Fehler:
- Erste neue Testdateien kompilierten zunächst wegen fehlender void-Rückgabetypen nicht (CS1520/CS0592); Testdeklarationen korrigiert. Kein erfolgreicher Lauf für diese Fehlversuche behauptet.
- Ein Render-Aufruf mit --no-build fand infolge des vorangegangenen Buildfehlers den neuen Test noch nicht; dieser Lauf zählt nicht als UI-Nachweis.
- Danach 10 neue Storage-Planungsfälle erfolgreich. Gesamtlauf zeigte eine alte M3-Fenstertitel-Erwartung; durch die stabile Lehrerverwaltungsbezeichnung ersetzt.
- Nach UI-Reiterumbau lieferte der logische Avalonia-Baum dieselbe Control-Instanz über mehrere Pfade; Testsuche dedupliziert. Anschließender Render-/Bedienungstest erfolgreich.

Abschluss:
- dotnet restore GSM-Kurswahl.sln --configfile NuGet.Config --locked-mode --artifacts-path artifacts/m3-review-build: erfolgreich, unveränderte Lockdateien (erweiterter Zugriff auf lokale NuGet-Konfiguration).
- dotnet build GSM-Kurswahl.sln -c Release --no-restore --artifacts-path artifacts/m3-review-build: erfolgreich, 0 Warnungen / 0 Fehler.
- dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --artifacts-path artifacts/m3-review-build --logger trx --results-directory artifacts/test-results/m4-final: **206/206 bestanden**, keine übersprungen (Domain 8, Rules 57, Storage 68, ImportExport 13, Application 37, UI/Integration 23). GSM_UI_SCREENSHOTS=artifacts/ui-m4-final aktiviert; UI-Smoke und Rendering in diesem Lauf enthalten.
- PlanningTests prüfen Variantentrennung, unveränderte aktive Wahlen bei Simulation, deterministische Abgängeranalyse, Gruppenschätzung, Bestätigungspflicht, erneutes Öffnen, doppelte Übernahme, drei Arten veralteter Vorschauen, kompletten Rollback bei Fehler mitten in einer Transaktion, unveränderliche Snapshots, unbekannte Schema-Version, Szenariospeicherung, Wiederherstellung und Katalog-Neuzustellung.
- PlanningWindowTests prüfen Aktivierung, Simulation, Übernahme erst nach Bestätigung, Navigation und Wiederaufnahme bei veralteter workspace-Katalogkopie nach bereits erfolgtem SQLite-Commit.
- pwsh -NoProfile -File scripts/Test-Schemas.ps1: 7/7 erfolgreich. git diff --check: erfolgreich. Alle Daten synthetisch; kein Schulnetz-Pilot oder fachliche Rechtsfreigabe behauptet.

M3-Korrekturen zuvor separat als 07c83c9 committet. Die M4-Änderungen liegen anschließend zur Prüfung im Arbeitsverzeichnis; kein Push durchgeführt.

### Vorbereiteter Testjahrgang mit 80 Schülern (28.10.2026)

- DemoCohort erzeugt vier Klassen mit je 20 synthetischen Personen und 16 LF-Kombinationen. Vorbildung passend zu den belegten Angeboten, normale/bilinguale BIO-Varianten, F/L, INF, PSY und SIA. 68 aktive, 8 voraussichtliche und 4 bestätigte Abgänger. Alle 80 Wahlen ohne Invalid-Meldungen; needs_review bleibt wegen der allgemeinen fachlichen Freigaben bestehen. Keine fingierte verbindliche Abgabe.
- DemoWorkspace legt einen separaten vollständigen Bestand mit Profilen, Katalogen und aktuell.json je Person an. Zwei Planungsstände und ein separates Streichungsszenario enthalten. Vorhandene nicht markierte Ordner werden abgewiesen, erneutes Öffnen erhält Experimente. Normaler Verwaltungsort bleibt unverändert.
- Start über Testversion-80-Schueler.cmd / scripts/Start-Demo80.ps1. Tatsächlicher Bestand unter artifacts/demo-80 vorbereitet und erneut geöffnet; 80 aktuell.json-Dateien gezählt, TESTBESTAND.json meldet 80 Schüler / 16 LF-Kombinationen / 0 ungültige Wahlen. Keine generierte Datenbank eingecheckt; Generator reproduzierbar im Quellcode.
- Erstversuch der neuen Tests scheiterte an fehlenden void-Deklarationen, korrigiert. Die Umstellung auf einen echten Avalonia-Test deckte einen Deadlock beim synchronen Warten auf LoadAsync im UI-Thread auf; abgebrochener Gesamtlauf ist kein Erfolgsnachweis. Der Generator verwendet jetzt den Hash der gerade geschriebenen Bytes ohne blockierendes async-Warten.
- Gezielte Tests danach: 2/2 bestanden, einschließlich Anlegen und Öffnen der 80-Personen-Oberfläche, Analyse, Erhalt einer Lehreränderung beim erneuten Start und Schutz fremder Ordner. Rendering artifacts/ui-demo80/demo-80-analysis.png erstellt und visuell geprüft.
- Abschließender Locked-Restore erfolgreich. Release-Build: 0 Warnungen, 0 Fehler. Gesamtsuite: 208/208 bestanden, keine übersprungen (8 Domain, 57 Rules, 68 Storage, 13 ImportExport, 37 Application, 25 UI/Integration). Berichte artifacts/test-results/demo80-complete. Befehle mit --artifacts-path artifacts/m3-review-build, Release, --no-build --no-restore beim Abschlusstest. Schema-Prüfung 7/7, git diff --check erfolgreich. Keine neuen Pakete.

### Arbeitsphasen, Ergebnistabelle und kompakte Planung (28.10.2026)

- Navigation Übersicht / Wahl / Wahlergebnisse / Kursplanung; vierstufiger Verlauf nur für die Wahlorganisation. Ergebnistabelle zeigt Kurswahlen und speichert Abgängeränderungen mit Versionsprüfung. Voraussetzungen bleiben im geschützten Bestand und in der Regelprüfung erhalten.
- Nachfrage aller 22 Fächer kompakt in zwei Blöcken bei breitem Fenster, einem Block bei schmalem Fenster. LF/BF/bilingual getrennt; identische Halbjahreszahlen zusammengefasst, Abweichungen explizit. Abgängerfilter wirkt sofort; Details in Tooltips und aufklappbaren Bereichen.
- Prüfverlauf: Build im bisherigen Ausgabeordner zunächst durch laufende TeacherApp gesperrt (10 MSB3026-Warnungen, MSB3027/MSB3021). Separaten Ausgabeordner compact-ui-build verwendet. Locked-Restore dort erfolgreich. Erste UI-Läufe fanden nach Navigation noch nicht materialisierte Inhalte; Testhelfer um UpdateLayout ergänzt. Gezielte Demo-Tests danach 2/2 bestanden. Zusätzlicher Größenwechseltest deckte ausbleibende Neuanordnung auf; Reaktion auf Fensterbreitenänderung korrigiert und beim Verlassen der Seite abgemeldet.
- Abschluss: dotnet build GSM-Kurswahl.sln -c Release --no-restore --artifacts-path artifacts/compact-ui-build erfolgreich, 0 Warnungen / 0 Fehler. Gesamtsuite im selben Ausgabeordner: 209/209 bestanden, keine übersprungen (Domain 8, Rules 57, Storage 68, ImportExport 13, Application 38, UI/Integration 25). TRX-Berichte: artifacts/test-results/compact-verified.
- UI-Smoke prüft Navigation, ausgeblendeten Verlauf, alle 22 Fachzeilen, Größenwechsel, 80 Ergebniszeilen und Abgängeränderung mit anschließenden Planungszahlen (68 → 67 ohne voraussichtliche bzw. 75 mit voraussichtlichen Abgängern). Renderbilder unter artifacts/ui-compact-verified: Planung breit/schmal und Wahlergebnisse erstellt und visuell geprüft.
- scripts/Test-Schemas.ps1: 7/7 erfolgreich; git diff --check erfolgreich (nur Git-Hinweise zur Zeilenendenkonvertierung). Keine neuen Pakete oder Schemaänderungen durch diesen UI-Umbau. Demo-Startskript nutzt den aktualisierten Ausgabeordner und erhält artifacts/demo-80. Kein Commit oder Push in diesem Schritt.

### GK/Geo nach Einzelhalbjahren (28.10.2026)

- Befund: Der bisherige Demo-Generator belegte GK-BF stets in HJ 1–4 und GEO-BF stets in HJ 3–4, auch bei W-LF. Die zusammengefassten Zahlen waren korrekt, die Testfälle aber zu einheitlich.
- Generator ergänzt GK HJ 1, HJ 1/2, HJ 1/3/4 und HJ 1–4 sowie GEO nur HJ 3 bei W-LF. Mindestkurszahl wird mit weiteren angebotenen Fächern aufgefüllt; jede Wahl durchläuft weiterhin den produktiven Regelkern ohne Invalid-Meldungen. Keine Regeländerung.
- Ergebnistabelle benennt GK-/Geo-BF-Halbjahre ausdrücklich. Planungsmatrix zeigt für beide BF die Nachfrage je angebotenem Halbjahr auch bei identischen Zahlen. Tests prüfen alle genannten Belegungsmuster und explizite Halbjahreszahlen.
- Neuer separater Bestand artifacts/demo-80-gk-geo tatsächlich vorbereitet (80 Personen); Start über Testversion-GK-Geo.cmd. Bisheriger Demo-Bestand bleibt erhalten.
- Erster Testlauf scheiterte beim Kompilieren an der Typbestimmung eines verschachtelten Sammlungsausdrucks im Generator; explizite Arrays und Klammerung korrigiert. Anschließend Locked-Restore erfolgreich, Release-Build 0 Warnungen / 0 Fehler, gesamte Testsuite 209/209 erfolgreich, keine übersprungen. Berichte artifacts/test-results/gk-geo-final; UI-Smoke enthalten. Renderbilder artifacts/ui-gk-geo für Planungsmatrix und Ergebnistabelle visuell geprüft. git diff --check erfolgreich (nur Zeilenendenhinweise). Keine neuen Abhängigkeiten.

### M4-Review und Korrekturen (28.10.2026)

- Ausgangsprüfung gegen Spezifikation und `AGENTS.md`: Locked-Restore erfolgreich, Release-Build 0 Warnungen / 0 Fehler, Baseline-Testsuite 209/209 bestanden (`artifacts/test-results/m4-review-check`), Schema-Prüfung 7/7 erfolgreich.
- Behobene Review-Befunde:
  1. `TeacherPlanning.cs`: Zielgruppengröße (`PlanningGroupSize`) in der oberen Planungsleiste verankert, persistent in `planningGroupSize` gehalten und bei Änderung sofort auf Bericht/Gruppenschätzungen angewendet.
  2. `CoursePlanning.cs`: Vorläufige Ersatzvorschläge (`ReplacementOffers`) prüfen bei Sonderfremdsprachen (`I`, `GR`, `SPA`, `RU`, `POR`) zusätzlich die bestätigte Sprachlaufbahn (`AcademicRules.Languages(profile)`) und priorisieren alternative Varianten desselben Fachs sowie passende Kursarten (LF vs. BF/Wahlfach).
  3. `CoursePlanning.cs` / `TeacherPlanning.cs`: Bei bestätigter Streichung eines Zusatzfaches ohne strukturelle `Invalid`-Meldung bleibt `Reselection.Required` unverändert (`false`, sofern nicht bereits offen) und erhält einen expliziten Vermerk (`nach Streichung weiterhin strukturell gültig`); im Reiter „Angebot ändern“ sind kombinierbare Filter nach Folge (`nachwahl_nötig`, `nach_streichung_weiterhin_gültig`, `ungeprüfter_sonderfall`, `ungültig`) und Abgängerstatus (`abgänger`) ergänzt.
  4. `TeacherResults.cs`: Änderung des Abgängerstatus in der Ergebnistabelle aktualisiert Zeilentooltip und Hintergrundliste sofort.
  5. `TeacherPlanning.cs`: Gespeicherte Planungsstände können neben dem aktuellen Bestand auch direkt mit jedem anderen gespeicherten Planungsstand/Szenario verglichen werden (inklusive Angebots-, Nachfrage- und LF-Kombinationsdifferenzen).
- Prüfverlauf und Zwischenfehler:
  - Erster Lauf des neuen Tests `ElectiveCancellationKeepsValidChoiceWithoutReselectionAndPrioritizesValidReplacements` in `GsmKurswahl.Storage.Tests` schlug fehl (`Assert.DoesNotContain("L-LF", ...)`), weil `AcademicRules.Languages(profile)` die regulären Schulsprachen `E`, `F`, `L` immer freigibt und nur Sonderfremdsprachen (`I`, `GR`, `SPA`, `RU`, `POR`) an `ContinuedLanguages` bindet. Filter in `CoursePlanning.BuildPreview` auf die Sonderfremdsprachen präzisiert und Testfall mit aktiviertem `I-BF` (bei `ItalianProfile = Yes`, aber ohne `I` in `ContinuedLanguages`) geprüft.
- Abschluss:
  - `dotnet restore GSM-Kurswahl.sln --configfile NuGet.Config --locked-mode -m:1 -p:NuGetAudit=false --disable-build-servers --artifacts-path artifacts/compact-ui-build`: erfolgreich, Lockdateien unverändert.
  - `dotnet build GSM-Kurswahl.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false --disable-build-servers -p:AvaloniaPreviewerTelemetryOptOut=true --artifacts-path artifacts/compact-ui-build`: erfolgreich, 0 Warnungen / 0 Fehler.
  - `dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --artifacts-path artifacts/compact-ui-build --logger trx --results-directory artifacts/test-results/m4-review-final`: **210/210 bestanden**, keine übersprungen (Domain 8, Rules 57, ImportExport 13, Application 38, Storage 69, StudentApp/UI-Smoke 25).
  - `pwsh -NoProfile -File scripts/Test-Schemas.ps1`: 7/7 erfolgreich. `git diff --check`: erfolgreich. Keine neuen Abhängigkeiten.

### Netzwechsel, Planungsdruck und vereinfachte Streichung (28.10.2026)

- Transport des aktiven Jahrgangs als versioniertes ZIP (.gsm-transfer, Manifest v1 und konsistente SQLite-Backupkopie). Neue lokale Zielbestände statt Überschreiben; unbekannte Versionen, beschädigte Prüfsummen, zusätzliche Archiveinträge und Änderungen nach Vorschau werden abgewiesen. Laufendes Einsammeln/offene Konflikte blockieren Export. Schaltflächen vor Verteilung und nach Einsammeln sowie Öffnen bei Ersteinrichtung/Einstellungen ergänzt.
- Planungsdruck rendert den zuletzt angezeigten Snapshot mit dessen Abgängerfilter als PDF; Öffnen im lokalen PDF-Programm für Papierdruck. Gruppengröße und Gruppenschätzungen aus UI entfernt, kompatible historische Schemafelder erhalten. Schulische Festangebote in Streichungsauswahl ausgeblendet; keine neue Rechtsregel.
- Streichungsvorschau als kompakte Tabelle mit sämtlichen betroffenen Schülern und verbleibenden Wahlen. Automatische einzelne Nachwahl-PDFs nach bestätigter Entscheidung, Wiederholung aus gespeicherten Streichungs-/Wiederherstellungsrevisionen möglich. Dateischreibfehler werden getrennt von der bereits gespeicherten Entscheidung angezeigt. Nachwahlstatus folgt weiterhin dem Regelkern, Bögen fordern Rückmeldung an.
- Prüfverlauf: erster Build 0 Fehler/0 Warnungen. Erste Gesamtsuite mit Transport-/UI-Tests 213/213 bestanden. Danach zusätzliche Prüfung auf nachträglich geänderte Transportvorschau/unerwartete Archiveinträge, präzisere PDF-Unterschriftsfelder und kompaktere UI. Kein fehlgeschlagener Testlauf in diesem Entwicklungsschritt.
- Abschluss: Locked-Restore erfolgreich (erweiterter Zugriff auf lokale NuGet-Konfiguration), Release-Build 0 Warnungen / 0 Fehler. Gesamtsuite **214/214 bestanden**, keine übersprungen (Domain 8, Rules 57, Storage 72, ImportExport 13, Application 39, UI/Integration 25). Befehle dotnet restore/build/test GSM-Kurswahl.sln mit --artifacts-path artifacts/compact-ui-build, Release; letzter Test --no-build --no-restore. TRX unter artifacts/test-results/transfer-complete. Schema-Prüfung 8/8 und git diff --check erfolgreich; Git meldet nur Zeilenendenkonvertierung.
- Tests decken Hin-/Rücktransport mit unveränderter Ursprungshistorie, Erhalt von Planungsständen und Nachwahlvermerken, Überschreibschutz, Prüfsumme, unbekannte Transportversion, geänderte Vorschau und unerwartete Archiveinträge ab. UI-Smoke prüft beide Netzwechsel-Schaltflächen, feste Angebotsauswahl, Simulation ohne Speicherung, explizite Übernahme und tatsächlich angelegte Nachwahl-PDFs.
- Renderbilder unter artifacts/ui-transfer-verified visuell geprüft, einschließlich kompakter Vorschau mit zehn betroffenen Schülern. PDFs unter artifacts/ui-transfer-final per Poppler gerendert und visuell geprüft: jeweils eine A4-Seite, klare Halbjahreszahlen bzw. verbleibende Wahl mit Schreib-/Unterschriftsfeldern. Byteidentische synthetische Vorschauen unter output/pdf/Beispiel-Nachwahlbogen.pdf und Beispiel-Wahlzahlen.pdf (nicht für Git vorgesehen). Physischer Druck und echter Schulnetzwechsel nicht durchgeführt; lokaler Roundtrip und PDF-Ausgabe geprüft.
- Bestehende Demo-Bestände bleiben erhalten. Keine neuen Paketabhängigkeiten; keine Zertifikatspflicht. Kein Commit/Push in diesem Schritt.

### Begründungsfeld bei Angebotsstreichung entfernt (28.10.2026)

- Nutzerentscheidung: Keine manuelle Begründung bei Kursstreichungen. Feld vollständig entfernt; Vorschau und Bestätigungshäkchen genügen. Bei fehlendem Häkchen wird diese Voraussetzung nun ausdrücklich benannt. Der Revisionsanlass wird automatisch aus gestrichenen Angebotskennungen erzeugt; bisherige Historie bleibt erhalten.
- Der zunächst begonnene Ansatz mit Pflichtfeldhinweis wurde während der Bearbeitung auf Nutzerwunsch verworfen. Dessen bereits gestarteter Testlauf meldete 213 bestanden / 1 UI-Fehler (Sichtbarkeitsassertion des inzwischen entfernten Feldhinweises); dies ist kein Erfolgsnachweis des finalen Stands.
- Abschließend Locked-Restore erfolgreich; Release-Build 0 Warnungen / 0 Fehler. Gesamtsuite 214/214 bestanden, keine übersprungen (8 Domain, 57 Rules, 72 Storage, 13 ImportExport, 39 Application, 25 UI/Integration). UI prüft fehlendes Begründungsfeld, Übernahme erst mit Häkchen und erzeugte Nachwahl-PDFs. Storage prüft Übernahme mit leerer Texteingabe und automatisch gespeicherten Anlass.
- Befehle dotnet restore/build/test GSM-Kurswahl.sln mit --artifacts-path artifacts/reason-ui-build, Release; Restore --locked-mode --configfile NuGet.Config; finaler Test --no-build --no-restore. TRX unter artifacts/test-results/no-reason-final. Schema-Prüfung 8/8 erfolgreich, git diff --check erfolgreich (nur Zeilenendenhinweise).
- Teststarter nutzt den frisch gebauten separaten Ausgabeordner, damit eine laufende frühere App nicht überschrieben werden muss. Beide vorhandenen Demo-Bestände bleiben erhalten. Kein Commit/Push in diesem Schritt.

### Gemeinsame Nachwahlbögen als PDF und Word (28.10.2026)

- Alle betroffenen Schüler werden in genau einer PDF und einer bearbeitbaren DOCX gesammelt, nach Klasse/Name/ID sortiert. Jeder Schüler beginnt auf einer neuen Seite; Inhalt wie zuvor mit verbleibender Wahl, Nachwahlfeld und Unterschrift. Keine Office-Installation zur Erstellung erforderlich: DOCX als OOXML-ZIP über das Framework, PDF weiterhin SkiaSharp. Keine neuen Pakete.
- UI-Integrationstest mit zwei betroffenen Schülern erweitert: genau eine PDF, genau eine DOCX, beide Namen, zwei Wahlentabellen, ein expliziter Seitenwechsel und neue Revisionen für beide Personen. Im 80-Schüler-Test erzeugt BIO-LF-BILI-Streichung einen gemeinsamen Satz für zehn Schüler.
- Locked-Restore erfolgreich; Release-Build 0 Warnungen / 0 Fehler. Gesamtsuite 214/214 erfolgreich, keine übersprungen (8 Domain, 57 Rules, 72 Storage, 13 ImportExport, 39 Application, 25 UI/Integration). Berichte artifacts/test-results/combined, Ausgabeordner artifacts/combined-print-build, UI-Smoke enthalten. Schema-Prüfung 8/8 und git diff --check erfolgreich.
- PDF mit Poppler gerendert: 10 A4-Seiten, jede visuell geprüft. DOCX-Standardrenderer render_docx.py konnte mangels gebündeltem LibreOffice nicht laufen. Word-COM war zunächst in der Sandbox wegen fehlender Anmeldesitzung blockiert, mit erweitertem Zugriff anschließend erfolgreich: unsichtbare eigene Word-Instanz, schreibgeschütztes Öffnen, PDF-Export, danach geschlossen. Export ebenfalls 10 A4-Seiten; sämtliche Seitenbilder visuell geprüft. Keine Layoutfehler festgestellt.
- Synthetische Downloads unter output/pdf/Nachwahlboegen-Gesamt.pdf und output/documents/Nachwahlboegen-Gesamt.docx; generierte Dokumente von Git ausgeschlossen. Teststarter auf aktuellen Ausgabeordner umgestellt, bestehende Testbestände erhalten. Kein Commit/Push in diesem Schritt.

### M5 – erster Funktionsschritt (28.10.2026)

- Allgemeiner CSV/XLSX-Abgleich mit vollständiger Vorschau, Einzelbestätigung, Herkunft und append-only Revision; Exporte, revisions-/stammdatengebundene manuelle Übertragungsvermerke mit explizitem DB-Schema 3 sowie Backup-Roundtrip ergänzt.
- Erster manueller Release-Build ohne AVALONIA_TELEMETRY_OPTOUT scheiterte mit zwei MSB4018-Fehlern beim Zugriff der AvaloniaStatsTask auf buildtasks.log. Mit gesetztem AVALONIA_TELEMETRY_OPTOUT=1 erneut gebaut: 0 Warnungen, 0 Fehler. Keine Quellcode-Kompilierfehler.
- Gezielter Lauf: dotnet test GSM-Kurswahl.sln -c Release --no-restore --filter 'FullyQualifiedName~ChoiceTableTests|FullyQualifiedName~Reconciliation' --logger trx --results-directory artifacts/test-results/m5-focused. 15/15 bestanden (10 ImportExport, 4 Storage/Abgleich, 1 Headless-UI), keine übersprungen. Andere Projekte haben erwartungsgemäß keine zum Filter passenden Tests.
- Abgedeckt: CSV/XLSX-Roundtrip, Semestergenauigkeit, falsche/fehlende Felder und Dubletten, Formelneutralisierung, Neuzugang/fehlende Person/Abweichung, Bestätigungspflicht, Herkunft/Historie, idempotenter Wiederimport, geänderte Stammdaten/Katalog/Revision, explizite Schemaaktivierung, Vermerke einschließlich Wiederherstellung, UI-Navigation und dokumentierte manuelle Eingabe ohne Wahländerung.

### M5 – Abschlussprüfung und Erprobungspaket (28.10.2026)

- `scripts/Test.ps1 -LockedMode` zunächst beim Restore durch Sandbox-Leseverbot auf die Benutzer-NuGet.Config blockiert. Mit erweitertem Zugriff erfolgreich: Restore, Release-Build (0 Warnungen/0 Fehler), 229/229 Tests und 8/8 Schemaprüfungen.
- Finaler isolierter Lauf nach Ergänzung der UI-Bilder und Korrektur des Dubletten-Testfalls: `dotnet restore/build/test GSM-Kurswahl.sln --artifacts-path artifacts/m5-ui-build` (Restore --locked-mode --configfile NuGet.Config, Build Release --no-restore, Tests Release --no-build --no-restore). **229/229 bestanden**, keine übersprungen: Domain 8, Rules 57, ImportExport 23, Application 39, Storage 76, UI/Integration 26. TRX: artifacts/test-results/m5-final. Danach Test-Schemas.ps1: 8/8 erfolgreich. UI-Bild artifacts/ui-m5/teacher-m5.png visuell geprüft: Navigation, Hinweise, Filter und Export-/Vermerksbedienung lesbar; weitere Inhalte scrollbar. Keine native DPI-/Screenreader-/Schulgeräteprüfung behauptet.
- Erster Selbstenthaltend-Publish scheiterte mit NU1004, weil die regulären Lockdateien keine Runtime-ID enthielten. Separate packages.win-x64.lock.json für alle acht ausgelieferten Projekte erzeugt; reguläre Lockdateien unverändert. Wiederholung mit scripts/Publish-Release.ps1 -SelfContained erfolgreich für beide Apps, .NET-Runtime 10.0.14 enthalten. Erste ZIP-Ausgabe artifacts/releases/M5-demo-20261028-191126-a21ef2cb.zip, SHA-256 67F1171506C384DD3C37FA9FF3A9BABFC4EC53817E928A58CF099B6A86475A80. Alle 481 im Manifest enthaltenen Dateiprüfsummen bestätigt.
- NuGet-Sicherheitsaudit aller 14 Projekte einschließlich transitiver Pakete erfolgreich, laut abgefragtem NuGet.org keine bekannten anfälligen Pakete. Eigene Lizenz und vollständige Drittanbieter-Notices bleiben Voraussetzung für endgültige Auslieferung.
- 80-Schüler-Teststarter auf artifacts/m5-ui-build umgestellt, bestehende Demo-Verwaltungsbestände erhalten. Kein Deployment, Commit oder Push. Echte ASV-Musterdatei, Schulnetz-Pilot und fachliche Abnahme weiterhin offen.
- Zusätzlicher Paket-Smoke: veröffentlichte selbstenthaltende Lehrer-EXE mit --prepare-demo-80 in isoliertem artifacts/release-smoke-Verzeichnis gestartet, Exitcode 0 und synthetischer 80er-Bestand erzeugt. Dies prüft Paketstart/SQLite/Katalog und ersetzt keinen nativen UI-Test auf einem Schulgerät. git diff --check nach Entfernen zusätzlicher Leerzeilen erfolgreich.
- Finales Paket mit aktualisierten Dokumenten und expliziter Runtime-Pinnung: artifacts/releases/M5-demo-20261028-191534-e417d469.zip; SHA-256 CE83C19FC898E8366857212D1F7512643E4793FBEED5E1F747D6091D2C73ACAF. ZIP-Inhalt vollständig gegen SHA256.json geprüft: 481 Dateien korrekt.


### Usability-Überarbeitung und direkte Schema-3-Initialisierung (28.10.2026)

- Vollständige Überarbeitung der Benutzeroberfläche für Gelegenheitsnutzer (Oberstufenberatung, jährliche Nutzung) gemäß Nutzerfreigabe („Alte Datenbanken gibt es nicht mehr. Brauchen also nicht beachtet zu werden“):
  - Direkte Schema-3-Initialisierung neuer SQLite-Verwaltungsdatenbanken mit allen Tabellen und Triggern; Entfernung technischer Schema-Wechsel-Schaltflächen in Kursplanung und Abgleich.
  - Split-View im Personen-Editor: Schülerliste links und vollständiger Editor rechts auf voller Höhe mit strukturierten Oberflächenkarten (Konto/Wahlordner, Vorbildung mit Tooltips/Dropdowns, Prüfhinweise & Nachwahl, Kurswahl durch die Beratung ändern, vollständige Historie).
  - Durchgängig verständliche deutsche Bezeichnungen, Tooltips und Formatierungshelfer für Enums (`ReviewStatus`, `Knowledge`, `DepartureStatus`, `CourseType`, `OfferStatus`, `RevisionSource`, `ActorRole`, `ReconciliationKind`).
  - Mündliche Prüfungsfächer als Auswahllisten (`ComboBox`) mit Klarnamen statt Freitext-Kürzeln.
  - Explizite deutsche Feldbezeichnungen und Tooltips beim externen Kurswahlabgleich (`Schüler-ID *`, `Abiturjahrgang *`, `Fachkürzel *`, `Kursart *`, `Variante *`, `Halbjahr *`).
  - Lesehilfe/Legende und Spaltenkopf-Tooltips in der Kursplanungsmatrix.
  - Nachfolge-Aktionskarte nach Kursstreichungen mit direkten Aktionen zum Öffnen/Erstellen der Nachwahlbögen und Wechsel zur Profilverteilung.
  - Farbcodiertes Statusbanner (Erfolg grün, Fehler rot, neutral grau) für sofortige visuelle Rückmeldung.
- Zwischenbefunde und Korrekturen:
  - `TeacherPlanning.cs`: `ShowProfiles` korrigiert zu `() => ShowPage("profiles")`.
  - `TeacherWorkspaceShell.cs`: Mehrdeutige Schaltfläche "Abgleich / Abschluss" im Dashboard zu "Abgleich / Abschluss öffnen" präzisiert, um Eindeutigkeit mit der Hauptnavigation zu sichern.
  - `TeacherWindow.cs`: TabControl im Editor durch direkte strukturierte Oberflächenkarten (`Surface`) ersetzt, wodurch die Controls im logischen Avalonia-Baum eindeutig und ohne doppelte Traversierung zugänglich sind.
  - Tests auf Schema-3-Standard aktualisiert (`PlanningTests.cs`, `ReconciliationTests.cs`, `PlanningWindowTests.cs`, `ReconciliationWindowTests.cs`).
- Testergebnisse:
  - Release-Build: **0 Warnungen, 0 Fehler**.
  - Gesamte Testsuite: **229/229 bestanden**, keine übersprungen:
    - Domain: 8 / 8
    - Application: 39 / 39
    - Rules: 57 / 57
    - ImportExport: 23 / 23
    - Storage: 76 / 76
    - StudentApp (inkl. Avalonia-Headless-UI): 26 / 26
  - Ausschließlich synthetische Personen und Kennungen verwendet.


### Nahtloses Schema-Upgrade für bestehende lokale Testbestände (28.10.2026)

- Fehleranalyse nach Nutzertest: Beim Start von `Testversion-80-Schueler.cmd` bzw. `Testversion-GK-Geo.cmd` erschien die Fehlermeldung „Unbekannte Verwaltungsschemaversion; keine Migration.“, weil die vorhandenen lokalen Testdatenbanken in `artifacts/demo-80` und `artifacts/demo-80-gk-geo` auf Schema-Version 2 lagen.
- Behebung:
  - `SqliteAdministrationStore.cs` akzeptiert neben 0 und 3 nun auch Schemaversion 1 und 2 und aktualisiert diese beim Öffnen atomar und abwärtskompatibel auf Schema 3 (`transfer_receipts` anlegen, `PRAGMA user_version = 3`), ohne bestehende Daten zu verändern.
  - `WorkspaceTransfer.cs` akzeptiert beim Transportimport nun ebenfalls Datenbanken der Versionen 1, 2 und 3.
  - Neuer automatisierter Regressionstest `Version2DatabaseUpgradesSeamlesslyToVersion3AndPreservesRecords` in `TeacherWorkspaceTests.cs`.
- Testergebnisse:
  - Release-Build: **0 Warnungen, 0 Fehler**.
  - Gesamte Testsuite: **230/230 bestanden** (Storage: 77/77, alle 6 Testprojekte grün).
  - Testbestände `artifacts/demo-80` und `artifacts/demo-80-gk-geo` öffnen fehlerfrei.

### Schüler-Jahresbelegung und automatische BF-/LF-Umwahl (28.10.2026)

- Fehlerkorrektur `AGVO-SEMESTERS` im gemeinsamen Regelkern: GK/GEO sind nicht mehr pauschal ausgenommen. Regulär HJ 1+2 bzw. HJ 3+4 zusammen; bei LF Wirtschaft nur GK im ersten und GEO im zweiten Schuljahr einzeln. GK HJ 1+4 wird beanstandet. Bereits dokumentierte Sportersatz-Ausnahmen und `needs_review` bleiben erhalten. Keine Schema-/Paketänderung.
- Schüleroberfläche schaltet reguläre Jahrespaare gemeinsam. Ein bisheriges BF wird bei Wahl desselben Fachs als LF automatisch ersetzt (auch über Varianten hinweg), mit sichtbarem Hinweis. Die mündliche Planung wird bereinigt; nach erneutem Durchlaufen werden fehlende Pflichthalbjahre ergänzt. Andere gewählte Fächer bleiben erhalten.
- Regressionen: alle 15 nichtleeren Halbjahresteilmengen je GK/GEO/regulärem BF mit und ohne Wirtschaft (90 Kombinationen innerhalb von 6 Theory-Fällen), unveränderte fehlerhafte Bestandswahl, BF->LF inklusive bilingualer Vorbelegung, verweigertes LF ohne Verlust des BF, weiter gesperrte umgekehrte Doppelbelegung, fertige Wahl -> LF-Wechsel -> neue mündliche Planung/Pflichtbelegung -> Speichern/Laden. Avalonia-Headless prüft beide vollständigen Bedienabläufe über tatsächliche Controls, Jahrespaare und zulässige Wirtschaftseinzelkurse.
- Zwischenbefunde:
  - Erster Locked-Restore in der Sandbox wegen Lesesperre auf die lokale Benutzer-NuGet.Config fehlgeschlagen. Mit erweitertem Zugriff bei unverändertem Befehl erfolgreich; Lockdateien unverändert.
  - Erster Release-Build erfolgreich (0 Warnungen/0 Fehler). Erster Gesamttestlauf: 214 erfolgreiche Tests in Domain/Rules/Application/Storage/ImportExport; UI-Teil blieb ohne Abschluss und wurde abgebrochen. Kein erfolgreicher Gesamtlauf daraus behauptet. Der neue UI-Test hatte am Ende einen ungespeicherten Entwurf; Bereinigung auf explizites Speichern vor dem Schließen geändert, um den Bestätigungsdialog zu vermeiden.
  - Danach gezielter UI-Lauf mit Renderausgabe und 60-Sekunden-Hangüberwachung: 2/2 bestanden. Bilder `artifacts/ui-student-choice-fix/student-lf-change.png` und `student-year-pairs.png` visuell geprüft: Hinweis und Weiter-Schaltfläche sichtbar, GK HJ 3+4 gekoppelt, GK HJ 2/GEO HJ 4 einzeln, reguläres BF in Jahrespaaren. Keine native Schulgeräte-/DPI-Prüfung behauptet.
- Abschluss im isolierten Ausgabeordner `artifacts/student-choice-fix-build`:
  - `dotnet restore GSM-Kurswahl.sln --configfile NuGet.Config --locked-mode --artifacts-path artifacts/student-choice-fix-build -m:1 --disable-build-servers`: erfolgreich.
  - `dotnet build GSM-Kurswahl.sln -c Release --no-restore --artifacts-path artifacts/student-choice-fix-build -m:1 -p:UseSharedCompilation=false --disable-build-servers` mit `AVALONIA_TELEMETRY_OPTOUT=1`: erfolgreich, **0 Warnungen / 0 Fehler**.
  - `dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --artifacts-path artifacts/student-choice-fix-build --logger trx --results-directory artifacts/test-results/student-choice-fix-final --blame-hang-timeout 60s --blame-hang-dump-type none`: **241/241 bestanden**, keine übersprungen (Domain 8, Rules 64, Application 42, Storage 77, ImportExport 23, UI/Integration 27). Alle UI-Tests abgeschlossen; kein Timeout.
  - `pwsh -NoProfile -File scripts/Test-Schemas.ps1`: **8/8 erfolgreich**. Keine neuen Abhängigkeiten, Paketversionen und Lockdateien unverändert.
  - Anschließend denselben Release-Build ohne `--artifacts-path` für die vorhandenen lokalen Teststarter ausgeführt: erfolgreich, 0 Warnungen / 0 Fehler. `Testversion-Schueler-Blank.cmd` verwendet damit die aktualisierte Schüler-App; vorhandene Demo-Wahlen wurden nicht zurückgesetzt. `git diff --check` erfolgreich (nur Hinweise zur LF-/CRLF-Konvertierung).

### Fortführung Religion und Wechselhinweis (28.10.2026)

- `AGVO-RELIGION` verlangte bisher selbst bei bestätigter Fortführung des Klasse-10-Fachs ein zusätzliches, oft unbekanntes Zustimmungsfeld. Fortführung KR/ER/ETH erzeugt deswegen keinen pauschalen Klärfall mehr. Explizites Nein und fehlende/verworfen vertrauenswürdige Vorbildung bleiben Klärfälle. Keine Zulassungsentscheidung aus Schüler-JSON.
- Wechsel KR/ER -> ETH zeigt den gewünschten Antrag-/Feststellungsprüfungshinweis direkt in Pflichtbereichen und Überblick. Zusatz erläutert, dass daraus noch keine mündliche Zulassung folgt; dokumentierte KOE-Einschränkung bleibt offen. Bestehende mündliche Kandidatenprüfung unverändert. Keine Schema-/Paketänderungen.
- Regressionstests: Fortführung KR/ER/ETH bei unbekanntem Zusatzfeld; ausdrückliches Nein; fehlendes Profil; bestehender Wechseltest mit gewünschtem Wortlaut. Headless-UI prüft fehlendes KR-Klärbadge und direkt sichtbaren Wechselhinweis in beiden Schritten.
- Locked-Restore im isolierten Ausgabeordner `artifacts/religion-fix-build` erfolgreich; Release-Build einschließlich abschließender Aktualisierung erfolgreich, 0 Warnungen / 0 Fehler. Schemaprüfung 8/8 und `git diff --check` erfolgreich. Teststarter auf diese Ausgabe umgestellt; bestehender Schülerentwurf und laufende App nicht verändert.
- Abschluss: `dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --artifacts-path artifacts/religion-fix-build --logger trx --results-directory artifacts/test-results/religion-fix-final --blame-hang-timeout 60s --blame-hang-dump-type none`: **245/245 bestanden**, keine übersprungen (Domain 8, Rules 67, Application 42, ImportExport 23, Storage 77, UI/Integration 28). Keine fehlgeschlagenen Tests in diesem Schritt.

### Verständliche Schüler-Prüfhinweise (28.10.2026)

- Veralteten Verweis auf Prüfung/Abgabe „in späteren Ausbaustufen“ entfernt. Überblick benennt vorhandene automatische Prüfung, Speichern/PDF-Export, weiterhin fehlende verbindliche Demo-Abgabe und ausstehende fachliche Jahrgangsfreigabe korrekt. Zuständigkeit für persönliche Sonderfälle, Prüfungsvoraussetzungen und Anrechnung: Oberstufenberatung. Schüler ergänzen fehlende Kurse und besprechen Rückfragen mit ihr.
- Technisches `needs_review` in der Schülerdarstellung verständlich übersetzt, Statusbanner und Kombinationsliste vereinfacht; interner Status und Sperren unverändert. Pauschale Aufzählung von Sportersatz/Religion unter offenen Aufgaben durch Verweis auf konkrete Prüfhinweise ersetzt. Fenstertitel ohne veraltetes M2-Etikett. UI-Smoke erweitert um neue Erklärung und Abwesenheit der alten Begriffe im Überblick.
- Locked-Restore erfolgreich. Erster Standard-Build wegen laufender Schüler-App beim Kopieren gesperrter DLL fehlgeschlagen (MSB3026/MSB3027/MSB3021, 10 Warnungen / 2 Fehler). Laufende App und Entwurf erhalten. Release-Build stattdessen in `artifacts/student-choice-fix-build`: erfolgreich, 0 Warnungen / 0 Fehler. Schüler-Teststarter bevorzugt diese neue Ausgabe; Änderung beim nächsten Start sichtbar. Schemaprüfung 8/8 erfolgreich. Keine Paket-/Schemaänderungen.
- Abschließender Gesamtlauf mit `dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --artifacts-path artifacts/student-choice-fix-build --logger trx --results-directory artifacts/test-results/student-texts-final --blame-hang-timeout 60s --blame-hang-dump-type none`: **241/241 bestanden**, keine übersprungen (Domain 8, Rules 64, Application 42, Storage 77, ImportExport 23, UI/Integration 27). PowerShell-Parserprüfung des Teststarters und `git diff --check` erfolgreich. Keine neue native Sichtprüfung behauptet.

### Gegenseitiger Ausschluss Religion/Ethik (29.10.2026)

- Nutzerfestlegung als gemeinsame Regel GSM-RELIGION-EXCLUSIVE (invalid, m2-2) dokumentiert und implementiert: ETH/KR/ER dürfen sich in keinem Halbjahr überschneiden. Beide Anwendungen validieren denselben Regelkern. Bestandswahlen werden beanstandet, nicht still korrigiert; zeitlich getrennte Wechsel bleiben anderen Pflicht-/Prüfungsregeln unterworfen.
- Konkurrierende Religion-/Ethikangebote erscheinen nicht mehr als weitere Kurse. Direkte Auswahl kollidierender Belegungen wird ohne Wahlverlust abgewiesen. Bewusster Wechsel im Pflichtbereich ersetzt die bisherige Antwort; geänderte mündliche Planung ersetzt kollidierende alte Religion-/Ethikbelegung mit sichtbarem Hinweis. Mündliche Zweierkombinationen dieser Fächer ausgeschlossen.
- Regressionen: alle drei Fachpaarungen mit überlappenden und getrennten Halbjahren, Status/Betroffene/Bestandserhalt, unzulässige mündliche Kombination, Zusatzwahl ohne Veränderung, Umwahl über mündliche Planung sowie erweiterter Headless-UI-Test auf fehlende Zusatzoption und ersetzte alte Auswahl.
- Locked-Restore erfolgreich. Release-Build in artifacts/religion-exclusive-build erfolgreich, 0 Warnungen / 0 Fehler. Teststarter verwendet diese neue Ausgabe. Schemaprüfung 8/8 und git diff --check erfolgreich. Keine neuen Pakete, unveränderte Lockdateien, keine Schemaänderung.
- Abschluss: dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --artifacts-path artifacts/religion-exclusive-build --logger trx --results-directory artifacts/test-results/religion-exclusive-final --blame-hang-timeout 60s --blame-hang-dump-type none: **253/253 bestanden**, keine übersprungen (Domain 8, Rules 74, Application 43, ImportExport 23, Storage 77, UI/Integration 28). Keine fehlgeschlagenen Tests in diesem Schritt. PowerShell-Parserprüfung des Teststarters erfolgreich.

### Dauerhafte Warnung über 37 Wochenstunden (29.10.2026)

- Nutzerfestlegung in Regel GSM-HOURS-UPPER dokumentiert (m2-2, needs_review): strikt mehr als 37 Wochenstunden je Halbjahr erzeugt einen Genehmigungshinweis mit tatsächlicher Stundenzahl und betroffenen Halbjahren. Kein Durchschnittswert als Auslöser, keine automatische Ablehnung oder Wahländerung.
- Schüler-App zeigt den Hinweis fest im Fußbereich außerhalb der Scrollbereiche in allen Schritten. Keine Ausblendschaltfläche; nach Reduktion auf höchstens 37 in allen Halbjahren verschwindet er. Regelkern wird gemeinsam von beiden Programmen verwendet.
- Neue Regeltests für 36/37/38 Stunden und betroffene Halbjahre; neuer Headless-UI-Test für laufende Aktualisierung, Schrittwechsel, Sichtbarkeit im Fenster, Speichern/Laden und Verschwinden nach Abwahl. Keine neuen Pakete oder Schemaänderungen. Schüler-Teststarter auf artifacts/hours-warning-build umgestellt; vorhandene Entwürfe bleiben erhalten.
- Locked-Restore erfolgreich; Schemaprüfung 8/8 und git diff --check erfolgreich.
- Erster Gesamtlauf: 256 bestanden / 1 neuer UI-Test fehlgeschlagen. Der synthetische Testaufbau hatte GK nur in HJ 1+2 und erreichte in HJ 3+4 tatsächlich 35 statt der erwarteten 38 Stunden. Testaufbau um die beabsichtigten GK-Wahlkurse HJ 3+4 ergänzt; keine Änderung der Warnlogik nötig. Release-Build danach erneut erfolgreich, 0 Warnungen / 0 Fehler.
- Gezielter UI-Test nach Korrektur 1/1 bestanden. Abschließender Gesamtlauf mit dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --artifacts-path artifacts/hours-warning-build --logger trx --results-directory artifacts/test-results/hours-warning-complete --blame-hang-timeout 60s --blame-hang-dump-type none: **257/257 bestanden**, keine übersprungen (Domain 8, Rules 77, Application 43, ImportExport 23, Storage 77, UI/Integration 29). Release-Build 0 Warnungen / 0 Fehler; Teststarter-Syntaxprüfung und abschließendes git diff --check erfolgreich.

### Abschlussprüfung vor Commit und Push (29.10.2026)

- `pwsh -NoProfile -File scripts/Test.ps1 -LockedMode` zunächst beim Restore durch den gesperrten Zugriff auf die lokale Benutzer-NuGet-Konfiguration abgebrochen. Wiederholung mit erweitertem Zugriff erfolgreich.
- Locked-Restore erfolgreich; Release-Build 0 Warnungen / 0 Fehler. Gesamte Testsuite **257/257 bestanden**, keine übersprungen: Domain 8, Rules 77, Application 43, ImportExport 23, Storage 77 und UI/Integration einschließlich Headless-Smoke 29. TRX unter `artifacts/test-results/final`.
- Schemaprüfung 8/8 erfolgreich. PowerShell-Parserprüfung von `scripts/Start-DemoStudent.ps1` und `git diff --check` erfolgreich; Git meldet ausschließlich Zeilenendenhinweise. Keine erneute native Sichtprüfung und kein Schulnetz-Pilot durchgeführt.

### Fehlersuche Schülerwahl mit Zufalls-Klickfolgen und Rück-/Vorweg (29.10.2026)

- Neue Headless-UI-Tests `StudentMonkeyTests` (zufällige Klickfolgen über die echten Steuerelemente: Schritt vor/zurück, Schrittleiste, Plan-Sprünge, An-/Abwahl, Speichern/Laden; zusätzlich fertige Wahl → Rücksprung nach Schritt 1/2 → LF- bzw. mündliche Änderung → erneut bis Schritt 5, sechsmal je Lauf) mit Invariantenprüfung nach jeder Aktion; vier synthetische Profile (fehlend, geprüft, Sportbefreiung HJ 2, Klasse 10 KR). Deterministische Reproduktionen in `StudentRevisionReproTests`. Erkundungsmodus über `GSM_MONKEY_EXPLORE=1`, Bericht über `GSM_MONKEY_REPORT`.
- Bestätigte Befunde (Reproduktionstests schlagen derzeit erwartungsgemäß fehl):
  1. Pflichtbereiche, Frage „Zweite Fremdsprache oder zweite Naturwissenschaft“ (analog weitere Fragen): Sind mehrere passende Kurse belegt, zeigt die Karte nur einen als gewählt; ein bereits belegter Kurs erscheint ungewählt. Ein Klick darauf entfernt ohne Hinweis den anderen Kurs (Beispiel: F-BF und CH-BF belegt, Klick auf CH-BF löscht F-BF).
  2. Schritt 1: Kurzes An- und Abhaken eines LF, dessen Fach als BF belegt ist, entfernt das BF dauerhaft. Der Hinweis „… ist jetzt Leistungsfach“ bleibt danach stehen, obwohl das LF wieder abgewählt ist. Beim erneuten Vorwärtsgehen fällt der Verlust nicht auf, wenn die betroffene Frage anderweitig erfüllt ist.
  3. Schritt 1 erlaubt mehr als drei LF; jedes zusätzlich angehakte LF entfernt sein BF (siehe 2).
  4. Geladener Entwurf mit unbekannter Angebotskennung eines bekannten Fachs: `Tasks` wirft `InvalidOperationException` („Sequence contains no elements“); im Fenster wird diese technische Meldung angezeigt und der folgende Refresh bricht ungefangen ab.
  5. Hinweis (keine Regelverletzung über UI): Mit Sportbefreiung bietet Schritt 4 Einzelhalbjahre für alle Fächer an; mehrere Einzelhalbjahre führen erst nachträglich zu `AGVO-SEMESTERS`.
- Ohne Befund: keine ungefangenen Ausnahmen bei Zufallsfolgen, keine Doppelbelegungen, keine Religion/Ethik-Überschneidung, keine fehlenden Pflichtkurse ab Schritt 3, mündliche Planung ab Schritt 3 stets gültig und in den möglichen Kombinationen enthalten, Stundenwarnung konsistent, Speichern/Laden ohne Konflikt.
- Gesamtlauf `dotnet test GSM-Kurswahl.sln -c Release --artifacts-path artifacts/monkey-build`: Domain 8/8, Rules 77/77, Application 43/43, ImportExport 23/23, Storage 77/77 bestanden; UI/Integration 29 bestanden / **13 fehlgeschlagen** (alle 13 sind die neuen Befund-Tests; alle 29 bisherigen UI-Tests bestanden). Keine Produktivcodeänderung in diesem Schritt.

### Behebung der Rück-/Vorweg-Befunde (29.10.2026)

- Befunde 1–4 aus dem vorherigen Abschnitt behoben: Fragekarten zeigen alle über HJ 1–4 belegten passenden Kurse als gewählt; Ersetzen per Optionsfeld nur bei genau einem ersetzbaren Kurs, sonst Kontrollkästchen ohne implizites Löschen; erneute Wahl eines belegten Kurses ist wirkungslos; misslungene Ersetzung wird vollständig zurückgenommen. Höchstens drei LF (weitere Kästchen deaktiviert, Session lehnt ab). Abwahl eines LF stellt das zuvor ersetzte BF (und ggf. mündliche Planung) in derselben Sitzung wieder her; veralteter Wechselhinweis entfällt. Unbekannte Angebotskennungen beantworten keine Frage mehr und führen nicht zum Absturz. Befund 5 bleibt als offener fachlicher Punkt dokumentiert.
- Reproduktionstests in `StudentRevisionRegressionTests` (6 Tests) als Regressionstests umgestellt; `StudentMonkeyTests` laufen im strengen Modus (4 Profile × Zufallsfolgen, 4 Profile × Rück-/Vorweg) ohne Befund. Testfenster werden vor dem Schließen gespeichert, da sonst der Bestätigungsdialog den Headless-Lauf blockiert (zunächst als Hänger beobachtet, kein Produktfehler).
- `pwsh -NoProfile -File scripts/Test.ps1 -LockedMode`: Locked-Restore erfolgreich, Release-Build 0 Warnungen / 0 Fehler, gesamte Suite **271/271 bestanden**, keine übersprungen (Domain 8, ImportExport 23, Application 43, Rules 77, Storage 77, UI/Integration einschließlich Headless-Smoke 43; UI-Dauer 2 m 57 s). Schemaprüfung 8/8 erfolgreich. Keine Paket-, Lockdatei- oder Schemaänderung. Keine native Sichtprüfung durchgeführt.

### Kein Einzelhalbjahres-Sportersatz bei der Kurswahl (29.10.2026)

- Nutzerfestlegung: Wer bei der Kurswahl in Klasse 10 bereits von einer Sportbefreiung weiß, wählt Ersatzkurse als ganze Schuljahre. Die KOE-Einzelhalbjahresregelung bleibt dem späteren Ausfall in der Kursstufe vorbehalten (nicht modelliert). `AcademicRules.SemesterSelectionUnits` bietet bei Sportbefreiung keine Einzelhalbjahre mehr an; `AGVO-SEMESTERS` (invalid, Quelle ergänzt, Version `m2-2` unverändert) beanstandet jedes Einzelhalbjahr außer GK/GEO bei LF Wirtschaft. Hinweistext `AGVO-SPORT` angepasst. Gemeinsamer Regelkern, gilt in beiden Programmen; vorhandene Entwürfe werden nicht still korrigiert.
- Tests: Regeltest auf Einzelhalbjahr = Beanstandung, ganzes Schuljahr = zulässig und nur Schuljahrespaare als Auswahleinheiten umgestellt; neuer Headless-UI-Test mit Sportbefreiung HJ 2 (SP HJ 1/3/4 gesetzt, GK nur als HJ 3+4 erweiterbar); Zufalls-Klicktest prüft `AGVO-SEMESTERS` ab Schritt 3 jetzt auch für das Profil mit Sportbefreiung.
- `pwsh -NoProfile -File scripts/Test.ps1 -LockedMode`: Locked-Restore erfolgreich, Release-Build 0 Warnungen / 0 Fehler, gesamte Suite **272/272 bestanden**, keine übersprungen (Domain 8, ImportExport 23, Application 43, Rules 77, Storage 77, UI/Integration 44). Schemaprüfung 8/8 erfolgreich. Keine Paket-, Lockdatei- oder Schemaänderung.
