# hackmud 简体中文模组 — 安装
# 用法: powershell -ExecutionPolicy Bypass -File install.ps1
#
# 原则：**只新增文件，绝不覆盖游戏既有文件**。
#   BepInEx 通过 winhttp.dll 代理注入，游戏本体与数据文件全程不动。
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\hackmud"
)
$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Say($m, $c = 'Gray') { Write-Host $m -ForegroundColor $c }

Say "== hackmud 简体中文模组 安装 ==" Cyan
if (-not (Test-Path -LiteralPath $GameDir)) { Say "!! 找不到游戏目录: $GameDir" Red; exit 1 }

$exe = Join-Path $GameDir 'hackmud_win.exe'
if (-not (Test-Path -LiteralPath $exe)) { Say "!! 该目录不是 hackmud 安装目录（缺 hackmud_win.exe）" Red; exit 1 }

$p = Get-Process -Name 'hackmud_win' -ErrorAction SilentlyContinue
if ($p) { Say "!! hackmud 正在运行，请先完全退出" Red; exit 1 }

# 待安装项（顶层文件 + 目录）
$topFiles = @('winhttp.dll', '.doorstop_version', 'doorstop_config.ini')
$topDirs  = @('BepInEx')

foreach ($f in $topFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $Here $f))) { Say "!! 包内缺少 $f" Red; exit 1 }
}

# ---- 冲突预检：既有文件一律不覆盖 ----
$conflicts = @()
foreach ($f in $topFiles) {
    if (Test-Path -LiteralPath (Join-Path $GameDir $f)) { $conflicts += $f }
}
foreach ($d in $topDirs) {
    if (Test-Path -LiteralPath (Join-Path $GameDir $d)) { $conflicts += "$d\" }
}
if ($conflicts.Count -gt 0) {
    Say "!! 目标目录已存在以下文件/目录，为避免破坏现有内容，安装中止：" Red
    $conflicts | ForEach-Object { Say "     $_" Red }
    Say "   若确认是本模组的旧版残留，请先运行 uninstall.ps1。" Yellow
    exit 1
}

# ---- 安装（纯新增）----
$n = 0
foreach ($f in $topFiles) {
    Copy-Item -LiteralPath (Join-Path $Here $f) -Destination (Join-Path $GameDir $f) -Force
    $n++
}
foreach ($d in $topDirs) {
    Copy-Item -LiteralPath (Join-Path $Here $d) -Destination (Join-Path $GameDir $d) -Recurse -Force
    $n++
}
Say "已新增 $n 项（未覆盖任何游戏文件）" Green

# ---- 安装后自检 ----
& (Join-Path $Here 'verify.ps1') -GameDir $GameDir
exit $LASTEXITCODE
