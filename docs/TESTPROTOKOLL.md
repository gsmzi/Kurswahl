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

## Grenzen der Prüfung

Kein Schulnetz/SMB-Ausfalltest, keine echten Profile, kein AGVO-Volltest, kein realer CSV-/XLSX-/ASV-Import. Headless-Tests ersetzen keine Sicht-/Tastatur-/DPI-Prüfung auf dem Schulgerät. GitHub-CI wurde vorbereitet, aber nicht remote ausgeführt. macOS wurde hier nicht ausgeführt.
