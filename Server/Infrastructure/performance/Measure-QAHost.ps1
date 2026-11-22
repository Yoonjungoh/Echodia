[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Start','Stop')][string]$Action,
    [Parameter(Mandatory)][ValidatePattern('^(single|split)-[a-z0-9]{1,12}$')][string]$Series,
    [Parameter(Mandatory)][ValidateSet('Game','Login','Database','Cache')][string]$Role
)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$root=Join-Path $env:ProgramData 'EchodiaPerf\Counters'
[void][IO.Directory]::CreateDirectory($root)
$name="EchodiaPerf-$Series-$Role"
$base=Join-Path $root $name
$logman=Join-Path $env:WINDIR 'System32\logman.exe'
if($Action -eq 'Start'){
    if(@(Get-ChildItem -Path "$base*.blg" -File -ErrorAction SilentlyContinue).Count){throw 'Existing profile recording would be overwritten'}
    $paths=@('\Processor(_Total)\% Processor Time','\Memory\Available MBytes',
             '\Memory\Committed Bytes','\System\Context Switches/sec','\System\Processor Queue Length',
             '\PhysicalDisk(_Total)\Disk Reads/sec','\PhysicalDisk(_Total)\Disk Writes/sec',
             '\PhysicalDisk(_Total)\Avg. Disk sec/Transfer','\TCPv4\Segments Retransmitted/sec')
    # Wildcards match duplicate process instances when the QA process starts later.
    $processNames=switch($Role){
        Game {@('Server','EchodiaLogin','LoginService','sqlservr','memurai')}
        Login {@('LoginService','EchodiaLogin')}
        Database {@('sqlservr')}
        Cache {@('vmmemWSL','vmmem','wslhost')}
    }
    $present=Get-Process | Group-Object ProcessName -AsHashTable -AsString
    foreach($p in $processNames){
        if($present.ContainsKey($p)){
            foreach($field in 'ID Process','% Processor Time','Working Set - Private','Private Bytes'){
                $paths+="\Process($p*)\$field"
            }
        }
    }
    [IO.File]::WriteAllLines("$base.counters.txt",$paths,[Text.Encoding]::ASCII)
    & $logman create counter $name -cf "$base.counters.txt" -o "$base.blg" -f bin -si 00:00:01 -rf 00:20:00 | Out-Null
    if($LASTEXITCODE){throw 'Performance counter set creation failed'}
    & $logman start $name | Out-Null
    if($LASTEXITCODE){& $logman delete $name|Out-Null;throw 'Performance counter start failed'}
    $os=Get-CimInstance Win32_OperatingSystem
    $names=@($processNames|ForEach-Object {Get-Process $_ -ErrorAction SilentlyContinue | ForEach-Object {@{name=$_.ProcessName;pid=$_.Id}}})
    [ordered]@{role=$Role;series=$Series;startUtc=[DateTime]::UtcNow.ToString('o');timezone=[TimeZoneInfo]::Local.Id;
        sampleSeconds=1;vcpus=[Environment]::ProcessorCount;physicalMemoryMiB=[math]::Round($os.TotalVisibleMemorySize/1024);
        processIds=$names;counters=$paths}|ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 "$base.meta.json"
    "Recording $name"
    return
}
& $logman stop $name | Out-Null
$logs=@(Get-ChildItem -Path "$base*.blg" -File -ErrorAction SilentlyContinue)
if($logs.Count -ne 1){throw 'Expected one native counter log'}
if($logs[0].FullName -ne "$base.blg"){Move-Item -LiteralPath $logs[0].FullName -Destination "$base.blg"}
& $logman delete $name | Out-Null
& "$env:WINDIR\System32\relog.exe" "$base.blg" -f csv -o "$base.csv" -y | Out-Null
if($LASTEXITCODE -or !(Test-Path "$base.csv")){throw 'Could not export native BLG'}
$lines=@(Get-Content -LiteralPath "$base.csv").Count-1
if($lines -lt 2){throw 'Insufficient counter samples'}
[pscustomobject]@{name=$name;samples=$lines;bytes=(Get-Item "$base.blg").Length;endUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Compress
