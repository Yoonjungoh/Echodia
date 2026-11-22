[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Game','Login')][string]$Role,
    [Parameter(Mandatory)][ipaddress]$DatabaseIp,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$ServerCertificate,
    [Parameter(Mandatory)][string]$EnvelopeFile,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$EnvelopeSha256
)
$ErrorActionPreference = 'Stop'
if ((Get-FileHash -LiteralPath $EnvelopeFile).Hash -ne $EnvelopeSha256) { throw 'The encrypted envelope differs from its service host.' }
if (!(Get-NetIPAddress -IPAddress $DatabaseIp.IPAddressToString -AddressFamily IPv4 -ErrorAction SilentlyContinue)) { throw 'Expected database host private IP on this machine.' }
$name = "Echodia$($Role)Runtime"
$certificate = Get-Item -LiteralPath ('Cert:\LocalMachine\My\'+$ServerCertificate)
$rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)
try {
    $clear = $rsa.Decrypt([IO.File]::ReadAllBytes($EnvelopeFile),[Security.Cryptography.RSAEncryptionPadding]::OaepSHA1)
    $parts = [Text.Encoding]::UTF8.GetString($clear).Split('|')
    if ($parts.Length -ne 3 -or $parts[0] -cne $name -or $parts[1] -cne $DatabaseIp.IPAddressToString -or
        $parts[2] -cnotmatch '^Aa1![A-Za-z0-9+/]{43}=$') { throw 'Envelope does not belong to this role and endpoint.' }
    # Shared memory only. The bootstrap administrator never uses a remote SQL login.
    $connection = New-Object System.Data.SqlClient.SqlConnection 'Data Source=lpc:.\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10'
    $connection.Open()
    $cmd = $connection.CreateCommand()
    [void]$cmd.Parameters.Add('@name',[Data.SqlDbType]::NVarChar,128)
    [void]$cmd.Parameters.Add('@password',[Data.SqlDbType]::NVarChar,128)
    $cmd.Parameters['@name'].Value = $name
    $cmd.Parameters['@password'].Value = $parts[2]
    $cmd.CommandText = @"
SET NOCOUNT ON;
IF @name NOT IN ('EchodiaGameRuntime','EchodiaLoginRuntime') THROW 51000,'Invalid runtime name.',1;
IF SUSER_ID(@name) IS NULL BEGIN
    DECLARE @ddl nvarchar(1000) = N'CREATE LOGIN '+QUOTENAME(@name)+N' WITH PASSWORD='+QUOTENAME(@password,'''')+N', CHECK_POLICY=ON, CHECK_EXPIRATION=OFF, DEFAULT_DATABASE=[GameDB]';
    EXEC (@ddl);
END ELSE IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name=@name AND is_disabled=0
                    AND is_policy_checked=1 AND PWDCOMPARE(@password,password_hash)=1)
    THROW 51001,'Existing login credential or policy differs.',1;
SELECT COUNT(*) FROM sys.sql_logins WHERE name IN ('EchodiaGameRuntime','EchodiaLoginRuntime');
"@
    $created = [int]$cmd.ExecuteScalar()
    $cmd.Parameters.Clear()
    if ($created -eq 2) {
        $connection.ChangeDatabase('GameDB')
        $cmd.CommandText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Grant-PrivateSqlRoles.sql'))
        [void]$cmd.ExecuteNonQuery()
    }
    [pscustomobject]@{Role=$Role; RuntimeLogins=$created; EffectivePermissionsVerified=($created -eq 2)}
} catch {
    # Provider messages or a failed dynamic DDL batch must not reach SSH logs.
    $ex=$_.Exception.GetBaseException()
    $code=if($ex -is [System.Data.SqlClient.SqlException]){$ex.Number}else{$ex.GetType().Name}
    throw ('Private SQL provisioning failed for '+$Role+' ('+$code+').')
} finally {
    if ($clear) { [Array]::Clear($clear,0,$clear.Length) }
    if ($parts) { [Array]::Clear($parts,0,$parts.Length) }
    if ($connection) { $connection.Dispose() }
    $rsa.Dispose()
}
