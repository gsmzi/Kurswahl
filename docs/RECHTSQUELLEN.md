# Quellenstand M1

### Stundenwarnung (29.10.2026)

`GSM-HOURS-UPPER`: Nutzerfestlegung 29.10.2026; Version m2-2, Geltungsbereich GSM-Wahlplanung je Halbjahr, Schweregrad needs_review. Bei strikt mehr als 37 gewählten Wochenstunden in mindestens einem Halbjahr: „Mehr als 37 Unterrichtsstunden pro Woche werden in der Regel nicht genehmigt.“ Betroffene Halbjahre und tatsächliche Wochenstunden werden aufgeführt. Keine automatische Ablehnung und keine erfundene gesetzliche Obergrenze. Gemeinsamer Regelkern für beide Programme; Schüler-App zeigt die Warnung außerhalb der Scrollbereiche dauerhaft in allen Schritten, bis die Überschreitung entfällt. Speichern/Laden ändert die Warnbedingung nicht.

### Gegenseitiger Ausschluss Religion/Ethik (29.10.2026)

`GSM-RELIGION-EXCLUSIVE`: Nutzerfestlegung vom 29.10.2026 und Pflichtenheft §6 Nr.9; Regelversion m2-2, Geltungsbereich GSM-Kurswahlen, Schweregrad invalid. ETH/KR/ER dürfen in keinem Halbjahr gleichzeitig belegt werden. Zeitlich getrennte Wechsel werden dadurch nicht pauschal verboten; bestehende Pflicht-/Wechsel- und Prüfungsregeln gelten weiter. Der gemeinsame Regelkern beanstandet auch geladene/importierte Doppelbelegungen mit betroffenen Fächern und Halbjahren, ohne den Bestand zu ändern. Mündliche Zweierkombinationen dieser Fächer werden ausgeschlossen. Zusatzangebote verstecken konkurrierende Fächer; bewusste Wechsel erfolgen im Pflichtbereich oder über geänderte mündliche Planung mit ersetzter bisheriger Belegung und sichtbarem Hinweis.

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

## M2-Regelkatalog (m2-1, 26.10.2026)

Die vorigen Abschnitte dokumentieren den historischen M1-Stand. M2 verwendet Regelversion `m2-1` und ersetzt `M1-COMPLETENESS` durch `M2-RELEASE`. Alle fortgeführten technischen und PLAN-Regeln tragen ebenfalls m2-1. Alte Entwürfe bleiben unverändert und werden bei abweichender Regel-/Angebotsversion geschützt abgewiesen; keine stille Migration.

