# Save-CodeBuddyWindows.ps1
# 用法：把当前所有 CodeBuddy 系 IDE 打开的窗口列表存成快照
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Save-CodeBuddyWindows.ps1

[CmdletBinding()]
param(
    [string]$OutDir,
    [switch]$IncludeEmpty
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CodeBuddyWindows.Common.ps1')

if (-not $OutDir) { $OutDir = $script:SnapshotDirDefault }
New-WindowSnapshot -OutDir $OutDir -IncludeEmpty:$IncludeEmpty | Out-Null
