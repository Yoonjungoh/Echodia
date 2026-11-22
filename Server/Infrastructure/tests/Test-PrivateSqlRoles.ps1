# Runs only on the developer's LocalDB instance using isolated databases and role names.
$ErrorActionPreference='Stop'
$tag=[Guid]::NewGuid().ToString('N')
$dbName='EchodiaRoleTest_'+$tag
$game='EchodiaGameRuntime_'+$tag
$login='EchodiaLoginRuntime_'+$tag
$conn=New-Object System.Data.SqlClient.SqlConnection 'Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True'
$conn.Open()
$cmd=$conn.CreateCommand()
function Invoke-Sql([string]$text) {
    $cmd.CommandText=$text
    [void]$cmd.ExecuteNonQuery()
}
function Assert-Rejected([string]$text,[int[]]$numbers) {
    try { Invoke-Sql $text }
    catch {
        if ($_.Exception.GetBaseException().Number -in $numbers) { return }
        throw
    }
    throw 'Unexpectedly accepted a prohibited SQL operation.'
}
try {
    Invoke-Sql "CREATE DATABASE [$dbName]"
    Invoke-Sql "CREATE LOGIN [$game] WITH PASSWORD=N'A1!$tag',CHECK_POLICY=OFF;CREATE LOGIN [$login] WITH PASSWORD=N'B2!$tag',CHECK_POLICY=OFF"
    $conn.ChangeDatabase($dbName)
    Invoke-Sql 'CREATE TABLE dbo.Account(AccountDbId int,AccountId nvarchar(128),AccountPassword nvarchar(128),PasswordHashV1 varbinary(48));CREATE TABLE dbo.Player(id int);CREATE TABLE dbo.Quest(id int);CREATE TABLE dbo.PlayerItem(id int)'
    $grant=[IO.File]::ReadAllText((Join-Path (Split-Path -Parent $PSScriptRoot) 'Grant-PrivateSqlRoles.sql'))
    $grant=$grant.Replace('GameDB',$dbName).Replace('EchodiaGameRuntime',$game).Replace('EchodiaLoginRuntime',$login)
    Invoke-Sql "INSERT dbo.Account(AccountId,AccountPassword) VALUES('legacy-fixture','dummy')"
    Assert-Rejected $grant @(547)
    Invoke-Sql 'UPDATE dbo.Account SET AccountPassword=NULL,PasswordHashV1=CRYPT_GEN_RANDOM(48)'
    Invoke-Sql $grant
    Invoke-Sql $grant

    Invoke-Sql "EXECUTE AS LOGIN=N'$login'"
    Assert-Rejected "INSERT dbo.Account(AccountId,AccountPassword,PasswordHashV1) VALUES('plaintext','dummy',CRYPT_GEN_RANDOM(48))" @(547)
    Assert-Rejected "INSERT dbo.Account(AccountId) VALUES('missing')" @(547)
    Assert-Rejected "INSERT dbo.Account(AccountId,PasswordHashV1) VALUES('short',0x01)" @(547)
    Invoke-Sql "INSERT dbo.Account(AccountId,PasswordHashV1) VALUES('valid',CRYPT_GEN_RANDOM(48))"
    Invoke-Sql "UPDATE dbo.Account SET PasswordHashV1=CRYPT_GEN_RANDOM(48) WHERE AccountId='valid'"
    Assert-Rejected 'SELECT id FROM dbo.Player' @(229,230)
    Invoke-Sql 'REVERT'

    Invoke-Sql "EXECUTE AS LOGIN=N'$game'"
    Invoke-Sql 'INSERT dbo.Player VALUES(1);UPDATE dbo.Player SET id=2;DELETE dbo.Player WHERE id=2'
    Assert-Rejected 'SELECT PasswordHashV1 FROM dbo.Account' @(229,230)
    Invoke-Sql 'REVERT'
    $cmd.CommandText='SELECT COUNT(*) FROM dbo.Account'
    if ([int]$cmd.ExecuteScalar() -ne 2) { throw 'Unexpected test account count.' }
    'SQL role checks passed, including legacy preflight and all three invalid credential writes.'
} finally {
    # Reset the login context and any aborted test transaction before removing these fixtures.
    $conn.Close()
    $conn.Open()
    try {
        Invoke-Sql "DROP DATABASE IF EXISTS [$dbName]; IF SUSER_ID(N'$game') IS NOT NULL DROP LOGIN [$game]; IF SUSER_ID(N'$login') IS NOT NULL DROP LOGIN [$login]"
    } finally { $conn.Dispose() }
}
