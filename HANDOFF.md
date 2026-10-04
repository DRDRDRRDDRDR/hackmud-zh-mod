# hackmud 简体中文模组 —— 完全交接文档（HANDOFF）

> 给接手者：这份文档面向**全新的 AI 会话/开发者**。读完它你就能在不翻历史对话的情况下继续工作。
> 历史数字以对应版本记录为准；最后更新：2026-10-01，v2.0.9 词典更新后。

---

## 0. 项目是什么

Steam 游戏 **hackmud**（appid `469920`，Unity `6000.0.59f2` Mono）的**简体中文汉化模组**。
技术路线：**BepInEx 5.4.23.5 + Harmony**，在**渲染层**做文本翻译，**不修改任何游戏既有文件**。

| 关键坐标 | 值 |
|---|---|
| 本地仓库 | `C:\Users\DR\Downloads\DSH\hackmud-zh-mod` |
| GitHub 仓库 | https://github.com/DRDRDRRDDRDR/hackmud-zh-mod （public） |
| 游戏目录 | `C:\Program Files (x86)\Steam\steamapps\common\hackmud` |
| 模组安装位置 | `<游戏目录>\BepInEx\plugins\hackmud-zh\`（三件套：`HackmudZh.dll` + `zh.json` + `wordmap.json`） |
| git HEAD | `0d61994657db941fb212068c553c1ca00505e7a7`（== 远端 main，57 个 tracked 文件） |
| 当前版本 | **v2.0.9**（已重建并接管真实目录；format-2 收据与 verify.ps1 已通过；游戏尚未启动，渲染验收待用户确认） |

**一句话现状**：离线盘点已覆盖字典与候选语料，但真实运行日志仍出现 `binlog`、`scratch`、`sys.status`、`chat` 等待审查项；不能据此宣称零漏译。v2.0.9 已完成真实目录接管和收据校验，但游戏内渲染验收仍待启动后完成。

---

## 1. 交付物

| 交付物 | 位置 | 哈希 |
|---|---|---|
| 安装包 v2.0.9 | `dist/hackmud-zh-mod-v2.0.9.zip`（693,068 B） | SHA256 `6111A1175C5952A0DA06E753C6C170C427FB8A0FAA563325B336A457EBF5D6F9` |
| 主词典 | `dict/zh.json`（**1355 条**，当前工作区实测） | — |
| 安全裸词表 | `dict/wordmap.json`（930 B，**当前为空**，见 §4.3） | — |
| 模组源码 | `src/HackmudZh/`（4 个文件：`Plugin.cs` / `Translator.cs` / `FontFix.cs` / `Sweeper.cs`） | — |
| 工具 | `tools/`（8 个 py + `zhcheck/` C# 验收门） | — |
| Release 历史 | v2.0.0 – v2.0.9 全部在 GitHub Releases | — |

旧版本 zip 也都在 `dist/` 里（v2.0.0 – v2.0.9 共 10 个），**不要删除**（历史交付物）。

---

## 2. 工作原理（架构 —— 接手前必须理解）

### 2.1 翻译链路

```
游戏文本 → TMP_Text.set_text / UnityEngine.UI.Text.set_text（Harmony prefix）
        → Translator.Translate(整段文本)
        → 渲染中文
