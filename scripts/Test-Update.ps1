# Checks scripts/release/Update-Kurswahl.ps1 with tiny synthetic packages (no real program build needed):
# first update, kept school presets, refusal while the teacher program runs, tampered and older packages,
# deliberate return to an older version and the backup limit. Runs the script with Windows PowerShell 5.1
# (as on school computers) when available.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('gsm-update-test-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$shell = if (Get-Command powershell.exe -ErrorAction SilentlyContinue) { 'powershell.exe' } else { 'pwsh' }

function New-Package([string]$version, [string]$marker, [switch]$Tamper) {
    $name = "GSM-Kurswahl-$version"
    $dir = Join-Path $work "bau\$name"
    foreach ($sub in 'TeacherApp', 'StudentApp', 'docs', '_Update') { New-Item -ItemType Directory -Path (Join-Path $dir $sub) -Force | Out-Null }
    Set-Content -LiteralPath (Join-Path $dir 'TeacherApp\GsmKurswahl.TeacherApp.exe') -Value "lehrer $marker"
    Set-Content -LiteralPath (Join-Path $dir 'TeacherApp\schulvorgaben.json') -Value '{"schemaVersion":1,"distributionRoot":"M:\\Allgemein"}'
    [System.IO.File]::WriteAllBytes((Join-Path $dir 'TeacherApp\lehrerprogramm.sperre'), [byte[]]@())
    Set-Content -LiteralPath (Join-Path $dir 'StudentApp\GsmKurswahl.StudentApp.exe') -Value "schueler $marker"
    Set-Content -LiteralPath (Join-Path $dir 'docs\README.md') -Value "doku $marker"
    Copy-Item -LiteralPath (Join-Path $repo 'scripts\release\Update-Kurswahl.ps1') -Destination (Join-Path $dir '_Update')
    Copy-Item -LiteralPath (Join-Path $repo 'scripts\release\Aktualisieren.cmd') -Destination $dir
    @{ schemaVersion = 1; version = $version; informationalVersion = "$version+test$marker" } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dir 'version.json')
    $hashes = Get-ChildItem -LiteralPath $dir -File -Recurse | ForEach-Object {
        [pscustomobject]@{ path = $_.FullName.Substring($dir.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } }
    ConvertTo-Json -InputObject @($hashes) | Set-Content -LiteralPath (Join-Path $dir 'SHA256.json')
    if ($Tamper) { Set-Content -LiteralPath (Join-Path $dir 'StudentApp\GsmKurswahl.StudentApp.exe') -Value 'manipuliert' }
    $zip = Join-Path $work "$name.zip"
    Compress-Archive -LiteralPath $dir -DestinationPath $zip
    return $zip
}
function Invoke-Update([string]$root, [string[]]$extra = @()) {
    # Like Aktualisieren.cmd: no module path inherited from PowerShell 7.
    $saved = $env:PSModulePath; $env:PSModulePath = $null
    try { & $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root '_Update\Update-Kurswahl.ps1') -Stammordner $root @extra *> (Join-Path $work 'letzter-lauf.txt') }
    finally { $env:PSModulePath = $saved }
    return $LASTEXITCODE
}
function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw "Update-Test fehlgeschlagen: $message" } ; Write-Output "OK: $message" }

