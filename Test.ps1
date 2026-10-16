[CmdletBinding()]
param([switch]$IncludeNative,[string]$OutputDirectory=(Join-Path ([IO.Path]::GetTempPath()) ('PitoNote-tests-'+[Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
$testRoot=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $testRoot){throw 'Tests require a new output directory; existing files will not be overwritten.'}
$app=Join-Path $PSScriptRoot '@Resources\PitoNote.exe'
if(-not(Test-Path -LiteralPath $app)){throw 'Run Build.ps1 first.'}
New-Item -ItemType Directory -Path $testRoot | Out-Null
& (Join-Path $PSScriptRoot '@Resources\tests\SnapGeometryChecks.exe')
if($LASTEXITCODE-ne0){throw 'Snap geometry checks failed.'}
$suites=@(@('--selftest','data','checks.txt'),@('--verify-settings','settings','popup-checks.txt'),@('--verify-delete','delete','delete-checks.txt'),@('--verify-management','management','management-checks.txt'),@('--verify-upgrade','upgrade','upgrade-checks.txt'))
if($IncludeNative){$suites+=,@('--verify-native-catalog','native\Skins\PitoNoteProbe','native-catalog-checks.txt');$suites+=,@('--verify-native-snap','snap\Skins\PitoNoteProbe','native-snap-checks.txt')}
foreach($suite in $suites){
    $path=Join-Path $testRoot $suite[1]
    $process=Start-Process -FilePath $app -ArgumentList ($suite[0]+' "'+$path+'"') -PassThru -WindowStyle Hidden
    if(-not $process.WaitForExit(60000)){throw ('Test did not finish. Inspect process '+$process.Id+' and '+$path)}
    $report=Join-Path $path $suite[2]
    if($process.ExitCode-ne0){if(Test-Path -LiteralPath $report){Get-Content -LiteralPath $report};throw ('Test failed: '+$suite[0])}
    Get-Content -LiteralPath $report | Select-Object -Last 1
}
$stage=Join-Path $testRoot 'fresh\PitoNote'
$process=Start-Process -FilePath $app -ArgumentList ('--stage-release "'+$stage+'"') -PassThru -Wait -WindowStyle Hidden
if($process.ExitCode-ne0){throw 'Clean staging failed.'}
$empty=Get-Content -LiteralPath (Join-Path $stage '@Resources\data.json') -Raw | ConvertFrom-Json
if(@($empty.Notes).Count-ne0-or$empty.Settings.FontName-ne'Segoe UI'){throw 'Fresh stage is not clean.'}
foreach($relative in @('Control\Control.ini','Catalog\Catalog.ini','@Resources\NativeCatalog.lua','@Resources\NativeEdit.lua','@Resources\Plugins\x64\PitoNoteSnap.dll')){if(-not(Test-Path -LiteralPath (Join-Path $stage $relative))){throw ('Missing fresh-install asset: '+$relative)}}
if(-not$empty.Settings.SmartSnap-or$empty.Settings.SnapDistance-ne10-or$empty.Settings.SnapGap-ne12){throw 'Fresh snapping defaults are incorrect.'}
. (Join-Path $PSScriptRoot 'Migration.ps1')
$ini="[Rainmeter]`r`nSkinPath=C:\Example\Skins\`r`n[OtherSkin\Main]`r`nActive=1`r`nWindowX=99`r`n[Notes-pp\Notes-pp]`r`nActive=1`r`n"
$fresh=Enable-PitoNoteToolbar -Text $ini
if(-not $fresh.StartsWith($ini.TrimEnd())-or$fresh-notmatch '\[PitoNote\\Control\]\r?\nActive=1'){throw 'Fresh installer modified unrelated skins.'}
$again=Enable-PitoNoteToolbar -Text $fresh
if($again-cne$fresh){throw 'Toolbar registration is not idempotent.'}
$migrated=Convert-PitoNoteRainmeterConfig -Text ($ini+"[NotesPlus-Studio\Notes\test]`r`nActive=0`r`nWindowX=11`r`n")
if($migrated-notmatch '\[PitoNote\\Notes\\test\]'-or$migrated-notmatch '\[Notes-pp\\Notes-pp\]'){throw 'Legacy migration did not preserve unrelated skins.'}
$runtime=Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -File | Where-Object {$_.Name-notmatch 'Checks\.cs$'}
foreach($file in $runtime){
    $lines=Get-Content -LiteralPath $file.FullName -Encoding UTF8
    foreach($line in $lines){
        if($line-cmatch "SetText\('TR\u00CD TU\u1EC6'\)"){continue} # Intentional Unicode search fixture, not UI copy.
        if($line-cmatch '[\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u024F\u1E00-\u1EFF]'){throw ('Non-English UI text remains in '+$file.Name)}
    }
}
Write-Output 'PASS: clean first-install assets, toolbar registration, migration isolation and English UI audit.'
Write-Output ('Reports: '+$testRoot)
