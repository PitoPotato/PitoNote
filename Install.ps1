[CmdletBinding()]
param([string]$BackupDirectory = (Join-Path $env:LOCALAPPDATA 'PitoNote\InstallBackups'))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Migration.ps1')
$rainmeter = Join-Path $env:ProgramFiles 'Rainmeter\Rainmeter.exe'
if (-not (Test-Path -LiteralPath $rainmeter)) { throw 'Rainmeter is not installed.' }
if ([version](Get-Item -LiteralPath $rainmeter).VersionInfo.FileVersion -lt [version]'5.0.0.5293') { throw 'PitoNote requires Rainmeter 5.0.0.5293 or later.' }
if (Get-Process -Name NotesStudio,PitoNote -ErrorAction SilentlyContinue) { throw 'Close PitoNote settings and let pending saves finish before installing.' }
$configFile = Join-Path $env:APPDATA 'Rainmeter\Rainmeter.ini'
if(-not (Test-Path -LiteralPath $configFile)){throw 'Start Rainmeter once before installing PitoNote.'}
$initialConfig = [IO.File]::ReadAllText($configFile)
$skinPathValue=[regex]::Match($initialConfig,'(?im)^SkinPath=(.+)\r?$').Groups[1].Value.Trim()
if([string]::IsNullOrWhiteSpace($skinPathValue)){throw 'Rainmeter SkinPath is missing. Open Rainmeter Manage and check its settings.'}
$skinsRoot = [IO.Path]::GetFullPath($skinPathValue)
$oldRoot = [IO.Path]::GetFullPath((Join-Path $skinsRoot 'NotesPlus-Studio'))
$targetRoot = [IO.Path]::GetFullPath((Join-Path $skinsRoot 'PitoNote'))
$fresh=$false
if (Test-Path -LiteralPath $oldRoot) {
    if (Test-Path -LiteralPath $targetRoot) { throw 'Both old and new skins exist. Refusing to overwrite either.' }
} elseif (Test-Path -LiteralPath $targetRoot) { $oldRoot = $targetRoot }
else {$fresh=$true}
if(-not $fresh-and-not(Test-Path -LiteralPath (Join-Path $oldRoot '@Resources\data.json'))){throw 'Existing skin has no data store. Refusing to overwrite it.'}
$sourceRoot=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
if($sourceRoot.Equals($targetRoot,[StringComparison]::OrdinalIgnoreCase)-or$sourceRoot.StartsWith($targetRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Run the installer from the extracted release folder, not from the installed skin.'}
$helper = Join-Path $PSScriptRoot '@Resources\PitoNote.exe'
if (-not (Test-Path -LiteralPath $helper)) { & (Join-Path $PSScriptRoot 'Build.ps1') }
$backupBase = [IO.Path]::GetFullPath($BackupDirectory)
$backup = [IO.Path]::GetFullPath((Join-Path $backupBase ('PitoNote-before-3.4.0-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
if (-not $backup.StartsWith($backupBase.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $backup)) { throw 'Unsafe or existing backup target.' }
foreach($path in @($oldRoot,$targetRoot)) { if ((Split-Path $path -Parent).TrimEnd('\') -ne $skinsRoot.TrimEnd('\')) { throw 'Skin target validation failed.' } }
if($backupBase.Equals($targetRoot,[StringComparison]::OrdinalIgnoreCase)-or$backupBase.StartsWith($targetRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'The backup directory must be outside the installed skin.'}
if($backupBase.Equals($sourceRoot,[StringComparison]::OrdinalIgnoreCase)-or$backupBase.StartsWith($sourceRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'The backup directory must be outside the extracted source/release folder.'}
New-Item -ItemType Directory -Path $backup | Out-Null
$wasRunning = @(Get-Process -Name Rainmeter -ErrorAction SilentlyContinue).Count -gt 0
$oldMoved=$false; $newCreated=$false; $configBackedUp=$false
try {
    if ($wasRunning) {
        & $rainmeter '!Quit'
        for($i=0;$i-lt80;$i++) { if (-not (Get-Process -Name Rainmeter -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 100 }
        if (Get-Process -Name Rainmeter -ErrorAction SilentlyContinue) { throw 'Rainmeter did not close; no skin files changed.' }
    }
    for($i=0;$i-lt50;$i++) { if (-not (Get-Process -Name NotesStudio,PitoNote -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 100 }
    if (Get-Process -Name NotesStudio,PitoNote -ErrorAction SilentlyContinue) { throw 'A note is still saving; no skin files changed.' }
    Copy-Item -LiteralPath $configFile -Destination (Join-Path $backup 'Rainmeter.ini')
    $configBackedUp=$true
    $config = [IO.File]::ReadAllText($configFile)
    if(-not $fresh){Copy-Item -LiteralPath $oldRoot -Destination (Join-Path $backup 'Before') -Recurse}
    $staging = Join-Path $backup 'Staging\PitoNote'
    $stagingResources = Join-Path $staging '@Resources'
    New-Item -ItemType Directory -Path $stagingResources -Force | Out-Null
    if(-not $fresh){foreach($item in (Get-ChildItem -LiteralPath (Join-Path $oldRoot '@Resources') -Force)) {
        if ($item.Name -in @('NotesStudio.exe','PitoNote.exe','NativeEdit.lua','Brand')) { continue }
        if ($item.Name -like 'PitoRich*') { throw 'Unexpected experimental rich-text module. No migration performed.' }
        Copy-Item -LiteralPath $item.FullName -Destination $stagingResources -Recurse
    }}
    foreach($item in @('source','Build.ps1','README.md','Install.ps1','Migration.ps1','LICENSE','NOTICE.md','CHANGELOG.md','docs','assets')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $item) -Destination $staging -Recurse }
    Copy-Item -LiteralPath $helper -Destination $stagingResources
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '@Resources\Brand') -Destination $stagingResources -Recurse
    $init=Start-Process -FilePath (Join-Path $stagingResources 'PitoNote.exe') -ArgumentList '--initialize' -PassThru -Wait -WindowStyle Hidden
    if ($init.ExitCode -ne 0) { throw 'Staging validation failed. Existing skin is untouched.' }
    $converted = Convert-PitoNoteRainmeterConfig -Text $config
    if($fresh){$converted=Enable-PitoNoteToolbar -Text $converted}
    [IO.File]::WriteAllText((Join-Path $backup 'Rainmeter-after-migration.ini'),$converted,[Text.Encoding]::Unicode)
    $retired=Join-Path $backup 'Retired'
    New-Item -ItemType Directory -Path $retired | Out-Null
    if(-not $fresh){
        $retiredOld=Join-Path $retired (Split-Path $oldRoot -Leaf)
        Move-Item -LiteralPath $oldRoot -Destination $retiredOld
        $oldMoved=$true
    }
    $newCreated=$true
    Copy-Item -LiteralPath $staging -Destination $targetRoot -Recurse
    [IO.File]::WriteAllText($configFile,$converted,[Text.Encoding]::Unicode)
    Start-Process -FilePath $rainmeter -WindowStyle Hidden
    Write-Output ('Installed: ' + $targetRoot)
    Write-Output ('Previous skin and configuration preserved: ' + $backup)
    Write-Output 'PitoNote is ready. Use + on the toolbar to create a note.'
} catch {
    $failure=$_
    if($newCreated -and (Test-Path -LiteralPath $targetRoot)) { Move-Item -LiteralPath $targetRoot -Destination (Join-Path $backup 'Failed-new-install') }
    if($oldMoved) { Move-Item -LiteralPath $retiredOld -Destination $oldRoot }
    if($configBackedUp) { Copy-Item -LiteralPath (Join-Path $backup 'Rainmeter.ini') -Destination $configFile -Force }
    if($wasRunning -and -not (Get-Process -Name Rainmeter -ErrorAction SilentlyContinue)) { Start-Process -FilePath $rainmeter -WindowStyle Hidden }
    throw $failure
}
