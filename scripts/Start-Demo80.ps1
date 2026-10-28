param([switch]$PrepareOnly, [switch]$GkGeo)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $projectRoot 'src/GsmKurswahl.TeacherApp/bin/Release/net10.0/GsmKurswahl.TeacherApp.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    $exe = Join-Path $projectRoot 'artifacts/m5-ui-build/bin/GsmKurswahl.TeacherApp/release/GsmKurswahl.TeacherApp.exe'
}
$demoRoot = Join-Path $projectRoot 'artifacts/demo-80'
if ($GkGeo) { $demoRoot = Join-Path $projectRoot 'artifacts/demo-80-gk-geo' }
if (-not (Test-Path -LiteralPath $exe)) {
    $env:AVALONIA_TELEMETRY_OPTOUT = '1'
    dotnet build (Join-Path $projectRoot 'src/GsmKurswahl.TeacherApp/GsmKurswahl.TeacherApp.csproj') -c Release --no-restore -maxcpucount:1
    $exe = Join-Path $projectRoot 'src/GsmKurswahl.TeacherApp/bin/Release/net10.0/GsmKurswahl.TeacherApp.exe'
}
$mode = if ($PrepareOnly) { '--prepare-demo-80' } else { '--demo-80' }
if ($PrepareOnly) {
    $process = Start-Process -FilePath $exe -ArgumentList @($mode, ('"' + $demoRoot + '"')) -PassThru -WindowStyle Hidden
} else {
    $process = Start-Process -FilePath $exe -ArgumentList @($mode, ('"' + $demoRoot + '"')) -PassThru
}
if ($PrepareOnly) {
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw 'Erstellen des Testbestands fehlgeschlagen. Vorhandene Ordner werden nicht ersetzt.' }
    Write-Output "Testbestand bereit: $demoRoot"
}
