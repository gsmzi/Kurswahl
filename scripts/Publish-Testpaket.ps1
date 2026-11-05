# Builds the ZIP for colleagues: both programs self-contained plus one batch file per test stage.
# The stages themselves are generated on the tester's computer (see DemoStages.cs), so the package holds no data.
# This file stays ASCII: Windows PowerShell 5.1 misreads UTF-8 without BOM.
$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$repo = Split-Path $PSScriptRoot -Parent
$name = 'GSM-Kurswahl-Testpaket-' + (Get-Date -Format 'yyyy-MM-dd-HHmm')
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

# Paths may contain brackets (e.g. "Testpaket (1)"), so this script uses goto instead of if-blocks.
Write-Batch (Join-Path $system 'stand.cmd') @(
    '@echo off'
    'rem Aufruf: stand.cmd lehrer ^<stand^>   oder   stand.cmd schueler ^<stand^> ^<Datenordner im Stand^>'
    'setlocal'
    'set "STAND=%LOCALAPPDATA%\GSM-Kurswahl-Testpaket\%~2"'
    'set "LEHRER=%~dp0TeacherApp\GsmKurswahl.TeacherApp.exe"'
    'set "SCHUELER=%~dp0StudentApp\GsmKurswahl.StudentApp.exe"'
    'echo Teststand wird vorbereitet. Beim ersten Mal kann das ein bis zwei Minuten dauern ...'
    'start "" /wait "%LEHRER%" --prepare-test-stage "%~2" "%STAND%"'
    'if errorlevel 1 goto fehler'
    'if /i "%~1"=="schueler" goto schueler'
    'if /i "%~2"=="lehrer-1-anfang" start "" explorer.exe "%STAND%"'
    'start "" "%LEHRER%" --test-stage "%~2" "%STAND%"'
    'exit /b 0'
    ':schueler'
    'start "" "%SCHUELER%" --wahlordner "%STAND%\%~3"'
    'exit /b 0'
    ':fehler'
    'echo.'
    'type "%STAND%-fehler.txt"'
    'echo.'
    'echo.'
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
    'Schueler 1 - Blanko'                         = 'schueler schueler-1-blanko "Kurswahl\Daten"'
    'Schueler 2 - Wahl angefangen'                = 'schueler schueler-2-angefangen "Kurswahl\Daten"'
    'Schueler 3 - Wahl fertig'                    = 'schueler schueler-3-fertig "Kurswahl\Daten"'
    'Schueler 4 - Blanko mit Bili, INF, Italienisch' = 'schueler schueler-4-alle-voraussetzungen "Kurswahl\Daten"'
    'Schueler 5 - Blanko mit Sportbefreiung'      = 'schueler schueler-5-sportbefreit "Kurswahl\Daten"'
    'Durchlauf - Schueler zu Lehrer 3'            = 'schueler lehrer-3-ausgeteilt "Austeilordner\10a\Testperson001.Demo\Kurswahl\Daten"'
}
foreach ($starter in $starters.GetEnumerator()) {
    Write-Batch (Join-Path $output ($starter.Key + '.cmd')) @('@echo off', ('call "%~dp0_system\stand.cmd" ' + $starter.Value))
}

Write-Batch (Join-Path $output 'Testdaten-Ordner oeffnen.cmd') @(
    '@echo off'
    'if not exist "%LOCALAPPDATA%\GSM-Kurswahl-Testpaket" mkdir "%LOCALAPPDATA%\GSM-Kurswahl-Testpaket"'
    'start "" explorer.exe "%LOCALAPPDATA%\GSM-Kurswahl-Testpaket"'
)
Write-Batch (Join-Path $output 'Alles zuruecksetzen.cmd') @(
    '@echo off'
    'echo Alle Testdaten dieses Pakets werden geloescht, auch eigene Aenderungen in den Staenden.'
    'echo Vorher bitte alle Programmfenster der Kurswahl schliessen.'
    'echo.'
    'choice /c JN /m "Wirklich loeschen"'
    'if errorlevel 2 exit /b 0'
    'rmdir /s /q "%LOCALAPPDATA%\GSM-Kurswahl-Testpaket"'
    'if exist "%LOCALAPPDATA%\GSM-Kurswahl-Testpaket" goto offen'
    'echo Alle Staende sind zurueckgesetzt.'
    'pause'
    'exit /b 0'
    ':offen'
    'echo Nicht alles konnte geloescht werden. Ist noch ein Programmfenster offen?'
    'pause'
    'exit /b 1'
)

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'testpaket/LIESMICH.txt') -Destination $output
Compress-Archive -LiteralPath $output -DestinationPath ($output + '.zip') -CompressionLevel Optimal
Write-Output "Testpaket: $output.zip"
