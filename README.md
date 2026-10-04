# hackmud 简体中文模组（BepInEx 版）

基于 **BepInEx + Harmony** 的 hackmud 汉化模组。**不修改任何游戏既有文件。**

## 与「文件补丁版」的区别

| | 文件补丁版 | 本模组 |
|---|---|---|
| 工作原理 | 直接改 `Core.dll` / Unity 资产里的字面量 | 在**渲染层**拦截文本并替换 |
| 改游戏文件 | 是（4 个文件） | **否**（只新增文件） |
| 覆盖**服务器下发**文本 | 不能 | **能**（词典内的固定文案） |
| 能安全翻译「单副本」串 | 不能（译了会破坏逻辑） | **能**（逻辑拿到的仍是英文） |
| 卸载 | 需从备份还原 | 删掉新增文件即可 |

**为什么渲染层拦截更安全**：翻译只发生在 `TMP_Text.set_text` 被调用时。游戏内部用于比较、
当作字典键、查找 GameObject 的字符串**始终是原始英文**，因此像 `scratch`、`chat`、`time`、
`[DEADNODE]` 这类「既显示又参与逻辑」的串也能翻译，而不会破坏功能。

## 安装

1. **完全退出 hackmud**
2. 解压本包
3. 运行：
   ```powershell
   powershell -ExecutionPolicy Bypass -File install.ps1
   ```
4. 启动游戏

> 若你之前装过「文件补丁版」汉化，建议先运行它的 `install_zh.ps1 rollback` 还原，
> 以便独立验证本模组。（也可共存，但无必要。）

## 校验 / 卸载

```powershell
powershell -ExecutionPolicy Bypass -File verify.ps1      # 证明游戏文件未被改动 + 模组完整
powershell -ExecutionPolicy Bypass -File uninstall.ps1   # 只删本模组新增的文件
```

`verify.ps1` 会把 4 个游戏关键文件的 SHA256 与**零售版钉死值**逐条比对 —— 全绿即证明
本模组确实没有改动任何游戏文件。

## 中文字形从哪来

游戏自带字体不含汉字。本模组在运行时：

1. 用 `Font.CreateDynamicFontFromOSFont` 从**系统已装字体**取一份 CJK 字体
   （依次尝试 微软雅黑 / SimHei / Noto Sans CJK / 宋体 / 微軟正黑體 …）
2. 用 `TMP_FontAsset.CreateFontAsset` 造一个**动态** TMP 字体资产
3. 挂到 `TMP_Settings.fallbackFontAssets` 与各字体资产的回退表

TMP 在找不到字形时自动回退到这份，**无需改动任何资源文件**。
常见汉字已预热，生僻字按需动态补。

## 词典

- 位置：`BepInEx/plugins/hackmud-zh/zh.json`
- 格式：`{ "原文": "译文", ... }`
- **可热更新**：改完重启游戏即生效（`HackmudZh.dll` 内也内嵌了一份兜底）

匹配策略（三层）：

1. **整串精确匹配** —— 按钮、标签这类短文本
2. **逐行处理** —— `set_text` 常收到整个终端缓冲区（多行），逐行翻译
3. **最长优先短语替换** —— 处理客户端拼接出的文案（词典里既有完整串也有片段）

只处理含 ASCII 字母的串；已含中文的行不碰。

## 已知限制

- **动态内容译不了**：玩家脚本输出、其他玩家消息、市场数据、用户名 —— 这些是任意内容，
  任何词典都覆盖不了，会原样显示英文。
- **服务器改文案**会导致对应词条失效（优雅回退到英文，不会报错）。
- 依赖 BepInEx 注入。当前已在真实 Steam 目录完成无启动安装接管并通过收据校验；**本轮尚未启动游戏验证新 DLL 的实际渲染效果**。首次启动请留意
  `BepInEx/LogOutput.log`，并使用截图/OCR与日志核对字体和漏译项。

## ⚠️ 风险声明

hackmud 官方规则禁止修改客户端文件，并把 client file modification 视为 "custom client"，
**检测到可能导致封号**。本模组不修改游戏文件，但**仍是客户端改动**，同样可能触及该规则。
使用风险自负。详见 `NOTICE.md`。
