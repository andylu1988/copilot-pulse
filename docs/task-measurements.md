# 任务计量与采样健康 / Task measurements and sampling health

v1.13.0。可用来观察单次任务（修复 Bug、方案准备、代码审查）的用量变化。

## 中文

1. 在主界面点击 **开始任务计量**，输入任务备注。点击时刻是起点；随后首个成功采样作为零基线，不使用点击之前的缓存读数。
2. 等待第一个成功采样建立零基线后开始工作，观察 **记录区间增量**。主界面显示采样健康：刚更新、正在采样、数据延迟、页面未刷新或暂停。悬停可查看最近成功时间和目标刷新间隔。
3. 任务结束后，等待页面读数更新，再点击 **结束并保存**。任务名称、起止时间、增量和采样记录保留，重启后仍可回看。
4. 点击 **任务记录 / 导出**，选择已保存任务，查看曲线、有效采样数、估计缺失次数、失败刷新、重试和最近成功时间；可修改任务备注、复制摘要。
5. 点击 **导出所选区间** 或 **导出全部历史**，选择 CSV / JSON。CSV 包含任务、采样和刷新事件的分类型行；JSON 保留结构化数据。

**计算口径**：这是配额页面的观测增量，不是精确任务账单。源页面可能延迟，其他同时运行的任务也可能计入。无有效读数时显示 `—`；缺失区间不会伪造补点。缺失数是根据采样间隔估计，不是确切漏采数；历史旧数据没有刷新事件日志。跨月按 UTC 配额重置边界和可比较账户计算，界面时间按本机系统时区显示。

**保存与分享**：在现有应用数据目录保存 `tasks.json`（任务元数据）、`history/yyyy-MM.jsonl`（成功读数）、`sampling/yyyy-MM.jsonl`（刷新状态）。数据未加密；日志不记录令牌或网页正文。匿名化默认开启，移除账户信息和任务名称，仍保留时间、金额和任务 ID，分享前请检查。CSV 不含账户详情。

**迷你模式**：“迷你显示记录区间”仍是手动选项，开启后显示当前任务的增量，不会自动替换月度累计显示。没有有效采样时暂显示 `—`。

## English

Version 1.13.0 observes usage changes during an individual task, such as a bug fix, proposal or code review.

1. Click **Start task** on the widget and enter a name. The click establishes the start time; the first successful sample afterwards establishes zero. Cached readings from before the click are excluded.
2. Work while observing the **Recorded period delta**. Sampling health shows fresh, sampling, delayed, page needs attention, or paused. Hover for the last successful read and target interval.
3. Wait for source readings to catch up, then choose **End & save**. Task names, times, deltas and samples remain available after restarting.
4. Open **Task records / Export**, select a saved task and inspect its chart, valid samples, estimated gaps, failed refreshes, retries and last successful read. Rename the task or copy its summary.
5. Use **Export selected** or **Export all history**, then choose CSV or JSON. CSV uses typed rows for tasks, samples and refresh events; JSON preserves structured records.

**Interpretation:** These are observed quota changes, not exact per-task billing. The source may lag and concurrent work can contribute to the same readings. No valid sample is shown as `—`; gaps are never filled with fabricated readings. Missing-sample counts are estimates based on cadence. Older history has no refresh event journal. Calculations respect UTC monthly resets and comparable accounts; displayed timestamps use the system's local timezone.

**Storage and sharing:** The existing app data folder contains `tasks.json` (metadata), `history/yyyy-MM.jsonl` (successful readings), and `sampling/yyyy-MM.jsonl` (refresh states). Local files are unencrypted. The sampling journal excludes tokens and page text. Anonymization is enabled by default: account details and task names are removed, while times, amounts and task IDs remain. Review before sharing. CSV never contains account details.

**Mini mode:** Showing the current task delta remains an explicit option on the main widget. It does not automatically replace monthly usage. The value is `—` until a valid sample exists.


单任务 PNG、双任务对比截图及后续功能见 [用量预估指南 / Usage forecast guide](usage-insights.md)。
