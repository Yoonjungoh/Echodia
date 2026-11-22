Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Get-PrivateV4([string]$Value) {
    $ip = [Net.IPAddress]::Parse($Value)
    $octets = $ip.GetAddressBytes()
    if ($octets.Length -ne 4 -or !($octets[0] -eq 10 -or
        ($octets[0] -eq 172 -and $octets[1] -ge 16 -and $octets[1] -le 31) -or
        ($octets[0] -eq 192 -and $octets[1] -eq 168))) {
        throw 'Use the private IPv4 address assigned to the EC2 network interface.'
    }
    return $ip.ToString()
}

function Set-ProtectedDirectory([string]$Path, [string]$ReaderSid, [switch]$Modify) {
    $dir = New-Item -Path $Path -ItemType Directory -Force
    if ($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse points are not supported.' }
    $acl = New-Object Security.AccessControl.DirectorySecurity
    $sddl = 'D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)'
    if ($ReaderSid -and $ReaderSid -notin @('S-1-5-18','S-1-5-32-544')) {
        $mask = if ($Modify) { '0x1301bf' } else { '0x1200a9' }
        $sddl += "(A;OICI;$mask;;;$ReaderSid)"
    }
    $acl.SetSecurityDescriptorSddlForm($sddl)
    Set-Acl -LiteralPath $dir.FullName -AclObject $acl
}

function Assert-Exit([string]$Step) {
    if ($LASTEXITCODE -ne 0) { throw "$Step failed with exit code $LASTEXITCODE." }
}
