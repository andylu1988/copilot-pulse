# Copilot Pulse user guide · v1.13.1

[中文](USER-GUIDE.zh-CN.md) · [Project](../README.md)


## English

**v1.13.1 combines the previous release's task observations, sampling health, exports, alerts, shortcuts and chart interactions with simpler forecasts and a consistent interface.**

![compact · Matrix](screenshots/v1.13.1/release-compact-en.png)

### Observe usage during a task

Choose **Start measurement**, enter a task note, and wait for the first successful sample to establish zero before starting work. Watch **Task measurement delta**; finish after source readings catch up using **End & save measurement**. **Measurement history** retains task names, times, observed increments and samples across restarts. Inspect sampling health, rename, copy a summary or export CSV / JSON / PNG.

![tasks · Matrix](screenshots/v1.13.1/release-tasks-en.png)

Source latency and concurrent work affect attribution; this is an observation, not an exact task bill. Missing samples are not fabricated. Anonymous exports remove account details and task names but retain timestamps, amounts and task IDs; review before sharing.

**Mini task delta remains opt-in.** Its main-panel button is disabled without an active measurement. Ending the task immediately restores monthly Overall usage. An active task awaiting its first valid sample has an explicit waiting state. Returning from mini restores the previous compact or full layout.

![mini-task · Matrix](screenshots/v1.13.1/release-mini-task-en.png)

### A smaller Overall forecast

The forecast emphasizes **projected month-end usage**, **whether the target is expected to be reached**, then progress and concise statistics. Calculation: **Overall month-to-date usage ÷ elapsed UTC quota-month duration × full month duration**. It no longer scans history, splits accounts or offers comparison/rate tabs.

An amount alert threshold configured in Settings takes priority over source caps. This is a personal target, not a change to the service quota. Without it, depletion is assessed only when complete numeric source caps are available. A percentage threshold cannot be converted to an Overall amount without those caps. Muting notifications retains the target. Stale, prior-month and future readings pause forecasts. Results are estimates, not bills.

![forecast · Matrix](screenshots/v1.13.1/release-forecast-en.png)

Connection actions have moved to the independent **⋯ → Connection diagnostics** window. Task comparison has been removed.

### History and notifications

Today starts at Windows local midnight. Rolling 24-hour / 7-day ranges and saved month selection remain available. Quota months reset at **00:00 UTC on the first day**, also shown in the system timezone. Net growth uses comparable observations and never assumes a missing starting value is zero.

Hover or pin points, wheel to zoom, drag to select, and double-click or right-click to reset. Refresh preserves zoom. Charts mark local midnight, UTC reset and unusual changes. History and measurement charts retain opaque backgrounds.

![hover · Matrix](screenshots/v1.13.1/release-hover-en.png)

With fictional usage of $1,234.56, a $2,000 threshold shows 61.73%; a $1,000 threshold shows 123.46%, exceeding it by $234.56. Widget warnings remain visible when notifications are muted.

![warning · Matrix](screenshots/v1.13.1/release-warning-en.png)

![Windows warning notification illustration](screenshots/usage-warning-notification-en.png)

The notification illustration is from an earlier version. Actual delivery depends on Windows settings. Persistent cooldowns, local quiet hours, silent mode, reset reminders and sampling interruption/recovery notifications are configurable. Manual pause does not trigger interruption warnings.

### Settings and appearance

Four equal glass buttons form a compact 2×2 layout. The rounded high-resolution badge, categorized Settings, integrated skin gallery, About, Online update and task naming share the selected skin. Close / Done stay visible. Main-menu Skin opens the same settings page. Choose language, digit font, refresh rate, animation intensity/frame rate and custom background.

![skins · Matrix](screenshots/v1.13.1/release-skins-en.png)

![about · Matrix](screenshots/v1.13.1/release-about-en.png)

![updates · Matrix](screenshots/v1.13.1/release-updates-en.png)

Matrix rain uses a composited frame and cached glass surfaces to reduce rendering work. The public demo menu is removed; documentation screenshots use a separate isolated review path.

Opt-in Ctrl+Alt+T / M / R / H shortcuts control task measurement, mini, refresh and recent tasks. Tray actions include pause/resume, and tray hover shows usage. Window placement supports multiple monitors. Connection diagnostics checks timezone, session, login, sampling, storage and shortcuts; anonymous reports exclude accounts, amounts, credentials, cookies, full URLs and page bodies.

![diagnostics · Matrix](screenshots/v1.13.1/release-diagnostics-en.png)

## 安装、连接、数据与更新

1. 下载 [最新版安装包](https://github.com/andylu1988/copilot-pulse/releases/latest)中的 `CopilotPulse-Setup-1.13.1-x64.exe`，按当前用户安装。
2. 点击 **连接 Microsoft 账户**，在官方登录窗口选择自己的公司账号并完成组织要求的验证。
3. 等待 Consumption 页面加载和第一次成功同步。增长曲线需要可比较的有效采样。
4. 数据不刷新时，从 **⋯ → 连接诊断** 检查并请求重新采样。

![首次连接操作步骤](screenshots/onboarding.png)

图中间栏是流程说明，并非登录页截图。WebView2 尝试 Windows 主账户 SSO，不保证复用系统浏览器会话或 PRT。每位用户使用自己的账号与源站权限，安装包不含作者账号、会话或密钥。安装器尚无发布者代码签名，请核对发布来源及 SHA-256。

设置、任务、历史与采样日志位于 `%USERPROFILE%\Documents\Codex\CopilotPulse-data`。旧安装会迁移数据，升级和卸载保留数据。文件未加密，备份前正常退出应用。历史保存在 `history/yyyy-MM.jsonl`，任务元数据为 `tasks.json`，刷新事件为 `sampling/yyyy-MM.jsonl`，设置为 `preferences.json`，提醒状态为 `reminders.json`。

每小时检查公开仓库正式版本，“迷你”左侧显示更新提醒。一键下载、SHA-256 校验、安装并重启客户端，Windows 无需重启。提醒未打开满 48 小时会自动安装；打开提醒会取消该次无人值守倒计时。关于显示本地和云端版本。

**Installation:** install the x64 setup, connect your own authorized corporate account and wait for synchronization. WebView2 attempts Windows-account SSO; browser-session/PRT reuse is not guaranteed. No author credentials are included. The installer is currently unsigned; check the source and SHA-256.

**Data:** unencrypted local files retain settings, tasks, history and sampling events across upgrades/uninstall in the directory above. Anonymous task exports retain times, amounts and task IDs. Diagnostic exports exclude accounts, amounts, credentials and page content.

**Updates:** hourly public release checks, a main-panel badge and one-click download, checksum verification, installation and app restart. Unopened reminders trigger automatic installation after 48 hours; opening one cancels that unattended countdown. Windows itself does not restart.
