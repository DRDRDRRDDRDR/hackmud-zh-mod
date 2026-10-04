# -*- coding: utf-8 -*-
"""Build a pinned localization inventory from the installed hackmud client.

This is an inventory, not a translation acceptance test. It separates likely
player-facing strings from code, regexes, identifiers, URLs, and command data.
"""
import collections
import hashlib
import json
import os
import re
import struct
import sys
from datetime import datetime, timezone

ROOT = r"C:\Users\DR\Downloads\DSH\hackmud-zh-mod"
GAME = r"C:\Program Files (x86)\Steam\steamapps\common\hackmud"
APPDATA = os.path.join(os.environ.get("APPDATA", ""), "hackmud")
RECON = r"C:\Users\DR\Downloads\DSH\hackmud-zh\recon"
sys.path.insert(0, RECON)
from il_logic_gate import us_map

OUT = os.path.join(ROOT, "dict")
CORE = os.path.join(GAME, "hackmud_win_Data", "Managed", "Core.dll")
ASSETS = [
    os.path.join(GAME, "hackmud_win_Data", "level0"),
    os.path.join(GAME, "hackmud_win_Data", "sharedassets0.assets"),
    os.path.join(GAME, "hackmud_win_Data", "resources.assets"),
]
SHELLS = [os.path.join(APPDATA, "shell.txt"),
          os.path.join(APPDATA, "shell.txt.bak-en-history-20260928-183537")]
TAG = re.compile(r"</?color(?:=#[0-9A-Fa-f]{8})?>")
URL = re.compile(r"^(?:https?://|mailto:|/|\\|CN=|OU=)", re.I)
IDENT = re.compile(r"^[A-Za-z0-9_.:#$@+\-/%\\]+$")
CODE = re.compile(r"[{}]|\\n|\\r|\\d\s*[+*?]|\(\?:|\[\\d|Newtonsoft|JObject|^\^|\$[0-9]")
UNITY_META = re.compile(
    r"(?:^|[\\s-])(?:Bevel|Bokeh|Bump|Clip Rect|Color Mask|Diffuse|Downsample|"
    r"Edge Softness|Face (?:Color|Dilate|Texture|UV)|Fill (?:Color|Texture)|"
    r"Font (?:Material|Texture)|LineBreaking|Mask Coordinates|LiberationSans|"
    r"Main Camera|Default Style Sheet|Shader|SDF|STEREO_|FOG_|UnityPer)", re.I)
ASSET_SOURCES = {"level0", "sharedassets0.assets", "resources.assets"}


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest().upper()


def iter_lenstr(data):
    i = 0
    while i <= len(data) - 4:
        ln = struct.unpack_from("<i", data, i)[0]
        if 0 < ln <= 20000 and i + 4 + ln <= len(data):
            try:
                s = data[i + 4:i + 4 + ln].decode("utf-8")
                if s:
                    yield s
                    i += 4 + ln
                    while i % 4:
                        i += 1
                    continue
            except UnicodeDecodeError:
                pass
        i += 1


def clean(s):
    return TAG.sub("", s).replace("\x00", "").strip()


def classify(s):
    """Classify only printable, user-facing candidates; assets contain binary noise."""
    if not s or len(s) < 2 or len(s) > 20000:
        return "non-display"
    if any((ord(ch) < 32 and ch not in "\n\r\t") or ord(ch) == 127 for ch in s):
        return "non-display"
    # Length-prefixed asset scanning can land inside arbitrary binary blobs.
    printable = sum(ch.isprintable() or ch in "\n\r\t" for ch in s)
    if printable < len(s) * 0.98:
        return "non-display"
    if not re.search(r"[A-Za-z]{2,}", s):
        return "non-english"
    if URL.search(s) or CODE.search(s) or IDENT.fullmatch(s):
        return "protected-or-code"
    # Shader/property/resource names are not display text even when separated by spaces.
    if (UNITY_META.search(s)
            or re.search(r"(?:^|[\s])(?:_MainTex|Hidden/|CanUseSpriteAtlas)", s)
            or s.count("_") > 4 and s.count(" ") < 3):
        return "non-display"
    if " " not in s and len(s) < 4:
        return "protected-or-code"
    return "candidate"


def classify_source(category, sources):
    """Downgrade candidate strings found only in serialized asset files."""
    if category == "candidate" and set(sources).issubset(ASSET_SOURCES):
        return "asset-review"
    return category


