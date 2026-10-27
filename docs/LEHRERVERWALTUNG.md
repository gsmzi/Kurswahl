# Lehrerverwaltung – M3

Stand: 27.10.2026. Technischer Erprobungsbetrieb ausschließlich mit synthetischen `demo-`-IDs und Demo-Katalog. Kein Schulnetzpilot und keine Freigabe für Echtdaten. Das Starten der Lehrer-EXE ist keine Autorisierung: Schutz von Datenbank, Zertifikatschlüssel und Installationsverzeichnis erfolgt durch administrativ geprüfte Betriebssystemrechte. Die manuelle Bearbeiterkennung dient im Demoablauf der Nachvollziehbarkeit, nicht der Authentifizierung.

## Start und Bestand

`dotnet run --project src/GsmKurswahl.TeacherApp -c Release --no-build --no-restore`

Beim ersten Start führt die Einrichtung durch Fachauswahl, Schülerliste und gemeinsame Verteilung. Danach öffnet sich automatisch die Aufgabenübersicht des zuletzt aktiven Jahrgangs. Angefangene Einrichtungen werden beim gespeicherten Schritt fortgesetzt. Der vollständige Ablauf und die Standardablage sind unten unter **Jahrgangsbezogener Einstieg und Einrichtung** beschrieben.

SQLite bleibt lokal und durch eine exklusive Begleitdateisperre gegen zwei schreibende Verwaltungsprozesse geschützt. Im Schülerbestand lassen sich Name/Klasse/ID, Prüfstatus und Nachwahl kombinieren. `invalid` führt zu Nachwahlbedarf; offene Freigaben bleiben `needs_review`. Bestätigte Änderungen werden sofort im Lehrerbestand gespeichert.

## Klasse-10-Import

CSV: UTF-8, Semikolon, Kopfzeile; Anführungszeichen und eingebettete Zeilenumbrüche unterstützt. XLSX: genau ein Blatt, keine Formeln, nur Werte; gemeinsame/inline Zeichenfolgen und lückenhafte Zellbereiche unterstützt. Es handelt sich um einen allgemeinen Tabellenadapter, nicht um einen ASV-Adapter. Grenzen: 8 Millionen CSV-Zeichen, 16 MB entpackte XLSX-Daten, höchstens 10000 Datensätze, XLSX höchstens 100 Spalten. Mehrblattdateien und Formeln werden ausdrücklich abgewiesen.

Spaltenzuordnung über deutsch beschriftete Auswahllisten: rechts die passende Spaltenüberschrift der geladenen Tabelle wählen. Gleichnamige technische Spalten werden vorgeschlagen; optionale Angaben können ohne Zuordnung bleiben. Pflichtfelder sind mit * markiert, Tooltips erläutern Bedeutung und Werte. Die technischen Feldnamen in der folgenden Tabelle dienen nur als Referenz. Pflichtfelder: `studentId`, `class`, `year` sowie `name` oder beide Felder `firstName` und `lastName`. Weitere Zielfelder:

| Felder | Bedeutung/Werte |
| --- | --- |
| firstName, lastName, folder | Vor-/Nachname für `Klasse/Nachname.Vorname`; `folder` überschreibt optional den Schülerordnernamen innerhalb der Klasse. |
| username, home | Optionales schulisches Benutzerkonto; optional expliziter Wahlordner statt der Ableitung aus Namen. |
| italian, bridge, imp, bilingual | ja/nein bzw. yes/no, true/false, 1/0; leer/unknown/unbekannt bleibt unbekannt |
| religion | ETH, ER, KR oder leer |
| sport | Kommagetrennte Halbjahre 1–4; `keine`/`none` bestätigt keine Befreiung; leer/unknown bedeutet unbekannt |
| languages | Bestätigte fortgeführte Sprachlaufbahnen als kommagetrennte Fachkennungen |
| religionPermission, religionExamPermission | Minimierte administrative Bestätigungen für Belegung/Prüfung; keine Ableitung aus dem Namen des Fachs |
| permanentSport | Dauerhafte Sportbefreiung, dreiwertig wie oben |
| departure | aktiv, voraussichtlich, bestaetigt; ohne sensible Begründung |

