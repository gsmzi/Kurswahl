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
