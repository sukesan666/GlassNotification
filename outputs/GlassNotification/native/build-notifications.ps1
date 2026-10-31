param([string[]]$Destinations)
$ErrorActionPreference='Stop'
$product=Split-Path $PSScriptRoot
$workspace=Split-Path (Split-Path $product)
$source=Join-Path $PSScriptRoot 'SystemNotifications.cpp'
$build=Join-Path $workspace ('work/notifications-native/'+(Get-FileHash $source).Hash.Substring(0,16)+'-'+(Get-FileHash $PSCommandPath).Hash.Substring(0,8))
New-Item -ItemType Directory -Force $build | Out-Null
$dll=Join-Path $build 'GlassNotifications.dll'
if(!(Test-Path $dll)){
 $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
 $vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
 if(!$vs){throw 'Visual Studio C++ tools are required.'}
 $generated=Join-Path $workspace 'work/native/generated'
 $command=@"
@echo off
call "$vs\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b %errorlevel%
cl /nologo /utf-8 /std:c++17 /EHsc /MT /LD /O2 /I "$generated" "$source" /Fo:"$build\SystemNotifications.obj" /Fe:"$dll" /link windowsapp.lib user32.lib
exit /b %errorlevel%
"@
 $commandPath=Join-Path $build 'compile.cmd'
 [IO.File]::WriteAllText($commandPath,$command,[Text.Encoding]::Default)
 & $env:ComSpec /c $commandPath
 if($LASTEXITCODE -ne 0){throw 'Notification listener build failed'}
}
foreach($destination in $Destinations){Copy-Item -LiteralPath $dll -Destination $destination -Force}
Write-Output 'NATIVE NOTIFICATIONS BUILD OK'
