# Offene Punkte / keine Freigabe

- Vollständiger jahrgangsbezogener GSM-Katalog einschließlich aller LF, W, Sport, NwT und Kooperationen fehlt. Das Sample ist ausdrücklich ein synthetisches Demoangebot; geplante, unbestätigte Optionen bleiben gesperrt.
- Abiturjahrgang 2029 ist ein Demowert. G8/G9, Wahlzeitraum und rechtsgültiger Regelstand sind schulisch zu bestätigen.
- Die in Abschnitt 19 genannten AGVO-/KOE-/Leitfaden-PDFs und die Prozessbeschreibung liegen nicht im Arbeitsverzeichnis. M1 implementiert ausschließlich technische Prüfungen und ausdrücklich spezifizierte Angebotsgrenzen, keine rechtliche Vollvalidierung.
- Individuelle signierte Profile, vertrauenswürdige Schlüssel, schulische Konto-/Home-Zuordnung und Domänenvertrauen fehlen. In M1 immer `needs_review`, eingeschränkte Angebote nicht sicher freigeben. Signaturprüfung M2; Erzeugung/Verteilung M3.
- Italienisch: Stundenzahl, Sprachbeginn und rechtliche Eignung als fortgeführte Fremdsprache bestätigen. Sample-Angebot bleibt geplant.
- Religion/Ethik, Sportersatz und genehmigungsbedürftige Ausnahmen erst nach Quellenprüfung vollständig umsetzen. Kein pauschales BF-Ethik-Verbot wegen fehlender Vorbildung.
- Exakte Home-/Import-/Profilpfade, Rechte und schreibberechtigter Verwaltungsarbeitsplatz fehlen. Keine Laufwerksbuchstaben annehmen.
- Importspalten und anonymisiertes ASV-Muster fehlen. Vorschau und Mapping in M3/M5; keine angenommenen ASV-Feldnamen.
- Schlüsselverwahrung/-rotation, Lehrerberechtigungen, Aufbewahrung, Löschung, Backup, Kontrollbogen/Unterschrift und Nachwahlfristen vor Echtdatenbetrieb klären.
- SMB-Atomizität, Stromausfallverhalten, AppLocker/SmartScreen und Start aus Freigabe müssen im Schulnetzpilot geprüft werden. Lokale Tests ersetzen diesen Pilot nicht.
- Selbstenthaltender Windows-Publish lokal durch fehlende/online nicht abrufbare .NET-10.0.11-Runtimepakete blockiert. Frameworkabhängige Ausgabe benötigt .NET 10. Standalone-Erstellung und Test auf einem Rechner ohne SDK vor Verteilung nachholen.

## Wahlassistent – Grenzen der Vorplanung (25.10.2026)

- Fünf Schritte gemäß Nutzerwunsch (26.10.2026): LF, mündliche Planung, Pflichtbereiche als Einzelfragen, weitere Kurse, Überblick. Die mündlichen Fächer sind ausdrücklich unverbindliche Planungsangaben.
- Englisch ist die einzige bekannte fortgeführte Fremdsprache des Demoangebots. Weitere Sprachen, NwT und persönliche Alternativen erfordern bestätigte Fach-/Laufbahnkonfiguration in M2.
- Kunst/Musik, Religion/Ethik und Naturwissenschafts-Alternativen werden nicht willkürlich automatisch entschieden; sie erscheinen als offene Wahlen. Sport wird vorläufig ergänzt, Befreiung und Ersatz bleiben needs_review.
- Nach LF-/Prüfungswechsel bleiben zusätzliche vorhandene Kurse erhalten und können bewusst abgewählt werden. Fehlende oder mehrdeutige Pflichtangebote werden gemeldet.
- Wahl-Schema v2 speichert plannedOralSubjects. v1 wird ohne Migration abgewiesen und vor Überschreiben geschützt; eine explizite Migration ist nicht implementiert.

## Geführte Oberfläche (26.10.2026)

