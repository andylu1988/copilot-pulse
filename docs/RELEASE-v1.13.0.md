# v1.13.0：从用量卡片到任务观察工具 / From usage widget to task observations

本次升级围绕“观察单次任务花费、判断配额风险、减少操作和界面等待”。所有新版截图均使用数字矩阵皮肤与隔离虚构数据；不是实际账户或账单。任务增量包含源页面延迟与并行用量，不代表精确单任务归因。

## 任务计量与迷你显示

开始时填写任务备注，等待首个成功采样作为零基线，再运行任务。结束后保存名称、起止时间、增量和采样记录；可重命名、复制摘要或导出。适合观察一次代码审查、Bug 修复或方案准备期间的用量变化。

![任务记录](screenshots/v1.13.0/release-tasks-zh-CN.png)

主界面可手动开启“迷你区间增量”，让迷你数字显示当前任务增量，默认仍显示月度累计。从迷你恢复时保留原来的紧凑 / 完整模式。

![迷你任务增量](screenshots/v1.13.0/release-mini-task-zh-CN.png)

## 采样健康、对比与导出

显示最近成功采样、失败、重试和估计缺失数；无数据不补造。两个任务可并排比较增量、时长、最大单次变化与采样数，导出 PNG；任务和历史支持匿名 CSV / JSON。匿名化移除账户和任务名，但仍保留金额、时间和任务 ID。

![任务对比](screenshots/v1.13.0/release-compare-zh-CN.png)

## 用量预估

优先显示预计月末累计量、按此速度是否耗尽，然后显示进度和小字统计依据。支持近一小时、24 小时和本月速度。按 UTC 月初 00:00 重置，并换算本机时区。至少两点且间隔一分钟；Standard 仅做标注的百分比近似推算，无数值额度不假定上限；低于 0.1%、过旧、上月或未来读数暂停相应预测。预测不是账单。

![用量预估](screenshots/v1.13.0/quota-risk-zh-CN.png)

## 提醒、快捷操作与图表

新增持久化提醒冷却、安静时段、完全静默、重置前提醒、中断与恢复通知。主界面阈值警告保持醒目，静音仍显示；托盘悬停可查看用量。

![阈值警告](screenshots/v1.13.0/release-warning-zh-CN.png)

可选 Ctrl+Alt+T / M / R / H 分别控制任务、迷你、刷新和最近任务；默认关闭。托盘支持任务和采样操作，多显示器位置恢复。图表支持悬停、固定提示、滚轮缩放、框选与重置；自动刷新保留缩放，并标记午夜、UTC 月重置和异常变化。

![历史图表](screenshots/v1.13.0/release-hover-zh-CN.png)

## 设置、诊断与性能

四个等大毛玻璃主按钮；左上角高清图标和外层玻璃统一圆角裁切。宽版分类设置固定关闭与完成操作，换肤合并到设置，主界面保留直达入口。历史与记录窗口保持实色。移除公开只读演示菜单，截图测试入口仍与真实数据隔离。

![设置与皮肤](screenshots/v1.13.0/release-skins-zh-CN.png)

连接诊断检查会话、时区、登录、采样、存储和快捷键，支持不含账户、金额、凭据或网页正文的报告。矩阵动画改为单张帧图，毛玻璃复用背景，解决此次测试复现的长时间窗口等待。本机两轮各 48 次窗口操作最大 UI 调度间隔约 1.2 / 1.0 秒；不同硬件表现会不同。

验证：224 项自动检查；隔离安装、重装、自动更新安装及客户端自动重启、版本 / 文件哈希和个人数据保留检查。测试工具也新增启动参数检查，避免缺少输出目录时弹出崩溃窗口。

## English

Version 1.13.0 adds named task observations, saved task history, sampling health, task comparison, anonymous CSV / JSON and PNG reports. Wait for the first successful sample before starting work; finish after source data catches up. These observations help compare individual tasks, but source latency and concurrent work prevent exact per-task billing.

![Task records](screenshots/v1.13.0/release-tasks-en.png)

Mini task delta is a manual main-panel option; monthly total remains the default. Returning from mini restores the previous compact or full layout.

Forecasts emphasize projected month-end usage, depletion risk, progress and concise statistics. Select one-hour, 24-hour or current-month rates. UTC monthly reset is displayed in the Windows timezone. Insufficient, stale, previous-month and future readings pause predictions. Standard without a numeric cap uses explicitly approximate percentage projections, with no assumed monetary cap and no extrapolation below 0.1%.

![Forecast](screenshots/v1.13.0/quota-risk-en.png)

Notifications gain persistent cooldowns, quiet hours, silent mode, reset, interruption and recovery alerts. Widget warnings remain visible while muted. Tray hover shows usage. Optional Ctrl+Alt+T / M / R / H shortcuts, tray actions and multi-monitor placement simplify daily use. Interactive charts add hover, pinning, zoom, range selection, persistent zoom on refresh and boundary / anomaly markers.

![Threshold warning](screenshots/v1.13.0/release-warning-en.png)

Four equal glass buttons, a crisp rounded app badge, wider categorized Settings and an integrated skin gallery refresh the interface. Close / Done stay visible; the main Skin shortcut opens the settings page directly. History windows remain opaque. The public demo menu is removed; synthetic screenshot tooling remains isolated.

![Settings and skins](screenshots/v1.13.0/release-skins-en.png)

Connection diagnostics checks timezone, session, login, sampling, storage and shortcuts; exports exclude accounts, amounts, credentials and page content. Matrix rendering now uses a single composited frame and shared glass backgrounds to reduce long window stalls. Local validation: 224 checks, isolated install / reinstall / detached updater / restart / version / checksum / data-preservation checks, plus two 48-operation window runs. Timing results are local measurements, not a universal performance guarantee.

All screenshots are fictional review scenarios. Anonymous task exports still include timestamps, amounts and task IDs. [Full introduction](../README.md) · [Download](https://github.com/andylu1988/copilot-pulse/releases/tag/v1.13.0)
