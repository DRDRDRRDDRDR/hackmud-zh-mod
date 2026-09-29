## hackmud 简体中文模组 v2.0.8

**全量 triage 收口版。** 本版把**补丁前的英文历史备份**也并入语料，
覆盖到当前会话已看不到的早期服务器文本。

### 本版修掉的真漏译

| 漏译 | 证据 | 修法 |
|---|---|---|
| `::: earned` | `Mark :::init::: earned` → `标记 :::init::: earned`（`earned` 留着英文） | 补 `::: earned → ::: 已获得` + 15 条已知标记的整句 |
| `hackmud runs on monthly donations from players like you` | 只译了 `donations from players` 片段，前后留着英文 | 补整句（v2.0.7） |
| `joined_corp: false` / `is_supporter: false` | 模板已中文化但值没译 | 补 `→ 已加入公司：否` 等（v2.0.7） |

### 语料来源这次扩到了三份

| 来源 | 行数 | 作用 |
|---|---|---|
| `shell.txt`（当前回滚缓冲） | 1821 | 当前会话 |
| `shell.txt.bak-en-history-*`（**补丁前英文历史**） | 1255 | **当前会话已看不到的早期文本** |
| 去重合并 | **769** | 全量 triage 输入 |

### triage 判据（可复用）

把真机缓冲区整段跑一遍翻译 → 按英文词频降序 → **逐条判定**：

| 剩余英文 | 判定 |
|---|---|
| `marks.*` / `accts.*` / `kiddie_pool.*` / `chats.*` / `scripts.get_level` | ✅ 脚本名，玩家要照着敲 |
| `help` / `user` / `clear` / `shutdown` / `create_user` | ✅ 命令名 |
| `connect` / `prompt` / `input` / `will_comply` / `favorite_number` / `gc_string` / `to` / `amount` / `day` | ✅ 命令参数名与数据示例 |
| `Steam` / `Trust` / `hackmud` / `GC` / `VU` / `VAC` / `SSL` / `HTTP` / `FULLSEC` / `MIDSEC` / `HIGHSEC` | ✅ 专有名词、单位、协议名、安全等级 |
| `Marco Polo` / `BearBuddy(TM)` / `BASIC_BOT` / `robovac_*` | ✅ 游戏内专名与机器人名 |
| `drrd` / `DRRD` / `givi` / `rynard` / `niko` / `gun8hoot` / 捐赠榜 | ✅ 用户名 |
| `support@hackmud.com` / `hackmud.com` | ✅ 邮箱与网址 |
| `marks.avaliable` / `marks.vertify` / `iddie_pool.splash` | ✅ **玩家自己敲错的**（客户端原样回显） |

### 标签配平的**正确判据**

真机语料里输入本身就是 `开 1792 / 闭 1791`（差 +1，某行被截断）。
**正确判据不是「输出必须相等」，而是「输出差值与输入一致」**：

```
输入 开1792 闭1791 (差+1)
输出 开1402 闭1401 (差+1)   ✅ 未改变配平
```

### 真机验证证据（`[未译]` 渲染层诊断 + OCR）

| 检查项 | 结果 |
|---|---|
| `[未译]` 总条数 | **9 条**，全是空行 / 脚本名 / 代码 / 横幅边框 / 命令提示符 |
| `earned` | 0 处 |
| `runs` / `monthly` / `like you` / `false` | 各 0 处 |
| `Top level marks…` / `To view progress…` / `admire` | 各 0 处 |
| 捐赠横幅（OCR） | `hackmud 全靠像你这样的玩家每月捐赠维持运转` |
| 布尔值（OCR） | `是支持者：否` / `已加入公司：否` |
| 游戏健康 | 运行中，无崩溃/挂起事件 |

### 现状

| 指标 | 数值 |
|---|---|
| 词典 | 1342 条 |
| 该译而未译的散文 | **0** |
| 多译 | **0** |

### 升级

> 已装旧版：替换 `BepInEx/plugins/hackmud-zh/` 下的
> **`HackmudZh.dll` + `zh.json` + `wordmap.json`** 三个文件即可。

### ⚠️ 风险声明

hackmud 官方规则禁止修改客户端文件，并把 client file modification 视为 "custom client"，
**检测到可能导致封号**。本模组不修改游戏文件，但**仍是客户端改动**，同样可能触及该规则。
使用风险自负。
