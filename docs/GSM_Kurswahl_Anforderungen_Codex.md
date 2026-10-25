# GSM-Kurswahl - Anforderungsspezifikation für Codex

**Gymnasium bei St. Michael, Schwäbisch Hall**  
**Stand:** 25.10.2026  
**Status:** fachlich abgestimmter Ausgangsentwurf, noch nicht rechtsverbindlich freigegeben  
**Ziel:** bisherige Winprosa-Kurswahl durch eine eigenständige, schulintern verwendete Desktopanwendung ablösen.

## 1. Auftrag an Codex und Entwicklungsprinzipien

Implementiere eine wartbare, deutschsprachige Kurswahlsoftware für die gymnasiale Kursstufe in Baden-Württemberg. Beginne mit einer lauffähigen Windows-Desktopanwendung; halte die Fachlogik und Dateispeicherung plattformneutral, sodass macOS später möglich ist. Die Anwendung wird zuerst mit synthetischen Testdaten entwickelt. Echte Namen, Listen, Laufwerkspfade, Zugangsdaten oder schulische Geheimnisse gehören nicht ins Repository.

Es sind zwei Anwendungen vorgesehen: **Schülerprogramm** für persönliche Kurswahlen und **Lehrerprogramm** für Imports, Validierung, Planung, Änderungen, Abgleiche und Auswertungen. Sie verwenden identische Domänenmodelle und dieselbe versionierte Regelprüfung. Keine doppelte Implementierung der Regeln.

**Priorität:** fachlich richtig vor optisch aufwendig. Ungeklärte Rechtsfragen erhalten einen sichtbaren Prüfstatus und werden nicht durch geratenen Code ersetzt. Bestehende Rechts- und Schulregeln nicht als freie Text-Heuristiken, sondern als testbare Regeln implementieren. Nach jeder Entwicklungsstufe soll ein startbares und getestetes Produkt vorliegen.

## 2. Technische Rahmenbedingungen

- Sprache: C# mit einer unterstützten .NET-LTS-Version; GUI: Avalonia UI.
- Zuerst Windows 11 x64. Spätere Portierung nach macOS (z.B. MacBook Neo) einplanen; iPadOS ist nicht Bestandteil des ersten Releases.
- Kein Anwendungsserver, keine Cloud, keine eigene LDAP-Anmeldung, kein Webserver und keine Online-Synchronisation in Version 1. Keine komplette Abiturorganisation, Notenverwaltung oder Untis-Stundenplanoptimierung in Version 1.
- Verteilung der Schüler-EXE über eine schulintern lesbare Netzwerkfreigabe oder lokale Bereitstellung. Selbstenthaltende Releases prüfen; keine lokalen Administratorrechte für den normalen Betrieb voraussetzen.
- Windows-Anmeldung ist die Ausgangsidentität. Username und gegebenenfalls SID über eine austauschbare Schnittstelle ermitteln. Nicht einer beliebig in JSON gespeicherten Benutzerkennung vertrauen. Individuelle, signierte Schülerprofile nach Abschnitt 5a bereitstellen.
- Schülerprogramm speichert die aktive Wahl als versioniertes JSON im persönlichen Home-Verzeichnis. Home-Pfad und Dateiname sind konfigurierbar, nicht im Code hart verdrahten.
- Lehrerprogramm liest Dateien aus den schülerspezifischen Home-Verzeichnissen über einen konfigurierbaren Lehrerzugriff (vermutlich S:, exakter Pfad folgt). Importpfad und Klassenstruktur werden später eingetragen; keinen konkreten Laufwerksbuchstaben annehmen.
- Der geschützte Lehrerbestand ist vom Schüler-JSON getrennt. Lokale SQLite-Datenbank für die Lehrerverwaltung ist möglich; SQLite nicht von mehreren Rechnern gleichzeitig über SMB als gemeinsame Datenbank öffnen. Zunächst genau ein schreibender Verwaltungsarbeitsplatz oder ein ausdrückliches Sperr-/Synchronisationskonzept.
- Datenaustausch: versionierte JSON-Schemata, CSV, XLSX und druckbare Kontrollbögen/PDF. ASV wird nicht direkt beschrieben.
- Quellcode, Beispieldaten, Regelkonfiguration und Testfälle in Git. Technische Vorgaben und Projektstruktur in `AGENTS.md`, Bedienung und Build-Befehle in `README.md`.

## 3. Rollen und Aufgaben

**Schüler:** Eigenes Wahlprofil öffnen, nur schulisch angebotene und persönlich zulässige Kursarten wählen, Warnungen verstehen, automatisch zwischenspeichern, bewusst abgeben, eigenen Kontrollbogen erstellen, bei Ersatzwahl eine neue Version abgeben.

**Oberstufenberatung / Lehrerversion:** Fachangebot je Abiturjahrgang konfigurieren, Klasse-10-Vorbildung importieren und prüfen, Schülerwahlen einsammeln, Änderungen vornehmen, Sonderfälle entscheiden, Kurse streichen oder simulieren, Nachwahl verwalten, ASV-Daten vergleichen, Planungsstände sichern, detaillierte Analyse und Exporte erstellen.

**Schulleitung:** Trifft außerhalb der Software Entscheidungen über das tatsächliche Kursangebot und genehmigungsbedürftige Sonderfälle. Die Anwendung darf Entscheidungen vorbereiten und dokumentieren, aber nicht fingieren.

## 4. Schulisches Fachangebot / feste GSM-Konfiguration

Das Angebot ist ein **eigens gepflegter Fächerkatalog pro Abiturjahrgang**, nicht die Liste aller theoretisch in Baden-Württemberg denkbaren Fächer. Der normale, an der Schule tatsächlich angebotene Fachkanon muss in der Konfiguration hinterlegt werden. Unbestätigte Fächer sind nicht stillschweigend freizuschalten. LF i. d. R. 5 Wochenstunden, BF in Sprachen/Deutsch/Mathematik/Naturwissenschaften i. d. R. 3, sonst i. d. R. 2; Abweichungen explizit konfigurierbar.

