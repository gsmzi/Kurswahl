$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixtures = 'tests/GsmKurswahl.Storage.Tests/Compatibility'
# Samples plus the frozen compatibility fixtures: every schema is checked against real program output (current)
# and against historical files that must stay readable (legacy).
foreach ($pair in @(
    @('src/GsmKurswahl.StudentApp/appsettings.json', 'schemas/student-settings.v2.schema.json'),
    @('samples/schueler-demo.json', 'schemas/course-choice.v2.schema.json'),
    @('samples/fachangebot-demo.json', 'schemas/offer-catalog.v2.schema.json'),
    @('samples/profil-demo-unsigned.json', 'schemas/student-profile.v2.schema.json'),
    @('src/GsmKurswahl.TeacherApp/schulvorgaben.json', 'schemas/school-defaults.v1.schema.json'),
    @("$fixtures/current/choice-v2.json", 'schemas/course-choice.v2.schema.json'),
    @("$fixtures/current/catalog-v2.json", 'schemas/offer-catalog.v2.schema.json'),
    @("$fixtures/current/profile-v2.json", 'schemas/student-profile.v2.schema.json'),
    @("$fixtures/current/workspace-v2.json", 'schemas/teacher-workspace.v2.schema.json'),
    @("$fixtures/current/transfer-v2.json", 'schemas/teacher-transfer.v2.schema.json'),
    @("$fixtures/legacy/choice-v1.json", 'schemas/course-choice.v1.schema.json'),
    @("$fixtures/legacy/catalog-v1.json", 'schemas/offer-catalog.v1.schema.json'),
    @("$fixtures/legacy/profile-v1.json", 'schemas/student-profile.v1.schema.json'),
    @("$fixtures/legacy/workspace-v1.json", 'schemas/teacher-workspace.v1.schema.json'),
    @("$fixtures/legacy/transfer-v1.json", 'schemas/teacher-transfer.v1.schema.json'),
    @("$fixtures/legacy/location-v1.json", 'schemas/teacher-location.v1.schema.json')
)) {
    $valid = Get-Content (Join-Path $root $pair[0]) -Raw | Test-Json -SchemaFile (Join-Path $root $pair[1])
    if (-not $valid) { throw "Schemafehler: $($pair[0])" }
    Write-Output "Schema OK: $($pair[0])"
}
