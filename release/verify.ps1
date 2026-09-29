# hackmud 简体中文模组 — 校验
# 用法: powershell -ExecutionPolicy Bypass -File verify.ps1
#
# 做两件事：
#   1. 证明**游戏既有文件未被改动**（与钉死的原版哈希逐条比对）
#   2. 检查模组文件齐全且哈希与包内 SHA256SUMS.txt 一致
#
# 退出码 0 = 全部通过
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\hackmud"
)
$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Data = Join-Path $GameDir 'hackmud_win_Data'

function Say($m, $c = 'Gray') { Write-Host $m -ForegroundColor $c }
function Sha([string]$p) {
    if (-not (Test-Path -LiteralPath $p)) { return '<缺失>' }
    (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash
}

$fails = 0

# ---- 1. 游戏原版文件哈希（钉死值 = 未打任何补丁的零售版）----
Say "== [1] 游戏既有文件未被改动 ==" Cyan
$Pristine = @{
    'Managed\Core.dll'      = 'D424EAB9372946946B5FFD9DC49B17D9C5060B3CEC638A5952E7EAD99CD696E6'
    'resources.assets'      = 'E2E661C96397F9C444936B9767F7F723B5FDAF64FC4ECB04B52E7D5B678C0003'
    'sharedassets0.assets'  = 'E07F027F18A41DB03E387DF729DB77D933AC1B0593826640A4E91FE6E7709D9B'
    'level0'                = '2D7FC2DA43E6273E8D1A2A3D2E2761869563C7C4D99DA34905E0581463B7441A'
}
foreach ($k in $Pristine.Keys) {
    $got = Sha (Join-Path $Data $k)
    if ($got -eq $Pristine[$k]) {
        Say ("   OK      {0,-24} {1}" -f $k, $got.Substring(0, 16)) Green
    } elseif ($got -eq '<缺失>') {
        Say ("   缺失    {0}" -f $k) Red; $fails++
    } else {
        Say ("   已改动  {0,-24} {1}" -f $k, $got.Substring(0, 16)) Red
        Say ("           （若你装过其它汉化补丁，这是预期的；本模组自身不改这些文件）") Yellow
        $fails++
    }
}

# ---- 2. 模组文件齐全 ----
Say ""
Say "== [2] 模组文件齐全 ==" Cyan
$need = @('winhttp.dll', '.doorstop_version', 'doorstop_config.ini',
          'BepInEx\core\BepInEx.dll', 'BepInEx\core\0Harmony.dll',
          'BepInEx\plugins\hackmud-zh\HackmudZh.dll',
          'BepInEx\plugins\hackmud-zh\zh.json')
foreach ($r in $need) {
    $p = Join-Path $GameDir $r
    if (Test-Path -LiteralPath $p) {
        Say ("   OK      {0,-46} {1,9:N0} B" -f $r, (Get-Item -LiteralPath $p).Length) Green
    } else {
        Say ("   缺失    {0}" -f $r) Red; $fails++
    }
}

# ---- 3. 模组自身哈希 vs SHA256SUMS.txt ----
Say ""
Say "== [3] 模组文件哈希 vs SHA256SUMS.txt ==" Cyan
$sums = Join-Path $Here 'SHA256SUMS.txt'
if (Test-Path -LiteralPath $sums) {
    $bad = 0
    foreach ($line in Get-Content -LiteralPath $sums -Encoding UTF8) {
        if ($line -notmatch '^([0-9A-Fa-f]{64})\s+(\S.*)$') { continue }
        $want = $Matches[1].ToUpper(); $rel = $Matches[2].Trim()
        $p = Join-Path $GameDir $rel
        if (-not (Test-Path -LiteralPath $p)) { continue }   # 只校验已安装的
        $got = Sha $p
        if ($got -ne $want) { Say ("   不符    {0}" -f $rel) Red; $bad++ }
    }
    if ($bad -eq 0) { Say "   OK      已安装文件哈希全部一致" Green } else { $fails += $bad }
} else {
    Say "   （包内无 SHA256SUMS.txt，跳过）" Yellow
}

# ---- 4. 词典可解析 ----
Say ""
Say "== [4] 词典 ==" Cyan
$zh = Join-Path $GameDir 'BepInEx\plugins\hackmud-zh\zh.json'
if (Test-Path -LiteralPath $zh) {
    try {
        $txt = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($zh))
        $obj = $txt | ConvertFrom-Json
        $c = ($obj.PSObject.Properties | Measure-Object).Count
        Say ("   OK      {0} 条词条" -f $c) Green
    } catch { Say ("   解析失败: {0}" -f $_.Exception.Message) Red; $fails++ }
} else { Say "   缺失 zh.json" Red; $fails++ }

Say ""
if ($fails -eq 0) { Say "== 结果: PASS ==" Green } else { Say ("== 结果: {0} 项不通过 ==" -f $fails) Red }
exit $(if ($fails -eq 0) { 0 } else { 1 })
