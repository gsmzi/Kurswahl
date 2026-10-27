# Schülerseitige Profilprüfung M2

Der Schülerclient liest `profil.json` und `profil.sig` im gleichen konfigurierten Verzeichnis wie die Wahl. Er schreibt diese Dateien niemals. Das neue Profilschema v1 ist unabhängig vom unveränderten Wahlschema v2 und Katalogschema v1. Das synthetische Sample `profil-demo-unsigned.json` ist absichtlich **nicht signiert und nicht vertrauenswürdig**. Es gibt keine privaten Schlüssel im Repository. In Tests entstehen Schlüssel nur im Arbeitsspeicher.

## Austauschvertrag

`profil.json`: exakte UTF-8-Bytes, Modell `StudentProfile`, Schema `schemas/student-profile.v1.schema.json`. Minimierte Verwaltungsbestätigungen, keine Diagnose oder Begründung. `continuedLanguages` bestätigt die rechtlich geeignete fortgeführte Sprachlaufbahn; `italianProfile=yes` allein bestätigt das nicht. Leere/unbekannte Angaben werden nicht zu `yes`. `sportExemptSemesters=null` bedeutet unbekannt, `[]` ausdrücklich keine Befreiung. Die beiden Religion/Ethik-Freigaben sind administrative Entscheidungen für Belegung bzw. Prüfung; sie sind nicht aus einer Zeichenfolge zur Klasse-10-Vorbildung herleitbar.

`profil.sig`: JSON nach `schemas/profile-signature.v1.schema.json`, Algorithmus exakt `ECDSA-P256-SHA256-P1363`, Signatur Base64-kodierte 64-Byte-P1363-Darstellung (r || s), Hash SHA-256 über die **unveränderten Profilbytes**. Kein JSON-Kanonisierungsverfahren. Schon eine Änderung von Leerzeichen oder Zeilenenden erfordert eine neue Signatur. Unbekannte Algorithmen, andere Kurven, doppelte JSON-Eigenschaften und unbekannte Schemas werden abgewiesen. Beide Dateien maximal 64 KiB. Ein während der administrativen Verteilung inkonsistentes Dateipaar wird abgewiesen; der Entwurf bleibt erhalten.

## Vertrauensanker und Konto

Die spätere geschützte Installation stellt neben der EXE `profile-trust.json` bereit:

```json
{
  "schemaVersion": 1,
  "keys": [],
  "allowedAccountDomains": []
}
```

Dies ist absichtlich eine leere Konfiguration. Ein Schlüsselobjekt enthält `keyId` und `subjectPublicKeyInfo` (Base64 von DER SubjectPublicKeyInfo eines öffentlichen P-256-Schlüssels). Keine PEM-Privatschlüssel. Unbekannte oder mehrdeutige keyIds werden abgewiesen. Die Vertrauenskonfiguration wird **nicht** über eine Konfiguration im Schüler-Home oder `GSM_KURSWAHL_SETTINGS` umgeleitet.

`OperatingSystemIdentityProvider` liest auf Windows Name und SID aus `WindowsIdentity`. Beide müssen mit dem signierten Einzelprofil übereinstimmen; zusätzlich muss die Kontodomäne administrativ zugelassen sein. Ein frei gewählter gleichnamiger lokaler Account genügt nicht. Nicht-Windows liefert keine SID und kann mit diesem Adapter kein Profil freigeben. Die konkrete schulische Domäne, die SID-Zuordnung und die Schreibschutzrechte der Installation sind vor Betrieb zu bestätigen. Ein von Schülern veränderbares EXE-/Schlüsselverzeichnis wäre kein Vertrauensanker. Der Client prüft offline, führt keine LDAP- oder Internetabfrage durch und kann keine aktuelle Anmeldung am Schulserver beweisen.

## Fehler, Aktualisierung und M3-Grenze

Fehlendes Profil: `Missing` / `needs_review`. Ungültige Signatur, Identität, Jahrgang, Revision, Schema oder Vertrauenskonfiguration: `ProfileInvalid` / `profile_invalid`; alle Voraussetzungen werden auf unbekannt gesetzt. Der Entwurf bleibt ladbar und unverändert. Ein technischer Profilfehler ist von einer fachlich ungültigen Wahl getrennt. Im Programmstart wird das Profil einmal eingelesen; nach Verteilung einer neuen Revision neu starten. Revision und Ausstellungsstand stehen in den Profildetails und auf dem Kontrollbogen. Laufzeit-Reload ist nicht implementiert.

M3 implementiert erst Signierung, Verteilung und verbindliche Prüfung gegen aktuelle geschützte Stammdaten. Schlüssel offline im geschützten Verwaltungsbereich erzeugen/verwahren, nur öffentliche Schlüssel in die schreibgeschützte Schülerinstallation übernehmen. Für Rotation zuerst neuen öffentlichen Schlüssel verteilen, dann mit neuer keyId signieren; alten Prüfschlüssel nach administrativem Übergangszeitraum entfernen. Sicherung und Wiederherstellung des privaten Schlüssels nur in administrativem, verschlüsseltem Backup mit beschränkten Zugriffsrechten. Konkretes Verwahrungs-/Rotationsverfahren ist noch freizugeben. Keine Signierfunktion und kein privater Schlüssel im Schülerprogramm.

Offline kann ein noch korrekt signiertes altes Profil nicht sicher als veraltet erkannt werden. Die Lehrerversion muss daher immer neu gegen ihren aktuellen Stammdatenbestand prüfen; die Profilprüfung ersetzt diese Grenze nicht.

## M3 / 27.10.2026

Administrationssignierung und Einzel-/Sammelverteilung sind jetzt in der getrennten Komponente AdministrationStorage implementiert. Schülerauslieferung referenziert diese Komponente nicht. Private Schlüssel kommen ausschließlich aus dem administrativen Windows-Zertifikatspeicher; öffentlicher Prüfschlüssel kann separat exportiert werden. Details zu Bereitstellung, Rotation, Verteilung, Wiederholung und Fehlern: [Lehrerverwaltung](LEHRERVERWALTUNG.md). Die vorstehenden Aussagen „folgt M3“ beschreiben den historischen M2-Stand. Profilschema, Signaturformat und Schülerprüfung bleiben unverändert.
