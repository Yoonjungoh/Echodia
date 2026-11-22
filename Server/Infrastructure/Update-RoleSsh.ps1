[CmdletBinding()]
param([string]$Profile='echodia-deploy',[string]$Stack='Echodia-Windows-Roles-1122')
$ErrorActionPreference='Stop'
$root=Join-Path $env:LOCALAPPDATA 'Echodia'
$target=Get-Content -Raw -LiteralPath (Join-Path $root 'aws-target.json') | ConvertFrom-Json
$aws=(Get-Command aws -ErrorAction SilentlyContinue).Source
if(!$aws){$aws=Join-Path $env:ProgramFiles 'Amazon/AWSCLIV2/aws.exe'}
function Query([string[]]$Request){
    $json=& $aws --profile $Profile --region $target.region --no-cli-pager @Request --output json
    if($LASTEXITCODE){throw 'AWS inventory query failed'}
    $json|ConvertFrom-Json
}
if((Query @('sts','get-caller-identity')).Account -ne $target.accountId){throw 'AWS account mismatch'}
$original=(Query @('ec2','describe-instances','--instance-ids',$target.instanceId)).Reservations[0].Instances[0]
$stackInfo=(Query @('cloudformation','describe-stacks','--stack-name',$Stack)).Stacks[0]
$outputs=@{};foreach($o in $stackInfo.Outputs){$outputs[$o.OutputKey]=$o.OutputValue}
$nodes=@{}; $keys=@()
foreach($role in 'Login','Database','Cache'){
    $id=$outputs[$role+'Id']
    $key=(ConvertFrom-Json $outputs[$role+'HostKey']).host
    if($key -notmatch '^ssh-ed25519 [A-Za-z0-9+/=]+$' -or $id -notmatch '^i-[0-9a-f]+$'){throw 'Invalid stack SSH identity'}
    $i=(Query @('ec2','describe-instances','--instance-ids',$id)).Reservations[0].Instances[0]
    if($i.VpcId -ne $original.VpcId -or $i.PrivateIpAddress -ne $outputs[$role+'PrivateIp']){throw 'Instance differs from the stack network'}
    $nodes[$role]=$i; $keys+="$id $key"
}
$nodes['Game']=$original
foreach($role in 'Game','Login','Database','Cache'){"$role "+$nodes[$role].State.Name}
if(@($nodes.Values|Where-Object {$_.State.Name -ne 'running' -or !$_.PublicIpAddress}).Count){return}
$pinFile=Join-Path $root 'role_known_hosts'
[IO.File]::WriteAllLines($pinFile,[string[]]$keys,[Text.Encoding]::ASCII)
$settings=@()
foreach($role in 'Game','Login','Database','Cache'){
    $i=$nodes[$role]
    $settings+="Host Echodia-$role","    HostName $($i.PublicIpAddress)"
    $known=if($role -eq 'Game'){Join-Path $root 'known_hosts'}else{$pinFile}
    $settings+='    UserKnownHostsFile "'+($known -replace '\\','/')+'"'
    if($role -ne 'Game'){$settings+="    HostKeyAlias $($i.InstanceId)"}
}
$settings+='Host *','    User Administrator','    IdentitiesOnly yes','    StrictHostKeyChecking yes','    BatchMode yes','    ForwardAgent no','    IdentityFile ~/.ssh/id_ed25519'
$path=Join-Path $root 'role_ssh_config'
[IO.File]::WriteAllLines($path,[string[]]$settings,[Text.Encoding]::UTF8)
"SSH config: $path"
