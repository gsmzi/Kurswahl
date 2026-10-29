param([switch]$Reset)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $projectRoot 'src/GsmKurswahl.StudentApp/bin/Release/net10.0/GsmKurswahl.StudentApp.exe'
$verifiedExe = Join-Path $projectRoot 'artifacts/hours-warning-build/bin/GsmKurswahl.StudentApp/release/GsmKurswahl.StudentApp.exe'
if (Test-Path -LiteralPath $verifiedExe) { $exe = $verifiedExe }
$demoDir = Join-Path $projectRoot 'artifacts/demo-schueler-blank'

if (-not (Test-Path -LiteralPath $exe)) {
    $env:AVALONIA_TELEMETRY_OPTOUT = '1'
    dotnet build (Join-Path $projectRoot 'src/GsmKurswahl.StudentApp/GsmKurswahl.StudentApp.csproj') -c Release --no-restore -maxcpucount:1
}

New-Item -ItemType Directory -Force -Path $demoDir | Out-Null
Copy-Item (Join-Path $projectRoot 'samples/fachangebot-demo.json') (Join-Path $demoDir 'fachangebot.json') -Force
Copy-Item (Join-Path $projectRoot 'samples/profil-demo-unsigned.json') (Join-Path $demoDir 'profil.json') -Force

if ($Reset) {
    Remove-Item (Join-Path $demoDir 'aktuell.json') -ErrorAction SilentlyContinue
    Write-Output "Wahldatei zurückgesetzt. Blanker Schülerordner bereit: $demoDir"
} elseif (-not (Test-Path (Join-Path $demoDir 'aktuell.json'))) {
    Write-Output "Blanker Schülerordner bereitgestellt (ohne bisherige Wahl): $demoDir"
} else {
    Write-Output "Schülerordner mit bisheriger Wahl geöffnet (zum Zurücksetzen: -Reset angeben): $demoDir"
}

Start-Process -FilePath $exe -ArgumentList @('--wahlordner', ('"' + $demoDir + '"'))
