$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot '../Switch-PrivateGame.ps1'
$errors=$null
$tokens=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path $path),[ref]$tokens,[ref]$errors)
if($errors.Count){throw $errors[0]}

# Reproduce the target NSSM 2.24-101 UTF-16 block cursor. Only synthetic names and values enter the environment.
# https://github.com/kirillkovalenko/nssm/blob/897c7ad10ba9542b650f256445d953eff908b985/env.cpp#L90-L115
function Read-NssmEntries([string[]]$Entries){
    $block=($Entries -join [char]0)+[char]0+[char]0
    $result=@{}
    for($start=0;$start -lt $block.Length -and $block[$start] -ne [char]0;$start++){
        $cursor=$start
        while($block[$cursor] -ne [char]0 -and $block[$cursor] -ne '='){$cursor++}
        if($block[$cursor] -eq '='){
            $key=$block.Substring($start,$cursor-$start)
            $cursor++
            $end=$block.IndexOf([char]0,$cursor)
            $result[$key]=$block.Substring($cursor,$end-$cursor)
            $cursor++
            while($cursor -lt $block.Length -and $block[$cursor] -ne [char]0){$cursor++}
        }
        $start=$cursor
    }
    return $result
}
$broken=Read-NssmEntries @('LEGACY=','PROTECTED=fixture','FOLLOWING=ok')
if($broken.ContainsKey('PROTECTED') -or $broken.FOLLOWING -ne 'ok'){throw 'Zero-length regression fixture failed.'}

# Evaluate only the $next array, without performing certificate, registry or Windows service operations.
$kept=@(('ECHODIA_TLS_CERT_THUMBPRINT='+('A'*40)),'UNCHANGED=yes')
$SqlCredential='C:\synthetic\sql.dpapi'
$RedisCredential='C:\synthetic\redis.dpapi'
$LoginPrivateIp=[ipaddress]'10.0.0.12'
$LoginPort=8443
$IssuerFile='C:\synthetic\ca.cer'
$GameClientThumbprint='B'*40
$assignment=$ast.Find({param($n) $n -is [System.Management.Automation.Language.AssignmentStatementAst] -and $n.Left.Extent.Text -eq '$next'},$true)
$values=& ([scriptblock]::Create($assignment.Right.Extent.Text))
$parsed=Read-NssmEntries $values
$expected=@{DB_CONNECTION_STRING=' ';ECHODIA_DB_CONNECTION_DPAPI_FILE=$SqlCredential;ECHODIA_REDIS_CONNECTION=' ';ECHODIA_REDIS_CONNECTION_DPAPI_FILE=$RedisCredential;ECHODIA_LOGIN_URL='https://10.0.0.12:8443';UNCHANGED='yes'}
foreach($key in $expected.Keys){if($parsed[$key] -cne $expected[$key]){throw "Setting did not survive NSSM environment parsing: $key"}}
Write-Output 'Zero-length NSSM skip reproduced; six cutover values preserved.'
