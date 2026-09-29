# -*- coding: utf-8 -*-
"""show_ocr.py — 打印某次 OCR 报告里汉字最多的那一帧（去空格后匹配）。"""
import re, sys, os
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

P = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\DR\AppData\Local\Temp\hz-now\ocr.txt"
TOP = int(sys.argv[2]) if len(sys.argv) > 2 else 34

if not os.path.exists(P):
    print("报告不存在:", P); sys.exit(1)

txt = open(P, encoding="utf-8").read()
parts = re.split(r"=====\s+(\S+\.png)\s+lines=(\d+)\s+cjk=(\d+)\s+=====", txt)
frames = []
for i in range(1, len(parts), 4):
    frames.append((parts[i], parts[i + 1], parts[i + 2], parts[i + 3]))

if not frames:
    print("未解析出帧"); sys.exit(1)

print("帧数: %d" % len(frames))
for nm, ln, cjk, body in frames:
    n = len(re.findall(r"[\u4e00-\u9fff]", body))
    print("  %-10s lines=%-4s cjk标签=%-4s 实际汉字=%d" % (nm, ln, cjk, n))

best = max(frames, key=lambda f: len(re.findall(r"[\u4e00-\u9fff]", f[3])))
print()
print("=== 汉字最多的一帧: %s ===" % best[0])
for L in [x for x in best[3].strip().split("\n") if x.strip()][:TOP]:
    print("   |" + L)