| Fach / Angebot | LF | BF / Wahlfach | GSM-Besonderheit |
| --- | --- | --- | --- |
| Informatik (INF) | Nein | BF, 3 Wochenstunden, HJ 1-4 | Schulversuch; nur mit Brückenkurs Klasse 10 oder IMP; darf die zweite Naturwissenschaft ersetzen. |
| Italienisch (I) | Nein | BF, reguläre Kursstufenbelegung | Nur für Schüler mit Italienisch als dritter Fremdsprache im Profil (Klasse 10 / bestätigte Profil-Laufbahn). |
| Psychologie (PSY) | Nein | Wahlfach, 2 Wochenstunden, HJ 1-2 | Keine Halbjahre 3-4. |
| Gemeinschaftskunde (GK) | Konfigurierbar | BF Pflicht HJ 1-2; BF Wahl HJ 3-4 | Wahlkurse 3-4 ermöglichen bei erfüllten Bedingungen eine mündliche Prüfung. |
| Geographie (GEO) | Konfigurierbar | BF nur HJ 3-4 | Keine zusätzlichen Wahlkurse am GSM; daher regulär keine mündliche Prüfung in GEO. |
| Biologie (BIO) | Ja | Ja | LF und BF wahlweise normal oder bilingual; bilingual nur bei durchgängigem bilingualem Bildungsgang. |
| Ethik (ETH), ev. Religion (ER), kath. Religion (KR) | Nein | BF | Rechtliche Regelungen zu Fachwahl, Wechsel und mündlicher Prüfung beachten. |
| Sport (SP) | gemäss Schulangebot | BF | Befreiung je Halbjahr und Ersatzkurs-Pflicht abbilden. |
| Schüler-Ingenieur-Akademie (SIA) | Nein | Seminarkurs, 3 Wochenstunden, HJ 1-2 | Zwei Seminarkurs-Halbjahre; kann bei Mindest-Basiskurszahl als zwei Kurse zählen, nicht als Sportersatz. |

**Nicht als LF anbieten:** Informatik, Italienisch, Ethik, evangelische und katholische Religion. Alle anderen LF-Optionen nur, wenn die Schule sie im jeweiligen Jahrgang freigibt.

**GSM-Festlegung für GK/GEO bei LF Wirtschaft:** Pflichtkurs GK ausschließlich HJ 1, Pflichtkurs GEO ausschließlich HJ 3. GK HJ 2 und GEO HJ 4 sind bei bestehendem Angebot möglich, aber nicht pflichtig. GK-Wahlkurse HJ 3+4 bleiben möglich; für GK als mündliches Prüfungsfach bei LF Wirtschaft genügen der verpflichtende GK-Kurs HJ 1 und die beiden Wahlkurse HJ 3+4. Ohne LF Wirtschaft: GK HJ 1+2 und GEO HJ 3+4 pflichtig; GK mündlich verlangt zusätzlich HJ 3+4. GEO mündlich nicht anbieten, da die zwei zusätzlichen GEO-Wahlkurse fehlen.

**Unbedingt getrennt halten:** Fach und Kursart bzw. bilinguale Variante, Halbjahresbelegung, schulisches Angebot, Kursgruppe in Untis und spätere Kurszuweisung. Die Schüler wählen keine Lehrer oder konkreten Parallelgruppen.

## 5. Persönliche Vorbildung und Einschränkungen

Das Lehrerprogramm muss einen Excel-/CSV-Import mit frei zuordenbaren Spalten (Mapping mit Vorschau) für folgende Angaben bereitstellen:

- stabile Schüler-ID (wenn vorhanden), Name, Vorname, Klasse, Abiturjahrgang, Windows-Benutzername; optional Jahrgang G8/G9;
- Religion/Ethik in der Einführungsphase: besuchtes Fach und nach Möglichkeit Halbjahr; gegebenenfalls erforderliche Zustimmung / klärungsbedürftiger Fall;
- Informatik: Brückenkurs Klasse 10 oder IMP; die beiden Zugangswege getrennt erfassen;
- Italienisch: Profilfach dritte Fremdsprache und ggf. Beginn des Fremdsprachenunterrichts;
- bilingualer Bildungsgang: durchgängig besucht, ja/nein;
- Sport: Verwaltungsmerkmal zur Befreiung, betroffene Halbjahre und Ersatzkurserfordernis; keine Diagnose oder Gesundheitsdetails speichern;
- Abgängerprognose: aktiv, voraussichtlicher Abgänger, bestätigter Abgänger; optional kurzer Planungsgrund ohne sensible Details.

Diese Merkmale stammen **autorisiert aus der Lehrerverwaltung**, nicht ungeprüft aus Schüler-JSON. Das Schülerprogramm verwendet hierfür die in Abschnitt 5a spezifizierte **individuelle, signierte Profildatei**. Ist die Verteilung noch nicht eingerichtet oder schlägt die Prüfung fehl, ist nur eine als vorläufig gekennzeichnete Bearbeitung möglich; sie darf keinen verbindlichen Grünstatus erzeugen. Beim Import in der Lehrerversion werden die Voraussetzungen erneut gegen den geschützten Stammdatenbestand geprüft.

Unbekannte Voraussetzungen führen zum Status **fachlich zu klären** statt zu einer vorgeblich korrekten Wahl. Eine menschlich bestätigte Ausnahme wird mit Grund, Bearbeiter, Datum und Rechtsbezug protokolliert.

## 5a. Individuelle Schülerprofile: Erzeugung, Verteilung und Prüfung

**Verbindliche Anforderung:** Das Schülerprogramm benötigt zur sofortigen Prüfung seiner persönlichen Wahlberechtigungen eine **individuelle, vom Lehrerprogramm erzeugte und digital signierte Profildatei**. Es darf **keine gemeinsame Datei mit den personenbezogenen Voraussetzungen des gesamten Jahrgangs** an Schülerinnen und Schüler ausgegeben werden.

