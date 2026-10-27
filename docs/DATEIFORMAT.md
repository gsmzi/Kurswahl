# JSON-Verträge: Wahl v2, Katalog v1

Maschinenlesbare Schemata: `schemas/course-choice.v2.schema.json` und `schemas/offer-catalog.v1.schema.json` (JSON Schema 2020-12). UTF-8, camelCase-Eigenschaften, Enumwerte in snake_case. Alle modellierten Felder sind erforderlich; unbekannte Felder, ganzzahlige Enums, fehlende Felder, null statt Pflichtwerten und unbekannte Schemaversionen werden abgewiesen. Maximal 2 MiB pro Wahl und JSON-Tiefe 32. Zusätzliche strukturelle Grenzen prüft `ModelGuard` (eindeutige Kennungen, Zeitfolge, Halbjahre, Wochenstundenbereich). Schulische Angebotsgrenzen sind Regeln mit ID in `GsmKurswahl.Rules` (siehe [Regelkatalog](RECHTSQUELLEN.md)). Das JSON-Schema ersetzt keine Fachprüfung.

`CourseChoice`: stabile `documentId` und `studentId`, Jahrgang, Angebots-/Regelversion, monoton steigende Speicherrevision, Erzeugungs-/Änderungszeit in UTC, Abgabephase/-status und vollständige Belegungen. Die Demo-ID ist weder Identitätsnachweis noch Schulberechtigung. Voraussetzungen, Diagnosen, Kontokennungen und behauptete Validität gehören nicht in diese Datei. `submitted`/`final` sind im Austauschmodell für spätere Meilensteine vorgesehen, werden aber vom M1-Editor nicht angenommen oder erzeugt.

Belegung: Angebots-ID plus Fach-ID, Kursart, Variante und Halbjahre. Die redundanten Angaben dienen dem nachvollziehbaren Austausch; sie werden gegen den Katalog geprüft. Jede Angebots-ID ist pro Wahl eindeutig. Unterrichtsgruppen und Lehrkräfte sind keine Wahleigenschaften.

Katalog: Version, Abiturjahrgang, explizites `isDemo`, Fächer und Angebote. Jedes Angebot hat belegbare Halbjahre mit konfigurierten Wochenstunden, Status und typisierte Voraussetzungen. Der Demo-Katalog ist keine Behauptung einer schulischen Freigabe. Italienisch-Stunden sind Platzhalter bei gesperrtem `planned`-Angebot. Zusätzliche LF/BF im Demo-Katalog sind synthetische Konfiguration; vollständig bestätigter GSM-Katalog folgt in M2.

Dateischicht: Vor dem Speichern exklusive `.lock`-Datei öffnen und SHA-256-Inhaltstoken des zuletzt gelesenen Stands vergleichen. Ohne Token nur neue Datei anlegen. Bestehende Identität und Revisionsfolge dürfen sich nicht unbemerkt ändern. In demselben Verzeichnis temporär schreiben, flushen, anschließend kontrolliert ersetzen; vorheriger Stand bleibt als `.bak`. Ein Abbruch vor dem Austausch erhält die aktive Datei. SHA-256 dient nur der Konflikterkennung, nicht als Signatur. Fremdprogramme können kooperative Sperren umgehen; SMB-/Ausfallgarantien sind im Pilot zu prüfen.

Speicherort: Das konfigurierte Home-Verzeichnis muss existieren und wird nie angelegt; angelegt wird höchstens der Unterordner darunter. Fehlt das Home-Verzeichnis (oder ohne konfiguriertes Home das Laufwerk/die Freigabe), melden Laden und Speichern „Speicherort nicht erreichbar“, statt einen leeren Entwurf vorzutäuschen. Verschwindet eine bereits geladene oder gespeicherte Datei, bleibt der geöffnete Entwurf erhalten; das nächste Speichern legt die Datei nur neu an und kann keine inzwischen anderweitig entstandene Datei ersetzen.

Autosave überschreibt den aktuellen Entwurf mit neuer Speicherrevision und genau einer Vorgängersicherung. Dies ist keine Lehrer-Audithistorie. Nach unverändertem Speichern entsteht keine neue Revision. Bewusste Abgaben und append-only Lehrerrevisionen werden erst später implementiert. `ChoiceRevision` modelliert dafür Vorgänger, Akteur, Quelle, Grund, Vollsnapshot und strukturierten Unterschied.

Konfiguration: `appsettings.json` neben der Schüler-EXE, alternativ absolute Datei über `GSM_KURSWAHL_SETTINGS`. `homeDirectory` ist `{HOME}` (aktuelles Betriebssystem-Home) oder absolut; `choiceDirectory` ist relativer Unterordner; `choiceFileName` einfacher JSON-Dateiname, nicht `profil.json`. `catalogPath` ist relativ zur Konfigurationsdatei oder absolut. Pfade werden nicht gegen das aktuelle Shell-Arbeitsverzeichnis aufgelöst. Produktive Pfad-/Identitätsfreigabe und Rechte folgen im Schulnetzpilot.

## Wahl-Schema v2 (25.10.2026)

