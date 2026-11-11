# Integration check against a fully extracted package; no installed .NET used by its launcher.
param([Parameter(Mandatory = $true)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$repo = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $repo ('artifacts\testpaket-check-' + [guid]::NewGuid().ToString('N'))
$local = Join-Path $testRoot 'Lokaler Test (1) [Demo]'
[IO.Directory]::CreateDirectory($local) | Out-Null
$previousLocal = $env:LOCALAPPDATA
$env:LOCALAPPDATA = $local
function Check([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Prepare([string]$stage, [string]$mode = 'lehrer') {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $package '_system\Start-Teststand.ps1') -Stage $stage -Mode $mode -PrepareOnly
    Check ($LASTEXITCODE -eq 0) "Start fehlgeschlagen: $stage"
}
function Runs([string]$stage) {
    $prefix = $stage.Substring(0, 1).ToUpperInvariant() + $stage.Split('-')[1]
    @(Get-ChildItem -LiteralPath (Join-Path $localRoot 'Laeufe') -Filter ($prefix + '-*') -Directory)
}
try {
    $packageId = [IO.File]::ReadAllText((Join-Path $package '_system\paket-id.txt')).Trim()
    $localRoot = Join-Path $local ('GSM-Kurswahl-Testpaket\' + $packageId)
    $sourceHashes = @(Get-ChildItem -LiteralPath $package -File -Recurse | Get-FileHash -Algorithm SHA256)
    # Simultaneous first starts must complete one cache and produce separate fresh student runs.
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $package '_system\Start-Teststand.ps1') + '" -Stage schueler-1-blanko -Mode schueler -PrepareOnly'
    $jobs = @(1..2 | ForEach-Object {
        Start-Process powershell.exe -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $testRoot "parallel-$_.txt") -RedirectStandardError (Join-Path $testRoot "parallel-$_.err")
    })
    foreach ($job in $jobs) { $job.WaitForExit(); $job.Refresh(); Check ($job.ExitCode -eq 0) 'Paralleler Erststart fehlgeschlagen.' }
    $runs = Runs 'schueler-1-blanko'
    Check ($runs.Count -eq 2) 'Parallele Starts teilen einen Testlauf.'
    $original = $runs[0].FullName
    [IO.File]::WriteAllText((Join-Path $original 'eigene-aenderung.txt'), 'keep')
    Prepare 'schueler-1-blanko' 'schueler'
    Check ((Runs 'schueler-1-blanko').Count -eq 3) 'Neustart erzeugt keinen frischen Lauf.'
    Check ([IO.File]::ReadAllText((Join-Path $original 'eigene-aenderung.txt')) -eq 'keep') 'Neustart veraendert alten Lauf.'

    foreach ($stage in @('lehrer-1-anfang', 'lehrer-2-importiert', 'lehrer-3-ausgeteilt', 'lehrer-4-wahlen-abgegeben', 'lehrer-5-eingesammelt', 'lehrer-6-nachwahl')) {
        Prepare $stage
        $root = (Runs $stage)[0].FullName
        $profiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'Austeilordner') -Filter profil.json -Recurse -File).Count
        $choices = @(Get-ChildItem -LiteralPath (Join-Path $root 'Austeilordner') -Filter aktuell.json -Recurse -File).Count
        $expectedProfiles = if ($stage -match '^lehrer-[1-2]-') { 0 } else { 80 }
        $expectedChoices = if ($stage -match '^lehrer-[1-3]-') { 0 } elseif ($stage -eq 'lehrer-4-wahlen-abgegeben') { 60 } else { 80 }
        Check ($profiles -eq $expectedProfiles -and $choices -eq $expectedChoices) "Unerwartete Testdaten: $stage"
        $presets = Get-Content -LiteralPath (Join-Path $root 'schulvorgaben.json') -Raw | ConvertFrom-Json
        Check ($presets.distributionRoot -eq (Join-Path $root 'Austeilordner')) 'Verteilung zeigt nicht auf den lokalen Lauf.'
    }
    foreach ($stage in @('schueler-2-angefangen', 'schueler-3-fertig', 'schueler-4-alle-voraussetzungen', 'schueler-5-sportbefreit', 'schueler-6-ohne-zusatzrechte', 'schueler-7-vorbildung-unbekannt')) {
        Prepare $stage 'schueler'
        $root = (Runs $stage)[0].FullName
        Check ([IO.File]::Exists((Join-Path $root 'Kurswahl\Daten\profil.json'))) "Profil fehlt: $stage"
        $hasChoice = [IO.File]::Exists((Join-Path $root 'Kurswahl\Daten\aktuell.json'))
        Check ($hasChoice -eq ($stage -match '^schueler-[2-3]-')) "Unerwarteter Wahlstand: $stage"
    }
    Prepare 'lehrer-3-ausgeteilt' 'durchlauf'
    $runs = Runs 'lehrer-3-ausgeteilt'
    Check ($runs.Count -eq 2) 'Durchlauf verwendet den separaten Lehrer-3-Lauf erneut.'
    foreach ($run in $runs) {
        Check ([IO.File]::Exists((Join-Path $run.FullName 'Austeilordner\10a\Testperson001.Demo\Kurswahl\Daten\profil.json'))) 'Durchlauf-Schueler fehlt.'
    }
    foreach ($source in $sourceHashes) { Check ((Get-FileHash -LiteralPath $source.Path).Hash -eq $source.Hash) 'Paket wurde beim Start veraendert.' }
    Check (-not [IO.File]::Exists((Join-Path $localRoot 'Programm\TeacherApp\schulvorgaben.json'))) 'Echte Schulvorgaben im Testprogramm.'
    Write-Output "Testpaket-Pruefung erfolgreich. Testdaten bleiben unter $testRoot"
}
finally { $env:LOCALAPPDATA = $previousLocal }
