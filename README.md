# GSM-Kurswahl

Deutschsprachige Avalonia-Desktopanwendungen für die schulinterne Kurswahl. M1 ist abgenommen; aktueller Ausbau ist **M3**, erster Einsatzjahrgang **Abitur 2029**. Grundlage: [Anforderungen](docs/GSM_Kurswahl_Anforderungen_Codex.md).

## Aktueller Stand

- Schülerassistent mit Wahl, automatischem Zwischenspeichern, sicherem Laden und Konflikterkennung.
- Standardangebot für 2029: vorhandene LF-fähige Pflichtfächer aktiviert; Religion, Ethik, Informatik und Italienisch nicht als LF. Psychologie bleibt Wahlfach, SIA Seminarkurs. Jahrgang, Status und einzelne Varianten sind im versionierten Katalog konfigurierbar. Die administrative Jahrgangs-/LF-Auswahl vor der Ausgabe steht im Lehrerprogramm unter „Angebot vorbereiten“.
- Schülerseitige Prüfung individueller signierter Profile: ECDSA P-256/SHA-256, Betriebssystemkonto/SID, Jahrgang und Schüler-ID. Unbekanntes bleibt unbekannt. Details: [Profilvertrag und Betrieb](docs/PROFILE.md).
- Belegungsprüfung mit stabilen Regel-IDs, Pflichtfächern, LF/BF-Zählung, Sprach-/NW-Alternativen, persönlichen Voraussetzungen, GK/GEO/Wirtschaft und Sportersatz. Mögliche mündliche Kombinationen aus den belegten Kursen einschließlich vorläufiger Mindestanrechnung.
- Direkter PDF-Kontrollbogen mit Wahl, Halbjahresstunden, Profilrevision, Prüfungsoptionen und allen offenen Punkten. Offline erzeugt; ohne Browser oder Druckfunktion.

**Keine verbindliche Freigabe:** Der Rechtsstand für Abitur 2029, interne KOE-Sonderfälle und administrative Entscheidungen sind noch abzugleichen. Auch eine strukturell vollständige Wahl bleibt `needs_review`; eindeutig fehlende Belegungen sind `invalid`. Es gibt keine verbindliche Abgabe. Der bisherige Sicherheitsrahmen beschränkt den lauffähigen Stand weiterhin auf synthetische demo-IDs und Demo-Kataloge. Keine Echtdaten verwenden. Die technische M3-Lehrerverwaltung ist implementiert; Bedienung und Betriebsgrenzen: [Lehrerverwaltung](docs/LEHRERVERWALTUNG.md).

## Architektur

| Projekt | Verantwortung |
| --- | --- |
| Domain | Dateisystemfreie Fach-, Wahl-, Profil- und Verwaltungsmodelle |
| Rules | Gemeinsame Regelprüfung, Verfügbarkeit und Planung; nur Domain-Abhängigkeit |
| Storage | Versioniertes JSON, Konflikterkennung, read-only Profilprüfung, Betriebssystemidentität; nur Domain |
| ImportExport | PDF-Kontrollbogen und Importvorschauvertrag; nur Domain |
| Application | UI-unabhängige Schülerlogik auf Domain/Rules |
| StudentApp | Avalonia-Schüleroberfläche, Zusammensetzung der Komponenten |
| AdministrationStorage | Nur lehrerseitig: SQLite, append-only Historie, Signierung und Verteilung; nur Domain |
| TeacherApp | Importvorschau CSV/XLSX, Stammdaten, Konto-/Home-Zuordnung, Profile, Einsammeln, Bearbeitung, Historie, Filter, Nachwahl und Angebotsvorbereitung |

Keine Rechtsregeln in UI oder Exportadapter. Planung, Schülerwahl und Verwaltungsrevisionen bleiben getrennt. Keine Onlinezugriffe durch die Desktopprogramme.

## Bauen und testen

.NET 10 LTS, SDK gemäß `global.json` (lokal 10.0.403), PowerShell 7:

```powershell
./scripts/Test.ps1 -LockedMode
```

