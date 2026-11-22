[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Game','Login')][string]$Role,
    [Parameter(Mandatory)][ipaddress]$DatabaseIp,
    [Parameter(Mandatory)][string]$DatabaseCertificateFile,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$DatabaseCertificateSha256,
    [Parameter(Mandatory)][string]$ServiceAccount,
    [Parameter(Mandatory)][ValidatePattern('\.wrap$')][string]$EnvelopeFile
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
if ((Get-FileHash -LiteralPath $DatabaseCertificateFile).Hash -ne $DatabaseCertificateSha256) { throw 'Unexpected database certificate.' }
$cert = New-Object Security.Cryptography.X509Certificates.X509Certificate2($DatabaseCertificateFile)
if ($cert.NotAfter -lt [DateTime]::Now -or $cert.NotBefore -gt [DateTime]::Now) { throw 'Database certificate is outside its validity period.' }
if ($DatabaseIp.AddressFamily -ne 'InterNetwork' -or $DatabaseIp.IPAddressToString -notlike '172.31.*') { throw 'Expected a project VPC IPv4 address.' }
$principal = New-Object Security.Principal.NTAccount($ServiceAccount)
$serviceSid = $principal.Translate([Security.Principal.SecurityIdentifier])
$admins = New-Object Security.Principal.SecurityIdentifier('S-1-5-32-544')
$system = New-Object Security.Principal.SecurityIdentifier('S-1-5-18')
$roleDir = Join-Path $env:ProgramData "Echodia\$($Role)SqlSecret"
$secretPath = Join-Path $roleDir 'connection.dpapi'
foreach ($dir in @((Split-Path -Parent $roleDir), $roleDir)) {
    [void][IO.Directory]::CreateDirectory($dir)
    $item = Get-Item -LiteralPath $dir
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected link in credential directory.' }
    if ((Get-Acl -LiteralPath $dir).GetOwner([Security.Principal.SecurityIdentifier]).Value -notin
        @($admins.Value, $system.Value, [Security.Principal.WindowsIdentity]::GetCurrent().User.Value)) { throw 'Credential directory owner is untrusted.' }
}
$acl = New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
$acl.SetOwner($admins)
foreach ($sid in @($admins,$system)) {
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow')))
}
if ($serviceSid.Value -notin @($system.Value,$admins.Value)) {
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($serviceSid,'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow')))
}
Set-Acl -LiteralPath $roleDir -AclObject $acl
$lockPath = Join-Path $roleDir 'provision.lock'
if ((Get-Item -LiteralPath $lockPath -ErrorAction SilentlyContinue).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected lock file link.' }
$lock = [IO.File]::Open($lockPath,'OpenOrCreate','ReadWrite','None')
$username = "Echodia$($Role)Runtime"
$source = "tcp:$($DatabaseIp.IPAddressToString),1433"
$encoding = New-Object Text.UTF8Encoding($false,$true)
try {
    if (Test-Path -LiteralPath $secretPath) {
        $file = Get-Item -LiteralPath $secretPath
        $fileAcl = Get-Acl -LiteralPath $secretPath
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $fileAcl.AreAccessRulesProtected -or
            @($fileAcl.Access | Where-Object { !$_.IsInherited }).Count -gt 0) { throw 'Unexpected ACL or link on credential file.' }
        $clear = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($secretPath),$null,'LocalMachine')
        $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($encoding.GetString($clear))
        if ($builder.UserID -cne $username -or $builder.DataSource -ne $source -or $builder.InitialCatalog -ne 'GameDB' -or
            !$builder.Encrypt -or $builder.TrustServerCertificate) { throw 'Existing protected credential belongs to another target.' }
    } else {
        $entropy = New-Object byte[] 32
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($entropy) } finally { $rng.Dispose() }
        $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
        $builder['Data Source']=$source
        $builder['Initial Catalog']='GameDB'
        $builder['User ID']=$username
        $builder['Password']='Aa1!'+[Convert]::ToBase64String($entropy)
        $builder['Encrypt']=$true
        $builder['TrustServerCertificate']=$false
        $builder['Persist Security Info']=$false
        $builder['Application Name']="Echodia-$Role"
        $clear=$encoding.GetBytes($builder.ConnectionString)
        $sealed=[Security.Cryptography.ProtectedData]::Protect($clear,$null,'LocalMachine')
        $temp = Join-Path $roleDir ([Guid]::NewGuid().ToString('N')+'.new')
        [IO.File]::WriteAllBytes($temp,$sealed)
        [IO.File]::Move($temp,$secretPath)
    }
    # Binding both role and endpoint prevents replaying one role's envelope as the other.
    $payload = $encoding.GetBytes(($username+'|'+$DatabaseIp.IPAddressToString+'|'+$builder.Password))
    $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($cert)
    try {
        # Legacy SQL SChannel CSP keys implement OAEP-SHA1 for RSA wrapping.
        $wrapped = $rsa.Encrypt($payload,[Security.Cryptography.RSAEncryptionPadding]::OaepSHA1)
        [IO.File]::WriteAllBytes([IO.Path]::GetFullPath($EnvelopeFile), $wrapped)
    } finally { $rsa.Dispose() }
    [pscustomobject]@{ Role=$Role; ProtectedFile=$secretPath; EnvelopeSha256=(Get-FileHash -LiteralPath $EnvelopeFile).Hash }
} catch {
    # Do not expose connection-string parser or cryptographic arguments to the remote shell.
    throw ('Could not provision the protected '+$Role+' credential ('+$_.Exception.GetBaseException().GetType().Name+').')
} finally {
    if ($clear) { [Array]::Clear($clear,0,$clear.Length) }
    if ($payload) { [Array]::Clear($payload,0,$payload.Length) }
    if ($entropy) { [Array]::Clear($entropy,0,$entropy.Length) }
    $cert.Dispose()
    $lock.Dispose()
}
