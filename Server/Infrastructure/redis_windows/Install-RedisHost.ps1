#Requires -RunAsAdministrator
param(
    [Parameter(Mandatory)][string]$RedisPrivateIp,
    [Parameter(Mandatory)][string]$GamePrivateIp,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$RedisAclSha256,
    [switch]$RestartIfNeeded
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$redis = Get-PrivateV4 $RedisPrivateIp
$game = Get-PrivateV4 $GamePrivateIp
if ($redis -eq $game -or !(Get-NetIPAddress -AddressFamily IPv4 -IPAddress $redis -ErrorAction SilentlyContinue)) {
    throw 'Expected separate game and cache hosts and the local private cache address.'
}
$dir = Join-Path $env:ProgramData 'EchodiaRedis'
Set-ProtectedDirectory $dir ''
$path = Join-Path $dir 'role.json'
$name = 'EchodiaRedisSvc'
$account = "$env:COMPUTERNAME\$name"
$svc = Get-LocalUser -Name $name -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $path) {
    $prev = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($prev.Redis -ne $redis -or $prev.Game -ne $game -or $prev.Verifier -ne $RedisAclSha256 -or !$svc -or $prev.ServiceSid -ne $svc.SID.Value) {
        throw 'This role configuration differs. Coordinate account, endpoint or credential rotation before updating it.'
    }
} elseif ($svc) { throw 'A different installation already owns the proposed Windows user name.' }
function New-RolePassword {
    $buf = New-Object byte[] 48
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($buf); return ([Convert]::ToBase64String($buf) + '!aA3') }
    finally { $rng.Dispose(); [Array]::Clear($buf,0,$buf.Length) }
}
if (!$svc) {
    $password = New-RolePassword
    $svc = New-LocalUser -Name $name -Password (ConvertTo-SecureString $password -AsPlainText -Force) -AccountNeverExpires -PasswordNeverExpires -UserMayNotChangePassword -Description 'Echodia dedicated standard user for WSL Redis'
}
$sid = $svc.SID.Value
& (Join-Path $PSScriptRoot 'Grant-RedisBatchLogon.ps1') -ServiceSid $sid
Set-ProtectedDirectory $dir $sid
$state = Join-Path $dir 'state'
Set-ProtectedDirectory $state $sid -Modify
$config = @{ Redis=$redis; Game=$game; Verifier=$RedisAclSha256.ToLowerInvariant(); ServiceSid=$sid }
[IO.File]::WriteAllText($path,($config | ConvertTo-Json -Compress))
foreach ($file in @('Common.ps1','Run-RedisHost.ps1','Publish-RedisProxy.ps1','guest.sh')) {
    $src = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot $file)).Path
    $dest = Join-Path $dir $file
    if ($src -ne $dest) { Copy-Item -LiteralPath $src -Destination $dest -Force }
}

$reboot = $false
foreach ($feature in @('Microsoft-Windows-Subsystem-Linux','VirtualMachinePlatform')) {
    $mode = (Get-WindowsOptionalFeature -Online -FeatureName $feature).State
    if ($mode -eq 'EnablePending') { $reboot=$true; continue }
    if ($mode -ne 'Enabled') {
        $result = Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart
        $reboot = $reboot -or $result.RestartNeeded
    }
}
# Install the machine MSI without opening a WSL session as LocalSystem. Bootstrap must also work via SSM.
$engine = Join-Path $env:ProgramFiles 'WSL\wsl.exe'
if (!(Test-Path -LiteralPath $engine)) {
    $msi = Join-Path $dir 'wsl.3.0.5.0.x64.msi'
    & curl.exe --fail --location --proto '=https' --retry 3 --output $msi 'https://github.com/microsoft/WSL/releases/download/3.0.5/wsl.3.0.5.0.x64.msi'
    Assert-Exit 'Download Microsoft WSL MSI'
    $signature = Get-AuthenticodeSignature -FilePath $msi
    if ((Get-FileHash $msi -Algorithm SHA256).Hash -ne '2313efc5873e3f8552f42e77f950fabd913c0289078fa89d2100f3421a525eaa' -or
        $signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(?:^|, )O=Microsoft Corporation(?:,|$)') {
        throw 'Microsoft WSL package verification failed.'
    }
    $install = Start-Process msiexec.exe -ArgumentList ('/i "' + $msi + '" /qn /norestart') -Wait -PassThru -WindowStyle Hidden
    if ($install.ExitCode -notin @(0,3010)) {throw "WSL MSI exited $($install.ExitCode)."}
    $reboot = $reboot -or $install.ExitCode -eq 3010
    Remove-Item -LiteralPath $msi
}
$trigger = New-ScheduledTaskTrigger -AtStartup
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -StartWhenAvailable -MultipleInstances IgnoreNew -RestartCount 5 -RestartInterval (New-TimeSpan -Minutes 1)
$exe = "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
foreach ($role in @('EchodiaRedis','EchodiaRedisProxy')) {
    $guest = $role -eq 'EchodiaRedis'
    $script = if ($guest) { 'Run-RedisHost.ps1' } else { 'Publish-RedisProxy.ps1' }
    $action = New-ScheduledTaskAction -Execute $exe -Argument ('-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File "' + (Join-Path $dir $script) + '"')
    $old = Get-ScheduledTask -TaskName $role -ErrorAction SilentlyContinue
    if ($old) {
        $owners = if ($guest) { @($sid,$account,$name) } else { @('S-1-5-18','SYSTEM','NT AUTHORITY\SYSTEM') }
        if ($old.Actions.Count -ne 1 -or $old.Actions[0].Execute -ne $action.Execute -or $old.Actions[0].Arguments -ne $action.Arguments -or $old.Principal.UserId -notin $owners) {
            throw "Another configuration owns $role."
        }
        Set-ScheduledTask -TaskName $role -Action $action -Trigger $trigger -Settings $settings | Out-Null
    } elseif ($guest) {
        if (!(Get-Variable password -ErrorAction SilentlyContinue)) {
            $password = New-RolePassword
            Set-LocalUser -Name $name -Password (ConvertTo-SecureString $password -AsPlainText -Force)
        }
        Register-ScheduledTask -TaskName $role -Action $action -Trigger $trigger -Settings $settings -User $account -Password $password -RunLevel Limited | Out-Null
        Remove-Variable password
    } else {
        $system = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
        Register-ScheduledTask -TaskName $role -Action $action -Trigger $trigger -Settings $settings -Principal $system | Out-Null
    }
}
if ($reboot) {
    if ($RestartIfNeeded) { Restart-Computer -Force; return }
    Write-Output 'Restart this cache EC2 to complete Windows component installation; the startup tasks then continue.'
} else {
    Start-ScheduledTask -TaskName EchodiaRedisProxy
    Start-ScheduledTask -TaskName EchodiaRedis
}
