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
