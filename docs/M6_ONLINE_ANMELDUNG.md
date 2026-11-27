# M6 (Ausbaustufe): Online-Kurswahl mit Anmeldung über das Schulverzeichnis (LDAP)

Stand 27.11.2026. **Konzeptpapier, nicht beauftragt und nicht umgesetzt.** Diese Ausbaustufe liegt außerhalb von Version 1 (Anforderungen Abschnitt 2: kein Anwendungsserver, kein Webserver, keine eigene LDAP-Anmeldung). Sie setzt einen abgeschlossenen und im Schulnetz erprobten Stand M1 bis M5 voraus. Bis zur ausdrücklichen Freigabe durch den Nutzer, die Schulleitung und den Datenschutz bleibt es bei dateibasiertem Austausch über die persönlichen Ordner. Auslöser: Am GSM steht jetzt ein LDAP-Zugang zur Verfügung (Nutzerhinweis 27.11.2026).

## 1. Ziel und Nutzen

Schülerinnen und Schüler wählen im Browser und melden sich dort mit ihrem Schulkonto an. Gegenüber dem dateibasierten Ablauf bringt das:

- **Zugang von überall:** von zu Hause, vom Tablet oder Mac, ohne Schülerprogramm und ohne Verteilung von Programm, `profil.json` und `fachangebot.json` in die persönlichen Ordner.
- **Vertrauenswürdige Datenlage:** Profil und Wahl liegen auf dem Server und sind für Schüler nicht manipulierbar. Abgabezeitpunkt und Wahlversionen werden serverseitig protokolliert.
- **Sofortiger Überblick:** keine Verzögerung durch Einsammeln, kein Ordner-Zuordnungsproblem (`Klasse\Nachname.Vorname`).
- **Wahlzeitraum steuerbar:** Öffnen und Schließen der Wahl je Jahrgang sowie Nachwahl für Einzelne.

Was sich **nicht** ändert: Die Oberstufenberatung arbeitet weiter im Lehrerprogramm mit der geschützten `verwaltung.db`. Die verbindliche Prüfung gegen den Lehrerbestand vor Übernahme und ASV-Abgleich bleibt bestehen. `needs_review` bleibt `needs_review`. Planungsszenarien verändern keine aktiven Wahlen.

## 2. Abgrenzung

- Keine Verwaltung der Lehrerseite im Browser (Stammdatenimport, Kursplanung, ASV-Abgleich bleiben Desktop). Eine spätere Ausbaustufe kann das neu bewerten.
- Keine Passwortspeicherung, keine eigene Benutzerverwaltung, kein Single Sign-on in Version M6.0 (spätere Option: OIDC/SAML, falls die Schulplattform es anbietet).
- Keine automatische Verbindung des Desktop-Schülerprogramms zum Server. Es bleibt als Rückfallebene unverändert (`Keine automatischen Onlinezugriffe der Desktopanwendungen`).
- Kein Schreiben nach ASV.

## 3. Architektur

```
Browser (Schüler)  ──HTTPS──>  Kurswahlserver (ASP.NET Core, .NET 10)
                                 ├─ Anmeldung: LDAP-Bind (LDAPS/StartTLS) ──> Schulverzeichnis
                                 ├─ Application/Rules/Domain (gemeinsamer Regelkern, unverändert)
                                 └─ Serverdatenbank (SQLite, ein Prozess)
                                        ▲                       │
                     Profile/Fachangebot│ Ausgabe               │ abgegebene Wahlen
                                        │                       ▼
                            Lehrerprogramm (Desktop, verwaltung.db) – Vorschau + bestätigte Übernahme
```

- **Neues Projekt `GsmKurswahl.Server`:** referenziert `Application`, `Rules`, `Domain` und eine neue Komponente `ServerStorage` (nur Domain-Abhängigkeit, analog zur Schichtregel in `AGENTS.md`). Die Regelprüfung läuft serverseitig in C# über dieselbe `StudentSession`-Logik wie im Desktopprogramm. Keine Regeln in Weboberfläche oder Anmeldemodul.
- **Weboberfläche:** serverseitig gerendert (z. B. Razor Pages) oder schlanke Seite mit Server-API. Die Ergebnisanzeige kommt immer vom Server, der Browser prüft nicht verbindlich. Die Browser-Testversion `web/Kurswahltest/` bleibt eine reine Erprobungsseite und wird nicht zur Produktivversion.
- **Schnittstellen im Domain-Modell:** `ICurrentIdentityProvider` (heute Windows-Kontoname) erhält eine Serverimplementierung aus der angemeldeten Sitzung; `IChoiceStore` eine Datenbankimplementierung. Das Desktop-Schülerprogramm ist davon nicht betroffen.
- **Datenhaltung:** eine SQLite-Datenbank, die nur der Serverprozess öffnet (kein SMB-Zugriff). Wahlen append-only mit Version, Zeitstempel und Herkunft „online“. Profil und Fachangebot je Jahrgang als vom Lehrerprogramm ausgegebene Stände mit Revision.

