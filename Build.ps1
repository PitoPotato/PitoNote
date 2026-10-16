$ErrorActionPreference = 'Stop'
$releaseRoot = $PSScriptRoot
& (Join-Path $releaseRoot 'BuildNative.ps1')
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if(-not(Test-Path -LiteralPath $compiler)){throw 'Build requires Windows x64 with .NET Framework 4.x and WPF.'}
$references = @('System.dll', 'System.Core.dll', 'System.Runtime.Serialization.dll', 'System.Xml.dll', 'System.Xaml.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll')
$references += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { Join-Path (Join-Path $framework 'WPF') $_ }
$out = Join-Path $releaseRoot '@Resources\PitoNote.exe'
New-Item -ItemType Directory -Path (Split-Path -Parent $out) -Force | Out-Null
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $releaseRoot 'source') -Filter '*.cs' | Select-Object -ExpandProperty FullName
$argsList = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/debug-','/codepage:65001',('/out:' + $out))
$argsList += $references | ForEach-Object { '/reference:' + $_ }
$argsList += ('/resource:'+(Join-Path $releaseRoot 'source\NativeCatalog.lua')+',PitoNote.NativeCatalog.lua')
$argsList += ('/resource:'+(Join-Path $releaseRoot 'source\ControlList.lua')+',PitoNote.ControlList.lua')
$argsList += ('/resource:'+(Join-Path $releaseRoot '@Resources\Plugins\x64\PitoNoteSnap.dll')+',PitoNote.PitoNoteSnap.dll')
& $compiler @argsList @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$brandDirectory=Join-Path $releaseRoot '@Resources\Brand'
$brandBuild=Start-Process -FilePath $out -ArgumentList ('--export-brand "'+$brandDirectory+'"') -Wait -PassThru -WindowStyle Hidden
if($brandBuild.ExitCode -ne 0){throw 'Brand asset export failed.'}
& $compiler @argsList ('/win32icon:'+(Join-Path $brandDirectory 'PitoNote.ico')) @sourceFiles
if($LASTEXITCODE -ne 0){throw 'Branded build failed.'}
$publicBrand=Join-Path $releaseRoot 'assets\brand'
New-Item -ItemType Directory -Path $publicBrand -Force | Out-Null
foreach($name in @('PitoNote.svg','PitoNote-64.png','PitoNote-256.png','PitoNote.ico')){Copy-Item -LiteralPath (Join-Path $brandDirectory $name) -Destination $publicBrand -Force}
Write-Output ('Built: ' + $out)
