# Lehrerverwaltung – M3

Stand: 27.10.2026. Technischer Erprobungsbetrieb ausschließlich mit synthetischen `demo-`-IDs und Demo-Katalog. Kein Schulnetzpilot und keine Freigabe für Echtdaten. Das Starten der Lehrer-EXE ist keine Autorisierung: Schutz von Datenbank, Zertifikatschlüssel und Installationsverzeichnis erfolgt durch administrativ geprüfte Betriebssystemrechte. Die manuelle Bearbeiterkennung dient im Demoablauf der Nachvollziehbarkeit, nicht der Authentifizierung.

## Start und Bestand

`dotnet run --project src/GsmKurswahl.TeacherApp -c Release --no-build --no-restore`

1. Bearbeiterkennung eingeben; „Verwaltung öffnen …“ wählen. Zuerst den Jahrgangskatalog, dann eine neue oder bestehende lokale `.db`-Datei auswählen. Eine bestehende Datenbank wird geöffnet, nicht ersetzt. Der Katalog `samples/fachangebot-demo.json` ist für den synthetischen Jahrgang 2029 vorbereitet.
2. Eine exklusive Begleitdateisperre verhindert zwei schreibende Verwaltungsprozesse. UNC-Pfade und Netzlaufwerke werden für SQLite abgewiesen. Eine liegengebliebene `.lock`-Datei nach einem Absturz ist kein dauerhaftes Schloss; entscheidend ist der offene Dateihandle. Rechte und lokale Ablage sind vor Echtdatenbetrieb zu prüfen.
3. Die Übersicht filtert kombinierbar nach Name/Klasse/ID, Prüfstatus und Nachwahl. Der Jahrgang stammt aus dem geöffneten Katalog. `invalid` führt zu Nachwahlbedarf; offene fachliche Freigaben bleiben `needs_review`. Ungeprüfte Wahl ist nicht eingereicht und nicht rechtsverbindlich freigegeben.

## Klasse-10-Import

CSV: UTF-8, Semikolon, Kopfzeile; Anführungszeichen und eingebettete Zeilenumbrüche unterstützt. XLSX: genau ein Blatt, keine Formeln, nur Werte; gemeinsame/inline Zeichenfolgen und lückenhafte Zellbereiche unterstützt. Es handelt sich um einen allgemeinen Tabellenadapter, nicht um einen ASV-Adapter. Grenzen: 8 Millionen CSV-Zeichen, 16 MB entpackte XLSX-Daten, höchstens 10000 Datensätze, XLSX höchstens 100 Spalten. Mehrblattdateien und Formeln werden ausdrücklich abgewiesen.

Spaltenzuordnung über deutsch beschriftete Auswahllisten: rechts die passende Spaltenüberschrift der geladenen Tabelle wählen. Gleichnamige technische Spalten werden vorgeschlagen; optionale Angaben können ohne Zuordnung bleiben. Pflichtfelder sind mit * markiert, Tooltips erläutern Bedeutung und Werte. Die technischen Feldnamen in der folgenden Tabelle dienen nur als Referenz. Pflichtfelder: `studentId`, `name`, `class`, `year`, `username`. Weitere Zielfelder:

| Felder | Bedeutung/Werte |
| --- | --- |
| accountId, home | Administrativ zu prüfende Windows-SID und absolutes persönliches Wahl-/Profilverzeichnis |
| italian, bridge, imp, bilingual | ja/nein bzw. yes/no, true/false, 1/0; leer/unknown/unbekannt bleibt unbekannt |
| religion | ETH, ER, KR oder leer |
| sport | Kommagetrennte Halbjahre 1–4; `keine`/`none` bestätigt keine Befreiung; leer/unknown bedeutet unbekannt |
| languages | Bestätigte fortgeführte Sprachlaufbahnen als kommagetrennte Fachkennungen |
| religionPermission, religionExamPermission | Minimierte administrative Bestätigungen für Belegung/Prüfung; keine Ableitung aus dem Namen des Fachs |
| permanentSport | Dauerhafte Sportbefreiung, dreiwertig wie oben |
| departure | aktiv, voraussichtlich, bestaetigt; ohne sensible Begründung |

Die Vorschau zeigt alle zu übernehmenden Angaben und bei bestehenden Personen auch den aktuellen geschützten Bestand. Fehler und Dubletten werden angezeigt. Jede Zeile wird ausdrücklich einzeln übernommen; bestehende Personen erfordern eine bewusste Korrekturentscheidung. Der Import setzt die Kontozuordnung auf ungeprüft. Identischer Dateiinhalt mit identischem Mapping erzeugt keine neue Stammdatenrevision und überschreibt spätere Korrekturen nicht. Geänderter Inhalt oder Mapping erfordert erneut die Einzelentscheidung. Wahlhistorien bleiben immer erhalten.