## 4. Anmeldung über LDAP

- **Ablauf:** Benutzername und Passwort werden einmalig per HTTPS an den Server gesendet. Der Server sucht mit einem nur lesenden Dienstkonto den Eintrag, prüft die Gruppenmitgliedigkeit und führt einen Bind mit den Benutzerdaten durch. Danach ausschließlich Sitzungscookie (`Secure`, `HttpOnly`, `SameSite=Strict`), Passwort wird verworfen und nirgends protokolliert.
- **Verschlüsselung:** nur LDAPS (Port 636) oder StartTLS mit geprüftem Zertifikat. Kein unverschlüsselter Simple Bind.
- **Bibliothek:** `System.DirectoryServices.Protocols` (Microsoft, plattformübergreifend). Version pinnen, Lizenz-/Sicherheitsstand in `ABHAENGIGKEITEN.md`.
- **Berechtigung:** Rolle ausschließlich aus Gruppen bzw. OU des Verzeichnisses (z. B. Schüler des Abiturjahrgangs). Ein Konto sieht nur die eigene Wahl. Ohne passende Gruppe kein Zugriff, auch nicht lesend.
- **Zuordnung Konto ↔ Schüler-ID:** Der Server vertraut nicht dem Benutzernamen allein. Maßgeblich ist die Zuordnung im Lehrerbestand (bestätigte Kontozuordnung, wie heute bei der Ordnerzuordnung). Bevorzugt über ein eindeutiges, stabiles Verzeichnisattribut (z. B. ASV-ID in `employeeID`, falls vom Netzwerk gepflegt), sonst Kontoname mit administrativ bestätigter Zuordnung. Unklare Zuordnung → kein Wahlzugang, Hinweis auf die Oberstufenberatung.
- **Schutz:** Begrenzung von Fehlversuchen je Konto und Adresse, CSRF-Schutz, Sitzungsablauf, Abmelden, Sicherheitsheader, keine Fehlermeldung, die verrät, ob ein Konto existiert.

## 5. Austausch mit dem Lehrerprogramm

Kein direkter Datenbankzugriff zwischen Lehrerprogramm und Server. Zwei versionierte Austauschwege nach `DATEIFORMAT.md` (neue Formate, Schemadateien, eingefrorene Testdateien):

1. **Ausgabe an den Server:** Das Lehrerprogramm erzeugt je Jahrgang ein Paket aus Fachangebot, minimierten Profilen (wie `profil.json`, Schema v2) und Konto-Zuordnungen. Hochladen über eine Verwaltungsseite oder einen geschützten Austauschordner, mit Vorschau und Bestätigung auf dem Server. Neue Revision ersetzt alte nur für noch nicht abgegebene Wahlen nicht stillschweigend: geänderte Voraussetzungen markieren betroffene Wahlen als `needs_review`.
2. **Rückgabe an das Lehrerprogramm:** Der Server stellt abgegebene Wahlen im bestehenden Wahlschema (`aktuell.json`-Struktur, v2) bereit. Das Lehrerprogramm übernimmt sie über den vorhandenen Einsammelweg (Vorschau, bestätigte Übernahme, Prüfung gegen `verwaltung.db`, neue Revision mit Herkunft „online“).

So bleibt der Lehrerbestand der Vertrauensanker; der Server ist nur Erfassungsweg.

## 6. Betrieb und Infrastruktur

