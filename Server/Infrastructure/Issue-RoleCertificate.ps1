# Run on the administrator PC with PowerShell 7 and the CSR obtained over verified SSH.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Database','Login','Cache','GameClient')][string]$Role,
    [Parameter(Mandatory)][string]$RequestFile,
    [Parameter(Mandatory)][string]$PublicCertificate,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9.-]+$')][string]$ExpectedCommonName,
    [Parameter(Mandatory)][ipaddress]$PrivateIp
)
$ErrorActionPreference='Stop'
if($PrivateIp.AddressFamily -ne 'InterNetwork' -or $PrivateIp.IPAddressToString -notlike '172.31.*'){throw 'Unexpected project VPC address'}
$caDir=Join-Path $env:LOCALAPPDATA 'Echodia\private-pki'
[void](New-Item $caDir -ItemType Directory -Force)
$issuerFile=Join-Path $caDir 'Echodia-role-issuer.cer'
if(!(Test-Path -LiteralPath $issuerFile)){
    $ca=New-SelfSignedCertificate -Subject 'CN=Echodia private role issuer' -CertStoreLocation 'Cert:\CurrentUser\My' -Type Custom -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -KeyUsage CertSign,CRLSign -TextExtension '2.5.29.19={critical}{text}CA=1&pathlength=0' -NotAfter ([DateTime]::Now.AddMonths(18))
    Export-Certificate -Cert $ca -FilePath $issuerFile | Out-Null
}
$pub=New-Object Security.Cryptography.X509Certificates.X509Certificate2($issuerFile)
$issuer=Get-Item -LiteralPath ('Cert:\CurrentUser\My\'+$pub.Thumbprint)
if(!$issuer.HasPrivateKey -or $issuer.Subject -ne 'CN=Echodia private role issuer'){throw 'Missing project issuer'}
$csr=Get-Content -Raw -LiteralPath $RequestFile
$csr=$csr.Replace('NEW CERTIFICATE REQUEST','CERTIFICATE REQUEST')
$req=[Security.Cryptography.X509Certificates.CertificateRequest]::LoadSigningRequestPem($csr,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.X509Certificates.CertificateRequestLoadOptions]::Default,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
if($req.SubjectName.Name -ne ('CN='+$ExpectedCommonName)){throw 'CSR subject does not match this host'}
if($req.PublicKey.Oid.Value -ne '1.2.840.113549.1.1.1' -or $req.PublicKey.GetRSAPublicKey().KeySize -lt 2048){throw 'Expected an RSA key of at least 2048 bits'}
$client=$Role -eq 'GameClient'
$purpose=if($client){'1.3.6.1.5.5.7.3.2'}else{'1.3.6.1.5.5.7.3.1'}
$oids=[Security.Cryptography.OidCollection]::new(); [void]$oids.Add([Security.Cryptography.Oid]::new($purpose))
$req.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false,$false,0,$true))
$req.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($oids,$true))
$req.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(([Security.Cryptography.X509Certificates.X509KeyUsageFlags]'DigitalSignature,KeyEncipherment'),$true))
$san=[Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
$san.AddDnsName($ExpectedCommonName); $san.AddIpAddress($PrivateIp); $req.CertificateExtensions.Add($san.Build())
$serial=[byte[]]::new(16); [Security.Cryptography.RandomNumberGenerator]::Fill($serial)
$leaf=$req.Create($issuer,[DateTimeOffset]::Now.AddMinutes(-3),[DateTimeOffset]::Now.AddDays(90),$serial)
[IO.File]::WriteAllBytes($PublicCertificate,$leaf.RawData)
[pscustomobject]@{Role=$Role; Thumbprint=$leaf.Thumbprint; Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($leaf.RawData)); ExpiresUtc=$leaf.NotAfter.ToUniversalTime(); IssuerPublicFile=$issuerFile}
