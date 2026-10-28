param([switch]$SelfContained)
$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo ('artifacts/releases/M5-demo-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $output | Out-Null
Push-Location $repo
try {
    foreach ($app in @('StudentApp', 'TeacherApp')) {
        $destination = Join-Path $output $app
        $arguments = @('publish', "src/GsmKurswahl.$app", '-c', 'Release', '--self-contained', $SelfContained.ToString().ToLowerInvariant(), '-p:PublishTrimmed=false', '-p:RestoreLockedMode=true', '-p:RestoreConfigFile=NuGet.Config', '--artifacts-path', (Join-Path $repo 'artifacts/m5-publish-build'), '-o', $destination)
        if ($SelfContained) { $arguments += @('-r', 'win-x64', '-p:NuGetLockFilePath=packages.win-x64.lock.json', '-p:RuntimeFrameworkVersion=10.0.14') }
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw "Publish $app fehlgeschlagen. Unvollständiger Ordner bleibt zur Diagnose erhalten; kein ZIP erstellt." }
    }
    Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination $output
    $docsDestination = Join-Path $output 'docs'
    New-Item -ItemType Directory -Path $docsDestination | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $repo 'docs') -Filter '*.md' -File | Copy-Item -Destination $docsDestination
    $mode = if ($SelfContained) { 'Windows x64, selbstenthaltend. Test ohne SDK im Schulnetz bleibt erforderlich.' } else { 'Frameworkabhängig: benötigt installierte .NET-10-Laufzeit; nicht standalone.' }
    @("M5-Erprobungspaket – ausschließlich synthetische Daten.", $mode, 'Keine Produktiv-/Rechtsfreigabe. Gesamten jeweiligen App-Ordner erhalten.', 'Keine automatischen Updates oder Deployments. Bedienung: docs/M5_ABSCHLUSS.md.') | Set-Content -LiteralPath (Join-Path $output 'PAKET-HINWEISE.txt') -Encoding utf8
    # Record exact output contents. No administrative database or private workspace is packaged.
    $hashes = Get-ChildItem -LiteralPath $output -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ path = [IO.Path]::GetRelativePath($output, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    ConvertTo-Json -InputObject @($hashes) -Depth 3 | Set-Content -LiteralPath (Join-Path $output 'SHA256.json') -Encoding utf8
    Compress-Archive -LiteralPath $output -DestinationPath ($output + '.zip') -CompressionLevel Optimal
    Get-FileHash -LiteralPath ($output + '.zip') -Algorithm SHA256
    Write-Output "Erprobungspaket: $output.zip"
}
finally { Pop-Location }
