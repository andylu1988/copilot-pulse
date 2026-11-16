# v1.12.0 之后的全部版本变更 / All updates after v1.12.0

核对日期：2026-11-16。v1.12.0 之后已发布 **v1.13.0、v1.13.1、v1.14.0、v1.15.0**。以下按公开发布版本列出（1.14.1 / 1.14.2 为本机迭代验证，已完整并入 v1.15.0 正式版）。时间为 Asia/Shanghai。

## v1.13.0 · 已发布 / Published · 2026-10-21 22:06

- 任务备注、计量记录、采样健康、失败 / 重试统计、摘要复制、CSV / JSON / PNG 导出。
- 迷你手动显示任务增量，并记住切换前紧凑 / 完整模式。
- 新增用量预估、多种速度口径与区间对比；后两项在 1.13.1 精简，已不属于当前功能。
- 提醒冷却、免打扰、静默、配额重置前提醒、采样中断和恢复通知。
- 可选全局快捷键、托盘采样操作、悬停用量、多显示器位置恢复。
- 图表悬停、固定提示、缩放、框选、刷新保留视图及午夜 / 重置 / 异常变化标记。
- 等大 2×2 玻璃按钮、高清圆角图标、分类设置、皮肤页合并；诊断与报告。
- 矩阵帧合成及玻璃背景缓存，减少窗口等待；修正测试启动参数异常。

[当时完整说明](RELEASE-v1.13.0.md) · [GitHub release](https://github.com/andylu1988/copilot-pulse/releases/tag/v1.13.0)

## v1.13.1 · 已发布 / Published · 2026-10-22 00:21

- 合并 1.13.0 功能与反馈修正，统一“任务计量 / 计量记录 / 用量预估”名称。
- 任务未开始时禁用旧版迷你任务选项；结束后安全恢复月度累计，消除无意义横线。
- 预估改为 Overall 本月平均速度；优先读取设置中的金额阈值，明确区分个人目标与服务额度。
- 突出预计月末累计、目标风险与进度；暂停过旧、上月或未来读数的推算。
- 移除区间对比和速度切换，移出登录 / 采样操作，诊断独立放入三点菜单。
- 关于、更新、命名、记录、设置与皮肤页统一玻璃风格；保留实色图表背景。
- 整理时区、阈值警告、设置与安装 / 更新相关验证和双语文档。

[当时完整说明](RELEASE-v1.13.1.md) · [GitHub release](https://github.com/andylu1988/copilot-pulse/releases/tag/v1.13.1)

## v1.14.0 · 已发布 / Published · 2026-10-23

1. 任务结束延迟结算：3 次新的成功采样，最多 10 分钟；可立即结束，超时标未确认。
2. 迷你双指标：主副独立选，本月总量可搭配今天 / 七天等增量，无需开启任务；任务选项关联有效计量状态。
3. 任务模板与再次计量：复用名称、减少重复输入，修正模板下拉框和勾选项可读性。
4. 每日热力图：整合到用量预估，每格金额、点击日期明细、月份选择、缺采样识别。
5. 共享屏幕保护：手动隐藏主副金额，详情入口保护，保留状态和进度。
6. 桌面自动收纳：可选贴边吸附、3 秒收起、悬停展开、全屏隐藏。
7. 更新说明卡：主要变化与相关设置入口。
8. 用量变化脉冲：增长时短暂强调，可关闭并沿用动画帧率限制。
9. 月度归档卡片：已有数据的累计、覆盖、活跃日、任务摘要，可复制图片。
10. 桌面锁定与鼠标穿透：托盘或 Ctrl+Alt+P 恢复操作。
11. 任务时间线标记：记录阶段备注，不推断具体模型费用。

另：主界面快捷按钮改为“月记录 / 热力图”；任务与桌面选项合入设置；去掉预估窗口多余的内部关闭按钮；修正隐藏金额后迷你背景和副指标可读空间，双指标与选择持久化联动。

[当时完整说明](RELEASE-v1.14.0.md) · [GitHub release](https://github.com/andylu1988/copilot-pulse/releases/tag/v1.14.0)

## v1.15.0 · 已发布 / Published · 2026-11-16

1. 原生 Windows ARM64 与双架构智能更新：同时提供 `win-x64` 与 `win-arm64` 自包含安装包（启用 `TieredPGO`）；Windows on ARM 上的旧版 x64 仿真客户端可通过在线更新自动切换至原生 ARM64 安装包；关于、更新与诊断窗口展示运行架构与能效档案。
2. 电源感知渲染与后台低内存采集：冻结画刷/画笔/字体渲染缓存与自适应 `WriteableBitmap` 矩阵缓冲；最小化或工具窗口失焦自动降至 `0 FPS`，电池供电与系统节电模式平滑限帧；后台采样完成后隐藏 WebView2 自动切换低内存目标。
3. 一键贴边收纳与中央恢复：支持立即靠左/靠右收纳并恢复鼠标交互，保留 24 DIP 裁剪可点击边条，支持拖动保护、32 DIP DPI 缩放吸附及“移回中央，停止自动隐藏”。
4. 可见增长脉冲与安全预览：延长正向增长徽章停留时间，新增不写入数据、不改金额的“预览用量增长脉冲”。
5. 桌面显示预设、新鲜度外框与开机自启：内置日常看板、专注计量、安静桌面、屏幕分享四种预设；可选采样新鲜度状态边框；新增当前用户登录 Windows 时启动开关；整理分类设置与三点分组菜单。

[完整介绍及最新截图](RELEASE-v1.15.0.md) · [GitHub release](https://github.com/andylu1988/copilot-pulse/releases/tag/v1.15.0)

## English version history

**v1.13.0 (published)** introduced named task measurements, retained records, sampling health, summaries, CSV/JSON/PNG exports, opt-in mini task delta, forecasts, task comparison, notification controls, shortcuts, tray usage, interactive charts, categorized settings and rendering improvements.

**v1.13.1 (published)** unified terminology and themed utilities, fixed inactive task mini states, simplified forecasts to the Overall month-to-date average with configured amount targets, removed task comparison and rate switching, and moved diagnostics into its own main-menu window.

**v1.14.0 (published)** adds delayed task settlement, independent mini dual metrics, templates/measure again, clickable daily heatmap, manual amount privacy, edge tuck/fullscreen hiding, What's new, usage-growth pulse, monthly archive cards, desktop lock/click-through and task timeline markers. It also integrates task/desktop controls into Settings and adds the main heatmap shortcut. [v1.14.0 release notes](RELEASE-v1.14.0.md).

**v1.15.0 (published)** adds native Windows `arm64` alongside `x64` self-contained installers with `TieredPGO`, automatic x64-to-ARM64 in-app update migration on Windows on ARM, power-aware FPS throttling (`0 FPS` when minimized/unfocused, adaptive battery & Energy Saver caps), frozen render caches, idle WebView2 low-memory target, immediate left/right edge tuck with 24 DIP multi-monitor clipping and center recovery, visible growth pulse with unrecorded preview, four desktop display presets, data freshness border states, and launch at Windows sign-in. Download [v1.15.0](RELEASE-v1.15.0.md) or update from the app.
