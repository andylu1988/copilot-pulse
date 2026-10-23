# v1.14.0 · 任务观察与桌面体验 / Task observations and desktop controls

> 2026-10-23 · [下载安装包 / Download](https://github.com/andylu1988/copilot-pulse/releases/tag/v1.14.0) · 支持客户端在线升级 / In-app updates supported.

[1.12.0 之后的全部变更](UPDATES-SINCE-v1.12.0.md) · [项目主页](../README.md)

## v1.14.0：任务观察与桌面体验

**v1.14.0 已发布。** 可从 GitHub 下载 Windows x64 安装包，或在客户端选择“在线更新”完成下载、校验、安装和自动重启。以下截图使用矩阵皮肤与隔离的虚构数据。

![紧凑主面板：矩阵皮肤与虚构数据](screenshots/v1.14.0/release-compact-zh-CN.png)

### 一次任务，从开始到延迟结算

1. 点击 **开始任务计量**，填写任务名；可选择常用模板，或在计量记录中选择 **再测一次** 沿用备注。
2. 等待首个成功采样建立零基线后再工作。计量期间可以添加“开始跑测试”“切换模型”等时间线标记。
3. 点击 **结束并保存计量** 后，默认等待 3 次新的成功采样，最长 10 分钟，以观察源页面延迟更新。可以立即结束、跳过等待；超时结果标为未确认。
4. 在 **计量记录** 回看任务名、起止时间、增量、采样次数、失败与重试，复制摘要或导出 CSV / JSON / PNG。

适合测试观察单次 Bug 修复、代码审查、生成图片或方案准备期间的花费。增量包含源页面延迟及同期并行用量，不能当作精确单任务账单；时间线标记也不能自动识别模型花费。

![常用任务模板与清晰的勾选文字](screenshots/v1.14.0/task-name-templates.png)

![计量记录、再次计量与阶段标记](screenshots/v1.14.0/tasks-markers-matrix.png)

### 迷你双指标，主副数字独立选

主界面 **迷你 ▾** 或迷你面板右键菜单可选择主指标，开启双指标并选择副指标。可显示本月总用量、今天、滚动 24 小时、7 天、本月或已保存月份的观测增量；有正在计量的任务时才可选任务增量。

**不需要开启任务计量，也能同时显示“本月累计＋今天 / 七天增量”。** 任务结束后不再显示不可用的任务数字；尚无足够采样时显示等待状态。退出迷你恢复此前的紧凑或完整布局，选择保存在本机。

![迷你双指标：本月累计 $1,234.56，七天观测增量 +$399.05](screenshots/v1.14.0/mini-month-seven-days.png)

![迷你双指标：任务增量与本月累计](screenshots/v1.14.0/mini-dual-task.png)

![迷你主副指标菜单](screenshots/v1.14.0/mini-metrics-menu-zh.png)

### 月记录 / 热力图：每天花了多少

主界面 **月记录 / 热力图** 直接打开用量预估窗口中的对应页，也可从三点菜单进入。选择月份，色阶展示每日观测增量；每格显示金额，点击某天查看日期、精确增量和采样数。缺采样与真实零增长分别显示，今天与选中日期有独立高亮。

月度归档从已有采样整理月末累计、覆盖情况、活跃日期和保存的任务，可复制月度卡片图片。月度累计来自源页面；每日金额是已有采样之间的观测变化，缺失区间不会补造，因此每日金额之和不保证等于月度累计。

![每日热力图：点击日期查看金额与采样次数](screenshots/v1.14.0/heatmap-archives-matrix.png)

### 用量预估与提醒

预估突出 **预计月末累计量 → 是否预计达到目标 → 进度条 → 统计依据**。仅按 Overall 本月累计及 UTC 配额月已过时长计算：本月累计 ÷ 已过时长 × 当月总时长。优先使用设置的金额提醒阈值；它是个人目标，不会修改服务额度。没有可用金额目标或完整数值额度时，不推断是否耗尽；过旧、上月和未来读数暂停预估。

![精简的 Overall 月末预估](screenshots/v1.14.0/release-forecast-zh-CN.png)

金额阈值进度始终可见。示例 $1,234.56 / $2,000 = 61.73%；阈值为 $1,000 时为 123.46%，超出 $234.56。应用内警告在通知静音时仍显示。支持提醒冷却、免打扰、完全静默、重置前提醒、采样中断与恢复通知；Windows 通知送达受系统设置影响。

![超过金额阈值时的醒目警告](screenshots/v1.14.0/release-warning-zh-CN.png)

### 隐私、动画和桌面收纳

| 功能 | 操作与行为 |
| --- | --- |
| 隐藏金额 | 手动开启共享屏幕保护，主副金额同步隐藏，保留进度和状态；含金额的详情入口会要求先关闭保护。不会自动检测屏幕共享。 |
| 用量脉冲 | 读数增长时短暂强调数字与边框；可关闭，持续动画按已设置帧率运行。 |
| 桌面收纳 | 贴边吸附、贴边 3 秒后收起、鼠标移入展开、全屏应用时自动隐藏，均可选。 |
| 桌面锁定 / 穿透 | 固定位置避免误拖；鼠标穿透可通过托盘或 Ctrl+Alt+P 恢复。 |
| 更新说明卡 | “这次改了什么”展示主要变化并直达相关设置。 |

![隐藏金额后的迷你面板](screenshots/v1.14.0/mini-privacy.png)

### 设置、皮肤与图表

**⋯ → 设置 → 任务与桌面** 集中管理新增选项；主界面换肤仍直达设置的皮肤页。支持中英文、采样频率、数字字体、动画帧率与强度、自定义背景。关闭与完成操作固定显示；关于、在线更新、命名、记录窗口同步当前皮肤。矩阵动画和玻璃背景复用绘制结果；历史与计量曲线保持实色背景方便读图。

![设置内的任务与桌面选项](screenshots/v1.14.0/workspace-options-matrix.png)

![设置中的皮肤库](screenshots/v1.14.0/release-skins-zh-CN.png)

历史趋势支持今天、24 小时、7 天、本月和指定月份，以及悬停读值、固定提示、滚轮缩放、框选、重置和变化标记。今天从 Windows 本机午夜开始；配额月按每月 1 日 UTC 00:00 重置，并换算系统时区显示。缺失起点不会假定为零，刷新保留缩放。

![历史趋势与精确采样提示](screenshots/v1.14.0/release-hover-zh-CN.png)

可选全局快捷键 Ctrl+Alt+T / M / R / H 用于任务、迷你、刷新和最近任务；托盘支持暂停采样、恢复和悬停查看用量。**⋯ → 连接诊断** 独立检查会话、时区、采样、存储及快捷键，并导出诊断报告。


## v1.14.0: task observations and desktop controls

**v1.14.0 is available.** Download the Windows x64 installer from GitHub or use Updates in the app to download, verify, install and restart automatically. Screenshots use the Matrix skin with isolated fictional data.

![Compact Matrix panel with fictional data](screenshots/v1.14.0/release-compact-en.png)

### Named tasks and delayed settlement

Start a measurement, enter a note or choose a saved template, and wait for the first successful sample before working. Add timeline markers such as “Run tests” or “Switch model”. End & save now waits for 3 new successful samples, up to 10 minutes, to observe delayed source changes. Finish immediately to skip waiting; a timeout remains unconfirmed. Measurement history supports Measure again, renaming, summaries, sampling-health details and CSV / JSON / PNG exports.

Use it to observe a bug fix, review, image-generation session or proposal task. Source latency and concurrent activity affect the delta. Markers are time notes and do not identify model costs; observations are not exact task bills.

![Measurement history](screenshots/v1.14.0/release-tasks-en.png)

### Independent primary and secondary mini metrics

Use **Mini ▾** or the mini panel context menu to choose the main metric, enable dual metrics and choose the secondary metric. Monthly total, Today, rolling 24 hours / 7 days, current month and saved-month changes are available. Task delta is available only while a task is active.

**Monthly total plus Today or 7-day delta works without starting a task.** Unavailable task metrics fall back safely; insufficient samples show a waiting state. Choices persist, and expanding mini restores the previous compact/full mode.

![Monthly total plus 7-day observed change; Chinese UI](screenshots/v1.14.0/mini-month-seven-days.png)

![English mini metric menu](screenshots/v1.14.0/mini-metrics-menu-en.png)

### Daily heatmap and monthly archive cards

The main **Monthly records / Heatmap** action opens the corresponding tab inside Usage forecast. Choose a month; each cell shows daily observed change. Click a date for its exact amount and sample count. Missing samples are distinct from zero; today and the selection are highlighted. Monthly cards summarize available month-end usage, coverage, busiest day and saved tasks and can be copied as an image. Daily observed changes may not sum to the source monthly total because gaps are not fabricated.

![Clickable daily heatmap; Chinese UI](screenshots/v1.14.0/heatmap-archives-matrix.png)

### Overall forecast and alerts

The compact forecast prioritizes projected month-end total, target risk, progress and concise statistics. It uses Overall month-to-date usage ÷ elapsed UTC quota-month time × total month time. A configured amount alert target takes priority and does not change the service quota. Without an amount target or complete numeric source caps, depletion cannot be assessed. Stale, prior-month and future readings pause forecasting.

![Overall month-end forecast](screenshots/v1.14.0/release-forecast-en.png)

Amount threshold progress remains visible even with muted notifications. Fictional usage of $1,234.56 is 61.73% of $2,000; against $1,000 it is 123.46%, exceeding the target by $234.56. Cooldowns, quiet hours, silent mode, reset and sampling interruption/recovery alerts are configurable. Windows controls notification delivery.

![Threshold warning](screenshots/v1.14.0/release-warning-en.png)

### Privacy, animation and desktop placement

Manually hide amounts before sharing a screen: both mini values are masked while progress and status remain. Amount-detail views require protection to be disabled. Screen sharing is not detected automatically. Optional growth pulses briefly emphasize increased readings. Edge snapping, tuck after 3 seconds, hover to reveal, fullscreen hiding, desktop position lock and click-through reduce obstruction; tray controls or Ctrl+Alt+P restore interaction. A What's new card summarizes changes and links to settings.

Manage these controls under **Settings → Tasks & desktop**. The Skin shortcut opens the integrated skin gallery. Language, sampling interval, digit fonts and animation settings remain configurable; About, Updates and utility windows follow the skin. Chart backgrounds remain opaque for legibility.

![Integrated skin settings](screenshots/v1.14.0/release-skins-en.png)

History supports local Today, rolling ranges and UTC quota months, hover, pinning, zoom, range selection and change markers. Refresh retains zoom. Quota months reset at 00:00 UTC on day 1, also displayed in the system timezone. Missing baselines are never assumed to be zero. Optional Ctrl+Alt+T / M / R / H shortcuts, tray sampling controls and independent **⋯ → Connection diagnostics** remain available.

![Interactive history](screenshots/v1.14.0/release-hover-en.png)

## 截图与验证 / Captures and checks

截图使用实际应用、Matrix 皮肤及隔离的虚构数据。发布回归包含 286 项核心检查、9 项 SSO 路由检查和 12 项解析检查；另验证独立测试安装包的安装、更新、自动重启及原有数据保留。长期桌面行为仍需实际使用反馈。

任务记录保存增加短暂文件占用的有限重试；最终保存失败时保留旧记录。 / Task saves retry brief Windows file contention and retain prior records if saving fails.