```

- **挂点**：`TMP_Text.set_text` 是**非抽象基类方法**、`TextMeshProUGUI` 未覆盖它 ⇒ 挂基类即全覆盖（终端是 TMP）。
- **渲染层翻译的独有优势**：底层逻辑拿到的仍是**英文** ⇒ 那些「既显示又参与逻辑」的单副本串
  （`scratch`/`chat`/`time`/`[DEADNODE]` 等）**可安全翻译**；也能覆盖**服务器下发**文本（文件补丁做不到）。
- **Sweeper（`Sweeper.cs`）**：周期遍历 `Resources.FindObjectsOfTypeAll<TMP_Text>()` 与 `UI.Text`，
  翻译**静态 UI 文本** —— 预制体/场景里序列化好的文本**根本不调用 `set_text`**，没有 Sweeper 就永远译不到。
  前 20 秒每秒一轮、之后每 5 秒一轮；写回会再经挂点但中文不再命中 ⇒ 幂等。
- **FontFix（`FontFix.cs`）**：运行时 `Font.CreateDynamicFontFromOSFont("Microsoft YaHei UI")` →
  `TMP_FontAsset.CreateFontAsset(族名, 样式, 字号)`（**正确重载是三个参数**）→ 挂到
  `TMP_Settings.fallbackFontAssets` + 各 `TMP_FontAsset.fallbackFontAssetTable`（4 处）。
  不改任何资源文件；日志显示 `mode=Dynamic, 已注册字形 653`。

### 2.2 词典引擎（`Translator.cs`，27 KB —— 全部核心逻辑在这）

匹配顺序：
1. **整串精确**（`Dict.TryGetValue`）
2. **归一化整串索引**（`_normWhole`，key 空白压平）
3. **整段短语匹配**（`ReplacePhrasesNormalized`）—— 构造「跳过颜色标签 + 空白归一化」的视图，
   记录每个视图字符对应的**原文区间**，命中后替换原文区间 ⇒ 颜色标签与空白差异都不影响匹配。

**四个不可再犯的细节**（每个都是血泪，详见 §4.2）：

| 细节 | 规则 |
|---|---|
| 换行归一化 | **两遍匹配**：第一遍换行**直接删**（硬折行断在单词中间 `mone`/`y.`）；第二遍换行**当空格**（折行落在空格处 `begin a`/`mark`）。第二遍仅在仍有英文时跑。 |
| `\r\n` | 连续 CRLF 算**一个**换行（否则 `a\r\nmark` 变两个空格，永远对不上） |
| 词边界 | **必须在原文上判**（`BoundaryOkOrig`）：原文里紧跟 `\n` = 天然边界放行；右侧紧跟 `.` 或 `_` 拒绝（防 `binmat` 命中 `binmat.log`）。**不能在归一化视图上判** —— 删换行后行尾与下一行行首会粘连，导致整条被误拒。 |
| 标签配平 | 替换区间会**切穿标签对**（`<color=…>mark` 被 key 覆盖、`</color>` 在区间外）。必须用 `IsRichTag` **精确识别真标签**（不能用 `line[t+1]=='/'` 粗判 —— `<mark_name>` 的尖括号会被误当开标签），按差额把标签**补发到译文前**，保住原配色。 |

**性能**：短语表按长度降序 + 首字符索引（`_byFirst`）。行级缓存 `LineMemo`（上限 20000）+ 整段缓存 `WholeMemo`。

**裸词规则**：普通裸词（无空格/标点）**只做整串匹配**，不参与短语替换（防 `from` 误伤 `transform`）。
例外走 `wordmap.json`（当前为空，见 §4.3）。

### 2.3 未译诊断（接手后排查漏译的**唯一入口**）

`Sweeper.NoteUntranslated` 把「译后仍含英文」的渲染层真实文本以 `[未译]` 前缀写进
`<游戏目录>\BepInEx\LogOutput.log`。判定用**逐字符扫描**（跳过所有 `<...>` 区段、只认连续 3 个 ASCII 字母）。

⚠️ **判读日志的三个坑**：
1. 日志里的多行文本块含**裸 CR**（只替换了 `\n`），PowerShell 显示时会按 CR 断行，把一段显示成十几条 —— 判读前先按 `\r` 拆行或逐字符 dump。
2. 不要相信「OCR」和 `%APPDATA%\hackmud\shell.txt`：shell.txt 存的是**未折行**的原文（客户端把 `output` 落盘、把 `wrapped_output` 送去显示），与屏幕不一致。
3. 用 UTF-16 探针在 DLL 里找**方法名**会得到假的「不在」—— .NET 元数据里**方法名/字段名是 UTF-8**、只有**字符串字面量是 UTF-16**。

---

## 3. 验收门与回归（任何改动必须跑）

### 3.1 定点验证

```powershell
$exe = (Get-ChildItem "$R\tools\zhcheck\bin\Release" -Recurse -Filter zhcheck.dll | Select-Object -First 1).FullName
# 逐行：打印 输入 -> 输出
dotnet $exe "$R\dict\zh.json" <测试文件> --show
# 整段：把整个文件当一段文本（复现真机 set_text 行为，测跨折行效果）
dotnet $exe "$R\dict\zh.json" <测试文件> --whole
```

### 3.2 整段语料回归（**改动后的强制步骤**）

```powershell
# 语料三份合并（含补丁前英文历史）
#   1) %APPDATA%\hackmud\shell.txt （当前回滚缓冲，约 1821 行）
#   2) %APPDATA%\hackmud\shell.txt.bak-en-history-* （补丁前英文历史，约 1255 行 —— 早期服务器文本的富矿）
#   去重合并 -> dict/corpus_all.txt
dotnet $exe "$R\dict\zh.json" "$R\dict\corpus_all.txt" --whole | Out-File out.txt -Encoding utf8
```

**回归判据（三条，缺一不可）**：
1. **汉字数不得回退**（当前基线约 6760；与上一版比，涨了才对）
2. **标签配平：输出差值与输入差值一致**（注意：真机语料输入本身就是 `1792/1791` 差 +1 —— 判据是**差值不变**，不是「输出开=输出闭」）
3. **英文词频 triage**：剥标签后按 `[A-Za-z]{3,}` 找仍含英文的行 → 英文词频降序 → 逐条判该不该译

### 3.3 真机验证

```powershell
# 启动游戏后（要提醒用户！见 §7.5），读诊断：
Get-Content "C:\Program Files (x86)\Steam\steamapps\common\hackmud\BepInEx\LogOutput.log" -Encoding UTF8 |
  Select-String '\[未译\]'
