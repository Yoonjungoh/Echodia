[CmdletBinding()]
param([Parameter(Mandatory)][string]$LeafFile,[Parameter(Mandatory)][string]$IssuerFile,
      [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$IssuerSha256,
      [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$LeafSha256,
      [string]$PrivateKeyReader)
$ErrorActionPreference='Stop'
foreach($pair in @(@($LeafFile,$LeafSha256),@($IssuerFile,$IssuerSha256))){
    if((Get-FileHash -LiteralPath $pair[0]).Hash -ne $pair[1]){throw 'PKI public file verification failed'}
}
$dest=Join-Path $env:ProgramData 'Echodia\Trust'
[void](New-Item -ItemType Directory -Force -Path $dest)
$caFile=Join-Path $dest 'private-role-ca.cer'
Copy-Item -LiteralPath $IssuerFile -Destination $caFile -Force
$public=New-Object Security.Cryptography.X509Certificates.X509Certificate2($caFile)
if($public.Subject -ne 'CN=Echodia private role issuer'){throw 'Unexpected issuer'}
Import-Certificate -FilePath $caFile -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
$leaf=New-Object Security.Cryptography.X509Certificates.X509Certificate2($LeafFile)
$path='Cert:\LocalMachine\My\'+$leaf.Thumbprint
$bound=Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
if(!$bound -or !$bound.HasPrivateKey) {
    certreq.exe -accept -machine -q $LeafFile | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Could not bind leaf to its local private key'}
    $bound=Get-Item -LiteralPath $path
}
$chain=New-Object Security.Cryptography.X509Certificates.X509Chain
try {
    # This small offline CA has no CRL. Pin the final CA as well as both received public files.
    $chain.ChainPolicy.RevocationMode='NoCheck'
    if(!$bound.HasPrivateKey -or !$chain.Build($bound) -or $chain.ChainElements.Count -ne 2 -or
       $chain.ChainElements[1].Certificate.Thumbprint -ne $public.Thumbprint){throw 'Role certificate validation failed'}
} finally { $chain.Dispose() }
if($PrivateKeyReader){
    $sid=(New-Object Security.Principal.NTAccount($PrivateKeyReader)).Translate([Security.Principal.SecurityIdentifier])
    $container=$bound.PrivateKey.CspKeyContainerInfo
    if(!$container.MachineKeyStore){throw 'Expected a machine CSP key'}
    $key=Join-Path "$env:ProgramData\Microsoft\Crypto\RSA\MachineKeys" $container.UniqueKeyContainerName
    icacls.exe $key /grant ('*'+$sid.Value+':(R)') | Out-Null
    if($LASTEXITCODE){throw 'Could not grant the service read access to its private key'}
}
$bound.Thumbprint
