## hackmud 简体中文模组 v2.0.1

**词典大幅扩容：625 → 908 条**；客户端语料覆盖率 **172/587 → 438/587**。

本版只改词典与工具，模组代码未变（除版本号）。

### 本版新增译文

补的是 **marks 教学剧情那一整块服务器文案** —— 这些是文件补丁版**完全做不到**的部分：

- `Use it. scripts.get_level is your best friend; your lifeline; your scam detector…`
- `Of course, life in the pool is not without transactional loss. You receive …`
- `RUNNING IN THE KIDDIE_POOL IS FOR THE BAD LITTLE FRIENDS. WE WANT ALL THE LITTLE FRIENDS TO BE LIKE THE GOOD LITTLE FRIENDS.`
- `I like exploring too. Being an explorer is a mindset, and a gut feeling…`
- `$ $ Congratulations! Your investment pot has grown by +85473% since your last login!`
- 以及 `FULLSEC/MIDSEC/HIGHSEC/LOWSEC/NULLSEC` 各等级的说明、`marks.*` 各步骤的引导语等

**含变量的行用「片段条目」处理**，例如：

| 片段（原文） | 译文 |
|---|---|
| `You approach ` | `你走近 ` |
| ` and cruelly splash them with water! Not fair at all, but you're not about fair.` | `，残忍地朝他们泼水！一点都不公平，但你本来就不讲公平。` |
| `They lose ` | `他们损失了 ` |
| ` units of progress, hindering their capacity to escape! And all of those units are yours...` | ` 点进度，削弱了他们逃离的能力！而这些进度全都归你了……` |
| `You have gained ` | `你获得了 ` |
| ` has been hidden` | ` 已被隐藏` |
| `Mark :::` | `标记 :::` |

所以 `You approach givi and cruelly splash them with water!` 这类**带玩家名的句子**也能整句变中文
（未翻译的只有 `givi` 这个用户名本身）。

### 验收门改进

`tools/zhcheck` 的缺口清单输出格式重写：

- 之前是 C# 匿名对象的丑陋打印（`{ raw = …, plain = …, tr = … }`）
- 现在是**去颜色标签后的纯文本行，按英文词数降序**，并去掉了 `Take(400)` 上限

这让「跑门 → 读缺口 → 补词条」变成一个可批量推进的闭环。

### 剩余英文是什么

语料 587 行里仍有 149 行含英文，但高频词是：

```
marks(62) pool(31) hackmud(28) kiddie(26) name(23) input(17)
protocol(16) drrd(15) accts(15) Trust(13) security(12) …
```

这些是**脚本名、命令名、专有名词与玩家名 —— 本该保留英文**（译了反而不能用）。
真正该译的散文基本已译完。

### 安装 / 升级

> 若你已装过 v2.0.0，**只需替换 `BepInEx/plugins/hackmud-zh/zh.json`** 即可，
> 不必重装整个模组。

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
