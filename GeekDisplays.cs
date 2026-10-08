using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace CopilotPulse;

internal static partial class DisplayArt
{
    internal static bool Geek(string id) => id is "shell" or "amber" or "scope" or "debug";

    private static void GeekTexture(DrawingContext dc, Rect r, SkinPalette p, double time, bool small)
    {
        if (!Geek(p.Id)) return;
        if (p.Id == "shell")
        {
            // A breathing cursor, not a simulated command or data update.
            double cursor = .28 + .6 * (.5 + .5 * Math.Cos(time * 2.1));
            double x = r.Right - 19, y = small ? r.Bottom - 9 : r.Top + 8;
            dc.DrawRectangle(Brush(p.Accent, cursor), null, new Rect(x, y, 7, 1.5));
            if (!small) dc.DrawLine(Line(p.Edge, .6), new Point(r.Left + 16, r.Top + 10), new Point(r.Right - 32, r.Top + 10));
        }
        else if (p.Id == "amber")
        {
            for (double y = r.Top + 2; y < r.Bottom; y += 3)
                dc.DrawLine(Line(p.Accent, .045, .6), new Point(r.Left + 3, y), new Point(r.Right - 3, y));
            double scan = r.Top + Cycle(time * .11) * r.Height;
            var beam = new LinearGradientBrush(new GradientStopCollection { new(Colors.Transparent, 0), new(Brush(p.Accent, .14).Color, .5), new(Colors.Transparent, 1) }, new Point(0, 0), new Point(0, 1));
            dc.DrawRectangle(beam, null, new Rect(r.Left + 3, scan - 8, r.Width - 6, 16));
            // The scale is an instrument bezel, not a usage indicator.
            for (int i = 0; i <= 20; i++)
            {
                double x = r.Left + 13 + i * (r.Width - 26) / 20;
                dc.DrawLine(Line(p.Accent, i % 5 == 0 ? .65 : .27, .7), new Point(x, r.Top + 21), new Point(x, r.Top + (i % 5 == 0 ? 17 : 19)));
            }
        }
        else if (p.Id == "scope")
        {
            double step = small ? 12 : 26;
            for (double x = r.Left + 7; x < r.Right; x += step)
                for (double y = r.Top + 7; y < r.Bottom; y += step)
                    dc.DrawEllipse(Brush(p.Accent, .16), null, new Point(x, y), .45, .45);
            var center = new Point(r.Left + r.Width * .79, r.Top + r.Height * (small ? .51 : .22));
            double radius = small ? 35 : r.Width * .24;
            dc.DrawLine(Line(p.Accent, .14), new Point(center.X - radius - 5, center.Y), new Point(center.X + radius + 5, center.Y));
            dc.DrawLine(Line(p.Accent, .14), new Point(center.X, center.Y - radius - 5), new Point(center.X, center.Y + radius + 5));
            // Lissajous trace is decorative phosphor art, explicitly labelled VECTOR FX.
            Point Trace(double t) => new(center.X + radius * Math.Sin(t * 2 + time * .18), center.Y + radius * .88 * Math.Sin(t * 3));
            var curve = new StreamGeometry();
            using (var path = curve.Open())
            {
                path.BeginFigure(Trace(0), false, false);
                for (int i = 1; i <= 128; i++) path.LineTo(Trace(i * Math.PI / 64), true, false);
            }
            curve.Freeze();
            dc.DrawGeometry(null, Line(p.Accent, .05, 4), curve);
            dc.DrawGeometry(null, Line(p.Accent, .42, .65), curve);
            for (int i = 0; i < 18; i++)
            {
                double head = time * .82 - i * .025;
                dc.DrawLine(Line(p.Accent, .8 * (1 - i / 18.0), 1.1), Trace(head), Trace(head + .025));
            }
            Halo(dc, Trace(time * .82), 3, 3, p.Accent, .45);
        }
        else if (p.Id == "debug")
        {
            double gutter = small ? 23 : 14;
            dc.DrawRectangle(Brush(p.Panel, .8), null, new Rect(r.Left, r.Top, gutter, r.Height));
            dc.DrawLine(Line(p.Edge, .75), new Point(r.Left + gutter, r.Top), new Point(r.Left + gutter, r.Bottom));
            if (small)
            {
                dc.DrawRectangle(Brush(p.Hover, .5), null, new Rect(r.Left + gutter, r.Top + 28, r.Width - gutter, 40));
                double alpha = .35 + .55 * (.5 + .5 * Math.Sin(time * 2));
                dc.DrawLine(Line(p.Accent, alpha, 1.1), new Point(r.Right - 9, r.Top + 36), new Point(r.Right - 9, r.Top + 60));
            }
            else
            {
                for (double y = r.Top + 19; y < r.Bottom; y += 25)
                    dc.DrawLine(Line(p.Faint, .2), new Point(r.Left + 5, y), new Point(r.Left + 9, y));
                double cursorY = r.Top + 15 + Cycle(time * .08) * (r.Height - 30);
                dc.DrawRectangle(Brush(p.Accent, .5), null, new Rect(r.Left + 3, cursorY, 2, 8));
            }
        }
    }