- **Ablauf:** (1) Lehrerprogramm importiert die Klasse-10-Stammdaten, (2) überprüft die Zuordnung Schüler-ID ↔ Windows-Benutzerkennung ↔ tatsächliches Home-Verzeichnis, (3) erzeugt für jeden Schüler ein minimiertes Profil, (4) signiert es mit einem ausschließlich administrativ verwahrten privaten Schlüssel, (5) verteilt Profil und Signatur in dessen persönliches Home-Verzeichnis, (6) protokolliert Erfolg, Fehlschlag und Profilversion pro Person.
- **Dateien:** Im konfigurierbaren Home-Unterordner liegen z.B. `profil.json` (vom Lehrerprogramm ausgestellt, vom Schülerprogramm nur gelesen), `profil.sig` (abgesetzte Signatur) und `aktuell.json` (vom Schüler bearbeitete Kurswahl). Datei- und Zielpfade sind Platzhalter; die echten Schulnetzpfade werden später ergänzt. Die Verwaltung darf Profile gezielt erneut verteilen, ohne `aktuell.json` oder ältere Schülerwahlen zu überschreiben.
- **Minimaler Profilinhalt:** `schemaVersion`, `profileRevision`, `studentId`, `windowsUsername` (ggf. dauerhaftes Account-Merkmal), `abiturjahrgang`, `issuedAt`, `keyId` sowie ausschließlich benötigte Zulassungsmerkmale: `italienischProfil`, `informatikBrueckenkurs`, `informatikIMP`, `bilingualDurchgaengig`, `ethikReligionKlasse10` (besuchtes Fach/ggf. Halbjahre), `sportBefreiungHalbjahre` bzw. freigegebene Ersatzpflicht. Keine Diagnosen, Passwörter oder Angaben über andere Schüler. Sondergenehmigungen nur als minimaler, administrativ geprüfter Status ohne vertrauliche Begründung.
- **Signatur:** Asymmetrische digitale Signatur mit etabliertem Verfahren (z.B. ECDSA P-256/SHA-256) über die **exakten UTF-8-Bytes** der Profildatei; die separate Signatur referenziert `keyId` und Algorithmus. Schülerprogramm enthält ausschließlich vertrauenswürdige öffentliche Prüfschlüssel. Der private Signierschlüssel wird **niemals** in Schüler-EXE, Profilordner, Git-Repository oder öffentlich lesbarer Freigabe gespeichert. Schlüsselablage, Sicherung und Rotation dokumentieren. Alternativ andere etablierte Kryptobibliothek, aber kein selbst entworfenes Verfahren.
- **Identitätsprüfung:** Bei Programmstart Windows-Konto aus dem Betriebssystem lesen und mit der vom Schulbetrieb bestätigten Zuordnung sowie `windowsUsername`/`studentId` des signierten Profils abgleichen. Die in JSON enthaltene Kennung alleine genügt nicht. Keine Annahme, dass frei gewählte lokale Windows-Konten einer schulisch authentifizierten Domänenanmeldung entsprechen. Bei Identitätsabweichung Profil ablehnen und Klärung anzeigen.
- **Fehlerbehandlung:** Fehlende Datei, ungültige Signatur, unbekannter Schlüssel, falscher Abiturjahrgang, nicht unterstütztes Schema, nicht passende Benutzerkennung und fehlende Einzelangaben müssen sichtbar als `needs_review`/`profile_invalid` markiert werden; persönlich eingeschränkte Fächer nicht als sicher wählbar freigeben. Die Anwendung muss den Schüler an die Oberstufenberatung verweisen und einen vorhandenen Wahlentwurf vor Verlust schützen.
- **Aktualisierung:** Importierte Vorbildung kann von der Oberstufenberatung korrigiert werden; erneute Profilausgabe erhöht `profileRevision`. Das Schülerprogramm liest beim Start die vorhandene Version, zeigt Profilrevision und Stand an und prüft die aktuelle Wahl neu. Ein ausschließlich dateibasierter Offline-Client kann nicht sicher erkennen, dass inzwischen ein neueres Profil existiert. Deshalb ist die Prüfung in der Lehrerversion **immer verbindlich** und darf sich nie auf ein vorgelegtes, möglicherweise veraltetes, aber gültig signiertes Profil verlassen.
- **Verbindlicher Lehrerbestand:** Auch gültig signierte Profile begründen keine verbindliche Kursberechtigung. Beim Einsammeln bzw. bei Lehreränderungen werden die Wahlen ausschließlich gegen aktuelle, geschützte Lehrerstammdaten, geltendes Angebot und Regelstand validiert. Eine Profilkorrektur löst eine neue Prüfung bestehender Wahlen und ggf. `nachwahl_noetig` aus.
- **Bedienung Lehrerprogramm:** Verteilungsdialog mit Vorschau, Zielpfad-/Benutzerprüfung, Sammelverteilung und Einzelwiederholung; Bericht `verteilt`, `nicht_gefunden`, `Zugriff_verweigert`, `Profil_abweichend`, `veraltet`. Ein erneuter Stammdatenimport darf bestehende Lehreränderungen und Wahlhistorien nicht überschreiben.

**Beispiel für synthetische Testdaten** (Schüler-ID und Account frei erfunden; Signatur liegt in separater Datei):

```json
{
  "schemaVersion": 1,
  "profileRevision": 3,
  "studentId": "demo-001",
  "windowsUsername": "testschueler01",
  "abiturjahrgang": 2029,
  "issuedAt": "2026-10-25T17:00:00Z",
  "keyId": "school-key-01",
  "voraussetzungen": {
    "italienischProfil": true,
    "informatikBrueckenkurs": false,
    "informatikIMP": true,
    "bilingualDurchgaengig": true,
    "ethikReligionKlasse10": "KR",
    "sportBefreiungHalbjahre": []
  }
}
```

## 6. Rechtliche Regelprüfung (ein gemeinsamer Regelkern)

