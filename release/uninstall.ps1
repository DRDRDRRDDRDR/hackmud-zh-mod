# hackmud 简体中文模组 - 安全卸载并恢复接管前文件
param([string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\hackmud")
$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $Here 'safety.ps1')
$ReceiptName = '.hackmud-zh-receipt.json'
function Say($m, $c = 'Gray') { Write-Host $m -ForegroundColor $c }
Say '== hackmud 简体中文模组 卸载 ==' Cyan
try {
    Assert-GameTarget $GameDir
    $manifest = Read-ModManifest $Here
    Test-ModManifest $Here $manifest
    $doc = Read-ModReceipt $GameDir $manifest
    $receipt = Resolve-SafeChild $GameDir $ReceiptName
    $backupMap = @{}
    if ($doc.backup) {
        foreach ($original in @($doc.takeover.originalFiles)) {
            $leaf = Split-Path -Leaf ([string]$original)
            $b = @($doc.backup.files) | Where-Object { (Split-Path -Leaf ([string]$_.path)) -eq $leaf } | Select-Object -First 1
            if (-not $b) { throw "Missing backup mapping: $original" }
            $backupMap[[string]$original] = $b
        }
    }
    $plan = @()
    foreach ($e in @($doc.entries)) {
        $rel = [string]$e.path
        $dst = Resolve-SafeChild $GameDir $rel
        if ($backupMap.ContainsKey($rel)) {
            $b = $backupMap[$rel]; $src = Resolve-SafeChild $GameDir ([string]$b.path)
            if ((Get-ModHash $src) -ne $b.sha256 -or (Get-Item $src).Length -ne $b.length) { throw "备份校验失败: $($b.path)" }
            $plan += [pscustomobject]@{ path=$rel; destination=$dst; backup=$src; restore=$true }
        } else { $plan += [pscustomobject]@{ path=$rel; destination=$dst; backup=$null; restore=$false } }
    }
    $staged = @(); $removed = @()
    try {
        foreach ($item in $plan) {
            if ($item.restore) {
                $tmp = $item.destination + '.' + [guid]::NewGuid().ToString('N') + '.restore'
                Copy-Item -LiteralPath $item.backup -Destination $tmp
                if ((Get-ModHash $tmp) -ne (Get-ModHash $item.backup)) { throw "恢复暂存校验失败: $($item.path)" }
                $staged += [pscustomobject]@{ path=$item.path; temp=$tmp; destination=$item.destination; backup=$item.backup }
            }
        }
        foreach ($item in $plan) {
            if ($item.restore) {
                $stage = $staged | Where-Object { $_.path -eq $item.path } | Select-Object -First 1
                Move-Item -LiteralPath $stage.temp -Destination $item.destination -Force
                if ((Get-ModHash $item.destination) -ne (Get-ModHash $item.backup)) { throw "恢复后校验失败: $($item.path)" }
            } else { Remove-Item -LiteralPath $item.destination -Force }
            $removed += $item
        }
        Remove-Item -LiteralPath $receipt -Force
    } catch {
        $removedArray = @($removed)
        for ($i = $removedArray.Count - 1; $i -ge 0; $i--) {
            $item = $removedArray[$i]
            if ($item.restore -and (Test-Path -LiteralPath $item.backup)) { Copy-Item -LiteralPath $item.backup -Destination $item.destination -Force }
            elseif (-not $item.restore -and (Test-Path -LiteralPath (Resolve-SafeChild $Here $item.path))) { Copy-Item -LiteralPath (Resolve-SafeChild $Here $item.path) -Destination $item.destination -Force }
        }
        foreach ($s in $staged) { if (Test-Path -LiteralPath $s.temp) { Remove-Item -LiteralPath $s.temp -Force } }
        throw
    }
    $pluginDir = Resolve-SafeChild $GameDir 'BepInEx\plugins\hackmud-zh'
    if ((Test-Path -LiteralPath $pluginDir -PathType Container) -and -not (Get-ChildItem -LiteralPath $pluginDir -Force)) { Remove-Item -LiteralPath $pluginDir -Force }
    Say ("已卸载汉化插件并恢复 {0} 个接管前文件；BepInEx 加载器、备份和其他插件均保留。" -f $plan.Count) Green
    exit 0
} catch { Say ('卸载失败，未完成更改: ' + $_.Exception.Message) Red; exit 1 }
