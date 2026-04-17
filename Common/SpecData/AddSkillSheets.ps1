
param(
    [string]$XlsxPath = "$PSScriptRoot\EchodiaSpecData.xlsx"
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Build-SheetXml {
    param([object[][]]$rows)
    $sb = [System.Text.StringBuilder]::new()
    $sb.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>') | Out-Null
    $sb.Append('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">') | Out-Null
    $sb.Append('<sheetData>') | Out-Null
    $rowIndex = 1
    foreach ($row in $rows) {
        $sb.Append("<row r=`"$rowIndex`">") | Out-Null
        $colIndex = 1
        foreach ($cell in $row) {
            $colLetter = Get-ColumnLetter $colIndex
            $cellRef = "$colLetter$rowIndex"
            if ($cell -is [int] -or $cell -is [double] -or $cell -is [float] -or $cell -is [long]) {
                $sb.Append("<c r=`"$cellRef`"><v>$cell</v></c>") | Out-Null
            } else {
                $escaped = [System.Security.SecurityElement]::Escape($cell.ToString())
                $sb.Append("<c r=`"$cellRef`" t=`"inlineStr`"><is><t>$escaped</t></is></c>") | Out-Null
            }
            $colIndex++
        }
        $sb.Append("</row>") | Out-Null
        $rowIndex++
    }
    $sb.Append('</sheetData></worksheet>') | Out-Null
    return $sb.ToString()
}

function Get-ColumnLetter {
    param([int]$col)
    $letters = ""
    while ($col -gt 0) {
        $col--
        $letters = [char](65 + ($col % 26)) + $letters
        $col = [int]($col / 26)
    }
    return $letters
}

$sheets = [ordered]@{
    "Skill" = @(
        @("int","enum(SkillType)","string","enum(SkillCategoryType)","enum(SkillActivationType)","List<enum>(CastDirectionType)","float","enum(SkillTargetingType)","int","int","List<enum>(PlayerJobType)","float","float","List<int>"),
        @("Id","SkillType","Name","SkillCategoryType","SkillActivationType","CastDirections","CastRange","TargetingType","MaxTargets","RequiredLevel","RequiredJobs","CoolTime","CastTime","SkillActionIds"),
        @([int]40001,"MagicMissile","Magic Missile","Attack","Instant","{Front}",[double]0.0,"Enemy",[int]1,[int]1,"{Mage}",[double]5.0,[double]0.0,"{1}")
    )
    "SkillCost" = @(
        @("int","int","enum(SkillCostType)","int","int"),
        @("Id","SkillId","CostType","Amount","ItemId"),
        @([int]1,[int]40001,"Mp",[int]30,[int]0)
    )
    "SkillAction" = @(
        @("int","int","enum(SkillActionType)","int","int"),
        @("Id","SkillId","ActionType","DelayMs","SkillDetailId"),
        @([int]1,[int]40001,"SpawnProjectile",[int]0,[int]1)
    )
    "SkillAttackDetail" = @(
        @("int","enum(JobMainDamageType)","float","enum(SkillAttackKind)","int"),
        @("Id","DamageType","DamageCoefficient","AttackKind","ProjectileTypeId"),
        @([int]1,"Magic",[double]1.0,"Ranged",[int]70001)
    )
    "SkillBuffDetail" = @(
        @("int","enum(StatType)","enum(BuffValueType)","float","float"),
        @("Id","BuffTargetStat","ValueType","Value","Duration")
    )
    "SkillDebuffDetail" = @(
        @("int","enum(DebuffType)","float","float"),
        @("Id","DebuffType","Value","Duration")
    )
}

# ── Step 1: Read existing data from the zip ───────────────────────────────────
$zipRead = [System.IO.Compression.ZipFile]::Open($XlsxPath, [System.IO.Compression.ZipArchiveMode]::Read)
$wbEntry   = $zipRead.GetEntry("xl/workbook.xml")
$wbStream  = $wbEntry.Open()
$wbXml     = [System.IO.StreamReader]::new($wbStream).ReadToEnd()
$wbStream.Close()

$wbRelsEntry  = $zipRead.GetEntry("xl/_rels/workbook.xml.rels")
$wbRelsStream = $wbRelsEntry.Open()
$wbRelsXml    = [System.IO.StreamReader]::new($wbRelsStream).ReadToEnd()
$wbRelsStream.Close()
$zipRead.Dispose()

$existingSheetCount = ([regex]::Matches($wbXml, '<sheet ')).Count
$existingRelCount   = ([regex]::Matches($wbRelsXml, '<Relationship ')).Count
Write-Host "Existing sheets: $existingSheetCount, rels: $existingRelCount"

$nextSheetId = $existingSheetCount + 1
$nextRid     = $existingRelCount + 1
$nextFileIdx = $existingSheetCount + 1

$newSheetEntries = ""
$newRelEntries   = ""
$newWorksheets   = @{}  # path -> xml content

foreach ($sheetName in $sheets.Keys) {
    if ($wbXml -match [regex]::Escape("name=`"$sheetName`"")) {
        Write-Host "Sheet '$sheetName' already exists - skipping."
        continue
    }
    $rid      = "rId$nextRid"
    $fileName = "sheet$nextFileIdx.xml"
    $wsPath   = "xl/worksheets/$fileName"

    $newWorksheets[$wsPath] = Build-SheetXml $sheets[$sheetName]
    $newSheetEntries += "<sheet name=`"$sheetName`" sheetId=`"$nextSheetId`" r:id=`"$rid`"/>"
    $newRelEntries   += "<Relationship Id=`"$rid`" Type=`"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet`" Target=`"worksheets/$fileName`"/>"

    Write-Host "Queued: $wsPath  (sheet='$sheetName' sheetId=$nextSheetId rid=$rid)"
    $nextSheetId++
    $nextRid++
    $nextFileIdx++
}

if ($newWorksheets.Count -eq 0) {
    Write-Host "Nothing to add."
    exit 0
}

# Patch XML strings
$wbXml     = $wbXml    -replace '</sheets>',        "$newSheetEntries</sheets>"
$wbRelsXml = $wbRelsXml -replace '</Relationships>', "$newRelEntries</Relationships>"

# ── Step 2: Write everything in Update mode (no reads, no deletes) ─────────────
$zipUpdate = [System.IO.Compression.ZipFile]::Open($XlsxPath, [System.IO.Compression.ZipArchiveMode]::Update)

# Add new worksheet files
foreach ($wsPath in $newWorksheets.Keys) {
    $entry  = $zipUpdate.CreateEntry($wsPath)
    $stream = $entry.Open()
    $bytes  = [System.Text.Encoding]::UTF8.GetBytes($newWorksheets[$wsPath])
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()
    Write-Host "Added $wsPath"
}

# Overwrite workbook.xml (delete + recreate)
$wbEntryUpdate = $zipUpdate.GetEntry("xl/workbook.xml")
$wbEntryUpdate.Delete()
$newWbEntry  = $zipUpdate.CreateEntry("xl/workbook.xml")
$wbWriteStream = $newWbEntry.Open()
$wbBytes = [System.Text.Encoding]::UTF8.GetBytes($wbXml)
$wbWriteStream.Write($wbBytes, 0, $wbBytes.Length)
$wbWriteStream.Close()
Write-Host "Updated xl/workbook.xml"

# Overwrite workbook.xml.rels
$wbRelsEntryUpdate = $zipUpdate.GetEntry("xl/_rels/workbook.xml.rels")
$wbRelsEntryUpdate.Delete()
$newWbRelsEntry = $zipUpdate.CreateEntry("xl/_rels/workbook.xml.rels")
$relsWriteStream = $newWbRelsEntry.Open()
$relsBytes = [System.Text.Encoding]::UTF8.GetBytes($wbRelsXml)
$relsWriteStream.Write($relsBytes, 0, $relsBytes.Length)
$relsWriteStream.Close()
Write-Host "Updated xl/_rels/workbook.xml.rels"

$zipUpdate.Dispose()
Write-Host ""
Write-Host "Done: $XlsxPath"
