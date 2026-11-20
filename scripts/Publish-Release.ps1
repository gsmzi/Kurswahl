param(
    # Also publish the teacher program self-contained (otherwise it needs an installed .NET 10 runtime).
    [switch]$SelfContained,
    # Optional replacement for the checked-in GSM schulvorgaben.json next to the teacher program.
    [string]$Schulvorgaben = '',
    # Allows a package from a working tree with uncommitted changes (name and version then say so).
    [switch]$Entwicklungsstand
)
$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    $dirty = git status --porcelain --untracked-files=no
    if ($dirty -and -not $Entwicklungsstand) {
        throw 'Nicht committete Änderungen vorhanden. Erst committen (die Versionsnummer zählt Commits) oder bewusst -Entwicklungsstand angeben.'
    }
    $build = Join-Path $repo ('artifacts/releases/_bau-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $build | Out-Null
    foreach ($app in @('StudentApp', 'TeacherApp')) {
        # The pupil program is always self-contained: pupil computers must not need an installed .NET runtime.
        $selfContainedApp = $app -eq 'StudentApp' -or $SelfContained
        $destination = Join-Path $build $app
        $arguments = @('publish', "src/GsmKurswahl.$app", '-c', 'Release', '--self-contained', $selfContainedApp.ToString().ToLowerInvariant(), '-p:PublishTrimmed=false', '-p:RestoreLockedMode=true', '-p:RestoreConfigFile=NuGet.Config', '--artifacts-path', (Join-Path $repo 'artifacts/m5-publish-build'), '-o', $destination)
        if ($selfContainedApp) { $arguments += @('-r', 'win-x64', '-p:NuGetLockFilePath=packages.win-x64.lock.json', '-p:RuntimeFrameworkVersion=10.0.14') }
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw "Publish $app fehlgeschlagen. Unvollständiger Ordner $build bleibt zur Diagnose erhalten; kein ZIP erstellt." }
    }
    # The version is stamped by build/Version.targets; both programs must carry the same one.
    $teacherVersion = (Get-Item (Join-Path $build 'TeacherApp/GsmKurswahl.TeacherApp.dll')).VersionInfo.ProductVersion
    $studentVersion = (Get-Item (Join-Path $build 'StudentApp/GsmKurswahl.StudentApp.dll')).VersionInfo.ProductVersion
    if ($teacherVersion -ne $studentVersion) { throw "Versionen weichen ab: Lehrerprogramm $teacherVersion, Schülerprogramm $studentVersion." }
    $short = $teacherVersion.Split('+')[0]
    $name = 'GSM-Kurswahl-' + $short + $(if ($teacherVersion -like '*.geaendert') { '-entwicklung' } else { '' })
    $output = Join-Path $repo "artifacts/releases/$name"
    if (Test-Path -LiteralPath $output) { $output = $output + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') }
    Move-Item -LiteralPath $build -Destination $output

    if ($Schulvorgaben.Length -gt 0) { Copy-Item -LiteralPath $Schulvorgaben -Destination (Join-Path $output 'TeacherApp/schulvorgaben.json') -Force }
    Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination $output
    $docsDestination = Join-Path $output 'docs'
    New-Item -ItemType Directory -Path $docsDestination | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $repo 'docs') -Filter '*.md' -File | Copy-Item -Destination $docsDestination
    # Update mechanism: Aktualisieren.cmd in the root folder, the script beside it in _Update.
    New-Item -ItemType Directory -Path (Join-Path $output '_Update') | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo 'scripts/release/Update-Kurswahl.ps1') -Destination (Join-Path $output '_Update')
    Copy-Item -LiteralPath (Join-Path $repo 'scripts/release/Aktualisieren.cmd') -Destination $output
    Copy-Item -LiteralPath (Join-Path $repo 'scripts/release/LIESMICH.txt') -Destination $output
    $mode = if ($SelfContained) { 'Beide Programme Windows x64, selbstenthaltend.' } else { 'Schülerprogramm selbstenthaltend (Windows x64); Lehrerprogramm frameworkabhängig, benötigt installierte .NET-10-Laufzeit.' }
    $presets = if ($Schulvorgaben.Length -gt 0) { 'Schulvorgaben aus ' + $Schulvorgaben + ' in TeacherApp/schulvorgaben.json.' } else { 'GSM-Schulvorgaben in TeacherApp/schulvorgaben.json (Austeilordner und StudentApp).' }
    [ordered]@{
        schemaVersion = 1
        version = $short
        informationalVersion = $teacherVersion
        builtAt = (Get-Date).ToUniversalTime().ToString('o')
        teacherSelfContained = [bool]$SelfContained
        notes = @($mode, $presets)
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'version.json') -Encoding utf8
    # Record exact output contents. No administrative database or private workspace is packaged.
    $hashes = Get-ChildItem -LiteralPath $output -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ path = $_.FullName.Substring($output.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    ConvertTo-Json -InputObject @($hashes) -Depth 3 | Set-Content -LiteralPath (Join-Path $output 'SHA256.json') -Encoding utf8
    $zip = $output + '.zip'
    Compress-Archive -LiteralPath $output -DestinationPath $zip -CompressionLevel Optimal
    Get-FileHash -LiteralPath $zip -Algorithm SHA256
    Write-Output "Programmpaket $teacherVersion`: $zip"
}
finally { Pop-Location }