Aktuelles Schema: `schemas/course-choice.v2.schema.json`; Katalog und Einstellungen bleiben v1. Das neue erforderliche Feld `plannedOralSubjects` enthält null bis zwei verschiedene Fachkennungen als JSON-Array (leer: noch keine Planung), keine verbindliche Prüfungsanmeldung. Schema v1 bleibt zur Dokumentation erhalten, wird vom neuen Editor aber wie unbekannte Versionen abgewiesen. Keine automatische Migration; der Ladefehler sperrt Speichern und erhält die vorhandene Datei. Für neue Demoentwürfe einen neuen Dateinamen konfigurieren; alte Dateien aufbewahren.

## M2 (26.10.2026): neue Profilgrenze, unveränderte Wahlschemas

Wahlschema v2 und Katalogschema v1 bleiben strukturell unverändert. Geänderter Regelstand `m2-1`, Kataloginhalt `demo-2029-v2`: bisherige Wahlentwürfe werden bei Versionsabweichung weiterhin geschützt abgewiesen. Es gibt keine automatische Migration, kein Zurücksetzen vorhandener Dateien und keine Änderung fremder Home-Dateien. Das aktualisierte Sample in einem separaten Testordner verwenden.

Neu: `student-profile.v1.schema.json` und `profile-signature.v1.schema.json`. Profilv1 ist erstmals implementiert; die englischen JSON-Feldnamen entsprechen dem bestehenden C#-Modell, nicht dem unverbindlichen deutschen Beispiel im Pflichtenheft. SID (`accountId`) ist für die Windows-Vertrauensprüfung zusätzlich verpflichtend. Weitere Details, Algorithmus und Schlüsselrotation: [PROFILE.md](PROFILE.md). Keine Vorbildung im Wahl-JSON; unbekannte Mitglieder werden weiterhin abgewiesen.

Der HTML-Kontrollbogen ist ein abgeleiteter Export ohne Rückimport. Er enthält keine Signatur und keine Berechtigungswirkung. Zeitstempel verwenden im Bogen ISO-8601 mit Offset.

### Nachfolgende Nutzerkorrektur: PDF-only / Sprachen

Der oben beschriebene HTML-Export ist ersetzt: ausschließlich direkte PDF-Datei, kein HTML und keine Druckfunktion. Kataloginhalt `demo-2029-v3` enthält Französisch und Latein LF/BF; Regelversion `m2-2`. JSON-Struktur bleibt unverändert. PDF ist ein abgeleiteter Export ohne Rückimport oder Berechtigungswirkung.

## Lehrer-Arbeitsstand und Speicherort v1 (27.10.2026)

Neu und unabhängig von den unveränderten Wahl-/Profil-/Katalog- und SQLite-Schemata: `schemas/teacher-workspace.v1.schema.json` und `schemas/teacher-location.v1.schema.json`. Beide verlangen `schemaVersion: 1`; unbekannte Versionen werden abgelehnt, nicht migriert.

`workspace.json` enthält `activeYear` sowie `years`: vollständiges `catalog`, absoluten `databasePath`, `step` (offer/import/mapping/review/complete), `actor`, `certificateThumbprint` und `keyId`. Kein privater Schlüssel. Eindeutige Jahrgänge, gültige aktive Referenz, absolute Pfade, Demo-Katalog und definierte Einrichtungsschritte werden zusätzlich beim Laden geprüft. Maximal 4 MiB beim Lesen. Die Auswahl des aktiven Jahrgangs erfolgt ausdrücklich, nicht kalenderabhängig.

`location.json` im lokalen Standard-Datenordner enthält das ausdrücklich ausgewählte absolute Verwaltungsverzeichnis (`directory`). Ohne diese Datei gilt der Standardordner. Ist ein gemerkter Ordner nicht erreichbar, kein automatischer Rückfall auf einen leeren Standardbestand.

Beide Dateien werden über temporäre Dateien im jeweiligen Zielverzeichnis, Flush und kontrollierten Austausch geschrieben. Exklusive kooperative Sperre und Inhaltsvergleich verhindern stilles Überschreiben konkurrierender Änderungen. Fehlende registrierte Datenbanken werden beim Öffnen abgewiesen. Der komplette Arbeitsstand gehört zusammen mit allen referenzierten Datenbanken ins geschützte Backup; bei eingebundenen alten Beständen liegen diese ggf. außerhalb des Verwaltungsverzeichnisses. Für Backup Anwendung vorher schließen.

### Vereinfachter Import/Assistent

Keine Änderung der JSON-Strukturen oder Schema-Versionen. Neue Jahrgänge starten weiterhin bei `offer`, danach `import` und `mapping` (gemeinsame Verteilung). Bereits gespeichertes `review` öffnet ebenfalls die Verteilung; bestehende Daten werden nicht migriert. Der Tabellenadapter akzeptiert zusätzlich `firstName`, `lastName` und `folder`. Vor Bestätigung kann das bestehende Home-Feld einen relativen Pfad `Klasse/Nachname.Vorname` enthalten; nach Verteilerbestätigung wird der absolute geprüfte Pfad als neue Stammdatenrevision gespeichert.
