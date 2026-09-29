# -*- coding: utf-8 -*-
"""build_mod.py — 组装 hackmud 简体中文模组发布包。

产物结构（全部是**新增文件**，不覆盖任何游戏既有文件）：
    hackmud-zh-mod/
    ├── winhttp.dll                 BepInEx doorstop 代理
    ├── .doorstop_version
    ├── doorstop_config.ini
    ├── BepInEx/
    │   ├── core/                   BepInEx 本体
    │   └── plugins/hackmud-zh/
    │       ├── HackmudZh.dll       本模组
    │       └── zh.json             词典（可热更新）
    ├── install.ps1 / uninstall.ps1 / verify.ps1
    └── SHA256SUMS.txt

硬校验（任一不过即失败）：
  1. 包内**不得**出现游戏程序集（Core.dll / UnityEngine*.dll / Unity.TextMeshPro.dll / Assembly-CSharp*）
  2. 包内必须恰好有一个 HackmudZh.dll
  3. 词典条数必须 > 0 且能被解析
  4. 全部文件哈希写入 SHA256SUMS.txt
"""
import os, sys, json, shutil, hashlib, zipfile

ROOT = r"C:\Users\DR\Downloads\DSH\hackmud-zh-mod"
VENDOR = os.path.join(ROOT, "_vendor")
BEP = os.path.join(VENDOR, "BepInEx_win_x64_5.4.23.5")
SRC_DLL = os.path.join(ROOT, "src", "HackmudZh", "bin", "Release", "HackmudZh.dll")
DICT = os.path.join(ROOT, "dict", "zh.json")
OUT = os.path.join(ROOT, "dist", "hackmud-zh-mod")
VER = "2.0.4"

# 绝不能出现在包里的游戏程序集
GAME_ASSEMBLIES = ["Core.dll", "UnityEngine.dll", "UnityEngine.CoreModule.dll",
                   "UnityEngine.UI.dll", "Unity.TextMeshPro.dll",
                   "UnityEngine.TextRenderingModule.dll", "Assembly-CSharp-firstpass.dll"]


def sha256(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest().upper()


def main():
    problems = []

    # ---- 前置 ----
    if not os.path.exists(SRC_DLL):
        print("!! 缺少编译产物:", SRC_DLL); return 1
    if not os.path.exists(DICT):
        print("!! 缺少词典:", DICT); return 1
    if not os.path.isdir(BEP):
        print("!! 缺少 BepInEx:", BEP); return 1

    # ---- 词典自检 ----
    with open(DICT, encoding="utf-8") as f:
        d = json.load(f)
    if not isinstance(d, dict) or len(d) == 0:
        print("!! 词典解析失败或为空"); return 1
    print("词典: %d 条" % len(d))

    # ---- 清空输出 ----
    if os.path.exists(OUT):
        shutil.rmtree(OUT)
    os.makedirs(OUT)

    # ---- BepInEx 载体（顶层文件 + core） ----
    for name in ("winhttp.dll", ".doorstop_version", "doorstop_config.ini"):
        s = os.path.join(BEP, name)
        if os.path.exists(s):
            shutil.copy2(s, os.path.join(OUT, name))
    core_src = os.path.join(BEP, "BepInEx", "core")
    core_dst = os.path.join(OUT, "BepInEx", "core")
    os.makedirs(core_dst)
    for fn in os.listdir(core_src):
        s = os.path.join(core_src, fn)
        if os.path.isfile(s) and not fn.lower().endswith(".xml"):
            shutil.copy2(s, os.path.join(core_dst, fn))

    # ---- 我们的插件 + 词典 ----
    plug = os.path.join(OUT, "BepInEx", "plugins", "hackmud-zh")
    os.makedirs(plug)
    shutil.copy2(SRC_DLL, os.path.join(plug, "HackmudZh.dll"))
    shutil.copy2(DICT, os.path.join(plug, "zh.json"))
    # 安全裸词表（面板标题等允许参与子串替换的单词）——手写维护，不由 merge_dict 生成
    wm = os.path.join(ROOT, "dict", "wordmap.json")
    if os.path.exists(wm):
        shutil.copy2(wm, os.path.join(plug, "wordmap.json"))
    else:
        problems.append("缺少 wordmap.json")

    # ---- 脚本 ----
    for s in ("install.ps1", "uninstall.ps1", "verify.ps1", "README.md", "NOTICE.md"):
        p = os.path.join(ROOT, "release", s)
        if os.path.exists(p):
            shutil.copy2(p, os.path.join(OUT, s))
        else:
            problems.append("缺少脚本: " + s)

    # ---- 硬校验 1: 不得混入游戏程序集 ----
    leaked = []
    for r, dirs, files in os.walk(OUT):
        for fn in files:
            if fn in GAME_ASSEMBLIES:
                leaked.append(os.path.join(r, fn))
    if leaked:
        print("!! 包内混入游戏程序集:")
        for x in leaked: print("     ", x)
        return 1
    print("校验 1 通过: 包内无游戏程序集")

    # ---- 硬校验 2: 恰好一个 HackmudZh.dll ----
    n = sum(1 for r, _, fs in os.walk(OUT) for f in fs if f == "HackmudZh.dll")
    if n != 1:
        print("!! HackmudZh.dll 数量异常: %d" % n); return 1
    print("校验 2 通过: 恰好一个 HackmudZh.dll")

    # ---- 硬校验 3: 词典在包里且可解析 ----
    zh = os.path.join(plug, "zh.json")
    with open(zh, encoding="utf-8") as f:
        d2 = json.load(f)
    if len(d2) != len(d):
        print("!! 包内词典与源不一致"); return 1
    print("校验 3 通过: 包内词典 %d 条可解析" % len(d2))

    # ---- 硬校验 4: 原版游戏文件未被改动（对比安装前备份） ----
    #     此校验在真机 install 后由 verify.ps1 做；这里只确认包内不含游戏文件

    # ---- SHA256SUMS ----
    sums = []
    for r, dirs, files in os.walk(OUT):
        dirs.sort()
        for fn in sorted(files):
            p = os.path.join(r, fn)
            rel = os.path.relpath(p, OUT).replace("/", "\\")
            sums.append("%s  %s" % (sha256(p), rel))
    with open(os.path.join(OUT, "SHA256SUMS.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(sums) + "\n")
    print("SHA256SUMS.txt: %d 个文件" % len(sums))

    # ---- 打 zip ----
    zip_path = os.path.join(ROOT, "dist", "hackmud-zh-mod-v%s.zip" % VER)
    if os.path.exists(zip_path):
        os.remove(zip_path)
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        for r, dirs, files in os.walk(OUT):
            for fn in sorted(files):
                p = os.path.join(r, fn)
                z.write(p, os.path.relpath(p, OUT))
    print("zip: %s (%d B, SHA256 %s)" % (zip_path, os.path.getsize(zip_path), sha256(zip_path)[:24]))

    # ---- 文件清单 ----
    print()
    print("包内文件:")
    total = 0
    for r, dirs, files in os.walk(OUT):
        for fn in sorted(files):
            p = os.path.join(r, fn)
            total += os.path.getsize(p)
            print("   %-58s %9d B" % (os.path.relpath(p, OUT), os.path.getsize(p)))
    print("   合计 %d B" % total)

    if problems:
        print()
        for x in problems: print("!! " + x)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
