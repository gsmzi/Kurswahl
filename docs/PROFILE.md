# Schülerseitige Profilprüfung (Stand: 27.10.2026 – ohne Zertifikats-/SID-Pflicht)

Der Schülerclient liest `profil.json` im gleichen konfigurierten Verzeichnis wie die Wahl (`aktuell.json`). Er schreibt diese Datei niemals. Das Profilschema v1 ist unabhängig vom Wahlschema v2 und Katalogschema v1. Auf eine separate Zertifikatssignatur (`profil.sig`), `profile-trust.json` und die Pflicht zur Windows-SID-Zuordnung wird gemäß Nutzerentscheidung vom 27.10.2026 verzichtet.

## Austauschvertrag

`profil.json`: UTF-8-Bytes, Modell `StudentProfile`, Schema `schemas/student-profile.v1.schema.json`, maximal 64 KiB. Minimierte Verwaltungsbestätigungen, keine Diagnose oder Begründung. `continuedLanguages` bestätigt die rechtlich geeignete fortgeführte Sprachlaufbahn; `italianProfile=yes` allein bestätigt das nicht. Leere/unbekannte Angaben werden nicht zu `yes`. `sportExemptSemesters=null` bedeutet unbekannt, `[]` ausdrücklich keine Befreiung. Die beiden Religion/Ethik-Freigaben sind administrative Entscheidungen für Belegung bzw. Prüfung; sie sind nicht aus einer Zeichenfolge zur Klasse-10-Vorbildung herleitbar. Doppelte JSON-Eigenschaften und unbekannte Schemas werden abgewiesen.

## Verbindliche Prüfung im Lehrerbestand

`profil.json` dient im Schülerprogramm ausschließlich als Arbeitshilfe zur Anzeige der persönlichen Voraussetzungen und zur Vorprüfung der wählbaren Kurse. Da Dateien im Schülerordner durch den Schüler verändert werden könnten, vertraut das Lehrerprogramm niemals einer `profil.json` oder `aktuell.json`, sondern prüft jede eingesammelte Wahl (und später den Export für ASV) verbindlich gegen die lokalen Stammdaten in `verwaltung.db`.

## Fehler, Aktualisierung und M3-Verteilung

Fehlendes Profil: `Missing` / `needs_review`. Defektes JSON, abweichende Schüler-ID, falscher Jahrgang, ungültige Revision oder ungültiges Schema: `ProfileInvalid` / `profile_invalid`; alle Voraussetzungen werden auf unbekannt gesetzt. Der Entwurf bleibt ladbar und unverändert. Im Programmstart wird das Profil einmal eingelesen; nach Verteilung einer neuen Revision neu starten. Revision und Ausstellungsstand stehen in den Profildetails und auf dem Kontrollbogen.

Erzeugung und Einzel-/Sammelverteilung von `profil.json` sind in der getrennten Komponente `AdministrationStorage` implementiert, die vom Schülerprogramm nicht referenziert wird. Details zur Verteilung und Wiederholung: [Lehrerverwaltung](LEHRERVERWALTUNG.md).
