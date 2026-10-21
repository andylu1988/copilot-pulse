# 文档截图说明

这组截图用于 Copilot Pulse 1.9.6 的介绍与指南。所有账户、金额、上限、比例和历史都是虚构示例，不来自任何真实用户。

截图由实际 WPF 控件渲染，保留示例标记。在仓库外建立隔离构建，仅调整示例数据与截取流程，使企业、总金额和历史末值一致；未修改发布版采集实现，未读取真实会话，未写入真实历史。

| 企业 | 示例登录名 | 累计金额 | 使用上限 | 比例 |
| --- | --- | --- | --- | --- |
| Example Enterprise | demo-emu | $1,200.00 | $1,500.00 | 80.00% |
| Example Organization | demo-user | $34.56 | $100.00 | 34.56% |
| 合计 | — | $1,234.56 | — | — |

历史覆盖约七天，按十分钟生成递增读数，留一处采样缺口以展示断线与变化柱状图。最后读数与主卡片一致，截图日期、采样次数与区间增长只是本组示例结果。

| 图片 | 内容 |
| --- | --- |
| `overview.png` | 实际组件、历史和迷你截图的总览拼图。 |
| `widget-full.png` / `widget-compact.png` | 矩阵完整与紧凑模式。 |
| `widget-cyber.png` | 霓虹协议完整模式。 |
| `mini-skins.png` | 八种迷你皮肤拼图。 |
| `mini-*.png` | 矩阵、液晶、霓虹、终端、辉光、雷达、机甲、电子墨水。 |
| `history-today.png` / `history-week.png` | 今天与七天累计走势。 |
| `history-hover.png` | 悬停读数详情。 |
| `history-changes.png` | 今天变化柱状图。 |
| `appearance.png` | 换肤与动效设置。 |

单窗口图片为实际控件的 PNG。总览和皮肤拼图只缩放、排版、添加标题，不另造按钮或功能。PNG 无损优化；静态图展示动画的一帧。
## 1.9.8 新增操作截图

- `onboarding-connect.png`：首次连接空状态的实际控件；不显示示例历史与金额。
- `onboarding-ready.png`：连接成功状态的实际控件，虚构总额 $1,234.56、两个示例账户。
- `onboarding.png`：上述截图组成三步图。中间栏是文字流程说明，不是登录页面截图，不伪造认证控件。
- `history-month-utc.png`：实际历史窗口，展示本月 UTC 起点转换为本机时间，以及“月份 ▾”入口。该组示例历史净变化为 +$13.90。

所有新增图来自仓库外隔离构建，不连接真实数据源、不读取账户或 Cookie、不写真实历史。金额与用户身份均为虚构。旧版截图保留以展示皮肤；其中旧的“全部”入口已在 1.9.8 替换为“月份 ▾”。

- v1.10.0 新增 `online-update.png`、`widget-threshold-progress.png` 并更新中英文设置截图，来自实际应用的隔离示例验证。阈值截图使用每个账户 $100,000 的虚构限额。

## v1.11.1 阈值与通知示例

- `compact-matrix-threshold.png`：实际紧凑矩阵控件的隔离示例截图，金额警告阈值 $1,000.00、本月用量 $1,234.56，进度 123.46%；两个示例账户使用虚构 $100,000 限额。
- `usage-warning-notification.png`：左侧缩放上述实际截图；右侧人工排版的深色 Windows 通知示意，标题和正文与 `MainWindow.Settings.cs` 的金额警告文案一致。不是实际系统通知截图，实际外观取决于 Windows 版本与通知设置；进度说明块属于介绍图注，不是应用控件。

新增图的金额、账户与趋势全部虚构。未连接数据源、读取登录资料或修改用户设置及历史。静态矩阵图片展示动画的一帧。

新增英文对应截图及 `threshold-progress-matrix.png` / `threshold-progress-matrix-en.png`：$2,000 阈值、61.73% 进度，尚未达到警告线。所有新图月初 $0、末值 $1,234.56、本月净增长 +$1,234.56；真实历史不补造月初基线。

## v1.11.2 在线更新

- `online-update.png` 更新为实际 1.11.2 控件截图，展示一键检查、下载、校验、安装与重启入口。来自隔离界面验证，未读取真实账户数据。
- The online update screenshot shows the actual 1.11.2 controls from an isolated UI review, without real account data.

## v1.12.0 记录区间增量 / Recorded period delta

- `record-point-matrix.png` / `record-point-matrix-en.png`：实际紧凑矩阵控件，加上单次任务用量观察的操作说明。
- `recorded-delta-matrix.png` / `recorded-delta-matrix-en.png`：实际独立增量窗口，累计增量 +$611.05，共50笔读数。
- `mini-restore-matrix.png` / `mini-restore-matrix-en.png`：实际迷你与紧凑矩阵控件的排版，说明恢复原先布局。

使用已发布的1.12.0程序集，`--demo --skin matrix --review-history` 与 `--demo --skin matrix --mini --capture` 隔离渲染。图注在控件外添加，未改造按钮或数字。所有账户、金额与日期历史均为虚构示例；静态图展示字符雨的一帧。

本月累计 $1,234.56，记录起点首笔 $623.51，记录后增量 $611.05；示例覆盖多天，不是一次真实任务。单次任务观察需等待基线，再运行任务并等源站更新，包含同期其他用量，不是精确账单。

Actual released v1.12.0 WPF controls rendered in isolated Matrix demo mode; surrounding captions are editorial. All data is synthetic. The multi-day sample illustrates the arithmetic, not a real task cost. Task observations depend on source latency and may include concurrent usage.

## v1.13.1 Matrix 截图 / Matrix screenshots

`v1.13.1/` 包含中英文主界面、任务计量、计量记录、迷你、预估、警告、历史交互、皮肤设置、关于、在线更新、任务命名与独立诊断。来自实际发布代码的隔离 review 入口，不连接真实数据、不读取用户登录、不改真实历史。全部为 Matrix 皮肤，截图标记为虚构示例。预估风险、达到阈值等图为独立验证场景；发布主截图月累计为 $1,234.56，完整本月净增长同为 $1,234.56。静态图片显示动画一帧。

Actual WPF controls rendered with synthetic accounts, amounts and tasks. All new screenshots use Matrix. Forecast scenario images test independent cases; no real user sessions or records are used. Notification illustrations retained from earlier versions are labeled separately.
