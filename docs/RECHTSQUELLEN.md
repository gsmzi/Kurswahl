# Quellenstand M1

Grundlage ist ausschließlich `GSM_Kurswahl_Anforderungen_Codex.md`, Stand 25.10.2026, ausdrücklich noch nicht rechtsverbindlich freigegeben.

Die dort genannten AGVO, KOE 2026, Leitfaden Abitur 2028 und Prozessbeschreibung wurden nicht mitgeliefert. Die spätere Geltung für Abitur 2029 ist offen. Es wird keine Rechtsfreigabe behauptet.

M1-Regeln prüfen technische Angebotskonsistenz und die ausdrücklich im Pflichtenheft genannten Angebotsgrenzen. Die vollständige AGVO-Prüfung einschließlich Anzahl/Kombination der LF, Pflichtkurse, Sportersatz, mündlicher Prüfungen und Anrechnung folgt erst in M2. Bis dahin enthält jedes Ergebnis `M1-COMPLETENESS` mit `needs_review`; technische Fehler haben Vorrang als Gesamtstatus `invalid`.

## Regelkatalog (Regelversion `m1-1`)

Alle Regeln sind in `GsmKurswahl.Rules` definiert (`ChoiceValidator.Definitions`, `CatalogRules.Definitions`). Ein Test stellt sicher, dass jede definierte Regel-ID hier aufgeführt ist. „Pflichtenheft“ meint `GSM_Kurswahl_Anforderungen_Codex.md`; keine Regel beruht bisher auf einer geprüften AGVO-/KOE-Fundstelle.

| Regel-ID | Kurzbeschreibung | Quelle | Geltungsbereich | Schweregrad |
| --- | --- | --- | --- | --- |
| `M1-COMPLETENESS` | Unvollständige fachliche Prüfung; M1 nie grün | Pflichtenheft §§6,17 | Alle M1-Wahlen | needs_review |
| `PROFILE-TRUST` | Vertrauenswürdiges Einzelprofil fehlt | Pflichtenheft §5a | Alle Schülerwahlen | needs_review |
| `OFFER-VERSION` | Jahrgang und Angebots-/Regelversion passen | Pflichtenheft §§4,8 | Alle Wahlen | invalid |
| `OFFER-SELECTION` | Angebot existiert, ist freigegeben, Variante und Halbjahre passen | Pflichtenheft §§4,8 | Jede Belegung | invalid |
| `SUBJECT-DUPLICATE` | Keine doppelte Fachbelegung über Kursarten/Varianten | Pflichtenheft §6 Nr.1,7 | Alle Fächer/Varianten | invalid |
| `PREREQUISITE-NO` | Persönliche Voraussetzung nicht erfüllt | Pflichtenheft §§4,5 | INF/I/bilingual | invalid |
| `PREREQUISITE-UNKNOWN` | Persönliche Voraussetzung ungeklärt | Pflichtenheft §§5,5a | INF/I/bilingual | needs_review |
| `CATALOG-LF-SEMESTERS` | LF-Angebot umfasst HJ 1–4 | Pflichtenheft §6 Nr.1,12 | LF-Angebote | invalid |
| `CATALOG-LF-EXCLUDED` | INF, I, ETH, ER, KR am GSM nicht als LF | Pflichtenheft §4 | LF-Angebote | invalid |
| `CATALOG-PSY` | Psychologie: Wahlfach, HJ 1–2, zweistündig | Pflichtenheft §4 | PSY-Angebote | invalid |
| `CATALOG-SIA` | SIA: Seminarkurs HJ 1–2, dreistündig | Pflichtenheft §4 | SIA-Angebote | invalid |
| `CATALOG-GEO-BF` | Geographie-BF nur HJ 3–4 | Pflichtenheft §4 | GEO-BF-Angebote | invalid |
| `CATALOG-INF` | Informatik: BF HJ 1–4, dreistündig, mit Vorbildungsprüfung | Pflichtenheft §4, §6 Nr.5 | INF-Angebote | invalid |
| `CATALOG-I-PROFILE` | Italienisch nur mit Profilprüfung | Pflichtenheft §4, §6 Nr.6 | I-Angebote | invalid |
| `CATALOG-BILINGUAL` | Bilinguale Variante nur mit Bili-Vorbildungsprüfung | Pflichtenheft §4, §6 Nr.7 | Bilinguale Angebote | invalid |

