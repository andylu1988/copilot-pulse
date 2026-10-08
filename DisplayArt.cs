using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace CopilotPulse;

internal static partial class DisplayArt
{
    internal static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    internal static SolidColorBrush Brush(string hex, double alpha = 1)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex); c.A = (byte)Math.Round(c.A * Math.Clamp(alpha, 0, 1));
        var brush = new SolidColorBrush(c); brush.Freeze(); return brush;
    }
    internal static FormattedText Text(string text, double size, string color, double dpi, string font = "Segoe UI", bool bold = false) => new(text, Us, FlowDirection.LeftToRight, new Typeface(new FontFamily(font), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, Brush(color), dpi);
    internal static double Radius(SkinPalette p) => Geek(p.Id) || p.Id is "arcade" or "eink" ? 3 : p.Id is "lcd" or "cyber" or "mecha" or "matrix" ? 8 : 17;
    internal static bool Advanced(string id) => id is "radar" or "mecha" or "synthwave" or "warp" or "plasma" or "matrix";
    internal static bool Ambient(string id) => Geek(id) || Advanced(id) || id is "cyber" or "terminal" or "nixie" or "aurora" or "arcade";
    internal static double EffectOpacity => Math.Clamp(SkinTheme.Settings.EffectIntensity / 1.5, .23, 1);

    internal static Geometry Frame(Rect r, SkinPalette p)
    {
        if (p.Id != "cyber" && p.Id != "mecha" && p.Id != "arcade") return new RectangleGeometry(r, Radius(p), Radius(p));
        double c = p.Id is "cyber" or "mecha" ? 12 : 5;
        var points = p.Id is "cyber" or "mecha" ? new[] { new Point(r.Left, r.Top), new Point(r.Right-c, r.Top), new Point(r.Right,r.Top+c), new Point(r.Right,r.Bottom),new Point(r.Left+c,r.Bottom),new Point(r.Left,r.Bottom-c) } :
            new[] { new Point(r.Left+c,r.Top),new Point(r.Right-c,r.Top),new Point(r.Right-c,r.Top+c),new Point(r.Right,r.Top+c),new Point(r.Right,r.Bottom-c),new Point(r.Right-c,r.Bottom-c),new Point(r.Right-c,r.Bottom),new Point(r.Left+c,r.Bottom),new Point(r.Left+c,r.Bottom-c),new Point(r.Left,r.Bottom-c),new Point(r.Left,r.Top+c),new Point(r.Left+c,r.Top+c) };
        var g = new StreamGeometry(); using (var ctx=g.Open()) { ctx.BeginFigure(points[0],true,true); ctx.PolyLineTo(points.Skip(1).ToList(),true,false); } g.Freeze(); return g;
    }

    internal static void Texture(DrawingContext dc, Rect r, SkinPalette p, double time, bool small)
    {
        dc.PushClip(new RectangleGeometry(r, Radius(p), Radius(p)));
        dc.PushOpacity(Ambient(p.Id) ? EffectOpacity : 1);
        AdvancedTexture(dc, r, p, time, small);
        GeekTexture(dc, r, p, time, small);
        string id = p.Id;
        if (id == "lcd")
        {
            for (double y=r.Top+2;y<r.Bottom;y+=3) dc.DrawLine(new Pen(Brush(p.Text,.045),.6),new Point(r.Left,y),new Point(r.Right,y));
            dc.DrawLine(new Pen(Brush("#FFFFFF",.38),1),new Point(r.Left+10,r.Top+3),new Point(r.Right-10,r.Top+3));
        }
        if (id is "cyber" or "terminal")
        {
            for(double y=r.Top;y<r.Bottom;y+=id=="terminal"?3:14) dc.DrawLine(new Pen(Brush(p.Accent,id=="terminal"?.07:.05),.7),new Point(r.Left,y),new Point(r.Right,y));
            if(id=="cyber") for(double x=r.Left;x<r.Right;x+=22) dc.DrawLine(new Pen(Brush(p.Accent,.035),.6),new Point(x,r.Top),new Point(x,r.Bottom));
            double scan = r.Top + (time % 9 / 9) * r.Height;
            var beam = new LinearGradientBrush(new GradientStopCollection { new(Brush(p.Accent,0).Color,0),new(Brush(p.Accent,.09).Color,.5),new(Brush(p.Accent,0).Color,1) },new Point(0,0),new Point(0,1));
            dc.DrawRectangle(beam,null,new Rect(r.Left,scan-6,r.Width,12));
        }
        if(id=="aurora")
        {
            for(int i=0;i<3;i++)
            {
                double x=r.Left+r.Width*(.2+i*.32+Math.Sin(time*.35+i)*.09),y=r.Top+r.Height*(.3+Math.Cos(time*.22+i)*.2);
                var glow=new RadialGradientBrush(Brush(i==1?p.Secondary:p.Accent,.22).Color,Colors.Transparent);
                dc.DrawEllipse(glow,null,new Point(x,y),r.Width*.48,r.Height*.8);
            }
            dc.DrawLine(new Pen(Brush("#FFFFFF",.20),1),new Point(r.Left+20,r.Top+4),new Point(r.Right-28,r.Top+4));
        }
        if(id=="arcade")
        {
            int count=small?15:55;
            for(int i=0;i<count;i++)
            {
                double x=r.Left+((i*67+23)%227)/227.0*r.Width,y=r.Top+((i*31+13)%97)/97.0*r.Height;
                double alpha=.1+.2*(.5+.5*Math.Sin(time*.85+i*1.9));
                dc.DrawRectangle(Brush(i%3==0?p.Secondary:p.Accent,alpha),null,new Rect(Math.Round(x),Math.Round(y),i%4==0?2:1,i%4==0?2:1));
            }
        }
        if(id=="eink")
            for(double y=r.Top+5;y<r.Bottom;y+=12) for(double x=r.Left+5;x<r.Right;x+=12) dc.DrawEllipse(Brush(p.Edge,.10),null,new Point(x,y),.45,.45);
        if(id=="nixie")
            for(double y=r.Top+4;y<r.Bottom;y+=5) dc.DrawLine(new Pen(Brush(p.Accent,.025),.6),new Point(r.Left,y),new Point(r.Right,y));
        dc.Pop(); dc.Pop();
    }

    internal static void Money(DrawingContext dc, Rect box, decimal? value, SkinPalette p, double time, double pulse, double dpi)
    {
        if(!value.HasValue) { CenterText(dc,box,"—",p,Math.Min(38,box.Height),dpi); return; }
        string text=value.Value.ToString("C2",Us);
        if (Geek(p.Id)) { GeekMoney(dc, box, text, p, dpi); return; }
        if(p.Id=="lcd") { SevenSegment(dc,box,text,p); return; }
        if(p.Id is "terminal" or "arcade") { Pixels(dc,box,text,p); return; }
        if(p.Id=="nixie") { Tubes(dc,box,text,p,time,pulse,dpi); return; }
        double size=Math.Min(43,box.Height*1.03);
        var font=p.Id is "cyber" or "eink" or "mecha" or "matrix" or "radar"?"Cascadia Mono, Consolas":"Segoe UI Variable Display, Segoe UI";
        var label=Text(text,size,p.Text,dpi,font,true);
        if(label.Width>box.Width) { size*=box.Width/label.Width; label=Text(text,size,p.Text,dpi,font,true); }
        var at=new Point(box.Left+(box.Width-label.Width)/2,box.Top+(box.Height-label.Height)/2);
        if(p.Id=="cyber")
        {
            dc.PushOpacity(.36); dc.DrawText(Text(text,size,p.Secondary,dpi,font,true),new Point(at.X+.85,at.Y+.25));
            dc.DrawText(Text(text,size,p.Accent,dpi,font,true),new Point(at.X-.75,at.Y-.1)); dc.Pop();
        }
        if (Advanced(p.Id) && p.Id != "matrix")
        {
            var shape = label.BuildGeometry(at);
            dc.DrawGeometry(null, new Pen(Brush(p.Accent, .10 * EffectOpacity), 3), shape);
        }
        if(pulse>0 && !p.Light) { var geometry=label.BuildGeometry(at); dc.DrawGeometry(Brush(p.Accent,pulse*.08),new Pen(Brush(p.Accent,pulse*.10),4),geometry); }
        dc.DrawText(label,at);
    }
    private static void CenterText(DrawingContext dc,Rect box,string value,SkinPalette p,double size,double dpi)
    {
        var text=Text(value,size,p.Text,dpi,"Segoe UI",true); dc.DrawText(text,new Point(box.Left+(box.Width-text.Width)/2,box.Top+(box.Height-text.Height)/2));
    }

    internal static readonly int[] SegmentMasks = { 0x3F,0x06,0x5B,0x4F,0x66,0x6D,0x7D,0x07,0x7F,0x6F };
    private static readonly Geometry[] Segments = {
        Geometry.Parse("M3,0 L19,0 L22,2 L19,4 L3,4 L0,2 Z"),
        Geometry.Parse("M19,5 L22,3 L22,19 L20,21 L18,19 L18,7 Z"),
        Geometry.Parse("M20,22 L22,24 L22,40 L19,38 L18,36 L18,25 Z"),
        Geometry.Parse("M3,39 L19,39 L22,41 L19,43 L3,43 L0,41 Z"),
        Geometry.Parse("M0,24 L2,22 L4,25 L4,36 L3,38 L0,40 Z"),
        Geometry.Parse("M0,3 L3,5 L4,7 L4,19 L2,21 L0,19 Z"),
        Geometry.Parse("M3,20 L19,20 L21,22 L19,24 L3,24 L1,22 Z")
    };
    private static double SegmentWidth(char c)=>char.IsDigit(c)?26:c=='$'?15:9;
    private static void SevenSegment(DrawingContext dc,Rect box,string text,SkinPalette p)
    {
        double full=text.Sum(SegmentWidth), scale=Math.Min(box.Width/full,box.Height/43);
        dc.PushTransform(new TranslateTransform(box.Left+(box.Width-full*scale)/2,box.Top+(box.Height-43*scale)/2)); dc.PushTransform(new ScaleTransform(scale,scale));
        double x=0;
        foreach(char c in text)
        {
            dc.PushTransform(new TranslateTransform(x,0));
            if(char.IsDigit(c)) for(int i=0;i<7;i++) dc.DrawGeometry(Brush((SegmentMasks[c-'0']&(1<<i))!=0?p.Text:p.Edge,(SegmentMasks[c-'0']&(1<<i))!=0?1:.24),null,Segments[i]);
            else if(c=='.') dc.DrawRectangle(Brush(p.Text),null,new Rect(1,39,4,4));
            else if(c==',') { dc.DrawRectangle(Brush(p.Text),null,new Rect(2,38,4,4)); dc.DrawLine(new Pen(Brush(p.Text),2),new Point(5,42),new Point(3,45)); }
            else if(c=='$') dc.DrawText(Text("$",20,p.Text,1,"Consolas",true),new Point(0,17));
            dc.Pop(); x+=SegmentWidth(c);
        }
        dc.Pop();dc.Pop();
    }

    private static readonly Dictionary<char,int[]> PixelGlyphs=new() {
        ['0']=new[]{14,17,19,21,25,17,14},['1']=new[]{4,12,4,4,4,4,14},['2']=new[]{14,17,1,2,4,8,31},['3']=new[]{30,1,1,14,1,1,30},
        ['4']=new[]{2,6,10,18,31,2,2},['5']=new[]{31,16,16,30,1,1,30},['6']=new[]{14,16,16,30,17,17,14},['7']=new[]{31,1,2,4,8,8,8},
        ['8']=new[]{14,17,17,14,17,17,14},['9']=new[]{14,17,17,15,1,1,14},['$']=new[]{4,15,20,14,5,30,4},['-']=new[]{0,0,0,31,0,0,0}
    };
    private static double PixelWidth(char c)=>c is '.' or ','?2:6;
    private static void Pixels(DrawingContext dc,Rect box,string text,SkinPalette p)
    {
        double width=text.Sum(PixelWidth)-1, unit=Math.Min(box.Width/width,box.Height/7.7);
        double x=box.Left+(box.Width-width*unit)/2,y=box.Top+(box.Height-7*unit)/2;
        foreach(char c in text)
        {
            if(c is '.' or ',') { dc.DrawRectangle(Brush(p.Accent),null,new Rect(x,y+6*unit,unit*.8,unit*.8)); if(c==',') dc.DrawRectangle(Brush(p.Accent),null,new Rect(x-unit*.25,y+7*unit,unit*.65,unit*.65)); }
            else if(PixelGlyphs.TryGetValue(c,out var rows))
                for(int row=0;row<7;row++)for(int col=0;col<5;col++) if((rows[row]&(1<<(4-col)))!=0)
                    dc.DrawRectangle(Brush(p.Id=="arcade"?p.Text:p.Accent),null,new Rect(x+col*unit,y+row*unit,unit*(p.Id=="terminal"?.74:.87),unit*(p.Id=="terminal"?.74:.87)));
            x+=PixelWidth(c)*unit;
        }
    }
    private static void Tubes(DrawingContext dc,Rect box,string text,SkinPalette p,double time,double pulse,double dpi)
    {
        double W(char c)=>char.IsDigit(c)?27:c=='$'?16:8;
        double width=text.Sum(W),scale=Math.Min(box.Width/width,box.Height/49);
        dc.PushTransform(new TranslateTransform(box.Left+(box.Width-width*scale)/2,box.Top+(box.Height-49*scale)/2)); dc.PushTransform(new ScaleTransform(scale,scale));
        double x=0;
        foreach(char c in text)
        {
            if(char.IsDigit(c))
            {
                var glass=new LinearGradientBrush(Brush("#FFE0A5",.055).Color,Brush("#150C04",.18).Color,0);
                dc.DrawRoundedRectangle(glass,new Pen(Brush(p.Accent,.27),.8),new Rect(x,0,24,47),8,8);
                dc.DrawLine(new Pen(Brush("#FFF0D4",.13),1),new Point(x+4,8),new Point(x+4,35));
                var label=Text(c.ToString(),36,p.Accent,dpi,"Bahnschrift, Segoe UI");
                var at=new Point(x+(24-label.Width)/2,3);
                var shape=label.BuildGeometry(at);
                double glow=(.10+.05*Math.Sin(time*.8)+pulse*.04)*EffectOpacity;
                dc.DrawGeometry(null,new Pen(Brush(p.Accent,glow),6),shape); dc.DrawGeometry(null,new Pen(Brush(p.Accent,.20),2.5),shape); dc.DrawText(label,at);
                dc.DrawLine(new Pen(Brush(p.Edge,.8),1),new Point(x+8,47),new Point(x+8,49)); dc.DrawLine(new Pen(Brush(p.Edge,.8),1),new Point(x+16,47),new Point(x+16,49));
            }
            else dc.DrawText(Text(c.ToString(),c=='$'?18:28,p.Accent,dpi,"Segoe UI"),new Point(x,c=='$'?14:12));
            x+=W(c);
        }
        dc.Pop();dc.Pop();
    }
}
