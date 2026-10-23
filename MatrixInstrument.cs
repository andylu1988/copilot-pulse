using System;
using System.Windows;
using System.Windows.Media;

namespace CopilotPulse;

public sealed partial class MoneyReadout
{
    private void DrawMatrixInstrument(DrawingContext dc, SkinPalette p, double dpi)
    {
        void Label(string text, double size, string color, double x, double y) => dc.DrawText(DisplayArt.Text(text, size, color, dpi, "Cascadia Mono, Consolas"), new Point(x, y));
        // The background rails are generated art; all labels, amounts and states remain live UI.
        dc.DrawRoundedRectangle(DisplayArt.Brush(p.Bottom, .36), null, new Rect(10, 23, 208, 51), 7, 7);
        Label(string.IsNullOrEmpty(MetricCaption) ? "COPILOT" : MetricCaption, 7.2, p.Muted, 16, 13);
        Label("USD", 6.4, p.Faint, 195, 13);
        dc.DrawLine(new Pen(DisplayArt.Brush(p.Accent, .28), .6), new Point(16, 26), new Point(212, 26));
        DisplayArt.Money(dc, new Rect(17, 31, 194, 38), amount, p, Phase, Pulse, dpi);
        bool example = Preview || state == "demo";
        string footer = example ? "示例预览" : StateLabel();
        string color = state is "live" or "syncing" ? p.Accent : example ? p.Faint : "#D8B67F";
        dc.DrawEllipse(DisplayArt.Brush(color), null, new Point(18, 79), 1.7, 1.7);
        dc.DrawText(DisplayArt.Text(footer, 6.6, p.Muted, dpi), new Point(24, 74));
        string stamp = !example && updated.HasValue ? updated.Value.LocalDateTime.ToString("HH:mm:ss", DisplayArt.Us) : "MATRIX / 01";
        var time = DisplayArt.Text(stamp, 6.4, p.Faint, dpi, "Cascadia Mono, Consolas");
        dc.DrawText(time, new Point(212 - time.Width, 74));
    }
}
