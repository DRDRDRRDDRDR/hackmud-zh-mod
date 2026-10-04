# hackmud 简体中文模组 - 严格校验
param([string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\hackmud")
$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $Here 'safety.ps1')
function Say($m, $c = 'Gray') { Write-Host $m -ForegroundColor $c }
try {
    Assert-GameTarget $GameDir
    $manifest = Read-ModManifest $Here
    Test-ModManifest $Here $manifest
    Test-ModManifest $GameDir $manifest -PayloadOnly
    $null = Read-ModReceipt $GameDir $manifest
    Test-PristineGame $GameDir
    $zh = Resolve-SafeChild $GameDir 'BepInEx\plugins\hackmud-zh\zh.json'
    $txt = Read-StrictUtf8 $zh
    $obj = $txt | ConvertFrom-Json
    $count = ($obj.PSObject.Properties | Measure-Object).Count
    if ($count -le 0) { throw '词典为空' }
    Say ("PASS: 收据完整，{0} 条词典，游戏目录未被修改。" -f $count) Green
    exit 0
} catch { Say ('FAIL: ' + $_.Exception.Message) Red; exit 1 }
