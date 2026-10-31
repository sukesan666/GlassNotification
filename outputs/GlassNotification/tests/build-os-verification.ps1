$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path (Split-Path $PSScriptRoot))
$build=Join-Path $workspace 'work/os-audio'
New-Item -ItemType Directory -Force $build | Out-Null
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$source=Join-Path $PSScriptRoot 'OsNotificationAudio.cpp'
$command=@"
@echo off
call "$vs\VC\Auxiliary\Build\vcvars64.bat" >nul
cl /nologo /std:c++17 /EHsc /MT /LD /O2 "$source" /Fo:"$build\Audio.obj" /Fe:"$build\GlassNotificationAudio.dll" /link ole32.lib uuid.lib
"@
$commandPath=Join-Path $build 'compile.cmd'
[IO.File]::WriteAllText($commandPath,$command,[Text.Encoding]::Default)
& $env:ComSpec /c $commandPath
if($LASTEXITCODE -ne 0){throw 'Audio probe compilation failed'}
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$refs=@('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll','WPF/WindowsBase.dll') | ForEach-Object {'/r:'+(Join-Path $framework $_)}
& (Join-Path $framework 'csc.exe') /nologo /utf8output /codepage:65001 /target:winexe /platform:x64 ('/out:'+(Join-Path $build 'OsVerification.exe')) $refs (Join-Path $PSScriptRoot 'OsVerificationWindow.cs')
if($LASTEXITCODE -ne 0){throw 'Audio fixture compilation failed'}
