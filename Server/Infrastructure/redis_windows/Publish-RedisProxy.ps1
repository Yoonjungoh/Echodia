#Requires -RunAsAdministrator
. (Join-Path $PSScriptRoot 'Common.ps1')
if (![Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { throw 'Run through the EchodiaRedisProxy task.' }
$config = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'role.json') | ConvertFrom-Json
$listen = Get-PrivateV4 $config.Redis
$game = Get-PrivateV4 $config.Game
if (!(Get-NetIPAddress -AddressFamily IPv4 -IPAddress $listen -ErrorAction SilentlyContinue)) {throw 'Private cache NIC unavailable.'}
$own = Join-Path $PSScriptRoot 'proxy-owned'
$status = Join-Path $PSScriptRoot 'proxy-status.txt'
$signal = Join-Path $PSScriptRoot 'state\guest-ready.json'
$boot = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime()
$mapping = "$listen/6380"
$reg = 'HKLM:\SYSTEM\CurrentControlSet\Services\PortProxy\v4tov4\tcp'
$hyperVRule = 'Echodia-WSL-Redis-TLS'
$hasHyperVFirewall = [bool](Get-Command Get-NetFirewallHyperVRule -ErrorAction SilentlyContinue)
function Remove-OwnProxy {
    if ($hasHyperVFirewall) {
        $hv = Get-NetFirewallHyperVRule -Name $hyperVRule -ErrorAction SilentlyContinue
        if ($hv) {
            if ($hv.DisplayName -ne 'Echodia WSL Redis TLS') {throw 'Foreign Hyper-V firewall rule uses the expected name.'}
            Remove-NetFirewallHyperVRule -Name $hyperVRule
        }
    }
    if (!(Test-Path -LiteralPath $own)) {return}
    $existing = Get-ItemProperty -LiteralPath $reg -Name $mapping -ErrorAction SilentlyContinue
    if ($existing) {
        $expected = (Get-Content -Raw -LiteralPath $own).Trim()
        if ($existing.$mapping -ne "$expected/6380") {throw 'The current port proxy is owned by a different configuration.'}
        & netsh.exe interface portproxy delete v4tov4 "listenaddress=$listen" listenport=6380 | Out-Null
        Assert-Exit 'Remove prior private proxy'
    }
}
$rule = 'Echodia-Private-Redis-TLS'
$active = $null
try {
    Remove-OwnProxy
    $firewall = Get-NetFirewallRule -Name $rule -ErrorAction SilentlyContinue
    if ($firewall) {
        if ($firewall.Group -ne 'Echodia Redis role') {throw 'Foreign firewall rule uses the expected name.'}
        $firewall | Remove-NetFirewallRule
    }
    New-NetFirewallRule -Name $rule -DisplayName 'Echodia private Redis TLS' -Group 'Echodia Redis role' -Direction Inbound -Protocol TCP -LocalAddress $listen -LocalPort 6380 -RemoteAddress $game -Action Allow -Profile Any | Out-Null
    Set-Service iphlpsvc -StartupType Automatic
    Start-Service iphlpsvc
    [IO.File]::WriteAllText($status,'Waiting for a verified guest from this boot.')
    while ($true) {
        $guest = $null
        $item = Get-Item -LiteralPath $signal -ErrorAction SilentlyContinue
        if ($item -and !($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -and $item.Length -lt 128 -and $item.LastWriteTimeUtc -ge $boot -and
            [Math]::Abs(([DateTime]::UtcNow - $item.LastWriteTimeUtc).TotalSeconds) -lt 45) {
            try { $guest = Get-PrivateV4 ((Get-Content -Raw -LiteralPath $signal | ConvertFrom-Json).Guest) } catch { $guest=$null }
        }
        if ($guest -ne $active) {
            Remove-OwnProxy
            $active=$null
            if ($guest) {
                $selected = @(Find-NetRoute -RemoteIPAddress $guest)
                $route = @($selected | Where-Object { $_.PSObject.Properties['DestinationPrefix'] })
                if ($route.Count -ne 1 -or $route[0].InterfaceAlias -notlike 'vEthernet (WSL*' -or $route[0].NextHop -ne '0.0.0.0') {throw 'The guest is outside the local WSL NAT interface.'}
                if ($hasHyperVFirewall) {
                    $sourceIp = Get-PrivateV4 (($selected | Where-Object { $_.PSObject.Properties['IPAddress'] }).IPAddress)
                    New-NetFirewallHyperVRule -Name $hyperVRule -DisplayName 'Echodia WSL Redis TLS' -Direction Inbound -Action Allow -VMCreatorId '{40E0AC32-46A5-438A-A0B2-2B479E8F2E90}' -Protocol TCP -LocalPorts 6380 -RemoteAddresses $sourceIp -LocalAddresses $guest | Out-Null
                }
                if (!(Test-NetConnection -ComputerName $guest -Port 6380 -InformationLevel Quiet -WarningAction SilentlyContinue)) {throw 'WSL guest TCP port is unreachable from the Windows proxy.'}
                if (Get-ItemProperty -LiteralPath $reg -Name $mapping -ErrorAction SilentlyContinue) {throw 'A different process owns this port proxy.'}
                [IO.File]::WriteAllText($own,$guest)
                & netsh.exe interface portproxy add v4tov4 "listenaddress=$listen" listenport=6380 "connectaddress=$guest" connectport=6380 | Out-Null
                Assert-Exit 'Publish private TLS port'
                $active=$guest
                [IO.File]::WriteAllText($status,'Ready: verified WSL guest mapped on private TCP 6380 for game IP.')
            } else {
                [IO.File]::WriteAllText($status,'Waiting for a fresh guest health signal.')
            }
        }
        Start-Sleep -Seconds 5
    }
} catch {
    [IO.File]::WriteAllText($status,('Error: ' + $_.Exception.Message))
    throw
} finally { Remove-OwnProxy }
