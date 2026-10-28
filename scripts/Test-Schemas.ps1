$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($pair in @(
    @('src/GsmKurswahl.StudentApp/appsettings.json', 'schemas/student-settings.v2.schema.json'),
    @('samples/schueler-demo.json', 'schemas/course-choice.v2.schema.json'),
    @('samples/fachangebot-demo.json', 'schemas/offer-catalog.v1.schema.json'),
    @('samples/profil-demo-unsigned.json', 'schemas/student-profile.v1.schema.json')
)) {
    $valid = Get-Content (Join-Path $root $pair[0]) -Raw | Test-Json -SchemaFile (Join-Path $root $pair[1])
    if (-not $valid) { throw "Schemafehler: $($pair[0])" }
    Write-Output "Schema OK: $($pair[0])"
}
foreach ($synthetic in @(
    @('{"schemaVersion":1,"keyId":"demo-key-01","algorithm":"ECDSA-P256-SHA256-P1363","signature":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=="}', 'schemas/profile-signature.v1.schema.json'),
    @('{"schemaVersion":1,"directory":"C:\\Demo\\Lehrerverwaltung"}', 'schemas/teacher-location.v1.schema.json'),
    @('{"schemaVersion":1,"activeYear":null,"years":[]}', 'schemas/teacher-workspace.v1.schema.json')
)) {
    $valid = $synthetic[0] | Test-Json -SchemaFile (Join-Path $root $synthetic[1])
    if (-not $valid) { throw "Schemafehler: $($synthetic[1])" }
    Write-Output "Schema OK: $($synthetic[1])"
}
$catalog = Get-Content (Join-Path $root 'samples/fachangebot-demo.json') -Raw | ConvertFrom-Json
$transfer = @{ schemaVersion = 1; createdAt = '2026-10-28T10:00:00Z'; year = @{ catalog = $catalog; databasePath = 'verwaltung.db'; step = 'complete'; actor = 'demo-transfer'; certificateThumbprint = ''; keyId = '' }; databaseHash = ('A' * 64) } | ConvertTo-Json -Depth 30
if (-not ($transfer | Test-Json -SchemaFile (Join-Path $root 'schemas/teacher-transfer.v1.schema.json'))) { throw 'Transport-Schemafehler' }
Write-Output 'Schema OK: schemas/teacher-transfer.v1.schema.json'