Das Skript führt Restore, Release-Build, gesamte Testsuite einschließlich Avalonia-Headless-Smoke und die drei Beispieldatei-Schemaprüfungen aus. Einzelbefehle:

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet restore GSM-Kurswahl.sln --configfile NuGet.Config --locked-mode
dotnet build GSM-Kurswahl.sln -c Release --no-restore
dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --logger trx
dotnet run --project src/GsmKurswahl.StudentApp -c Release --no-build --no-restore
dotnet run --project src/GsmKurswahl.TeacherApp -c Release --no-build --no-restore
```

Tatsächliche Ergebnisse und fehlgeschlagene Zwischenläufe: [Testprotokoll](docs/TESTPROTOKOLL.md). Pakete sind explizit gepinnt, Lockdateien bleiben erhalten; [Abhängigkeiten](docs/ABHAENGIGKEITEN.md). Die vorhandene CI-Konfiguration ist kein Nachweis eines beobachteten CI-Laufs.

## Lehrerprogramm: Jahrgang öffnen und weiterarbeiten

Beim ersten Start führt ein Assistent durch Jahrgang (Vorschlag 2029), Fachauswahl, Schülerimport und Kontozuordnung. Danach lädt die App automatisch den zuletzt aktiven Jahrgang und zeigt offene Aufgaben: Zuordnungen prüfen, Profile bereitstellen, Zustellungen wiederholen oder Kurswahlen einsammeln. Speicherort, bestehende Datenbanken und Zertifikate sind unter Einstellungen erreichbar; der Jahrgangswechsel unter Jahrgang verwalten. Standardablage ist der lokale Benutzer-Datenordner, alternativ ein ausdrücklich gewähltes lokales Verzeichnis. [Bedienung und Fehlerbehandlung](docs/LEHRERVERWALTUNG.md).

## Bedienung Schülerprogramm

1. Start: Profil wird gelesen und geprüft, gespeicherter Wahlentwurf geladen. Die Fußzeile zeigt Profilstatus, Revision und Ausstellungsstand; Voraussetzungen sind aufklappbar und nicht editierbar. Ungültige Profile verlieren alle Berechtigungswirkung, niemals den Wahlentwurf.
2. Drei LF wählen. Persönlich oder schulisch gesperrte Angebote stehen mit Begründung in einer aufklappbaren Liste. Sport-LF benötigt bestätigte Auskunft ohne Befreiung.
3. Zwei mündliche Fächer für die Planung wählen (Pflichtschritt; Deutsch/Mathematik ohne entsprechendes LF sind automatisch gesetzt). Die spätere Prüfung sucht zusätzlich alle möglichen Kombinationen in der tatsächlichen Belegung.
4. Pflichtbereiche beantworten, weitere Kurse ergänzen, Halbjahresplan rechts kontrollieren. Eine Fremdsprache und eine Naturwissenschaft sind Pflicht. Anschließend erscheint immer die gleichwertige Alternative zweite Fremdsprache oder zweite Naturwissenschaft; LF zählen mit. Englisch, Französisch und Latein sind reguläre Angebote; Italienisch erfordert eine bestätigte geeignete Sprachlaufbahn. INF ersetzt nie die erste Naturwissenschaft. Bekannte Sportbefreiungen werden bei automatischen Pflichten berücksichtigt; vorhandene Kurse bleiben bei Änderungen erhalten.
5. Im Überblick Fehler und Klärungsfälle lesen, Prüfungsoptionen ansehen und **Kontrollbogen als PDF exportieren** wählen. Der Export enthält den aktuellen, gegebenenfalls noch ungespeicherten Entwurf.

Autosave nach 1,5 Sekunden ohne Änderung ist abschaltbar. Laden und Schließen schützen ungespeicherte Änderungen durch Rückfrage. LF-/Prüfungswechsel löschen zusätzliche Kurse nicht automatisch. Eine neue Profilrevision wird beim nächsten Programmstart eingelesen und bewertet die erhaltene Wahl neu. Der Offlineclient kann veraltete, noch gültig signierte Profile nicht zuverlässig erkennen; maßgeblich bleibt der aktuelle Lehrerbestand.

## Konfiguration und Dateiversionen

`appsettings.json` liegt neben der EXE. Konfigurierbar: `homeDirectory`, `choiceDirectory`, `choiceFileName`, `catalogPath`, `demoStudentId`. `GSM_KURSWAHL_SETTINGS` kann einen absoluten Pfad zu einer Testkonfiguration angeben. Standardziel ist der synthetische Demoordner `{HOME}/GSM-Kurswahl-Demo/aktuell.json`, kein Schulnetzpfad.

Profil und Signatur heißen `profil.json` und `profil.sig` im selben Wahlordner. Die **öffentlichen** Prüfschlüssel und erlaubten Kontodomänen kommen ausschließlich aus `profile-trust.json` in der geschützten Installation. Das unsignierte Profilsample ist kein gültiger Berechtigungsnachweis.

Wahlschema bleibt v2, Katalogschema v1. Neuer Kataloginhalt: `demo-2029-v3`; Regelversion: `m2-2`; erstes Profilschema: v1. Entwürfe mit früherem Angebots-/Regelstand werden geschützt abgewiesen, nicht automatisch migriert oder überschrieben. Für M2 einen separaten Testordner mit aktualisiertem Sample verwenden. [Dateiformat](docs/DATEIFORMAT.md).

## Manueller Probelauf

Testkonfiguration mit absolutem isoliertem Home-Ordner und absolutem Katalogpfad anlegen. Wahl mit D/M/E als LF beginnen; zwei mündliche Fächer planen; Pflichtbereiche ergänzen, speichern, neu starten und Halbjahre vergleichen. Ohne Profil bleiben INF/Bili und Sport-LF gesperrt. Kontrollbogen im Überblick direkt als PDF exportieren und die A4-Seiten prüfen. Eine zweite Instanz muss veraltetes Speichern als Konflikt melden; defekte JSON-Dateien dürfen nicht überschrieben werden. Aktualisierte Profile einschließlich Kontobindung werden automatisiert mit synthetischen Identitäten getestet; ein tatsächlicher Schuldomänen-/SMB-Test steht aus.

UI startet maximiert, Wiederherstellungsgröße 1440 × 900; Kopfbereich passt sich schmaleren Flächen an. Native Tastatur-/Screenreader-, 125/150-%-DPI- und PDF-Sichttests auf Schulgeräten bleiben erforderlich. Headless-Smoke ist kein Ersatz für diese Sichttests.

## Auslieferung und nächste Meilensteine

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet publish src/GsmKurswahl.StudentApp -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:RestoreConfigFile=NuGet.Config -o artifacts/publish/student-win-x64
dotnet publish src/GsmKurswahl.TeacherApp -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:RestoreConfigFile=NuGet.Config -o artifacts/publish/teacher-win-x64
```

