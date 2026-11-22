# Target the November 2026 SQL Server 2022 GDR security floor for this cutover.
# Microsoft publishes this package SHA256 at https://support.microsoft.com/en-us/servicing/sql/sql-server-2022/general-distribution-release/kb5132747-november-2026
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageFile)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
function Get-EngineVersion {
    $c=New-Object System.Data.SqlClient.SqlConnection 'Data Source=lpc:.\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10'
    try {
        $c.Open()
        $q=$c.CreateCommand()
        $q.CommandText="SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion'))"
        [version]([string]$q.ExecuteScalar())
    } finally {$c.Dispose()}
}
$before=Get-EngineVersion
if($before.Major -ne 16){throw 'Unexpected SQL Server major version'}
if($before.Build -ge 4000) {
    if($before -lt [version]'16.0.4315.2'){throw 'CU servicing branch requires its matching security package'}
    [pscustomobject]@{Before=[string]$before;After=[string]$before;UpdateNeeded=$false}
    return
}
$floor=[version]'16.0.1220.3'
if($before -ge $floor){
    [pscustomobject]@{Before=[string]$before;After=[string]$before;UpdateNeeded=$false}
    return
}
$exe=Get-Item -LiteralPath $PackageFile
$auth=Get-AuthenticodeSignature -LiteralPath $exe.FullName
if($auth.Status -ne 'Valid' -or $auth.SignerCertificate.Subject -notmatch 'CN=Microsoft Corporation' -or
    (Get-FileHash $exe.FullName).Hash -ne 'B566F09A461AE4A563A11FF65FD6385993088437A0F3243AA114A90AF5B71A79'){
    throw 'Microsoft GDR authenticity check failed'
}
$p=Start-Process -FilePath $exe.FullName -ArgumentList @('/quiet','/action=patch','/instancename=SQLEXPRESS','/IAcceptSQLServerLicenseTerms') -WindowStyle Hidden -Wait -PassThru
if($p.ExitCode -notin 0,3010){throw "SQL Server patch failed ($($p.ExitCode))"}
$after=Get-EngineVersion
if($after -lt $floor){throw 'Database engine version does not include the expected GDR'}
[pscustomobject]@{Before=[string]$before;After=[string]$after;UpdateNeeded=$true;ExitCode=$p.ExitCode}
