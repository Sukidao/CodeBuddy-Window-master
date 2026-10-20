# CodeBuddyWindows.Common.ps1
# 公共函数：读取 / 保存 CodeBuddy 系 IDE（VS Code 内核）的窗口状态
# 说明：所有 IDE 都会把"当前打开的窗口列表"实时写入
#       %APPDATA%\<产品>\User\globalStorage\storage.json 的 windowsState.openedWindows
#       本模块负责把这份状态读出来并生成快照。

$script:SnapshotDirDefault = Join-Path $PSScriptRoot 'snapshots'

function Get-Prop {
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $p = $Object.PSObject.Properties[$Name]
    if ($p) { return $p.Value }
    return $null
}

function Get-IdeProductDefs {
    $defs = @(
        @{
            Id       = 'codebuddy-cn'
            Name     = 'CodeBuddy CN'
            UserData = (Join-Path $env:APPDATA 'CodeBuddy CN')
            ExeNames = @('CodeBuddy CN.exe')
            Roots    = @(
                (Join-Path $env:LOCALAPPDATA 'Programs\CodeBuddy CN'),
                (Join-Path $env:ProgramFiles 'CodeBuddy CN'),
                (Join-Path ${env:ProgramFiles(x86)} 'CodeBuddy CN')
            )
        },
        @{
            Id       = 'workbuddy'
            Name     = 'WorkBuddy'
            UserData = (Join-Path $env:APPDATA 'WorkBuddy')
            ExeNames = @('WorkBuddy.exe')
            Roots    = @(
                (Join-Path $env:LOCALAPPDATA 'Programs\WorkBuddy'),
                (Join-Path $env:ProgramFiles 'WorkBuddy')
            )
        },
        @{
            Id       = 'trae-cn'
            Name     = 'Trae CN'
            UserData = (Join-Path $env:APPDATA 'Trae CN')
            ExeNames = @('Trae CN.exe')
            Roots    = @(
                (Join-Path $env:LOCALAPPDATA 'Programs\Trae CN'),
                (Join-Path $env:ProgramFiles 'Trae CN')
            )
        },
        @{
            Id       = 'vscode'
            Name     = 'VS Code'
            UserData = (Join-Path $env:APPDATA 'Code')
            ExeNames = @('Code.exe')
            Roots    = @(
                (Join-Path $env:LOCALAPPDATA 'Programs\Microsoft VS Code'),
                (Join-Path $env:ProgramFiles 'Microsoft VS Code')
            )
        }
    )
    foreach ($d in $defs) {
        $exe = $null
        foreach ($root in $d.Roots) {
            foreach ($n in $d.ExeNames) {
                $candidate = Join-Path $root $n
                if (Test-Path -LiteralPath $candidate) { $exe = $candidate; break }
            }
            if ($exe) { break }
        }
        [pscustomobject]@{
            Id       = $d.Id
            Name     = $d.Name
            UserData = $d.UserData
            Exe      = $exe
            Storage  = (Join-Path $d.UserData 'User\globalStorage\storage.json')
        }
    }
}

function Test-ProductRunning {
    # 判断该 IDE 当前是否真的有进程在跑（storage.json 可能残留很久以前的状态）
    param($Product)
    if (-not $Product.Exe) { return $false }
    $procName = [IO.Path]::GetFileNameWithoutExtension($Product.Exe)
    $procs = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq $procName }
    return [bool]$procs
}

function Get-DetectedProducts {
    param([switch]$OnlyWithExe, [switch]$OnlyRunning)
    $all = Get-IdeProductDefs
    $result = @()
    foreach ($p in $all) {
        if (-not (Test-Path -LiteralPath $p.Storage)) { continue }
        if ($OnlyWithExe -and -not $p.Exe) { continue }
        if ($OnlyRunning -and -not (Test-ProductRunning $p)) { continue }
        $result += $p
    }
    return $result
}

function Read-JsonFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try {
        $raw = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
        if (-not $raw -or -not $raw.Trim()) { return $null }
        return ($raw | ConvertFrom-Json)
    } catch {
        return $null
    }
}