def self_test():
    """Regression checks for display/code and source-aware classification."""
    assert classify("Player received 10 GC") == "candidate"
    assert classify("Shader Graph") == "non-display"
    assert classify("Main Camera") == "non-display"
    assert classify("marks.available") == "protected-or-code"
    assert classify("line\nnext") == "candidate"
    assert classify("line\x00next") == "non-display"
    assert classify_source("candidate", {"resources.assets"}) == "asset-review"
    assert classify_source("candidate", {"resources.assets", "shell.txt"}) == "candidate"
    assert classify_source("protected-or-code", {"resources.assets"}) == "protected-or-code"
    print("inventory self-test: PASS")


def main():
    if "--self-test" in sys.argv:
        self_test()
        return
    os.makedirs(OUT, exist_ok=True)
    sources = []
    pool = collections.defaultdict(set)
    if os.path.exists(CORE):
        for value in us_map(CORE).values():
            value = clean(value)
            if value:
                pool[value].add("Core.dll#US")
        sources.append({"path": CORE, "kind": "Core.dll#US", "sha256": sha256(CORE)})
    for path in ASSETS:
        if not os.path.exists(path):
            continue
        data = open(path, "rb").read()
        for value in iter_lenstr(data):
            value = clean(value)
            if value:
                pool[value].add(os.path.basename(path))
        sources.append({"path": path, "kind": "Unity asset", "sha256": sha256(path)})
    for path in SHELLS:
        if not os.path.exists(path):
            continue
        for line in open(path, encoding="utf-8", errors="replace"):
            value = clean(line)
            if value:
                pool[value].add(os.path.basename(path))
        sources.append({"path": path, "kind": "rendered shell history", "sha256": sha256(path)})

    dict_path = os.path.join(OUT, "zh.json")
    dictionary = json.load(open(dict_path, encoding="utf-8")) if os.path.exists(dict_path) else {}
    rows = []
    counts = collections.Counter()
    namespace_rows = []
    namespace_counts = collections.Counter()
    normalized_keys = {re.sub(r"\s+", " ", key).strip() for key in dictionary}
    for text in sorted(pool):
        category = classify(text)
        # Asset metadata is useful for audit, but is not a translation candidate.
        category = classify_source(category, pool[text])
        matched = text in dictionary or re.sub(r"\s+", " ", text).strip() in normalized_keys
        status = "dictionary-key-match" if matched else "needs-review"
        if category in ("non-display", "non-english"):
            status = "not-english-display-candidate"
        counts[(category, status)] += 1
        row = {"text": text, "sources": sorted(pool[text]), "category": category,
               "status": status, "length": len(text)}
        rows.append(row)
        namespaces = sorted(set(re.findall(r"(?i)\b(marks|risk|trust)\.([a-z_][a-z0-9_]*)", text)))
        if namespaces:
            namespace_rows.append(dict(row, namespaces=["%s.%s" % pair for pair in namespaces],
                                       translated=text in dictionary))
            for pair in namespaces:
                namespace_counts["%s.%s" % pair] += 1

    manifest = {"generated_utc": datetime.now(timezone.utc).isoformat(),
                "game_root": GAME, "dictionary": dict_path,
                "dictionary_entries": len(dictionary), "unique_strings": len(rows),
                "counts": {"%s/%s" % k: v for k, v in sorted(counts.items())},
                "sources": sources, "strings": rows,
                "namespaces": {"counts": dict(sorted(namespace_counts.items())),
                                "strings": namespace_rows}}
    out_json = os.path.join(OUT, "localization_inventory.json")
    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    out_md = os.path.join(OUT, "localization_inventory.md")
    with open(out_md, "w", encoding="utf-8") as f:
        f.write("# hackmud 汉化盘点\n\n")
        f.write("生成时间（UTC）：%s\n\n" % manifest["generated_utc"])
        f.write("字典条目：%d；去重字符串：%d。\n\n" % (len(dictionary), len(rows)))
        f.write("|类别|状态|数量|\n|---|---|---:|\n")
        for key, value in sorted(counts.items()):
            f.write("|%s|%s|%d|\n" % (key[0], key[1], value))
        f.write("\n## 待审查候选\n\n")
        for row in rows:
            if row["status"] == "needs-review" and row["category"] in ("candidate", "asset-review"): 
                f.write("- `%s`（来源：%s）\n" % (row["text"].replace("`", "\\`"), ", ".join(row["sources"])))
    print("inventory: %d strings, dictionary=%d" % (len(rows), len(dictionary)))
    for key, value in sorted(counts.items()):
        print("  %-24s %d" % ("%s/%s" % key, value))
    print(out_json)
    print(out_md)


if __name__ == "__main__":
    main()
