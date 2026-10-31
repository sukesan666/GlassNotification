$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path (Split-Path $PSScriptRoot))
$build=Join-Path $workspace 'work/native-icon-tests'
New-Item -ItemType Directory -Force $build | Out-Null
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$generated=Join-Path $workspace 'work/native/generated'
$source=Join-Path $PSScriptRoot 'NativeAppIconTests.cpp'
$command=@"
@echo off
call "$vs\VC\Auxiliary\Build\vcvars64.bat" >nul
cl /nologo /utf-8 /std:c++17 /EHsc /MT /O2 /I "$generated" "$source" /Fo:"$build\NativeAppIconTests.obj" /Fe:"$build\NativeAppIconTests.exe" /link windowsapp.lib user32.lib
"@
$commandPath=Join-Path $build 'compile.cmd'
[IO.File]::WriteAllText($commandPath,$command,[Text.Encoding]::Default)
& $env:ComSpec /c $commandPath
if($LASTEXITCODE -ne 0){throw 'Native icon regression compilation failed'}
& (Join-Path $build 'NativeAppIconTests.exe')
if($LASTEXITCODE -ne 0){throw ('Native icon regression failed: '+$LASTEXITCODE)}