- Pflichtenheft §7 nennt für nicht wählbare Fächer „vorzugsweise ausgegraut mit verständlicher Begründung“. Auf Nutzerwunsch stehen sie jetzt mit Begründung in einer eingeklappten Liste je Schritt statt ausgegraut zwischen den Optionen. Mit der Oberstufenberatung bestätigen.
- Bei Fragen mit genau einer Antwort ersetzt eine neue Antwort die vorherige, außer diese ist durch Prüfungsplanung oder Pflicht gesetzt. Wer zusätzlich Kunst und Musik belegen möchte, ergänzt das zweite Fach in „Weitere Kurse“.
- Offene Pflichtbereiche blockieren den Weiter-Schritt nicht, damit fehlende Angebote nicht festsetzen; sie bleiben sichtbar. Ob vor einer späteren Abgabe blockiert werden soll, entscheidet M2.
- Reihenfolge und Formulierung der Fragen sind ein Entwurf und mit der Beratungspraxis abzustimmen. Kein Sicht-, DPI- oder Screenreader-Test auf Schulgeräten.
- Voraussetzungsanzeige: In M1 zeigt sie mangels signiertem Profil überall „ungeprüft“. Selbstangaben der Schüler sind bewusst nicht vorgesehen (Pflichtenheft §5). Darstellung von Profilrevision/-stand, `profile_invalid` und Identitätsabweichung folgt mit der Profilprüfung in M2.
- Zweite Fremdsprache statt zweiter Naturwissenschaft (Pflichtenheft §6 Nr.4): in M2 die Frage `PLAN-AREA-SCIENCE` aufteilen in „eine Naturwissenschaft“ und „zweite Naturwissenschaft oder zweite fortgeführte Fremdsprache“, mit Sprachkennzeichnung im Katalog und Freigabe über das Profil.

## Aktualisierung M2 / Nutzerentscheidungen 26.10.2026

Die obigen M1-Einträge sind historisch. Neu entschieden/umgesetzt:

- Erster tatsächlicher Einsatzjahrgang: **2029**. Jahrgang soll im Lehrerprogramm einstellbar sein; Dialog folgt M3, Datenmodell/Katalog sind bereits jahrgangsbezogen.
- LF-Angebote vor Verteilung im Lehrerprogramm auswählbar (M3). Standard laut Nutzer: alle im vorhandenen Katalog LF-fähigen Pflichtfächer angeboten, außer Religion, Ethik, INF, Italienisch. G, BK, MU ergänzt; GK/GEO/W/SP aktiviert. Persönliche Einschränkungen bleiben wirksam. Weitere Fremdsprachen/NwT/Kooperationen sind noch nicht spezifiziert und wurden nicht erfunden. Italienisch-BF bleibt bis zur Stundenzahl-/Laufbahnklärung geplant.
- Profilprüfung ist implementiert, einschließlich Windows-SID und separater öffentlicher Vertrauenskonfiguration; Signierung/Verteilung bleibt M3. Reale Domänen-/Home-Zuordnung und Installation mit geprüften ACLs fehlen weiterhin. Profiländerungen werden beim Neustart geprüft, kein Live-Reload.
- Fachregeln prüfen die ausdrücklich beschriebenen Belegungsbedingungen, mögliche Prüfungsfächer und eine vorläufige Pflichtanrechnung. Aktuelle AGVO-Volltexte, interne KOE und Freigabe für 2029 müssen noch abgeglichen werden. M2 ist damit **noch nicht fachlich abnahmefähig**; keine Behauptung vollständiger rechtlicher Validierung.
- Religion/Ethik: minimierte signierte administrative Belegungs-/Prüfungsbestätigungen modelliert. Vollständige Ableitung aus Konfession, Halbjahreswechsel und Feststellungsprüfung benötigt belastbare KOE-Fundstellen. Keine erfundenen Sonderregeln; offene Fälle bleiben needs_review.
- Sport: Befreiungshalbjahre und dauerhafte Befreiung getrennt; zusätzliche Ersatzkurse unabhängig von 42 und ohne SIA geprüft. Administrative Zuordnung/Umfang und Sondergenehmigung weiterhin prüfen. Einzelhalbjahre als Sportersatz sind bei der Kurswahl ausgeschlossen (Nutzerfestlegung 29.10.2026, siehe unten).
- Zweite bestätigte fortgeführte Sprache wird als Alternative berücksichtigt. Englische Laufbahn bleibt Annahme des synthetischen Demoangebots; für Produktivbetrieb alle individuellen Sprachlaufbahnen verbindlich importieren und prüfen.
- Prüfungsoptionen enthalten keine automatische BLL-/SIA-Ersetzung. Eine vollständige fachliche BLL-Entscheidung und jahrgangsbezogen bestätigte 40-Kurs-Anrechnung bleiben offen. Zwei mündliche Prüfungsfächer müssen in Schritt 2 geplant werden (Option zum Zurückstellen auf Nutzerwunsch entfernt).
- Kontrollbogen als druckbares, offline erzeugtes HTML implementiert; PDF über Browserdruck. Keine native PDF-Erzeugung, kein getesteter Druckertreiber/Schulbogen und keine Unterschriftenfreigabe.
- Endwahlfenster/Unterrichtsende sind noch nicht konfiguriert; verbindliche Abgabe bleibt gesperrt. Alte Angebots-/Regelversionen werden geschützt abgewiesen; eine explizite Migration bestehender M1-Entwürfe ist noch nicht vorhanden.

