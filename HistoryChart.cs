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
    private int hovered = -1;
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
            if (!plot.Contains(position)) { hovered = -1; InvalidateVisual(); return; }
            double stamp = minTime + (position.X - plot.Left) / plot.Width * (maxTime - minTime);
            int best = 0;
            double distance = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                double d = Math.Abs(points[i].Time.ToUnixTimeMilliseconds() - stamp);
                if (d < distance) { best = i; distance = d; }
            }
            if (hovered != best) { hovered = best; InvalidateVisual(); }
        };
        MouseLeave += (_, _) => { hovered = -1; InvalidateVisual(); };
    }

    public void SetData(IReadOnlyList<HistoryPoint> values, bool changes = false, DateTimeOffset? from = null, DateTimeOffset? until = null)
    {
        points = values;
        Changes = changes;
        rangeStart = from;
        rangeEnd = until;
        hovered = -1;
        InvalidateVisual();
    }

    internal void InspectPointForReview(int index) { hovered = points.Count == 0 ? -1 : Math.Clamp(index, 0, points.Count - 1); InvalidateVisual(); }

    private FormattedText Label(string text, double size, string color) => new(text, Us, FlowDirection.LeftToRight, new Typeface("Segoe UI, Microsoft YaHei UI"), size, Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
    private decimal? Value(HistoryPoint point) => Changes ? point.Delta : point.Value;
    private Point Position(int index, double value) => new(plot.Left + (points[index].Time.ToUnixTimeMilliseconds() - minTime) / (maxTime - minTime) * plot.Width, plot.Bottom - (value - minValue) / (maxValue - minValue) * plot.Height);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brush("#101D30"), null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (ActualWidth < 20 || ActualHeight < 20) return;
        plot = Mini ? new Rect(3, 5, Math.Max(1, ActualWidth - 6), Math.Max(1, ActualHeight - 10)) : new Rect(78, 27, Math.Max(1, ActualWidth - 105), Math.Max(1, ActualHeight - 70));
        var valid = points.Select((p, i) => (p, i, value: Value(p))).Where(x => x.value.HasValue).ToList();
        if (valid.Count == 0)
        {
            if (!Mini)
            {
                string message = points.Count > 0 ? "下一次连续刷新后，将显示单次变化" : "正在积累真实记录";
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
        if (!Changes) minValue = Math.Max(0, minValue);
        maxValue += padding;
        minTime = (rangeStart ?? points[0].Time).ToUnixTimeMilliseconds();
        maxTime = (rangeEnd ?? points[^1].Time).ToUnixTimeMilliseconds();
        if (maxTime <= minTime)
        {
            if (rangeStart.HasValue) maxTime = minTime + 1;
            else { minTime -= 30000; maxTime += 30000; }
        }
        if (!Mini)
        {
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
        dc.Pop();
        if (!Mini && hovered >= 0 && hovered < points.Count)
        {
            var point = points[hovered];
            double x = Position(hovered, 0).X;
            dc.DrawLine(new Pen(Brush("#708FAD"), 1) { DashStyle = DashStyles.Dash }, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var amount = Value(point);
            if (amount.HasValue) dc.DrawEllipse(Brush(Accent), new Pen(Brush("#E5F8FF"), 1.5), Position(hovered, (double)amount.Value), 4, 4);
            string delta = point.Delta.HasValue ? $"较上次 {Signed(point.Delta.Value)} · 间隔 {point.IntervalSeconds / 60:0.#} 分钟" : point.Reason;
            var first = Label(point.Time.ToLocalTime().ToString("MM/dd HH:mm:ss", Us), 11, "#A9BDD6");
            var second = Label(amount.HasValue ? (Changes ? Signed(amount.Value) : amount.Value.ToString("C2", Us)) : "—", 20, "#EFF8FF");
            var third = Label(delta, 10, "#91AAC9");
            double width = Math.Max(210, third.Width + 26), left = Math.Clamp(x + 14, 5, Math.Max(5, ActualWidth - width - 6));
            var box = new Rect(left, 5, width, 90);
            dc.DrawRoundedRectangle(Brush("#21334C"), new Pen(Brush("#4B6687"), 1), box, 9, 9);
            dc.DrawText(first, new Point(left + 12, 13)); dc.DrawText(second, new Point(left + 12, 33)); dc.DrawText(third, new Point(left + 12, 66));
        }
    }

    public static string Signed(decimal value) => (value > 0 ? "+" : value < 0 ? "−" : "") + Math.Abs(value).ToString("C2", Us);
}
