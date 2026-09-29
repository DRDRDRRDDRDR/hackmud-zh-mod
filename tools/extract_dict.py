# -*- coding: utf-8 -*-
"""extract_dict.py — 从既有补丁里榨出 (原文, 译文) 词典，作为模组的初始词典。

来源：
  1. Managed/Core.dll.bak  vs  Managed/Core.dll   —— #US 堆逐偏移比对
  2. *.assets 的原版备份   vs  当前文件           —— 长度前缀 UTF-8 串比对
输出：
  hackmud-zh-mod/dict/zh.json   { "原文": "译文", ... }
  hackmud-zh-mod/dict/extract_report.txt
"""
import os, sys, json, re, struct
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.path.insert(0, r"C:\Users\DR\Downloads\DSH\hackmud-zh\recon")
from il_logic_gate import us_map

GD = r"C:\Program Files (x86)\Steam\steamapps\common\hackmud\hackmud_win_Data"
RECON = r"C:\Users\DR\Downloads\DSH\hackmud-zh\recon"
OUTDIR = r"C:\Users\DR\Downloads\DSH\hackmud-zh-mod\dict"

# 配对：原版 = 游戏目录里的零售备份；补丁版 = hackmud-zh/recon 的真源。
# （游戏目录已回滚成零售原版，所以"原版"这一侧取 Managed\Core.dll.bak。）
PAIRS = [
    (os.path.join(GD, r"Managed\Core.dll.bak"),
     os.path.join(RECON, "Core_zh.dll")),
]
# 资产：原版用零售备份，补丁版用 recon 真源
ASSET_PAIRS = [
    (os.path.join(GD, "level0.bak-boot-20260929-124456"),
     os.path.join(RECON, "level0_zh")),
    (os.path.join(GD, "sharedassets0.assets.bak-startup-20260929-122227"),
     os.path.join(RECON, "sharedassets0_zh.assets")),
]


def clean(s):
    """去掉为对齐补的尾部空格与结尾哨兵。"""
    return s.rstrip("\x00").rstrip()


def from_core():
    pairs = {}
    for orig, patched in PAIRS:
        if not (os.path.exists(orig) and os.path.exists(patched)):
            print("  跳过（缺文件）:", orig, patched); continue
        ho = us_map(orig)
        hp = us_map(patched)
        print("  原版 %d 条 / 补丁版 %d 条" % (len(ho), len(hp)))
        for off, po in hp.items():
            oo = ho.get(off)
            if oo is None or po is None or oo == po:
                continue
            a, b = clean(oo), clean(po)
            if not a or not b or a == b:
                continue
            # 必须真是"英文 -> 中文"的变化
            if not re.search(r"[A-Za-z]", a):
                continue
            if not re.search(r"[\u4e00-\u9fff]", b):
                continue
            pairs[a] = b
    return pairs


def iter_lenstr(d):
    i = 0
    while i <= len(d) - 4:
        ln = struct.unpack_from("<i", d, i)[0]
        if 0 < ln <= 8000 and i + 4 + ln <= len(d):
            try:
                s = d[i + 4:i + 4 + ln].decode("utf-8")
                if s:
                    yield i, s
                    i += 4 + ln
                    while i % 4:
                        i += 1
                    continue
            except Exception:
                pass
        i += 1


def from_assets():
    pairs = {}
    for orig, patched in ASSET_PAIRS:
        if not (os.path.exists(orig) and os.path.exists(patched)):
            print("  跳过（缺文件）:", os.path.basename(orig)); continue
        a = open(orig, "rb").read()
        b = open(patched, "rb").read()
        if len(a) != len(b):
            print("  跳过（大小不同）:", os.path.basename(orig)); continue
        sa = {off: s for off, s in iter_lenstr(a)}
        sb = {off: s for off, s in iter_lenstr(b)}
        n = 0
        for off, s1 in sa.items():
            s2 = sb.get(off)
            if s2 is None or s1 == s2:
                continue
            x, y = clean(s1), clean(s2)
            if not x or not y or x == y:
                continue
            if not re.search(r"[A-Za-z]", x) or not re.search(r"[\u4e00-\u9fff]", y):
                continue
            pairs[x] = y
            n += 1
        print("  %s: %d 条" % (os.path.basename(orig), n))
    return pairs


def main():
    os.makedirs(OUTDIR, exist_ok=True)
    print("== 从 Core.dll 提取 ==")
    core = from_core()
    print("  -> %d 条" % len(core))
    print("== 从 Unity 资产提取 ==")
    assets = from_assets()
    print("  -> %d 条" % len(assets))

    allp = {}
    allp.update(core)
    allp.update(assets)
    # 注意：这里**不能**按长度过滤！
    # DLL 里的帮助文本是「一整条含 \n 的长串」（最长 2000+ 字符），
    # 早期版本加了 len<=400 的上限，把这些整串全丢了 ——
    # 于是多行展开也就无从谈起，帮助文本永远译不出来。
    # 长串交由 merge_dict.py 的 expand_lines() 拆成单行词条。
    allp = {k: v for k, v in allp.items() if len(k) >= 1}

    out = os.path.join(OUTDIR, "zh.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(allp, f, ensure_ascii=False, indent=1, sort_keys=True)
    print()
    print("合并后 %d 条 -> %s" % (len(allp), out))

    rep = os.path.join(OUTDIR, "extract_report.txt")
    with open(rep, "w", encoding="utf-8") as f:
        f.write("原文 -> 译文 提取报告（%d 条）\n" % len(allp))
        f.write("=" * 100 + "\n")
        for k in sorted(allp):
            f.write("%r\n    -> %r\n" % (k, allp[k]))
    print("报告 ->", rep)
    print()
    print("样例（前 12 条）:")
    for k in sorted(allp)[:12]:
        print("   %-56r -> %r" % (k[:56], allp[k][:40]))


if __name__ == "__main__":
    main()
