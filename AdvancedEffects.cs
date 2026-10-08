using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace CopilotPulse;

internal static partial class DisplayArt
{
    // Deterministic decorative motion: no randomness or transformations of financial values.
    private static double Cycle(double value) => value - Math.Floor(value);
    private static Point Polar(Point center, double radius, double angle) => new(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
    private static Pen Line(string color, double opacity, double width = 1) => new(Brush(color, opacity), width);
    private static void Arc(DrawingContext dc, Point center, double radius, double start, double length, Pen pen)
    {
        var shape = new StreamGeometry();
        using (var g = shape.Open())
        {
            g.BeginFigure(Polar(center, radius, start), false, false);
            g.ArcTo(Polar(center, radius, start + length), new Size(radius, radius), 0, length > Math.PI, SweepDirection.Clockwise, true, false);
        }
        shape.Freeze(); dc.DrawGeometry(null, pen, shape);
    }
    private static void Halo(DrawingContext dc, Point center, double rx, double ry, string color, double alpha)
    {
        var glow = new RadialGradientBrush(Brush(color, alpha).Color, Colors.Transparent);
        dc.DrawEllipse(glow, null, center, rx, ry);
    }
    private static void GlowLine(DrawingContext dc, Point a, Point b, string color, double alpha, double width = 1)
    {
        dc.DrawLine(Line(color, alpha * .12, width + 5), a, b);
        dc.DrawLine(Line(color, alpha * .25, width + 2), a, b);
        dc.DrawLine(Line(color, alpha, width), a, b);
    }

    private static void AdvancedTexture(DrawingContext dc, Rect r, SkinPalette p, double time, bool small)
    {
        switch (p.Id)
        {
            case "radar": Radar(dc, r, p, time, small); break;
            case "mecha": Mecha(dc, r, p, time, small); break;
            case "synthwave": Synthwave(dc, r, p, time, small); break;
            case "warp": Warp(dc, r, p, time, small); break;
            case "plasma": Plasma(dc, r, p, time, small); break;
            case "matrix": Matrix(dc, r, p, time, small); break;
            case "cyber": Circuit(dc, r, p, time); break;
        }
    }

    private static void Radar(DrawingContext dc, Rect r, SkinPalette p, double time, bool small)
    {
        var c = new Point(r.Left + r.Width * .78, r.Top + r.Height * (small ? .52 : .22));
        double radius = small ? 53 : r.Width * .6, angle = time * .62;
        Halo(dc, c, radius * 1.2, radius * 1.2, p.Accent, .18);
        for (int ring = 1; ring <= 4; ring++) dc.DrawEllipse(null, Line(p.Accent, ring == 4 ? .38 : .19), c, radius * ring / 4, radius * ring / 4);
        for (int i = 0; i < 36; i++)
        {
            double a = i * Math.PI / 18;
            dc.DrawLine(Line(p.Accent, i % 3 == 0 ? .5 : .2), Polar(c, radius - (i % 3 == 0 ? 5 : 2), a), Polar(c, radius, a));
        }
        dc.DrawLine(Line(p.Accent, .16), new Point(c.X - radius, c.Y), new Point(c.X + radius, c.Y));
        dc.DrawLine(Line(p.Accent, .16), new Point(c.X, c.Y - radius), new Point(c.X, c.Y + radius));
        for (int i = 0; i < 24; i++)
        {
            double a = angle - i * .035;
            dc.DrawLine(Line(p.Accent, .3 * (1 - i / 24.0), small ? 2 : 4), c, Polar(c, radius, a));
        }
        GlowLine(dc, c, Polar(c, radius, angle), p.Accent, .85);
        for (int i = 0; i < 7; i++)
        {
            double a = i * 2.399 + .45, age = Cycle((angle - a) / (2 * Math.PI));
            var at = Polar(c, radius * (.23 + i * .097), a);
            double alpha = .1 + .9 * Math.Exp(-age * 8);
            Halo(dc, at, 7, 7, p.Accent, alpha * .5);
            dc.DrawEllipse(Brush(p.Accent, alpha), null, at, 1.7, 1.7);
        }
        for (int i = 0; i < 4; i++) dc.DrawLine(Line(p.Secondary, .55, 2), new Point(r.Left + 15 + i * 10, r.Bottom - 5), new Point(r.Left + 21 + i * 10, r.Bottom - 5));
    }

    private static void Mecha(DrawingContext dc, Rect r, SkinPalette p, double time, bool small)
    {
        var c = new Point(r.Left + r.Width * .8, r.Top + r.Height * (small ? .48 : .24));
        double radius = small ? 39 : 112;
        Halo(dc, c, radius * 1.8, radius * 1.8, p.Accent, .13);
        for (int ring = 0; ring < 3; ring++)
        {
            double rad = radius + ring * 7, rotation = time * (ring % 2 == 0 ? .38 : -.28);
            for (int part = 0; part < 3; part++) Arc(dc, c, rad, rotation + part * Math.PI * 2 / 3, 1.4, Line(ring == 1 ? p.Secondary : p.Accent, .5, ring == 1 ? 2 : .8));
        }
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            dc.DrawLine(Line(p.Accent, .4), Polar(c, radius - 4, a), Polar(c, radius, a));
        }
        for (int i = 0; i < 22; i++)
        {
            double alpha = .12 + .8 * Math.Pow(.5 + .5 * Math.Sin(i * .4 - time * 1.7), 4);
            double x = r.Left + 19 + i * (r.Width - 38) / 22;
            dc.DrawRectangle(Brush(i > 16 ? p.Secondary : p.Accent, alpha), null, new Rect(x, r.Top + 3, (r.Width - 38) / 22 - 2, 3));
        }
        for (int side = 0; side < 2; side++)
        {
            double x = side == 0 ? r.Left + 5 : r.Right - 5, direction = side == 0 ? 1 : -1;
            GlowLine(dc, new Point(x, r.Top + 25), new Point(x, r.Bottom - 20), p.Accent, .72);
            GlowLine(dc, new Point(x, r.Top + 25), new Point(x + direction * 7, r.Top + 18), p.Accent, .72);
            for (int i = 0; i < 5; i++) dc.DrawLine(Line(p.Secondary, .35 + .45 * Math.Pow(.5 + .5 * Math.Sin(time * 1.4 - i), 3), 2), new Point(x + direction * 5, r.Bottom - 21 - i * 5), new Point(x + direction * 9, r.Bottom - 21 - i * 5));
        }
        GlowLine(dc, new Point(r.Right - 70, r.Bottom - 4), new Point(r.Right - 19, r.Bottom - 4), p.Secondary, .8, 2);
    }

