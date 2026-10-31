# Trusts (or removes) the Glass Notification signing certificate so the signed package can be registered
# without developer mode. Needs an elevated PowerShell. Run once; use -Remove to undo.
param([switch]$Remove,[string]$Certificate=(Join-Path (Split-Path $PSScriptRoot) 'GlassNotification.OSLink.cer'))
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if (!([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated (administrator) PowerShell.'
}
$store='Cert:\LocalMachine\TrustedPeople'
if ($Remove) {
    $found=@(Get-ChildItem $store | Where-Object { $_.Subject -eq 'CN=GlassNotification' })
    $found | Remove-Item
    Write-Output ('REMOVED: ' + $found.Count + ' certificate(s)')
    return
}
if (!(Test-Path -LiteralPath $Certificate)) { throw "Certificate not found: $Certificate. Run identity\build-package.ps1 first." }
$imported=Import-Certificate -FilePath $Certificate -CertStoreLocation $store
Write-Output ('TRUSTED: ' + $imported.Subject + ' ' + $imported.Thumbprint)
