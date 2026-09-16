param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = $PSScriptRoot }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','Microsoft.CSharp.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll')
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',('/out:' + (Join-Path $OutputDirectory 'Dayglance.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
$arguments += @((Join-Path $PSScriptRoot 'Core.cs'),(Join-Path $PSScriptRoot 'Dayglance.cs'),(Join-Path $PSScriptRoot 'Tests.cs'))
$arguments += @((Join-Path $PSScriptRoot 'Design.cs'),(Join-Path $PSScriptRoot 'Views.cs'))
$arguments += (Join-Path $PSScriptRoot 'Enhancements.cs')
$arguments += (Join-Path $PSScriptRoot 'ColorPicker.cs')
if (Test-Path (Join-Path $PSScriptRoot 'Dayglance.ico')) { $arguments += '/win32icon:' + (Join-Path $PSScriptRoot 'Dayglance.ico') }
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output 'Built Dayglance.exe'
