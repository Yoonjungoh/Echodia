[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MediaPath,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{40}$')][string]$ServerCertificate,
    [Parameter(Mandatory)][ipaddress]$GameIp,
    [Parameter(Mandatory)][ipaddress]$LoginIp
)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$media=Get-Item -LiteralPath $MediaPath
if((Get-FileHash $media.FullName).Hash -ne '2E61C8BBDE6021F9026C54AD9DB4BBB1227E68761D4C00A6A50A2C70FE7AFE05'){
    throw 'Unexpected SQL Server 2022 Express media'
}
$signature=Get-AuthenticodeSignature $media.FullName
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=Microsoft Corporation'){throw 'Invalid Microsoft signature'}
$certificate=Get-Item -LiteralPath ('Cert:\LocalMachine\My\'+$ServerCertificate)
if(!$certificate.HasPrivateKey -or $certificate.NotAfter -le [DateTime]::Now){throw 'SQL TLS identity unavailable'}
$svcName='MSSQL$SQLEXPRESS'
if(!(Get-Service -Name $svcName -ErrorAction SilentlyContinue)){
    $extract=Join-Path $env:ProgramData 'Echodia\SqlSetupMedia'
    [void](New-Item -Path $extract -ItemType Directory -Force)
    $process=Start-Process $media.FullName -ArgumentList @('/Q',('/X:'+$extract)) -Wait -PassThru -WindowStyle Hidden
    if($process.ExitCode -ne 0){throw 'Failed to extract SQL media'}
    $setup=Get-Item -LiteralPath (Join-Path $extract 'SETUP.EXE')
    if((Get-AuthenticodeSignature $setup.FullName).Status -ne 'Valid'){throw 'SQL Setup signature invalid'}
    $setupArgs=@('/Q','/ACTION=Install','/FEATURES=SQLENGINE','/INSTANCENAME=SQLEXPRESS',
        '/IACCEPTSQLSERVERLICENSETERMS','/SUPPRESSPRIVACYSTATEMENTNOTICE','/TCPENABLED=0',
        '/NPENABLED=0','/BROWSERSVCSTARTUPTYPE=Disabled','/SQLSVCSTARTUPTYPE=Automatic',
        ('/SQLSYSADMINACCOUNTS="'+[Security.Principal.WindowsIdentity]::GetCurrent().Name+'"'),'/UPDATEENABLED=True')
    $process=Start-Process $setup.FullName -ArgumentList $setupArgs -Wait -PassThru -WindowStyle Hidden
    if($process.ExitCode -notin 0,3010){throw "SQL Setup exit $($process.ExitCode)"}
}
$id=(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL').'SQLEXPRESS'
if(!$id -or $id -notlike 'MSSQL16.*'){throw 'Unexpected instance version'}
$dbRoot="HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$id\MSSQLServer"
$network="$dbRoot\SuperSocketNetLib"
$keyContainer=$certificate.PrivateKey.CspKeyContainerInfo
if(!$keyContainer.MachineKeyStore -or $keyContainer.KeyNumber -ne 'Exchange'){throw 'SQL TLS requires a machine exchange key'}
$keyPath=Join-Path "$env:ProgramData\Microsoft\Crypto\RSA\MachineKeys" $keyContainer.UniqueKeyContainerName
icacls.exe $keyPath /grant ("NT SERVICE\"+$svcName+':R') | Out-Null
if($LASTEXITCODE){throw 'SQL certificate key ACL failed'}
Set-ItemProperty $dbRoot -Name LoginMode -Value 2
Set-ItemProperty $network -Name Certificate -Value $certificate.Thumbprint.ToLowerInvariant()
Set-ItemProperty $network -Name ForceEncryption -Value 1
Set-ItemProperty "$network\Tcp\IPAll" -Name TcpDynamicPorts -Value ''
Set-ItemProperty "$network\Tcp\IPAll" -Name TcpPort -Value '1433'
Set-ItemProperty "$network\Tcp" -Name Enabled -Value 1
$allowed=@($GameIp.IPAddressToString,$LoginIp.IPAddressToString)
if($allowed[0] -eq $allowed[1] -or @($allowed | Where-Object { $_ -notlike '172.31.*' }).Count){throw 'Expected two distinct project VPC source addresses'}
$rule=Get-NetFirewallRule -Name EchodiaPrivateSql -ErrorAction SilentlyContinue
if($rule) {
    $rule | Set-NetFirewallRule -Direction Inbound -Action Allow -Enabled True -Protocol TCP -LocalPort 1433 -RemoteAddress $allowed | Out-Null
} else {
    New-NetFirewallRule -Name EchodiaPrivateSql -DisplayName 'Echodia SQL from login and game' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 1433 -RemoteAddress $allowed | Out-Null
}
Restart-Service $svcName
(Get-Service $svcName).WaitForStatus('Running',[TimeSpan]::FromSeconds(60))
'SQL Express configured for private TLS'