## PDF und Sprachauswahl – aktueller Stand (26.10.2026)

Nutzerkorrektur ersetzt den zuvor dokumentierten HTML-/Browserdruck vollständig: nur direkter PDF-Export, keine Druckfunktion, kein HTML-Export. PDF-Renderer in ImportExport; PDF-Bytes werden vor dem Öffnen der Zieldatei erzeugt. Zielendung .pdf zwingend, Wahl-/Profildateien geschützt. Mehrseitiges A4 mit automatischem Umbruch und eingebetteten Schriften; Beispiel mit langen Bezeichnungen/18 Prüfhinweisen gerendert und geprüft.

Französisch und Latein als reguläre zweite Fremdsprachen vom Nutzer bestätigt; beide nach der gewünschten LF-Standardauswahl als LF/BF aufgenommen. Kataloginhalt `demo-2029-v3`, Regelstand `m2-2`. Wahlschema bleibt v2, Katalogschema v1. Alte Versionen werden weiterhin nicht stillschweigend migriert/überschrieben.

Die erste Fremdsprache wird ausdrücklich als Pflichtentscheidung angeboten, wenn kein passendes LF gewählt ist. Keine automatische Festlegung auf Englisch. Erste Naturwissenschaft separat (BIO/CH/PH); danach immer zweite Sprache oder zweite NW einschließlich berechtigtem INF. Italienisch bleibt von administrativer Profil- und Sprachlaufbahnbestätigung abhängig. Dies ersetzt die historisch beschriebene, zu enge Zwei-NW-Frage.

## Abgleich mit AGVO (Stand 04.02.2025) und KOE 2026 (Stand 05.02.2026) am 26.10.2026

- `docs/Rechtsgrundlage AGVO BW GymAbiPrV_BW_2018.pdf` liegt im Repository vor; zudem wurden AOE 2025 und `docs/Kursstufenorganisationserlasse (KOE) 2026_KOE_05.02.2026.pdf` (RP Stuttgart, Stand 05.02.2026) lokal fachlich ausgewertet. Gemäß Pflichtenheft § 19 („nur intern entsprechend schulischem Berechtigungskreis nutzen“) und dem Vermerk auf dem KOE-Deckblatt („ausschließlich für die Hand der Schulleitung und der Oberstufenberatung“) ist die KOE-PDF in `.gitignore` vom öffentlichen Git-Repository ausgeschlossen; die geprüften Regeln sind in `docs/RECHTSQUELLEN.md` mit exakten Kapiteln hinterlegt.
- **Sportersatz (KOE 2026 Kap. 3.4 S. 7):** Jeder zusätzliche Kurs in anderen Basisfächern aus Pflicht- oder Wahlbereich (`Bf` oder `Elective`) ersetzt befreite Sport-Halbjahre; entscheidend sind mindestens 42 Halbjahreskurse ohne SIA sowie die entsprechende Anzahl zusätzlicher Kurse. Einzelhalbjahre sind im Umfang der befreiten Sport-Halbjahre als Sportersatz zulässig. Bei teilweiser oder dauerhafter Sportbefreiung ist Sport weder als LF noch als mündliches Prüfungsfach (AGVO § 21 Abs. 2 Nr. 6) wählbar.
- **Religion/Ethik (AGVO § 21 Abs. 2 Nr. 4–5, KOE 2026 Kap. 3.3 S. 6 & Kap. 8.6.3 S. 35):** Ohne explizite Ausnahmebestätigung ist nur das in Klasse 10 besuchte Fach (`ReligionEthicsGrade10`) als mündliches Prüfungsfach wählbar. Bei einem Wechsel der Belegung gegenüber Klasse 10 oder bei abweichender Prüfungsplanung wird explizit auf die erforderliche Überprüfung/Feststellungsprüfung zu Beginn von HJ 1 hingewiesen (bzw. auf den Ausschluss der mündlichen Prüfung bei Abmeldung Rel -> Ethik in der Kursstufe).
- **Offen für spätere Meilensteine / Produktivbetrieb:** Formelle Bestätigung des Rechtsstands für den Abiturjahrgang 2029 (`M2-RELEASE`), Stundenzahl/Sprachlaufbahn Italienisch, Endwahltermin (§ 14 AGVO), Lehrer-Import/Profilsignierung (M3), Kursplanung (M4) und ASV-Musterdatei/Schulnetzpilot (M5).

