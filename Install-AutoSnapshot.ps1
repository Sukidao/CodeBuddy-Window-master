# Install-AutoSnapshot.ps1
# 可选：注册一个"登录时"计划任务，开机登录后自动存一次窗口快照。
# 好处：重启后如果你先手动打开了一个窗口，storage.json 会被覆盖，
#       但开机瞬间存下的快照仍然保存着重启前的完整窗口列表。
#
# 安装： powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-AutoSnapshot.ps1
# 卸载： powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-AutoSnapshot.ps1 -Uninstall

[CmdletBinding()]
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$taskName = 'CodeBuddy-WindowSnapshot'

if ($Uninstall) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "已卸载计划任务: $taskName" -ForegroundColor Green
    return
}

$script = Join-Path $PSScriptRoot 'Save-CodeBuddyWindows.ps1'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument ('-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "{0}"' -f $script)
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger `
    -Settings $settings -Principal $principal -Force | Out-Null

Write-Host "已注册计划任务: $taskName（每次登录自动保存窗口快照）" -ForegroundColor Green
