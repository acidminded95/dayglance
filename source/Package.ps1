# Builds a release folder and ZIP: dist\Dayglance-<version>\ and dist\Dayglance-<version>.zip
# The ZIP includes dayglance-files.txt, which the in-app updater uses to replace exactly these files next time.
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $root 'dist' }
$version = (Select-String -Path (Join-Path $PSScriptRoot 'Version.cs') -Pattern 'Version="([0-9.]+)"').Matches[0].Groups[1].Value
$stage = Join-Path $OutputDirectory "Dayglance-$version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
& (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $stage | Out-Null
foreach ($file in 'README.md','LICENSE.txt','Schedule-format.md') { Copy-Item (Join-Path $root $file) $stage }
$names = @(Get-ChildItem $stage -File | ForEach-Object { $_.Name }) + 'dayglance-files.txt'
Set-Content -Path (Join-Path $stage 'dayglance-files.txt') -Value $names -Encoding UTF8
$zip = Join-Path $OutputDirectory "Dayglance-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Write-Output $zip