**Rechtsbasis:** aktuelle AGVO für den jeweiligen Abiturjahrgang, insbesondere Paragraphen 9-15, 17 und 21; KOE 2026 als fachliche Auslegung und für Sonderfälle, jedoch für spätere Abiturjahrgänge vor Freigabe erneut auf Aktualität prüfen. Der Leitfaden Abitur 2028 kann zur Plausibilisierung und für Beispielwahlen dienen. Jede Regel braucht stabile ID, Kurzbeschreibung, Quelle, Geltungsbereich, Schweregrad, Version und automatisierte Tests.

Pflichtprüfungen:

1. Genau drei verschiedene Leistungsfächer (je vier Halbjahreskurse), davon mindestens zwei aus Deutsch, Mathematik, fortgeführten Fremdsprachen oder Naturwissenschaften im Sinne von Paragraph 12 AGVO. Verbotene LF-Angebote und unzulässige Doppelfachwahl erkennen; das dritte LF muss ebenfalls aus dem rechtlich zulässigen Pflichtbereich stammen.
2. Mindestens 12 LF-Halbjahreskurse plus 30 weitere Kurse; insgesamt mindestens 42. Seminarkurs SIA darf mit **zwei**, nicht vier, Kursen zur Mindestbelegung beitragen. Belegung, Wochenstunden und spätere Anrechnung sind getrennte Begriffe.
3. Deutsch, Mathematik, mindestens eine geeignete fortgeführte Fremdsprache, mindestens eine Naturwissenschaft (BIO/CH/PH), vier Kurse Geschichte, vier Kurse Kunst oder Musik, vier Kurse Religion oder Ethik, Sport (oder rechtmässige Ersatzregel) sowie GK/GEO nach GSM-Zeitplan berücksichtigen. LF kann die entsprechende BF-Belegpflicht erfüllen, ohne dass dasselbe Fach parallel als BF doppelt gezählt wird.
4. Entweder zwei geeignete Fremdsprachen oder zwei Naturwissenschaften mit jeweils vier Kursen; das 3-stündige BF Informatik kann als gesetzlich vorgesehene Schulversuchs-Alternative die **zweite**, nicht die erste Naturwissenschaft ersetzen. NwT nur bei tatsächlichem Angebot und geprüfter Profilvoraussetzung.
5. Informatik BF nur bei Brückenkurs oder IMP. Informatik als mögliches mündliches Prüfungsfach nur bei Voraussetzungen des Schulversuchs, vier belegten BF-Kursen und übriger zulässiger Prüfungskombination. Kein Informatik-LF am GSM.
6. Italienisch BF nur bei festgestelltem Italienisch-Profil. Ob Italienisch die allgemeine Fremdsprachenpflicht erfüllen kann, ergibt sich aus der rechtlich maßgeblichen Sprachlaufbahn (Beginn / Niveausicherung), **nicht** allein aus der Aufschrift 'Italienisch in Klasse 10'.
7. BIO normal/bilingual nicht doppelt zählen und nicht gleichzeitig im selben Fach als LF und BF anbieten. Für bilingualen Kurs ist der bestätigte durchgängige Bili-Bildungsgang erforderlich.
8. GK/GEO/Wirtschaft exakt wie Abschnitt 4. Geographie mangels Wahlkursen am GSM nicht als mündliche Prüfung vorschlagen. GK als mündliche Prüfung nur mit allen erforderlichen Pflicht- und Wahlkursen, inklusive Sonderregel LF Wirtschaft.
9. Religion/Ethik nach Paragraphen 11, 13 und 21 AGVO prüfen: grundsätzlich konfessionsentsprechende Religionslehre; erforderliche Zustimmungen und Sonderfälle; Ethik, wenn kein Religionsunterricht besucht wird und Ethik angeboten ist; Religions- oder Ethikwechsel in der Kursstufe sachgerecht behandeln. Mündliche Prüfung in Religion/Ethik nur bei erfüllter Vorbildung bzw. anerkannter Überprüfung und entsprechender vierfacher Belegung; bei Wechsel Rel -> Ethik nach KOE i. d. R. weder Religion noch Ethik als mündliches Fach. Keine automatische Sperre des BF Ethik allein weil Klasse 10 Ethik nicht besucht wurde.
10. LF Sport bei zum Wahlzeitpunkt auch nur teilweiser dauerhafter Sportbefreiung nicht als normale Wahl zulassen; Sonderfälle zur Prüfung ausweisen. Sportbefreiungen differenziert nach Halbjahr und Umfang: erforderliche zusätzliche Basisfachkurse nach Paragraph 13 Absatz 5 AGVO; nicht vom blossen Erreichen der 42-Kurs-Grenze entfallen lassen. Seminarkurs ist kein Sportersatz. Medizinische und genehmigungspflichtige Sonderfälle als manuelle Prüfung ausweisen.
11. Abiturprüfungsoptionen prüfen: die drei LF sind spätere schriftliche Prüfungsfächer; es muss **mindestens eine zulässige Kombination** aus zwei möglichen mündlichen Prüfungsfächern bestehen, die Deutsch, Mathematik und alle drei Aufgabenfelder abdeckt und die Pflicht-Anrechnung von höchstens 40 Kursen nach Paragraph 21 berücksichtigt. Die beiden mündlichen Fächer bei der ersten Kurswahl noch nicht verbindlich verlangen. BLL/Seminarkurs als möglicher Ersatz einer mündlichen Prüfung nach besonderen Vorgaben modellieren, nicht pauschal gleichsetzen.
12. Allgemeine Halbjahreslogik: regulär Wahl für zwei Halbjahre/Jahrgangsstufe, LF alle vier; Ausnahmen nur gemäss AGVO/KOE, etwa besondere Sportersatzkurse. Sportkurse/Profilangebote als konfigurierbare spätere Verfeinerung.
13. Wahlzeitraum unterscheiden: unverbindliche Vorwahl zur Planung und spätere verbindliche Endwahl. Den rechtlichen frühesten Endwahltermin (Paragraph 14 AGVO: nicht früher als acht Wochen vor Ende des Unterrichts in der Einführungsphase) berücksichtigen. Einzel- und Sammel-Änderungen danach als kontrollierten Vorgang behandeln.

