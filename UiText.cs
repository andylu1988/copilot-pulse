using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CopilotPulse;

internal static class UiText
{
    internal static string Language { get; set; } = "zh-CN";
    internal static bool English => Language == "en";
    internal static string T(string zh, string en) => English ? en : zh;
    internal static string TaskName(string name) => name switch
    {
        "Recorded period" => T("记录区间增量", "Recorded period"),
        "Untitled task" => T("未命名任务", "Untitled task"),
        _ => name
    };
    private static readonly Dictionary<string, string> Words = new()
    {
        ["任务记录 / 导出 ↗"] = "Tasks / Export ↗",
        ["开始任务计量"] = "Start task",
        ["用量预估 ↗"] = "Usage forecast ↗",
        ["迷你"] = "Mini",
        ["换肤"] = "Appearance",
        ["展开"] = "Expand",
        ["本月内部用量"] = "Monthly internal usage",
        ["等待连接数据源"] = "Waiting for data source",
        ["历史趋势  ↗"] = "Usage history  ↗",
        ["从首次记录开始积累"] = "Records accumulate from first capture",
        ["本月净增长 —"] = "Month net change —",
        ["示例预览 · 虚构金额，非实时数据"] = "Demo preview · synthetic amounts, not live",
        ["企业用量"] = "Enterprise usage",
        ["让用量，随时可见。"] = "Keep your usage in view.",
        ["连接 Microsoft 账户后，自动同步本月总额和企业明细。"] = "Connect your Microsoft account to sync monthly totals and enterprise details.",
        ["连接 Microsoft 账户  ↗"] = "Connect Microsoft account  ↗",
        ["Monthly limit 为使用上限，不代表预算。"] = "Monthly limit is a usage cap, not a budget.",
        ["切换迷你数字框"] = "Switch to mini widget",
        ["选择皮肤和自定义背景"] = "Choose skin and background",
        ["切换置顶"] = "Toggle always on top",
        ["设置 / 历史 / 刷新"] = "Settings / History / Refresh",
        ["设置…"] = "Preferences…",
        ["设置"] = "Settings",
        ["收起到系统托盘"] = "Hide to system tray",
        ["打开用量历史"] = "Open usage history",
        ["立即刷新"] = "Refresh now",
        ["打开原始页面 / 登录"] = "Open source page / Sign in",
        ["打开原始页面或登录"] = "Open source page or sign in",
        ["展开完整模式"] = "Expand full mode",
        ["选择皮肤"] = "Choose skin",
        ["正在连接"] = "Connecting",
        ["尚未同步"] = "Not synced yet",
        [" 个企业账户"] = " enterprise accounts",
        ["已使用 "] = "Used ",
        ["页面显示的限额方案："] = "Quota plan from source: ",
        ["。页面未提供具体美元限额。"] = ". The page does not provide a numeric USD quota.",
        ["已同步 · "] = "Synced · ",
        [" 秒自动刷新"] = " seconds auto refresh",
        ["正在同步…"] = "Syncing…",
        ["需要 Microsoft 登录"] = "Microsoft sign-in required",
        ["连接失败 · 等待重试"] = "Connection failed · retry pending",
        ["页面数据未就绪"] = "Source data not ready",
        ["WebView2 无法启动"] = "WebView2 could not start",
        ["上次保存的数据"] = "Previously saved data",
        ["示例预览"] = "Demo preview",
        ["点击右下角 ↗ 打开页面，完成 Microsoft 登录后自动同步。"] = "Use ↗ to open the page. Sync resumes after Microsoft sign-in.",
        ["暂时无法刷新，请检查网络或 VPN。稍后自动重试。"] = "Check your network or VPN. Automatic retry will follow.",
        ["未能完整读取用量。请打开原页面，确认 Consumption 标签已加载。"] = "Open the source page and check that Consumption has loaded.",
        ["请安装或修复 Microsoft Edge WebView2 Runtime，然后重启。"] = "Install or repair Microsoft Edge WebView2 Runtime, then restart.",
        ["当前显示上次成功同步的数据。"] = "Showing the last successfully synced data.",
        ["虚构示例 · 自动刷新未连接"] = "Synthetic demo · live refresh disconnected",
        ["更新于 "] = "Updated ",
        ["自动刷新已暂停"] = "Auto refresh paused",
        ["减少动态效果"] = "Reduce motion",
        ["始终置顶"] = "Always on top",
        ["自动刷新间隔"] = "Auto refresh interval",
        ["暂停自动刷新"] = "Pause auto refresh",
        ["用量历史"] = "Usage history",
        ["Windows 账户登录 / 数据源"] = "Windows account sign-in / Data source",
        ["在系统浏览器打开页面"] = "Open page in system browser",
        ["退出 Copilot Pulse"] = "Exit Copilot Pulse",
        ["Copilot Pulse · 本月用量"] = "Copilot Pulse · Monthly usage",
        ["显示小组件"] = "Show widget",
        ["迷你数字模式"] = "Mini number mode",
        ["换肤与背景…"] = "Skins and backgrounds…",
        ["退出"] = "Exit",
        ["显示模式"] = "Display mode",
        ["迷你数字"] = "Mini number",
        ["紧凑"] = "Compact",
        ["完整"] = "Full",
        ["皮肤预览与图片设置…"] = "Skin previews and image settings…",
        ["Copilot Pulse · 用量历史"] = "Copilot Pulse · Usage history",
        ["本机记录"] = "Local records",
        ["历史文件夹 ↗"] = "History folder ↗",
        ["关闭历史窗口"] = "Close history window",
        ["总用量  ▾"] = "Total usage  ▾",
        ["总用量"] = "Total usage",
        ["今天"] = "Today",
        ["24 小时"] = "24 hours",
        ["7 天"] = "7 days",
        ["本月"] = "This month",
        ["月份 ▾"] = "Months ▾",
        ["区间最后读数"] = "Latest in range",
        ["区间净变化"] = "Net change in range",
        ["成功采样"] = "Successful samples",
        ["累计用量"] = "Cumulative usage",
        ["单次变化"] = "Per-sample change",
        ["示例模式"] = "Demo mode",
        ["示例数据 · 非真实历史"] = "Demo data · not real history",
        ["暂时无法读取历史文件，请检查磁盘或权限。"] = "Cannot read history. Check disk and permissions.",
        ["末次读数减去首次读数；零金额企业增减不影响比较，负变化保留。"] = "Last minus first reading; zero-cost membership changes are ignored. Negative changes are retained.",
        ["实际记录 "] = "Recorded ",
        [" 次"] = " samples",
        ["单次变化 = 本次减去上次连续读数；每次间隔取决于刷新频率。中断和跨月不计算增量。"] = "Per-sample change compares consecutive readings. The interval depends on refresh frequency. Gaps and month resets break continuity.",
        ["鼠标悬停查看读数 · 采样中断与跨月重置分段显示 · 纵轴按实际金额缩放"] = "Hover for readings · Gaps and month resets are segmented · Axis scales to actual amounts",
        ["示例数据用于验证外观，不会写入你的历史文件。"] = "Demo data is for appearance checks and is never saved to your history.",
        ["这个时间范围内没有记录。可点击“月份 ▾”选择有记录的月份。"] = "No records in this range. Choose a recorded month from Months ▾.",
        ["最后记录 "] = "Last record ",
        [" · 数据仅保存在本机，不补造采集前的历史。"] = " · Data stays on this PC; earlier history cannot be reconstructed.",
        ["净变化为负：区间记录可能包含源页面累计值归零或金额修正，不代表负消费。  "] = "Negative net change may reflect source resets or corrections, not negative spending.  ",
        ["净变化不可计算："] = "Net change unavailable: ",
        ["随成功刷新持续记录"] = "Recorded on successful refresh",
        ["显示已保存记录"] = "Showing saved records",
        ["还没有月份记录"] = "No recorded months yet",
        ["暂时无法读取历史月份，请检查磁盘或权限。"] = "Cannot read recorded months. Check disk and permissions.",
        ["净增长 "] = " net change ",
        ["示例趋势 · 点击展开"] = "Demo trend · click to expand",
        [" 次记录 · 点击展开"] = " records · click to expand",
        ["历史暂不可读 · 点击查看"] = "History unavailable · click to inspect",
        ["所选区间总用量的末次读数减去首次读数；未采集时段不补算。负变化可能包含源页面累计值归零或金额修正，不代表负消费。"] = "Last minus first reading in the selected range. Missing data is not invented; negative changes may reflect resets or corrections.",
        ["记录不足：需要至少两次读数。"] = "At least two readings are required.",
        ["区间跨月：月累计值会重置，请选择单个月份内的区间。"] = "Monthly totals reset; select a range within one UTC month.",
        [" 有非零金额企业加入或移出，合计口径改变，无法比较。"] = ": nonzero-cost account membership changed; totals cannot be compared.",
        ["下一次连续刷新后，将显示单次变化"] = "Per-sample changes appear after another consecutive refresh",
        ["正在积累真实记录"] = "Collecting usage records",
        ["起始记录"] = "First record",
        ["新月份 · 累计值重新开始"] = "New month · total resets",
        ["企业账户集合变化"] = "Account membership changed",
        ["采样中断 · 不连接缺失区间"] = "Collection gap · missing interval omitted",
        ["较上次 "] = "Change ",
        [" · 间隔 "] = " · Interval ",
        [" 分钟"] = " minutes",
        [" 秒"] = " seconds",
        ["示例金额 "] = "Example amount ",
        ["本月内部用量："] = "Monthly internal usage: ",
        ["最近更新："] = "Last update: ",
        ["尚无记录"] = "No records yet",
        ["拖动移动 · 双击展开 · 右键设置"] = "Drag to move · double-click to expand · right-click for settings",
        ["已同步"] = "Synced",
        ["正在同步"] = "Syncing",
        ["示例"] = "Demo",
        ["已暂停"] = "Paused",
        ["需要登录"] = "Sign-in required",
        ["缓存 · 待更新"] = "Cached · update pending",
        ["等待连接"] = "Waiting for connection",
        ["Copilot Pulse · 换肤"] = "Copilot Pulse · Appearance",
        ["换一个心情"] = "A change of mood",
        ["选择即生效 · 组件和历史窗口同步切换"] = "Apply instantly to widget and history",
        ["关闭换肤窗口"] = "Close appearance window",
        ["迷你框：双击展开 / 右键设置"] = "Mini: double-click to expand / right-click for settings",
        ["实时预览 · 示例金额"] = "Live preview · sample amount",
        ["特效强度"] = "Effect intensity",
        ["动画速度"] = "Animation speed",
        ["也可以使用自己的图片"] = "Use your own background image",
        ["PNG / JPG / BMP · 保存本机副本"] = "PNG / JPG / BMP · local copy saved",
        ["选择图片…"] = "Choose image…",
        ["动态效果：开启"] = "Motion: on",
        ["动态效果：关闭"] = "Motion: off",
        ["隐藏到托盘自动停止特效"] = "Effects stop while hidden in tray",
        ["已遵循 Windows 的减少动画设置"] = "Following Windows reduce-motion setting",
        ["照片遮罩"] = "Photo dimming",
        ["向右更暗，让数字更清楚"] = "Move right to darken the photo",
        ["水平取景"] = "Horizontal focus",
        ["垂直取景"] = "Vertical focus",
        ["背景水平取景"] = "Background horizontal focus",
        ["背景垂直取景"] = "Background vertical focus",
        ["照片遮罩深浅"] = "Background dimming",
        ["选择会自动记住。关闭窗口即可继续使用。"] = "Choices are saved automatically. Close this window to continue.",
        ["自定义图片已保存到本机"] = "Custom image saved on this PC",
        ["选择自定义皮肤图片"] = "Choose custom background image",
        ["较上次 "] = "Change ",
        [" · 间隔 "] = " · Interval ",
        [" 分钟"] = " min",
        ["背景图片"] = "Background images",
        ["图片已保存。可用下方滑块调整遮罩和取景。"] = "Image saved. Use sliders to adjust dimming and focus.",
        ["未能加载图片。请选择有效的 PNG、JPG 或 BMP，大小不超过 25 MB。"] = "Choose a valid PNG, JPG or BMP up to 25 MB.",
        ["动态效果已暂停，保留静态外观"] = "Motion paused; static appearance retained",
        ["柔和 ← 强度 → 炫酷 · 自动保存"] = "Subtle ← intensity → vivid · automatically saved",
        ["当前为静态皮肤，可选择上方动态皮肤"] = "Static skin selected; choose an animated skin above",
        ["矩阵雨 · 精工"] = "Matrix rain",
        ["曜石金属 · 翡翠微光"] = "Obsidian metal · emerald glow",
        ["极客终端"] = "Geek terminal",
        ["黑白等宽 · 呼吸光标"] = "Monospace · breathing cursor",
        ["琥珀工作站"] = "Amber workstation",
        ["单色数码 · 慢速扫描"] = "Monochrome digits · slow scan",
        ["矢量示波器"] = "Vector scope",
        ["磷光轨迹 · 精密点网"] = "Phosphor trails · dot grid",
        ["调试面板"] = "Debug panel",
        ["代码行号 · 插入光标"] = "Code line numbers · caret",
        ["全息雷达"] = "Holographic radar",
        ["旋转扫掠 · 回波光点"] = "Rotating sweep · echo dots",
        ["机甲 HUD"] = "Mecha HUD",
        ["反向环轨 · 能量灯带"] = "Counter-rotating rings · energy strips",
        ["落日合成波"] = "Sunset synthwave",
        ["条纹落日 · 无限网格"] = "Striped sunset · infinite grid",
        ["星际穿梭"] = "Warp drive",
        ["纵深星流 · 曲速航行"] = "Starfield · warp travel",
        ["等离子流"] = "Plasma flow",
        ["双色光带 · 粒子追逐"] = "Dual light ribbons · particles",
        ["掌上液晶"] = "Pocket LCD",
        ["七段数字 · 复古显示屏"] = "Seven-segment digits · retro display",
        ["霓虹协议"] = "Neon protocol",
        ["双向扫光 · 电路追光"] = "Bidirectional light · circuits",
        ["绿磷终端"] = "Green phosphor",
        ["点阵数字 · CRT 扫描线"] = "Dot-matrix digits · CRT scanlines",
        ["琥珀辉光"] = "Amber glow",
        ["玻璃灯管 · 温暖呼吸光"] = "Glass tubes · warm glow",
        ["极光玻璃"] = "Aurora glass",
        ["流动光晕 · 通透质感"] = "Flowing glow · glass",
        ["像素街机"] = "Pixel arcade",
        ["像素数字 · 闪烁星点"] = "Pixel digits · twinkling stars",
        ["电子墨水"] = "E-ink",
        ["纸张纹理 · 安静无动效"] = "Paper texture · no animation",
        ["深海蓝"] = "Midnight blue",
        ["经典配色"] = "Classic palette",
        ["曜石黑"] = "Graphite",
        ["简洁沉稳"] = "Simple and calm",
        ["松林绿"] = "Forest green",
        ["柔和清新"] = "Soft and fresh",
        ["暮光紫"] = "Twilight violet",
        ["温柔夜色"] = "Soft night colors",
        ["巴依老爷"] = "Bayi",
        ["你的图片背景"] = "Your background image",
        ["自定义图片"] = "Custom image",
        ["选择本机图片"] = "Choose local image",
        ["使用"] = "Use ",
        ["自定义图片暂不可用，请重新选择。当前使用深色底。"] = "Custom image unavailable. Please choose it again.",
        ["记录仅保存在本机；安装前的历史无法补回。"] = "Records stay on this PC; history before installation cannot be recovered.",
        ["鼠标悬停查看读数 · 采样中断与跨月重置分段显示"] = "Hover for readings · Gaps and month resets are segmented",
    };
    private static readonly KeyValuePair<string, string>[] Ordered = Words.OrderByDescending(p => p.Key.Length).ToArray();
    private static readonly System.Text.RegularExpressions.Regex Pattern = new(string.Join("|", Ordered.Select(p => System.Text.RegularExpressions.Regex.Escape(p.Key))), System.Text.RegularExpressions.RegexOptions.Compiled);
    internal static string Translate(string text)
    {
        if (!English) return text;
        if (Words.TryGetValue(text, out string? exact)) return exact;
        // Match source segments once, avoiding replacements inside already translated segments.
        return Pattern.Replace(text, m => Words[m.Value]);
    }
    internal static string Range(string? range) => Translate(HistoryRanges.Label(range));
    private sealed class Originals { internal Dictionary<DependencyProperty, (string Source, string Display)> Values = new(); }
    private static readonly ConditionalWeakTable<DependencyObject, Originals> Sources = new();
    private static void Property(DependencyObject node, DependencyProperty property)
    {
        if (node.GetValue(property) is not string text) return;
        var values = Sources.GetOrCreateValue(node).Values;
        string source = values.TryGetValue(property, out var old) && old.Display == text ? old.Source : text;
        string display = Translate(source);
        values[property] = (source, display);
        if (text != display) node.SetCurrentValue(property, display);
    }
    internal static void Apply(DependencyObject node)
    {
        if (node is TextBlock) Property(node, TextBlock.TextProperty);
        if (node is ContentControl) Property(node, ContentControl.ContentProperty);
        if (node is HeaderedItemsControl) Property(node, HeaderedItemsControl.HeaderProperty);
        if (node is FrameworkElement) Property(node, FrameworkElement.ToolTipProperty);
        if (node is Window) Property(node, Window.TitleProperty);
        Property(node, System.Windows.Automation.AutomationProperties.NameProperty);
        if (node is ItemsControl items) foreach (object item in items.Items) if (item is DependencyObject child) Apply(child);
        if (node is Visual) for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Apply(VisualTreeHelper.GetChild(node, i));
    }
}