function ConvertTo-WindowRecords {
    param($Product, [switch]$IncludeEmpty)

    $records = @()
    $storage = Read-JsonFile $Product.Storage
    if (-not $storage) { return $records }

    $state = Get-Prop $storage 'windowsState'
    if (-not $state) { return $records }

    $items = @()
    $items += @(Get-Prop $state 'openedWindows')
    $last = Get-Prop $state 'lastActiveWindow'
    if ($last) { $items += $last }

    $seen = @{}
    foreach ($w in $items) {
        if ($null -eq $w) { continue }
        $folderUri = Get-Prop $w 'folder'
        $wsUri = Get-Prop $w 'workspace'
        $uri = $null; $kind = $null
        if ($folderUri) { $uri = [string]$folderUri; $kind = 'folder' }
        elseif ($wsUri) { $uri = [string]$wsUri; $kind = 'workspace' }

        if (-not $uri) {
            if (-not $IncludeEmpty) { continue }
            $uri = ''
            $kind = 'empty'
        }
        if ($seen.ContainsKey($uri)) { continue }
        $seen[$uri] = $true

        $records += [pscustomobject]@{
            product         = $Product.Id
            productName     = $Product.Name
            exe             = $Product.Exe
            kind            = $kind
            uri             = $uri
            remoteAuthority = (Get-Prop $w 'remoteAuthority')
            uiState         = (Get-Prop $w 'uiState')
        }
    }
    return $records
}

function New-WindowSnapshot {
    param(
        [string]$OutDir = $script:SnapshotDirDefault,
        [switch]$IncludeEmpty,
        [switch]$IncludeStopped,
        [switch]$Quiet
    )

    $records = @()
    foreach ($p in (Get-DetectedProducts -OnlyRunning:(-not $IncludeStopped))) {
        $records += ConvertTo-WindowRecords -Product $p -IncludeEmpty:$IncludeEmpty
    }

    $snapshot = [pscustomobject]@{
        capturedAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
        computer   = $env:COMPUTERNAME
        user       = $env:USERNAME
        count      = @($records).Count
        windows    = @($records)
    }

    if (@($records).Count -gt 0) {
        if (-not (Test-Path -LiteralPath $OutDir)) {
            New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
        }
        $json = $snapshot | ConvertTo-Json -Depth 8
        $enc = New-Object System.Text.UTF8Encoding($false)
        [IO.File]::WriteAllText((Join-Path $OutDir 'windows-latest.json'), $json, $enc)
        $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
        [IO.File]::WriteAllText((Join-Path $OutDir ("windows-$stamp.json")), $json, $enc)

        # 只保留最近 40 份时间戳快照
        $old = Get-ChildItem -LiteralPath $OutDir -Filter 'windows-20*.json' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -Skip 40
        foreach ($f in $old) { Remove-Item -LiteralPath $f.FullName -Force -ErrorAction SilentlyContinue }
    }

    if (-not $Quiet) {
        Write-Host ""
        Write-Host ("快照时间: " + $snapshot.capturedAt) -ForegroundColor DarkGray
        if (@($records).Count -eq 0) {
            Write-Host "没有读到任何已打开的窗口（IDE 可能未运行过，或状态已被清空）" -ForegroundColor Yellow
        } else {
            $i = 0
            foreach ($r in $records) {
                $i++
                $title = $r.uri
                if ($r.kind -eq 'empty') { $title = '(空白窗口)' }
                Write-Host ("  [$i] " + $r.productName + "  " + $title)
            }
            Write-Host ("共 {0} 个窗口，已保存到: {1}" -f $i, (Join-Path $OutDir 'windows-latest.json')) -ForegroundColor Green
        }
    }
    return $snapshot
}

function ConvertFrom-WindowUri {
    # 本地 file:// URI 转成本机路径；远程 URI 原样返回 $null
    param([string]$Uri)
    if (-not $Uri) { return $null }
    if ($Uri -like 'file://*') {
        try { return ([uri]$Uri).LocalPath } catch { return $null }
    }
    return $null
}
