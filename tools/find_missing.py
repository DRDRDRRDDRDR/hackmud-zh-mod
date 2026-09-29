# -*- coding: utf-8 -*-
"""find_missing.py — 遍历游戏代码里的**全部**字符串，找出词典完全没覆盖的。

与之前的差异：
  以前靠 `%APPDATA%\\hackmud\\shell.txt` 语料 —— 那只覆盖**玩家恰好触发过**的内容。
  本工具改为**直接遍历代码**：Core.dll 的整个 #US 堆 + Unity 资产里的长度前缀串，
  再与词典逐条比对，因此覆盖面与"跑过多少流程"无关。

分类：
  A 完全未覆盖 —— 既不是词典的精确 key，也没有任何词典 key 是它的子串
  B 部分覆盖   —— 词典里有 key 是它的子串（只译了片段）
  C 已精确覆盖 —— 词典有同名 key

输出：dict/missing.txt（A + B，供补词条）、dict/coverage.txt（统计）
"""
import os, re, sys, json, struct, collections
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.path.insert(0, r"C:\Users\DR\Downloads\DSH\hackmud-zh\recon")
from il_logic_gate import us_map

GD = r"C:\Program Files (x86)\Steam\steamapps\common\hackmud\hackmud_win_Data"
RECON = r"C:\Users\DR\Downloads\DSH\hackmud-zh\recon"
ROOT = r"C:\Users\DR\Downloads\DSH\hackmud-zh-mod\dict"

CORE_RETAIL = os.path.join(GD, r"Managed\Core.dll.bak")
ASSETS_RETAIL = [
    os.path.join(GD, "level0.bak-boot-20260929-124456"),
    os.path.join(GD, "sharedassets0.assets.bak-startup-20260929-122227"),
    os.path.join(GD, "resources.assets.bak"),
]

# 明显不是给玩家看的：URL / 证书 / 资源名 / 单标识符 / 格式化模板等
NOISE = re.compile(
    r"^(https?://|/|\\|CN=|OU=)|\.(json|dll|exe|png|ttf|txt|cs|mat|prefab|asset|xml|log)$"
    r"|^[A-Za-z0-9_.]+$|^[\s\W]*$", re.I)


def iter_lenstr(d):
    i = 0
    while i <= len(d) - 4:
        ln = struct.unpack_from("<i", d, i)[0]
        if 0 < ln <= 20000 and i + 4 + ln <= len(d):
            try:
                s = d[i + 4:i + 4 + ln].decode("utf-8")
                if s:
                    yield s
                    i += 4 + ln
                    while i % 4:
                        i += 1
                    continue
            except Exception:
                pass
        i += 1


def interesting(s):
    """是否值得汉化（含英文单词、像给人看的文本、且不是二进制垃圾）"""
    t = s.strip()
    if len(t) < 3 or len(t) > 320:
        return False
    # 必须是可打印文本：不允许控制字符，非可打印占比要极低
    bad = 0
    for c in t:
        o = ord(c)
        if c in "\n\t":
            continue
        if o < 32 or o == 127:
            bad += 1
    if bad:
        return False
    if not re.search(r"[A-Za-z]{2,}", t):
        return False
    if re.search(r"[\u4e00-\u9fff]", t):     # 已含中文：是译文或原文本身含中文
        return False
    # 客户端富文本像素图（开场动画的填充块）——整串都是 ¡ 与空格
    core = re.sub(r"</?color(?:=#[0-9A-Fa-f]{8})?>", "", t)
    core = core.replace("¡", "").replace("◢", "").strip()
    if len(core) < 3:
        return False
    if NOISE.search(t):
        return False
    # 至少要有空格/标点，或者长度够长（排除纯标识符）
    if " " not in t and not re.search(r"[.!?,:;()\[\]<>'\"/-]", t):
        return False
    return True


