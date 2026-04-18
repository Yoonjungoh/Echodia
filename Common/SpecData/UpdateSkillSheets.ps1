
param([string]$XlsxPath = "$PSScriptRoot\EchodiaSpecData.xlsx")

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# ── Build worksheet XML from 2D array ─────────────────────────────────────────
function Build-SheetXml {
    param([object[][]]$rows)
    $sb = [System.Text.StringBuilder]::new()
    $sb.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>') | Out-Null
    $sb.Append('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>') | Out-Null
    $rowIdx = 1
    foreach ($row in $rows) {
        $sb.Append("<row r=`"$rowIdx`">") | Out-Null
        $colIdx = 1
        foreach ($cell in $row) {
            $col = Get-ColLetter $colIdx
            $ref = "$col$rowIdx"
            if ($cell -eq $null) {
                # 빈 셀 skip
            } elseif ($cell -is [int] -or $cell -is [double] -or $cell -is [float] -or $cell -is [long]) {
                $sb.Append("<c r=`"$ref`"><v>$cell</v></c>") | Out-Null
            } else {
                $esc = [System.Security.SecurityElement]::Escape($cell.ToString())
                $sb.Append("<c r=`"$ref`" t=`"inlineStr`"><is><t>$esc</t></is></c>") | Out-Null
            }
            $colIdx++
        }
        $sb.Append("</row>") | Out-Null
        $rowIdx++
    }
    $sb.Append('</sheetData></worksheet>') | Out-Null
    return $sb.ToString()
}

function Get-ColLetter {
    param([int]$col)
    $s = ""
    while ($col -gt 0) {
        $col--
        $s = [char](65 + ($col % 26)) + $s
        $col = [int]($col / 26)
    }
    return $s
}

# ── Sheet data ─────────────────────────────────────────────────────────────────
# Korean name strings built from char codes to avoid script encoding issues
$nameMagic    = -join [char[]]@(47588,51649,48120,49324,51068)   # 매직미사일
$nameLightning = -join [char[]]@(48264,44060,52293,46684,47553)  # 번개채널링

$sheetData = [ordered]@{

    "sheet14" = @(  # Skill
        @("int","enum(SkillType)","string","enum(SkillCategoryType)","enum(SkillActivationType)","List<enum>(CastDirectionType)","float","enum(SkillTargetingType)","int","int","List<enum>(PlayerJobType)","float","float","List<int>","List<float>","int"),
        @("Id","SkillType","Name","SkillCategoryType","SkillActivationType","CastDirections","CastRange","TargetingType","MaxTargets","RequiredLevel","RequiredJobs","CoolTime","CastTime","SkillActionIds","CastHalfAngles","ChannelTickIntervalMs"),
        @([int]700001,"MagicMissile",$nameMagic,"Attack","Instant","{Front}",[double]0.0,"Enemy",[int]1,[int]1,"{Mage}",[double]5.0,[double]0.0,"{1}","{45}",[int]0),
        @([int]700002,"LightningChannel",$nameLightning,"Attack","Channeling","{Sphere}",[double]5.0,"Enemy",[int]3,[int]1,"{Mage}",[double]10.0,[double]3.0,"{2}","{0}",[int]1000)
    )

    "sheet15" = @(  # SkillCost
        @("int","int","enum(SkillCostType)","int","int"),
        @("Id","SkillId","CostType","Amount","ItemId"),
        @([int]1,[int]700001,"Mp",[int]30,[int]0),
        @([int]2,[int]700002,"Mp",[int]50,[int]0)
    )

    "sheet16" = @(  # SkillAction
        @("int","int","enum(SkillActionType)","int","int"),
        @("Id","SkillId","ActionType","DelayMs","SkillDetailId"),
        @([int]1,[int]700001,"SpawnProjectile",[int]0,[int]1),
        @([int]2,[int]700002,"Damage",[int]0,[int]2)
    )

    "sheet17" = @(  # SkillAttackDetail
        @("int","enum(JobMainDamageType)","float","enum(SkillAttackKind)","int"),
        @("Id","DamageType","DamageCoefficient","AttackKind","ProjectileTypeId"),
        @([int]1,"Magic",[double]1.0,"Ranged",[int]70001),
        @([int]2,"Magic",[double]0.5,"Melee",[int]0)
    )

    "sheet18" = @(  # SkillBuffDetail
        @("int","enum(StatType)","enum(BuffValueType)","float","float"),
        @("Id","BuffTargetStat","ValueType","Value","Duration"),
        @([int]1,"Atk","Percent",[double]0.2,[double]5.0),
        @([int]2,"Def","Flat",[double]50.0,[double]10.0)
    )

    "sheet19" = @(  # SkillDebuffDetail
        @("int","enum(DebuffType)","float","int","float"),
        @("Id","DebuffType","Value","DamagePerTick","Duration"),
        @([int]1,"Stun",[double]0.0,[int]0,[double]2.0),
        @([int]2,"Slow",[double]0.3,[int]0,[double]4.0),
        @([int]3,"Poison",[double]0.0,[int]20,[double]6.0),
        @([int]4,"Bleed",[double]0.0,[int]15,[double]4.0)
    )
}

# ── Read existing [Content_Types].xml ─────────────────────────────────────────
$zipR = [System.IO.Compression.ZipFile]::Open($XlsxPath, [System.IO.Compression.ZipArchiveMode]::Read)
$ctEntry  = $zipR.GetEntry("[Content_Types].xml")
$ctStream = $ctEntry.Open()
$ctXml    = [System.IO.StreamReader]::new($ctStream).ReadToEnd()
$ctStream.Close()
$zipR.Dispose()

# ── Update mode: replace sheet14-19 content ───────────────────────────────────
$zipU = [System.IO.Compression.ZipFile]::Open($XlsxPath, [System.IO.Compression.ZipArchiveMode]::Update)

$ctChanged = $false

foreach ($sheetKey in $sheetData.Keys) {
    $path = "xl/worksheets/$sheetKey.xml"
    $xml  = Build-SheetXml $sheetData[$sheetKey]

    # Delete existing entry if present
    $existing = $zipU.GetEntry($path)
    if ($existing -ne $null) {
        $existing.Delete()
    }

    # Create new entry
    $entry  = $zipU.CreateEntry($path)
    $stream = $entry.Open()
    $bytes  = [System.Text.Encoding]::UTF8.GetBytes($xml)
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()
    Write-Host "Updated $path"

    # Ensure [Content_Types].xml has this sheet registered
    $partName = "/xl/worksheets/$sheetKey.xml"
    $ctType   = 'application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml'
    if ($ctXml -notmatch [regex]::Escape($partName)) {
        $insertTag = "<Override PartName=`"$partName`" ContentType=`"$ctType`"/>"
        $ctXml     = $ctXml -replace '</Types>', "$insertTag</Types>"
        $ctChanged  = $true
        Write-Host "Added content-type for $partName"
    }
}

# Patch [Content_Types].xml if changed
if ($ctChanged) {
    $ctEntryU = $zipU.GetEntry("[Content_Types].xml")
    $ctEntryU.Delete()
    $newCt  = $zipU.CreateEntry("[Content_Types].xml")
    $ctS    = $newCt.Open()
    $ctB    = [System.Text.Encoding]::UTF8.GetBytes($ctXml)
    $ctS.Write($ctB, 0, $ctB.Length)
    $ctS.Close()
    Write-Host "Updated [Content_Types].xml"
}

$zipU.Dispose()
Write-Host ""
Write-Host "Done: $XlsxPath"
