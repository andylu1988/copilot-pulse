# Copilot Pulse

Windows 桌面 Copilot 用量小组件：实时刷新、历史趋势、迷你数字框、22 套皮肤和动态效果。

**当前版本：1.9.4 · Windows 10 1809+ / Windows 11 · x64**

## 下载与安装

从 [最新版本](https://github.com/andylu1988/copilot-pulse/releases/latest) 下载 `CopilotPulse-Setup-1.9.4-x64.exe`，双击按当前用户安装。无需管理员权限，自带 .NET 8 Desktop Runtime；缺少 WebView2 时会运行微软官方引导程序，该步骤需要联网。安装包尚未进行代码签名。

默认安装到 `%LOCALAPPDATA%\Programs\CopilotPulse`，提供开始菜单入口和可选桌面快捷方式。升级前请从托盘退出组件；升级和卸载保留历史及个人设置。

此仓库为私有仓库，下载者需要仓库访问权限。

## 登录与适用范围

数据来自 [Microsoft 内部 Copilot Consumption 页面](https://copilot.github.microsoft.com/?section=limits&tab=consumption)，需要使用者具有该站点的访问权限和相应网络环境。这不是通用的 GitHub Copilot 个人订阅统计工具，也不是 Microsoft 或 GitHub 官方产品。

组件默认尝试通过 WebView2 使用当前 Windows 主账户 SSO；已有本机组件登录会话也可能决定当前账户。多账户、本地 Windows 账户、MFA 或访问策略可能要求点击“连接 Microsoft 账户”完成登录，因此无法保证所有 corp 账户都能静默登录。

每位用户使用自己的账户。安装包不包含作者的登录会话、真实用量、历史或 API Key；不会导入系统浏览器 Cookie，组件不读取或导出 PRT。运行时不调用图像生成服务。

## 使用

- 默认每 60 秒同步，可选择 30 秒、5 分钟或暂停；统计时效仍取决于源网站。
- 完整、紧凑、迷你数字三种模式；拖动移动，双击迷你框展开。
- 右键或设置菜单选择皮肤、动效强度和速度，可关闭动画。
- 历史趋势支持累计折线、变化柱状图，以及总额和单个企业。
- 收起到托盘后双击图标恢复；托盘菜单退出。

连接失败时保留并标记上次成功读数，首次未登录时不展示虚构金额。示例模式明确标为示例。

## 本机数据

用量、设置、历史与自定义背景保存在：

```text
%USERPROFILE%\Documents\Codex\CopilotPulse-data
```

历史按月追加到 `history\YYYY-MM.jsonl`。WebView2 正常保存本机登录会话；常规桌面运行目录为 `%LOCALAPPDATA%\CopilotPulse\WebView2`，具体目录可能受启动环境影响。用量不会上传到其他服务。

## 从源码构建

需要 Windows、.NET 8 SDK；解析器测试需要 Node.js。

```powershell
dotnet publish .\CopilotPulse.csproj -c Release -r win-x64 --self-contained true -o .\publish
node --test .\tests\parser.test.cjs
dotnet run --project .\tests\SsoPolicy\SsoPolicy.csproj
dotnet run --project .\tests\HistoryTests\HistoryTests.csproj -- .\work\history-tests
```

构建安装包需要 [Inno Setup](https://jrsoftware.org/isinfo.php)（已验证 6.7.3）与 [Microsoft WebView2 官方引导程序](https://go.microsoft.com/fwlink/p/?LinkId=2124703)。脚本检查引导程序的 Microsoft 签名。

```powershell
.\installer\Build-Installer.ps1 `
  -CompilerPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' `
  -WebViewBootstrapper '.\MicrosoftEdgeWebview2Setup.exe' `
  -OutputDirectory '.\dist' `
  -BuildDirectory '.\work\installer-build'
```

仅用于外观预览的 `publish\CopilotPulse.exe --demo` 使用虚构账号与金额，不覆盖真实缓存。页面布局变化可能需要更新 `extract-consumption.js`。

版本与功能说明见 [CHANGELOG.md](CHANGELOG.md)。素材来源见 [Assets/MATRIX-ART.md](Assets/MATRIX-ART.md) 和 [所选图标](Assets/SELECTED-DESKTOP-ICON.md)。

## English

Copilot Pulse is an unofficial Windows desktop widget for the Microsoft internal Copilot Consumption page. It provides live refresh, local history charts, compact and mini modes, and 22 animated themes. Access to the internal site is required.

Download the per-user x64 installer from Releases. It includes the .NET runtime and installs WebView2 if needed. Windows account SSO is attempted, but account selection, MFA, or organizational policy may require interactive sign-in. Each recipient uses their own account; no author credentials or usage history are distributed. The installer is currently unsigned. Usage and history stay on the local machine.