Für den ersten Versuch kann `samples/klasse10-demo.csv` verwendet werden. SID und Zielverzeichnis sind absichtlich nicht gesetzt. Nach Import eine Person öffnen, schulisch geprüfte Konto-/SID-/Home-Zuordnung eintragen und ausdrücklich bestätigen. **Home bezeichnet hier das bereits vorhandene Verzeichnis für profil.json, profil.sig und aktuell.json**, nicht ein automatisch anzulegendes Netzlaufwerk. Leere oder nicht erreichbare Ziele werden niemals automatisch angelegt. Doppelte IDs, Konten, SIDs und Zielverzeichnisse werden zurückgewiesen.

## Signierung, Schlüssel und Verteilung

Das Lehrerprogramm verwendet ein administrativ bereitgestelltes P-256-Zertifikat mit zugänglichem privaten Schlüssel aus dem Windows-Zertifikatspeicher **Aktueller Benutzer / Eigene Zertifikate**. Es erzeugt oder exportiert keinen privaten Schlüssel. Im Reiter „Profile ausgeben“ Zertifikat-Fingerabdruck und eigene eindeutige Schlüsselkennung angeben. Ein Zertifikat allein begründet keine Lehrerberechtigung; Zugriff auf den privaten Schlüssel muss auf berechtigte Verwaltungsaccounts beschränkt sein.

„Öffentlichen Prüfschlüssel … exportieren“ erzeugt ausschließlich ein `TrustedProfileKey`-Objekt. Dieses wird administrativ in `keys` der schreibgeschützten Schülerinstallation (`profile-trust.json`, Schema 1) aufgenommen. `allowedAccountDomains` muss separat den tatsächlich bestätigten schulischen Kontobereich enthalten. Kein privater Schlüssel und keine vollständige Schülerliste werden verteilt. Der Schülerclient vertraut nicht einer Konfiguration aus dem Home.

Vor der Signierung stehen Person, Konto, Ziel und Bestätigungsstatus in der Vorschau. Strg/Umschalt erlaubt Einzel- oder Mehrfachauswahl. „1 · Neue Profile erstellen und signieren“ erstellt pro Person eine neue fortlaufende Profilrevision im geschützten Bestand; die exakten UTF-8-Bytes werden mit ECDSA-P256/SHA-256/P1363 signiert. Unbekannte Zulassungsmerkmale bleiben unbekannt. Signierfehler einer Person werden protokolliert; die übrigen Personen werden weiterbearbeitet.

„2 · Profile an Wahlordner verteilen / erneut zustellen“ verteilt die zuletzt ausgestellte Version. Eine Wiederholung benötigt keine neue Signierung und erzeugt keine Wahlrevision. Änderungen der Vorbildung oder der Zuordnung machen die alte Ausgabe unbrauchbar; erneut prüfen, neu signieren und höhere Revision verteilen. Verteilungsbericht dauerhaft pro Person/Revision: `verteilt`, `nicht_gefunden`, `Zugriff_verweigert`, `Profil_abweichend`, `veraltet`.

Der Austausch benutzt temporäre Dateien im Zielverzeichnis, eine kooperative Verteilungssperre und einen Vergleich des vorgefundenen Inhalts. Defekte, fremde oder unbekannt versionierte Profile werden nicht überschrieben. Wahl-Dateien werden nicht geöffnet oder verändert. Die zwei Dateien können nicht als gemeinsame Dateisystemtransaktion ersetzt werden: Ein Abbruch zwischen Profil und Signatur führt beim Schüler zu `profile_invalid`, niemals zu einer falschen Berechtigung. Wiederholung desselben Ausgabestands repariert einen solchen gemischten Stand. Ein unabhängig beschädigtes Profil/Signaturdokument muss administrativ untersucht und separat gesichert werden; kein automatisches Überschreiben.

Rotation: neuen öffentlichen Prüfschlüssel zuerst in der geschützten Installation bereitstellen; danach neue Profilrevisionen mit neuer `keyId` signieren. Alten Prüfschlüssel erst nach geplantem Übergang entfernen. Schlüsselbackup ausschließlich im administrativen, verschlüsselten Sicherungsverfahren oder über die verwaltete Zertifikatsinfrastruktur; nie im Repository oder Schüler-Home. Der konkrete Provisionierungs-, Wiederherstellungs- und Rotationsprozess ist vor Echtdatenbetrieb noch freizugeben. Die automatisierten Tests nutzen nur flüchtige Schlüssel im Arbeitsspeicher; der reale Windows-Zertifikatspeicher wurde nicht verändert.

## Einsammeln, Korrektur und Nachwahl

Die Sammelvorschau liest `aktuell.json` aus jedem bestätigten Verzeichnis. Herkunft, Schüler-ID, Jahrgang und Kontozuordnung müssen übereinstimmen. Die Anwendung nutzt ausschließlich aktuelle Vorbildung aus dem Verwaltungsbestand; auch ein gültiges altes Profil kann diese nicht ersetzen. Eine administrativ bestätigte Herkunft ist hier eine geschützte Zuordnung, keine zusätzliche LDAP- oder Onlineprüfung. Das Schulnetz muss diesen Vertrauensanker durch ACLs absichern.

