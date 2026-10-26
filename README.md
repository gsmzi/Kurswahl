# GSM-Kurswahl

Desktopanwendungen für das Gymnasium bei St. Michael. Umsetzung der [Anforderungsspezifikation](docs/GSM_Kurswahl_Anforderungen_Codex.md), beginnend mit M1. Ausschließlich synthetische Daten; kein Produktivbetrieb und keine verbindliche Abgabe in M1.

## Architektur und Projektplan

| Projekt | Verantwortung |
| --- | --- |
| Domain | Fach, Angebot/Variante, Wahlentwurf, Profilvoraussetzungen, versionierte Verwaltungs- und Planungsmodelle, Schnittstellen |
| Rules | gemeinsame strukturierte Prüfungen; M1: Angebotskonsistenz und vorläufiger Prüfstatus |
| Storage | versioniertes JSON, Dateipfade, kontrollierter Dateiaustausch und Konflikterkennung |
| ImportExport | vorbereitete Importvorschau und Herkunftsmodelle; keine erfundenen ASV-Adapter |
| Application | unabhängig von Avalonia testbare Schüler-Bedienlogik |
| StudentApp | startbare Avalonia-Schüleroberfläche |
| TeacherApp | separates startbares Avalonia-Grundgerüst; Verwaltung ab M3 |

Abhängigkeiten: Domain ← Rules/Storage/ImportExport; Domain + Rules ← Application; Application + Storage ← Desktopprogramme. Kein UI-Verweis in Fachbibliotheken. Die zusätzliche Application-Schicht verhindert Fachlogik in Ereignisbehandlern und erlaubt Tests ohne Desktop.

M1-Schritte: (1) Solution/Modelle und Domänentests, (2) konfigurierbares Demoangebot und technische Regeltests, (3) JSON-Schemata/Dateischicht mit Fehler- und Konflikttests, (4) Schüleroberfläche und Anwendungstests, (5) Release-Build, Smoke-Test, Dokumentation. Nach jedem Schritt Tests protokollieren.

M2 ergänzt vollständiges Angebot, signierte Einzelprofile, belegte Rechtsregeln, Prüfungsoptionen und Kontrollbogen. M3 ergänzt geschützten Lehrerbestand, Stammdatenimport, Profilsignierung/-verteilung, Historie und Nachwahl. M4 ergänzt Szenarien, Nachfrage und Planungsstände. M5 ergänzt ASV nach Musterdatei, Exporte, Netzpilot, Backup und Auslieferung.

Lehrerbestand und Schülerdateien bleiben getrennt. Die spätere Importgrenze prüft authentische Herkunft und aktuelle Lehrerstammdaten erneut. Kursgruppen sind keine Angebote; eine Gruppenreduzierung ist keine Angebotsstreichung. Ein Szenario referenziert einen Planungsstand und darf ihn nicht mutieren. Historische Wiederherstellung wird als neue Revision modelliert.

## Bauen, testen, starten

Voraussetzungen für Entwicklung: .NET SDK 10.0.400 (oder kompatibler Patch gemäß `global.json`), PowerShell 7 für die Skripte. .NET 10 ist eine [LTS-Version mit Unterstützung bis November 2028](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support). NuGet-Zugriff ist beim ersten Restore nötig, nicht beim Betrieb der Anwendung.

```powershell
./scripts/Test.ps1 -LockedMode
```

