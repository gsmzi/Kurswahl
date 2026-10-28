# M5: Abgleich, Exporte und Betriebsvorbereitung

Stand 28.10.2026. Funktionaler Ausbau vom Nutzer ausdrücklich beauftragt. Ausschließlich synthetischer Erprobungsbetrieb; die fachliche Freigabe für Abitur 2029 bleibt offen. Keine ASV-Schreibschnittstelle, keine automatische Schulnetzverteilung.

## Allgemeiner Abgleich

Unter **Abgleich / Abschluss** eine CSV (UTF-8, Semikolon) oder XLSX (genau ein Blatt, nur Werte) auswählen. Sechs Spalten frei zuordnen:

| Zielfeld | Inhalt |
| --- | --- |
| studentId | Stabile, eindeutige synthetische demo-ID; keine Namensheuristik |
| year | Abiturjahrgang des geöffneten Bestands |
| subject | Exakte Fachkennung aus dem Katalog |
| type | Lf, Bf, Elective oder Seminar |
| variant | Normal oder Bilingual, ausdrücklich erforderlich |
| semester | Eine Zahl 1–4 |

Eine Zeile entspricht einem Halbjahreskurs. Dies ist der allgemeine Vertrag `choice-table-v1`, **kein behauptetes ASV-Exportformat**. Der konkrete ASV-Adapter benötigt weiterhin eine anonymisierte Musterdatei und ihre Feldsemantik. Abweichende ASV-Werte müssen vorläufig in diesen allgemeinen Vertrag übersetzt werden; keine automatische Interpretation.

Vor der Vorschau bestätigen, dass für jede enthaltene Person die vollständige Belegung vorliegt. Die Software kann Vollständigkeit nicht aus fehlenden Zeilen erschließen. Ein fehlendes Fach bei einer enthaltenen Person bedeutet eine vorgeschlagene Entfernung; eine vollständig fehlende Person bedeutet keine Änderung. Eine vollständig leere Wahl kann mit diesem Zeilenformat nicht importiert werden. Fehlerhafte Zeilen, doppelte Belegungen, unbekannte Fach-/Kursart-/Variantenkombinationen und falsche Jahrgänge verwerfen die gesamte Vorschau. Es gibt keinen stillen Teilimport.

Der Vergleich zeigt `Identical`, `Changed`, `OnlyInternal`, `OnlyExternal` und `InvalidIdentity`. Identische Belegungen erzeugen keine Revision. Neue Personen bleiben unübernehmbar, bis ihre Stammdaten über den vorhandenen Schülerimport angelegt und geprüft wurden; danach den Abgleich neu laden. Namen werden niemals zur automatischen Zusammenführung benutzt.

Bei Abweichungen erscheinen interne/externe Belegung und Ergebnisse des gemeinsamen Regelkerns auf Basis der **aktuellen geschützten Vorbildung**. Nach Häkchen und Änderungsgrund ersetzt die Einzelübernahme die vollständige Belegung. Mündliche Planung bleibt erhalten und wird erneut geprüft; die Wahl bleibt Entwurf. Auch ungültige oder fachlich offene Wahlen können zur dokumentierten Klärung übernommen werden, ohne Grünstatus oder Abgabe zu erzeugen. Schülerdateien werden nicht verändert. Jede Übernahme erzeugt eine append-only Lehrerrevision mit Quelle `AsvReconciliation`, Quelldateiname, SHA-256, Spaltenmapping und vollständigem Snapshot. Änderungen an Quelle, Stammdaten, aktiver Revision oder Katalog seit Vorschau verhindern die Übernahme. Keine Sammelübernahme implementiert: jede Abweichung wird einzeln bestätigt.

## Exporte und manuelle Übertragungsvermerke

CSV/XLSX-Belegungslisten nach Schüler-ID/Klasse sowie offenen Prüf-/Nachwahlfällen filtern. Die Liste enthält stabile Fachkennungen, Kursart, Variante und Halbjahr sowie Verwaltungsrevision und Prüfstatus. Keine Vorbildungsdaten, Konten oder Home-Pfade. Abgleich nach Konfliktart filtern und exportieren. In der Kursplanung können die angezeigten Nachfragezahlen einschließlich Planungsstand/Szenario und Abgängerfilter als CSV/XLSX exportiert werden; der bestehende PDF-Druck bleibt verfügbar. Schüler-Kontrollbögen und Nachwahlbögen nutzen die vorhandenen PDF-Funktionen.

Exportschreiben erfolgt über eine temporäre Datei im Zielverzeichnis; vorhandene Ziele werden nicht überschrieben. CSV schützt vor Formelinterpretation; XLSX schreibt ausschließlich Textzellen, keine Formeln oder externen Verknüpfungen. Exportlisten sind Arbeitsunterlagen, keine Rechtsfreigabe. Die Belegungsliste enthält nur Personen mit vorhandenen Halbjahresbelegungen; die Übertragungs-/Ausnahmeliste führt auch Personen ohne Wahl.

**Übertragungsvermerke** ausdrücklich aktivieren: Datenbankschema 2 → 3 (vorher Kursplanung aktivieren und sichern). Kein automatisches Upgrade beim Start. Schema 3 ergänzt ausschließlich eine append-only Tabelle `transfer_receipts`; ältere Programme lehnen diesen Bestand ab. Vermerksschema 1 bindet manuelle Eingabe oder Wiederöffnung an Schüler-ID, Wahlrevision, Stammdatenversion, Angebots-/Regelversion, Bearbeiter, Zeitpunkt und Pflichtnotiz. Nach Änderung wird der alte Vermerk `veraltet`. `manuell_uebertragen` bestätigt ausschließlich die menschlich protokollierte Eingabe, niemals ASV-Inhalt, fachliche Gültigkeit oder eine automatische Übermittlung. Prüfstatus bleibt separat sichtbar. Liste und Ausnahmen nach CSV/XLSX exportierbar.

## Sicherung und Wiederherstellung

**Jahrgang sichern** erstellt eine `.gsm-transfer` mit Manifest v1, vollständiger SQLite-Backupkopie und SHA-256. Enthalten sind aktiver Jahrgangskatalog, Stammdaten, Wahlhistorien, Profile/Zustellberichte, Nachwahlvermerke, Planungsstände und Übertragungsvermerke. Nicht enthalten: andere Jahrgänge, EXE/Installationskonfiguration, Schüler-Home-Dateien und unbestätigte Vorschauen. Jeden benötigten Jahrgang separat sichern. Laufendes Einsammeln oder offene Einsammelkonflikte blockieren Sicherung.

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