Die tabellarische Vorschau wählt alle fehlerfreien Zeilen vor. Eine gemeinsame Bestätigung übernimmt sie, auch beim Nachimport. Geänderte Felder sind gelb markiert; Tooltip zeigt bisherigen und vorgeschlagenen Stand. Fehler bleiben ausgeschlossen. Bei gleichbleibender Herkunft werden nachträgliche Verwaltungskorrekturen geschützt. Inhaltlich unveränderte Zeilen erzeugen auch in geänderten Dateien keine Revision. Bereits aufgelöste Ordner bleiben bei unverändertem relativen Ziel erhalten. Änderungen an Konto/Ordner heben die Zuordnungsbestätigung auf. Wahlhistorien bleiben erhalten. Falls einzelne Speicherungen scheitern, nennt die Anzeige Teilerfolge und abgelehnte Personen; keine behauptete Gesamttransaktion.

`samples/klasse10-demo.csv` enthält synthetische Vor-/Nachnamen und einen abweichenden Ordnernamen. Eine Windows-SID oder Zertifikatseinrichtung wird nicht benötigt. Home bezeichnet den vorhandenen Ordner für `profil.json` und `aktuell.json`. Fehlende Ordner werden nicht erzeugt.

## Gemeinsame Verteilung und automatisches Einsammeln

Nach Fachauswahl und Import nur Laufwerk/Grundverzeichnis mit den Klassenordnern angeben. „Ziele prüfen“ zeigt die vollständigen Zielpfade gemeinsam. Die Sammelbestätigung bestätigt die angezeigten Ordnerzuordnungen und erstellt/verteilt `profil.json` für alle bereiten Personen. Nicht vorhandene Ordner, doppelte Ziele, Verknüpfungen und Pfade außerhalb des gewählten Grundverzeichnisses bleiben offen. Die absoluten bestätigten Schülerpfade werden gespeichert; für das Einsammeln ist keine erneute Laufwerkswahl erforderlich.

Die technischen Einzelfunktionen für einzelne Profile und den Verteilungsbericht sind eingeklappt. Erfolgreiche Gesamtausgabe führt direkt zur Übersicht; „Später verteilen“ erlaubt eine Unterbrechung mit sichtbarer Restaufgabe.

Bei geöffneter App werden bestätigte Schülerordner beim Start, nach der Verteilung und alle 60 Sekunden gelesen. Neue konfliktfreie Wahlen werden mit Herkunft und vollständiger Revision übernommen, auch wenn die fachliche Prüfung Hinweise enthält. Fachliche Hinweise bleiben in der Schülerübersicht; Übernahme ist keine Freigabe. Lehreränderungen, abweichende Dokument-IDs und alte Schülerrevisionen werden nicht automatisch überschrieben. Unter „Kurswahlen einsammeln“ sind Konflikte und Lesefehler sichtbar, mit manueller Konfliktentscheidung. Bei geschlossener App läuft kein Dienst.

## Profilerzeugung und Verteilung (ohne Zertifikats-/SID-Pflicht)

Auf Zertifikatssignierung (`profil.sig`), Windows-Zertifikatsspeicher, öffentliche Prüfschlüssel (`profile-trust.json`) und Windows-SID-Zuordnung wird verzichtet. Das Lehrerprogramm erzeugt pro Person eine fortlaufende Profilrevision (`profil.json`) im geschützten Bestand und verteilt diese in den bestätigten Wahlordner.

Unter „Einzelne Profile und Verteilungsbericht“ lassen sich Profile auch gezielt einzeln erstellen („1 · Neue Profile erstellen“) und zustellen („2 · Profile an Wahlordner verteilen / erneut zustellen“). Eine Wiederholung benötigt keine neue Profilversion und erzeugt keine Wahlrevision. Änderungen der Vorbildung machen die alte Ausgabe unbrauchbar; erneut erstellen und höhere Revision verteilen. Verteilungsbericht dauerhaft pro Person/Revision: `verteilt`, `nicht_gefunden`, `Zugriff_verweigert`, `Profil_abweichend`, `veraltet`.

Der Austausch benutzt eine temporäre Datei im Zielverzeichnis, eine kooperative Verteilungssperre (`.profil.lock`) und einen Vergleich des vorgefundenen Inhalts. Defekte, fremde oder unbekannt versionierte Profile werden nicht überschrieben. Wahl-Dateien (`aktuell.json`) werden beim Verteilen nicht verändert.

