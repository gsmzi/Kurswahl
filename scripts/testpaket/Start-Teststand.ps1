# ASCII for Windows PowerShell 5.1. Invoked by the package's .cmd files.
param(
    [Parameter(Mandatory = $true)][ValidateSet('lehrer', 'schueler', 'durchlauf')][string]$Mode,
    [Parameter(Mandatory = $true)][string]$Stage,
    # Prepare without opening windows, for package verification.
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
function Start-LocalApp([string]$exe, [string]$arguments) {
    # ProcessStartInfo avoids PowerShell 5.1 wildcard handling of paths containing square brackets.
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $exe
    $info.Arguments = $arguments
    $info.WorkingDirectory = [IO.Path]::GetDirectoryName($exe)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    return [Diagnostics.Process]::Start($info)
}
try {
    if ($Stage -notmatch '^(lehrer|schueler)-[1-7]-[a-z0-9-]+$') { throw 'Ungueltiger Teststand.' }
    if (($Mode -eq 'lehrer' -and -not $Stage.StartsWith('lehrer-')) -or
        ($Mode -eq 'schueler' -and -not $Stage.StartsWith('schueler-')) -or
        ($Mode -eq 'durchlauf' -and $Stage -ne 'lehrer-3-ausgeteilt')) { throw 'Startart passt nicht zum Teststand.' }
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA) -or -not [IO.Path]::IsPathRooted($env:LOCALAPPDATA) -or $env:LOCALAPPDATA.StartsWith('\\')) {
        throw 'LOCALAPPDATA muss auf einen lokalen Benutzerordner zeigen.'
    }
    $packageId = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'paket-id.txt')).Trim()
    if ($packageId -notmatch '^[a-f0-9]{32}$') { throw 'Paketkennung fehlt oder ist ungueltig.' }
    $localRoot = Join-Path $env:LOCALAPPDATA ('GSM-Kurswahl-Testpaket\' + $packageId)
    $program = Join-Path $localRoot 'Programm'
    [IO.Directory]::CreateDirectory($localRoot) | Out-Null

    # One local copy per package/user. Exclusive file lock also covers simultaneous first starts.
    $copyLock = $null
    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    Write-Host 'Testprogramme werden lokal bereitgestellt ...'
    while ($null -eq $copyLock) {
        try { $copyLock = [IO.File]::Open((Join-Path $localRoot 'programm.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] {
            if ([DateTime]::UtcNow -ge $deadline) { throw 'Ein anderer Start belegt noch die Programmkopie. Bitte spaeter erneut versuchen.' }
            Start-Sleep -Milliseconds 250
        }
    }
    try {
        $complete = Join-Path $program 'kopie.complete'
        if (-not [IO.File]::Exists($complete)) {
            [IO.Directory]::CreateDirectory($program) | Out-Null
            foreach ($app in @('StudentApp', 'TeacherApp')) {
                # LiteralPath handles spaces, parentheses and square brackets, including UNC sources.
                Copy-Item -LiteralPath (Join-Path $PSScriptRoot $app) -Destination $program -Recurse -Force
            }
            [IO.File]::WriteAllText($complete, $packageId)
        }
        foreach ($app in @('StudentApp', 'TeacherApp')) {
            foreach ($file in @("GsmKurswahl.$app.exe", 'hostfxr.dll', 'coreclr.dll')) {
                if (-not [IO.File]::Exists((Join-Path (Join-Path $program $app) $file))) { throw 'Lokale Programmkopie unvollstaendig. Bitte Paketordner ueber Testdaten-Ordner oeffnen pruefen.' }
            }
        }
    }
    finally { $copyLock.Dispose() }

    # Always new: no live database is overwritten, deleted or shared with a second click.
    # Short folder names leave room for SQLite and the nested pupil folders on Windows.
    $prefix = $Stage.Substring(0, 1).ToUpperInvariant() + $Stage.Split('-')[1]
    $run = Join-Path $localRoot ('Laeufe\' + $prefix + '-' + [guid]::NewGuid().ToString('N'))
    $teacher = Join-Path $program 'TeacherApp\GsmKurswahl.TeacherApp.exe'
    $student = Join-Path $program 'StudentApp\GsmKurswahl.StudentApp.exe'
    Write-Host 'Frischer Teststand wird erzeugt. Das kann beim ersten Start ein bis zwei Minuten dauern ...'
    $preparation = Start-LocalApp $teacher ('--prepare-test-stage ' + $Stage + ' "' + $run + '"')
    $preparation.WaitForExit()
    $exitCode = $preparation.ExitCode
    $preparation.Dispose()
    if ($exitCode -ne 0) {
        $report = $run + '-fehler.txt'
        if ([IO.File]::Exists($report)) { throw [IO.File]::ReadAllText($report) }
        throw "Vorbereitung abgebrochen (Exitcode $exitCode)."
    }
    Write-Host "Testdaten: $run"
    if ($PrepareOnly) { Write-Output $run; return }
    if ($Mode -ne 'schueler') {
        (Start-LocalApp $teacher ('--test-stage ' + $Stage + ' "' + $run + '"')).Dispose()
    }
    if ($Mode -ne 'lehrer') {
        $data = if ($Mode -eq 'durchlauf') { 'Austeilordner\10a\Testperson001.Demo\Kurswahl\Daten' } else { 'Kurswahl\Daten' }
        (Start-LocalApp $student ('--wahlordner "' + (Join-Path $run $data) + '"')).Dispose()
    }
    if ($Stage -eq 'lehrer-1-anfang') { Start-Process explorer.exe -ArgumentList ('"' + $run + '"') | Out-Null }
}
catch {
    Write-Host ('Teststart fehlgeschlagen: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
