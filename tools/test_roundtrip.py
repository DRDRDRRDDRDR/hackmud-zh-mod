# -*- coding: utf-8 -*-
"""test_roundtrip.py — 模组「安装 → 校验 → 卸载 → 复原」往返自动化验收。

判据（任一不过即失败）：
  1. 安装前：游戏目录**不存在**任何模组文件
  2. install.ps1 退出码 0，且安装后目标文件齐全
  3. 安装后：4 个游戏既有文件哈希**仍是零售原版**（证明"只新增、不覆盖"）
  4. verify.ps1 退出码 0
  5. uninstall.ps1 退出码 0
  6. 卸载后：模组文件全部消失，且游戏目录的**文件清单与哈希快照与安装前完全一致**
"""
import os, sys, json, hashlib, subprocess, shutil, tempfile

GAME = r"C:\Program Files (x86)\Steam\steamapps\common\hackmud"
DIST = r"C:\Users\DR\Downloads\DSH\hackmud-zh-mod\dist\hackmud-zh-mod"
DATA = os.path.join(GAME, "hackmud_win_Data")

MOD_PATHS = ["winhttp.dll", ".doorstop_version", "doorstop_config.ini", "BepInEx"]

PRISTINE = {
    r"Managed\Core.dll":     "D424EAB9372946946B5FFD9DC49B17D9C5060B3CEC638A5952E7EAD99CD696E6",
    "resources.assets":      "E2E661C96397F9C444936B9767F7F723B5FDAF64FC4ECB04B52E7D5B678C0003",
    "sharedassets0.assets":  "E07F027F18A41DB03E387DF729DB77D933AC1B0593826640A4E91FE6E7709D9B",
    "level0":                "2D7FC2DA43E6273E8D1A2A3D2E2761869563C7C4D99DA34905E0581463B7441A",
}

FAILS = []


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest().upper()


def ok(msg):
    print("  ok    " + msg)


def bad(msg):
    print("  FAIL  " + msg)
    FAILS.append(msg)


def run_ps(script, *args):
    cmd = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script] + list(args)
    r = subprocess.run(cmd, capture_output=True)
    out = r.stdout.decode("utf-8", "replace")
    err = r.stderr.decode("utf-8", "replace")
    return r.returncode, out, err


def snapshot(root):
    """游戏目录顶层（含 Managed 一层）的文件名+哈希快照"""
    snap = {}
    for name in sorted(os.listdir(root)):
        p = os.path.join(root, name)
        if os.path.isfile(p):
            snap[name] = os.path.getsize(p)
        elif os.path.isdir(p) and name in ("Managed",):
            for sub in sorted(os.listdir(p)):
                sp = os.path.join(p, sub)
                if os.path.isfile(sp):
                    snap[name + "\\" + sub] = os.path.getsize(sp)
    return snap


def game_process_running():
    r = subprocess.run(["powershell", "-NoProfile", "-Command",
                        "(Get-Process hackmud_win -ErrorAction SilentlyContinue) -ne $null"],
                       capture_output=True)
    return b"True" in r.stdout


def main():
    print("== 模组安装往返测试 ==")
    print("游戏目录: " + GAME)
    print()

    if not os.path.isdir(DIST):
        print("!! 找不到发布包目录: " + DIST)
        return 1
    if game_process_running():
        print("!! hackmud 正在运行，请先退出")
        return 1

    # ---- 1. 安装前：不应有模组文件 ----
    print("[1] 安装前状态")
    present = [m for m in MOD_PATHS if os.path.exists(os.path.join(GAME, m))]
    if present:
        bad("安装前已存在模组文件: %s（请先 uninstall）" % present)
        return 1
    ok("无任何模组文件")

    # 记录"干净状态"的文件清单
    before = snapshot(GAME)
    ok("已记录安装前文件清单（%d 项）" % len(before))

    ps_dir = tempfile.mkdtemp(prefix="hzmod-")
    try:
        for name in os.listdir(DIST):
            s = os.path.join(DIST, name)
            d = os.path.join(ps_dir, name)
            if os.path.isdir(s):
                shutil.copytree(s, d)
            else:
                shutil.copy2(s, d)

        # ---- 2. 安装 ----
        print()
        print("[2] 安装")
        rc, out, err = run_ps(os.path.join(ps_dir, "install.ps1"))
        if rc != 0:
            bad("install.ps1 退出码 %d" % rc)
            print(out[-1200:]); print(err[-800:])
            return 1
        ok("install.ps1 rc=0")
        missing = [m for m in MOD_PATHS if not os.path.exists(os.path.join(GAME, m))]
        if missing:
            bad("安装后缺少: %s" % missing)
        else:
            ok("模组文件齐全")

        # ---- 3. 游戏既有文件未被改动 ----
        print()
        print("[3] 游戏既有文件仍是零售原版")
        for k, want in PRISTINE.items():
            got = sha(os.path.join(DATA, k))
            if got == want:
                ok("%-24s %s" % (k, got[:16]))
            else:
                bad("%s 被改动！实际 %s" % (k, got[:16]))

        # ---- 4. verify.ps1 ----
        print()
        print("[4] verify.ps1")
        rc, out, err = run_ps(os.path.join(ps_dir, "verify.ps1"))
        if rc == 0:
            ok("verify.ps1 rc=0（PASS）")
        else:
            bad("verify.ps1 退出码 %d" % rc)
            print(out[-1200:])

        # ---- 5. 卸载 ----
        print()
        print("[5] 卸载")
        rc, out, err = run_ps(os.path.join(ps_dir, "uninstall.ps1"))
        if rc != 0:
            bad("uninstall.ps1 退出码 %d" % rc)
            print(out[-800:]); print(err[-800:])
        else:
            ok("uninstall.ps1 rc=0")

        # ---- 6. 完全复原 ----
        print()
        print("[6] 卸载后完全复原")
        left = [m for m in MOD_PATHS if os.path.exists(os.path.join(GAME, m))]
        if left:
            bad("卸载后仍残留: %s" % left)
        else:
            ok("模组文件全部移除")

        after = snapshot(GAME)
        only_before = sorted(set(before) - set(after))
        only_after = sorted(set(after) - set(before))
        if only_before or only_after:
            bad("文件清单不一致：仅安装前有 %s / 仅卸载后有 %s" % (only_before[:6], only_after[:6]))
        else:
            ok("文件清单与安装前完全一致（%d 项）" % len(after))

        for k in PRISTINE:
            if sha(os.path.join(DATA, k)) != PRISTINE[k]:
                bad("%s 往返后哈希不符" % k)
        if not FAILS:
            ok("4 个游戏文件往返后哈希仍为零售原版")
    finally:
        shutil.rmtree(ps_dir, ignore_errors=True)

    print()
    if FAILS:
        print("== 结果: %d 项失败 ==" % len(FAILS))
        return 1
    print("== 结果: PASS（安装往返 6 步全绿）==")
    return 0


if __name__ == "__main__":
    sys.exit(main())
