$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$shortcutPath = Join-Path $projectRoot 'Gengo.exe.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
$shortcut.Arguments = '-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + (Join-Path $PSScriptRoot 'launch.ps1') + '"'
$shortcut.WorkingDirectory = $projectRoot
$shortcut.IconLocation = (Join-Path $projectRoot 'artifacts/Gengo/Gengo.exe') + ',0'
$shortcut.Description = 'Open the latest Gengo build with your normal personal dictionary data.'
$shortcut.Save()
Write-Output "Shortcut: $shortcutPath"