**Rückgaben der Regel-Engine:** `valid`, `invalid`, `needs_review` plus strukturierte Meldungen mit Regel-ID, betroffenen Fächern/Halbjahren, klarer Erklärung und möglicher Abhilfe. Fehlende Informationen nie als erfolgreich geprüft behandeln. Unterscheide formale Belegbarkeit, mögliche Abiturprüfungsfächer und späteren Anrechnungsstand. Durchschnittliche Wochenstunden je Halbjahr berechnen; die im Leitfaden genannten 32 Wochenstunden zunächst als gesonderten Planungs- und Prüfhinweis ausgeben, bis ihr exakter Rechtsstatus für den relevanten Jahrgang geklärt ist.

## 7. Schülerprogramm / Bedienung

- Geführter Ablauf: **signiertes persönliches Profil laden und prüfen** -> persönliche Voraussetzungen anzeigen -> drei LF -> Pflicht-BF -> zusätzliche BF/Wahlfächer -> Halbjahres- und Stundenübersicht -> mögliche Abiturprüfungsfachkombinationen -> Kontrolle -> Abgabe.
- Beim Wechsel eines LF Pflichten und Basisfächer neu berechnen, aber Nutzerwahl nicht ohne Hinweis destruktiv überschreiben. Nicht wäh lbare Fa echer vorzugsweise ausgegraut mit verständlicher Begründung anzeigen.
- Sofortige Prüfung und hilfreiche Fehlermeldungen, z.B. 'Du brauchst noch ...', 'GK HJ 3+4 für eine mögliche mündliche Prüfung'.
- Entwurf jederzeit speichern; vor Abgabe umfassend prüfen. Abgabe erzeugt eigenständigen Versionsstand; bei Nachwahl ein weiterer Versionsstand.
- Übersicht pro Halbjahr über Fach, LF/BF/Variante, Wochenstunden und Kurszählung, Sonderfälle und noch offene Entscheidungen. Kontrollbogen druckbar.
- Verschiedene Zustände trennen: gespeichert/eingereicht, formal gültig/ungültig/Prüffall, Nachwahl benötigt, in Lehrerbestand übernommen.
- Schüler erhalten keine administrativen Bearbeitungsrechte; eine EXE oder JSON-Datei aus dem gemeinsamen Netzwerk darf keine Vertrauensquelle für Berechtigungen sein.

## 8. Dateiformat und Identifikation

- Ein klar dokumentiertes, versioniertes JSON-Schema für die Schülerwahl mit stabiler Schüler-ID, Abiturjahrgang, Angebotsversion, Fächer/Varianten/Halbjahre, Abgabestatus, Revisionsnummer und Zeitangaben; keine Klartextpasswörter.
- Wahl-Datei im eigenen Home (`{HOME}/Kurswahl/aktuell.json` als **Platzhalter**, nicht als harter Pfad); daneben **`profil.json` und `profil.sig` als Lehrerdateien** gemäß Abschnitt 5a. Ältere Wahlrevisionen getrennt speicherbar; Autosaves dürfen die Historie nicht mit jeder Checkbox unkontrolliert aufblasen.
- Schreiben mittels temporärer Datei und kontrolliertem Austausch; Dateisperren, defekte JSON, fehlende Netzlaufwerke, Unterbrechungen und doppelte Einlesungen sicher behandeln.
- Einlesen durch Lehrerprogramm anhand authentischer Herkunft des Home-Pfads/Benutzerkontos und Zuordnungstabelle; Namensgleichheiten und falsche/stale IDs als Konflikt melden, nicht stillschweigend zuordnen.
- Schülerdateien sind **nicht manipulationssicher**. Der geschützte Lehrerbestand berechnet die Regelergebnisse unabhängig neu und übernimmt keine Schüler-Aussage wie 'gültig: true' als Beleg.
- Wenn nach einer Lehrerentscheidung eine alte Schülerdatei erneut eingesammelt wird: Änderungen erkennen, Konflikt anzeigen; nie Lehrerentscheidungen automatisch zurücksetzen. Keine Schreibrechte in fremde Schüler-Homes voraussetzen.

## 9. Lehrerprogramm: Stammdaten, Fachangebot und Korrekturen

- Importschnittstelle für Excel/CSV mit Spaltenmapping, Testlauf, Fehlerliste, Dublettenbehandlung und idempotenter Wiederholung. Stammdaten und persönliche Zulassungsvoraussetzungen nach Abschnitt 5 erfassen; **individuelle Profile nach Abschnitt 5a signieren, verteilen, aktualisieren und die Verteilung dokumentieren**.
- Lehrerseitige Kurswahl für jeden Schüler einsehen, bearbeiten, erneut validieren und speichern; Änderungsgrund ermöglichen. Nicht automatisch alte Schülerdaten überschreiben.
- Angebotsverwaltung: Fach + Kursart + ggf. bilingual + belegbare Halbjahre + Wochenstunden + Voraussetzungen + Status + Version + Abiturjahrgang.
- Fachangebotstatus `geplant`, `angeboten`, `nicht_zustande`, optional `fest_eingerichtet`; Status darf als Gesamtfach oder gezielte Variante gelten. Parallelgruppen sind davon unabhängig.
- Nachwahl-Ablauf: Wiedervorlage, betroffene Schüler, offene Rechtsverstosse, Nachwahlfrist, erneute Abgabe, Freigabe/Abschluss durch Oberstufenberatung.
- Lehrerbestand führt eigene eindeutige IDs und importbezogene Herkunftsinformationen. Kein technischer Administratorzugang für Schüler durch Starten einer Lehrer-EXE.

## 10. Kursstreichung, Ersatzwahl und Planungsszenarien

