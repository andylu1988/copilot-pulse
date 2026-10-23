using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CopilotPulse;

/// <summary>Native, interactive chart. No fabricated values, interpolation over gaps, or remote assets.</summary>
public sealed class HistoryChart : FrameworkElement
{
    private IReadOnlyList<HistoryPoint> points = Array.Empty<HistoryPoint>();
    private IReadOnlyList<TaskMarker> markers = Array.Empty<TaskMarker>();
    public void SetMarkers(IReadOnlyList<TaskMarker> values) { markers = values; InvalidateVisual(); }
    private int hovered = -1, pinned = -1;
    private double? zoomStart, zoomEnd, dragStart;
    private double dragEnd;
    public bool IsZoomed => zoomStart.HasValue;
    public bool Mini { get; set; }
    public bool Changes { get; set; }
    public string Accent { get; set; } = "#80C9F0";
    private Rect plot;
    private double minValue, maxValue, minTime, maxTime;
    private DateTimeOffset? rangeStart, rangeEnd;
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static SolidColorBrush Brush(string hex) => SkinTheme.Resolve(hex);

    public HistoryChart()
    {
        SnapsToDevicePixels = true;
        MouseMove += (_, e) =>
        {
            if (Mini || points.Count == 0) return;
            var position = e.GetPosition(this);
            if (dragStart.HasValue) { dragEnd = Math.Clamp(position.X, plot.Left, plot.Right); InvalidateVisual(); return; }
            if (pinned >= 0) return;
            if (!plot.Contains(position)) { hovered = -1; InvalidateVisual(); return; }
            int best = Nearest(position.X);
            if (hovered != best) { hovered = best; InvalidateVisual(); }
        };
        MouseLeave += (_, _) => { hovered = pinned; InvalidateVisual(); };
        MouseWheel += (_, e) => {
            if (Mini || points.Count < 2 || !plot.Contains(e.GetPosition(this))) return;
            double ratio = Math.Clamp((e.GetPosition(this).X - plot.Left) / plot.Width, 0, 1);
            double anchor = minTime + ratio * (maxTime - minTime), span = (maxTime - minTime) * (e.Delta > 0 ? .65 : 1.5);
            ZoomTo(anchor - span * ratio, anchor + span * (1 - ratio)); e.Handled = true;
        };
        MouseLeftButtonDown += (_, e) => {
            if (Mini || points.Count == 0 || !plot.Contains(e.GetPosition(this))) return;
            if (e.ClickCount == 2) { ResetZoom(); e.Handled = true; return; }
            dragStart = dragEnd = e.GetPosition(this).X; CaptureMouse(); e.Handled = true;
        };
        MouseLeftButtonUp += (_, e) => {
            if (!dragStart.HasValue) return;
            double start = dragStart.Value, end = Math.Clamp(e.GetPosition(this).X, plot.Left, plot.Right); dragStart = null; ReleaseMouseCapture();
            if (Math.Abs(start - end) > 8) ZoomTo(minTime + (Math.Min(start, end) - plot.Left) / plot.Width * (maxTime - minTime), minTime + (Math.Max(start, end) - plot.Left) / plot.Width * (maxTime - minTime));
            else { int nearest = Nearest(end); pinned = pinned == nearest ? -1 : nearest; hovered = pinned; ToolTip = pinned < 0 ? null : UiText.T("采样已固定；再次点击解除，双击或右键恢复全图。", "Sample pinned; click again to release, double-click or right-click to reset."); }
            e.Handled = true; InvalidateVisual();
        };
        LostMouseCapture += (_, _) => { dragStart = null; InvalidateVisual(); };
        MouseRightButtonUp += (_, e) => { if (!Mini) { ResetZoom(); e.Handled = true; } };
    }

    public void SetData(IReadOnlyList<HistoryPoint> values, bool changes = false, DateTimeOffset? from = null, DateTimeOffset? until = null)
    {
        if (Changes != changes || rangeStart != from) { zoomStart = zoomEnd = null; }
        points = values;
        Changes = changes;
        rangeStart = from;
        rangeEnd = until;
        hovered = pinned = -1;
        if (zoomStart.HasValue && points.Count > 1) ZoomTo(zoomStart.Value, zoomEnd!.Value);
        InvalidateVisual();
    }

