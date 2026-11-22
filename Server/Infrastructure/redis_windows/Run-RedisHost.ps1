. (Join-Path $PSScriptRoot 'Common.ps1')
$ProgressPreference = 'SilentlyContinue'
$config = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'role.json') | ConvertFrom-Json
if ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value -ne $config.ServiceSid) {throw 'Run via the dedicated standard-user task.'}
$listen = Get-PrivateV4 $config.Redis
$game = Get-PrivateV4 $config.Game
if ($config.Verifier -notmatch '^[0-9a-f]{64}$') {throw 'Invalid cache verifier.'}
if ($PSScriptRoot -ine 'C:\ProgramData\EchodiaRedis') {throw 'Install the role in its dedicated ProgramData directory.'}
$state = Join-Path $PSScriptRoot 'state'
$status = Join-Path $state 'status.txt'
$signal = Join-Path $state 'guest-ready.json'
$wsl = Join-Path $env:ProgramFiles 'WSL\wsl.exe'
$linux = 'EchodiaRedis'
$linuxScript = '/mnt/c/ProgramData/EchodiaRedis/guest.sh'
try {
    if (Test-Path -LiteralPath $signal) {Remove-Item -LiteralPath $signal}
    [IO.File]::WriteAllText($status,'Preparing official WSL distribution')
    $distros = @(& $wsl --list --quiet | ForEach-Object { ($_ -replace [char]0,'').Trim() })
    $vhd = Join-Path $state 'distribution'
    if ($distros -notcontains $linux) {
        $image = Join-Path $state 'ubuntu-24.04.5-wsl-amd64.wsl'
        $url = 'https://releases.ubuntu.com/24.04.5/ubuntu-24.04.5-wsl-amd64.wsl'
        $sha = 'bb415d824822c4b878125729af451a5d18fb13d1cf5cbed9a7393ad64ac6039e'
        if (!(Test-Path -LiteralPath $image) -or (Get-FileHash $image -Algorithm SHA256).Hash -ne $sha) {
            & curl.exe --fail --location --proto '=https' --retry 3 --output $image $url
            Assert-Exit 'Download pinned Canonical WSL image'
        }
        if ((Get-FileHash -LiteralPath $image -Algorithm SHA256).Hash -ne $sha) {throw 'Ubuntu signature manifest hash mismatch.'}
        & $wsl --import $linux $vhd $image --version 2 | Out-Null
        Assert-Exit 'Import verified Ubuntu into the dedicated WSL2 account'
        Remove-Item -LiteralPath $image
    }
    $registered = @(Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss' |
        Get-ItemProperty | Where-Object { $_.PSObject.Properties['DistributionName'] -and $_.DistributionName -eq $linux })
    if ($registered.Count -ne 1 -or ($registered[0].BasePath -replace '^\\\\\?\\','').TrimEnd('\') -ine $vhd -or $registered[0].Version -ne 2) {
        throw 'Expected the dedicated user-owned WSL2 distribution in the protected state directory.'
    }
    & $wsl --distribution $linux --user root --exec /bin/bash $linuxScript prepare $listen $config.Verifier
    Assert-Exit 'Prepare Redis, CSR and ACL'
    if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ca.pem')) -or !(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'server.pem'))) {
        [IO.File]::WriteAllText($status,'Waiting for signed server.pem and ca.pem. Copy the public CSR from state\server.csr, return both PEM files, then start EchodiaRedis again.')
        return
    }
    $launchArgs = @('--distribution',$linux,'--user','root','--exec','/bin/bash',$linuxScript,'serve',$listen,$config.Verifier)
    $worker = Start-Process -WindowStyle Hidden -PassThru -FilePath $wsl -ArgumentList $launchArgs -RedirectStandardOutput (Join-Path $state 'redis.log') -RedirectStandardError (Join-Path $state 'redis.err')
    $ready = $false
    for ($i=0; $i -lt 45 -and !$worker.HasExited; $i++) {
        & $wsl --distribution $linux --user root --exec /bin/bash $linuxScript health $listen 2>$null
        if ($LASTEXITCODE -eq 0) {$ready=$true; break}
        Start-Sleep -Seconds 1
    }
    if (!$ready) {throw 'Redis private TLS listener did not become ready.'}
    $guestIp = Get-PrivateV4 ((& $wsl --distribution $linux --user root --exec /bin/bash $linuxScript ip $game | Out-String).Trim())
    Assert-Exit 'Read guest IP'
    [IO.File]::WriteAllText($signal,(@{Guest=$guestIp} | ConvertTo-Json -Compress))
    [IO.File]::WriteAllText($status,'Guest TLS and ACL check passed. See proxy-status.txt for Windows publication.')
    while (!$worker.WaitForExit(10000)) { [IO.File]::SetLastWriteTimeUtc($signal,[DateTime]::UtcNow) }
    throw 'Redis foreground process stopped.'
} catch {
    [IO.File]::WriteAllText($status,('Error: ' + $_.Exception.Message))
    throw
} finally {
    if (Test-Path -LiteralPath $signal) {Remove-Item -LiteralPath $signal}
    if ((Get-Variable worker -ErrorAction SilentlyContinue) -and !$worker.HasExited) {
        & $wsl --terminate $linux | Out-Null
    }
}