- **Hosting:** Schulserver im Verwaltungs- oder pädagogischen Netz, oder externer Dienstleister mit Auftragsverarbeitungsvertrag. Für Zugriff von außen: Reverse-Proxy, TLS-Zertifikat, Firewall-Freigabe, Erreichbarkeit des LDAP-Servers nur vom Kurswahlserver aus (nicht aus dem Internet).
- **Wartung:** Zuständigkeit für Updates von Betriebssystem, .NET-Laufzeit und Programm klären. Programmpaket analog `Publish-Release.ps1`, Versionsnummer wie `build/Version.targets`.
- **Sicherung:** tägliche Sicherung der Serverdatenbank, Wiederherstellungsprobe, Aufbewahrungs- und Löschfristen je Jahrgang.
- **Protokollierung:** Anmeldungen, Abgaben und Verwaltungsaktionen ohne Passwörter und ohne Wahlinhalt im technischen Log.
- **Ausfall:** Fällt der Server im Wahlzeitraum aus, bleibt das Desktop-Schülerprogramm mit Ordnerverteilung als Rückfallebene. Doppelte Wahlen aus beiden Wegen erkennt das Lehrerprogramm über Version und Zeitstempel und legt sie zur Entscheidung vor (kein automatisches Zusammenführen).

## 7. Datenschutz und Freigaben (vor jeder Umsetzung)

- Zustimmung Schulleitung, Einbindung der/des Datenschutzbeauftragten.
- Eintrag ins Verzeichnis der Verarbeitungstätigkeiten; Prüfung, ob eine Datenschutz-Folgenabschätzung nötig ist.
- Information der Schüler und Eltern (Zweck, Speicherdauer, Ansprechpartner).
- Datenminimierung: auf dem Server nur Fachangebot, minimierte Profile, Wahlen und Kontozuordnung; keine Noten, Diagnosen oder Begründungen.
- Abstimmung mit dem Netzwerkbetreuer über Dienstkonto, Gruppen, Firewall und Zertifikate.

## 8. Umsetzungsschritte (Vorschlag)

| Schritt | Inhalt | Ergebnis |
| --- | --- | --- |
| M6.0 Klärung | Fragen aus Abschnitt 9 beantworten, Freigaben einholen | Entscheidung Ja/Nein, Hostingmodell |
| M6.1 Prototyp | Serverprojekt, LDAP-Anmeldung gegen lokalen Test-LDAP mit synthetischen Konten, Wahl mit Demo-Katalog | lauffähiger Prototyp, Tests, kein Echtbetrieb |
| M6.2 Austausch | Ausgabe- und Rückgabeformat, Übernahme im Lehrerprogramm, Kompatibilitätstests | durchgehender Ablauf mit synthetischen Daten |
| M6.3 Härtung | Sicherheitsprüfung (Anmeldung, Sitzungen, Rechte), Last mit Jahrgangsgröße, Sicherung/Wiederherstellung | Prüfbericht im Testprotokoll |
| M6.4 Pilot | Betrieb auf dem Schulserver mit Testkonten, danach ein Pilotjahrgang parallel zum Dateiweg | Pilotnachweis, keine fingierte Freigabe |

Alle Schritte nur mit synthetischen Personen und Konten in Code, Tests und Protokollen. Test-LDAP z. B. als lokaler OpenLDAP-Container im Testlauf; keine Verbindung zum Schulverzeichnis aus der Entwicklungsumgebung.

## 9. Offene Fragen an Schule und Netzwerkbetreuung

1. Welches Verzeichnissystem (paedML Windows/Active Directory, paedML Linux/OpenLDAP, IServ, anderes)? Pädagogisches oder Verwaltungsnetz?
2. LDAP-Serveradresse, LDAPS oder StartTLS, Zertifikatsaussteller, Base-DN.
3. Gibt es Gruppen oder OUs je Klasse bzw. Abiturjahrgang? Wie heißen sie, wer pflegt sie zum Schuljahreswechsel?
4. Welches Attribut identifiziert ein Konto dauerhaft (Kontoname, `objectGUID`/`entryUUID`, ASV-ID)? Ändern sich Kontonamen (z. B. Namensänderung, Endziffer `2`)?
5. Wird ein nur lesendes Dienstkonto bereitgestellt?
6. Nur Schulnetz oder auch von zu Hause? Falls von außen: welcher Server, welche Domain, wer stellt das Zertifikat?
7. Wer betreibt und aktualisiert den Server, wer sichert ihn?
8. Freigabe durch Schulleitung und Datenschutz; Informationsschreiben an Schüler/Eltern.
9. Soll das Desktop-Schülerprogramm parallel als Rückfallebene erhalten bleiben (Empfehlung: ja, mindestens im ersten Online-Jahrgang)?
