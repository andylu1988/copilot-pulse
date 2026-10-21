# 用量预估与快捷操作 / Usage forecast and shortcuts

v1.13.1。入口：主界面 **用量预估**；主菜单 **连接诊断** 打开独立窗口。

## 中文

### 精简后的用量预估

窗口依次突出 **预计月末累计量 → 按此速度是否会达到目标 → 进度条 → 小字统计依据**。只看 Overall，不再分账户、选择不同速度或读取历史文件；移除了区间对比及连接操作。诊断改为主界面 **⋯ → 连接诊断** 的独立窗口。

计算：**本月累计 ÷ UTC 配额月已过时长 × 当月总时长**。配置金额提醒阈值时，优先按该阈值判断是否预计超出；例如 $2,000。提醒阈值是个人目标，不会改变源站配额，因此界面明确称“达到提醒阈值”。没有金额阈值时，仅在源站提供可合计的完整数值上限时判断配额耗尽；百分比阈值缺少金额上限时会说明无法换算。静音通知不会清除预估目标。

![forecast · Matrix](screenshots/v1.13.1/release-forecast-zh-CN.png)

源读数过旧、属于上月或未来时暂停预估；按源读数时间计算，重绘不会推迟预计达到目标的时间。预估会随后续工作强度变化，仅供参考。


### 提醒与免打扰

在“设置 → 通知与免打扰”选择：

- 同一事件仅提醒一次，或每小时 / 每 4 小时 / 每天最多一次；冷却状态跨重启保存。
- 完全静默，以及按本机时间生效的免打扰时段。开始与结束相同表示全天静音。主界面警告仍显示。
- 配额重置前提醒、成功采样中断提醒，以及警告解除 / 采样恢复通知。主动暂停采样不触发中断提醒。

系统通知使用 Windows 通知能力；实际呈现取决于 Windows 通知设置和免打扰。静默期间不会追补每次重复提醒。

### 快捷操作与窗口

“设置 → 全局快捷操作”可选择启用全局快捷键，默认关闭：

| 快捷键 | 操作 |
| --- | --- |
| Ctrl+Alt+T | 开始 / 结束并保存任务 |
| Ctrl+Alt+M | 切换迷你，恢复此前紧凑 / 完整模式 |
| Ctrl+Alt+R | 立即刷新 |
| Ctrl+Alt+H | 显示最近任务 |

注册冲突会提示，并可在连接诊断查看。托盘右键支持任务开始 / 结束、最近任务、刷新、暂停 / 恢复采样、用量预估和退出。窗口位置允许负坐标显示器；恢复时检查可见区域，显示器断开后移回可用屏幕。可从窗口空白及边缘区域拖动，按钮、输入框和图表保留各自交互。

### 图表交互

悬停查看精确时间、数值和采样状态；点击采样点固定提示。滚轮按光标位置缩放，拖动框选时间范围，双击或右键恢复完整范围。自动刷新保留当前缩放范围；切换趋势区间会重置。缩放到无有效采样区间会提示恢复视图。图上标记本机午夜、UTC 月重置边界，以及金额下降 / 异常跳增；跳增标记是提示性判断，不是错误结论。范围内没有记录时不伪造采样点。

### 首次使用与诊断

初次无缓存数据时打开连接诊断，也可随时从菜单进入。检查 Windows 时区、内嵌浏览器会话、登录与源页面、采样刷新、本机存储和快捷键注册状态。使用“登录数据源”完成本人公司账号验证，在 Consumption 标签等待页面加载，然后“立即采样”和“刷新”诊断。

“导出匿名诊断报告”生成 JSON，仅包含版本、时区和固定诊断状态，不包含账户名、用量金额、令牌、Cookie、完整网页 URL 或网页正文。绿色 OK、黄色 WARN、红色 ERROR 表示检查状态；该检查不能证明服务端数据已实时结算。

新增 `reminders.json` 仅保存提醒冷却和恢复状态，位于现有应用数据目录；不改变历史、任务和登录的保存位置。演示模式不发送真实通知、不注册全局快捷键、不修改真实数据。

## English

Version 1.13.1. Usage forecast and Connection diagnostics are separate windows.

### A smaller Overall forecast

The forecast emphasizes **projected month-end usage**, **whether the target is expected to be reached**, then progress and concise statistics. Calculation: **Overall month-to-date usage ÷ elapsed UTC quota-month duration × full month duration**. It no longer scans history, splits accounts or offers comparison/rate tabs.

An amount alert threshold configured in Settings takes priority over source caps. This is a personal target, not a change to the service quota. Without it, depletion is assessed only when complete numeric source caps are available. A percentage threshold cannot be converted to an Overall amount without those caps. Muting notifications retains the target. Stale, prior-month and future readings pause forecasts. Results are estimates, not bills.

![forecast · Matrix](screenshots/v1.13.1/release-forecast-en.png)

Connection actions have moved to the independent **⋯ → Connection diagnostics** window. Task comparison has been removed.


### Notifications

**Settings → Notifications and quiet hours** provides once-per-event or hourly / 4-hourly / daily cooldowns, silent notifications, local quiet hours, reset warnings, sampling interruption alerts and recovery notifications. Equal quiet-hour boundaries mean all day. Cooldowns persist across restarts. Manual sampling pauses do not trigger interruption alerts. Widget warnings remain visible when notifications are muted. Windows settings and Do Not Disturb affect actual notification delivery; repeated alerts during quiet hours are not replayed individually.

### Shortcuts, tray and windows

Global shortcuts are opt-in and disabled by default:

| Shortcut | Action |
| --- | --- |
| Ctrl+Alt+T | Start / end and save a task |
| Ctrl+Alt+M | Toggle mini and restore compact / full mode |
| Ctrl+Alt+R | Refresh now |
| Ctrl+Alt+H | Show recent tasks |

Conflicts are reported and included in diagnostics. The tray menu provides tasks, refresh, pause / resume, usage forecast and exit. Window restoration accepts negative monitor coordinates, checks visible bounds and moves to an available screen if a monitor disappears. Drag blank areas or edges; controls and charts retain their interactions.

### Interactive charts

Hover for precise readings and sample status; click a point to pin its tooltip. Use the wheel to zoom around the cursor or drag to select a time range. Double-click or right-click to reset. Sampling refresh preserves zoom; switching trend ranges resets it. Empty ranges offer a reset hint. Local midnight, UTC month resets, downward corrections and unusually large jumps are marked. Jump markers are heuristic hints, not proof of errors. Missing samples are never fabricated.

### First-use diagnostics

Diagnostics open automatically on first use without cached data and remain available from the menu. Check timezone, browser initialization, source sign-in, recent samples, local storage and shortcut registration. Choose **Sign in**, complete your company authentication and open Consumption, then **Refresh source** and **Refresh** diagnostics.

Export an anonymous JSON report containing only version, timezone and fixed diagnostic states. It excludes account names, usage amounts, tokens, cookies, full URLs and page text. OK / WARN / ERROR are colored green / yellow / red. A healthy check does not prove immediate server-side billing updates.

The existing data directory additionally stores `reminders.json` for cooldown and recovery state. History, task and sign-in locations remain unchanged. Demo mode sends no real notifications, registers no global shortcuts and does not change real data.