## Einsammeln, Korrektur und Nachwahl

Die Sammelvorschau liest `aktuell.json` aus jedem bestätigten Verzeichnis. Herkunft, Schüler-ID, Jahrgang und Kontozuordnung müssen übereinstimmen. Die Anwendung nutzt ausschließlich aktuelle Vorbildung aus dem Verwaltungsbestand; auch ein gültiges altes Profil kann diese nicht ersetzen. Eine administrativ bestätigte Herkunft ist hier eine geschützte Zuordnung, keine zusätzliche LDAP- oder Onlineprüfung. Das Schulnetz muss diesen Vertrauensanker durch ACLs absichern.

Vorschau zeigt Fehler, bereits übernommene Inhalte und Konflikte. Nach Lehreränderungen sowie bei abweichender Dokument-ID oder alten Schülerrevisionen ist eine ausdrückliche Konfliktentscheidung nötig. Ein erneutes Einlesen desselben Inhalts erzeugt keine neue Wahlrevision. Änderungen des Bestands seit Vorschau blockieren die Übernahme. Teilerfolge im Sammellauf bleiben erhalten, die Ergebnisanzeige nennt abgelehnte Personen.

Im Personenbereich lassen sich neben Kontozuordnung und Ordner auch sämtliche persönlichen Voraussetzungen (`PriorEducation`: Italienisch-Profil, Informatik-Brückenkurs, IMP, durchgehend bilingual, Religion/Ethik Klasse 10, Belegungs-/Prüfungsgenehmigung Religion/Ethik, befreite Sport-Halbjahre, dauerhafte Sportbefreiung, fortgeführte Fremdsprachen) sowie der Abgangsstatus (`DepartureStatus`) direkt bearbeiten und speichern. Zudem lassen sich Halbjahre pro Angebot und die mündliche Planung bearbeiten und mit Änderungsgrund als neue Lehrerrevision speichern (unter Aktualisierung von Angebots-/Regelversion und Revisionsnummer). Die ursprüngliche Schülerabgabe bleibt als eigener Snapshot erhalten. Historie zeigt Quelle, Bearbeiter, Zeit, Anlass, geplante mündliche Prüfungsfächer und Halbjahresunterschiede. Wiederherstellen erstellt eine neue Revision mit Verweis auf den vorher aktiven Stand. Aktuelle Validierung und Nachwahlbedarf werden beim Anzeigen aus dem derzeitigen Stammdaten- und Katalogstand neu berechnet. Offene Konflikte beim Einsammeln werden auch auf der Jahrgangsübersicht gemeldet und lassen sich in „Kurswahlen einsammeln“ gezielt übernehmen oder zugunsten des Lehrerstands verwerfen.

Nachwahl kann mit Begründung und Frist vorgemerkt werden. Abschluss ist nur bei `valid` möglich. Solange der gemeinsame Regelkern die fachliche Freigabe für 2029 als offen führt, wird kein künstlicher gültiger Abschluss erzeugt. Verbindliche Endwahlabgabe bleibt bis zur geklärten Termin-/Freigabekonfiguration gesperrt.

## Angebot vor der Ausgabe

Die Fachauswahl erfolgt im Einrichtungsassistenten und wird im aktuellen Jahrgang gespeichert. Unter „Jahrgang verwalten“ kann das aktuelle Fachangebot angesehen und unverändert für die Schülerinstallation exportiert werden. Solange keine Verwaltungswahlen vorliegen, kann die Fachauswahl erneut geöffnet werden. Mit bestehenden Wahlen bleibt dieser Weg gesperrt; kontrollierte spätere Angebotsänderungen gehören zu M4.

## Datenformat und Sicherung

SQLite `PRAGMA user_version=1`; unbekannte Versionen, nichtleere unversionierte und beschädigte Datenbanken werden abgewiesen. Keine automatische Migration. Tabellen: `students` (vollständige Stammdaten je Version), `revisions` (vollständige Wahl-Snapshots samt Herkunft und Änderungen), `reselections` (Nachwahlereignisse), `profiles` (ausgestellte exakte Bytes und Signatur), `distributions` (Verteilungsereignisse). UPDATE/DELETE-Trigger schützen diese Tabellen im normalen Betrieb; sie ersetzen keine ACLs und keinen forensischen Vertrauensanker.

