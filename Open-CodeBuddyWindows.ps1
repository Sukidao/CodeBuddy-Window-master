# Open-CodeBuddyWindows.ps1
# 一键恢复：按快照把之前打开的所有 IDE 窗口重新拉起来（每个窗口一个进程）
#
# 用法：
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Open-CodeBuddyWindows.ps1
# 常用参数：
#   -DryRun          只列出将要打开哪些窗口，不真的打开
#   -Force           已经开着的窗口也再开一个（默认跳过，避免重复）
#   -DelayMs 1500    两个窗口之间的间隔毫秒数（电脑慢可调大）
#   -Snapshot <path> 指定快照文件（默认 snapshots\windows-latest.json）
#   -IncludeEmpty    连"空白窗口"也恢复

[CmdletBinding()]
param(
    [string]$Snapshot,
    [int]$DelayMs = 1500,
    [switch]$Force,
    [switch]$DryRun,
    [switch]$IncludeEmpty
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CodeBuddyWindows.Common.ps1')

$snapshotDir = $script:SnapshotDirDefault
if (-not $Snapshot) { $Snapshot = Join-Path $snapshotDir 'windows-latest.json' }

function Get-SnapshotSource {
    param([string]$Path)
    $snap = Read-JsonFile $Path
    if ($snap) { return $snap }
    Write-Host "未找到历史快照，改为读取 IDE 当前实时窗口状态..." -ForegroundColor Yellow
    return (New-WindowSnapshot -OutDir $snapshotDir -IncludeEmpty:$IncludeEmpty -Quiet)
}

$snap = Get-SnapshotSource -Path $Snapshot
$entries = @(Get-Prop $snap 'windows')

if ($entries.Count -eq 0) {
    Write-Host ""
    Write-Host "快照里没有任何窗口记录，无法恢复。" -ForegroundColor Red
    Write-Host "请确认之前用 Save-CodeBuddyWindows.ps1 保存过快照，或 IDE 正在运行。" -ForegroundColor DarkGray
    exit 1
}

Write-Host ""
Write-Host ("快照来源: {0}" -f $Snapshot) -ForegroundColor DarkGray
Write-Host ("快照时间: {0}   窗口数: {1}" -f (Get-Prop $snap 'capturedAt'), $entries.Count) -ForegroundColor DarkGray

# 已打开的窗口（用于去重）
$openUris = @{}
if (-not $Force) {
    foreach ($p in Get-DetectedProducts) {
        foreach ($r in ConvertTo-WindowRecords -Product $p -IncludeEmpty:$IncludeEmpty) {
            if ($r.uri) { $openUris[$r.uri.ToLower()] = $true }
        }
    }
}

# 产品 id -> 可执行文件
$productMap = @{}
foreach ($p in Get-IdeProductDefs) { $productMap[$p.Id] = $p }
foreach ($p in Get-DetectedProducts) { $productMap[$p.Id] = $p }

$launched = 0
$skipped = 0
$failed = 0
$index = 0

foreach ($e in $entries) {
    $index++
    $uri = [string](Get-Prop $e 'uri')
    $kind = [string](Get-Prop $e 'kind')
    $pid_ = [string](Get-Prop $e 'product')
    $name = [string](Get-Prop $e 'productName')
    if (-not $name) { $name = $pid_ }

    $exe = [string](Get-Prop $e 'exe')
    if (-not $exe -or -not (Test-Path -LiteralPath $exe)) {
        $def = $productMap[$pid_]
        if ($def -and $def.Exe) { $exe = $def.Exe }
    }

    $label = $uri
    if ($kind -eq 'empty' -or -not $uri) { $label = '(空白窗口)' }

    if ($kind -eq 'empty' -and -not $IncludeEmpty) {
        $skipped++
        Write-Host ("  [{0}] 跳过（空白窗口）" -f $index) -ForegroundColor DarkGray
        continue
    }

    if (-not $exe) {
        $failed++
        Write-Host ("  [{0}] 跳过（找不到 {1} 的可执行文件）  {2}" -f $index, $name, $label) -ForegroundColor Red
        continue
    }

    if ($uri -and $openUris.ContainsKey($uri.ToLower())) {
        $skipped++
        Write-Host ("  [{0}] 已打开，跳过  {1}" -f $index, $label) -ForegroundColor DarkGray
        continue
    }

    # 组装命令行参数
    $argList = @()
    $localPath = ConvertFrom-WindowUri $uri
    if ($localPath) {
        if (-not (Test-Path -LiteralPath $localPath) -and $kind -ne 'workspace') {
            $failed++
            Write-Host ("  [{0}] 跳过（文件夹已不存在）  {1}" -f $index, $localPath) -ForegroundColor Red
            continue
        }
        $argList = @('-n', $localPath)
    } elseif ($uri) {
        # 远程 SSH / WSL / 开发容器等：用 URI 打开
        $argList = @('-n', '--folder-uri', $uri)
    } else {
        $argList = @('-n')
    }

    if ($DryRun) {
        Write-Host ("  [{0}] 将打开  {1}  {2}" -f $index, $name, $label) -ForegroundColor Cyan
        continue
    }

    try {
        Start-Process -FilePath $exe -ArgumentList $argList | Out-Null
        $launched++
        Write-Host ("  [{0}] 已启动  {1}  {2}" -f $index, $name, $label) -ForegroundColor Green
        if ($DelayMs -gt 0) { Start-Sleep -Milliseconds $DelayMs }
    } catch {
        $failed++
        Write-Host ("  [{0}] 启动失败: {1}" -f $index, $_.Exception.Message) -ForegroundColor Red
    }
}

Write-Host ""
if ($DryRun) {
    Write-Host "（试运行，未真正打开窗口）" -ForegroundColor Yellow
} else {
    Write-Host ("完成：新开 {0} 个，跳过 {1} 个，失败 {2} 个" -f $launched, $skipped, $failed) -ForegroundColor Green
}
if ($failed -gt 0) { exit 1 }
exit 0
