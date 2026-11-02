param(
    # Also publish the teacher program self-contained (otherwise it needs an installed .NET 10 runtime).
    [switch]$SelfContained,
    # Optional replacement for the checked-in GSM schulvorgaben.json next to the teacher program.
    [string]$Schulvorgaben = ''
)
$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo ('artifacts/releases/M5-demo-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $output | Out-Null
Push-Location $repo
try {
    foreach ($app in @('StudentApp', 'TeacherApp')) {
        # The pupil program is always self-contained: pupil computers must not need an installed .NET runtime.
        $selfContainedApp = $app -eq 'StudentApp' -or $SelfContained
        $destination = Join-Path $output $app
        $arguments = @('publish', "src/GsmKurswahl.$app", '-c', 'Release', '--self-contained', $selfContainedApp.ToString().ToLowerInvariant(), '-p:PublishTrimmed=false', '-p:RestoreLockedMode=true', '-p:RestoreConfigFile=NuGet.Config', '--artifacts-path', (Join-Path $repo 'artifacts/m5-publish-build'), '-o', $destination)
        if ($selfContainedApp) { $arguments += @('-r', 'win-x64', '-p:NuGetLockFilePath=packages.win-x64.lock.json', '-p:RuntimeFrameworkVersion=10.0.14') }
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw "Publish $app fehlgeschlagen. Unvollständiger Ordner bleibt zur Diagnose erhalten; kein ZIP erstellt." }
    }
    if ($Schulvorgaben.Length -gt 0) { Copy-Item -LiteralPath $Schulvorgaben -Destination (Join-Path $output 'TeacherApp/schulvorgaben.json') -Force }
    Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination $output
    $docsDestination = Join-Path $output 'docs'
    New-Item -ItemType Directory -Path $docsDestination | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $repo 'docs') -Filter '*.md' -File | Copy-Item -Destination $docsDestination
    $mode = if ($SelfContained) { 'Beide Programme Windows x64, selbstenthaltend.' } else { 'Schülerprogramm selbstenthaltend (Windows x64); Lehrerprogramm frameworkabhängig, benötigt installierte .NET-10-Laufzeit.' }
    $presets = if ($Schulvorgaben.Length -gt 0) { 'Schulvorgaben aus ' + $Schulvorgaben + ' in TeacherApp/schulvorgaben.json.' } else { 'GSM-Schulvorgaben in TeacherApp/schulvorgaben.json (Austeilordner und StudentApp).' }
    @("M5-Erprobungspaket – ausschließlich synthetische Daten.", $mode, $presets, 'Keine Produktiv-/Rechtsfreigabe. Gesamten jeweiligen App-Ordner erhalten.', 'Keine automatischen Updates oder Deployments. Bedienung: docs/M5_ABSCHLUSS.md.') | Set-Content -LiteralPath (Join-Path $output 'PAKET-HINWEISE.txt') -Encoding utf8
    # Record exact output contents. No administrative database or private workspace is packaged.
    $hashes = Get-ChildItem -LiteralPath $output -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ path = $_.FullName.Substring($output.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    ConvertTo-Json -InputObject @($hashes) -Depth 3 | Set-Content -LiteralPath (Join-Path $output 'SHA256.json') -Encoding utf8
    Compress-Archive -LiteralPath $output -DestinationPath ($output + '.zip') -CompressionLevel Optimal
    Get-FileHash -LiteralPath ($output + '.zip') -Algorithm SHA256
    Write-Output "Erprobungspaket: $output.zip"
}
finally { Pop-Location }
