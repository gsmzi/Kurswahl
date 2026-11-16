param(
    [string]$CatalogPath = 'samples/fachangebot-demo.json',
    [string]$OutputDirectory = 'artifacts/kurswahltest'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$webDir = Join-Path $repo 'web/Kurswahltest'

$resolvedCatalog = if ([IO.Path]::IsPathRooted($CatalogPath)) { $CatalogPath } else { Join-Path $repo $CatalogPath }
if (-not (Test-Path -LiteralPath $resolvedCatalog)) {
    throw "Fachangebot nicht gefunden: $resolvedCatalog"
}
$resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $repo $OutputDirectory }

$utf8NoBom = [Text.UTF8Encoding]::new($false)
$catalogText = [IO.File]::ReadAllText($resolvedCatalog, [Text.Encoding]::UTF8)
$catalogObj = $catalogText | ConvertFrom-Json
if ($null -eq $catalogObj.schemaVersion -or [string]::IsNullOrWhiteSpace($catalogObj.version) -or $null -eq $catalogObj.graduationYear -or $catalogObj.subjects.Count -eq 0 -or $catalogObj.offers.Count -eq 0) {
    throw "Ungültiges Fachangebot in $resolvedCatalog"
}

$targetJson = Join-Path $webDir 'fachangebot.json'
$targetJs = Join-Path $webDir 'catalog.js'
[IO.File]::WriteAllText($targetJson, $catalogText, $utf8NoBom)
[IO.File]::WriteAllText($targetJs, "window.GSM_DEFAULT_CATALOG = " + $catalogText.Trim() + ";`n", $utf8NoBom)

New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
foreach ($file in @('index.html', 'styles.css', 'kurswahltest.js', 'catalog.js', 'fachangebot.json', '.nojekyll')) {
    $src = Join-Path $webDir $file
    if (-not (Test-Path -LiteralPath $src)) {
        throw "Webdatei fehlt: $src"
    }
    Copy-Item -LiteralPath $src -Destination (Join-Path $resolvedOutput $file) -Force
}

Write-Output "Kurswahltest-Bundle erstellt unter: $resolvedOutput (Angebot $($catalogObj.version), Abitur $($catalogObj.graduationYear))"
