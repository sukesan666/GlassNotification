param([string]$Root=(Split-Path $PSScriptRoot))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
if(!('System.Security.Cryptography.Pkcs.SignedCms' -as [type])){Add-Type -AssemblyName System.Security}
$workspace=Split-Path (Split-Path $Root)
$fixture=Join-Path $workspace ('work/package-tests/'+[Guid]::NewGuid().ToString('N')+'/outputs/GlassNotification')
New-Item -ItemType Directory -Force $fixture | Out-Null
foreach($name in @('GlassNotification.Linked.exe','GlassNotification.Linked.exe.config')){Copy-Item -LiteralPath (Join-Path $Root $name) -Destination $fixture}
Get-ChildItem -LiteralPath $Root -File | Where-Object {$_.Extension -in @('.dll','.pri')} | Copy-Item -Destination $fixture
Copy-Item -LiteralPath (Join-Path $Root 'assets') -Destination $fixture -Recurse
[IO.File]::WriteAllText((Join-Path $fixture 'settings.json'),'{"private":"must not be packaged"}')
[IO.File]::WriteAllText((Join-Path $fixture 'private.pfx'),'not a real key; must not be packaged')
$builder=Join-Path $Root 'identity/build-package.ps1'
$shell=(Get-Process -Id $PID).Path
# Separate processes must reload the same protected identity, with no dependency on a user-profile key store.
& $shell -NoProfile -File $builder -Root $fixture -UseLocalSigningKey
if($LASTEXITCODE -ne 0){throw 'Initial package signing failed'}
$package=Join-Path $fixture 'GlassNotification.OSLink.msix'
$zip=[IO.Compression.ZipFile]::OpenRead($package)
try{
    $exe=$zip.GetEntry('GlassNotification.Linked.exe')
    if(!$exe){throw 'MSIX must contain the current application executable, not only its identity'}
    foreach($name in @('GlassNotification.Linked.exe.config','GlassNotifications.dll','GlassBackdrop.dll','assets/chime.wav','assets/AppIcon.ico')){
        if(!$zip.GetEntry($name)){throw ('Missing packaged runtime file: '+$name)}
    }
    $stream=$exe.Open();$hash=[Security.Cryptography.SHA256]::Create()
    try{$embeddedHash=([BitConverter]::ToString($hash.ComputeHash($stream))).Replace('-','')}finally{$stream.Dispose();$hash.Dispose()}
    if($embeddedHash -ne (Get-FileHash -LiteralPath (Join-Path $fixture 'GlassNotification.Linked.exe')).Hash){throw 'Packaged executable is not the current build'}
    if(@($zip.Entries | Where-Object {$_.FullName -match '(^|/)settings\.json$|\.(pfx|p12|key|dpapi)$'}).Count){throw 'Personal settings and signing keys must not be included in MSIX'}
    $reader=[IO.StreamReader]::new($zip.GetEntry('AppxManifest.xml').Open())
    try{[xml]$manifest=$reader.ReadToEnd()}finally{$reader.Dispose()}
    if($manifest.SelectSingleNode('//*[local-name()="AllowExternalContent"]')){throw 'Standalone MSIX must not require an external application folder'}
    if([version]$manifest.Package.Identity.Version -le [version]'1.2.0.0'){throw 'MSIX version must permit updating the previous installed version'}
}finally{$zip.Dispose()}
$cer=Join-Path $fixture 'GlassNotification.OSLink.cer'
$first=[Security.Cryptography.X509Certificates.X509Certificate2]::new($cer)
& $shell -NoProfile -File $builder -Root $fixture -UseLocalSigningKey
if($LASTEXITCODE -ne 0){throw 'Repeat package signing failed'}
$second=[Security.Cryptography.X509Certificates.X509Certificate2]::new($cer)
if($first.Thumbprint -ne $second.Thumbprint){throw 'Rebuild changed the signing identity'}
$package=Join-Path $fixture 'GlassNotification.OSLink.msix'
$zip=[IO.Compression.ZipFile]::OpenRead($package)
$buffer=[IO.MemoryStream]::new()
try{$stream=$zip.GetEntry('AppxSignature.p7x').Open();try{$stream.CopyTo($buffer)}finally{$stream.Dispose()}}finally{$zip.Dispose()}
$bytes=$buffer.ToArray();$buffer.Dispose()
if([Text.Encoding]::ASCII.GetString($bytes,0,4) -ne 'PKCX'){throw 'Package has no MSIX signature header'}
$cms=[Security.Cryptography.Pkcs.SignedCms]::new();$cms.Decode([byte[]]$bytes[4..($bytes.Length-1)])
$cms.CheckSignature($true)
if($cms.SignerInfos.Count -ne 1 -or $cms.SignerInfos[0].Certificate.Thumbprint -ne $first.Thumbprint){throw 'Public certificate does not match package signer'}
if(!('GlassPackageSigner' -as [type])){Add-Type -Path (Join-Path $Root 'identity/PackageSigner.cs')}
$status=[GlassPackageSigner]::Verify($package)
if($status -notin @(0,-2146762487,-2146762486)){throw ('Windows package verification failed: 0x{0:X8}' -f $status)}
$tampered=Join-Path $fixture 'tampered.msix'
# Change compressed asset bytes in place, preserving the ZIP headers and the signature entry.
# ZipArchive.Update rewrites APPX-specific ZIP metadata differently on .NET Framework and .NET.
$raw=[IO.File]::ReadAllBytes($package);$changed=$false
for($i=0;$i -lt $raw.Length-46;$i++){
    if([BitConverter]::ToUInt32($raw,$i) -ne 0x02014b50){continue}
    $nameLength=[BitConverter]::ToUInt16($raw,$i+28)
    if([Text.Encoding]::UTF8.GetString($raw,$i+46,$nameLength) -ne 'assets/StoreLogo.png'){continue}
    $local=[BitConverter]::ToUInt32($raw,$i+42)
    $start=$local+30+[BitConverter]::ToUInt16($raw,$local+26)+[BitConverter]::ToUInt16($raw,$local+28)
    $position=$start+[int]([BitConverter]::ToUInt32($raw,$i+20)/2)
    $raw[$position]=$raw[$position] -bxor 1;$changed=$true;break
}
if(!$changed){throw 'Could not find packaged asset to tamper with'}
[IO.File]::WriteAllBytes($tampered,$raw)
$status=[GlassPackageSigner]::Verify($tampered)
if($status -ne -2146869232){throw ('Windows did not reject modified package with TRUST_E_BAD_DIGEST: 0x{0:X8}' -f $status)}
$key=Join-Path (Split-Path (Split-Path $fixture)) 'work/signing/oslink-key.dpapi'
if(!(Test-Path -LiteralPath $key)){throw 'Protected local signing key was not saved'}
$acl=Get-Acl -LiteralPath (Split-Path $key)
if(!$acl.AreAccessRulesProtected){throw 'Signing directory must not inherit broad access rules'}
$first.Dispose();$second.Dispose()
$preservedHash=(Get-FileHash -LiteralPath $package).Hash
$missing=[IO.Path]::GetFullPath((Join-Path $fixture 'GlassNotifications.dll'))
if(!$missing.StartsWith([IO.Path]::GetFullPath($fixture)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid fixture cleanup target'}
Remove-Item -LiteralPath $missing
$oldPreference=$ErrorActionPreference;$ErrorActionPreference='Continue'
try{$failure=& $shell -NoProfile -File $builder -Root $fixture -UseLocalSigningKey 2>&1}finally{$ErrorActionPreference=$oldPreference}
if($LASTEXITCODE -eq 0){throw 'A package missing a required native dependency must not be published'}
if((Get-FileHash -LiteralPath $package).Hash -ne $preservedHash){throw 'Failed package build replaced the previously valid package'}
Write-Output 'PASS: standalone runtime payload, latest executable, no private files, signature, persistent identity, tamper detection and failed-build preservation'
