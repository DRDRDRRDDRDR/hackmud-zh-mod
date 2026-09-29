## hackmud 简体中文模组 v2.0.2

修掉三个**让译文「明明在词典里却不生效」**的根因，词典 908 → **1281 条**。

### 修了什么

#### 1. 客户端会用**不同的对齐宽度**重排文本

源码里是 `` `Chelp`                 [see this again] ``（13 个空格），屏幕上却只有 11 个。
带对齐空白的 key 因此永远对不上。

→ 引擎改为在**归一化空白**的视图上匹配（连续空白压成一个空格），
并新增「归一化整串」索引。现在 `help [see this again]`、`clear [clear the window]` 都能命中。

#### 2. 客户端会**按宽度折行**

`AddOutput` 里调 `MEGNKIOGEBH(line, char_width)`，超过一行宽度（终端 108 列）的长句
在屏幕上被断成两行 —— 整句 key 自然匹配不到。

→ 引擎把 `\n` 也当空白归一化；同时为超长词条生成「按终端宽度预折行」的变体。

#### 3. 颜色码的**结束符是单独一个反引号**

`` `C `` 开色、`` ` `` 收色。我把 `` `C `` 当 token 切分，却漏了收尾的 `` ` ``，
于是切出来的片段带着一个多余反引号：

```
key  = 'help`                 [see this again]\n  '   ← 多了个 `
屏幕 = '  help                 [see this again]'      ← 没有 `
```

→ tokenizer 把单独反引号也算 token。这是 `[see this again]` 一直译不出来的**真正原因**。

### 本版新增译文

- `[see this again]` / `[change users]` / `[create a user]` / `[clear the window]` / `[disconnect from terminal]`
- `The second most important thing, after making money, is spending it…`
- `Obtain and provide a verification code to this script…`
- `Top level marks are available to earn now, run marks.<mark_name> to begin a mark`
- `name: "<mark_name>" - Search for mark by name` / `pos: [x,y] - View mark at specified coordinates`
- `Will you be able to trust 3 alpha H!T forever?`
- `even if not a vac, you are at least friend of _2_11_45_36! maybe even worthy of truth!`

### 验收门改进

`tools/zhcheck` 新增 **`--show` 模式**：逐行打印「输入 → 输出」。
以前只能靠「这行还在不在缺口清单里」反推，容易误判；现在可以定点验证某一行到底译成了什么。

```
dotnet tools/zhcheck/bin/Release/net8.0/zhcheck.dll dict/zh.json <语料文件> --show
```

### 现状

| 指标 | 数值 |
|---|---|
| 词典 | **1281 条** |
| 语料行数 | 587 |
| 已译行数 | **450 / 587** |

剩下的 137 行里，**绝大多数是命令语法示例**，例如：

```
kiddie_pool.dive {connect:"BASIC_BOT", prompt:"<input>"}
accts.xfer_gc_to { to: "trust", amount: "18GC" }
marks.security_levels { script: "<script_name>", level: "<security level>" }
marks.protocol 查看已完成的标记 { name: "money_manager" }   ← 已译，只是含脚本名
```

这些是**玩家要照着敲的命令** —— 译了反而不能用。所以剩下的英文基本都该保留。

### 升级

> 已装过旧版：**只需替换 `BepInEx/plugins/hackmud-zh/` 下的两个文件**
> （`HackmudZh.dll` + `zh.json`）即可，不必重装整个模组。

全新安装：

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1
```

### 校验值

见包内 `SHA256SUMS.txt`。

### ⚠️ 风险声明

hackmud 官方规则禁止修改客户端文件，并把 client file modification 视为 "custom client"，
**检测到可能导致封号**。本模组不修改游戏文件，但**仍是客户端改动**，同样可能触及该规则。
使用风险自负。