    private static void Synthwave(DrawingContext dc, Rect r, SkinPalette p, double time, bool small)
    {
        double horizon = r.Top + r.Height * (small ? .6 : .34), sunRadius = small ? 30 : 80;
        var sun = new Point(r.Left + r.Width * .78, horizon - sunRadius * .48);
        Halo(dc, sun, sunRadius * 2.3, sunRadius * 1.8, p.Secondary, .38);
        dc.PushClip(new EllipseGeometry(sun, sunRadius, sunRadius));
        var sunset = new LinearGradientBrush(Brush("#FFD583", .85).Color, Brush(p.Secondary, .65).Color, 90);
        for (double y = sun.Y - sunRadius; y < sun.Y + sunRadius; y += 5)
        {
            double gap = Math.Max(.4, (y - sun.Y + sunRadius) / (2 * sunRadius) * 2.8);
            dc.DrawRectangle(sunset, null, new Rect(sun.X - sunRadius, y, sunRadius * 2, 5 - gap));
        }
        dc.Pop();
        var vanishing = new Point(r.Left + r.Width * .5, horizon);
        for (int i = -8; i <= 8; i++) dc.DrawLine(Line(p.Accent, .25), vanishing, new Point(r.Left + r.Width * .5 + i * r.Width * .19, r.Bottom));
        for (int i = 0; i < 11; i++)
        {
            double depth = Cycle(i / 11.0 + time * .10);
            double y = horizon + depth * depth * (r.Bottom - horizon);
            GlowLine(dc, new Point(r.Left, y), new Point(r.Right, y), p.Accent, .2 + depth * .38, .7);
        }
        GlowLine(dc, new Point(r.Left, horizon), new Point(r.Right, horizon), p.Secondary, .7);
        for (int i = 0; i < 13; i++)
        {
            double x = r.Left + Cycle(i * .618 + .2) * r.Width, y = r.Top + Cycle(i * .381 + .13) * (horizon - r.Top);
            dc.DrawEllipse(Brush(p.Text, .15 + .25 * Math.Pow(Math.Sin(time * .5 + i), 2)), null, new Point(x, y), .65, .65);
        }
    }

