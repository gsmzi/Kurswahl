# M4: Kursplanung und Speichervertrag

Stand: 28.10.2026. Der Nutzerauftrag erweitert den bisherigen M3-Umfang auf M4. M5 bleibt ausgenommen. Die fachlichen Freigabegrenzen von M2 gelten weiter.

## Schichten

`Domain/Planning.cs` enthält immutable Daten und `IPlanningStore`, ohne Dateisystem oder UI. `Application/CoursePlanning.cs` berechnet Nachfrage, Kombinationen, Überschneidungen und Vorschauen; persönliche Verfügbarkeit und vollständige Validierung stammen aus dem vorhandenen Regelkern. `AdministrationStorage/PlanningStorage.cs` speichert ausschließlich Domain-Daten und hat keine Application-/Rules-Abhängigkeit. Das Schülerprogramm referenziert AdministrationStorage weiterhin nicht.

## Explizite Versionierung

Bestehende SQLite-Datenbanken bleiben beim Öffnen in `PRAGMA user_version=1`. Erst **Kursplanung aktivieren** erweitert sie in einer Transaktion auf Version 2. Bestehende Tabellen, Daten und JSON-Verträge bleiben unverändert. Unbekannte Datenbankversionen werden abgewiesen. Ein älteres Programm unterstützt Datenbankschema 2 nicht; es wird keine Rückmigration angeboten.

Schema 2 ergänzt append-only Tabellen:

| Tabelle | Inhalt |
| --- | --- |
| planning_snapshots | Laufende Sequenz, eindeutige Dokument-ID und vollständiger JSON-Snapshot |
| planning_decisions | Laufende Sequenz und bestätigte Angebotsentscheidung als JSON |

Beide Tabellen haben UPDATE-/DELETE-Sperrtrigger. Bestätigte Angebotsentscheidung, Verwaltungsrevisionen, Basis-Snapshot und Nachwahlvermerke werden gemeinsam in einer SQLite-Transaktion geschrieben. Eine fehlgeschlagene Zeile rollt den gesamten Vorgang zurück.

## JSON-Verträge (intern, Version 1)

`PlanningDocument`:

- `schemaVersion=1`, `id`, `createdAt`, `name`, `actor`, `ruleVersion`;
- `catalog`: vollständiger Katalog nach bestehendem Schema 1;
- `students`: vollständige versionierte `StudentRecord`-Snapshots einschließlich Vorbildung, Abgängerstatus und Herkunft;
- `choices`: vollständige aktive `ChoiceRevision`-Snapshots mit Wahl-Schema 2;
- `followUps`: Schüler-ID, Nachwahlpflicht, Frist und Notiz;
- `includeExpectedDepartures`, `targetGroupSize` (1–100, Standard 24);
- `isScenario` und `basisId` kennzeichnen eine Simulation und deren Ausgangsstand.

`PlanningDecision`: `schemaVersion=1`, `id`, `at`, `actor`, `reason`, `basisId`, vollständiger `catalog`.

Leser prüfen die Versionen; unbekannte Versionen werden nicht migriert. Die internen Snapshots sind kein neuer Importkanal. Szenario-Revisionen haben eigene IDs und bleiben ausschließlich im Szenario. Eine Simulation wird niemals als Basis einer direkten Übernahme akzeptiert: Zur Übernahme wird eine neue Vorschau gegen den aktuellen Bestand erstellt.

## Konkurrenz und Wiederaufnahme

Vor der Übernahme werden Schülerbestand, aktive Wahlrevisionen, Nachwahlvermerke und das zuletzt bestätigte Angebot mit der Vorschau verglichen. Auch Änderungen bei nicht von der Streichung betroffenen Personen erzwingen eine neue Vorschau. Der reine Abgängerfilter schließt niemanden aus der offiziellen Änderung aus.

Nach einer bestätigten Planung ist das zuletzt bestätigte Angebot in SQLite maßgeblich. `workspace.json` wird anschließend aktualisiert. Bei einem Abbruch dazwischen wird die Kopie beim nächsten Öffnen aus SQLite wiederhergestellt. Eine doppelte Übernahme derselben Vorschau wird abgewiesen. Der Verteilungsstatus berücksichtigt den Zeitpunkt der Angebotsentscheidung und fordert eine erneute Katalogzustellung.

## Fachliche Bedeutung

Streichungen verwenden genaue Angebotskennungen. BIO LF bilingual und BIO LF normal bleiben getrennt. Nur ausgewählte Belegungen entfallen; es wird kein Ersatz automatisch eingetragen. Aktive Wahlen werden gegen den neuen Katalog und die geschützten Voraussetzungen geprüft. `needs_review` bleibt offen und wird bei betroffenen Personen als Nachwahl-/Klärungsbedarf erhalten. Bestehende Nachwahlvermerke werden nicht stillschweigend abgeschlossen.

Nachfrage zählt Personen je Angebot und Halbjahr; doppelte Zählung einer Person wird vermieden. LF-Kombinationen sind nach Angebotskennung sortiert. Paarweise gemeinsame Belegungen bilden eine dünn besetzte Überschneidungsmatrix je Halbjahr (nicht belegte Paare haben den Wert 0). Bestätigte Abgänger sind nie planungsrelevant; voraussichtliche nach dem gespeicherten Umschalter. Gruppenschätzungen sind `ceil(planungsrelevant / Zielgröße)` und verändern weder Wahlen noch Unterrichtsgruppen.

Eine Angebotswiederherstellung erzeugt eine neue Katalogversion und neue Verwaltungsrevisionen. Sie trägt keine früher entfernten Schülerwünsche automatisch ein; die bewusste Einzelwiederherstellung bleibt im Schülerbestand verfügbar.

## Kompakte Darstellung nach Arbeitsphase (28.10.2026)

Die UI trennt Wahlorganisation und Vorbildung von Wahlergebnissen und Kursplanung. Die Daten-/Regelverträge bleiben unverändert. Abgängeränderungen in der Ergebnistabelle speichern eine neue Stammdatenrevision mit Versionsprüfung, ohne Voraussetzungen oder Kurswahlen zu ersetzen. Planung zählt danach anhand dieser aktuellen Abgängermerkmale.

PlanningDisplay komprimiert ausschließlich identische Zählwerte: vier gleiche Werte zu einer Zahl, gleiche Paare zu KS1/KS2. Abweichungen innerhalb einer Jahrgangsstufe bleiben nach Halbjahr aufgeschlüsselt. Fehlender Zeitraum (null) bleibt von Nachfrage 0 unterscheidbar. Fach, Kursart und bilinguale Variante werden in der Matrix nicht zusammengelegt.
