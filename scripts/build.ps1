param([string]$DotnetPath = 'dotnet', [switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/packages'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    # Both normal builds and -Publish refresh the permanent launcher target.
    # Keep -Publish compatible with previous commands (skip the extra Debug build).
    $outputDirectory = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/Gengo'))
    $toolsDirectory = [IO.Path]::GetFullPath((Join-Path $projectRoot '.tools'))
    $workspacePrefix = [IO.Path]::GetFullPath($projectRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (!$outputDirectory.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase) -or !$toolsDirectory.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Build output and temporary paths must stay inside the project.' }
    $active = Get-Process Gengo -ErrorAction SilentlyContinue | Where-Object { $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq (Join-Path $outputDirectory 'Gengo.exe') }
    if ($active) { throw 'Close Gengo before building so the permanent application can be updated. Your current build has been preserved.' }
    if (!$Publish) {
        & $DotnetPath build src/Kotoba.App -p:UseSharedCompilation=false -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    }
    $staging = [IO.Path]::GetFullPath((Join-Path $toolsDirectory ('publish-' + [Guid]::NewGuid().ToString('N'))))
    $previous = [IO.Path]::GetFullPath((Join-Path $toolsDirectory ('previous-build-' + [Guid]::NewGuid().ToString('N'))))
    $toolsPrefix = $toolsDirectory.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (!$staging.StartsWith($toolsPrefix, [StringComparison]::OrdinalIgnoreCase) -or !$previous.StartsWith($toolsPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Temporary build paths must stay inside .tools.' }
    try {
        & $DotnetPath publish src/Kotoba.App -c Release -r win-x64 --self-contained true -p:UseSharedCompilation=false -m:1 -o $staging
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Your current build has been preserved.' }
        Copy-Item THIRD-PARTY-NOTICES.md $staging -Force
        Copy-Item LICENSES $staging -Recurse -Force
        Copy-Item README.md $staging -Force
        Copy-Item .docs $staging -Recurse -Force
        New-Item -ItemType Directory -Path (Split-Path -Parent $outputDirectory) -Force | Out-Null
        if (Test-Path -LiteralPath $outputDirectory) { Move-Item -LiteralPath $outputDirectory -Destination $previous }
        try { Move-Item -LiteralPath $staging -Destination $outputDirectory }
        catch {
            if (Test-Path -LiteralPath $previous) { Move-Item -LiteralPath $previous -Destination $outputDirectory }
            throw
        }
        & (Join-Path $PSScriptRoot 'create-shortcut.ps1')
        if (Test-Path -LiteralPath $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
        Write-Output "Ready: $outputDirectory/Gengo.exe"
    } finally {
        if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    }
} finally { Pop-Location }