## M3 / aktueller technischer Stand 27.10.2026

M3 technisch implementiert: Tabellenimport mit Vorschau/Einzelbestätigung und Wiederholungsschutz, Konto-/SID-/Home-Zuordnung, SQLite-Einzelwriter, append-only Stammdaten/Wahlrevisionen/Wiederherstellung, aktuelle lehrerseitige Prüfung, Nachwahl/Filter, Zertifikatssignierung, Einzel-/Sammelverteilung und Fehlerbericht, Jahrgang/LF-Auswahl für neue Angebotsversionen. Historische Hinweise „folgt M3“ oben sind damit ersetzt. Bedienung und genaue Grenzen: LEHRERVERWALTUNG.md.

Offen bleiben: Echtdatenfreigabe, echte schulische Kontozuordnung und ACL-Prüfung, Zertifikatbereitstellung und Schlüsselrotation/-backup, native Zertifikats- und Schulnetztests, Rechtsfreigabe 2029/Endwahlzeitraum, produktive Nachwahlabschlussfreigabe, Aufbewahrungs-/Löschkonzept, DPI/Screenreader/Schulgerätetest. Weiterhin nur synthetische demo-IDs; keine technische Authentifizierung aus der Bearbeiterkennung. XLSX bewusst auf ein Blatt ohne Formeln begrenzt; Tabellarische Sammelbestätigung; Fehlerzeilen ausgeschlossen. Allgemeiner Tabellenimport ist kein ASV-Adapter. M4/M5 nicht umgesetzt.

## Jahrgangsbezogener Betrieb (Nutzerentscheidung 27.10.2026)

Automatisches Öffnen des ausdrücklich aktiven Jahrgangs und fortsetzbare Einrichtung sind umgesetzt. Standard ist ein lokaler beschreibbarer Benutzer-Datenordner; ein anderer lokaler Ordner (auch neben dem Programm) kann ausdrücklich gewählt und gemerkt werden. Bestehende Verwaltungsdateien werden nur bewusst eingebunden. Bestätigte Importzeilen und Einrichtungsstufe bleiben erhalten; unbestätigte Importvorschauen werden nach Neustart erneut aus der Quelle geladen. Die Aufgabenübersicht verwendet gespeicherte Zustellberichte, keine laufende Prüfung der Schülerordner. Bestehende fachliche Freigabe-/Netzpilot-Grenzen gelten unverändert.

## Vereinfachter Verwaltungsablauf (27.10.2026)

Nutzerauftrag ersetzt die Einzelbestätigung durch vorausgewählte Sammelübernahme und erlaubt automatisches Einsammeln konfliktfreier Schülerwahlen bei geöffneter App. Konto-/SID-Vertrauensanker und einmalige Zertifikats-/Katalogbereitstellung bleiben erforderlich. Ordnernamen allein bestätigen keine Windows-Identität. Kein Hintergrunddienst bei geschlossener Anwendung; native Netz-/Zertifikatprüfung weiterhin offen.

## M3 Review-Korrekturen, Verzicht auf Zertifikats-/SID-Pflicht und Direkt-Navigation in der Verlaufsleiste (27.10.2026)