    private static void Warp(DrawingContext dc, Rect r, SkinPalette p, double time, bool small)
    {
        var center = new Point(r.Left + r.Width * .58, r.Top + r.Height * (small ? .50 : .3));
        Halo(dc, center, r.Width * .55, small ? r.Height : r.Width * .6, p.Secondary, .22);
        int count = small ? 54 : 110;
        for (int i = 0; i < count; i++)
        {
            double depth = Cycle(i * .61803398875 + time * (.115 + i % 5 * .012));
            double angle = i * 2.39996323, radius = Math.Max(r.Width, r.Height) * .8 * depth * depth;
            double stretch = .52 + depth * .48;
            var head = new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius * stretch);
            double tailRadius = radius * (1 - .11 * depth);
            var tail = new Point(center.X + Math.Cos(angle) * tailRadius, center.Y + Math.Sin(angle) * tailRadius * stretch);
            double opacity = Math.Sin(Math.PI * depth) * (.25 + depth * .7);
            string color = i % 5 == 0 ? p.Secondary : i % 3 == 0 ? p.Text : p.Accent;
            dc.DrawLine(Line(color, opacity * .17, 3), tail, head);
            dc.DrawLine(Line(color, opacity, .65 + depth * .65), tail, head);
            if (depth > .45) dc.DrawEllipse(Brush(color, opacity), null, head, .6, .6);
        }
    }

    private static void Plasma(DrawingContext dc, Rect r, SkinPalette p, double time, bool small)
    {
        for (int ribbon = 0; ribbon < 4; ribbon++)
        {
            string color = ribbon % 2 == 0 ? p.Accent : p.Secondary;
            var curve = new StreamGeometry();
            using (var path = curve.Open())
            {
                for (int n = 0; n <= 60; n++)
                {
                    double x = n / 60.0;
                    double wave = Math.Sin(x * 6.5 + time * .7 + ribbon * 1.9) * .12 + Math.Sin(x * 12 - time * .45 + ribbon) * .055;
                    double baseline = ribbon < 2 ? .13 : .88;
                    var point = new Point(r.Left + x * r.Width, r.Top + r.Height * (baseline + wave));
                    if (n == 0) path.BeginFigure(point, false, false); else path.LineTo(point, true, false);
                }
            }
            curve.Freeze();
            dc.DrawGeometry(null, Line(color, .06, small ? 15 : 28), curve);
            dc.DrawGeometry(null, Line(color, .17, 5), curve);
            dc.DrawGeometry(null, Line(color, .72, 1.3), curve);
            double phase = Cycle(time * .085 + ribbon * .28);
            double y = (ribbon < 2 ? .13 : .88) + Math.Sin(phase * 6.5 + time * .7 + ribbon * 1.9) * .12 + Math.Sin(phase * 12 - time * .45 + ribbon) * .055;
            var at = new Point(r.Left + phase * r.Width, r.Top + r.Height * y);
            Halo(dc, at, 12, 9, color, .8);
            dc.DrawEllipse(Brush(p.Text, .85), null, at, 1.5, 1.5);
        }
    }

    // Mixed half-width kana, Latin symbols and numerals; glyph outlines are built only once.
    private const string RainAlphabet = "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789:+=<>/*{}";
    private static readonly Lazy<Geometry[]> RainGlyphs = new(() => RainAlphabet.Select((c, index) =>
    {
        var glyph = Text(c.ToString(), 10, "#FFFFFF", 1, "Yu Gothic, MS Gothic, Meiryo, Segoe UI Symbol, Consolas").BuildGeometry(new Point(0, 0));
        var bounds = glyph.Bounds;
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) { glyph.Freeze(); return glyph; }
        double scale = Math.Min(6.1 / bounds.Width, 8.0 / bounds.Height);
        var transforms = new TransformGroup();
        transforms.Children.Add(new TranslateTransform(-bounds.X, -bounds.Y));
        transforms.Children.Add(new ScaleTransform(scale, scale));
        transforms.Children.Add(new TranslateTransform((6.1 - bounds.Width * scale) / 2, 0));
        if (c >= 'ｱ' && c <= 'ﾝ' && index % 2 == 0)
        {
            transforms.Children.Add(new ScaleTransform(-1, 1));
            transforms.Children.Add(new TranslateTransform(6.1, 0));
        }
        glyph.Transform = transforms; glyph.Freeze(); return glyph;
    }).ToArray());
    private static void Matrix(DrawingContext dc, Rect r, SkinPalette p, double time, bool small)
    {
        int columns = small ? 28 : 42;
        int trail = small ? 16 : 20;
        const double rowStep = 10;
        double period = (trail + (small ? 8 : 9)) * rowStep;
        // Reuse the trail brushes across columns; dense rain must not allocate a brush per glyph.
        var bright = new SolidColorBrush[trail];
        var dim = new SolidColorBrush[trail];
        for (int row = 0; row < trail; row++)
        {
            double fade = Math.Pow(1 - row / (double)trail, 1.25);
            bright[row] = Brush(row == 0 ? "#DEFFE8" : "#64ED91", row == 0 ? .94 : fade * .76);
            dim[row] = Brush(row == 0 ? "#C1FFDA" : "#45BA76", row == 0 ? .70 : fade * .54);
        }
        for (int col = 0; col < columns; col++)
        {
            double x = r.Left + 5 + col * (r.Width - 10) / columns;
            double offset = Cycle(col * .618 + time * (12 + col % 5 * 3) / period) * period;
            var brushes = col % 3 == 0 ? bright : dim;
            // Repeat each stream down tall windows so the rain covers the whole surface.
            for (double head = offset; head < r.Height + trail * rowStep; head += period)
            for (int row = 0; row < trail; row++)
            {
                double y = r.Top + head - row * rowStep;
                if (y < r.Top - 8 || y > r.Bottom) continue;
                dc.PushTransform(new TranslateTransform(x, y));
                int epoch = (int)Math.Floor(time * (.55 + col % 4 * .12) + row * .17);
                uint hash = unchecked((uint)(col * 92821 + row * 68917 + epoch * 283 + (int)(head / period) * 1531));
                hash ^= hash >> 13; hash *= 1274126177u; hash ^= hash >> 16;
                dc.DrawGeometry(brushes[row], null, RainGlyphs.Value[hash % (uint)RainGlyphs.Value.Length]);
                dc.Pop();
            }
        }
        GlowLine(dc, new Point(r.Left + 12, r.Bottom - 3), new Point(r.Left + 65, r.Bottom - 3), p.Accent, .7);
    }

    private static void Circuit(DrawingContext dc, Rect r, SkinPalette p, double time)
    {
        for (int i = 0; i < 3; i++)
        {
            double inset = 5 + i * 5, y = r.Bottom - inset;
            double start = r.Right - 90 - i * 17, end = r.Right - 6;
            Point a = new(start, y - 9), b = new(start + 9, y), c = new(end, y);
            string color = i % 2 == 0 ? p.Accent : p.Secondary;
            dc.DrawLine(Line(color, .3), a, b); dc.DrawLine(Line(color, .3), b, c);
            double progress = Cycle(time * .22 + i * .33), x = b.X + (c.X - b.X) * progress;
            Halo(dc, new Point(x, y), 7, 5, color, .6);
            dc.DrawLine(Line(color, .9, 1.5), new Point(x, y), new Point(Math.Min(end, x + 9), y));
        }
        double scanX = r.Left + Cycle(time * .12) * r.Width;
        var beam = new LinearGradientBrush(new GradientStopCollection { new(Colors.Transparent, 0), new(Brush(p.Secondary, .15).Color, .5), new(Colors.Transparent, 1) }, new Point(0, 0), new Point(1, 0));
        dc.DrawRectangle(beam, null, new Rect(scanX - 12, r.Top, 24, r.Height));
    }
}