try {
    New-Item -ItemType Directory -Path $work | Out-Null
    $root = Join-Path $work 'GSM-Kurswahl'
    # First installation: unpack 1.0.0 and rename the folder.
    $first = New-Package '1.0.0' 'A'
    Expand-Archive -LiteralPath $first -DestinationPath (Join-Path $work 'install')
    Move-Item -LiteralPath (Join-Path $work 'install\GSM-Kurswahl-1.0.0') -Destination $root
    Set-Content -LiteralPath (Join-Path $root 'TeacherApp\schulvorgaben.json') -Value '{"schemaVersion":1,"distributionRoot":"X:\\Eigen"}'
    New-Item -ItemType Directory -Path (Join-Path $root 'Updates') | Out-Null

    Copy-Item -LiteralPath (New-Package '1.0.1' 'B') -Destination (Join-Path $root 'Updates')
    # First update through the launcher a user double-clicks (pause answered by the pipe).
    cmd /c "echo.| `"$root\Aktualisieren.cmd`"" *> (Join-Path $work 'letzter-lauf.txt')
    Assert-True ((Get-Content (Join-Path $work 'letzter-lauf.txt') -Raw) -match 'Aktualisierung abgeschlossen') 'Update 1.0.0 -> 1.0.1 über Aktualisieren.cmd läuft durch'
    Assert-True ((Get-Content (Join-Path $root 'TeacherApp\GsmKurswahl.TeacherApp.exe')) -eq 'lehrer B') 'neues Lehrerprogramm eingesetzt'
    Assert-True ((Get-Content (Join-Path $root 'StudentApp\GsmKurswahl.StudentApp.exe')) -eq 'schueler B') 'neues Schülerprogramm eingesetzt'
    Assert-True ((Get-Content (Join-Path $root 'TeacherApp\schulvorgaben.json') -Raw) -match 'Eigen') 'eigene Schulvorgaben bleiben erhalten'
    Assert-True ((Get-Content (Join-Path $root 'version.json') -Raw | ConvertFrom-Json).version -eq '1.0.1') 'version.json zeigt 1.0.1'
    Assert-True (@(Get-ChildItem (Join-Path $root 'Sicherung') -Directory).Count -eq 1) 'vorheriger Stand gesichert'
    Assert-True (Test-Path (Join-Path $root 'Updates\eingespielt\GSM-Kurswahl-1.0.1.zip')) 'Paket nach eingespielt verschoben'

    # Running teacher program (exclusive handle on the lock file) blocks the update.
    Copy-Item -LiteralPath (New-Package '1.0.2' 'C') -Destination (Join-Path $root 'Updates')
    $lease = [System.IO.File]::Open((Join-Path $root 'TeacherApp\lehrerprogramm.sperre'), 'Open', 'Read', 'None')
    try { Assert-True ((Invoke-Update $root) -ne 0) 'laufendes Lehrerprogramm verhindert das Update' } finally { $lease.Dispose() }
    Assert-True ((Get-Content (Join-Path $root 'TeacherApp\GsmKurswahl.TeacherApp.exe')) -eq 'lehrer B') 'Stand nach Abbruch unverändert'
    Assert-True ((Invoke-Update $root) -eq 0) 'nach dem Schließen läuft das Update 1.0.2 durch'

    # Tampered package is rejected before anything changes.
    Copy-Item -LiteralPath (New-Package '1.0.3' 'D' -Tamper) -Destination (Join-Path $root 'Updates')
    Assert-True ((Invoke-Update $root) -ne 0) 'manipuliertes Paket wird abgewiesen'
    Assert-True ((Get-Content (Join-Path $root 'StudentApp\GsmKurswahl.StudentApp.exe')) -eq 'schueler C') 'Stand nach Prüfsummenfehler unverändert'
    Remove-Item -LiteralPath (Join-Path $root 'Updates\GSM-Kurswahl-1.0.3.zip')

    # Older package only with explicit permission.
    Copy-Item -LiteralPath (Join-Path $root 'Updates\eingespielt\GSM-Kurswahl-1.0.1.zip') -Destination (Join-Path $root 'Updates')
    Assert-True ((Invoke-Update $root) -ne 0) 'ältere Version wird ohne Freigabe abgewiesen'
    Assert-True ((Invoke-Update $root @('-ErlaubeAeltereVersion')) -eq 0) 'bewusste Rückkehr zu 1.0.1 möglich'
    Assert-True ((Get-Content (Join-Path $root 'TeacherApp\GsmKurswahl.TeacherApp.exe')) -eq 'lehrer B') 'Rückkehr hat 1.0.1 eingesetzt'

    foreach ($v in '1.0.4', '1.0.5') { Copy-Item -LiteralPath (New-Package $v $v) -Destination (Join-Path $root 'Updates'); Assert-True ((Invoke-Update $root) -eq 0) "Update auf $v" }
    Assert-True (@(Get-ChildItem (Join-Path $root 'Sicherung') -Directory).Count -eq 3) 'höchstens drei Sicherungen bleiben'
    Assert-True ((Get-Content (Join-Path $root 'Updates\update-protokoll.txt') -Raw) -match 'Fertig: Version 1\.0\.5') 'Protokoll geschrieben'
    Write-Output "Update-Test bestanden ($shell)."
}
catch { if (Test-Path (Join-Path $work 'letzter-lauf.txt')) { Get-Content (Join-Path $work 'letzter-lauf.txt') }; throw }
finally { if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force } }
