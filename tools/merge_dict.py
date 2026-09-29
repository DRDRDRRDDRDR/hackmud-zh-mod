# -*- coding: utf-8 -*-
"""merge_dict.py — 合并补充译文进主词典。

关键处理：**带占位符的格式模板在渲染层匹配不到**。
    DLL 里的 `-TERMINAL HEIGHT: {0}-` 到 set_text 时已经变成 `-TERMINAL HEIGHT: 67-`，
    所以整条模板永远不可能命中。这里把它拆成**前后缀片段**：
        `-TERMINAL HEIGHT: `  -> `-终端高度: `
        （`-` 结尾的那段太短且无意义，丢弃）
    这样渲染层就能靠短语替换命中。

规则：
  · 主词典已有的键**不被覆盖**（保持与既有补丁的术语一致）
  · 补充里的新键追加
  · 产物写回 dict/zh.json，并输出合并报告
"""
import os, re, sys, json, collections
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = r"C:\Users\DR\Downloads\DSH\hackmud-zh-mod\dict"
MAIN = os.path.join(ROOT, "zh.json")
SUPS = [os.path.join(ROOT, "zh_supplement.json"),
        os.path.join(ROOT, "zh_supplement2.json"),
        os.path.join(ROOT, "zh_supplement3.json")]
REPORT = os.path.join(ROOT, "merge_report.txt")

PH = re.compile(r"\{[0-9]\}")

# 会被客户端**语法高亮**成独立 token 的东西：
#   `X  颜色/样式码（`C `V `M …）
#   marks.available  之类的脚本名（含点号）
TOKENIZE = re.compile(r"(`[A-Za-z]|[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+)")


def split_segments(s):
    """切成 [纯文本, token, 纯文本, token, ..., 纯文本]（偶数下标 = 纯文本段）"""
    out, last = [], 0
    for m in TOKENIZE.finditer(s):
        out.append(s[last:m.start()])
        out.append(m.group(0))
        last = m.end()
    out.append(s[last:])
    return out


def plain_fragments(key, val):
    """把「被高亮 token 拆开」的词条救回来。

    为什么需要：渲染后 `marks.available` 会被包上 <color> 标签，
    于是整行与词典 key 对不上，整条词条失效。
    但**纯文本段**在两边都是一样的连续子串，可以单独作为短语词条注册。
    """
    ks = split_segments(key)
    vs = split_segments(val)
    if len(ks) != len(vs) or len(ks) < 3:
        return []
    out = []
    for i in range(0, len(ks), 2):
        a, b = ks[i], vs[i]
        if len(a.strip()) >= 8 and a != b:
            out.append((a, b))
    return out


def fragments(key, val):
    """把含占位符的模板拆成可用片段。返回 [(k,v), ...]"""
    if not PH.search(key):
        return []
    out = []
    kparts = PH.split(key)
    vparts = PH.split(val) if PH.search(val) else [val]
    # 只处理单个占位符的常见情形
    if len(kparts) == 2 and len(vparts) == 2:
        pre_k, post_k = kparts
        pre_v, post_v = vparts
        if len(pre_k.strip()) >= 4:
            out.append((pre_k, pre_v))
        if len(post_k.strip()) >= 4:
            out.append((post_k, post_v))
    return out


def expand_lines(key, val):
    """把**多行**词条按行拆开。

    为什么必须做：渲染层是**逐行**处理终端缓冲区的，而 DLL 里的帮助文本等
    是「一整条含 \\n 的长串」。整串在缓冲区里永远匹配不到，必须拆成单行词条。
    """
    if "\n" not in key or "\n" not in val:
        return []
    kl = key.split("\n")
    vl = val.split("\n")
    if len(kl) != len(vl):
        return []
    out = []
    for a, b in zip(kl, vl):
        a = a.strip()
        b = b.strip()
        if len(a) >= 8 and a != b:
            out.append((a, b))
    return out


