$ErrorActionPreference='Stop'
$native=Join-Path $PSScriptRoot 'native'
$build=Join-Path $PSScriptRoot '@Resources\build'
$output=Join-Path $PSScriptRoot '@Resources\Plugins\x64\PitoNoteSnap.dll'
$tests=Join-Path $PSScriptRoot '@Resources\tests\SnapGeometryChecks.exe'
foreach($path in @($build,(Split-Path $output -Parent),(Split-Path $tests -Parent))){New-Item -ItemType Directory -Path $path -Force|Out-Null}
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation=if(Test-Path -LiteralPath $vswhere){& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath}
if($installation){
    $tools=Get-ChildItem -LiteralPath (Join-Path $installation 'VC\Tools\MSVC') -Directory|Sort-Object Name -Descending|Select-Object -First 1
    $compiler=Join-Path $tools.FullName 'bin\Hostx64\x64\cl.exe'
    $kits=(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -ErrorAction SilentlyContinue).KitsRoot10
    if(-not$kits){$kits=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'}
    $sdk=Get-ChildItem -LiteralPath (Join-Path $kits 'Include') -Directory|Where-Object {Test-Path -LiteralPath (Join-Path $_.FullName 'um\Windows.h')}|Sort-Object Name -Descending|Select-Object -First 1
    if(-not$sdk){throw 'The Windows SDK is required to build the native snapping module.'}
    $common=@('/nologo','/O2','/MT','/std:c++17','/utf-8','/EHsc','/DUNICODE','/D_UNICODE',('/I'+(Join-Path $tools.FullName 'include')))
    foreach($part in @('ucrt','shared','um')){$common+=('/I'+(Join-Path $sdk.FullName $part))}
    $libs=@(('/LIBPATH:'+(Join-Path $tools.FullName 'lib\x64')),('/LIBPATH:'+(Join-Path $kits ('Lib\'+$sdk.Name+'\ucrt\x64'))),('/LIBPATH:'+(Join-Path $kits ('Lib\'+$sdk.Name+'\um\x64'))))
    & $compiler @common /LD ('/Fo'+(Join-Path $build 'PitoNoteSnap.obj')) (Join-Path $native 'PitoNoteSnap.cpp') user32.lib comctl32.lib /link @libs ('/OUT:'+$output) ('/IMPLIB:'+(Join-Path $build 'PitoNoteSnap.lib'))
    if($LASTEXITCODE-ne0){throw 'Native snap module build failed.'}
    & $compiler @common ('/Fo'+(Join-Path $build 'SnapGeometryChecks.obj')) ('/Fe'+$tests) (Join-Path $native 'SnapGeometryChecks.cpp') /link @libs
}else{
    $gpp=Get-Command g++.exe -ErrorAction SilentlyContinue
    if(-not$gpp){throw 'Native build requires MSVC C++ Build Tools with Windows SDK, or an x64 MinGW-w64 g++ compiler.'}
    $common=@('-std=c++17','-O2','-s','-static','-static-libgcc','-static-libstdc++','-DUNICODE','-D_UNICODE')
    & $gpp.Source @common -shared (Join-Path $native 'PitoNoteSnap.cpp') -lcomctl32 -luser32 -o $output
    if($LASTEXITCODE-ne0){throw 'Native snap module build failed.'}
    & $gpp.Source @common (Join-Path $native 'SnapGeometryChecks.cpp') -o $tests
}
if($LASTEXITCODE-ne0){throw 'Native geometry test build failed.'}
Write-Output ('Built native snapping: '+$output)
