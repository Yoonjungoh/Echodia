[CmdletBinding()]
param([Parameter(Mandatory)][string]$BackupFile,[Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$BackupSha256)
$ErrorActionPreference='Stop'
if((Get-FileHash -LiteralPath $BackupFile).Hash -ne $BackupSha256){throw 'Backup checksum mismatch'}
$conn=New-Object System.Data.SqlClient.SqlConnection 'Data Source=.\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;Connect Timeout=15'
$conn.Open()
try {
    $cmd=$conn.CreateCommand()
    $cmd.CommandText="SELECT CONVERT(nvarchar(350), SERVERPROPERTY('InstanceDefaultBackupPath'))"
    $dir=[string]$cmd.ExecuteScalar()
    $dest=Join-Path $dir ('Echodia-cutover-'+$BackupSha256.Substring(0,16)+'.bak')
    if(!(Test-Path $dest)){Copy-Item -LiteralPath $BackupFile -Destination $dest}
    if((Get-FileHash $dest).Hash -ne $BackupSha256){throw 'Copied backup differs'}
    $cmd.CommandTimeout=180
    [void]$cmd.Parameters.Add('@bak',[Data.SqlDbType]::NVarChar,512)
    $cmd.Parameters['@bak'].Value=$dest
    $cmd.CommandText=@"
SET NOCOUNT ON;
IF DATABASEPROPERTYEX('GameDB','Status') IS NOT NULL THROW 51000,'A database named GameDB already exists on the destination',1;
RESTORE VERIFYONLY FROM DISK=@bak WITH CHECKSUM;
DECLARE @d nvarchar(512)=CONVERT(nvarchar(350),SERVERPROPERTY('InstanceDefaultDataPath'))+N'GameDB.mdf';
DECLARE @l nvarchar(512)=CONVERT(nvarchar(350),SERVERPROPERTY('InstanceDefaultLogPath'))+N'GameDB_log.ldf';
RESTORE DATABASE GameDB FROM DISK=@bak WITH MOVE N'GameDB' TO @d,MOVE N'GameDB_log' TO @l,CHECKSUM,RECOVERY;
DBCC CHECKDB ('GameDB') WITH NO_INFOMSGS;
"@
    [void]$cmd.ExecuteNonQuery()
    $cmd.CommandText=@"
SELECT (SELECT COUNT(*) FROM GameDB.dbo.Account) AS accounts,
       (SELECT COUNT(*) FROM GameDB.dbo.Account WHERE AccountPassword IS NOT NULL OR DATALENGTH(PasswordHashV1)<>48 OR PasswordHashV1 IS NULL) AS invalidCredentials,
       (SELECT COUNT(*) FROM GameDB.dbo.Player) AS characters,
       (SELECT COUNT(*) FROM GameDB.dbo.Quest) AS quests,
       (SELECT COUNT(*) FROM GameDB.dbo.PlayerItem) AS items FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
"@
    $cmd.ExecuteScalar()
} finally { $conn.Dispose() }
