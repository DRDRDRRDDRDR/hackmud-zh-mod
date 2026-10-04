# Shared install safety helpers (Windows PowerShell 5.1).
$ErrorActionPreference = 'Stop'
$PluginPrefix = 'BepInEx\plugins\hackmud-zh\'
$PluginFiles = @('BepInEx\plugins\hackmud-zh\HackmudZh.dll','BepInEx\plugins\hackmud-zh\zh.json','BepInEx\plugins\hackmud-zh\wordmap.json')
function Assert-PlainPath([string]$Path) {
    $p = [IO.Path]::GetFullPath($Path)
    while ($p) {
        if (Test-Path -LiteralPath $p) {
            if ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point refused: $p" }
        }
        $parent = Split-Path -Parent $p
        if ($parent -eq $p) { break }
        $p = $parent
    }
}
function Resolve-SafeChild([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or $Relative -match '[:/]' -or $Relative.StartsWith('\')) { throw "Unsafe relative path: $Relative" }
    foreach ($part in $Relative.Split('\')) {
        if ($part -eq '' -or $part -eq '.' -or $part -eq '..' -or $part -match '[. ]$|[<>"|?*\x00-\x1f]' -or $part -match '^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)') { throw "Unsafe path segment: $part" }
    }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $p = [IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if (-not $p.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path escapes root' }
    Assert-PlainPath $p
    return $p
}
function Get-ModHash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant() }
function Read-StrictUtf8([string]$Path) { return (New-Object Text.UTF8Encoding($false, $true)).GetString([IO.File]::ReadAllBytes($Path)).TrimStart([char]0xfeff) }
function Read-ModManifest([string]$Package) {
    Assert-PlainPath $Package
    $manifest = Join-Path $Package 'SHA256SUMS.txt'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'Missing SHA256SUMS.txt' }
    $items = @{}
    foreach ($line in (Read-StrictUtf8 $manifest).Split("`n")) {
        $line = $line.TrimEnd("`r")
        if ($line -eq '') { continue }
        if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') { throw "Malformed manifest line: $line" }
        $hash = $Matches[1].ToUpperInvariant(); $rel = $Matches[2]
        $null = Resolve-SafeChild $Package $rel
        if ($items.ContainsKey($rel)) { throw "Duplicate manifest path: $rel" }
        $items[$rel] = $hash
    }
    $required = @('winhttp.dll','.doorstop_version','doorstop_config.ini','BepInEx\core\BepInEx.dll','BepInEx\core\0Harmony.dll','BepInEx\plugins\hackmud-zh\HackmudZh.dll','BepInEx\plugins\hackmud-zh\zh.json','BepInEx\plugins\hackmud-zh\wordmap.json','install.ps1','verify.ps1','uninstall.ps1','safety.ps1')
    foreach ($rel in $required) { if (-not $items.ContainsKey($rel)) { throw "Manifest missing required entry: $rel" } }
    return $items
}
function Test-ModManifest([string]$Root, $Items, [switch]$PayloadOnly) {
    foreach ($rel in $Items.Keys) {
        if ($PayloadOnly -and -not (Test-ModPayload $rel)) { continue }
        $p = Resolve-SafeChild $Root $rel
        if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "Missing manifest file: $rel" }
        if ((Get-ModHash $p) -ne $Items[$rel]) { throw "Hash mismatch: $rel" }
    }
}
function Test-ModPayload([string]$Relative) { return $Relative -in @('winhttp.dll','.doorstop_version','doorstop_config.ini') -or $Relative.StartsWith('BepInEx\',[StringComparison]::OrdinalIgnoreCase) }
function Test-ModPlugin([string]$Relative) { return $Relative -in $PluginFiles }
function Read-ModReceipt([string]$Root, $Manifest) {
    $p = Resolve-SafeChild $Root '.hackmud-zh-receipt.json'
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw 'Missing ownership receipt; no automatic deletion allowed' }
    $doc = (Read-StrictUtf8 $p) | ConvertFrom-Json
    if ($doc.format -ne 2 -or $doc.product -ne 'hackmud-zh-mod' -or -not $doc.entries) { throw 'Invalid ownership receipt' }
    $seen = @{}
    foreach ($e in @($doc.entries)) {
        $rel = [string]$e.path
        $file = Resolve-SafeChild $Root $rel
        if (-not (Test-ModPlugin $rel) -or $seen.ContainsKey($rel) -or -not $Manifest.ContainsKey($rel)) { throw "Invalid receipt path: $rel" }
        if ([string]$e.sha256 -notmatch '^[0-9A-Fa-f]{64}$' -or $e.sha256 -ne $Manifest[$rel]) { throw "Receipt not bound to package: $rel" }
        if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-ModHash $file) -ne $Manifest[$rel] -or (Get-Item -LiteralPath $file).Length -ne $e.length) { throw "Owned file changed: $rel" }
        $seen[$rel] = $true
    }
    foreach ($rel in $PluginFiles) { if (-not $seen.ContainsKey($rel)) { throw "Receipt missing plugin file: $rel" } }
    if ($doc.backup) {
        $backupRel = [string]$doc.backup.path
        if ($backupRel -notmatch '^\.hackmud-zh-backups\\[A-Za-z0-9TzZ-]+$') { throw 'Invalid takeover backup path' }
        $bp = Resolve-SafeChild $Root $backupRel
        if (-not (Test-Path -LiteralPath $bp -PathType Container)) { throw 'Missing takeover backup' }
        $backupSeen = @{}
        foreach ($b in @($doc.backup.files)) {
            $rel = [string]$b.path
            if ($rel -notmatch ('^' + [regex]::Escape($backupRel) + '\\(HackmudZh\.dll|zh\.json|wordmap\.json)$')) { throw "Invalid takeover backup entry: $rel" }
            if ($backupSeen.ContainsKey($rel) -or [string]$b.sha256 -notmatch '^[0-9A-Fa-f]{64}$' -or [long]$b.length -lt 0) { throw "Invalid takeover backup metadata: $rel" }
            $f = Resolve-SafeChild $Root $rel
            if (-not (Test-Path -LiteralPath $f -PathType Leaf) -or (Get-ModHash $f) -ne $b.sha256 -or (Get-Item $f).Length -ne [long]$b.length) { throw "Takeover backup changed: $rel" }
            $backupSeen[$rel] = $true
        }
        if ($backupSeen.Count -lt 1) { throw 'Takeover backup has no files' }
        if (-not $doc.takeover -or -not $doc.takeover.originalFiles) { throw 'Missing takeover original file list' }
        $originalSeen = @{}
        foreach ($original in @($doc.takeover.originalFiles)) {
            if (-not (Test-ModPlugin ([string]$original)) -or $originalSeen.ContainsKey([string]$original)) { throw 'Invalid takeover original file list' }
            $expected = $backupRel + '\' + (Split-Path -Leaf ([string]$original))
            if (-not $backupSeen.ContainsKey($expected)) { throw "Takeover backup missing entry: $expected" }
            $originalSeen[[string]$original] = $true
        }
        if ($originalSeen.Count -ne $backupSeen.Count) { throw 'Takeover backup and original file lists differ' }
    } elseif ($doc.takeover -and @($doc.takeover.originalFiles).Count -gt 0) { throw 'Takeover original list has no backup' }
    return $doc
}
function Test-PristineGame([string]$Root) {
    $expected = @{'Managed\Core.dll'='D424EAB9372946946B5FFD9DC49B17D9C5060B3CEC638A5952E7EAD99CD696E6';'resources.assets'='E2E661C96397F9C444936B9767F7F723B5FDAF64FC4ECB04B52E7D5B678C0003';'sharedassets0.assets'='E07F027F18A41DB03E387DF729DB77D933AC1B0593826640A4E91FE6E7709D9B';'level0'='2D7FC2DA43E6273E8D1A2A3D2E2761869563C7C4D99DA34905E0581463B7441A'}
    foreach ($rel in $expected.Keys) { $p=Resolve-SafeChild $Root ('hackmud_win_Data\'+$rel); if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "Missing original game file: $rel" }; if ((Get-ModHash $p) -ne $expected[$rel]) { throw "Game does not match supported pristine baseline: $rel" } }
}
function Assert-GameTarget([string]$Root) { Assert-PlainPath $Root; if (-not (Test-Path -LiteralPath (Join-Path $Root 'hackmud_win.exe') -PathType Leaf)) { throw 'Missing hackmud_win.exe' }; if (Get-Process -Name 'hackmud_win' -ErrorAction SilentlyContinue) { throw 'Game is running; exit it first' } }