def main():
    os.makedirs(ROOT, exist_ok=True)
    dpath = os.path.join(ROOT, "zh.json")
    d = json.load(open(dpath, encoding="utf-8"))
    print("词典 %d 条" % len(d))
    keys = list(d.keys())
    keynorm = set()
    for k in keys:
        keynorm.add(re.sub(r"\s+", " ", k).strip())

    # ---- 收集游戏代码里的全部字符串 ----
    pool = collections.Counter()
    core_pool = set()
    if os.path.exists(CORE_RETAIL):
        h = us_map(CORE_RETAIL)
        n = 0
        for off, v in h.items():
            if v and interesting(v):
                pool[v.strip()] += 1
                core_pool.add(v.strip())
                n += 1
        print("Core.dll(#US): %d 条候选" % n)
    for a in ASSETS_RETAIL:
        if not os.path.exists(a):
            continue
        b = open(a, "rb").read()
        n = 0
        for s in iter_lenstr(b):
            if interesting(s):
                pool[s.strip()] += 1
                n += 1
        print("%s: %d 条候选" % (os.path.basename(a), n))

    print("去重后待检查: %d 条（其中 Core.dll 客户端代码 %d 条）" % (len(pool), len(core_pool)))

    # ---- 分类 ----
    A, B, C = [], [], 0
    for s in pool:
        if s in d:
            C += 1
            continue
        ns = re.sub(r"\s+", " ", s).strip()
        if ns in keynorm:
            C += 1
            continue
        # 有没有词典 key 是它的子串（部分覆盖）
        hit = None
        for k in keys:
            if len(k) >= 6 and k in s:
                hit = k
                break
        if hit:
            B.append((s, hit))
        else:
            A.append(s)

    print()
    print("A 完全未覆盖 : %d 条（其中 Core.dll 客户端代码 %d 条）"
          % (len(A), len([s for s in A if s in core_pool])))
    print("B 部分覆盖   : %d 条" % len(B))
    print("C 已覆盖     : %d 条" % C)

    A_core = [s for s in A if s in core_pool]
    A_asset = [s for s in A if s not in core_pool]

    with open(os.path.join(ROOT, "missing.txt"), "w", encoding="utf-8") as f:
        f.write("# A 完全未覆盖 —— 需要新增词条\n")
        f.write("# 来源：遍历 Core.dll 的整个 #US 堆 + Unity 资产的串\n")
        f.write("# 生成 %s\n\n" % __import__("datetime").datetime.now().strftime("%Y-%m-%d %H:%M:%S"))
        f.write("=" * 90 + "\n## 来自 Core.dll（客户端代码）: %d 条\n" % len(A_core) + "=" * 90 + "\n")
        for s in sorted(A_core, key=lambda x: -len(x)):
            f.write(s + "\n\n")
        f.write("\n" + "=" * 90 + "\n## 来自 Unity 资产: %d 条\n" % len(A_asset) + "=" * 90 + "\n")
        for s in sorted(A_asset, key=lambda x: -len(x)):
            f.write(s + "\n\n")
        f.write("\n\n# B 部分覆盖（词典里有片段命中）\n")
        for s, k in sorted(B, key=lambda x: -len(x[0]))[:400]:
            f.write("%s\n    <- 片段: %s\n\n" % (s, k))

    with open(os.path.join(ROOT, "coverage.txt"), "w", encoding="utf-8") as f:
        f.write("词典 %d 条\n候选 %d 条（Core.dll %d）\nA 完全未覆盖 %d（Core %d）\nB 部分覆盖 %d\nC 已覆盖 %d\n覆盖比 %.1f%%\n"
                % (len(d), len(pool), len(core_pool), len(A), len(A_core), len(B), C,
                   100.0 * C / max(1, len(pool))))

    print()
    print("=== A 完全未覆盖 · 来自 Core.dll（客户端代码）: %d 条 ===" % len(A_core))
    for s in sorted(A_core, key=lambda x: -len(x))[:40]:
        print("   %s" % s[:120].replace("\n", "\\n"))
    print()
    print("清单 -> %s/missing.txt" % ROOT)


if __name__ == "__main__":
    main()
