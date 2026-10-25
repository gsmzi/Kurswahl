param([switch]$LockedMode)
$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $restoreArgs = @('restore', 'GSM-Kurswahl.sln', '--configfile', 'NuGet.Config')
    if ($LockedMode) { $restoreArgs += '--locked-mode' }
    & dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw 'Restore fehlgeschlagen.' }
    dotnet build GSM-Kurswahl.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
    dotnet test GSM-Kurswahl.sln -c Release --no-build --no-restore --logger trx --results-directory artifacts/test-results/final
    if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen.' }
    & "$PSScriptRoot/Test-Schemas.ps1"
}
finally { Pop-Location }