    public void ResetZoom() { zoomStart = zoomEnd = null; hovered = pinned = -1; ToolTip = null; InvalidateVisual(); }
    private void ZoomTo(double start, double end)
    {
        if (points.Count < 2) return;
        double first = (rangeStart ?? points[0].Time).ToUnixTimeMilliseconds(), last = (rangeEnd ?? points[^1].Time).ToUnixTimeMilliseconds();
        if (last <= first) return;
        double span = last - first, requested = Math.Clamp(end - start, Math.Min(1000, span), span);
        if (requested >= span * .999) { ResetZoom(); return; }
        zoomStart = Math.Clamp(start, first, last - requested); zoomEnd = zoomStart + requested;
        hovered = pinned = -1; InvalidateVisual();
    }
    private int Nearest(double x)
    {
        double stamp = minTime + (x - plot.Left) / plot.Width * (maxTime - minTime);
        return Enumerable.Range(0, points.Count).Where(i => points[i].Time.ToUnixTimeMilliseconds() >= minTime && points[i].Time.ToUnixTimeMilliseconds() <= maxTime).OrderBy(i => Math.Abs(points[i].Time.ToUnixTimeMilliseconds() - stamp)).DefaultIfEmpty(-1).First();
    }
    internal void ZoomForReview(DateTimeOffset from, DateTimeOffset until) => ZoomTo(from.ToUnixTimeMilliseconds(), until.ToUnixTimeMilliseconds());
    internal void InspectPointForReview(int index) { hovered = points.Count == 0 ? -1 : Math.Clamp(index, 0, points.Count - 1); InvalidateVisual(); }

