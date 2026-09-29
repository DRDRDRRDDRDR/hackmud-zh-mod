## hackmud 简体中文模组 v2.0.0（BepInEx + Harmony）

**不修改任何游戏既有文件**的汉化模组。在**渲染层**拦截文本并替换。

> 这是本项目的**模组版**。同仓库另有一个「文件补丁版」（直接替换 4 个游戏文件），
> 见 <https://github.com/DRDRDRRDDRDR/hackmud-zh>。

### 它比「文件补丁版」多做了什么

| | 文件补丁版 | 本模组 |
|---|---|---|
| 改动游戏文件 | 是（4 个） | **否**（只新增文件） |
| 覆盖**服务器下发**文本 | ❌ 完全做不到 | ✅ 词典内可达 |
| 能安全翻译「单副本」串 | ❌ 译了会破坏逻辑 | ✅ 逻辑读到的仍是英文 |
| 卸载 | 从备份还原 | 删掉新增文件即可 |

**「单副本」是什么意思**：`#US` 堆里每个字符串只有一份。像 `scratch`、`chat`、`time`、
`[DEADNODE]` 这些**既显示又被当作字典键 / GameObject 名**的串，改字面量会让查找失效。
渲染层拦截只在「写进文本框」那一刻替换，游戏内部逻辑读到的仍是原文 —— 所以安全。

### 真机实测

在 **Unity 6000.0.59 Mono** 上实测：

```
Running under Unity v6000.0.59.15673372
Patching [UnityEngine.CoreModule] with [BepInEx.Chainloader]
Loading [hackmud 简体中文 2.0.0]
系统字体可用: Microsoft YaHei UI
字体资产来源: CreateFontAsset(族名) Microsoft YaHei UI/Regular
已挂载到 4 处回退位置
已挂载 2 个翻译入口
hackmud 汉化已加载：词典 625 条，字体 (mode=Dynamic, 已注册字形 653)
```

**BepInEx 5.4.23.5 可直接注入 Unity 6**，无需 BepInEx 6 的 dev 线。

屏幕 OCR 客观取证（游戏文件仍是**零售原版**，而屏幕显示中文）：

| 原文 | 现显示 |
|---|---|
| `joined: {...} // Wow! You've been playing hackmud for about 2 months!` | `加入：{...} // 哇！你已经玩 hackmud 大约 2 个月了！` |
| `last_login: {...} // Welcome back to hackmud!` | `上次登录：{...} // 欢迎回到 hackmud!` |
| `oldest_user:` / `highest_tier:` / `is_supporter:` | `最早用户：` / `最高层级：` / `是支持者：` |
| `Top Donors` / `donations from players` | `捐榜` / `来自玩家的捐赠` |
| `WHAT'S NEW` / `for more information` | `= 最新动态 =` / `了解更多信息` |
| `Your users: drrd [1/2]` / `Retired users:` | `你的用户：drrd [1/2]` / `已退役用户：` |
| `To run a script, type the name of the script and press enter.` | `要运行脚本，请输入脚本名称并按回车。` |
| `Common scripts:` / `Special shell commands:` | `常用脚本：` / `特殊外壳命令：` |
| `-terminal active-` / `-authentication success-` | `一终端已激活一` / `一身份验证成功一` |

**上表左侧大部分是服务器下发的** —— 文件补丁永远做不到，模组做到了。

### 安装

1. **完全退出 hackmud**
2. 解压本包
3. `powershell -ExecutionPolicy Bypass -File install.ps1`
4. 启动游戏

安装脚本**只新增文件**；遇到同名文件会**中止**而不是覆盖。

```powershell
powershell -ExecutionPolicy Bypass -File verify.ps1      # 证明游戏文件未被改动
powershell -ExecutionPolicy Bypass -File uninstall.ps1   # 只删本模组新增的文件
```

### 中文字形来源

游戏自带 TMP 字体不含汉字。模组在运行时用系统 CJK 字体
（`Font.CreateDynamicFontFromOSFont`）造一个**动态** TMP 字体资产
（`TMP_FontAsset.CreateFontAsset(familyName, styleName, pointSize)`），
挂到全局回退表与各字体资产的回退表上。**不改动任何资源文件。**
已预热 653 字形，生僻字按需动态补。

### 词典可热更新

`BepInEx/plugins/hackmud-zh/zh.json`，改完**重启游戏即生效**，无需重编译。

三层匹配：整串精确 → 逐行 → **标签感知短语替换**
（客户端高亮会把脚本名包进 `<color=…>`，引擎在去标签视图上匹配再映射回原文，**颜色完整保留**）。

### 自动化验收门（随源码）

```powershell
dotnet tools/zhcheck/bin/Release/net8.0/zhcheck.dll   # 翻译覆盖率 + 缺口清单
python tools/test_roundtrip.py                        # 安装往返 6 步
python tools/build_mod.py                             # 打包（硬校验：包内无游戏程序集）
```

### 校验值

见包内 `SHA256SUMS.txt`。

### 已知限制

- **动态内容译不了**：玩家脚本的任意输出、其他玩家消息、市场数据 —— 词典覆盖不到，原样显示英文。
- 服务器改文案会让对应词条失效（**优雅回退到英文**，不会报错）。
- 词典当前 625 条；验收门显示客户端语料中 **76/143 行**已译，其余英文为命令名/脚本名/玩家名（**本该保留**）。

### ⚠️ 风险声明

hackmud 官方规则禁止修改客户端文件，并把 client file modification 视为 "custom client"，
**检测到可能导致封号**。本模组不修改游戏文件，但**仍是客户端改动**，同样可能触及该规则。
使用风险自负。