`CATALOG-*`-Regeln prüfen die Angebotskonfiguration. Ein Verstoß sperrt das betroffene Angebot mit sichtbarer Regel-ID und macht eine Wahl dieses Angebots `invalid`; der übrige Katalog bleibt nutzbar. Diese Regeln waren zuvor als Strukturprüfung im Domain-`ModelGuard` hinterlegt und haben den gesamten Katalog abgewiesen. Inhaltlich sind die Grenzen unverändert, deshalb bleibt die Regelversion `m1-1` und bestehende Entwürfe bleiben ladbar.

## Wahlassistent (25.10.2026, Regelversion m1-2)

Die ergänzenden PLAN-Regeln steuern ausschließlich die vom Nutzer gewünschte vorläufige Planung. Vollständige AGVO-/Profil-/Anrechnungsprüfung bleibt M2; der Gesamtstatus bleibt needs_review. Die bisherige technische Validate-Prüfung bleibt unverändert, der Assistent ruft die Teilprüfungen separat auf.

| Regel-ID | Kurzbeschreibung | Quelle | Geltungsbereich | Schweregrad |
| --- | --- | --- | --- | --- |
| `PLAN-LF` | Genau drei LF, mindestens zwei Demo-Kernfächer | Pflichtenheft §6 Nr.1; Nutzerablauf 25.10.2026 | M1-Demo-Assistent | invalid |
| `PLAN-ORAL` | Zwei unterschiedliche BF-Prüfungsplanungen; D/M und drei Aufgabenfelder; GEO ausgeschlossen | Pflichtenheft §6 Nr.8,11 | M1-Demo-Prüfungsplanung | invalid |
| `PLAN-REQUIRED` | Eindeutige Pflichtkurse ergänzen; Wahlalternativen anzeigen | Pflichtenheft §4; §6 Nr.3,4,8,10 | M1-Demo-Belegungsplanung | needs_review |
| `PLAN-AREA-ART` | Kunst oder Musik mit HJ 1–4 planen | Pflichtenheft §6 Nr.3 | M1-Demo-Belegungsplanung | needs_review |
| `PLAN-AREA-RELIGION` | Religion oder Ethik mit HJ 1–4 planen; Konfession/Zustimmung offen | Pflichtenheft §6 Nr.3,9 | M1-Demo-Belegungsplanung | needs_review |
| `PLAN-AREA-SCIENCE` | Zwei Naturwissenschaften mit HJ 1–4 planen; INF zählt nur als zweite | Pflichtenheft §6 Nr.3,4,5 | M1-Demo-Belegungsplanung | needs_review |
| `PLAN-MIN-COURSES` | Planungsziel mindestens 42 Halbjahreskurse | Pflichtenheft §6 Nr.2 | M1-Demo-Belegungsplanung, keine Anrechnung | needs_review |

Die Demo-Zuordnung kennt Englisch als einzige bestätigte fortgeführte Sprache im Demo-Katalog. Weitere Sprachkennungen werden nicht geraten. Religion/Ethik und Sport bleiben auch bei vorläufiger Auswahl ungeklärt. Alle Aufgabenfelder sind nur eine notwendige Teilbedingung, keine Aussage zur Zulassung oder 40-Kurs-Anrechnung.

Ab m1-2 werden die Metadaten aller bestehenden Regeln unter dem neuen Regelstand geführt; fachlich hinzugekommen sind die oben aufgeführten PLAN-Regeln. Neue Entwürfe tragen m1-2, abweichende Regelstände bleiben beim Laden geschützt gesperrt.

### Aufgabenmodell (26.10.2026)

`PLAN-AREA-*` und `PLAN-MIN-COURSES` geben den bisher als freie Hinweistexte ausgegebenen offenen Wahlen (Kunst/Musik, Religion/Ethik, Naturwissenschaften, 42 Kurse) eine stabile Regel-ID und eine strukturierte Form (`ChoiceWorkflow.Tasks`): Optionen, bereits durch LF erfüllte Fächer, gewählte Angebote, nicht wählbare Angebote mit Begründung. Inhaltlich kommt keine neue Bedingung hinzu, `Validate` ist unverändert und alle Aufgaben haben den Schweregrad needs_review. Deshalb bleibt der Regelstand `m1-2`, und vorhandene Entwürfe bleiben ladbar.

Die Naturwissenschaftsfrage plant zwei Naturwissenschaften, weil das Demoangebot keine zweite bestätigte fortgeführte Fremdsprache kennt; die Fremdsprachen-Alternative bleibt needs_review. Die Religion/Ethik-Frage prüft keine Konfession oder Zustimmung (§6 Nr.9 bleibt M2).