def main():
    main_d = json.load(open(MAIN, encoding="utf-8"))
    print("主词典 %d 条" % len(main_d))

    added_all = []
    frags_all = []
    kept_all = []
    for sup_path in SUPS:
        if not os.path.exists(sup_path):
            print("  跳过（不存在）:", os.path.basename(sup_path)); continue
        sup_d = json.load(open(sup_path, encoding="utf-8"))
        print("  补充 %s: %d 条" % (os.path.basename(sup_path), len(sup_d)))
        for k, v in sup_d.items():
            if PH.search(k):
                for fk, fv in fragments(k, v):
                    if fk not in main_d:
                        main_d[fk] = fv
                        frags_all.append((fk, fv))
                continue
            if k in main_d:
                kept_all.append(k)
                continue
            main_d[k] = v
            added_all.append(k)
            for a, b in expand_lines(k, v):
                if a not in main_d:
                    main_d[a] = b
                    added_all.append(a)

    # ---- 先把主词典自身的多行词条展开成一行的 ----
    line_exp = []
    for k in list(main_d.keys()):
        for a, b in expand_lines(k, main_d[k]):
            if a not in main_d:
                main_d[a] = b
                line_exp.append((a, b))
        # 行内被高亮 token 拆开的，也把纯文本段救回来
        for a, b in plain_fragments(k, main_d[k]):
            if a not in main_d:
                main_d[a] = b
                line_exp.append((a, b))
    print("主词典多行展开 + 行内片段: %d 条" % len(line_exp))

    added, kept, frags = added_all, kept_all, frags_all

    # ---- 去掉「大小写不敏感」的重复键 ----
    # 两个原因：
    #   1. Windows PowerShell 5.1 的 ConvertFrom-Json 把键当大小写不敏感 -> 报「重复键」，
    #      会让 verify.ps1 的词典自检失败；
    #   2. 语义上也冗余（Confirm / confirm 都译成同一个词）。
    seen = {}
    for k in sorted(main_d.keys()):
        lk = k.lower()
        if lk in seen:
            continue
        seen[lk] = k
    dup = len(main_d) - len(seen)
    if dup:
        main_d = dict((k, main_d[k]) for k in seen.values())
    print("大小写去重: 移除 %d 条" % dup)

    json.dump(main_d, open(MAIN, "w", encoding="utf-8"),
              ensure_ascii=False, indent=1, sort_keys=True)

    L = ["词典合并报告", "=" * 96, "",
         "补充新增  : %d 条" % len(added),
         "模板拆片段: %d 条" % len(frags),
         "多行/行内展开: %d 条" % len(line_exp),
         "保留原译  : %d 条（补充未覆盖）" % len(kept),
         "合并后    : %d 条" % len(main_d), "",
         "--- 新增（前 80）---"]
    for k in added[:80]:
        if k in main_d:
            L.append("   %-70r -> %r" % (k[:70], main_d[k][:40]))
    L.append("")
    L.append("--- 多行展开/行内片段 ---")
    for k, v in line_exp[:60]:
        L.append("   %-64r -> %r" % (k[:64], v[:36]))
    L.append("")
    L.append("--- 模板拆出的片段 ---")
    for k, v in frags:
        L.append("   %-40r -> %r" % (k, v))
    L.append("")
    L.append("--- 保留原译（未覆盖）---")
    for k in kept:
        L.append("   %r" % k)
    L.append("")
    L.append("--- 被大小写去重移除的键 ---")
    for lk, k in sorted(seen.items()):
        pass
    L.append("   (共 %d 条)" % dup)
    open(REPORT, "w", encoding="utf-8").write("\n".join(L))

    print("新增 %d / 片段 %d / 展开 %d / 保留 %d / 合并后 %d"
          % (len(added), len(frags), len(line_exp), len(kept), len(main_d)))
    print("报告 ->", REPORT)


if __name__ == "__main__":
    main()
