# Builds the ZIP for colleagues: both programs self-contained plus one batch file per test stage.
# The stages themselves are generated on the tester's computer (see DemoStages.cs), so the package holds no data.
# This file stays ASCII: Windows PowerShell 5.1 misreads UTF-8 without BOM.
$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$repo = Split-Path $PSScriptRoot -Parent
$packageId = [guid]::NewGuid().ToString('N')
$name = 'GSM-Kurswahl-Testpaket-' + (Get-Date -Format 'yyyy-MM-dd-HHmmss') + '-' + $packageId.Substring(0, 8)
$output = Join-Path $repo "artifacts/testpaket/$name"
$system = Join-Path $output '_system'
New-Item -ItemType Directory -Path $system | Out-Null

Push-Location $repo
try {
    foreach ($app in @('StudentApp', 'TeacherApp')) {
        & dotnet publish "src/GsmKurswahl.$app" -c Release --self-contained true -r win-x64 -p:PublishTrimmed=false -p:RestoreLockedMode=true `
            -p:RestoreConfigFile=NuGet.Config -p:NuGetLockFilePath=packages.win-x64.lock.json -p:RuntimeFrameworkVersion=10.0.14 `
            --artifacts-path (Join-Path $repo 'artifacts/m5-publish-build') -o (Join-Path $system $app)
        if ($LASTEXITCODE -ne 0) { throw "Publish $app fehlgeschlagen; kein ZIP erstellt." }
    }
}
finally { Pop-Location }
# The test package must never know the real school drive.
Remove-Item -LiteralPath (Join-Path $system 'TeacherApp/schulvorgaben.json') -Force

function Write-Batch([string]$path, [string[]]$lines) {
    [IO.File]::WriteAllText($path, (($lines + '') -join "`r`n"), [Text.Encoding]::ASCII)
}

# Each click starts a new local run; the share is only read. No tester data is deleted.
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'testpaket/Start-Teststand.ps1') -Destination $system
[IO.File]::WriteAllText((Join-Path $system 'paket-id.txt'), $packageId, [Text.Encoding]::ASCII)
Write-Batch (Join-Path $system 'stand.cmd') @(
    '@echo off'
    'rem Aufruf: stand.cmd lehrer/schueler/durchlauf ^<stand^>'
    'setlocal'
    'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Teststand.ps1" -Mode "%~1" -Stage "%~2"'
    'if errorlevel 1 goto fehler'
    'exit /b 0'
    ':fehler'
    'echo Teststart fehlgeschlagen. Bitte die Meldung oben an die Betreuung weitergeben.'
    'pause'
    'exit /b 1'
)

$starters = [ordered]@{
    'Lehrer 1 - Ganz am Anfang'                   = 'lehrer lehrer-1-anfang'
    'Lehrer 2 - Schueler importiert'              = 'lehrer lehrer-2-importiert'
    'Lehrer 3 - Ausgeteilt, noch keine Wahlen'    = 'lehrer lehrer-3-ausgeteilt'
    'Lehrer 4 - Wahlen zum Einsammeln bereit'     = 'lehrer lehrer-4-wahlen-abgegeben'
    'Lehrer 5 - Wahlen eingesammelt'              = 'lehrer lehrer-5-eingesammelt'
    'Lehrer 6 - Kurs gestrichen, Nachwahl offen'  = 'lehrer lehrer-6-nachwahl'
    'Schueler 1 - Blanko'                         = 'schueler schueler-1-blanko'
    'Schueler 2 - Wahl angefangen'                = 'schueler schueler-2-angefangen'
    'Schueler 3 - Wahl fertig'                    = 'schueler schueler-3-fertig'
    'Schueler 4 - Blanko mit Bili, INF, Italienisch' = 'schueler schueler-4-alle-voraussetzungen'
    'Schueler 5 - Blanko mit Sportbefreiung'      = 'schueler schueler-5-sportbefreit'
    'Schueler 6 - Ohne besondere Voraussetzungen' = 'schueler schueler-6-ohne-zusatzrechte'
    'Schueler 7 - Vorbildung unbekannt'          = 'schueler schueler-7-vorbildung-unbekannt'
    'Durchlauf - Schueler und Lehrer gemeinsam'  = 'durchlauf lehrer-3-ausgeteilt'
}
foreach ($starter in $starters.GetEnumerator()) {
    Write-Batch (Join-Path $output ($starter.Key + '.cmd')) @('@echo off', ('call "%~dp0_system\stand.cmd" ' + $starter.Value))
}

Write-Batch (Join-Path $output 'Testdaten-Ordner oeffnen.cmd') @(
    '@echo off'
    'if not exist "%LOCALAPPDATA%\GSM-Kurswahl-Testpaket" mkdir "%LOCALAPPDATA%\GSM-Kurswahl-Testpaket"'
    'start "" explorer.exe "%LOCALAPPDATA%\GSM-Kurswahl-Testpaket"'
)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'testpaket/LIESMICH.txt') -Destination $output
Compress-Archive -LiteralPath $output -DestinationPath ($output + '.zip') -CompressionLevel Optimal
Write-Output "Testpaket: $output.zip"
