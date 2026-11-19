# M5: Abgleich, Exporte und Betriebsvorbereitung

Stand 28.10.2026. Funktionaler Ausbau vom Nutzer ausdrücklich beauftragt. Ausschließlich synthetischer Erprobungsbetrieb; die fachliche Freigabe für Abitur 2029 bleibt offen. Keine ASV-Schreibschnittstelle, keine automatische Schulnetzverteilung.

## ASV-Abgleich („zz Kursstufe Fächerwahl“) und Schülerdaten-Quelle (Aktualisierung 19.11.2026)

Die frühere Seite „Abgleich / Abschluss“ wurde auf Wunsch des Nutzers zu **ASV-Abgleich** entschlackt: Sie dient ausschließlich der schnellen **Deckungsgleichheitsprüfung** zwischen dem Lehrerbestand und dem ASV-BW-Export **`zz Kursstufe Fächerwahl`** (`.xlsx` direkt aus `Datei → Verwaltung → Exportformatbibliothek → Schüler → zz Kursstufe Fächerwahl` oder als `.csv`, UTF-8 bzw. Windows-1252/Latin-1, Komma, Semikolon oder Tabulator). Im Lehrerbestand werden dabei keine Wahlen oder Revisionen verändert; die früheren Bedienelemente für manuelle Übertragungs-Abhaklisten und doppelte Sicherungs-Buttons wurden aus der UI entfernt (Jahrgangssicherung liegt weiterhin unter *Einstellungen* und den Netzwechsel-Expandern; DB-Schema 3/4 mit `transfer_receipts` bleibt rückwärtskompatibel erhalten).

Der Adapter `AsvCourseChoiceImport` liest das ASV-Format (inklusive leerer erster Spaltenüberschrift für die Klasse, `Name mit Vornamen`, `ASV-ID`, `Fächer Kursstufe` mit Paaren `Fach [WST];Code`) und übersetzt die ASV-Kürzel (`MUS`→`MU`, `WI`→`W`, `REV`→`ER`, `RRK`→`KR`, `SPO`→`SP`, `PSYCH`→`PSY`), Halbjahre (`X`/`Z` belegt, `-` nicht belegt), Leistungsfächer (`-L-` / `1..3`) sowie geplante mündliche Prüfungsfächer (`m1`/`m2`, nicht `z-P-...`). Da ASV-BW im Kurswahlexport nicht zwischen normal und bilingual unterscheidet, wertet der Abgleich intern `Biologie bilingual` gegen `BIO` in ASV als deckungsgleich und weist dies mit einem Hinweis aus. Unbekannte ASV-Fächer werden als Abweichung gemeldet; fehlende `ASV-ID`s erscheinen als `InvalidIdentity` ohne Namensheuristik. Abweichungen lassen sich filtern (Standard: *Nur Abweichungen & Auffälligkeiten*), nach XLSX/CSV exportieren und per Sprungbutton direkt unter *Kurswahlen* öffnen. Der frühere allgemeine Vertrag `choice-table-v1` (`ChoiceTableImport` / `ChoiceReconciliation.Confirm`) bleibt auf Anwendungsebene kompatibel erhalten.

Für den vorhergehenden Klasse-10-Schülerimport empfiehlt der Importdialog das ASV-BW-Exportformat `zz Exportformat für paedML als XLSX` / `… als CSV` (oder eine schuleigene Kopie mit Vorbildungsspalten), damit die **dauerhafte 36-stellige `ASV-ID`** von Beginn an identisch vorliegt; `StudentColumnMatcher` erkennt entsprechende deutsche/ASV-Spaltenüberschriften automatisch (im Erprobungsstand gelten im Schülerimport weiterhin `demo-`-IDs).

## Exporte und Datenbankschema 3/4 (Historie)

