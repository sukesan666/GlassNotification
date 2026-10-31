param([string[]]$Destinations)
$ErrorActionPreference='Stop'
$product=Split-Path $PSScriptRoot
$workspace=Split-Path (Split-Path $product)
$cache=Join-Path $workspace 'work/dependencies/windowsappsdk'
$nativeRoot=Join-Path $workspace 'work/native'
$source=Join-Path $PSScriptRoot 'GlassBackdrop.cpp'
$build=Join-Path $nativeRoot (((Get-FileHash $source).Hash.Substring(0,12))+'-'+((Get-FileHash $PSCommandPath).Hash.Substring(0,12)))
New-Item -ItemType Directory -Force $cache,$build | Out-Null
$packages=@(
 @('foundation','microsoft.windowsappsdk.foundation','1.8.260803002','B9232041AFD605B606C6F78F442D92EAD0076453F1F2A3260D2B7F8089BCAB0E'),
 @('interactive','microsoft.windowsappsdk.interactiveexperiences','1.8.260708001','496EEA92D353B5D3601B67353F06DCADD6D2D9B635575ACEBE6E42587DBFAD76'),
 @('cppwinrt','microsoft.windows.cppwinrt','3.0.260818.1','A8993608AE9263A8288E1A1D9EE2684A5632F4ACC59E1E89DDD86B3B889A7367')
)
foreach($p in $packages){
 $archive=Join-Path $cache ($p[0]+'.zip');$folder=Join-Path $cache $p[0]
 if(!(Test-Path $archive)){
  $probeArchive=Join-Path $workspace ('work/sdk-probe/packages/'+$p[0]+'.zip')
  if(Test-Path $probeArchive){Copy-Item -LiteralPath $probeArchive -Destination $archive}
  else {Invoke-WebRequest -UseBasicParsing ('https://api.nuget.org/v3-flatcontainer/'+$p[1]+'/'+$p[2]+'/'+$p[1]+'.'+$p[2]+'.nupkg') -OutFile $archive}
 }
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $p[3]){throw ('SDK package hash mismatch: '+$p[1])}
 if(!(Test-Path $folder)){Expand-Archive -LiteralPath $archive -DestinationPath $folder}
}
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'Visual Studio C++ desktop tools are required.'}
$sdkRoot=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
$sdkVersion=Get-ChildItem (Join-Path $sdkRoot 'UnionMetadata') -Directory | Where-Object {$_.Name -match '^10\.\d+\.\d+\.\d+$' -and (Test-Path (Join-Path $_.FullName 'Windows.winmd'))} | Sort-Object {[version]$_.Name} -Descending | Select-Object -First 1 -ExpandProperty Name
if(!$sdkVersion){throw 'Windows SDK metadata is required.'}
$generated=Join-Path $nativeRoot 'generated'
if(!(Test-Path (Join-Path $generated 'winrt/Microsoft.UI.Composition.SystemBackdrops.h'))){
 & (Join-Path $cache 'cppwinrt/bin/cppwinrt.exe') -in (Join-Path $cache 'interactive/metadata/10.0.18362.0') -in (Join-Path $sdkRoot ('UnionMetadata/'+$sdkVersion+'/Windows.winmd')) -out $generated
 if($LASTEXITCODE -ne 0){throw 'C++/WinRT projection generation failed'}
}
$dll=Join-Path $build 'GlassBackdrop.dll'
if(!(Test-Path $dll)){
 $compileRoot=Join-Path $build ('attempt-'+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory -Force $compileRoot | Out-Null
 $commandFile=Join-Path $compileRoot 'compile.cmd'
 $command=@"
@echo off
call "$vs\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b %errorlevel%
cl /nologo /std:c++17 /EHsc /MT /LD /O2 /I "$generated" /I "$cache\foundation\include" "$source" /Fo:"$compileRoot\GlassBackdrop.obj" /Fe:"$compileRoot\GlassBackdrop.dll" /link /IMPLIB:"$compileRoot\GlassBackdrop.lib" /LIBPATH:"$cache\foundation\lib\native\x64" Microsoft.WindowsAppRuntime.lib windowsapp.lib CoreMessaging.lib dwmapi.lib d3d11.lib d2d1.lib user32.lib
exit /b %errorlevel%
"@
 [IO.File]::WriteAllText($commandFile,$command,[Text.Encoding]::Default)
 & $env:ComSpec /c $commandFile
 if($LASTEXITCODE -ne 0){throw 'Native backdrop compilation failed'}
 Copy-Item -LiteralPath (Join-Path $compileRoot 'GlassBackdrop.dll') -Destination $dll
}
foreach($destination in $Destinations){
 New-Item -ItemType Directory -Force $destination,(Join-Path $destination 'licenses') | Out-Null
 Copy-Item -LiteralPath $dll -Destination $destination -Force
 foreach($package in @('interactive','foundation')){
  Get-ChildItem (Join-Path $cache ($package+'/runtimes-framework/win-x64/native')) -File | Where-Object Extension -in '.dll','.pri' | Copy-Item -Destination $destination -Force
  Copy-Item -LiteralPath (Join-Path $cache ($package+'/license.txt')) -Destination (Join-Path $destination ('licenses/WindowsAppSDK-'+$package+'.txt')) -Force
 }
}
Write-Output 'NATIVE SDK BUILD OK'
