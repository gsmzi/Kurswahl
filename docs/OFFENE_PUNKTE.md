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
