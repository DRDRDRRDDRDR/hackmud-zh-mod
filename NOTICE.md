# 权利归属与使用风险

## 权利归属

- **hackmud** 及其全部游戏文件、资源、商标，版权归 hackmud 的开发者所有。
- 本模组**不分发任何游戏文件**。包内只有：
  - [BepInEx](https://github.com/BepInEx/BepInEx)（LGPL-2.1，第三方开源项目，原样附带）
  - 本模组原创的插件代码（`HackmudZh.dll`）
  - 本模组原创的词典（`zh.json`）
  - 本模组原创的安装/校验脚本
- 安装脚本**只新增文件，不覆盖、不修改任何游戏既有文件**。这一点可由 `verify.ps1`
  对 4 个游戏关键文件的 SHA256 比对来证明。

## 使用风险（请务必阅读）

hackmud 官方规则中明确写着：

> Modifying any existing files is not permitted.
>
> …client file modification is considered a 'custom client'…
>
> Any detected custom client activity will result in bans.

**本模组不修改游戏文件，但它仍然是对客户端的改动**（通过 BepInEx 注入进程、挂钩游戏方法）。
因此：

- 它在规则层面**同样可能被视作 "custom client"**
- 一旦被判定为使用自定义客户端，**账号可能被封禁**
- 本项目作者**不对任何封号、数据丢失或其它后果负责**

### 关于检测

本项目不对客户端完整性检查、服务端风控、举报或封禁风险作任何判断，也不保证模组能够规避检测。检测可能来自：

- 服务端的异常行为分析
- 客户端或启动器更新后的完整性检查
- 其他玩家的举报（中文界面在截图里一眼可见）

**不要**因为缺少已知的本地检测证据，就认为没有风险。

## 免责

本模组仅供个人学习与本地化研究使用。使用者需自行承担全部风险。
若你不接受上述任何一条，请不要安装本模组。
