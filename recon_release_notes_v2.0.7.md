## hackmud 简体中文模组 v2.0.7

**本版是「真机全量 triage」的结果** —— 不再靠语料覆盖率数字，而是把真机上的渲染结果
按英文词频排序，逐条判断哪些该译、哪些本就该保留。

### 上版（v2.0.6）修掉的折行漏译

| 缺陷 | 说明 |
|---|---|
| 换行有两种折行风格 | 落在**单词中间**须删换行（`mone`+`y.`=`money.`）；落在**空格处**须当空格（`begin a`+`mark`=`a mark`）。v2.0.4 只做了前者 → 后者全漏 |
| `\r\n` 被当成两个换行 | `a\r\nmark` 变 `a  mark`（两个空格）→ 与词典单空格对不上 |
| 词边界用错了坐标系 | 删换行后行尾与下一行行首粘连，`mark` 后紧跟 `T` 被判定「还在标识符里」→ 整条拒绝 |

**修法**：`Translate` 跑两遍（删换行 / 换行当空格）；连续 `\r\n` 视为一个换行；
新增 `BoundaryOkOrig` 用**原文下标**判边界。

### 本版（v2.0.7）修掉的全量 triage 剩余项

把真机缓冲区跑一遍翻译后，按英文词频列出剩余词，逐条判断：

| 剩余英文 | 判定 |
|---|---|
| `hackmud runs on monthly donations from players like you` | ❌ **真漏译** —— 只译了 `donations from players` 片段，`runs on monthly` / `like you` 留着英文 → **已补整句** |
| `false`（48 次：`已加入公司： false` / `是支持者： false`） | ❌ **真漏译** —— 模板已中文化但值没译 → **已补 `joined_corp: false→已加入公司：否`** 等 4 条 |
| `marks.*` / `accts.*` / `scratch` / `risk.reward` / `scripting` / `lost_and_found` | ✅ 脚本名，玩家要照着敲 |
| `help` / `user` / `clear` / `shutdown` / `create_user` | ✅ 命令名 |
| `to` / `amount` | ✅ 命令参数名 |
| `Steam` / `Trust` / `hackmud` / `GC` / `VAC` / `SSL` / `HTTP` | ✅ 专有名词与协议名 |
| `drrd` / `DRRD` / 捐赠榜人名 | ✅ 用户名 |
| `support@hackmud.com` / `hackmud.com` | ✅ 邮箱与网址 |
| `marks.avaliable` | ✅ **玩家自己敲错的**（客户端原样回显） |

### 诊断本身也修了一个误报

`[未译]` 诊断的英文正则 `[A-Za-z]{3,}` 会**误匹配颜色十六进制值**——
`<color=#00FFFFFF>是支持者：否</color>` 里的 `00FFFFFF` 让它把**已经译好**的行报成未译，
清单里混进大量噪声。现在检测前先剥掉颜色标签。

修后真机 `[未译]` 从 13 条降到 **9 条**，且全部是空行、脚本名、分隔符、代码、命令提示符。

### 真机验证证据（OCR，非推断）

| 检查项 | 结果 |
|---|---|
| 捐赠横幅 | `hackmud 全靠像你这样的玩家每月捐赠维持运转` |
| 布尔值 | `是支持者：否` / `已加入公司：否` |
| `runs` / `monthly` / `like you` / `false` | 各 **0 处** |
| `Top level marks…` / `To view progress…` / `admire` | 各 **0 处** |
| 标签配平 | 输入 2385/2385 → 输出 2008/2008 ✅ |

### 现状

| 指标 | 数值 |
|---|---|
| 词典 | 1322 条 |
| 真机缓冲区汉字数 | 7266 → **7554** |
| 该译而未译的散文 | **0** |
| 多译 | **0** |

### 升级

> 已装旧版：替换 `BepInEx/plugins/hackmud-zh/` 下的
> **`HackmudZh.dll` + `zh.json` + `wordmap.json`** 三个文件即可。

### ⚠️ 风险声明

hackmud 官方规则禁止修改客户端文件，并把 client file modification 视为 "custom client"，
**检测到可能导致封号**。本模组不修改游戏文件，但**仍是客户端改动**，同样可能触及该规则。
使用风险自负。