    private FormattedText Label(string text, double size, string color) => new(SkinTheme.Settings.HideAmounts && (text.Contains("$") || decimal.TryParse(text, System.Globalization.NumberStyles.Any, Us, out _)) ? "••••" : text, Us, FlowDirection.LeftToRight, new Typeface("Segoe UI, Microsoft YaHei UI"), size, Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
    private decimal? Value(HistoryPoint point) => Changes ? point.Delta : point.Value;
    private Point Position(int index, double value) => new(plot.Left + (points[index].Time.ToUnixTimeMilliseconds() - minTime) / (maxTime - minTime) * plot.Width, plot.Bottom - (value - minValue) / (maxValue - minValue) * plot.Height);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brush("#101D30"), null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (ActualWidth < 20 || ActualHeight < 20) return;
        plot = Mini ? new Rect(3, 5, Math.Max(1, ActualWidth - 6), Math.Max(1, ActualHeight - 10)) : new Rect(78, 27, Math.Max(1, ActualWidth - 105), Math.Max(1, ActualHeight - 70));
        minTime = zoomStart ?? (rangeStart ?? points.FirstOrDefault()?.Time ?? DateTimeOffset.Now).ToUnixTimeMilliseconds();
        maxTime = zoomEnd ?? (rangeEnd ?? points.LastOrDefault()?.Time ?? DateTimeOffset.Now).ToUnixTimeMilliseconds();
        if (maxTime <= minTime) { minTime -= 30000; maxTime += 30000; }
        var valid = points.Select((p, i) => (p, i, value: Value(p))).Where(x => x.value.HasValue && (!zoomStart.HasValue || x.p.Time.ToUnixTimeMilliseconds() >= zoomStart.Value && x.p.Time.ToUnixTimeMilliseconds() <= zoomEnd!.Value)).ToList();
        if (valid.Count == 0)
        {
            if (!Mini)
            {
                string message = IsZoomed ? UiText.T("此范围没有有效采样；双击或右键恢复全图", "No valid samples in this range; double-click or right-click to reset") : UiText.Translate(points.Count > 0 ? "下一次连续刷新后，将显示单次变化" : "正在积累真实记录");
                var label = Label(message, 14, "#879FBE");
                dc.DrawText(label, new Point(Math.Max(8, (ActualWidth - label.Width) / 2), ActualHeight / 2 - 12));
            }
            else dc.DrawLine(new Pen(Brush("#35465D"), 1), new Point(4, ActualHeight / 2), new Point(ActualWidth - 4, ActualHeight / 2));
            return;
        }
        minValue = valid.Min(x => (double)x.value!.Value);
        maxValue = valid.Max(x => (double)x.value!.Value);
        if (Changes) { minValue = Math.Min(0, minValue); maxValue = Math.Max(0, maxValue); }
        double padding = Math.Max(0.02, (maxValue - minValue) * 0.16);
        minValue -= padding;
        if (!Changes && valid.All(x => x.value >= 0)) minValue = Math.Max(0, minValue);
        maxValue += padding;
        minTime = zoomStart ?? (rangeStart ?? points[0].Time).ToUnixTimeMilliseconds();
        maxTime = zoomEnd ?? (rangeEnd ?? points[^1].Time).ToUnixTimeMilliseconds();
        if (maxTime <= minTime)
        {
            if (rangeStart.HasValue) maxTime = minTime + 1;
            else { minTime -= 30000; maxTime += 30000; }
        }
        if (!Mini)
        {
            foreach (var marker in markers.Where(m => m.TimeUtc.ToUnixTimeMilliseconds() >= minTime && m.TimeUtc.ToUnixTimeMilliseconds() <= maxTime))
            {
                double x = plot.Left + (marker.TimeUtc.ToUnixTimeMilliseconds() - minTime) / (maxTime - minTime) * plot.Width;
                dc.DrawLine(new Pen(Brush(Accent), 1) { DashStyle = DashStyles.Dash }, new Point(x, plot.Top), new Point(x, plot.Bottom));
                var note = Label(marker.Name.Length > 16 ? marker.Name[..16] + "…" : marker.Name, 10, Accent);
                dc.DrawText(note, new Point(Math.Clamp(x, plot.Left, Math.Max(plot.Left, plot.Right - note.Width)), 8));
            }
            for (int i = 0; i <= 4; i++)
            {
                double y = plot.Top + plot.Height * i / 4;
                dc.DrawLine(new Pen(Brush("#24374F"), 1), new Point(plot.Left, y), new Point(plot.Right, y));
                var label = Label((maxValue - (maxValue - minValue) * i / 4).ToString("N2", Us), 10, "#7E94B2");
                dc.DrawText(label, new Point(plot.Left - label.Width - 12, y - label.Height / 2));
            }
            dc.DrawText(Label("USD", 9, "#7085A3"), new Point(23, 4));
            for (int i = 0; i <= 4; i++)
            {
                var time = DateTimeOffset.FromUnixTimeMilliseconds((long)(minTime + (maxTime - minTime) * i / 4)).ToLocalTime();
                string format = maxTime - minTime > TimeSpan.FromDays(1).TotalMilliseconds ? "MM/dd HH:mm" : maxTime - minTime < 600000 ? "HH:mm:ss" : "HH:mm";
                var label = Label(time.ToString(format, Us), 10, "#7E94B2");
                dc.DrawText(label, new Point(plot.Left + plot.Width * i / 4 - label.Width / 2, plot.Bottom + 14));
            }
        }
        if (!Mini) DrawBoundaries(dc);
        var pen = new Pen(Brush(Accent), Mini ? 1.6 : 2) { LineJoin = PenLineJoin.Round };
        dc.PushClip(new RectangleGeometry(new Rect(plot.Left - 4, plot.Top - 4, plot.Width + 8, plot.Height + 8)));
        if (Changes)
        {
            double zero = Position(0, 0).Y;
            dc.DrawLine(new Pen(Brush("#58728E"), 1), new Point(plot.Left, zero), new Point(plot.Right, zero));
            // Render per-pixel min/max changes so a dense month does not hide spikes.
            foreach (var group in valid.GroupBy(v => (int)Position(v.i, (double)v.value!.Value).X))
            {
                foreach (var item in new[] { group.MinBy(v => v.value)!, group.MaxBy(v => v.value)! }.Distinct())
                {
                    var at = Position(item.i, (double)item.value!.Value);
                    var color = Brush(item.value < 0 ? "#F1B58B" : Accent);
                    dc.DrawLine(new Pen(color, Math.Clamp(plot.Width / Math.Max(1, valid.Count) * 0.6, 1, 9)), new Point(at.X, zero), at);
                    if (item.value == 0) dc.DrawEllipse(color, null, at, 1.8, 1.8);
                }
            }
        }
        else
        {
            var segment = new List<int>();
            void DrawSegment()
            {
                if (segment.Count == 0) return;
                // Keep first, extrema and last in each horizontal pixel, in chronological order.
                var reduced = segment.GroupBy(i => (int)Position(i, (double)points[i].Value).X).SelectMany(g => new[] { g.First(), g.MinBy(i => points[i].Value), g.MaxBy(i => points[i].Value), g.Last() }.Distinct().OrderBy(i => i)).ToList();
                if (reduced.Count > 1)
                {
                    var geometry = new StreamGeometry();
                    using (var context = geometry.Open())
                    {
                        context.BeginFigure(Position(reduced[0], (double)points[reduced[0]].Value), false, false);
                        foreach (int index in reduced.Skip(1)) context.LineTo(Position(index, (double)points[index].Value), true, false);
                    }
                    geometry.Freeze();
                    dc.DrawGeometry(null, pen, geometry);
                }
                if (segment.Count <= 80) foreach (int index in reduced) dc.DrawEllipse(Brush(Accent), null, Position(index, (double)points[index].Value), Mini ? 1.5 : 2.6, Mini ? 1.5 : 2.6);
                segment.Clear();
            }
            for (int i = 0; i < points.Count; i++) { if (points[i].BreakBefore) DrawSegment(); segment.Add(i); }
            DrawSegment();
        }
        if (!Mini)
        {
            var positives = valid.Where(x => x.p.Delta > 0).Select(x => x.p.Delta!.Value).OrderBy(x => x).ToArray();
            decimal median = positives.Length == 0 ? 0 : positives[positives.Length / 2];
            foreach (var v in valid.Where(x => x.p.Delta < 0 || median > 0 && x.p.Delta > Math.Max(1m, median * 5)).Take(200))
                dc.DrawEllipse(null, new Pen(Brush("#FFBC70"), 2), Position(v.i, (double)v.value!.Value), 5, 5);
            if (dragStart.HasValue) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(60, 100, 210, 255)), new Pen(Brush(Accent), 1), new Rect(Math.Min(dragStart.Value, dragEnd), plot.Top, Math.Abs(dragEnd - dragStart.Value), plot.Height));
        }
        dc.Pop();
        if (!Mini && IsZoomed) dc.DrawText(Label(UiText.T("已缩放 · 双击/右键恢复", "Zoomed · Double/right-click resets"), 10, Accent), new Point(plot.Left + 5, 5));
        if (!Mini && hovered >= 0 && hovered < points.Count)
        {
            var point = points[hovered];
            double x = Position(hovered, 0).X;
            dc.DrawLine(new Pen(Brush("#708FAD"), 1) { DashStyle = DashStyles.Dash }, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var amount = Value(point);
            if (amount.HasValue) dc.DrawEllipse(Brush(Accent), new Pen(Brush("#E5F8FF"), 1.5), Position(hovered, (double)amount.Value), 4, 4);
            string delta = point.Delta.HasValue ? UiText.T($"较上次 {Signed(point.Delta.Value)} · 间隔 {point.IntervalSeconds / 60:0.#} 分钟", $"Change {Signed(point.Delta.Value)} · {point.IntervalSeconds / 60:0.#} min") : point.Reason;
            if (point.Delta < 0) delta += UiText.T(" · 金额修正/下降", " · Correction/drop");
            var first = Label(point.Time.ToLocalTime().ToString("MM/dd HH:mm:ss", Us), 11, "#A9BDD6");
            var second = Label(amount.HasValue ? (Changes ? Signed(amount.Value) : amount.Value.ToString("C2", Us)) : "—", 20, "#EFF8FF");
            var third = Label(UiText.Translate(delta), 10, "#91AAC9");
            double width = Math.Max(210, third.Width + 26), left = Math.Clamp(x + 14, 5, Math.Max(5, ActualWidth - width - 6));
            var box = new Rect(left, 5, width, 90);
            dc.DrawRoundedRectangle(Brush("#21334C"), new Pen(Brush("#4B6687"), 1), box, 9, 9);
            dc.DrawText(first, new Point(left + 12, 13)); dc.DrawText(second, new Point(left + 12, 33)); dc.DrawText(third, new Point(left + 12, 66));
        }
    }

    private void DrawBoundaries(DrawingContext dc)
    {
        double lastDayLabelX = double.NegativeInfinity;
        void Mark(DateTimeOffset time, string label, string color, double labelY, bool spaceLabels = false)
        {
            double stamp = time.ToUnixTimeMilliseconds(); if (stamp < minTime || stamp > maxTime) return;
            double x = plot.Left + (stamp - minTime) / (maxTime - minTime) * plot.Width;
            dc.DrawLine(new Pen(Brush(color), 1) { DashStyle = DashStyles.Dash }, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var text = Label(label, 9, color);
            double labelX = Math.Clamp(x + 3, plot.Left, Math.Max(plot.Left, plot.Right - text.Width));
            if (!spaceLabels || labelX >= lastDayLabelX + text.Width + 12)
            {
                dc.DrawText(text, new Point(labelX, labelY));
                if (spaceLabels) lastDayLabelX = labelX;
            }
        }
        var first = DateTimeOffset.FromUnixTimeMilliseconds((long)minTime);
        var last = DateTimeOffset.FromUnixTimeMilliseconds((long)maxTime);
        var day = first.ToLocalTime().Date.AddDays(1); int days = 0;
        while (days++ < 32 && day <= last.ToLocalTime().Date)
        {
            if (!TimeZoneInfo.Local.IsInvalidTime(day)) Mark(new DateTimeOffset(day, TimeZoneInfo.Local.GetUtcOffset(day)), UiText.T("本机午夜", "Local midnight"), "#52778C", plot.Top + 3, true);
            day = day.AddDays(1);
        }
        var month = HistoryRanges.Start("month", first).AddMonths(1); int months = 0;
        while (months++ < 60 && month <= last) { Mark(month, UiText.T("UTC 配额重置", "UTC quota reset"), "#E5AE6B", plot.Top + 15); month = month.AddMonths(1); }
    }
    public static string Signed(decimal value) => (value > 0 ? "+" : value < 0 ? "−" : "") + Math.Abs(value).ToString("C2", Us);
}