Amtliche Recherche am 26.10.2026: [AGVO-Gesamtausgabe, letzte Änderung 04.02.2025](https://www.landesrecht-bw.de/bsbw/document/jlr-GymAbiPrVBW2018V2P26/part/X), [Ministerium: Leitfaden Abitur 2028](https://km.baden-wuerttemberg.de/de/service/publikation/did/leitfaden-fuer-die-gymnasiale-oberstufe-2028), [ZSL: Ratgeber Schulsport](https://zsl-bw.de/ZSL%2CLen/startpage/uebergreifendes/ratgeber-schulsport). Die Landesrecht-Gesamtausgabe war im Suchindex zugänglich, beim direkten Öffnen nur als JavaScript-Hinweis. §17 wurde zusätzlich nur in einer historischen Fassung im Index gefunden: **keine Freigabe der Anrechnung für 2029 daraus ableiten**. Volltextabgleich mit den angeforderten AGVO-/KOE-Dateien und Freigabe für 2029 bleiben offen. Implementierte Bedingungen folgen dem ausdrücklich freigegebenen Pflichtenheft; Quellenangaben benennen den noch abzugleichenden Rechtsbezug.

Geltungsbereich aller folgenden Regeln: GSM-Kurswahl, strukturelle Prüfung unter jahrgangsbezogenem Freigabevorbehalt. Version: m2-1. Die Prüfungsoptionen sind Möglichkeiten aus tatsächlich belegten Kursen, keine verbindlichen Prüfungswahlen. Anrechnung wird über Mengen von Fach/Halbjahr getrennt von der Belegungszahl berechnet; Sonderfälle und Schulversuch bleiben needs_review.

| Regel-ID | Prüfung / Quelle | Schweregrad |
| --- | --- | --- |
| `M2-RELEASE` | Fehlende jahrgangsbezogene fachliche Freigabe; Pflichtenheft §§6,18 | needs_review |
| `AGVO-LF` | Drei verschiedene LF, mindestens zwei Kernfächer; §12 / Pflichtenheft §6 Nr.1 | invalid |
| `AGVO-COURSES` | 12 LF + mindestens 30 weitere Halbjahreskurse; §§12,13 / Pflichtenheft §6 Nr.2 | invalid |
| `AGVO-REQUIRED` | Pflichtfächer, LF erfüllt BF-Pflicht, GK/GEO mit/ohne Wirtschaft; §13 / Pflichtenheft §§4,6 | invalid |
| `AGVO-LANGUAGE-SCIENCE` | Eine Sprache und erste NW, dazu zweite Sprache/NW oder berechtigtes INF; §13 / Pflichtenheft §6 Nr.4–6 | invalid |
| `AGVO-SEMESTERS` | Jahrgangsbelegung HJ 1+2 bzw. HJ 3+4; einzige Ausnahme GK/GEO bei LF Wirtschaft; kein Einzelhalbjahres-Sportersatz bei der Kurswahl (Nutzerfestlegung 29.10.2026); §10 / Pflichtenheft §6 Nr.12 | invalid |
| `AGVO-RELIGION` | Zustimmungen/Wechsel ungeklärt; BF Ethik nicht wegen fehlender Vorbildung sperren; §§11,21 / Pflichtenheft §6 Nr.9 | needs_review |
| `AGVO-SPORT` | Befreiung/Umfang, LF-Sport und administrative Ersatzzuordnung; §13 Abs.5 / Pflichtenheft §6 Nr.10 | needs_review |
| `AGVO-SPORT-REPLACEMENT` | Zusätzlich zu sonstiger Pflichtbelegung benötigte BF-/Wahlfachkurse; SIA ausgeschlossen; §13 Abs.5 / ZSL Ratgeber | invalid |
| `AGVO-EXAM` | Aus belegten Kursen mindestens eine Zweierkombination mit D/M und drei Aufgabenfeldern; §21 / Pflichtenheft §6 Nr.11 | invalid |
| `AGVO-CREDIT` | Vorläufige Pflichtanrechnung, 40-Grenze, keine BLL-Pauschalanerkennung; §§17,21 / Pflichtenheft §6 Nr.11 | needs_review |
| `AGVO-ELECTION` | Wahlphase/Endwahltermin noch nicht freigegeben; §14 / Pflichtenheft §6 Nr.13 | needs_review |
| `PLAN-HOURS` | Durchschnittliche Wochenstunden, Hinweis bei unter 32; Pflichtenheft §6 | needs_review |
| `PLAN-AREA-BREADTH` | Zweite bestätigte Sprache als Alternative zur zweiten NW; Pflichtenheft §6 Nr.4–6 | needs_review |

`AcademicRulesTests` deckt Belegungszahl, Kern-LF, Pflichtfächer, Sprachlaufbahn/Italienisch, INF nur als zweite NW, Sportersatz trotz 42 Kursen, SIA-Ausschluss, Sport-LF, Religion/Ethik-Prüfungsfreigabe, Semester, Kombinationen und getrennte Anrechnung ab. Technische Profilablehnungen haben Status profile_invalid und führen über PROFILE-TRUST zur Klärung; Kryptoprüfung ist keine Rechtsregel. Englische Sprachlaufbahn ist weiterhin eine explizite Annahme des synthetischen Demoangebots; weitere Sprachen werden nur aus administrativ signierten Laufbahnbestätigungen berücksichtigt.

| `AGVO-LANGUAGE-REVIEW` | Ungeklärte persönliche Sprachlaufbahn oder INF-Alternative nicht als falsch werten; §13 / Pflichtenheft §6 Nr.4–6 | needs_review |

### Korrektur Sprach-/Naturwissenschaftsplanung (26.10.2026)

`PLAN-AREA-LANGUAGE`: mindestens eine geeignete fortgeführte Fremdsprache für HJ 1–4; Pflichtenheft §6 Nr.3,4,6; Schweregrad needs_review im Assistenten, fehlende Belegung wird fachlich separat invalid. PLAN-AREA-SCIENCE plant nur die erste Naturwissenschaft BIO/CH/PH. PLAN-AREA-BREADTH bietet immer zweite Fremdsprache oder zweite Naturwissenschaft an, unabhängig davon, ob ein signiertes Profil vorliegt. INF bleibt nur zweite NW, Italienisch benötigt zusätzlich die bestätigte Sprachlaufbahn. Englisch, Französisch und Latein sind nach Nutzerbestätigung die regulären Sprachangebote; F/L jeweils LF/BF im Standardkatalog. Die tatsächliche persönliche Laufbahn ist vor Produktivbetrieb administrativ abzugleichen. Keine automatische Festlegung auf Englisch bei fehlendem Sprach-LF.

### Aktualisierung m2-2 (26.10.2026)

Alle Regelmetadaten tragen jetzt m2-2; neu/berichtigend PLAN-AREA-LANGUAGE und die immer sichtbare Alternative PLAN-AREA-BREADTH. F/L im regulären GSM-Sprachkanon nach Nutzerbestätigung. Die erste Sprache wird nicht mehr automatisch auf Englisch festgelegt. `LanguageChoiceTests` prüft fehlende Fremdsprache, F/L oder CH als gleichwertige zweite Belegung, Wechsel CH -> F ohne Verlust der ersten NW, zwei Sprachen ohne NW und zwei NW ohne Sprache.

### Abgleich mit AGVO (Stand 04.02.2025) und KOE 2026 (Stand 05.02.2026) am 26.10.2026

Die vollständige **AGVO** (`docs/Rechtsgrundlage AGVO BW GymAbiPrV_BW_2018.pdf`, Stand 04.02.2025), die **Allgemeinen Hinweise Abitur 2025 (AOE)** sowie der **Kursstufen-Organisationserlass (KOE) 2026** (RP Stuttgart, Stand 05.02.2026) wurden im Volltext geprüft und mit der Regelimplementierung abgeglichen:

- **Sportersatzkurse (`AGVO-SPORT`, `AGVO-SPORT-REPLACEMENT`, `AGVO-SEMESTERS`):**
  - Rechtsquelle: **AGVO § 13 Abs. 5**, **KOE 2026 Kap. 2.1 (S. 2), Kap. 2.3 (S. 4) und Kap. 3.4 (S. 7)**.
  - Jeder zusätzliche Kurs in anderen Basisfächern aus dem **Pflicht- oder Wahlbereich** (`CourseType.Bf` und `CourseType.Elective`, z. B. auch Psychologie) kann befreite Sportkurse ersetzen; insgesamt müssen mindestens **42 Kurse ohne Seminarkurs (SIA)** erreicht werden. Der Seminarkurs ist kein Basiskurs im Sinne der AGVO und kann nicht als Sportersatz eingebracht werden (KOE Kap. 3.4 S. 7, Kap. 3.8.1 S. 8).
  - Ersatzkurse müssen nicht in denselben Halbjahren besucht werden, in denen die Sportbefreiung vorliegt, und dürfen ausnahmsweise als **Einzelhalbjahreskurse** belegt werden (KOE Kap. 2.1 S. 2, Kap. 2.3 S. 4, Kap. 3.4 S. 7). **Nutzerfestlegung 29.10.2026 (GSM):** Diese Einzelhalbjahresregelung ist für einen späteren Ausfall während der Kursstufe gedacht. Wer bei der Kurswahl in Klasse 10 bereits von der Sportbefreiung weiß, wählt Ersatzkurse ordnungsgemäß als ganze Schuljahre; ein Ersatzkurs mehr schadet nicht. `AGVO-SEMESTERS` lässt deshalb bei der Kurswahl keine Einzelhalbjahre als Sportersatz zu, und die Schülerwahl bietet sie nicht an. Ein nachträglicher Ausfall in der Kursstufe ist nicht modelliert und bleibt Einzelfall der Oberstufenberatung.
  - Bei auch nur teilweiser Sportbefreiung entfällt Sport sowohl als Leistungsfach (**AGVO § 12 Abs. 3 Satz 5**) als auch als mündliches Prüfungsfach (**AGVO § 21 Abs. 2 Nr. 6**; in `ChoiceWorkflow.OralCandidates` gesperrt).
- **Religion und Ethik (`AGVO-RELIGION`, `PLAN-ORAL`):**
  - Rechtsquelle: **AGVO § 11 Abs. 2–3, § 12 Abs. 3 Satz 4, § 13 Abs. 1 Nr. 7 & Abs. 4, § 21 Abs. 2 Nr. 4–5**, **KOE 2026 Kap. 3.3 (S. 6) und Kap. 8.6.3 (S. 35)**.
  - Als mündliches Prüfungsfach kann regulär nur das bereits in der Einführungsphase (Klasse 10, zumindest 2. Halbjahr mit Zeugnisnote) besuchte Fach (`ReligionEthicsGrade10`) gewählt werden.
  - Wechselt eine Schülerin oder ein Schüler gegenüber Klasse 10 das Fach in der Kursstufe oder plant ein in Klasse 10 nicht besuchtes Fach (`ETH`, `ER`, `KR`) als mündliches Prüfungsfach, weist `AGVO-RELIGION` bzw. `PLAN-ORAL` explizit darauf hin, dass für eine mündliche Prüfung eine **Überprüfung/Feststellungsprüfung zu Beginn des 1. Schulhalbjahres** erforderlich ist (bzw. bei Abmeldung von Religionslehre zu Ethik in der Kursstufe nach KOE Kap. 3.3 weder Religionslehre noch Ethik als mündliches Prüfungsfach gewählt werden kann).
- **Gemeinschaftskunde, Geographie und Wirtschaft (`AGVO-REQUIRED`, `AGVO-EXAM`, `AGVO-CREDIT`):**
  - Rechtsquelle: **AGVO § 10 Abs. 3–4, § 13 Abs. 1 Nr. 6 & Abs. 2 Satz 3, § 17 Abs. 1 Nr. 2 g & Nr. 3, § 21 Abs. 2 Nr. 2**, **KOE 2026 Kap. 3.2 (S. 5–6) und Kap. 8.6.2 (S. 34–35)** sowie **AOE 2025 Ziff. 4.19–4.20 (S. 23)**.
  - Die frühere Kombinationsprüfung GEO/GK ist entfallen. Bei LF Wirtschaft ist in GK und GEO jeweils nur der zuerst unterrichtete Pflichtkurs (am GSM: GK HJ 1, GEO HJ 3) beleg- und anrechnungspflichtig; für die mündliche Prüfung in GK genügen dieser Pflichtkurs und die beiden Wahlkurse (HJ 1, 3, 4 = 3 anzurechnende Kurse).
- **Informatik und Naturwissenschaften (`AGVO-LANGUAGE-SCIENCE`, `CATALOG-INF`):**
  - Rechtsquelle: **AGVO § 13 Abs. 1, § 21 Abs. 2 Nr. 7**, **KOE 2026 Kap. 3.6–3.7 (S. 8)**.
  - Bestätigt: Das 3-stündige Basisfach Informatik im Schulversuch ersetzt die 2. Naturwissenschaft auch dann, wenn die 1. Naturwissenschaft nur als Basisfach belegt ist; für die mündliche Prüfung in Informatik genügt Brückenkurs Kl. 10 oder IMP Kl. 8–10 in Verbindung mit 4 Halbjahren BF Informatik.
- **Vorbehalt für Abitur 2029 (`M2-RELEASE`):**
  - Da KOE 2026 formell für das Abitur 2026 gilt, bleibt der jahrgangsbezogene Freigabevorbehalt (`M2-RELEASE`, `needs_review`) für den ersten Einsatzjahrgang 2029 bestehen.

### Fehlerkorrektur Halbjahresbelegung (28.10.2026)

Religion/Ethik, Nutzerpräzisierung 28.10.2026: `AGVO-RELIGION` (m2-2, needs_review, GSM-Jahrgangsvorbehalt; Pflichtenheft §6 Nr.9) verlangt bei bestätigter Fortführung desselben Fachs aus Klasse 10 keine zusätzliche pauschale Zustimmung. Explizites Nein und fehlende vertrauenswürdige Vorbildung bleiben Klärfälle. Beim Wechsel KR/ER -> ETH erscheint der gewünschte Hinweis auf beantragten Austritt und notwendige Feststellungsprüfung direkt. Dies ändert keine mündliche Zulassung: Der dokumentierte Widerspruch zur KOE-Regel bei Religionsabmeldung bleibt fachlich zu klären; Feststellungsprüfung allein wird nicht als hinreichende Freigabe behandelt.

`AGVO-SEMESTERS`, Regelstand `m2-2`, Geltungsbereich GSM-Kurswahl unter unverändertem Jahrgangsvorbehalt, Schweregrad `invalid`: Umsetzung der bereits geltenden Jahresbelegung aus AGVO §10 / Pflichtenheft §6 Nr.12 berichtigt. Nutzerhinweis vom 28.10.2026 konkretisiert den Fehlerfall GK HJ 1+4 bei LF Wirtschaft. Regulär gehören HJ 1+2 und HJ 3+4 zusammen. Die Wirtschaftsausnahme aus Pflichtenheft §4 betrifft nur GK HJ 1/2 und GEO HJ 3/4; zusätzliche GK-Kurse HJ 3+4 bleiben ein vollständiges Schuljahr. Die früher pauschale Ausnahme sämtlicher GK-/GEO-Belegungen ist entfernt. Bestätigte Sportersatz-Einzelkurse bleiben nach den oben dokumentierten KOE-Regeln auf die Zahl bestätigter Befreiungshalbjahre begrenzt; keine neue Ausnahme oder Rechtsfreigabe.

Die Schüleroberfläche bezieht ihre Belegungseinheiten aus dem gemeinsamen Regelkern und schaltet reguläre Jahrespaare zusammen. Die Validierung prüft ebenso gespeicherte/importierte Einzelbelegungen, ohne sie zu verändern. Keine Schema- oder normative Regeländerung; Wahlformat v2 und Regelstand m2-2 bleiben lesbar. `AcademicRulesTests` prüft sämtliche nichtleeren Halbjahresteilmengen für GK, GEO und ein reguläres BF mit/ohne LF Wirtschaft sowie unveränderte fehlerhafte Bestandswahlen; die vorhandenen Sportersatztests bleiben bestehen.

Beim Wechsel BF -> LF gilt die ausdrücklich gewünschte Bedienänderung vom 28.10.2026 zu Pflichtenheft §7: Die BF-Belegung desselben Fachs wird unabhängig von der Variante automatisch durch das gewählte LF ersetzt und mit sichtbarem Hinweis erklärt. Andere Fächer bleiben erhalten. Prüfungsplanung und Pflichtbelegung werden beim erneuten Durchlaufen überprüft/ergänzt; keine automatische Ersatzwahl für ein abgewähltes LF.