Neue interne Stammdatenstruktur `StudentRecord.schemaVersion=1`, unabhängig von Wahlschema 2, Katalogschema 1 und Profilschema 1. Bestehende Austauschschemata unverändert. Domänenmodelle und gemeinsamer JSON-Vertrag liegen ohne Dateisystem in Domain; AdministrationStorage hängt nur von Domain ab. SQLite und Signierung werden nur vom Lehrerprogramm referenziert.

Vor Sicherung Anwendung schließen; konsistenten lokalen Bestand in den geschützten Backupbereich kopieren und Wiederherstellung an separater Kopie prüfen. Kein laufendes SMB-Sharing und kein automatischer Datenbankersatz. Aufbewahrung/Löschung, Rechte, Zertifikatbereitstellung, Schuldomänen-/SMB-Verhalten sowie native Bedienung/DPI/Screenreader sind noch im Betriebsprozess zu bestätigen. Kein Deployment durch diesen Entwicklungsschritt.

## Orientierung in der Oberfläche (27.10.2026)

Die unter **Hilfe** erreichbare Dateiorientierung erklärt den Ablauf sowie Herkunft, Inhalt und Verwendung aller Dateien. Aufklappbare Hilfen unterscheiden Schülerlisten (.xlsx/.csv), Fachangebot (.json), lokalen Lehrerbestand (.db), persönliche Profile (profil.json + profil.sig), Kurswahlen (aktuell.json), PDF-Kontrollbogen und technische Einrichtungsdateien. Eine eigene Hilfe ordnet die vorhandenen Testdateien zu.

Die Arbeitsbereiche sind über die Aufgabenübersicht und Jahrgang verwalten erreichbar. Jede Import-/Ausgabeseite erklärt die Aufgabe vor den nummerierten Aktionen. Dateidialoge filtern passende Endungen; die Inhalts-/Schemaprüfung bleibt maßgeblich, weil verschiedene JSON-Dateien dieselbe Endung haben. Die Signiereinrichtung liegt aufklappbar unter „Signierung einrichten · Schul-IT“. Tooltips ergänzen die sichtbaren Hinweise an Aktionen, Spaltenauswahl und Kontozuordnung; zentrale Erklärungen sind auch ohne Maus aufklappbar erreichbar.

## Jahrgangsbezogener Einstieg und Einrichtung (27.10.2026)

Ablauf ab dem ersten Start:

1. Beim ersten Start Jahrgang (Vorschlag 2029) und synthetische Bearbeiterkennung bestätigen. Das mitgelieferte Fachangebot dient als Ausgangspunkt; keine Katalogdateiauswahl nötig.
2. Leistungsfächer auswählen und bestätigen.
3. Schülerliste laden, tabellarische Vorschau prüfen und alle gewünschten Zeilen gemeinsam übernehmen.
4. Laufwerk wählen, Ziele gemeinsam prüfen und Schülerdateien verteilen. Danach öffnet sich die Übersicht. Weitere Wahlen werden automatisch eingesammelt.

Nach Abschluss öffnet sich automatisch die **Übersicht des aktiven Abiturjahrgangs**. Sie zeigt Schülerzahl, aktuelle erfolgreiche Profilzustellungen laut letztem Bericht, eingelesene Wahlen, Nachwahlen und Klärungsfälle. „Als Nächstes“ priorisiert offene Zuordnungen, fehlende/überholte Profile, Zustellprobleme und ausstehende Kurswahlen. Daneben bleiben Schülerprofile bereitstellen, Kurswahlen einsammeln, Schülerbestand bearbeiten und Schülerliste aktualisieren direkt erreichbar. Eine frühere erfolgreiche Zustellung wird nach neuerem Fehler oder Vorbildungskorrektur nicht als aktueller Erfolg gezählt; die Anzeige ist keine Live-Prüfung der Schülerordner.

**Unterbrechen:** Jahrgang, Fachauswahl, bestätigte Importe, Zuordnungen und Einrichtungsstufe bleiben erhalten. Nach Neustart öffnet sich der gespeicherte Schritt. Noch nicht bestätigte Importvorschauen werden absichtlich nicht übernommen; zum Fortsetzen die Quelldatei erneut laden. Bereits übernommene Zeilen bleiben erhalten und werden über den vorhandenen Wiederholungsschutz erkannt.