- Eine geplante Streichung vor Wirksamwerden simulieren: Welche Schüler sind betroffen, welche Wahlfächer und welche Mindest- oder Pflichtbelegungen fehlen danach?
- Bei bestätigter Streichung alle betroffenen **aktiven Verwaltungswahlen** anpassen und getrennt davon die ursprünglichen Schülerwünsche erhalten; pro Betroffenem neue Revision/Audit-Ereignis erstellen. Vorher Vorschau und ausdrückliche Bestätigung.
- Ergebnisfilter: `nachwahl_nötig`, `nach_streichung_weiterhin_gültig`, `ungeprüfter_sonderfall`, `ungültig`, `noch_nicht_eingereicht`, `abgänger` usw. Filter kombinierbar und exportierbar.
- Ersatzvorschläge auf Grundlage aktuell angebotener und persönlich zulässiger Fächer; keine automatische Wahl im Namen der Schüler.
- Streichung von `Biologie LF bilingual` betrifft nicht `Biologie LF normal`, sofern letztere weiter angeboten wird; die alternative Variante nicht automatisch eintragen.
- Reine Reduktion von zwei auf eine parallele Unterrichtsgruppe ist **keine** Streichung des Fachangebots und löst nicht automatisch eine Nachwahl aus.
- Planungsszenarien dürfen keine aktiven Wahlen verändern. Erst eine Bestätigung schreibt eine neue offizielle Version; Rücknahme einer Entscheidung erzeugt wiederum eine neue Version (keine Historienlöschung).

## 11. ASV-Datenimport, Abgleich und Export

- Lehrerprogramm kann **ASV-exportierte Kurswahlen** einlesen und mit dem internen Bestand pro Schüler, Fach, Kursart, Variante und Halbjahr vergleichen.
- **Keine ASV-Schreibschnittstelle voraussetzen.** Die erstmalige Übertragung erfolgt manuell; dafür ASV-nahen Kontrollbogen und übersichtliche Tabellenexporte anbieten.
- Formatadapter für ASV erst nach Vorliegen einer echten, anonymisierten Beispieldatei finalisieren. Bis dahin allgemeines CSV/XLSX-Mapping statt erfundener Spaltennamen.
- Konflikte: in beiden Quellen identisch; nur intern; nur ASV (Neuzugang); abweichendes Fach/Variante/Halbjahr; unklare ID oder Namensdublette.
- Import ist nie ein unbestätigtes Massenüberschreiben; Einzel- und Sammelentscheidungen nach Vorschau, jede Änderung mit Quelle versioniert.
- Praktische Exporte: Schüler-Kontrollbogen PDF, jahrgangsweite LF-/BF-Liste, Fächer je Halbjahr, CSV/XLSX mit stabilen Fachkennungen, Liste `in ASV übertragen` mit Bearbeitungsstatus und Ausnahmeliste.
- Auch später hinzugekommene Schüler müssen aus ASV importiert und danach vervollständigt werden können.

## 12. Auswertung und Analyse

- Dashboard nach Abiturjahrgang und Planungsstand: Gesamtzahl, aktive Schüler, voraussichtliche und bestätigte Abgänger, ausgefüllte/abgegebene/gültige/ungültige Wahlen, offene Nachwahlen und Klärungsfälle.
- LF- und BF-Nachfrage je Fach, Variante (normal/bili), Halbjahr und Kursart; SIA, PSY, INF, Italienisch eigens auswerten.
- LF-Dreierkombinationen; gemeinsame Fachbelegungen; Überschneidungsmatrizen als Vorarbeit für die spätere Untis-Verschienung. Keine automatische Stundenplanung im ersten Release.
- Je Schüler: Zahl der Kurse und Wochenstunden pro Halbjahr, Pflichtfächer, 2-Fremdsprachen-/2-Naturwissenschaften-Variante, mögliche mündliche Prüfungsfachkombinationen, Sonderfälle.
- Aggregation von Kursnachfrage zu möglichen Gruppengrößen und Ressourcenbedarf, aber klar als **Planungsschätzung**, nicht als bereits erzeugte Untis-Unterrichtsgruppen.
- Umschalter `voraussichtliche Abgänger einbeziehen`: Jeder Bericht zeigt bei Bedarf `alle`, `abgänger` und `planungsrelevant`. Schüler werden **nicht gelöscht**, sondern nur aus der aktiven Planungszahl herausgerechnet.
- Export sämtlicher gefilterter Ansichten nach XLSX/CSV, zusätzlich sinnvoll gestalteter PDF-Bericht. Vergleich zweier Planungsstände / Vorwahl gegen final.

## 13. Versionierung, Änderungsprotokoll und Planungsstände

- Versioniere relevante Einzelwahl-Ereignisse: erste Abgabe, neue Schülerabgabe, Lehreränderung, Angebotsstreichung, ASV-Abgleich/-Übernahme, Wiederherstellung einer früheren Wahl.
- Pro Revision speichern: eindeutige ID, Schüler-ID, Vorgängerrevision, Änderungszeitpunkt, Akteur/Rolle, Quelle, Anlass, Angebots- und Regelversion, vollständiger Snapshot sowie strukturierten Unterschied.
- Zwei Versionen müssen im Lehrerprogramm vergleichbar sein. Wiederherstellen ist **eine neue Revision**, kein Überschreiben der alten Historie.
- Jederzeit kompletter Jahrgangs-Snapshot mit aktivem Angebot, Schülerwahlen, Abgängerstatus, Sondergenehmigungen und Auswertungsparametern; Planungsstände vergleichen und bei Bedarf exportieren.
- Historie im vor Schülerzugriff geschützten Verwaltungsbestand. Schüler-Home-Historie ist nur zusätzliche Arbeitshilfe und kein unveränderbares Beweismittel.
- Backup und Aufbewahrungs-/Löschkonzept für Verwaltungsdaten vor Echtdatenbetrieb festlegen. Änderungsprotokolle sind nachvollziehbar, aber ohne Serververtrauensanker nicht forensisch manipulationssicher.

## 14. Sicherheit, Datenschutz und Betriebsqualität

