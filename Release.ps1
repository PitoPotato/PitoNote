[CmdletBinding()]
param([string]$OutputDirectory=(Join-Path $PSScriptRoot ('dist\3.5.0-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))))
$ErrorActionPreference='Stop'
$version='3.5.0'
$destination=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $destination){throw 'Packaging requires a new output directory; existing releases are never overwritten.'}
& (Join-Path $PSScriptRoot 'Build.ps1')
$app=Join-Path $PSScriptRoot '@Resources\PitoNote.exe'
if((Get-Item -LiteralPath $app).VersionInfo.FileVersion-ne($version+'.0')){throw 'Release version does not match the built helper.'}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$repo=Join-Path $destination 'repository\PitoNote'
$windows=Join-Path $destination 'windows\PitoNote'
New-Item -ItemType Directory -Path $repo,$windows -Force | Out-Null
# Allowlist: never enumerate an installed skin or copy the development root.
$files=@('Build.ps1','BuildNative.ps1','Install.ps1','Migration.ps1','Test.ps1','Release.ps1','README.md','LICENSE','NOTICE.md','CHANGELOG.md','CONTRIBUTING.md','SECURITY.md','.gitignore','.gitattributes')
$files+=Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -File | Where-Object {$_.Extension-in@('.cs','.lua')} | ForEach-Object {'source\'+$_.Name}
$files+=@('native\PitoNoteSnap.cpp','native\SnapGeometry.h','native\SnapGeometryChecks.cpp')
$files+=@('docs\INSTALLATION.md','docs\ARCHITECTURE.md','docs\PUBLISHING.md','.github\workflows\build.yml','.github\ISSUE_TEMPLATE\bug_report.yml','.github\ISSUE_TEMPLATE\feature_request.yml','.github\PULL_REQUEST_TEMPLATE.md')
$files+=@('assets\brand\PitoNote.svg','assets\brand\PitoNote-64.png','assets\brand\PitoNote-256.png','assets\brand\PitoNote.ico')
foreach($file in $files){
    $source=Join-Path $PSScriptRoot $file
    if(-not(Test-Path -LiteralPath $source -PathType Leaf)){throw ('Missing public file: '+$file)}
    foreach($root in @($repo,$windows)){
        $target=Join-Path $root $file
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
    }
}
$resources=Join-Path $windows '@Resources'
New-Item -ItemType Directory -Path $resources -Force | Out-Null
Copy-Item -LiteralPath $app -Destination $resources
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '@Resources\Brand') -Destination $resources -Recurse
$pluginResources=Join-Path $resources 'Plugins\x64'
New-Item -ItemType Directory -Path $pluginResources -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '@Resources\Plugins\x64\PitoNoteSnap.dll') -Destination $pluginResources
$allowed=@{}
foreach($relative in $files){$allowed[$relative.ToLowerInvariant()]=$true}
$manifest=[Collections.Generic.List[string]]::new()
$privatePattern='(?i)C:'+'\\Users\\|D:'+'\\Application\\|'+'One'+'Drive\\|'+'gh'+'p_[A-Za-z0-9]{20,}|'+'github'+'_pat_|-----'+'BEGIN .*PRIVATE '+'KEY-----'
foreach($root in @($repo,$windows)){
    foreach($file in Get-ChildItem -LiteralPath $root -Recurse -File -Force){
        $relative=$file.FullName.Substring($root.Length+1)
        $runtime=$root-eq$windows-and($relative-eq'@Resources\PitoNote.exe'-or$relative-eq'@Resources\Plugins\x64\PitoNoteSnap.dll'-or$relative.StartsWith('@Resources\Brand\'))
        if(-not$allowed.ContainsKey($relative.ToLowerInvariant())-and-not$runtime){throw ('Unapproved public file: '+$relative)}
        if($file.Extension-in@('.cs','.cpp','.h','.lua','.ps1','.md','.yml','.svg')-or$file.Name-eq'LICENSE'){
            $text=[IO.File]::ReadAllText($file.FullName)
            if($text-match $privatePattern){throw ('Private path or credential pattern in '+$relative)}
        }
        $prefix=if($root-eq$repo){'source/PitoNote/'}else{'windows/PitoNote/'}
        $manifest.Add((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$prefix+$relative.Replace('\','/'))
    }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sourceZip=Join-Path $destination ('PitoNote-'+$version+'-source.zip')
$windowsZip=Join-Path $destination ('PitoNote-'+$version+'-windows.zip')
[IO.Compression.ZipFile]::CreateFromDirectory((Split-Path $repo -Parent),$sourceZip)
[IO.Compression.ZipFile]::CreateFromDirectory((Split-Path $windows -Parent),$windowsZip)
[IO.File]::WriteAllLines((Join-Path $destination 'MANIFEST.sha256'),($manifest|Sort-Object),[Text.UTF8Encoding]::new($false))
$sums=foreach($file in @($sourceZip,$windowsZip,(Join-Path $destination 'MANIFEST.sha256'))){(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+(Split-Path $file -Leaf)}
[IO.File]::WriteAllLines((Join-Path $destination 'SHA256SUMS.txt'),$sums,[Text.UTF8Encoding]::new($false))
Write-Output 'PASS: allowlisted files only; no installed data, machine-specific paths or credential patterns.'
Write-Output ('Clean GitHub repository: '+$repo)
Write-Output ('Release artifacts: '+$destination)
