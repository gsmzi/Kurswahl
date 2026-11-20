<#
  GSM-Kurswahl: Programmaktualisierung im Stammordner.

  Ablauf: Das neue Programmpaket (GSM-Kurswahl-<Version>.zip) in den Ordner "Updates" des Stammordners legen
  und "Aktualisieren.cmd" starten. Das Skript
    1. wählt das ZIP mit der höchsten Version in "Updates" (ältere nur mit -ErlaubeAeltereVersion),
    2. entpackt es in einen Prüfordner und prüft alle Dateien gegen SHA256.json,
    3. bricht ab, solange das Lehrerprogramm läuft (Startsperre lehrerprogramm.sperre, auch an anderen Rechnern),
    4. verschiebt TeacherApp/StudentApp/docs in "Sicherung\<alte Version>_<Zeit>" und setzt die neuen Ordner ein,
    5. übernimmt eine vorhandene TeacherApp\schulvorgaben.json (Schulvorgaben bleiben erhalten),
    6. prüft die eingesetzten Dateien erneut, protokolliert in Updates\update-protokoll.txt und legt das ZIP
       unter Updates\eingespielt ab. Bei einem Fehler wird der vorherige Stand wiederhergestellt.
  Verwaltungsdaten (Datenbank, Arbeitsstand, Schülerordner) liegen nicht im Programmordner und bleiben unberührt.
  Kompatibel mit Windows PowerShell 5.1.
