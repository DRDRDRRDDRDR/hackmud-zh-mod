## hackmud 简体中文模组 v2.0.5

本版修掉一个**漏译**和一个**多译**，都是用户真机截图指出的。

### ① 多译（本版最重要）：撤掉「安全裸词表」的条目

v2.0.3 我加了 `dict/wordmap.json`，把面板标题 `scratch` / `chat` / `binlog` / `binmat`
译成中文。**这是错的** —— 用户截图指出：标记树里那一行也变成了 `草稿`：

```
> marks.navigation
|
\---------- 草稿          ← 这是玩家要照着敲的脚本名 scratch！
```

**根因**：面板标题 `scratch` 与标记树里的**脚本名** `scratch` 是同一个词。
按词替换必然误伤，而这一行是玩家必须输入的。

**修复**：清空 wordmap.json（保留机制与「标识符守卫」以备将来使用），
面板标题恢复英文 —— 宁可少译，不能译坏。

> 结论写进了 `wordmap.json` 的说明字段：在这个游戏里**裸词无法安全翻译**，
> 因为显示用的词与要输入的命令名共用同一份字符串。

### ② 漏译：`When you want to admire how much wealth you've accumulated…`

该句在词典里**存在**且剥离标签后完全一致，却没生效。原因在 `_normWhole` 索引：

- `_normWhole` 的 key 来自 `Normalize(词典key)`，而 **`Normalize` 不剥离颜色标签**
- 词典 key 是纯文本 `…hit up accts.balance`
- 屏幕上是 `…hit up <color=#FF8000FF>accts</color>.<color=#1EFF00FF>balance</color>`
- ⇒ **整串索引永远对不上**

短语匹配器（会剥标签）本该兜住它 —— 实测确认现在两条路径都能命中：

```
输入: When you want to admire how much wealth you've accumulated, hit up <color=…>accts</color>.<color=…>balance</color>
输出: <color=#FF8000FF>想欣赏一下自己攒了多少财富时，就用 accts.balance</color>
```

（配色也完整保留。）

### 真机验证结果

用模组自带的「渲染层未译诊断」读真机日志：

| 检查项 | 结果 |
|---|---|
| `[未译]` 含 `admire` | **0 条** ✅ |
| `[未译]` 含 `草稿` | **0 条** ✅ |
| 剩余 `[未译]` | 全是脚本名 / 命令名 / 用户名 / 分隔符，以及已译行里的脚本名 |

### 当前状态

| 指标 | 数值 |
|---|---|
| 词典 | 1168 条 |
| `zh.json` | 197 KB |
| `HackmudZh.dll` | 220 KB |
| 标签配平 | ✅ 输入 1814/1814 → 输出 1662/1662 |

### 升级

> 已装旧版：替换 `BepInEx/plugins/hackmud-zh/` 下的
> **`HackmudZh.dll` + `zh.json` + `wordmap.json`** 三个文件即可。

### 校验值

见包内 `SHA256SUMS.txt`。

### ⚠️ 风险声明

hackmud 官方规则禁止修改客户端文件，并把 client file modification 视为 "custom client"，
**检测到可能导致封号**。本模组不修改游戏文件，但**仍是客户端改动**，同样可能触及该规则。
使用风险自负。
