# Builds and signs a standalone MSIX containing the current application and its runtime dependencies.
param([string]$Root=(Split-Path $PSScriptRoot),[switch]$UseLocalSigningKey)
$ErrorActionPreference='Stop'
if(!('System.Security.Cryptography.ProtectedData' -as [type])){Add-Type -AssemblyName System.Security}
$subject='CN=GlassNotification'
$Root=[IO.Path]::GetFullPath($Root)
$workspace=Split-Path (Split-Path $Root)
$kits=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
function Find-Tool([string]$name){
    $tool=Get-ChildItem $kits -Recurse -Filter $name -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\10\.\d+\.\d+\.\d+\\x64\\' } |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (!$tool) { throw "$name was not found. Install the Windows SDK." }
    $tool
}
$makeappx=Find-Tool 'makeappx.exe'
$signtool=Find-Tool 'signtool.exe'
$required=@('GlassNotification.Linked.exe','GlassNotification.Linked.exe.config','GlassNotifications.dll','GlassBackdrop.dll','wuceffectsi.dll','Microsoft.WindowsAppRuntime.dll','Microsoft.UI.pri')
$assets=@('AppIcon.ico','StoreLogo.png','Square44x44Logo.png','Square150x150Logo.png','chime.wav')
foreach($name in $required){if(!(Test-Path -LiteralPath (Join-Path $Root $name) -PathType Leaf)){throw ('Missing runtime file. Build the application first: '+$name)}}
foreach($name in $assets){if(!(Test-Path -LiteralPath (Join-Path $Root ('assets/'+$name)) -PathType Leaf)){throw ('Missing application asset: '+$name)}}
# Prefer the existing identity. On build accounts without a loaded profile, keep a protected local key
# and use an ephemeral CNG key in the signing process instead of CertEnroll's profile-dependent store.
$keyFolder=Join-Path $workspace 'work/signing'
$keyFile=Join-Path $keyFolder 'oslink-key.dpapi'
function Read-LocalCertificate {
    Protect-KeyDirectory
    $protected=[IO.File]::ReadAllBytes($keyFile)
    $bytes=[Security.Cryptography.ProtectedData]::Unprotect($protected,$null,[Security.Cryptography.DataProtectionScope]::LocalMachine)
    try { return [Security.Cryptography.X509Certificates.X509Certificate2]::new($bytes,[string]::Empty,[Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet) }
    finally { [Array]::Clear($bytes,0,$bytes.Length) }
}
function Protect-KeyDirectory {
    # Machine DPAPI works without a user profile; restrict the ciphertext to the folder owner,
    # this build account, SYSTEM and Administrators before writing any key material.
    New-Item -ItemType Directory -Force $keyFolder | Out-Null
    $owner=(Get-Acl -LiteralPath $keyFolder).GetOwner([Security.Principal.SecurityIdentifier])
    $projectOwner=(Get-Acl -LiteralPath $Root).GetOwner([Security.Principal.SecurityIdentifier])
    $acl=Get-Acl -LiteralPath $keyFolder
    $acl.SetAccessRuleProtection($true,$false)
    foreach($rule in @($acl.Access)){$acl.RemoveAccessRuleSpecific($rule)}
    $sids=@($owner,$projectOwner,[Security.Principal.WindowsIdentity]::GetCurrent().User,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'),[Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
    foreach($sid in $sids){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
    # Set only the DACL through .NET. Set-Acl can also request audit privileges on restricted accounts.
    if('System.IO.FileSystemAclExtensions' -as [type]){[IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($keyFolder),$acl)}
    else{[IO.Directory]::SetAccessControl($keyFolder,$acl)}
}
function New-LocalCertificate {
    Protect-KeyDirectory
    $rsa=[Security.Cryptography.RSACng]::new(2048)
    $created=$null;$bytes=$null
    try {
        $request=[Security.Cryptography.X509Certificates.CertificateRequest]::new($subject,$rsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false,$false,0,$true))
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature,$true))
        $eku=[Security.Cryptography.OidCollection]::new();$eku.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3')) | Out-Null
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku,$true))
        $created=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddYears(5))
        $bytes=$created.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx,[string]::Empty)
        $protected=[Security.Cryptography.ProtectedData]::Protect($bytes,$null,[Security.Cryptography.DataProtectionScope]::LocalMachine)
        [IO.File]::WriteAllBytes($keyFile,$protected)
    }finally{if($bytes){[Array]::Clear($bytes,0,$bytes.Length)};if($created){$created.Dispose()};$rsa.Dispose()}
    return Read-LocalCertificate
}
$localSigning=$UseLocalSigningKey -or (Test-Path -LiteralPath $keyFile)
$cert=$null
if($localSigning){$cert=if(Test-Path -LiteralPath $keyFile){Read-LocalCertificate}else{New-LocalCertificate}}
if(!$cert){$cert=Get-ChildItem Cert:\CurrentUser\My | Where-Object {
    $_.Subject -eq $subject -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) -and ($_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3')
} | Sort-Object NotAfter -Descending | Select-Object -First 1}
if (!$cert) {
    try {$cert=New-SelfSignedCertificate -Type Custom -Subject $subject -KeyUsage DigitalSignature -FriendlyName 'Glass Notification sideload signing' `
        -KeyExportPolicy NonExportable -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3','2.5.29.19={text}')
    }catch{
        if($_.Exception.Message -notmatch '0x80070002|0x80090016|0x80090020'){throw}
        Write-Output 'Certificate profile unavailable; using protected local signing key.'
        $cert=New-LocalCertificate;$localSigning=$true
    }
}
try {
if($cert.Subject -ne $subject -or !$cert.HasPrivateKey -or $cert.NotAfter -le (Get-Date).AddDays(30)){throw 'The signing identity is invalid or expires within 30 days. Renew it explicitly before building.'}
$buildFolder=Join-Path $workspace ('work/package-'+[Guid]::NewGuid().ToString('N'))
$stage=Join-Path $buildFolder 'input'
New-Item -ItemType Directory -Force (Join-Path $stage 'assets') | Out-Null
[xml]$manifest=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'AppxManifest.xml'))
# The loose manifest remains available for legacy external-folder registration; the MSIX is self-contained.
foreach($node in @($manifest.SelectNodes('//*[local-name()="AllowExternalContent"]'))){$node.ParentNode.RemoveChild($node) | Out-Null}
$manifest.Save((Join-Path $stage 'AppxManifest.xml'))
foreach($name in @('GlassNotification.Linked.exe','GlassNotification.Linked.exe.config')){Copy-Item -LiteralPath (Join-Path $Root $name) -Destination $stage}
Get-ChildItem -LiteralPath $Root -File | Where-Object {$_.Extension -in @('.dll','.pri')} | Copy-Item -Destination $stage
foreach($name in $assets){Copy-Item -LiteralPath (Join-Path $Root ('assets/'+$name)) -Destination (Join-Path $stage 'assets')}
$package=Join-Path $buildFolder 'GlassNotification.OSLink.msix'
& $makeappx pack /o /d $stage /p $package | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx failed' }
if($localSigning){
    if(!('GlassPackageSigner' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'PackageSigner.cs')}
    [GlassPackageSigner]::Sign($package,$cert.Handle)
}else{
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint /s My $package | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'signtool failed' }
}
if(!('GlassPackageSigner' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'PackageSigner.cs')}
$status=[GlassPackageSigner]::Verify($package)
if($status -notin @(0,-2146762487,-2146762486)){throw ('Package signature verification failed: 0x{0:X8}' -f $status)}
[IO.File]::WriteAllBytes((Join-Path $Root 'GlassNotification.OSLink.cer'),$cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
Copy-Item -LiteralPath $package -Destination (Join-Path $Root 'GlassNotification.OSLink.msix') -Force
Write-Output ('PACKAGE OK: GlassNotification.OSLink.msix (certificate ' + $cert.Thumbprint + ')')
}finally{$cert.Dispose()}
