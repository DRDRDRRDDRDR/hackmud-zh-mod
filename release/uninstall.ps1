# hackmud 简体中文模组 — 卸载
# 用法: powershell -ExecutionPolicy Bypass -File uninstall.ps1
#
# 只删除本模组新增的文件；游戏本体与数据文件从未被改动，因此无需"还原"。
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\hackmud"
)
$ErrorActionPreference = 'Stop'
function Say($m, $c = 'Gray') { Write-Host $m -ForegroundColor $c }

Say "== hackmud 简体中文模组 卸载 ==" Cyan
$p = Get-Process -Name 'hackmud_win' -ErrorAction SilentlyContinue
if ($p) { Say "!! hackmud 正在运行，请先完全退出" Red; exit 1 }

# 只删这三类"本模组新增"的东西
$targets = @('winhttp.dll', '.doorstop_version', 'doorstop_config.ini', 'BepInEx')
$removed = 0
foreach ($t in $targets) {
    $p = Join-Path $GameDir $t
    if (Test-Path -LiteralPath $p) {
        Remove-Item -LiteralPath $p -Recurse -Force
        Say ("   已删除 {0}" -f $t) Green
        $removed++
    }
}
Say ("共删除 {0} 项" -f $removed) Green
Say "游戏本体与 hackmud_win_Data 未被触碰。" Gray

# 若游戏目录里还有 BepInEx 的日志残留，一并提示
$log = Join-Path $GameDir 'BepInEx\LogOutput.log'
if (Test-Path -LiteralPath $log) { Say "（BepInEx 日志: $log）" DarkGray }
exit 0