#>
param(
    [string]$Stammordner = (Split-Path $PSScriptRoot -Parent),
    [switch]$ErlaubeAeltereVersion,
    [int]$SicherungenBehalten = 3
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$Stammordner = [System.IO.Path]::GetFullPath($Stammordner.TrimEnd('\'))
$updates = Join-Path $Stammordner 'Updates'
$sicherung = Join-Path $Stammordner 'Sicherung'
$protokoll = Join-Path $updates 'update-protokoll.txt'
$ersetzt = @('TeacherApp', 'StudentApp', 'docs')
$einzeldateien = @('README.md', 'LIESMICH.txt', 'Aktualisieren.cmd', 'version.json', 'SHA256.json')

function Write-Log([string]$text) {
    $line = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '  ' + $text
    Write-Host $text
    if (Test-Path -LiteralPath $updates) { Add-Content -LiteralPath $protokoll -Value $line -Encoding UTF8 }
}

function Read-PackageVersion([string]$zipPath) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -match '^([^/\\]+[/\\])?version\.json$' } | Select-Object -First 1
        if (-not $entry) { return $null }
        $reader = New-Object System.IO.StreamReader($entry.Open(), [System.Text.Encoding]::UTF8)
        try { $json = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        return $json
    }
    finally { $zip.Dispose() }
}

# .NET hashing instead of Get-FileHash: works even if the module path of another PowerShell version is inherited.
function Get-Sha256([string]$path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($path)
    try { return ([System.BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
}

function Test-Hashes([string]$folder, [string[]]$ignore) {
    $listPath = Join-Path $folder 'SHA256.json'
    if (-not (Test-Path -LiteralPath $listPath)) { throw "SHA256.json fehlt in $folder." }
    $list = Get-Content -LiteralPath $listPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $fehler = @()
    foreach ($item in $list) {
        if ($ignore -contains $item.path) { continue }
        $file = Join-Path $folder $item.path
        if (-not (Test-Path -LiteralPath $file)) { $fehler += "fehlt: $($item.path)"; continue }
        if ((Get-Sha256 $file) -ne $item.sha256) { $fehler += "abweichend: $($item.path)" }
    }
    if ($fehler.Count -gt 0) { throw ("Prüfsummenfehler (" + $fehler.Count + "): " + (($fehler | Select-Object -First 5) -join '; ')) }
    return $list.Count
}

if (-not (Test-Path -LiteralPath $updates)) { New-Item -ItemType Directory -Path $updates | Out-Null }
Write-Log "=== Aktualisierung in $Stammordner ==="

# Installed version (none on a first installation).
$installiert = $null
$installedJson = Join-Path $Stammordner 'version.json'
if (Test-Path -LiteralPath $installedJson) { $installiert = Get-Content -LiteralPath $installedJson -Raw -Encoding UTF8 | ConvertFrom-Json }
$installiertText = if ($installiert) { $installiert.informationalVersion } else { '(keine)' }
Write-Log "Installiert: $installiertText"

# Choose the package with the highest version.
$kandidaten = @()
foreach ($zip in Get-ChildItem -LiteralPath $updates -Filter '*.zip' -File) {
    $v = Read-PackageVersion $zip.FullName
    if ($v -and $v.version) { $kandidaten += [pscustomobject]@{ Zip = $zip; Info = $v; Version = [version]$v.version } }
    else { Write-Log "Übersprungen (kein GSM-Kurswahl-Paket): $($zip.Name)" }
}
if ($kandidaten.Count -eq 0) { throw "Kein Programmpaket in $updates gefunden. Neues GSM-Kurswahl-*.zip dort ablegen." }
$paket = $kandidaten | Sort-Object Version -Descending | Select-Object -First 1
Write-Log "Paket: $($paket.Zip.Name) (Version $($paket.Info.informationalVersion))"
if ($installiert) {
    $alt = [version]$installiert.version
    if ($paket.Version -lt $alt -and -not $ErlaubeAeltereVersion) { throw "Das Paket ($($paket.Version)) ist älter als die installierte Version ($alt). Für eine bewusste Rückkehr: Aktualisieren.cmd -ErlaubeAeltereVersion" }
    if ($paket.Version -eq $alt -and $paket.Info.informationalVersion -eq $installiert.informationalVersion) { throw "Version $alt ist bereits installiert." }
}

# Extract and verify before anything in the root folder changes.
$pruefordner = Join-Path $updates ('_pruefung-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
[System.IO.Compression.ZipFile]::ExtractToDirectory($paket.Zip.FullName, $pruefordner)
$inhalt = $pruefordner
$oben = @(Get-ChildItem -LiteralPath $pruefordner)
if ($oben.Count -eq 1 -and $oben[0].PSIsContainer) { $inhalt = $oben[0].FullName }
foreach ($teil in @('TeacherApp', 'StudentApp')) { if (-not (Test-Path -LiteralPath (Join-Path $inhalt $teil))) { throw "Paket unvollständig: $teil fehlt." } }
$anzahl = Test-Hashes $inhalt @()
Write-Log "Paket geprüft: $anzahl Dateien, Prüfsummen in Ordnung."

# The teacher program holds lehrerprogramm.sperre exclusively while it runs (also from another computer).
$sperre = Join-Path $Stammordner 'TeacherApp\lehrerprogramm.sperre'
if (Test-Path -LiteralPath $sperre) {
    try { $probe = [System.IO.File]::Open($sperre, 'Open', 'Read', 'None'); $probe.Dispose() }
    catch { Remove-Item -LiteralPath $pruefordner -Recurse -Force; throw 'Das Lehrerprogramm ist noch geöffnet (auch an einem anderen Rechner möglich). Bitte schließen und Aktualisieren.cmd erneut starten.' }
}

# Swap with backup; on any error the previous state is moved back.
$altName = if ($installiert) { $installiert.version } else { 'ohne-version' }
$ziel = Join-Path $sicherung ($altName + '_' + (Get-Date -Format 'yyyy-MM-dd_HH-mm-ss-fff'))
New-Item -ItemType Directory -Path $ziel -Force | Out-Null
$verschoben = @()
$schulvorgaben = Join-Path $Stammordner 'TeacherApp\schulvorgaben.json'
$behalteVorgaben = Test-Path -LiteralPath $schulvorgaben
try {
    foreach ($teil in $ersetzt + $einzeldateien) {
        $quelle = Join-Path $Stammordner $teil
        if (Test-Path -LiteralPath $quelle) { Move-Item -LiteralPath $quelle -Destination (Join-Path $ziel $teil); $verschoben += $teil }
    }
    foreach ($teil in $ersetzt + $einzeldateien) {
        $neu = Join-Path $inhalt $teil
        if (Test-Path -LiteralPath $neu) { Move-Item -LiteralPath $neu -Destination (Join-Path $Stammordner $teil) }
    }
    $ignorieren = @()
    if ($behalteVorgaben) {
        Copy-Item -LiteralPath (Join-Path $ziel 'TeacherApp\schulvorgaben.json') -Destination $schulvorgaben -Force
        $ignorieren += 'TeacherApp\schulvorgaben.json'
        Write-Log 'Bisherige Schulvorgaben (TeacherApp\schulvorgaben.json) übernommen.'
    }
    # _Update itself: replace the script last (PowerShell has already read this file).
    $neuesSkript = Join-Path $inhalt '_Update\Update-Kurswahl.ps1'
    if (Test-Path -LiteralPath $neuesSkript) { Copy-Item -LiteralPath $neuesSkript -Destination (Join-Path $Stammordner '_Update\Update-Kurswahl.ps1') -Force }
    $ignorieren += '_Update\Update-Kurswahl.ps1'
    $geprueft = Test-Hashes $Stammordner $ignorieren
    Write-Log "Eingesetzt und geprüft: $geprueft Dateien."
}
catch {
    Write-Log "FEHLER: $($_.Exception.Message) – stelle vorherigen Stand wieder her."
    foreach ($teil in $ersetzt + $einzeldateien) {
        $eingesetzt = Join-Path $Stammordner $teil
        if (Test-Path -LiteralPath (Join-Path $ziel $teil)) {
            if (Test-Path -LiteralPath $eingesetzt) { Remove-Item -LiteralPath $eingesetzt -Recurse -Force }
            Move-Item -LiteralPath (Join-Path $ziel $teil) -Destination $eingesetzt
        }
    }
    Write-Log 'Vorheriger Stand wiederhergestellt.'
    throw
}
finally {
    if (Test-Path -LiteralPath $pruefordner) { Remove-Item -LiteralPath $pruefordner -Recurse -Force }
}

$eingespielt = Join-Path $updates 'eingespielt'
New-Item -ItemType Directory -Path $eingespielt -Force | Out-Null
Move-Item -LiteralPath $paket.Zip.FullName -Destination (Join-Path $eingespielt $paket.Zip.Name) -Force

# Keep only the newest backups; never touch anything outside the backup folder.
$alle = @(Get-ChildItem -LiteralPath $sicherung -Directory | Sort-Object CreationTime -Descending)
foreach ($ordner in ($alle | Select-Object -Skip $SicherungenBehalten)) {
    if ($ordner.FullName.StartsWith($sicherung + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $ordner.FullName -Recurse -Force
        Write-Log "Alte Sicherung entfernt: $($ordner.Name)"
    }
}
Write-Log "Fertig: Version $($paket.Info.informationalVersion) ist installiert. Vorheriger Stand liegt in $ziel."
