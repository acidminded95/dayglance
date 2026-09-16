param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = $PSScriptRoot }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','Microsoft.CSharp.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll')
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',('/out:' + (Join-Path $OutputDirectory 'Dayglance.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
# Every C# file in this folder is part of the app.
$arguments += @(Get-ChildItem -Path $PSScriptRoot -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
if (Test-Path (Join-Path $PSScriptRoot 'Dayglance.ico')) { $arguments += '/win32icon:' + (Join-Path $PSScriptRoot 'Dayglance.ico') }
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
if ((Resolve-Path $OutputDirectory).Path.TrimEnd('\') -ne (Resolve-Path $PSScriptRoot).Path.TrimEnd('\')) { Copy-Item -Path (Join-Path $PSScriptRoot 'Dayglance.exe.config') -Destination $OutputDirectory -Force }
Write-Output 'Built Dayglance.exe'