Vorschau zeigt Fehler, bereits übernommene Inhalte und Konflikte. Nach Lehreränderungen sowie bei abweichender Dokument-ID oder alten Schülerrevisionen ist eine ausdrückliche Konfliktentscheidung nötig. Ein erneutes Einlesen desselben Inhalts erzeugt keine neue Wahlrevision. Änderungen des Bestands seit Vorschau blockieren die Übernahme. Teilerfolge im Sammellauf bleiben erhalten, die Ergebnisanzeige nennt abgelehnte Personen.

Im Personenbereich Halbjahre pro Angebot und die mündliche Planung bearbeiten; mit Änderungsgrund als neue Lehrerrevision speichern. Die ursprüngliche Schülerabgabe bleibt als eigener Snapshot erhalten. Historie zeigt Quelle, Bearbeiter, Zeit, Anlass und Halbjahresunterschiede. Wiederherstellen erstellt eine neue Revision mit Verweis auf den vorher aktiven Stand. Aktuelle Validierung und Nachwahlbedarf werden beim Anzeigen aus dem derzeitigen Stammdaten- und Katalogstand neu berechnet.

Nachwahl kann mit Begründung und Frist vorgemerkt werden. Abschluss ist nur bei `valid` möglich. Solange der gemeinsame Regelkern die fachliche Freigabe für 2029 als offen führt, wird kein künstlicher gültiger Abschluss erzeugt. Verbindliche Endwahlabgabe bleibt bis zur geklärten Termin-/Freigabekonfiguration gesperrt.

## Angebot vor der Ausgabe

Im Reiter „Fachangebot“ Jahrgang und angebotene LF wählen. Eine neue eindeutige Angebotsversion und eine neue Datei sind zwingend. Der gemeinsame Regelkern prüft schulische Angebotsgrenzen. Der aktive Katalog und Verwaltungswahlen werden nicht verändert. Zur anschließenden Verwendung Bestand mit dem neuen Katalog öffnen; Schülerinstallation entsprechend konfigurieren. Eine produktive Angebotsstreichung mit Auswirkungen/Umwahl ist M4 und wird hier nicht vorweggenommen.

## Datenformat und Sicherung

SQLite `PRAGMA user_version=1`; unbekannte Versionen, nichtleere unversionierte und beschädigte Datenbanken werden abgewiesen. Keine automatische Migration. Tabellen: `students` (vollständige Stammdaten je Version), `revisions` (vollständige Wahl-Snapshots samt Herkunft und Änderungen), `reselections` (Nachwahlereignisse), `profiles` (ausgestellte exakte Bytes und Signatur), `distributions` (Verteilungsereignisse). UPDATE/DELETE-Trigger schützen diese Tabellen im normalen Betrieb; sie ersetzen keine ACLs und keinen forensischen Vertrauensanker.

Neue interne Stammdatenstruktur `StudentRecord.schemaVersion=1`, unabhängig von Wahlschema 2, Katalogschema 1 und Profilschema 1. Bestehende Austauschschemata unverändert. Domänenmodelle und gemeinsamer JSON-Vertrag liegen ohne Dateisystem in Domain; AdministrationStorage hängt nur von Domain ab. SQLite und Signierung werden nur vom Lehrerprogramm referenziert.

Vor Sicherung Anwendung schließen; konsistenten lokalen Bestand in den geschützten Backupbereich kopieren und Wiederherstellung an separater Kopie prüfen. Kein laufendes SMB-Sharing und kein automatischer Datenbankersatz. Aufbewahrung/Löschung, Rechte, Zertifikatbereitstellung, Schuldomänen-/SMB-Verhalten sowie native Bedienung/DPI/Screenreader sind noch im Betriebsprozess zu bestätigen. Kein Deployment durch diesen Entwicklungsschritt.

## Orientierung in der Oberfläche (27.10.2026)

Der zunächst geöffnete Reiter **Start & Dateien** erklärt den Ablauf sowie Herkunft, Inhalt und Verwendung aller Dateien. Aufklappbare Hilfen unterscheiden Schülerlisten (.xlsx/.csv), Fachangebot (.json), lokalen Lehrerbestand (.db), persönliche Profile (profil.json + profil.sig), Kurswahlen (aktuell.json), PDF-Kontrollbogen und technische Einrichtungsdateien. Eine eigene Hilfe ordnet die vorhandenen Testdateien zu.

Die Arbeitsreiter heißen Schülerbestand, Schülerlisten, Profile ausgeben, Wahlen einsammeln und Fachangebot. Jede Import-/Ausgabeseite erklärt die Aufgabe vor den nummerierten Aktionen. Dateidialoge filtern passende Endungen; die Inhalts-/Schemaprüfung bleibt maßgeblich, weil verschiedene JSON-Dateien dieselbe Endung haben. Die Signiereinrichtung liegt aufklappbar unter „Signierung einrichten · Schul-IT“. Tooltips ergänzen die sichtbaren Hinweise an Aktionen, Spaltenauswahl und Kontozuordnung; zentrale Erklärungen sind auch ohne Maus aufklappbar erreichbar.