- **Verzicht auf Zertifikats- und SID-Pflicht (Nutzerentscheidung 27.10.2026):** `profil.json` wird direkt ohne Zertifikatssignatur (`profil.sig`), ohne Windows-Zertifikatsspeicher und ohne Windows-SID-Pflicht erstellt, verteilt und vom Schülerprogramm gelesen. Auch `username` ist beim Tabellenimport optional. Die verbindliche Prüfung findet im Lehrerprogramm (und vor der ASV-Eingabe) ausschließlich gegen den geschützten Lehrerbestand (`verwaltung.db`) statt.
- Persönliche Voraussetzungen (`PriorEducation`) und Abgangsstatus (`DepartureStatus`) sind im Personenbereich (`ShowStudent`) direkt bearbeitbar; relative Schülerordner bleiben beim Speichern unbestätigter Zuordnungen erhalten.
- Die drei Schrittkarten der Verlaufsleiste (`Fächer auswählen`, `Schülerliste importieren`, `Dateien verteilen`) sind direkt anklickbar; die redundanten Sprungbuttons darunter wurden entfernt.
- Offene Rückfrage mit dem Nutzer: Phasenmodell beim Einsammeln vs. Lehrerbearbeitung (Schüler-Wahlphase übernimmt automatisch neuere Schülerstände; ab Lehrer-Bearbeitungsphase ist der Lehrerbestand verbindlich).

### M4-Erprobung

- Zielgruppengröße 24 ist ein änderbarer Demo-Planungsparameter, keine rechtliche oder schulorganisatorische Festlegung. Keine tatsächlichen Parallelgruppen oder automatischen Schienen.
- Wiederherstellung eines Planungsangebots stellt dessen Status wieder her; entfernte Schülerwünsche werden über die vorhandene individuelle Historie bewusst übernommen. Keine automatische Vollbestandsrücksetzung.
- Allgemeine fachliche Freigaben, Schulnetz-/Berechtigungsprüfung und ASV-Musterdatei bleiben offen. Die M4-Planung hebt `needs_review` nicht auf.

## M5 – aktueller Ausbau (28.10.2026)

Die vorherige Beschränkung auf Modelle/Schnittstellen wurde vom Nutzer ausdrücklich aufgehoben. Allgemeiner CSV/XLSX-Abgleich, Einzelübernahme mit Historie, gefilterte Tabellenexporte, manuelle revisionsgebundene Übertragungsvermerke (explizites DB-Schema 3), Backup-Wiederherstellung und Release-Skript sind implementiert. Bedienung/Grenzen: M5_ABSCHLUSS.md.

Offen bleiben: anonymisierte ASV-Musterdatei und Feldsemantik für einen spezifischen Adapter; fachliche Freigabe 2029/Endwahl; reale schulische ACLs und Pilot mit synthetischen Konten; zuständige Personen und verbindliche Backup-/Aufbewahrungs-/Löschfristen; Start des Releasepakets auf Zielgerät ohne SDK, Paketlizenz und vollständige Drittanbieter-Notices vor endgültiger Auslieferung. Der allgemeine Abgleich benötigt vollständige Belegungen je enthaltener Person; ein rein zeilenbasiertes Format kann leere Wahlen nicht darstellen. Neuzugänge werden zuerst über Stammdatenimport angelegt, dann erneut abgeglichen. Sammelentscheidungen sind nicht implementiert; Einzelbestätigung ist der verfügbare Weg. Kein Produktivbetrieb oder ASV-Schreibzugriff freigegeben.

M5-Publish aktualisiert: Selbstenthaltende Windows-x64-Ausgabe beider Apps mit .NET 10.0.14 jetzt erfolgreich erstellt (separate Runtime-Lockdateien). Der historische Hinweis auf nicht verfügbare Runtimepakete ist damit für den lokalen Build ersetzt; Zielgerätetest ohne SDK steht weiterhin aus.

### Schüler-Umwahl und Jahresbelegung (28.10.2026)

Nutzerfestlegung 29.10.2026: ETH, ER und KR schließen sich bei gleichzeitiger Halbjahresbelegung gegenseitig aus. Regel `GSM-RELIGION-EXCLUSIVE` prüft dies unabhängig vom Profil für beide Programme. Zeitlich getrennte Fachwechsel bleiben gesondert nach bestehenden Pflicht-/Prüfungsregeln zu behandeln; keine pauschale Freigabe eines Wechsels. Zusatzkurse dürfen keinen zweiten Religion-/Ethikkurs eröffnen. Änderung einer mündlichen Planung ersetzt eine kollidierende alte Belegung mit sichtbarem Hinweis; geladene Doppelbelegungen bleiben bis zur bewussten Korrektur erhalten.

