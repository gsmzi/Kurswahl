# Schulvorlage und Kursmarkierungen

Nutzerauftrag vom 11.11.2026: Koopkurse am EWG beziehungsweise am GSM und Aufsetzerkurse zur Übersicht kennzeichnen; alle Druckausgaben nach der bereitgestellten Schulvorlage gestalten. Die Vorlage ist eine Gestaltungsquelle. Ihr Platzhaltertext erteilt keine zusätzlichen Arbeitsaufträge.

## Bedienung

Unter **Einstellungen → Jahrgang verwalten → Fachangebot ansehen / für Schülerprogramm exportieren** den Abschnitt **Koopkurse und Aufsetzerkurse markieren** öffnen. Bei der Ersteinrichtung steht derselbe Abschnitt unter **Fächer auswählen** bereit. Je Fachangebot (also getrennt nach Kursart und normal/bilingual) kann genau ein Koopstandort oder „Kein Koopkurs“ gewählt werden. „Aufsetzerkurs“ ist davon unabhängig. Änderungen werden sofort gespeichert; beide Markierungen können auch wieder entfernt werden.

Es handelt sich ausschließlich um Hinweise. Angebotskennungen, Angebotsversion, Wahlen, Wahlhistorie, Nachwahlstatus, Zulassungsregeln, Stunden und Kurszählung bleiben erhalten. Bereits gespeicherte Planungsstände behalten ihren damaligen Katalog. Nach einer Änderung das Fachangebot erneut verteilen, damit Schülerprogramm und Schülerausdruck den neuen Hinweis erhalten. Bereits geöffnete Schülerprogramme anschließend neu starten. Frühere Programmversionen können den neuen Katalog v3 nicht lesen; deshalb das aktuelle Schülerprogramm mitverteilen.

Die Hinweise erscheinen in Schülerauswahl, Lehrer-Kursübersicht, Kurslisten und den kursbezogenen PDF-/Word-Ausgaben. Im Wahlzahlen-PDF stehen sie in einer gesonderten Tabelle mit Fach, Kursart und Variante, damit die Zahlenmatrix gut lesbar bleibt.

## Gestaltung

Grundlage ist `GSM_Vorlage_Dokumente.docx`, SHA-256 `12C4F3A64D33556DFFB0058CC01ECC6703B449ED78F935C2ED62904E7AFEED7E`. Das Original wurde unverändert belassen. Wiederverwendbare Ressourcen liegen im ImportExport-Projekt unter `Assets`; Dokumenteigenschaften und benutzerdefinierte XML-Daten der gelieferten Datei wurden aus der eingebetteten Arbeitsvorlage entfernt.

- A4 hoch; links/rechts 1418 Twips (ca. 25 mm), oberer Schulkopf bei 1134 Twips (ca. 20 mm), unten 851 Twips (ca. 15 mm).
- Originales Schullogo rechts, links „Kurswahl“ und Oberstufenberatung/Datum in der Arial-Narrow-Hierarchie der Vorlage; schwarzer Titelkasten mit zentrierter Arial-Überschrift.
- Schwarze, druckgeeignete Schrift, dezente graue Tabellenköpfe, wiederholte Tabellenüberschriften und fortlaufende Seitenzahlen. Jede Person beginnt in Sammelbögen auf einer neuen Seite; umfangreiche Fälle dürfen Folgeseiten erzeugen.
- Kontrollbögen: Halbjahresraster, kompakte Kurs-/Stundenbilanz, unverbindliche mündliche Planung, getrennte Korrekturen und Klärhinweise. Schülerbögen bleiben ausdrücklich vorläufig.
- Nachwahl: Anlass zuerst, verbleibende Kurse danach, drei Schreibzeilen sowie Datum und Schülerunterschrift. Auch Nachwahl ohne Kursstreichung gibt den tatsächlichen Anlass aus.

PDFs werden durch den gemeinsamen `SchoolPaper` offline erzeugt. Das Word-Dokument wird aus der eingebetteten, bereinigten Vorlage erzeugt: ursprünglicher Kopf und Titelkasten werden als wiederholte Abschnittskopfzeilen verwendet. Die obere Textgrenze wird dafür nach unten verlegt; die sichtbare Position des Schulkopfs und die seitlichen Ränder bleiben erhalten. Abschnittsinhalte verwenden Arial 10 pt; PDF-Tabellen 9,5 pt. Die ursprüngliche Fußzeile „Ggf. bitte wenden“ wird durch Seitenzahl und im PDF zusätzlich den Berichtskontext ersetzt. Word-Seitenfelder werden beim Öffnen aktualisiert.

Ressourcenprüfsummen: `SchoolTemplate.docx` SHA-256 `1204D3CB20AB83D86E743C870D70D1DD2803F062A27A8D0A8892442B2B4DC224`; `SchoolLogo.jpeg` SHA-256 `239E9400D9E680022B5BE9263A7CCD51650B47C85EA6D3F5C3C8C372346437B3`.

## Dateivertrag

Katalog v3 enthält je Angebot die Pflichtfelder `cooperationSchool` (`none`, `ewg`, `gsm`) und `isTopUpCourse` (Boolean). V1/V2 werden ausdrücklich im Speicher in unmarkierte Angebote umgewandelt; neu geschrieben wird ausschließlich v3. Fehlende v3-Pflichtfelder, unbekannte Standorte und neuere Katalogversionen werden abgewiesen. Wahl-/Profilformat und Datenbankschema ändern sich nicht.

Workspace v2, Transportmanifest v2 und Planungsdokument v1 behalten ihre eigene Struktur und enthalten den unabhängig versionierten Katalog v3. Ihre Katalog-Schemavarianten wurden ergänzt. Die bisherigen Schreibformen mit eingebettetem v2-Katalog sind unverändert als Legacy-Fixtures eingefroren; aktuelle Fixtures halten v3 fest. Kataloge in append-only Datenbankzeilen werden beim Lesen umgewandelt, niemals rückwirkend umgeschrieben. Markierungen nach einer verbindlichen Planungsentscheidung werden als weiterer vollständiger Katalog-Snapshot gespeichert; es entstehen keine zusätzlichen Schülerrevisionen.

## Prüfung und Betriebsgrenzen

Regressionstests prüfen alte und neue Dateiverträge, die unveränderte Regelbewertung, Speicherung nach Kursstreichung, Wiederöffnen und unveränderte Wahlhistorie. Die Layoutprüfung verwendet ausschließlich synthetische Personen und alle sechs PDF-Ausgabearten sowie normale und mehrseitige Word-Sammelbögen. Tatsächliche Läufe und Fehler stehen im [Testprotokoll](TESTPROTOKOLL.md).

Der direkte PDF-/Word-Export benötigt weder Microsoft Office noch LibreOffice. Word wurde ausschließlich als Prüfhilfe eingesetzt, weil der gebündelte DOCX-Prüfruntime hier kein LibreOffice enthält. Papierdruck, fremde PDF-Programme und Verhalten auf anderen Betriebssystemen bleiben Teil der jeweiligen Erprobung.