- Studentischer Speicherbereich nur eigener Benutzer; zentraler Programmordner schreibgeschützt; Lehrerbestand und Importe nur für berechtigte Lehrkräfte. Rechte auf SMB/NTFS im Schulnetz vor Produktiveinsatz prüfen.
- Keine Passwörter speichern, keine direkte LDAP-Verbindung aus der Schüleranwendung, keine stillen Internetzugriffe. **Private Profilsignierschlüssel nur im geschützten Verwaltungsbereich verwahren, Schüler-App enthält allein öffentliche Prüfschlüssel.** Anonyme/synthetische Daten im Repository.
- Besondere Vorsicht bei Angaben zu Religionsunterricht und Sportbefreiung: Datenminimierung, keine Krankheitsdiagnosen, eingeschränkte Auswertung, keine unnötigen personenbezogenen Daten in Exporten.
- Fehlertoleranz: fehlendes Netzlaufwerk, abgebrochene Dateizugriffe, mangelhafte Rechte, defekte CSV/JSON, Versionskonflikte, Backup-Ausfall und unerwartete Regelkonfiguration dürfen nicht zu stillen Datenverlusten führen.
- Barrierearme Bedienung: Tastatur, lesbare Schrift, deutliche Statusmeldungen, klare Fehlersprache und bestätigte destruktive Aktionen.
- Verteilung in der Schule testen (AppLocker/SmartScreen, UNC-Pfade, Ausführung aus Freigaben, Windows-Profilrechte). Einheitliches Logging ohne Passwörter und ohne volle Schülerlisten in normalen Protokolldateien.

## 15. Automatisierte Tests und verbindliche Akzeptanzfälle

Mindestens xUnit-Tests für Domäne, Dateischicht, Import und Analyse; UI-Smoke-Tests, soweit machbar. Jeder rechtliche Test benennt die Quelle. Beispiele:

1. Drei LF mit nur einem zulässigen Kernfach -> Fehler; genau drei mit mindestens zwei berechtigten Kernfächern -> Teilprüfung erfolgreich.
2. Informatik, Italienisch, ETH, ER, KR sind nicht als LF wählbar.
3. INF BF ohne Brückenkurs/IMP -> blockiert; mit Voraussetzung und vier Kursen -> zweite Naturwissenschaft ersetzbar, erste nicht.
4. Italienisch BF nur bei Profilberechtigung. Bili-BIO nur bei bestätigtem durchgängigem Bili-Bildungsgang.
5. Ohne LF W GK HJ 1+2 und GEO HJ 3+4 Pflicht; mit LF W nur GK HJ 1 und GEO HJ 3 Pflicht.
6. GK mündlich ohne W nur mit HJ 1-4, mit W mit HJ 1,3,4; GEO mündlich am GSM nicht möglich.
7. Psychologie nur HJ 1+2, SIA 3-stündig in HJ 1+2, SIA zählt als zwei Basiskurse, nie als Sportersatz.
8. Sportbefreiung mit fehlenden Kursen -> erforderliche Ersatzkurszahl; bei bereits 42 Kursen darf Ersatzpflicht nicht stillschweigend entfallen.
9. Religion/Ethik: fehlende Vorbildung in der Einführungsphase beeinflusst mündliche Prüfungsoption, nicht pauschal die BF-Wahl; Wechsel und zustimmungspflichtige Sonderfälle ergeben die richtigen Prüfhinweise.
10. Mindestens 42 Belegungskurse ist nicht dasselbe wie höchstens 40 später anzurechnende Kurse. Abiturprüfungskombination nach Paragraph 21 separat validieren.
11. Streichung eines BF-Wahlfachs kann weiter gültige Wahl hinterlassen; Streichung eines erforderlichen LF erzeugt Nachwahlbedarf.
12. Streichung einer Bili-Variante entfernt nicht unbesehen normale BIO-Varianten; Gruppenanzahlreduzierung löst keine Nachwahl aus.
13. Lehrer bearbeitet Schülerwahl -> neue Revision; später erneut gelesene alte Home-Datei darf diese Änderung nicht überschreiben.
14. ASV-Import mit neuer Person, Dublette und Abweichung -> getrennte Konflikt- und Übernahmeoptionen; keine stillen Änderungen.
15. Abgänger-Filter ändert Planungszahlen, nicht den Wahlbestand. Gleiche Analyse nach erneutem Import liefert deterministische Zahlen.
16. Eine frühere Wahl wiederherstellen erzeugt neue Revision; Jahrgangs-Snapshot ist unveränderlich rekonstruierbar.
17. Unterbrochener Schreibvorgang darf vorherige gültige JSON-Datei nicht zerstören; doppelte Einlesungen erzeugen nicht willkürlich neue Revisionen.
18. Fehlende oder widersprüchliche Vorbildungsangaben führen zu `needs_review` statt einem falschen Grünstatus.
19. Ein signiertes Profil mit Bili/IMP/Italienisch steuert die persönlichen Wahlmöglichkeiten; dieselben JSON-Daten mit geänderter Vorbildung und alter Signatur werden abgewiesen.
20. Profil eines anderen Schülers, unpassender Windows-Account, unbekannter Schlüssel, falscher Jahrgang und ungültiges Schema werden erkannt und mit `profile_invalid`/`needs_review` gemeldet; vorhandene Wahl bleibt erhalten.
21. Lehrer verteilt 100 synthetische Einzelprofile; genau ein Ziel ist nicht schreibbar. Die 99 erfolgreichen Zustellungen bleiben bestehen, der Fehlerbericht nennt die fehlgeschlagene Zustellung, und keine Schülerwahl wird überschrieben.
22. Lehrer korrigiert eine Vorbildung und verteilt eine höhere Profilrevision. Schüler-App prüft Wahl neu; ein weiterhin gültig signiertes altes Profil darf beim verbindlichen Lehrerimport nicht gegen aktualisierte Verwaltungsdaten gewinnen.
23. Nach erneutem Import der Klasse-10-Daten bleiben protokollierte Lehreränderungen und die Kurswahl-Historie bestehen. Ein wiederholter Profilverteilungslauf erzeugt keine unnötigen zusätzlichen Wahlrevisionen.

