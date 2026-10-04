# hackmud 简体中文模组 - 安装/无收据接管
param([string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\hackmud")
$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $Here 'safety.ps1')
$ReceiptName = '.hackmud-zh-receipt.json'
$PluginFiles = @('BepInEx\plugins\hackmud-zh\HackmudZh.dll','BepInEx\plugins\hackmud-zh\zh.json','BepInEx\plugins\hackmud-zh\wordmap.json')
$LoaderFiles = @('winhttp.dll','.doorstop_version','doorstop_config.ini','BepInEx\core\BepInEx.dll','BepInEx\core\0Harmony.dll')
function Say($m, $c = 'Gray') { Write-Host $m -ForegroundColor $c }
function Copy-Checked([string]$Source, [string]$Destination) {
    $parent = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    if ((Get-ModHash $Destination) -ne (Get-ModHash $Source) -or (Get-Item $Destination).Length -ne (Get-Item $Source).Length) { throw "Copy verification failed: $Destination" }
}
Say '== hackmud 简体中文模组 安装/接管 ==' Cyan
try {
    Assert-GameTarget $GameDir
    Assert-PlainPath $Here
    $receipt = Resolve-SafeChild $GameDir $ReceiptName
    if (Test-Path -LiteralPath $receipt) { throw '已存在本模组安装收据；请先运行卸载脚本' }
    $manifest = Read-ModManifest $Here
    Test-ModManifest $Here $manifest
    foreach ($rel in $LoaderFiles) {
        $dst = Resolve-SafeChild $GameDir $rel
        if (-not (Test-Path -LiteralPath $dst -PathType Leaf)) { throw "目标缺少兼容的 BepInEx 加载器文件: $rel；本次接管不会安装或拥有加载器" }
        if ((Get-ModHash $dst) -ne $manifest[$rel]) { throw "目标加载器与发布包不匹配: $rel" }
    }
    $backupRoot = $null; $backupEntries = @(); $copied = @(); $existing = @()
    $pluginDir = Resolve-SafeChild $GameDir 'BepInEx\plugins\hackmud-zh'
    try {
        foreach ($rel in $PluginFiles) {
            $dst = Resolve-SafeChild $GameDir $rel
            if (Test-Path -LiteralPath $dst) {
                if (-not (Test-Path -LiteralPath $dst -PathType Leaf)) { throw "目标插件路径不是文件: $rel" }
                $existing += $rel
            }
        }
        if ($existing.Count -gt 0) {
            $backupId = ((Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + [guid]::NewGuid().ToString('N'))
            $backupRoot = Resolve-SafeChild $GameDir ('.hackmud-zh-backups\' + $backupId)
            New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
            foreach ($rel in $existing) {
                $name = Split-Path -Leaf $rel
                $bp = Join-Path $backupRoot $name
                Copy-Checked (Resolve-SafeChild $GameDir $rel) $bp
                $backupEntries += [pscustomobject]@{ path=('.hackmud-zh-backups\' + $backupId + '\' + $name); sha256=(Get-ModHash $bp); length=(Get-Item $bp).Length }
            }
            Say ("已备份既有汉化文件到 {0}" -f ('.hackmud-zh-backups\' + $backupId)) Yellow
        }
        foreach ($rel in $PluginFiles) {
            $src = Resolve-SafeChild $Here $rel
            $dst = Resolve-SafeChild $GameDir $rel
            Copy-Checked $src $dst
            $copied += $rel
        }
        $entries = @()
        foreach ($rel in $PluginFiles) { $p=Resolve-SafeChild $GameDir $rel; $entries += [pscustomobject]@{ path=$rel; sha256=(Get-ModHash $p); length=(Get-Item $p).Length } }
        $doc = [pscustomobject]@{ format=2; product='hackmud-zh-mod'; installedAt=(Get-Date).ToUniversalTime().ToString('o'); entries=@($entries); takeover=@{ originalFiles=@($existing) }; backup=if ($backupRoot) { [pscustomobject]@{ path=('.hackmud-zh-backups\' + (Split-Path -Leaf $backupRoot)); files=@($backupEntries) } } else { $null } }
        $tmp = $receipt + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
        [IO.File]::WriteAllText($tmp, ($doc | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
        if ((Get-ModHash $tmp).Length -ne 64) { throw 'Receipt write verification failed' }
        Move-Item -LiteralPath $tmp -Destination $receipt -Force
        $null = Read-ModReceipt $GameDir $manifest
    } catch {
        foreach ($rel in $copied) {
            $p=Resolve-SafeChild $GameDir $rel
            if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force }
        }
        if ($backupRoot -and (Test-Path -LiteralPath $backupRoot)) {
            foreach ($rel in $existing) {
                $name = Split-Path -Leaf $rel
                $bp = Join-Path $backupRoot $name
                $dst = Resolve-SafeChild $GameDir $rel
                if (Test-Path -LiteralPath $bp -PathType Leaf) { Copy-Checked $bp $dst }
            }
            Say ('安装失败；已恢复旧汉化文件。备份保留在 {0}' -f ('.hackmud-zh-backups\' + (Split-Path -Leaf $backupRoot))) Yellow
        }
        if (Test-Path -LiteralPath $receipt) { Remove-Item -LiteralPath $receipt -Force }
        throw
    }
    Say ("已接管并安装 {0} 个汉化插件文件；BepInEx 加载器与其他插件未改动。" -f $copied.Count) Green
    exit 0
} catch { Say ('安装失败: ' + $_.Exception.Message) Red; exit 1 }
