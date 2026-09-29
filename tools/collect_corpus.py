# -*- coding: utf-8 -*-
"""collect_corpus.py — 从客户端终端历史里提取「渲染后文本」，作为模组词典的真实语料。

为什么 shell.txt 是最佳语料：
  它保存的就是**已经渲染到界面上的文本**（含 <color=...> 标签），
  与模组在 TMP_Text.set_text 处收到的东西完全同构 —— 不需要任何推测。

输出：
  hackmud-zh-mod/dict/corpus_en.txt    去重后的英文行（含服务器下发）
  hackmud-zh-mod/dict/corpus_stat.txt  统计
"""
import os, re, sys, collections
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

APPDATA = os.path.join(os.environ["APPDATA"], "hackmud")
SOURCES = [
    os.path.join(APPDATA, "shell.txt"),
    os.path.join(APPDATA, "shell.txt.bak-en-history-20260928-183537"),
]
OUT = r"C:\Users\DR\Downloads\DSH\hackmud-zh-mod\dict"

TAG = re.compile(r"</?color(?:=#[0-9A-Fa-f]{8})?>")
PLACEHOLDER = re.compile(r"[\u00c8\u00c9]")   # È É 是客户端高亮占位符


def strip_tags(s):
    return TAG.sub("", s)


def main():
    os.makedirs(OUT, exist_ok=True)
    lines = collections.Counter()
    per_src = collections.Counter()

    for src in SOURCES:
        if not os.path.exists(src):
            print("  跳过（不存在）:", src); continue
        txt = open(src, encoding="utf-8", errors="replace").read()
        n = 0
        for raw in txt.split("\n"):
            s = strip_tags(raw).strip()
            if not s:
                continue
            # 只要含英文单词的行
            if not re.search(r"[A-Za-z]{2,}", s):
                continue
            # 丢掉纯命令回显（以 >>> 开头）
            if s.startswith(">>"):
                continue
            lines[s] += 1
            n += 1
        per_src[os.path.basename(src)] = n
        print("  %-46s %d 行" % (os.path.basename(src), n))

    print()
    print("去重后英文行: %d" % len(lines))

    # 分类：疑似服务器下发 vs 客户端
    serverish = []
    clientish = []
    for s in lines:
        # 服务器文案特征：句子长、含句号/逗号、非代码
        if len(s) > 40 and not s.startswith((">", "|", "#")) and not re.search(r"[{}]", s):
            serverish.append(s)
        else:
            clientish.append(s)

    p1 = os.path.join(OUT, "corpus_en.txt")
    with open(p1, "w", encoding="utf-8") as f:
        f.write("# 疑似服务器下发 / 长文案（%d 条）\n" % len(serverish))
        for s in sorted(serverish):
            f.write(s + "\n")
        f.write("\n# 短文本 / 客户端（%d 条）\n" % len(clientish))
        for s in sorted(clientish):
            f.write(s + "\n")
    print("语料 ->", p1)

    p2 = os.path.join(OUT, "corpus_stat.txt")
    with open(p2, "w", encoding="utf-8") as f:
        f.write("来源统计: %s\n\n" % dict(per_src))
        f.write("=== 长文案（疑似服务器下发，前 60 条）===\n")
        for s in sorted(serverish)[:60]:
            f.write("  %r\n" % s)
        f.write("\n=== 短文本（前 60 条）===\n")
        for s in sorted(clientish)[:60]:
            f.write("  %r\n" % s)
    print("统计 ->", p2)
    print()
    print("=== 长文案样例（前 18 条）===")
    for s in sorted(serverish)[:18]:
        print("   %s" % s[:100])


if __name__ == "__main__":
    main()