Religion -> Ethik (28.10.2026): Gewünschter Schülerhinweis auf Antrag zum Austritt und notwendige Feststellungsprüfung ergänzt. Ob und wann diese bei Religionsabmeldung für den konkreten Jahrgang eine mündliche Ethikprüfung ermöglicht, bleibt wegen der bisher dokumentierten KOE-Einschränkung offen. Keine automatische Zulassung daraus. Fortführung des bestätigten Klasse-10-Fachs benötigt keine pauschale zusätzliche Zustimmung; explizite Einschränkungen bleiben wirksam.

Nutzerhinweis „ganzes Halbjahr“ im Kontext GK HJ 1+4 entsprechend der bestehenden Anforderung §6 Nr.12 als vollständiges **Schuljahr** (HJ 1+2 bzw. HJ 3+4) umgesetzt. Bei LF Wirtschaft bleiben GK im ersten und GEO im zweiten Schuljahr einzeln belegbar; GK HJ 3+4 bleibt gekoppelt. Bereits dokumentierte KOE-Sportersatz-Ausnahmen werden beibehalten. Allgemeine fachliche Freigabe für 2029 bleibt offen; kein neuer verbindlicher Gültigkeitsstatus.

Automatisches Entfernen des entsprechenden BF bei LF-Umwahl ist durch die Nutzerentscheidung vom 28.10.2026 freigegeben und wird sichtbar erklärt. Die erneute Prüfungsplanung und Pflichtbelegung bleiben erforderlich. Bestehende fehlerhafte Entwürfe werden beim Laden erhalten und mit `AGVO-SEMESTERS` markiert; keine stille Dateimigration.

Schülertexte am 28.10.2026 berichtigt: Die automatische Regelprüfung ist vorhanden. Die Schüler-Demo hat weiterhin keine verbindliche Abgabefunktion; jahrgangsbezogene fachliche Freigabe und offene persönliche Prüf-/Anrechnungsfragen liegen bei der Oberstufenberatung. Der veraltete Verweis auf spätere Ausbaustufen und technische Statusbegriffe in der Schüleranzeige sind durch konkrete Erläuterungen ersetzt. Interner Status `needs_review` und fachliche Sperren bleiben erhalten.

### Rück-/Vorweg in der Schülerwahl (29.10.2026)

- Fragen in „Pflichtbereiche“ zeigen alle bereits über HJ 1–4 belegten passenden Kurse als gewählt. Ersetzen per Optionsfeld nur, solange genau ein ersetzbarer Kurs belegt ist; sonst Kontrollkästchen ohne implizites Entfernen. Erneute Wahl eines belegten Kurses ändert nichts. Keine Regel- oder Schemaänderung.
- Schritt 1 erlaubt höchstens drei LF; Tausch nur durch vorheriges Abwählen. Wird ein LF, das ein BF ersetzt hatte, wieder abgewählt, stellt die laufende Sitzung das vorherige BF (Halbjahre, ggf. mündliche Planung bei freiem Platz) mit sichtbarem Hinweis wieder her, sofern weiterhin wählbar und konfliktfrei. Diese Erinnerung wird nicht in der Wahldatei gespeichert und endet beim Laden/Neustart (bewusst kein Schemaeingriff).
- Unbekannte Angebotskennungen aus geladenen Entwürfen bleiben erhalten und werden als `OFFER-SELECTION` gemeldet, beantworten aber keine Pflichtbereichsfrage.
- Nutzerfestlegung 29.10.2026: Bei einer zum Zeitpunkt der Kurswahl bekannten Sportbefreiung keine Einzelhalbjahre als Sportersatz. Ersatzkurse werden als ganze Schuljahre gewählt; ein Ersatzkurs mehr schadet nicht. Die KOE-Einzelhalbjahresregelung ist für einen späteren Ausfall während der Kursstufe gedacht. Schülerwahl bietet nur noch Schuljahrespaare an (Ausnahme unverändert GK/GEO bei LF Wirtschaft); `AGVO-SEMESTERS` beanstandet jedes andere Einzelhalbjahr in beiden Programmen. Regelversion bleibt `m2-2` wie bei den übrigen Festlegungen vom 29.10.2026, damit bestehende Entwürfe ladbar bleiben; solche Entwürfe mit Einzelhalbjahren werden beim Laden erhalten und beanstandet, nicht still korrigiert.
- Offen: Ein nachträglicher Sportausfall während der Kursstufe (dort zulässiger Einzelhalbjahres-Ersatz) ist nicht modelliert und bleibt Einzelfall der Oberstufenberatung.
