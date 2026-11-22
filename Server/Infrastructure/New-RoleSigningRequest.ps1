[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('Database','Login','GameClient')][string]$Role,
      [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$dest=(Resolve-Path -LiteralPath $OutputDirectory).Path
$subj=if($Role -eq 'Database'){$env:COMPUTERNAME}else{'Echodia-'+$Role}
if($subj -notmatch '^[A-Za-z0-9-]+$'){throw 'Invalid computer name'}
$req=Join-Path $dest "$Role.req"
if(Test-Path -LiteralPath $req){throw 'A signing request is already present'}
$inf=Join-Path $dest "$Role.inf"
@"
[Version]
Signature="__WINDOWS_SIGNATURE__"
[NewRequest]
Subject="CN=$subj"
MachineKeySet=TRUE
Exportable=FALSE
KeySpec=1
KeyLength=3072
ProviderName="Microsoft RSA SChannel Cryptographic Provider"
ProviderType=12
HashAlgorithm=sha256
RequestType=PKCS10
Silent=TRUE
"@.Replace('__WINDOWS_SIGNATURE__','$Windows NT$') | Set-Content -Encoding Ascii -LiteralPath $inf
& certreq.exe -new -machine -q $inf $req | Out-Null
if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $req)){throw 'Could not create the machine signing request'}
Write-Output $req