**Speicherort:** Standard ist der lokale Benutzer-Datenordner `LocalApplicationData/GSM-Kurswahl/Lehrerverwaltung`. Darin verwaltet die App `workspace.json` und pro neuem Jahrgang `Abitur-<Jahr>/verwaltung.db`. Das vollständige aktive Fachangebot ist im Arbeitsstand gespeichert. Unter **Einstellungen** lässt sich ein anderes vorhandenes lokales Verzeichnis wählen, auch ein beschreibbarer Ordner neben dem Programm. Diese Auswahl wird in `location.json` im Standardordner für den nächsten Start gemerkt. Es werden dabei keine Daten verschoben oder kopiert. UNC-/Netzlaufwerke bleiben für den Verwaltungsbestand gesperrt.

**Bestehender M3-Bestand:** Unter Einstellungen einmalig „Vorhandenen Lehrerbestand einbinden“ wählen, bisherigen Katalog und vorhandene lokale Datenbank zuordnen. Die Datenbank bleibt am bisherigen Ort; Historie und Profile bleiben erhalten. Der Assistent führt ab der Schülerliste zur Überprüfung und zum Abschluss; vorhandene Personen müssen nicht erneut importiert werden. Ein bereits registrierter Jahrgang wird nicht ersetzt.

**Jahrgang verwalten:** Zwischen vorhandenen Jahrgängen wechseln oder einen weiteren anlegen. Der aktive Jahrgang wird ausdrücklich gespeichert und nicht aus dem Kalenderjahr berechnet. Das aktuelle Fachangebot ist einsehbar und unverändert für die Schülerinstallation exportierbar. Eine erneute Fachauswahl ist nur möglich, solange für diesen Jahrgang keine Verwaltungswahlen vorliegen; danach bleibt der spätere kontrollierte Angebotsänderungsablauf erforderlich. Eine exportierte Datei überschreibt keine vorhandene Datei. In der Schülerinstallation den `catalogPath` auf genau diesen Katalog setzen, damit Angebotsversionen übereinstimmen.

**Signiereinstellungen:** Fingerabdruck und Schlüsselkennung können pro Jahrgang ausdrücklich gespeichert werden. Private Schlüssel werden weiterhin ausschließlich im administrativen Zertifikatspeicher verwahrt. Unter „Signierung einrichten · Schul-IT“ steht auch der Export des aktuellen Fachangebots bereit.

**Fehler:** Ein fehlender registrierter Lehrerbestand, ein defekter oder unbekannt versionierter Arbeitsstand, gesperrte Datenbank oder unerreichbarer gemerkter Speicherort erzeugt eine sichtbare Fehlerseite. Es wird kein leerer Ersatzbestand angelegt. Daten und Einstellungen werden nicht stillschweigend repariert. Bei konkurrierender Änderung des Arbeitsstands ist ein Neustart nötig.

## Gestaltung und Verlaufsanzeige (27.10.2026)

Die Lehreroberfläche verwendet eine helle Gestaltung mit klaren Arbeitsflächen, farbig hervorgehobenen Hauptaktionen, Fachkacheln und Kennzahlenkarten. Die grafische Verlaufsleiste zeigt **Fächer auswählen → Schülerliste importieren → Dateien verteilen** und ist direkt anklickbar, um ohne zusätzliche Navigationsbuttons darunter zwischen den drei Schritten zu wechseln. Der aktuelle Schritt, erledigte Schritte und offene Zustellungen sind jeweils mit Text und Symbol gekennzeichnet; das Anklicken wechselt die Ansicht, überspringt aber keine Bestätigung. Eine verschobene oder fehlgeschlagene Verteilung wird nicht als erledigt dargestellt. Fachliche Freigaben bleiben davon getrennt. Einstellungen und Hilfe behalten den bestehenden Einrichtungsstand; die aktive Seite ist in der Navigation hervorgehoben.

Die nächste Aufgabe steht in der Übersicht vor den Kennzahlen, damit sie auch in kleinen Fenstern sichtbar bleibt. Weitere Inhalte bleiben scrollbar. Die helle Farbgestaltung gilt nur im Lehrerfenster, nicht global für das Schülerprogramm.
