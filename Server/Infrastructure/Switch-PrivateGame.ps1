#Requires -RunAsAdministrator
<#
.SYNOPSIS
Switch a drained NSSM game service to the private SQL, login and Redis roles.
.DESCRIPTION
The caller takes and verifies the production DB backup before stopping the old game writer.
The one-time protected snapshot restores NSSM and machine settings via -RestoreStoppedConfiguration.
Reconcile the authoritative database before subsequently starting a restored service.
Listener readiness only covers the game TCP endpoint. Verify a normal Unity login, character list and reward afterward.
#>
[CmdletBinding(DefaultParameterSetName='Switch')]
param(
    [Parameter(Mandatory,ParameterSetName='Switch')][ipaddress]$LoginPrivateIp,
    [Parameter(Mandatory,ParameterSetName='Switch')][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$GameClientThumbprint,
    [Parameter(Mandatory,ParameterSetName='Switch')][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$IssuerSha256,
    [Parameter(ParameterSetName='Switch')][ValidateRange(1,65535)][int]$LoginPort=8443,
    [Parameter(ParameterSetName='Switch')][ValidateRange(1,65535)][int]$GamePort=7777,
    [Parameter(ParameterSetName='Switch')][string]$ReleaseDirectory='C:\echodia\split-game',
    [Parameter(ParameterSetName='Switch')][string]$SqlCredential=(Join-Path $env:ProgramData 'Echodia\GameSqlSecret\connection.dpapi'),
    [Parameter(ParameterSetName='Switch')][string]$RedisCredential=(Join-Path $env:ProgramData 'EchodiaRedisGameCredential\connection.dpapi'),
    [Parameter(ParameterSetName='Switch')][string]$IssuerFile=(Join-Path $env:ProgramData 'Echodia\Trust\private-role-ca.cer'),
    [Parameter(Mandatory,ParameterSetName='Restore')][switch]$RestoreStoppedConfiguration,
    [ValidatePattern('^[a-zA-Z0-9_.-]+$')][string]$ServiceName='EchodiaServer',
    [string]$ExpectedPriorExecutable='C:\echodia\server\Server.exe',
    [string]$RollbackDirectory=(Join-Path $env:ProgramData 'Echodia\CutoverRollback')
)
$ErrorActionPreference='Stop'
$parameters="HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName\Parameters"
if((Get-Service -Name $ServiceName).Status -ne 'Stopped'){throw 'Drain and stop the game writer before changing its configuration.'}
$prior=Get-ItemProperty -LiteralPath $parameters
$expected=[IO.Path]::GetFullPath($ExpectedPriorExecutable)
$rollback=(New-Item -Path $RollbackDirectory -ItemType Directory -Force)
if($rollback.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Unexpected rollback directory reparse point.'}
$acl=New-Object Security.AccessControl.DirectorySecurity
$acl.SetSecurityDescriptorSddlForm('D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)')
Set-Acl -LiteralPath $rollback.FullName -AclObject $acl
$snapshot=Join-Path $rollback.FullName 'before-private-services.dpapi'
Add-Type -AssemblyName System.Security
$scope=[Security.Cryptography.DataProtectionScope]::LocalMachine
$saved=$null
if(Test-Path -LiteralPath $snapshot){
    $info=Get-Item -LiteralPath $snapshot
    if($info.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Unexpected rollback file reparse point.'}
    $clear=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($snapshot),$null,$scope)
    try{$saved=[Text.Encoding]::UTF8.GetString($clear)|ConvertFrom-Json} finally{[Array]::Clear($clear,0,$clear.Length)}
    # The original deployment snapshot predates ServiceName and belongs to the original EchodiaServer service.
    $name=if($saved.PSObject.Properties['ServiceName']){$saved.ServiceName}else{'EchodiaServer'}
    if($name -ne $ServiceName -or $saved.Application -ne $expected -or !$saved.AppDirectory){throw 'Rollback snapshot belongs to another configuration.'}
}
if($RestoreStoppedConfiguration){
    if(!$saved){throw 'Missing protected rollback snapshot.'}
    [Environment]::SetEnvironmentVariable('DB_CONNECTION_STRING',$saved.MachineSql,'Machine')
    Set-ItemProperty -LiteralPath $parameters -Name Application -Value $saved.Application
    Set-ItemProperty -LiteralPath $parameters -Name AppDirectory -Value $saved.AppDirectory
    if($null -eq $saved.AppEnvironmentExtra){
        if((Get-Item -LiteralPath $parameters).GetValueNames() -contains 'AppEnvironmentExtra'){Remove-ItemProperty -LiteralPath $parameters -Name AppEnvironmentExtra}
    }else{
        New-ItemProperty -LiteralPath $parameters -Name AppEnvironmentExtra -Value ([string[]]$saved.AppEnvironmentExtra) -PropertyType MultiString -Force | Out-Null
    }
    Write-Output 'Previous configuration restored. Game service is stopped.'
    return
}
$bytes=$LoginPrivateIp.GetAddressBytes()
if($bytes.Length -ne 4 -or !($bytes[0] -eq 10 -or ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31) -or ($bytes[0] -eq 192 -and $bytes[1] -eq 168))){throw 'Use the private login role IPv4 address.'}
$base=(Get-Item -LiteralPath $ReleaseDirectory).FullName
$newExe=Join-Path $base 'Server.exe'
if($expected -eq $newExe -or ($prior.Application -ne $expected -and !($saved -and $prior.Application -eq $newExe))){throw 'Unexpected current or destination game executable.'}
foreach($file in $newExe,$SqlCredential,$RedisCredential,$IssuerFile){
    if(!([IO.Path]::IsPathRooted($file)) -or !(Test-Path -LiteralPath $file -PathType Leaf)){throw 'A required absolute release, credential or CA file is missing.'}
}
if((Get-FileHash -LiteralPath $IssuerFile -Algorithm SHA256).Hash -ne $IssuerSha256){throw 'Private role issuer fingerprint mismatch.'}
$identity=Get-Item -LiteralPath ("Cert:\LocalMachine\My\"+$GameClientThumbprint)
if(!$identity.HasPrivateKey){throw 'Game client identity needs its matching local private key.'}
$issuer=New-Object Security.Cryptography.X509Certificates.X509Certificate2($IssuerFile)
$chain=New-Object Security.Cryptography.X509Certificates.X509Chain
try{
    $chain.ChainPolicy.RevocationMode='NoCheck' # The pinned internal CA issues without a CRL.
    [void]$chain.ChainPolicy.ApplicationPolicy.Add((New-Object Security.Cryptography.Oid('1.3.6.1.5.5.7.3.2')))
    if(!$chain.Build($identity) -or $chain.ChainElements[$chain.ChainElements.Count-1].Certificate.Thumbprint -ne $issuer.Thumbprint){throw 'Game identity does not chain to the expected trusted client-authentication issuer.'}
}finally{$chain.Dispose();$issuer.Dispose()}
$kept=[string[]]@($prior.AppEnvironmentExtra|Where-Object{$_ -and $_ -notmatch '^(DB_CONNECTION_STRING|ECHODIA_(DB_CONNECTION_DPAPI_FILE|REDIS_CONNECTION(_DPAPI_FILE)?|LOGIN_.*))='})
if(!($kept -match '^ECHODIA_TLS_CERT_THUMBPRINT=[a-fA-F0-9]{40}$')){throw 'Existing public TLS certificate setting is missing.'}
if(!$saved){
    $record=@{ServiceName=$ServiceName;Application=$prior.Application;AppDirectory=$prior.AppDirectory;AppEnvironmentExtra=$prior.AppEnvironmentExtra;MachineSql=[Environment]::GetEnvironmentVariable('DB_CONNECTION_STRING','Machine')}
    $clear=[Text.Encoding]::UTF8.GetBytes(($record|ConvertTo-Json -Compress))
    $temp=Join-Path $rollback.FullName ([guid]::NewGuid().ToString('N')+'.pending')
    try{
        $sealed=[Security.Cryptography.ProtectedData]::Protect($clear,$null,$scope)
        [IO.File]::WriteAllBytes($temp,$sealed)
        [IO.File]::Move($temp,$snapshot)
    }finally{
        [Array]::Clear($clear,0,$clear.Length)
        if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}
    }
}
# NSSM 2.24-101 env.cpp advances past the following entry for a zero-length value.
# An ASCII space overrides inherited settings, preserves the next entry and is ignored by IsNullOrWhiteSpace.
$next=$kept+@(
    'DB_CONNECTION_STRING= ',
    ('ECHODIA_DB_CONNECTION_DPAPI_FILE='+$SqlCredential),
    'ECHODIA_REDIS_CONNECTION= ',
    ('ECHODIA_REDIS_CONNECTION_DPAPI_FILE='+$RedisCredential),
    'ECHODIA_LOGIN_MODE=remote',
    ('ECHODIA_LOGIN_URL=https://'+$LoginPrivateIp.IPAddressToString+':'+$LoginPort),
    ('ECHODIA_LOGIN_CA_FILE='+$IssuerFile),
    ('ECHODIA_LOGIN_CERT_THUMBPRINT='+$GameClientThumbprint)
)
[Environment]::SetEnvironmentVariable('DB_CONNECTION_STRING',$null,'Machine')
Set-ItemProperty -LiteralPath $parameters -Name Application -Value $newExe
Set-ItemProperty -LiteralPath $parameters -Name AppDirectory -Value $base
New-ItemProperty -LiteralPath $parameters -Name AppEnvironmentExtra -Value ([string[]]$next) -PropertyType MultiString -Force | Out-Null
try{
    Start-Service -Name $ServiceName
    for($i=0;$i -lt 50;$i++){
        $app=@(Get-Process -Name Server -ErrorAction SilentlyContinue|Where-Object Path -eq $newExe)
        if($app -and (Get-NetTCPConnection -State Listen -LocalPort $GamePort -ErrorAction SilentlyContinue|Where-Object OwningProcess -in $app.Id)){
            Write-Output 'Private-services game listener ready.'
            return
        }
        Start-Sleep -Seconds 1
    }
    throw 'Game listener readiness timed out.'
}catch{
    Stop-Service -Name $ServiceName -ErrorAction Stop
    throw
}
