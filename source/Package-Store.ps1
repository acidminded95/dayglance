# Builds the Microsoft Store package: dist\store\Dayglance-<version>.0.msix (unsigned; the Store signs it).
# Needs the Windows 10/11 SDK (makeappx.exe and makepri.exe). Pass -Revision only for a rebuild of the same app version.
param([string]$OutputDirectory,[int]$Revision=0)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $root 'dist\store' }
$version = (Select-String -Path (Join-Path $PSScriptRoot 'Version.cs') -Pattern 'Version="([0-9.]+)"').Matches[0].Groups[1].Value
# The Store needs four parts and reserves the last one, so 2.1.1 becomes 2.1.1.0.
$packageVersion = "$version.$Revision"
if ($packageVersion.Split('.').Count -ne 4) { throw "Version.cs must hold three parts (found $version)." }
$sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\makeappx.exe" -ErrorAction SilentlyContinue | Sort-Object { [version]$_.Directory.Parent.Name } | Select-Object -Last 1
if (-not $sdk) { throw 'makeappx.exe not found. Install the Windows SDK (it also brings the App Certification Kit).' }
$makeappx = $sdk.FullName; $makepri = Join-Path $sdk.DirectoryName 'makepri.exe'
$stage = Join-Path $OutputDirectory "Dayglance-$packageVersion"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
& (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $stage -Store | Out-Null
Copy-Item (Join-Path $root 'LICENSE.txt') $stage
Copy-Item (Join-Path $root 'store\Assets') (Join-Path $stage 'Assets') -Recurse
$manifest = (Get-Content (Join-Path $root 'store\AppxManifest.xml') -Raw).Replace('$(Version)',$packageVersion)
Set-Content -Path (Join-Path $stage 'AppxManifest.xml') -Value $manifest -Encoding UTF8
# resources.pri maps Assets\Square150x150Logo.png to the scale-* and targetsize-* files.
$priConfig = Join-Path $OutputDirectory 'priconfig.xml'
& $makepri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri createconfig failed.' }
& $makepri new /pr $stage /cf $priConfig /mn (Join-Path $stage 'AppxManifest.xml') /of (Join-Path $stage 'resources.pri') /o
if ($LASTEXITCODE -ne 0) { throw 'makepri new failed.' }
$msix = Join-Path $OutputDirectory "Dayglance-$packageVersion.msix"
& $makeappx pack /d $stage /p $msix /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx pack failed.' }
Write-Output $msix
