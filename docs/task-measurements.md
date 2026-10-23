# 任务计量 / Task measurements

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


### Named tasks and delayed settlement

Start a measurement, enter a note or choose a saved template, and wait for the first successful sample before working. Add timeline markers such as “Run tests” or “Switch model”. End & save now waits for 3 new successful samples, up to 10 minutes, to observe delayed source changes. Finish immediately to skip waiting; a timeout remains unconfirmed. Measurement history supports Measure again, renaming, summaries, sampling-health details and CSV / JSON / PNG exports.

Use it to observe a bug fix, review, image-generation session or proposal task. Source latency and concurrent activity affect the delta. Markers are time notes and do not identify model costs; observations are not exact task bills.

![Measurement history](screenshots/v1.14.0/release-tasks-en.png)

### Independent primary and secondary mini metrics

Use **Mini ▾** or the mini panel context menu to choose the main metric, enable dual metrics and choose the secondary metric. Monthly total, Today, rolling 24 hours / 7 days, current month and saved-month changes are available. Task delta is available only while a task is active.

**Monthly total plus Today or 7-day delta works without starting a task.** Unavailable task metrics fall back safely; insufficient samples show a waiting state. Choices persist, and expanding mini restores the previous compact/full mode.

![Monthly total plus 7-day observed change; Chinese UI](screenshots/v1.14.0/mini-month-seven-days.png)

![English mini metric menu](screenshots/v1.14.0/mini-metrics-menu-en.png)


[完整说明](RELEASE-v1.14.0.md)
