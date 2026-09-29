# -*- coding: utf-8 -*-
"""find_bare_words.py — 专找 Core.dll 里「词典没有的**裸单词**」。

为什么单独做：
  上一版 find_missing.py 用 NOISE 正则把「无空格无标点的单词」当标识符排除了，
  但 UI 文本里恰恰有大量单词型文案（Received / CONNECTING / Unauthorized / Loading …）。
  这是我在这个项目里**第二次**栽在同一类过滤上，所以单独写一个工具兜底。

输出：dict/bare_words.txt —— 按「像不像给玩家看的文案」分三档
  H 高嫌疑：全大写、或首字母大写的普通英文词（云 cai 大概率是 UI 文案）
  M 中嫌疑：小写单词
  L 低嫌疑：明显是键名/缩写/技术标识
"""
import os, re, sys, json, collections
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.path.insert(0, r"C:\Users\DR\Downloads\DSH\hackmud-zh\recon")
from il_logic_gate import us_map

GD = r"C:\Program Files (x86)\Steam\steamapps\common\hackmud\hackmud_win_Data"
RECON = r"C:\Users\DR\Downloads\DSH\hackmud-zh\recon"
ROOT = r"C:\Users\DR\Downloads\DSH\hackmud-zh-mod\dict"
CORE_RETAIL = os.path.join(GD, r"Managed\Core.dll.bak")

# 明显的技术标识：含下划线、点号、或纯十六进制、或长度<2
TECHY = re.compile(r"[._]|^[0-9A-Fa-f]{4,}$|^[A-Z]{1,2}$")

# 已知的禁译白名单（命令名/脚本名/协议键/引擎名）
KNOWN_KEEP = set("""
success ok err error true false null none yes no
help user create_user retire_user clear shutdown
accts autos scripts users sys corps chats gui escrow market kernel binmat kiddie_pool bbs trust risk
marks scripts accts chats gui sys market corp store trade
FULLSEC MIDSEC HIGHSEC LOWSEC NULLSEC PRIVATE PUBLIC
GC VU
Unity UnityEngine TMPro TextMeshPro
OnFocus OnLayout OnShutdown OnSwitchUser
Authorization Content-Type
""".split())

CODEY = re.compile(r"^(?:[a-z]+_)+[a-z]+$|^[A-Z][a-z]+[A-Z][A-Za-z]*$")


def main():
    d = json.load(open(os.path.join(ROOT, "zh.json"), encoding="utf-8"))
    keys = set(d.keys())
    keynorm = set(re.sub(r"\s+", " ", k).strip() for k in keys)
    keylower = set(k.lower() for k in keys)
    print("词典 %d 条" % len(d))

    h = us_map(CORE_RETAIL)
    bare = collections.Counter()
    for off, v in h.items():
        if not v:
            continue
        t = v.strip()
        if not t or len(t) < 2:
            continue
        # 只保留「不含空格、不含标点」的裸词
        if " " in t or "\n" in t:
            continue
        if re.search(r"[^\w'-]", t):        # 有其它标点就不算裸词
            continue
        if not re.search(r"[A-Za-z]{2}", t):
            continue
        bare[t] += 1

    print("Core.dll 裸词总数: %d" % len(bare))

    missing = []
    for w in bare:
        if w in keys or re.sub(r"\s+", " ", w) in keynorm or w.lower() in keylower:
            continue
        if w in KNOWN_KEEP:
            continue
        missing.append(w)

    H, M, L = [], [], []
    for w in missing:
        if TECHY.search(w) or CODEY.match(w):
            L.append(w)
        elif w.isupper() or (w[:1].isupper() and w[1:].islower()):
            H.append(w)
        else:
            M.append(w)

    out = os.path.join(ROOT, "bare_words.txt")
    with open(out, "w", encoding="utf-8") as f:
        f.write("# Core.dll 里词典未覆盖的裸单词（已排除已知禁译项）\n")
        f.write("# 共 %d 个：H 高嫌疑 %d / M 中嫌疑 %d / L 低嫌疑 %d\n\n"
                % (len(missing), len(H), len(M), len(L)))
        f.write("=" * 80 + "\n## H 高嫌疑（全大写或首字母大写）—— 优先人工判断\n" + "=" * 80 + "\n")
        for w in sorted(H):
            f.write("%-40s x%d\n" % (w, bare[w]))
        f.write("\n" + "=" * 80 + "\n## M 中嫌疑（小写单词）\n" + "=" * 80 + "\n")
        for w in sorted(M):
            f.write("%-40s x%d\n" % (w, bare[w]))
        f.write("\n" + "=" * 80 + "\n## L 低嫌疑（技术标识）\n" + "=" * 80 + "\n")
        for w in sorted(L):
            f.write("%-40s x%d\n" % (w, bare[w]))

    print("未覆盖裸词: %d（H %d / M %d / L %d）" % (len(missing), len(H), len(M), len(L)))
    print()
    print("=== H 高嫌疑 ===")
    for w in sorted(H):
        print("   %-34s x%d" % (w, bare[w]))
    print()
    print("=== M 中嫌疑（前 60）===")
    for w in sorted(M)[:60]:
        print("   %-34s x%d" % (w, bare[w]))
    print()
    print("清单 ->", out)


if __name__ == "__main__":
    main()
