param([ValidateSet('None','Core','UI','All','Visual')][string]$Test = 'None')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$workspace = Split-Path (Split-Path $root)
# Sparse identity resolves visual assets from the external application directory.
New-Item -ItemType Directory -Force (Join-Path $root 'assets') | Out-Null
Get-ChildItem (Join-Path $root 'identity/assets') -Filter '*.png' | Copy-Item -Destination (Join-Path $root 'assets') -Force
Copy-Item -LiteralPath (Join-Path $root 'identity/assets/AppIcon.ico') -Destination (Join-Path $root 'assets/AppIcon.ico') -Force
$build = Join-Path $workspace 'work/build'
New-Item -ItemType Directory -Force -Path $build | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $build 'assets') | Out-Null
Get-ChildItem (Join-Path $root 'identity/assets') -Filter '*.png' | Copy-Item -Destination (Join-Path $build 'assets') -Force
Copy-Item -LiteralPath (Join-Path $root 'identity/assets/AppIcon.ico') -Destination (Join-Path $build 'assets/AppIcon.ico') -Force
& (Join-Path $root 'native/build-native.ps1') -Destinations @($root,$build)
& (Join-Path $root 'native/build-notifications.ps1') -Destinations @($root,$build)
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$refs = @('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$refs += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/r:' + (Join-Path $framework ('WPF/' + $_)) }
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' -ErrorAction SilentlyContinue | ForEach-Object FullName)
function Compile([string]$output,[string]$main,[string[]]$files,[bool]$gui,[string]$manifest=(Join-Path $root 'app.manifest')) {
    $target = if ($gui) { '/target:winexe' } else { '/target:exe' }
    $arguments = @('/nologo','/utf8output','/codepage:65001','/optimize+','/platform:x64',$target,('/out:'+$output),('/main:'+$main),('/win32manifest:'+$manifest)) + $refs + $files
    if ($gui) { $arguments += '/win32icon:' + (Join-Path $root 'identity/assets/AppIcon.ico') }
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    Copy-Item -LiteralPath (Join-Path $root 'App.config') -Destination ($output+'.config') -Force
}
if (Test-Path (Join-Path $root 'src/App.cs')) {
    Compile (Join-Path $root 'GlassNotification.exe') 'Glass.App' $sources $true
    $linkedManifest=Join-Path $build 'linked.manifest'
    $manifestText=[IO.File]::ReadAllText((Join-Path $root 'app.manifest')).Replace('</assembly>','<msix xmlns="urn:schemas-microsoft-com:msix.v1" publisher="CN=GlassNotification" packageName="GlassNotification.OSLink" applicationId="App" /></assembly>')
    [IO.File]::WriteAllText($linkedManifest,$manifestText)
    Compile (Join-Path $root 'GlassNotification.Linked.exe') 'Glass.App' $sources $true $linkedManifest
    # Package tests need freshly built executables, including on a clean clone.
    if ($Test -ne 'None') {
        & (Join-Path $root 'tests/test-native-icons.ps1')
        & (Join-Path $root 'tests/test-package-signing.ps1') -Root $root
        $files = $sources + @(Get-ChildItem (Join-Path $root 'tests') -Filter '*.cs' | ForEach-Object FullName)
        $testExe = Join-Path $build 'GlassTests.exe'
        Compile $testExe 'Glass.Tests' $files $false
        & $testExe $Test $root
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    }
    Write-Output 'BUILD OK: GlassNotification.exe'
    # A successful build includes a signed identity package for registration without developer mode.
    & (Join-Path $root 'identity/build-package.ps1') -Root $root
}