Das Skript führt Restore, Release-Build, sämtliche xUnit-/Avalonia-Headless-Tests und beide JSON-Schemaprüfungen aus. Einzelbefehle:

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet restore GSM-Kurswahl.sln --configfile NuGet.Config --locked-mode
dotnet build GSM-Kurswahl.sln -c Release --no-restore
dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --logger trx
dotnet run --project src/GsmKurswahl.StudentApp -c Release --no-build --no-restore
dotnet run --project src/GsmKurswahl.TeacherApp -c Release --no-build --no-restore
```

Die GitHub-Actions-Konfiguration prüft Windows und Linux. Der lokale Teststand steht im [Testprotokoll](docs/TESTPROTOKOLL.md); ein angelegter Workflow ist noch kein beobachteter CI-Lauf. Pakete und transitive Versionen sind mit `packages.lock.json` festgehalten. SDK-/Paketupdates bewusst testen und Lockdateien aktualisieren.

## Bedienung und Konfiguration

1. Schülerprogramm starten. Es zeigt ausschließlich die synthetische ID `demo-001`, Abitur 2029 und den Status **vorläufig / needs_review**. Die angezeigte Betriebssystemkennung wird nicht als Schulberechtigung interpretiert oder in die Wahl geschrieben.
2. Den fünfstufigen Assistenten durchlaufen: drei LF mit sofortiger Teilprüfung, zwei vorläufige mündliche Fächer, Pflichtbereiche als Einzelfragen, weitere Kurse und Überblick. Deutsch/Mathematik werden ohne entsprechendes LF mündlich gesetzt. Rechts zeigt „Dein Plan“ jederzeit alle Belegungen je Halbjahr. Details stehen unter „Geführter Wahlablauf“.
3. Geplante Angebote sowie INF, Italienisch und bilinguale Varianten ohne verifiziertes Profil sind nicht wählbar; sie stehen mit Begründung in einer eingeklappten Liste je Schritt. BF Ethik wird nicht wegen unbekannter Klasse-10-Vorbildung pauschal gesperrt.
4. Halbjahreskurse, Wochenstunden und Prüfhinweise rechts prüfen. Eindeutige Pflichtbelegungen werden ergänzt, Alternativen und Sonderfälle bleiben sichtbar offen. Vollständige Rechtsprüfung folgt in M2; es gibt keine Schaltfläche zur verbindlichen Abgabe.
5. **Entwurf speichern** schreibt JSON. Automatisches Zwischenspeichern erfolgt nach 1,5 Sekunden ohne weitere Änderung und ist abschaltbar. **Entwurf laden** verlangt bei ungespeicherten Änderungen eine Bestätigung. Auch Schließen schützt einen ungespeicherten Entwurf durch Rückfrage.

Die mitgelieferte `appsettings.json` wird beim Build neben die EXE kopiert. Standardziel ist `{HOME}/GSM-Kurswahl-Demo/aktuell.json`, ausdrücklich ein Demoordner im persönlichen Home, kein Schulnetzpfad. `homeDirectory`, `choiceDirectory`, `choiceFileName` und `catalogPath` sind konfigurierbar. Eine externe Konfiguration lässt sich über `GSM_KURSWAHL_SETTINGS` angeben. Details und Schemata: [Dateiformat](docs/DATEIFORMAT.md).

Für einen isolierten Probelauf eine Kopie von `appsettings.json` anlegen, darin `homeDirectory` auf einen absoluten Testordner und `catalogPath` auf den absoluten Pfad von `samples/fachangebot-demo.json` setzen. Optional `samples/schueler-demo.json` als `aktuell.json` in den gewählten Unterordner kopieren. Diese Beispielwahl ist bewusst unvollständig. Dann:

```powershell
$env:GSM_KURSWAHL_SETTINGS = 'C:/absoluter/Testordner/appsettings.json'
dotnet run --project src/GsmKurswahl.StudentApp -c Release --no-build --no-restore
```

Beim manuellen Probelauf Deutsch-LF wählen, Speichern abwarten, schließen, erneut starten und die vier Halbjahre kontrollieren. INF und Bili-BIO dürfen nur in den eingeklappten „nicht wählbar“-Listen erscheinen. Autosave ausschalten, Auswahl verändern und Laden/Schließen abbrechen: Änderungen müssen erhalten bleiben. Dieselbe Datei in einer zweiten Instanz ändern: veraltetes Speichern muss einen Konflikt melden. Bei defektem JSON muss Laden scheitern und Speichern gesperrt bleiben. Kontrolliere Tastaturbedienung, Fokus, Lesbarkeit bei 125/150 % DPI und schmale Fenster zusätzlich auf dem Zielrechner.

## Windows-Auslieferung

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet publish src/GsmKurswahl.StudentApp -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:RestoreConfigFile=NuGet.Config -o artifacts/publish/student-win-x64
dotnet publish src/GsmKurswahl.TeacherApp -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:RestoreConfigFile=NuGet.Config -o artifacts/publish/teacher-win-x64
```

Den **gesamten** jeweiligen Ausgabeordner verteilen, einschließlich DLLs, Konfiguration und Demo-Katalog; die EXE alleine genügt nicht. Selbstenthaltend bedeutet: auf dem Zielgerät wird keine Entwicklungsumgebung und keine vorinstallierte .NET-Laufzeit benötigt. Vor produktiver Verteilung aktuelle unterstützte SDK-/Runtime-Patches verwenden und auf einem Rechner ohne SDK sowie mit Schulnetzrechten testen. Keine Installation in systemgeschützte Ordner und keine Administratorrechte für den normalen Dateibetrieb vorgesehen.

**Lokales Ergebnis:** Der selbstenthaltende Publish konnte hier wegen nicht abrufbarer/vollständig gecachter .NET-10.0.11-Runtimepakete nicht abgeschlossen werden. Die obigen Befehle sind die vorgesehene Auslieferungsanleitung, kein Nachweis eines erfolgreichen Standalone-Pakets. Als M1-Ausgabe werden frameworkabhängige Ordner erstellt, die eine installierte .NET-10-Laufzeit benötigen:

