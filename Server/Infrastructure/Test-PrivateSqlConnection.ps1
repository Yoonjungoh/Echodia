[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('Game','Login')][string]$Role)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$path=Join-Path $env:ProgramData "Echodia\$($Role)SqlSecret\connection.dpapi"
$clear=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($path),$null,'LocalMachine')
try {
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder([Text.Encoding]::UTF8.GetString($clear))
    $expected="Echodia$($Role)Runtime"
    if (!$builder.Encrypt -or $builder.TrustServerCertificate -or $builder.UserID -cne $expected -or
        $builder.InitialCatalog -ne 'GameDB' -or $builder.DataSource -notmatch '^tcp:172\.31\.\d{1,3}\.\d{1,3},1433$') { throw 'Protected SQL configuration did not pass endpoint checks.' }
    $conn=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
    $conn.Open()
    $cmd=$conn.CreateCommand()
    # Return identity, DB and permission booleans only. Avoid a SELECT on credentials.
    $cmd.CommandText=@"
SELECT ORIGINAL_LOGIN(), DB_NAME(),
 ISNULL(HAS_PERMS_BY_NAME('dbo.Account','OBJECT','SELECT','PasswordHashV1','COLUMN'),0),
 ISNULL(HAS_PERMS_BY_NAME('dbo.Player','OBJECT','UPDATE'),0);
"@
    $reader=$cmd.ExecuteReader()
    if (!$reader.Read() -or $reader.GetString(0) -cne $expected -or $reader.GetString(1) -ne 'GameDB' -or
        $reader.GetInt32(2) -ne [int]($Role -eq 'Login') -or $reader.GetInt32(3) -ne [int]($Role -eq 'Game')) {
        throw 'SQL authenticated identity or effective role differs.'
    }
    [pscustomobject]@{ Role=$Role; Database='GameDB'; CertificateValidated=$true; RoleVerified=$true }
} catch {
    $ex=$_.Exception.GetBaseException()
    $code=if($ex -is [System.Data.SqlClient.SqlException]){$ex.Number}else{$ex.GetType().Name}
    throw ('Private SQL connection probe failed for '+$Role+' ('+$code+').')
} finally {
    if ($conn) { $conn.Dispose() }
    [Array]::Clear($clear,0,$clear.Length)
}
