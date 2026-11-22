#Requires -RunAsAdministrator
param(
    [Parameter(Mandatory)][string]$RedisPrivateIp,
    [Parameter(Mandatory)][string]$GameServiceAccount
)
. (Join-Path $PSScriptRoot 'Common.ps1')
Add-Type -AssemblyName System.Security
$ip = Get-PrivateV4 $RedisPrivateIp
$sid = (New-Object Security.Principal.NTAccount($GameServiceAccount)).Translate([Security.Principal.SecurityIdentifier]).Value
$dir = Join-Path $env:ProgramData 'EchodiaRedisGameCredential'
Set-ProtectedDirectory $dir $sid
$file = Join-Path $dir 'connection.dpapi'
$scope = [Security.Cryptography.DataProtectionScope]::LocalMachine
$utf8 = New-Object Text.UTF8Encoding($false,$true)
if (Test-Path -LiteralPath $file) {
    $bytes = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($file),$null,$scope)
    try { $value = $utf8.GetString($bytes) } finally { [Array]::Clear($bytes,0,$bytes.Length) }
    $pattern = '^' + [regex]::Escape($ip) + ':6380,user=echodia,password=([0-9a-f]{64}),'
    if ($value -notmatch $pattern) { throw 'Existing credential belongs to a different endpoint or format.' }
    $password = $Matches[1]
} else {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes); $password = ([BitConverter]::ToString($bytes)).Replace('-','').ToLowerInvariant() }
    finally { $rng.Dispose(); [Array]::Clear($bytes,0,$bytes.Length) }
    # This role has one Redis primary and no application pub/sub. Disable topology probes it does not serve.
    $value = "$($ip):6380,user=echodia,password=$password,ssl=true,sslHost=$ip,defaultDatabase=0,version=8.0,configChannel=,tieBreaker=," +
        '$CONFIG=,$CLUSTER=,$SENTINEL=,$SUBSCRIBE=,$PSUBSCRIBE='
    $bytes = $utf8.GetBytes($value)
    try { $sealed = [Security.Cryptography.ProtectedData]::Protect($bytes,$null,$scope) }
    finally { [Array]::Clear($bytes,0,$bytes.Length) }
    $temp = Join-Path $dir ([guid]::NewGuid().ToString('N') + '.pending')
    [IO.File]::WriteAllBytes($temp,$sealed)
    [IO.File]::Move($temp,$file)
}
$sha = [Security.Cryptography.SHA256]::Create()
try { $verifier = ([BitConverter]::ToString($sha.ComputeHash($utf8.GetBytes($password)))).Replace('-','').ToLowerInvariant() }
finally { $sha.Dispose() }
[pscustomobject]@{ ProtectedConnection=$file; RedisAclSha256=$verifier }
