param([string]$DataDirectory = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'artifacts/Gengo/Gengo.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Build the self-contained app first: scripts/build.ps1 -Publish' }
$info = New-Object System.Diagnostics.ProcessStartInfo
$info.FileName = $exe
$info.WorkingDirectory = Split-Path -Parent $exe
$info.UseShellExecute = $false
if ($DataDirectory) { $info.EnvironmentVariables['KOTOBA_DATA_DIR'] = [System.IO.Path]::GetFullPath($DataDirectory) }
else { $info.EnvironmentVariables.Remove('KOTOBA_DATA_DIR') }
[System.Diagnostics.Process]::Start($info) | Out-Null