```powershell
dotnet publish src/GsmKurswahl.StudentApp -c Release --self-contained false -p:RestoreConfigFile=NuGet.Config -o artifacts/publish/student-framework-dependent
dotnet publish src/GsmKurswahl.TeacherApp -c Release --self-contained false -p:RestoreConfigFile=NuGet.Config -o artifacts/publish/teacher-framework-dependent
```

## Grenzen und weitere Arbeit

M1 ist ein lauffähiger Demonstrator. Vollständige AGVO-Prüfung, signierte Profile, verbindliche Abgabe, PDF, Lehrerdatenbank, reale Importadapter und Planung sind **noch nicht implementiert**. Die Lehrer-App zeigt den vorgesehenen Ausbau an. ImportExport enthält ausschließlich den Vorschauvertrag und einen entsprechenden Vertragstest, keinen CSV-/XLSX-Parser. Die Domain enthält vorbereitende Versions-/Planungsmodelle, keine vorgetäuschte Historienpersistenz.

[Offene Punkte](docs/OFFENE_PUNKTE.md), [Quellenstand](docs/RECHTSQUELLEN.md), [Abhängigkeiten](docs/ABHAENGIGKEITEN.md) und [Entwicklungsanweisungen](AGENTS.md) sind Bestandteil der Übergabe. Das Quellcode-Repository ist [gsmzi/Kurswahl](https://github.com/gsmzi/Kurswahl). Build-Ausgaben und lokale Testartefakte sind durch `.gitignore` ausgeschlossen.

### Geführter Wahlablauf

Oben zeigt eine Schrittleiste Fortschritt und offene Pflichtbereiche; erledigte Schritte sind direkt anklickbar. Rechts steht dauerhaft „Dein Plan“: ein Raster Fach × HJ 1–4 mit Kursart (LF/BF/Wahlfach), Markierungen „Pflicht“, „mündl.“ und „klären“, gestrichelten Platzhaltern für offene Entscheidungen, dem Stand zum Planungsziel von 42 Halbjahreskursen und Wochenstunden je Halbjahr. Ein Klick auf eine Zeile springt zum zugehörigen, bereits erledigten Schritt.

Über dem Plan zeigt „Deine Voraussetzungen“ (in Schritt 1 aufgeklappt) die persönlichen Zulassungsmerkmale: Informatik-Brückenkurs/IMP, Italienisch-Profil, bilingualer Bildungsgang, Religion/Ethik in Klasse 10 und Sportbefreiung, jeweils mit den davon abhängigen Angeboten. Die Werte stammen ausschließlich aus dem Profil (`StudentSession.Prerequisites`) und sind nicht editierbar. Ohne signiertes Profil (M1) stehen alle auf „ungeprüft“.

1. Leistungsfächer: nur wählbare LF-Angebote. Die Teilprüfung kontrolliert Anzahl, Demo-Kernfächer und ob eine Prüfungsplanung mit D/M und allen Aufgabenfeldern möglich ist.
2. Prüfungsfächer: Deutsch/Mathematik ohne entsprechendes LF werden als feste Information angezeigt. Angeboten werden nur Fächer, die mit der bisherigen Planung noch eine vollständige Kombination ergeben; GEO nie.
3. Pflichtbereiche: Pflicht-BF und Prüfungsbelegungen werden eingetragen und nur zusammengefasst. Offene Bereiche (Kunst/Musik, Religion/Ethik, Naturwissenschaften) kommen als einzelne Fragen mit den passenden Angeboten; beantwortete Fragen klappen zu und lassen sich über „Ändern“ öffnen. Fragen, die schon durch LF erfüllt sind, entfallen. Offene Fragen blockieren den Weiter-Schritt nicht, bleiben aber in Schrittleiste, Plan und Überblick sichtbar.
4. Weitere Kurse: eigene Kurse anpassen (Pflicht-Halbjahre sind fest) und weitere wählbare Kurse hinzufügen.
5. Überblick: Halbjahressummen, offene Punkte mit Regel-ID und die Prüfhinweise der Regelprüfung.

Die Fragen stammen aus dem Aufgabenmodell `ChoiceWorkflow.Tasks` (Regeln `PLAN-AREA-*`) und `StudentSession.Plan`; die Oberfläche enthält keine eigenen Fachregeln. Zurück erlaubt Änderungen; zusätzliche vorhandene Belegungen bleiben erhalten. Ein schon belegtes BF wird in Schritt 1 eigens angezeigt und kann dort bewusst abgewählt werden, bevor dasselbe Fach als LF gewählt wird. Vorläufige mündliche Planung wird mitgespeichert; Laden startet zur erneuten Prüfung bei Schritt 1. Wahl-Schema ist jetzt v2, v1-Dateien werden geschützt abgewiesen (siehe DATEIFORMAT.md). Vollständige Rechtsprüfung und verbindliche Abgabe bleiben außerhalb M1.
