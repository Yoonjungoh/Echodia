"""Emit a CloudFormation template for the three Windows hosts beside the game EC2."""
import json


def ref(name):
    return {"Ref": name}


def attr(resource, name):
    return {"Fn::GetAtt": [resource, name]}


# Only the public SSH host key is returned via the short-lived CloudFormation signal URL.
BOOT = r"""<powershell>
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$signalUrl = '${SignalHandle}'
try {
    $deadline = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(80)
    $stop = New-ScheduledTaskAction -Execute 'shutdown.exe' -Argument '/s /t 15 /d p:0:0'
    Register-ScheduledTask -TaskName 'EchodiaProvisioningStop' -Trigger $deadline -Action $stop -User 'SYSTEM' -Force | Out-Null
    $sshd = Join-Path $env:WINDIR 'System32\OpenSSH\sshd.exe'
    if (!(Test-Path $sshd)) { Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 | Out-Null }
    Set-Service sshd -StartupType Automatic
    if (!(Test-Path "$env:ProgramData\ssh\sshd_config")) { Start-Service sshd; Stop-Service sshd }
    $dir = Join-Path $env:ProgramData 'ssh'
    $config = Join-Path $dir 'sshd_config'
    $original = Get-Content -Raw -LiteralPath $config
    if (!$original.StartsWith('# Echodia SSH')) {
        $policy = "# Echodia SSH`r`nAuthenticationMethods publickey`r`nPasswordAuthentication no`r`nPubkeyAuthentication yes`r`n"
        [IO.File]::WriteAllText($config, $policy + $original)
    }
    $keyfile = Join-Path $dir 'administrators_authorized_keys'
    [IO.File]::WriteAllText($keyfile, '${AdminPublicKey}' + [Environment]::NewLine)
    icacls.exe $keyfile /inheritance:r /grant:r '*S-1-5-18:F' '*S-1-5-32-544:F' | Out-Null
    if ($LASTEXITCODE) { throw 'Key ACL failed' }
    Get-NetFirewallRule -Name OpenSSH-Server-In-TCP -ErrorAction SilentlyContinue | Disable-NetFirewallRule
    New-NetFirewallRule -Name EchodiaAdminSSH -DisplayName 'Echodia administrative SSH' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 22 -RemoteAddress '${AdminCidr}' | Out-Null
    & $sshd -t
    if ($LASTEXITCODE) { throw 'Invalid sshd configuration' }
    Restart-Service sshd
    $key = (Get-Content -Raw (Join-Path $dir 'ssh_host_ed25519_key.pub')).Trim().Split(' ')
    if ($key[0] -ne 'ssh-ed25519') { throw 'Missing SSH host public key' }
    $signal = @{Status='SUCCESS'; Reason='SSH ready'; UniqueId='host'; Data=($key[0]+' '+$key[1])}
} catch {
    $signal = @{Status='FAILURE'; Reason='Windows bootstrap failed'; UniqueId='host'; Data=''}
}
$body = $signal | ConvertTo-Json -Compress
Invoke-WebRequest -UseBasicParsing -Method Put -Uri $signalUrl -ContentType '' -Body $body | Out-Null
</powershell>"""


def build_template():
    parameters = {
        "VpcId": {"Type": "AWS::EC2::VPC::Id"},
        "SubnetId": {"Type": "AWS::EC2::Subnet::Id"},
        "GameGroup": {"Type": "AWS::EC2::SecurityGroup::Id"},
        "ImageId": {"Type": "AWS::EC2::Image::Id"},
        "AdminCidr": {"Type": "String", "AllowedPattern": r"(?:\d{1,3}\.){3}\d{1,3}/32"},
        "AdminPublicKey": {"Type": "String", "AllowedPattern": r"ssh-ed25519 [A-Za-z0-9+/=]+"},
    }
    resources = {}
    outputs = {}
    for role, machine in (("Login", "t3.small"), ("Database", "t3.medium"), ("Cache", "m7i-flex.large")):
        group, launch, host, handle, signal = (role + suffix for suffix in ("Group", "Template", "Host", "Handle", "Ready"))
        tags = [{"Key": "Name", "Value": "Echodia-" + role}, {"Key": "EchodiaRole", "Value": role}]
        resources[group] = {"Type": "AWS::EC2::SecurityGroup", "Properties": {
            "GroupDescription": "Echodia " + role + " restricted ingress", "VpcId": ref("VpcId"), "Tags": tags,
            "SecurityGroupIngress": [{"IpProtocol": "tcp", "FromPort": 22, "ToPort": 22, "CidrIp": ref("AdminCidr")}]}}
        resources[handle] = {"Type": "AWS::CloudFormation::WaitConditionHandle"}
        data = {"ImageId": ref("ImageId"), "InstanceType": machine,
                "InstanceInitiatedShutdownBehavior": "stop",
                "MetadataOptions": {"HttpTokens": "required", "HttpEndpoint": "enabled"},
                "BlockDeviceMappings": [{"DeviceName": "/dev/sda1",
                    "Ebs": {"VolumeSize": 30, "VolumeType": "gp3", "Encrypted": True, "DeleteOnTermination": True}}],
                "NetworkInterfaces": [{"DeviceIndex": 0, "AssociatePublicIpAddress": True,
                                       "SubnetId": ref("SubnetId"), "Groups": [ref(group)]}],
                "TagSpecifications": [{"ResourceType": "volume", "Tags": tags}],
                "UserData": {"Fn::Base64": {"Fn::Sub": [BOOT, {"SignalHandle": ref(handle)}]}}}
        if role == "Cache":
            data["CpuOptions"] = {"NestedVirtualization": "enabled"}
        else:
            data["CreditSpecification"] = {"CpuCredits": "standard"}
        resources[launch] = {"Type": "AWS::EC2::LaunchTemplate", "Properties": {"LaunchTemplateData": data}}
        resources[host] = {"Type": "AWS::EC2::Instance", "DeletionPolicy": "Retain", "UpdateReplacePolicy": "Retain",
            "Properties": {"LaunchTemplate": {"LaunchTemplateId": ref(launch), "Version": attr(launch, "LatestVersionNumber")}, "Tags": tags}}
        resources[signal] = {"Type": "AWS::CloudFormation::WaitCondition", "DependsOn": host,
            "Properties": {"Handle": ref(handle), "Timeout": "1200", "Count": 1}}
        outputs[role + "Id"] = {"Value": ref(host)}
        outputs[role + "PrivateIp"] = {"Value": attr(host, "PrivateIp")}
        outputs[role + "HostKey"] = {"Value": attr(signal, "Data")}

    for name, dest, source, port in (("GameToLogin", "LoginGroup", "GameGroup", 8443),
            ("GameToSql", "DatabaseGroup", "GameGroup", 1433), ("LoginToSql", "DatabaseGroup", "LoginGroup", 1433),
            ("GameToRedis", "CacheGroup", "GameGroup", 6380)):
        resources[name] = {"Type": "AWS::EC2::SecurityGroupIngress", "Properties": {
            "GroupId": ref(dest), "SourceSecurityGroupId": ref(source), "IpProtocol": "tcp",
            "FromPort": port, "ToPort": port, "Description": name}}

    return {"AWSTemplateFormatVersion": "2010-09-09", "Description": "Private service links and restricted SSH for Echodia.",
            "Parameters": parameters, "Resources": resources, "Outputs": outputs}


if __name__ == "__main__":
    print(json.dumps(build_template(), indent=2))
