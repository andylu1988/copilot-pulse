# 用量预估与快捷操作 / Usage forecast and shortcuts

v1.13.0。入口：主界面 **用量预估**（含区间对比与配额预测），菜单 **连接诊断**；通知和快捷键在 **⋯ → 设置** 中配置。

## 中文

### 区间对比与截图

在“用量预估 → 区间对比”选择两个不同任务。并排查看观测增量、持续时间、单次最大变化和有效采样数，下方各自的曲线保留实际时间轴。A − B 表示两个任务的观测增量之差。至少需要两个任务；没有足够有效读数显示 `—`。

“导出对比截图”保存 PNG，默认隐藏任务名。“任务记录 / 导出”也可以导出单个任务截图。匿名截图仍含时间和金额，分享前请检查。观测区间可以估计单次任务花费，源页面延迟和同时运行的其他工作会影响归属；峰值是相邻有效采样变化，受采样间隔影响。

### 配额预测

“用量预估 → 配额预测”可选择近 1 小时、过去 24 小时或本月记录作为外推速度。每个账户显示已用金额和页面百分比、剩余额度、月末预计累计量，以及按剩余重置时间均摊的每日参考额度。记录覆盖范围与采样数随结果显示，至少需要两个可比较读数且跨度不少于一分钟；断档会明确提示端点平均。

页面提供数值上限时，按数值上限计算剩余金额；页面仅显示 `Standard` 时，不假定固定金额上限，而按当前金额与页面已用百分比的比例估算月末百分比及耗尽风险，标记 `≈ 百分比外推`。页面百分比四舍五入会影响精度，低于 0.1% 时不据此外推；全零且无数值上限时保持未知。

重置以 **UTC 月初 00:00** 为边界，同时换算 Windows 当前系统时区显示。计算以源读数时间为锚点，刷新窗口不会把预计耗尽时间向后推移。源读数延迟超过采样容忍范围、属于上个月或未来时间时暂停预测，并提示重新采样。金额下调、账户缺失和记录不足分别说明原因；已达到额度与低速度下重置前不会耗尽分别显示。

“刷新”只重读本机记录；“立即采样”请求刷新数据源。成功采样不保证页面已结算，预测不是账单或保证，未来工作强度变化会影响预测。

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

Version 1.13.0. Open **Usage forecast** for task comparison and quota forecasts, or **Connection diagnostics** from the menu. Notifications and shortcuts are configured in **Settings**.

### Comparison and reports

Choose two distinct tasks in **Usage forecast → Compare tasks**. Compare observed delta, duration, largest sample change and valid sample count. Each chart retains its actual timestamps. A − B is the difference between observed task deltas. Insufficient readings are shown as `—`.

Export a PNG comparison with task names hidden by default, or export an individual task image from **Task records / Export**. Anonymous images still contain timestamps and amounts; review before sharing. Observations help estimate the cost of a single task, but source lag and concurrent work may shift attribution. Peak sample changes depend on sampling cadence.

### Quota forecasts

**Quota forecast** lets you select the last hour, last 24 hours or observed current month as the extrapolation basis. Each account shows source cost and percentage, remaining quota, projected month-end usage and a daily allowance evenly spread until reset. Actual coverage, sample counts and gaps are disclosed. At least two comparable readings spanning one minute are required.

Numeric source limits provide monetary remaining quota. For `Standard` plans without numeric limits, the current cost-to-percentage ratio supports an explicitly approximate percentage projection and depletion risk. No fixed monetary limit is assumed. Source rounding affects precision; percentages below 0.1% are not extrapolated, and all-zero readings without a numeric limit remain unknown.

Resets follow **00:00 UTC on the first day of the month**, also displayed in the current Windows timezone. Forecasts are anchored to the source timestamp, so repainting does not postpone depletion. Stale readings, prior-month readings and future timestamps pause predictions. Corrections, missing accounts and insufficient samples have separate explanations. Already exhausted and low-rate usage that will not exhaust before reset are shown explicitly.

**Refresh** reloads local records; **Refresh source** requests a new source reading. Successful sampling does not guarantee settled page data. Forecasts are estimates rather than billing or guarantees, and changing workloads affect results.

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
