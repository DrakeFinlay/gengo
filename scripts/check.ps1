param([string]$DotnetPath = 'dotnet', [string]$ReferenceDirectory = '', [string]$ParityBaseline = '', [switch]$Ui)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/packages'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    & $DotnetPath build tests/Kotoba.Checks -p:UseSharedCompilation=false -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Checks build failed.' }
    & $DotnetPath tests/Kotoba.Checks/bin/Debug/net10.0/Kotoba.Checks.dll $ReferenceDirectory $ParityBaseline
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
    if ($Ui) {
        if (!$ReferenceDirectory) { throw 'UI checks require an isolated .tools/ reference fixture directory.' }
        & $DotnetPath build tests/Kotoba.UiChecks -p:UseSharedCompilation=false -m:1
        if ($LASTEXITCODE -ne 0) { throw 'UI checks build failed.' }
        & $DotnetPath tests/Kotoba.UiChecks/bin/Debug/net10.0-windows/Kotoba.UiChecks.dll $ReferenceDirectory
        if ($LASTEXITCODE -ne 0) { throw 'UI checks failed.' }
    }
} finally { Pop-Location }
