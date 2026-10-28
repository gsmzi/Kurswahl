# Abhängigkeiten, Lizenzen und Sicherheit

Stand der M1-Implementierung: 25.10.2026. Exakte transitive Auflösung und Integritätshashes stehen in den projektweisen `packages.lock.json`.

| Direkte Abhängigkeit | Version | Lizenz/Verwendung |
| --- | --- | --- |
| .NET SDK | 10.0.400 | Microsoft .NET SDK; .NET-LTS-Zielframework `net10.0` |
| Avalonia.Desktop / Themes.Fluent | 11.3.20 | MIT, freie Desktop-UI |
| Avalonia.Headless.XUnit | 11.3.20 | MIT, ausschließlich Tests |
| xunit | 2.9.2 | Apache-2.0, ausschließlich Tests |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0, ausschließlich Tests |
| Microsoft.NET.Test.Sdk | 17.12.0 | MIT, ausschließlich Tests |

Die Versionen wurden für den vorhandenen Paketbestand und die kompatible Avalonia-11-Headless-Testsuite explizit gepinnt; das ist keine Behauptung, es seien die neuesten Versionen. xUnit v2 ist inzwischen als nicht mehr gepflegt markiert; Upgrade auf xUnit v3 samt passendem UI-Testadapter vor langfristiger Nutzung planen. [xUnit-Paketinformationen](https://www.nuget.org/packages/xunit/2.9.2). Keine kommerziellen Avalonia-Accelerate-Komponenten verwendet.

Avalonia bringt Build-Telemetrie mit. `scripts/Test.ps1` und CI setzen das offizielle `AVALONIA_TELEMETRY_OPTOUT=1`; bei manuellen Build-/Publish-Aufrufen ebenso setzen. [Avalonia: Opt-out](https://github.com/AvaloniaUI/Avalonia/wiki/Telemetry). Auch .NET-CLI-Telemetrie wird im Testskript deaktiviert. Die Anwendungen selbst implementieren keine Netzverbindung, Cloud-Synchronisation oder Telemetrie.

Restore bezieht Pakete ausschließlich über den expliziten NuGet.org-Feed. Für reproduzierbare CI-Restores `--locked-mode` verwenden. Sicherheitsprüfung bei Paketänderung und vor Auslieferung:

```powershell
dotnet list GSM-Kurswahl.sln package --vulnerable --include-transitive --no-restore
dotnet list GSM-Kurswahl.sln package --include-transitive --no-restore
```

Ergebnisse dieses Laufs siehe Testprotokoll. Ein Paket-Audit ist keine vollständige Sicherheitsprüfung. Vor Echtdatenbetrieb: aktuelle .NET-Patches, Schlüsselverwaltung, signierte Profile, Schulnetz-ACLs, Backup-/Aufbewahrungskonzept und M2–M5 erfüllen. Private Schlüssel gehören niemals in dieses Repository.

Die transitive Laufzeit enthält unter anderem Avalonia (MIT), SkiaSharp/native Skia (MIT/BSD-Familie), HarfBuzzSharp/native HarfBuzz (MIT-Familie) und MicroCom.Runtime (MIT). Lizenztexte/Notices der tatsächlich ausgelieferten Pakete sind vor endgültiger Veröffentlichung beizulegen. Es wurde keine Lizenz für den eigenen Anwendungscode festgelegt; diese Entscheidung liegt beim Projektträger.

## Wahlassistent 25.10.2026

Keine neuen Pakete oder Paketversionsänderungen; bestehende Lockdateien bleiben unverändert. Lokales SDK 10.0.403. Der abschließende Restore verwendet den vorhandenen Paketcache, `--locked-mode`, `--ignore-failed-sources` und `-p:NuGetAudit=false`, weil Onlinezugriff in dieser Ausführungsumgebung gesperrt ist. Dies ist kein neuer Sicherheits-Audit; der zuvor dokumentierte Lizenz-/Sicherheitsstand bleibt maßgeblich.

## M2 (26.10.2026)

Keine neuen externen Pakete und keine Paketversionsänderungen. ECDSA/SHA-256 aus .NET-System.Security.Cryptography; HTML-Export nutzt System.Net.WebUtility und benötigt keine Browserbibliothek. StudentApp referenziert jetzt intern ImportExport; die beiden betroffenen Lockdateien wurden per Restore aktualisiert. SDK lokal 10.0.403. Desktopprogramme bleiben ohne Onlinezugriffe. Der Restore lief nach einem Sandbox-Lesefehler auf die lokale NuGet-Konfiguration erfolgreich mit erweitertem Zugriff; keine Änderung der NuGet-Konfiguration.

Audit am 26.10.2026: `dotnet list GSM-Kurswahl.sln package --vulnerable --include-transitive --no-restore` erfolgreich; für alle Projekte keine bekannten anfälligen Pakete gemäß abgefragtem NuGet.org-Feed. Dies ist keine umfassende Sicherheitsfreigabe.

## Direkter PDF-Export (26.10.2026)

ImportExport pinnt jetzt `SkiaSharp` **2.88.9** und `SkiaSharp.NativeAssets.Linux` **2.88.9** explizit (MIT). Diese Versionen waren bereits transitiv über Avalonia vorhanden und werden wiederverwendet; keine Aktualisierung auf eine abweichende Skia-Hauptversion. Native Windows-/macOS-Assets über SkiaSharp, Linux explizit für CI. [Paket/Lizenz](https://www.nuget.org/packages/SkiaSharp/2.88.9), [PDF-API](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skdocument.createpdf). Schriftauflösung erfolgt lokal über Skia/systemeigene Schriftverwaltung (Arial oder Plattformersatz), ausgewählte Schriften werden in die PDF eingebettet. Keine heruntergeladenen Fonts, keine Browser-/Druckerabhängigkeit und keine Onlinezugriffe beim Export. Lockdateien wurden per Restore aktualisiert. Aktueller Paket-Audit folgt im Abschlusslauf.

Abschließender Audit nach PDF-Referenzänderung am 26.10.2026 erfolgreich: für sämtliche Projekte einschließlich transitiver Pakete keine bekannten anfälligen Pakete laut NuGet.org. Locked-Restore und Release-Build erfolgreich; 151 Tests bestanden. Plattform-Sichtprüfung hier auf Windows; Linux/macOS-Fontauflösung bleibt im jeweiligen Zielbetrieb zu testen.

## M3 / 27.10.2026

Neue **lehrerseitige** Abhängigkeit: Microsoft.Data.Sqlite **10.0.14** (MIT), transitiv Microsoft.Data.Sqlite.Core **10.0.14** und SQLitePCLRaw core/bundle/provider/native **2.1.13** (Apache-2.0 für SQLitePCLRaw; SQLite-Kern Public Domain; Paketnotices bei Auslieferung beachten). Gepinnt im separaten AdministrationStorage-Projekt; keine Referenz durch StudentApp. Lockdateien nach Architekturtrennung mit --force-evaluate erneuert. CSV/XLSX-Import nutzt ausschließlich .NET (Text, ZIP, XML), keine Excelinstallation und keine zusätzlichen Tabellenpakete. Kryptografie aus .NET; private Schlüssel ausschließlich administrativer Zertifikatspeicher.

Erster Versuch mit Microsoft.Data.Sqlite 10.0.0 wurde wegen NU1903 in SQLitePCLRaw.lib.e_sqlite3 2.1.11 verworfen (GHSA-2m69-gcr7-jv3q), nicht durch Deaktivieren des Audits umgangen. 10.0.14 löst 2.1.13 auf; erfolgreicher Restore. Quellen: [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.14), [Microsoft-Dokumentation](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/), [SQLitePCLRaw](https://www.nuget.org/packages/SQLitePCLRaw.core/2.1.13). Abschließender Audit siehe TESTPROTOKOLL.md. Keine Onlinezugriffe der Desktopanwendungen.

M3-Abschlussaudit 27.10.2026 erfolgreich: für alle 14 Projekte einschließlich transitiver Abhängigkeiten keine bekannten anfälligen Pakete laut NuGet.org. Locked-Restore, Release-Build und 168 Tests erfolgreich; Details/Fehlversuche im Testprotokoll.

## M4 / 28.10.2026

Keine neuen Pakete und keine Änderungen an Paketversionen oder Lockdateien. Kursplanung verwendet den vorhandenen Regelkern und die lehrerseitige SQLite-Komponente. Keine neuen Onlinezugriffe. Der letzte Paket-Audit bleibt der oben dokumentierte M3-Audit; für M4 wird daraus keine neue Sicherheitsfreigabe abgeleitet.

- 28.10.2026: Netzwechsel-/Planungsdruck ergänzt ohne neue NuGet-Pakete. PDF-Erstellung nutzt weiterhin SkiaSharp 2.88.9; ZIP/SHA-256 und SQLite-Backup verwenden bestehende Framework-/Paketabhängigkeiten. Lockdateien unverändert.

- 28.10.2026: Gemeinsame Nachwahl-DOCX nutzt System.IO.Compression und System.Xml.Linq aus .NET 10. Keine zusätzlichen NuGet-Pakete oder Änderungen an Lockdateien. Word wurde nur zur manuellen Layoutprüfung eingesetzt und ist keine Laufzeitvoraussetzung.

## M5 (28.10.2026)

Keine neuen Pakete oder Paketversionsänderungen. XLSX-Export nutzt .NET ZIP/XML, CSV .NET-Textfunktionen, Herkunftsnachweis SHA-256. Lockdateien bleiben unverändert. Release-Skript erstellt ausdrücklich Erprobungspakete; eigene Lizenz und Drittanbieter-Notices bleiben vor endgültiger Auslieferung offen. Tatsächlicher Publish-/Auditstatus im Testprotokoll; keine neue Sicherheitsfreigabe aus alten Auditergebnissen ableiten.

M5-Abschluss: separate `packages.win-x64.lock.json` für die acht ausgelieferten Projekte ergänzt; bisherige `packages.lock.json` unverändert. Windows-Publish nutzt diese Runtime-Lockdateien im Locked-Modus und pinnt die selbstenthaltende .NET-Runtime auf 10.0.14. Beide Anwendungen erfolgreich für win-x64 publiziert. NuGet-Audit (`dotnet list GSM-Kurswahl.sln package --vulnerable --include-transitive --no-restore`) am 28.10.2026 erfolgreich: alle 14 Projekte laut NuGet.org ohne bekannte anfällige Pakete. Keine umfassende Sicherheits-/Produktivfreigabe.
