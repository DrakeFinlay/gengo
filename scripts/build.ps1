param([string]$DotnetPath = 'dotnet', [switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/packages'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    if ($Publish) {
        & $DotnetPath publish src/Kotoba.App -c Release -r win-x64 --self-contained true -p:UseSharedCompilation=false -m:1 -o artifacts/Gengo
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
        Copy-Item THIRD-PARTY-NOTICES.md artifacts/Gengo -Force
        Copy-Item LICENSES artifacts/Gengo -Recurse -Force
        Copy-Item README.md artifacts/Gengo -Force
        Copy-Item .docs artifacts/Gengo -Recurse -Force
        Write-Output "Ready: $projectRoot/artifacts/Gengo/Gengo.exe"
    } else {
        & $DotnetPath build src/Kotoba.App -p:UseSharedCompilation=false -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    }
} finally { Pop-Location }