# OCR 关键词核对（排除/确认漏译）：
python tools\show_ocr.py
```

---

## 4. 必须遵守的结论（勿重蹈覆辙）

### 4.1 不可译清单（逐条 triage 判定过）

| 类别 | 例子 | 原因 |
|---|---|---|
| 脚本名 | `marks.*` / `accts.*` / `kiddie_pool.*` / `chats.*` / `scripts.get_level` / `risk.reward` | 玩家要照着敲 |
| 命令名 | `help` / `user` / `clear` / `shutdown` / `create_user` | 同上 |
| 命令参数名/数据示例 | `connect` / `prompt` / `input` / `will_comply` / `favorite_number` / `gc_string` / `to` / `amount` / `day` | 同上 |
| 专有名词/单位/协议 | `Steam` / `Trust` / `hackmud` / `GC` / `VU` / `VAC` / `SSL` / `HTTP` / `FULLSEC`/`MIDSEC`/`HIGHSEC` | 保留原文 |
| 游戏内专名 | `Marco Polo` / `BearBuddy(TM)` / `BASIC_BOT` / `robovac_*` / `:::mark名:::` | 同上 |
| 用户名/玩家名 | `drrd` / `givi` / `rynard` / 捐赠榜人名 | 同上 |
| 邮箱/网址 | `support@hackmud.com` | 同上 |
| 玩家自己敲错的串 | `marks.avaliable` / `marks.vertify` | 客户端原样回显 |

**服务器端禁译**（查证结论，报告在 `../hackmud-zh/recon/research_marks.md`）：`marks.*` 全是服务器端
Trust 脚本/mark 名，玩家必须按英文输入；安全等级名（`NULLSEC`…`FULLSEC`）属官方 sandbox 安全边界。

### 4.2 已修掉的问题（版本历史 = 防坑清单）

| 版本 | 修了什么 | 教训 |
|---|---|---|
| v2.0.3 | 静态 UI 文本扫描器（Sweeper）；[未译] 诊断 | 预制体文本不走 `set_text` |
| v2.0.4 | 归一化空白匹配；长句折行（删换行）；`</color>` 配平 | — |
| v2.0.5 | `_normWhole` 不剥标签导致整串索引失效；**撤回裸词表** | 裸词不可安全翻译 |
| v2.0.6 | 换行两种折行风格（两遍匹配）；`\r\n` 当一个换行；词边界改原文判 | 单行测试测不出多行 bug |
| v2.0.7 | 捐赠横幅整句；布尔值模板（`joined_corp:/is_supporter:`） | triage 靠词频 |
| v2.0.8 | `::: earned`；语料并入补丁前英文历史 | 补丁前历史是富矿 |
| v2.0.9 | 诊断自身的色码误报（改逐字符扫描） | 正则编进插件后行为可能不符 |

### 4.3 多译事故（最重要的一条结论）

v2.0.3 我加了 `wordmap.json` 把面板标题 `scratch`/`chat`/`binlog`/`binmat` 译成中文 —— **错的**：
`scratch` 同时是**标记树里的脚本名**（玩家要照着敲），用户截图抓出 `\---------- 草稿`。

**结论：在这个游戏里，裸词无法安全翻译** —— 显示用的词与要输入的命令名共用同一份字符串。
`wordmap.json` 已清空（保留机制与「标识符守卫」），**不要再往里加面板标题类单词**，
除非能证明该词不会同时作为命令/脚本名出现。宁可少译，不能译坏。

---

## 5. 工作流（改词典 / 改代码 / 发版）

### 5.1 改词典（最常用）

1. 写 `dict/zh_supplementN.json`（第 N 批，自增；结构 `{"英文": "中文"}`）
2. `merge_dict.py` 里 `SUPS` 列表追加该文件
3. 跑合并：`python tools\merge_dict.py`（它会：多行展开 + 句子拆分 + 标签片段救回 + 大小写去重 → 重写 `zh.json`）
   - 注意：句子拆分用 **`SENT_EN`（ASCII 句末）拆 key、`SENT_ZH`（中文句末）拆 value** —— 用同一个正则拆两边会导致段数不匹配、整条被跳过（这是 `The second most important thing…` 曾一直只有整句 key 的原因）
4. `zhcheck` 定点 + 整段回归（§3）
5. 词典是 DLL 内嵌 + 外部 JSON 双份：改了 `zh.json` 后**必须重编 DLL**（`dotnet build src\HackmudZh -c Release`）+ 替换游戏目录里两份文件
6. 真机验证（提醒用户）

### 5.2 改引擎代码

- 只改 `src/HackmudZh/*.cs`；`tools/zhcheck` 是**复用同一份 `Translator.cs` 源码**的验收门，改引擎后必须同步重编两者
- 任何改动后跑 §3.2 整段回归（汉字数 + 配平差值 + 词频 triage）

### 5.3 发版（六步，全部跑通才算完）

```powershell
$R='C:\Users\DR\Downloads\DSH\hackmud-zh-mod'
# 1) 版本号三处同步：src\HackmudZh\HackmudZh.csproj (<Version>)、Plugin.cs (Version const)、tools\build_mod.py (VER)
# 2) 编译 + 打包（包内校验：无游戏程序集 / 恰好一个 HackmudZh.dll / 词典可解析）
dotnet build $R\src\HackmudZh -c Release
python $R\tools\build_mod.py
# 3) 安装往返 6 步（先卸载，test_roundtrip 可传 dist 路径参数）
powershell -NoProfile -ExecutionPolicy Bypass -File $R\dist\hackmud-zh-mod\uninstall.ps1
python $R\tools\test_roundtrip.py
powershell -NoProfile -ExecutionPolicy Bypass -File $R\dist\hackmud-zh-mod\install.ps1
# 4) 提交推送（提交信息写 UTF-8 文件再 -F 引用，用绝对路径！）
git add -A; git commit -F <abs-msg-file>; git push origin main
# 5) 发 Release（通用脚本在 hackmud-zh\recon\create_release.ps1，带 -Tag/-Zip/-Notes/-Repo）
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Users\DR\Downloads\DSH\hackmud-zh\recon\create_release.ps1 -Tag v2.0.x -Zip <zip> -Notes <notes.md> -Repo hackmud-zh-mod
# 6) 匿名下载 + 哈希与本地一致 + 用下载包再跑一次往返
```

Release notes 命名：`recon_release_notes_v2.0.x.md`（仓库里已有 10 份模板可参考）。
GitHub API 用 token（`git credential fill` 取），`api.github.com` 被本机 hosts 指向加速器。

---

## 6. 剩余可选工作（非必须，但接手者可能被问）

1. **继续 triage 补词**：语料富矿 = `%APPDATA%\hackmud\shell.txt.bak-en-history-*`（补丁前英文历史，覆盖当前会话看不到的早期服务器文本）。流程同 §5.1。
2. **未决问题：`Success`/`Failure` 翻译是否安全** —— 查证报告有冲突结论（`final_verdict.txt` 判「翻译必失配」vs `generate_vs_compare.txt` 以已实测安全的 `-terminal active-` 为基准判「同构可译」），**需真机 A/B 实测终结**，未定论前不要动。
3. **风险声明（必须对用户保持透明）**：官方 Rules 把 client modification 视为 custom client、`Any detected custom client activity will result in bans`。模组不改游戏文件，但仍是客户端改动，`NOTICE.md` 已载明，风险由使用者承担。

---

## 7. 本机环境坑（Windows + PowerShell 5.1 + 并行任务）

1. **PS 5.1 `Set-Content` 不带 `-Encoding` 会用 ANSI**，直接弄坏源码里的中文（症状：`error CS1010 常量中有换行符`）。改中文文件用 `-Encoding UTF8`，或直接用编辑工具；坏了就 `git checkout` 恢复。
2. **PS 5.1 `ConvertFrom-Json` 键不分大小写**：词典合并时 `Confirm`/`confirm` 会冲突，`merge_dict.py` 已做大小写去重。
3. **`[IO.File]::WriteAllText` 相对路径按进程 CWD 解析**（不是 PowerShell 当前位置）⇒ `git commit -F` 静默失败（只 add 未 commit）。用绝对路径。
4. **`Select-Object -First N` 截断 dotnet 输出会提前关管道杀掉进程**，其写盘产物（gaps.txt 等）会为空。重定向到文件再读。
5. **PowerShell `>` 重定向默认写 UTF-16**，python 按 UTF-8 读会 `UnicodeDecodeError: 0xff`。用 `Out-File -Encoding utf8` + 读侧 `utf-8-sig`。
6. **从日志行截文本时前缀偏移量算错会静默吃掉开头字符**（`When`→`en`），用 `len(MARK)` 别硬编码数字。
7. **并行任务警告**：工作区 `C:\Users\DR\Downloads\DSH` 下有**其他项目在并行推进**（s.p.l.i.t 的 `work/`、`dist/split.exe` 等被其他代理改写）。**只动 `hackmud-zh-mod\` 与游戏目录的 `BepInEx\plugins\hackmud-zh\`**，别碰其他项目目录。
8. **网络**：hosts 把 `github.com`/`api.github.com` 等指向 127.0.0.1（本地加速器），env 有 `HTTP_PROXY=HTTPS_PROXY=http://127.0.0.1:9549`。git 走本机 credential manager。
9. **游戏 SSL 报错与模组无关**：`无法完成 SSL 连接` / `处理你的请求时发生严重错误` 是游戏走旧代理环境变量（`NO_PROXY` 是 9/29 改的，Steam 从 9/28 起没重启过）⇒ 重启 Steam 本体。
10. **用户规则**（对 GPT 同样适用）：面向用户输出尽量中文；**启动游戏等本机实操必须提前提醒用户**；「百分之一万确定没问题之前不要中断对话」—— 没真机证据不得声称「已修好」。

---

## 8. 一句话总结（给快速扫读的人）

模组已完成离线盘点、真实目录安装和收据验证；运行时尚未取得游戏窗口内的完整验收证据，不能宣称零漏译或完整汉化。接手后最可能的活是「用户又截图说哪里没翻译」：
**唯一入口 = `BepInEx\LogOutput.log` 的 `[未译]` 前缀**（按 `\r` 拆行后判读）→ 补 `zh_supplementN.json`
→ `merge_dict.py` → 整段回归（汉字数/配平差值/词频）→ 重编 DLL → 替换游戏目录 → 真机验证 → 发版六步。
切记：脚本名/命令名/用户名/玩家敲错的串**不要译**；裸词**永远不要译**（`scratch` 教训）。