    private static void GeekMoney(DrawingContext dc, Rect box, string value, SkinPalette p, double dpi)
    {
        // Fall back to type for signs/parentheses: the segment alphabet cannot represent them.
        if (p.Id == "amber" && value.All(c => char.IsAsciiDigit(c) || c is '$' or '.' or ',')) { SevenSegment(dc, box, value, p); return; }
        double size = Math.Min(37, box.Height * .92);
        string font = "Cascadia Mono, Consolas";
        var label = Text(value, size, p.Text, dpi, font, p.Id == "debug");
        if (label.Width > box.Width) { size *= box.Width / label.Width; label = Text(value, size, p.Text, dpi, font, p.Id == "debug"); }
        var at = new Point(box.Left + (box.Width - label.Width) / 2, box.Top + (box.Height - label.Height) / 2);
        dc.DrawText(label, at);
    }
}

public sealed partial class MoneyReadout
{
    internal string GeekStateText => Preview ? "PREVIEW / EXAMPLE" : state switch
    {
        "demo" => "DEMO / EXAMPLE",
        "live" => "SYNCED",
        "syncing" => "SYNCING",
        "paused" => "PAUSED",
        "login" => "LOGIN REQUIRED",
        "network" or "layout" or "cached" or "stale" or "runtime" => "CACHED / STALE",
        _ => "WAITING"
    };

    private void DrawGeekTile(DrawingContext dc, SkinPalette p, double dpi)
    {
        void Label(string value, double size, string color, double x, double y) => dc.DrawText(DisplayArt.Text(value, size, color, dpi, "Cascadia Mono, Consolas"), new Point(x, y));
        bool debugger = p.Id == "debug";
        double left = debugger ? 30 : 13;
        string title = p.Id switch { "shell" => "> copilot usage --month", "amber" => "COPILOT / MONTH / USD", "scope" => "COPILOT / VECTOR FX", _ => "watch: usage.monthly" };
        Label(title, 7, p.Muted, left, 8);
        if (p.Id == "shell") dc.DrawLine(new Pen(DisplayArt.Brush(p.Edge), .7), new Point(13, 23), new Point(215, 23));
        if (p.Id == "scope") Label("USD", 6.5, p.Accent, 195, 19);
        if (debugger)
        {
            Label("01", 6, p.Faint, 6, 10); Label("02", 6, p.Accent, 6, 44); Label("03", 6, p.Faint, 6, 81);
        }
        if (p.Id == "amber")
            dc.DrawRoundedRectangle(DisplayArt.Brush(p.Bottom, .55), new Pen(DisplayArt.Brush(p.Edge, .6), .7), new Rect(9, 27, 210, 46), 1, 1);
        var box = debugger ? new Rect(30, 29, 179, 42) : p.Id == "amber" ? new Rect(15, 32, 198, 35) : new Rect(13, 29, 202, 42);
        DisplayArt.Money(dc, box, amount, p, Phase, Pulse, dpi);
        dc.DrawLine(new Pen(DisplayArt.Brush(p.Edge, .8), .7), new Point(left, 76), new Point(215, 76));
        bool example = Preview || state == "demo";
        string color = state is "live" or "syncing" || example ? p.Faint : "#E4B071";
        Label(GeekStateText, 6.4, color, left, 81);
        if (updated.HasValue && !example)
        {
            string stamp = updated.Value.LocalDateTime.ToString("HH:mm:ss", DisplayArt.Us);
            var time = DisplayArt.Text(stamp, 6.4, p.Faint, dpi, "Cascadia Mono, Consolas");
            dc.DrawText(time, new Point((p.Id == "shell" ? 198 : 215) - time.Width, 81));
        }
        // An actual sync-state dot, distinct from the decorative cursor and phosphor trace.
        dc.DrawRectangle(DisplayArt.Brush(state == "live" ? p.Accent : state == "syncing" ? p.Secondary : "#C9A778"), null, new Rect(210, 10, 3, 3));
    }
}