## 16. Projektstruktur und Auslieferung

Vorgeschlagene Solution-Struktur (Codex darf fachlich begründete Verbesserungen vornehmen):

```text
GSM-Kurswahl/
  AGENTS.md
  README.md
  docs/
    ANFORDERUNGEN.md
    RECHTSQUELLEN.md
    OFFENE_PUNKTE.md
  src/
    GsmKurswahl.Domain/
    GsmKurswahl.Rules/
    GsmKurswahl.Storage/
    GsmKurswahl.ImportExport/
    GsmKurswahl.StudentApp/
    GsmKurswahl.TeacherApp/
  tests/
    GsmKurswahl.Domain.Tests/
    GsmKurswahl.Rules.Tests/
    GsmKurswahl.Storage.Tests/
    GsmKurswahl.ImportExport.Tests/
  samples/
    fachangebot-demo.json
    schüler-demo.json
```

Sämtliche Schüler-Testdaten synthetisch. Lizenz-/Sicherheitsregeln und installierte Paketversionen dokumentieren. Ein Windows-Release muss ohne Entwicklungsumgebung startbar sein. In der Dokumentation genaue Befehle für Restore, Build, Tests, Publizieren und einen manuellen Probelauf angeben.

## 17. Entwicklungsreihenfolge / Meilensteine

**M1 - Grundgerüst:** Solution, Model, JSON-Schema, synthetische Daten, konfigurierbare Dateipfade, Schüler-App mit einfacher Wahl und Speichern/Laden, Regel-Tests, CI-freundlicher Build.

**M2 - Fachliche Vollständigkeit:** vollständiger GSM-Angebotskatalog, persönliche Voraussetzungen, **signiertes Profilmodell mit Schülerprüfung**, AGVO-Validierung mit Regel-IDs und Testfällen, Prüfungsfach-Möglichkeiten und Kontrollbogen.

**M3 - Lehrerverwaltung:** Klassen-10-Import, **Profilsignierung und Einzel-/Sammelverteilung in Schüler-Homes mit Fehlerbericht**, Einsammeln, Lehrerbearbeitung, SQLite-Verwaltungsbestand, Versionierung, Filter und Nachwahl.

**M4 - Kursplanung:** Angebotsvarianten streichen, Vorschau/Szenarien, Auswirkungen, Abgängerfilter, Nachfrage- und Kombinationsanalysen, Planungs-Snapshots.

**M5 - ASV und Abschluss:** ASV-Importadapter nach Musterdatei, Differenzvergleich, Exporte, Schulnetz-Pilot mit Dateirechten, Dokumentation, Backup und Releasepaket.

Bei jedem Meilenstein Tests ausführen, beobachtbare Ergebnisse dokumentieren und keine ungetesteten Behauptungen über Vollständigkeit machen.

## 18. Offene Informationen - Konfiguration statt Annahmen

- Exakte Home- und Sammelpfade (Lehrerlaufwerk wahrscheinlich S:), Verzeichnisrechte einschließlich **Schreiben der Profile durch die Oberstufenberatung**, ob die Schüler-EXE auf dem Tauschlaufwerk ausgeführt werden darf; Verfahren zur sicheren Aufbewahrung und Rotation des Signierschlüssels.
- Vollständige Liste der am GSM angebotenen LF/BF neben den ausdrücklich genannten Fächern, inklusive W, Sport, NwT und möglicher Kooperationen.
- Abiturjahrgang, konkreter Wahlzeitraum, G8/G9 und anzuwendender Rechtsstand. KOE 2026 bezieht sich auf das Abitur 2026; spätere Vorgaben vor einer verbindlichen Wahl prüfen.
- Klassen-10-Importformat (Namen, Benutzerkennung, Ethik/Religion, INF-Vorbildung, Italienisch, Bili) und reale, anonymisierte ASV-Exportdatei.
- Italienisch: genaue Stundenzahl und gewünschte Einstufung als fortgeführte Fremdsprache anhand der individuellen Sprachlaufbahn.
- Vorgaben für Kurswahl-Unterschrift/Kontrollbogen, Nachwahlfristen, Lehrerberechtigungen, Aufbewahrungsfristen und Standort des geschützten Lehrerbestands.

**Arbeitsregel für Codex:** Fehlende Informationen in `docs/OFFENE_PUNKTE.md` notieren, mit Demo-/Konfigurationswerten weiterbauen, fachlich ungeklärte Fälle explizit mit `needs_review` versehen; keine unbelegte gesetzliche Regel erfinden.

## 19. Relevante Quellen für die Implementierung

- AGVO: `AGVO Abiturverordnung GymAbiPrV_BW_2018.pdf` (bereitgestellte Fassung, laut Deckblatt zuletzt geändert 04.02.2025), insbesondere Paragraphen 9-15, 17 und 21.
- KOE: `Kursstufenorganisationserlasse (KOE) 2026_KOE_05.02.2026.pdf`, insbesondere Abschnitte 2, 3 und 8.6.2/8.6.3; nur intern entsprechend schulischem Berechtigungskreis nutzen.
- Leitfaden: `2025-12-16-Leitfaden-ABI-2028.pdf`, insbesondere Kurswahlen, Abiturprüfungsfächer und Musterwahlbogen.
- GSM-Prozessbeschreibung: `01_Prozessbeschreibung_Oberstufenberatung.docx`, insbesondere Klasse 10: Vorwahl, Auswertung, ASV-Transfer, Kontrollbogen und Umwahl.

Es gilt stets die **jeweils für den konkreten Abiturjahrgang rechtsgültige Fassung**. Widersprüche zwischen Quelle, Schulpraxis und Annahmen dokumentieren und von der Oberstufenberatung entscheiden lassen, statt sie stillschweigend aufzulösen.