CSV/XLSX-Belegungslisten unter *Kurswahlen*/*Kursplanung* und die gefilterte Differenzliste unter *ASV-Abgleich* bleiben als Arbeitsunterlagen verfügbar. Exportschreiben erfolgt über eine temporäre Datei im Zielverzeichnis; CSV schützt vor Formelinterpretation und XLSX schreibt ausschließlich Textzellen. Datenbankschema 3/4 (mit der Tabelle `transfer_receipts`) bleibt aus Kompatibilitätsgründen erhalten, wird in der UI jedoch nicht mehr über eine separate Abhak-Tabelle bespielt.

## Sicherung und Wiederherstellung

**Jahrgang sichern** erstellt eine `.gsm-transfer` mit Manifest v2 (v1 bleibt importierbar; Stand 02.11.2026, ebenso Datenbankschema 4 mit Archiv eingesammelter Dateien, siehe DATEIFORMAT.md), vollständiger SQLite-Backupkopie und SHA-256. Enthalten sind aktiver Jahrgangskatalog, Stammdaten, Wahlhistorien, Profile/Zustellberichte, Nachwahlvermerke, Planungsstände und Übertragungsvermerke. Nicht enthalten: andere Jahrgänge, EXE/Installationskonfiguration, Schüler-Home-Dateien und unbestätigte Vorschauen. Jeden benötigten Jahrgang separat sichern. Laufendes Einsammeln oder offene Einsammelkonflikte blockieren Sicherung.

Vor Schemaerweiterung und nach jedem abgeschlossenen Arbeitstag sichern; Ziel muss außerhalb des lokalen Verwaltungsgeräts in einem von der Schule freigegebenen, geschützten Sicherungsbereich liegen. Dies ist ein Betriebsentwurf: Verantwortliche, Häufigkeit, Aufbewahrung und Löschfristen sind vor Echtdatenbetrieb verbindlich festzulegen. Die App führt keine automatische Sicherung und keine automatische Löschung durch. Archive enthalten schutzbedürftige Verwaltungsdaten; die Prüfsumme bietet Integritätsprüfung, keine Verschlüsselung oder Authentifizierung.

**Sicherung prüfen und als neuen Bestand öffnen** zeigt den Jahrgang/Stand und benötigt Bestätigung. Unbekannte Schemafassungen, beschädigte Prüfsummen, zusätzliche Archiveinträge und Änderungen nach Vorschau werden abgewiesen. Wiederherstellung immer in einen neuen lokalen Ordner; der bisherige Bestand bleibt erhalten. Nicht beide Bestände parallel bearbeiten. Stichprobe nach Wiederherstellung: Schülerzahl, aktive Wahlen, mehrere Historien, Nachwahlvermerke, Planungsstände, Übertragungsvermerke. Wiederherstellungsdatum, Paketversion und Ergebnis im schulinternen Betriebsprotokoll festhalten. Testwiederherstellung vor produktivem Betrieb und regelmäßig danach erforderlich.

## Release und Schulnetz-Pilot

`./scripts/Test.ps1 -LockedMode` führt Restore, Release-Build, Tests einschließlich Headless-UI und Schemaprüfung aus. `./scripts/Publish-Release.ps1` erstellt ein **frameworkabhängiges Erprobungspaket** mit beiden Apps, Dokumentation, Dateiprüfsummen und ZIP. `-SelfContained` fordert Windows-x64-Runtimepakete an und scheitert bei fehlenden Paketen; es entsteht dann kein fertiges ZIP. Ein fehlgeschlagener Publish darf nicht als Standalone-Release verteilt werden. Keine bestehenden Ausgabeordner werden überschrieben, keine Verwaltungsdaten eingepackt. Eigene Softwarelizenz und vollständige Drittanbieter-Lizenztexte sind vor endgültiger Auslieferung noch festzulegen/beizulegen.

Pilot ausschließlich mit synthetischen Personen und Konten, auf tatsächlichen Schulgeräten:

1. Paketprüfsumme, Version und Start ohne SDK prüfen; für frameworkabhängige Ausgabe .NET-10-Laufzeit voraussetzen. AppLocker/SmartScreen sowie 125/150-%-DPI, Tastatur und Screenreader erproben.
2. Schülerkonto: eigenes Profil lesen, eigene Wahl schreiben; fremde Wahlordner und Lehrerbestand dürfen unzugänglich sein. Zentralen Programmordner schreibgeschützt halten. Lehrerprogrammstart allein darf keine Rechte gewähren.
3. Berechtigtes Lehrerkonto: persönliche Profile verteilen und Wahlen einsammeln; Verwaltungsdatenbank ausschließlich lokal, ein schreibender Arbeitsplatz. Keine SQLite-Datenbank über SMB öffnen.
4. Unterbrochene Netzwerkverbindung, fehlende Rechte, doppelte App-Instanz und konkurrierende Wahländerungen erproben. Vorhandene Dateien müssen erhalten bleiben. Fehlerbericht und Wiederholung prüfen.
5. Netzwechsel durch Sicherung/Import, manuelle ASV-Testeingabe und allgemeinen Rückvergleich mit synthetischen Daten durchführen; produktiven Adapter erst gegen freigegebene Musterdatei testen.
6. Sicherung in neuen Bestand wiederherstellen und Historie/Planungsstand/Vermerke vergleichen. Ergebnis und Freigabe durch Verantwortliche protokollieren.

Diese Schritte sind ein **noch auszuführender Pilotplan**, kein absolvierter Schulnetztest. Rechtliche Freigabe, konkrete ACLs/Zuordnungen, ASV-Musterdatei, Aufbewahrung und tatsächlicher Start auf einem Zielgerät bleiben externe Abnahmevoraussetzungen.

### Beobachteter Paketstand

Am 28.10.2026 wurden beide Apps erfolgreich selbstenthaltend für Windows x64 mit .NET 10.0.14 publiziert. Separate Runtime-Lockdateien liegen neben den bisherigen Lockdateien; der Publish verwendet sie im Locked-Modus. Der frühere Blocker durch fehlende Runtimepakete ist für diesen Entwicklungsrechner damit erledigt. Der tatsächliche Schulgerätetest ohne SDK bleibt offen. Testergebnisse und Prüfsummen stehen in TESTPROTOKOLL.md.