Den gesamten jeweiligen Ausgabeordner verteilen. Der zuvor versuchte selbstenthaltende M1-Publish scheiterte an fehlenden Runtimepaketen; ein M2-Standalone-Paket wird mit diesem Entwicklungsstand nicht als getestet behauptet. Frameworkabhängiger Publish ist mit `--self-contained false` ohne Runtime-ID möglich und benötigt .NET 10 auf dem Zielgerät. Keine automatische Verteilung.

M3 technisch umgesetzt: Jahrgang/LF-Angebote vor Ausgabe konfigurieren, Klasse-10-Import, Profilsignierung/-verteilung, separater SQLite-Lehrerbestand und Historie. Native Schulnetz-/Zertifikatsprüfung und fachliche Freigabe bleiben offen. Einstieg: [M3-Bedienung und Betrieb](docs/LEHRERVERWALTUNG.md), synthetisches Importsample `samples/klasse10-demo.csv`. M4: Planungen und Streichungen. M5: ASV nach Muster, Netzpilot und endgültiges Release. Fachliche Grenzen und Entscheidungen: [Offene Punkte](docs/OFFENE_PUNKTE.md), [Regel-/Quellenstand](docs/RECHTSQUELLEN.md), [Entwicklungsanweisungen](AGENTS.md).

Lehrer-Alltag: Fächer auswählen → Schülerliste tabellarisch und gesammelt übernehmen → Grundverzeichnis der Klassenordner wählen und gemeinsam verteilen. Standardziel: `Klasse/Nachname.Vorname`, optional abweichender Ordner im Import. Bei geöffneter App werden neue Wahlen automatisch alle 60 Sekunden eingelesen; Konflikte bleiben zur Entscheidung offen. Schulzertifikat und geprüfte Kontodaten werden einmalig administrativ vorbereitet.
