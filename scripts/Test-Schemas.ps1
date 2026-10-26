$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($pair in @(
    @('samples/schueler-demo.json', 'schemas/course-choice.v2.schema.json'),
    @('samples/fachangebot-demo.json', 'schemas/offer-catalog.v1.schema.json'),
    @('samples/profil-demo-unsigned.json', 'schemas/student-profile.v1.schema.json')
)) {
    $valid = Get-Content (Join-Path $root $pair[0]) -Raw | Test-Json -SchemaFile (Join-Path $root $pair[1])
    if (-not $valid) { throw "Schemafehler: $($pair[0])" }
    Write-Output "Schema OK: $($pair[0])"
}
