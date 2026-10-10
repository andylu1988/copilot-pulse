using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CopilotPulse;

internal sealed record SkinPalette(string Id, string Name, string Description, string Top, string Bottom, string Panel, string PanelAlt, string Edge, string Muted, string Faint, string Accent, string Secondary, string Hover, string Chart, string Grid)
{
    internal string Text => Id switch { "lcd" => "#24391F", "eink" => "#20282C", "shell" => "#E1E8E3", "amber" => "#FFD28C", "scope" => "#B9F5CC", _ => "#F0F4FC" };
    internal bool Light => Id is "lcd" or "eink";
    internal bool Photo => Id is "bayi" or "custom";
}

internal static class SkinTheme
{
    internal static readonly SkinPalette[] Catalog = {
        new("matrix", "矩阵雨 · 精工", "曜石金属 · 翡翠微光", "#0B1613", "#050B09", "#101B18", "#13211D", "#34584A", "#C0D8CD", "#92AFA1", "#78EFB0", "#A6D5C0", "#203C30", "#091511", "#203C2D"),
        new("shell", "极客终端", "黑白等宽 · 呼吸光标", "#0C100F", "#080C0B", "#121917", "#141C19", "#35483E", "#B6C6BA", "#91A398", "#C4E5B4", "#A5C7B5", "#25352C", "#0A110D", "#283C2E"),
        new("amber", "琥珀工作站", "单色数码 · 慢速扫描", "#1B140A", "#0D0B06", "#221A0D", "#281D0F", "#665232", "#D6BE93", "#B6A075", "#FFC568", "#E4A95B", "#3A2D18", "#130F08", "#45351D"),
        new("scope", "矢量示波器", "磷光轨迹 · 精密点网", "#09170F", "#050D09", "#102319", "#132B1D", "#315B41", "#B1D3BA", "#82B193", "#7BFFA6", "#C6E7B0", "#234832", "#091910", "#20412D"),
        new("debug", "调试面板", "代码行号 · 插入光标", "#15191F", "#0E1217", "#1A2129", "#20252F", "#3B4A5A", "#BECCDB", "#91A4B8", "#94CAFF", "#D0B1F8", "#263649", "#121A23", "#2B3949"),
        new("radar", "全息雷达", "旋转扫掠 · 回波光点", "#082A2E", "#031216", "#0D2E31", "#123837", "#2B6266", "#BEE6DF", "#8DBEB8", "#52FFE0", "#9DCFFF", "#20504C", "#061F23", "#1A4848"),
        new("mecha", "机甲 HUD", "反向环轨 · 能量灯带", "#14263A", "#08131F", "#162F43", "#29302E", "#3C5A6E", "#C9E0EB", "#96B6CB", "#6CDAFF", "#FFB84D", "#2C4557", "#0C2031", "#294355"),
        new("synthwave", "落日合成波", "条纹落日 · 无限网格", "#331947", "#140A2A", "#301E43", "#38223F", "#6C438B", "#EDCDEF", "#C1A2D5", "#8A98FF", "#FF65B3", "#513060", "#211232", "#4C2B60"),
        new("warp", "星际穿梭", "纵深星流 · 曲速航行", "#121D3A", "#050B1D", "#15263E", "#231E40", "#364C79", "#CADCF5", "#94AFD8", "#95CFFF", "#B58CFF", "#2B3D62", "#0D162E", "#263655"),
        new("plasma", "等离子流", "双色光带 · 粒子追逐", "#291331", "#100B20", "#2F1E39", "#173039", "#664574", "#E2CFEF", "#BAA4D0", "#F68FFF", "#60F7E0", "#463151", "#1E132C", "#483151"),
        new("lcd", "掌上液晶", "七段数字 · 复古显示屏", "#D3DCAA", "#ACBD8B", "#C5D3A0", "#B9CB94", "#748957", "#344928", "#435934", "#2F552F", "#49672C", "#A9BD80", "#CDD9AA", "#A0B180"),
        new("cyber", "霓虹协议", "双向扫光 · 电路追光", "#13152C", "#080D1B", "#141D35", "#241934", "#365771", "#C2DBF3", "#93B9D1", "#55E5F4", "#FF6AAE", "#24395A", "#0C1428", "#243C57"),
        new("terminal", "绿磷终端", "点阵数字 · CRT 扫描线", "#13241C", "#07110E", "#102B20", "#173325", "#2E6447", "#B6DBBD", "#88BD93", "#81F0A4", "#C6E992", "#244937", "#0A1D14", "#1F4833"),
        new("nixie", "琥珀辉光", "玻璃灯管 · 温暖呼吸光", "#30231D", "#160E0D", "#30221C", "#35251D", "#79553E", "#E9CDB9", "#C5A084", "#FFAA62", "#FFD79E", "#4D3425", "#211612", "#503626"),
        new("aurora", "极光玻璃", "流动光晕 · 通透质感", "#172C3F", "#091727", "#163347", "#173C40", "#3B6B7F", "#C2E2E8", "#9BC3CE", "#76E4DA", "#9CAAFF", "#295368", "#102B3D", "#2C5065"),
        new("arcade", "像素街机", "像素数字 · 闪烁星点", "#2A2048", "#140F25", "#2B2042", "#312447", "#6B528C", "#E3D2F5", "#BDA6D8", "#B899FF", "#FF8FB3", "#483565", "#201631", "#49345F"),
        new("eink", "电子墨水", "纸张纹理 · 安静无动效", "#F3F2E9", "#DBE0D9", "#ECEEE5", "#E2E8DE", "#A3B0A3", "#3D4C47", "#56675E", "#2F5551", "#596A37", "#D2DCD1", "#EEF1E8", "#CBD4C6"),
        new("midnight", "深海蓝", "经典配色", "#19283F", "#0D1728", "#172A42", "#152F37", "#3B536D", "#BDCCE0", "#95ABC7", "#83C6FF", "#70DDC5", "#2B4665", "#101E32", "#314762"),
        new("graphite", "曜石黑", "简洁沉稳", "#282B32", "#111418", "#22272F", "#262B33", "#4A5361", "#CDD2DB", "#A0A9B8", "#A9C1FF", "#D0D8E8", "#3A4250", "#181D24", "#353E4C"),
        new("forest", "松林绿", "柔和清新", "#1C3832", "#0C1D1C", "#17352F", "#203A35", "#3B6559", "#BBD9CF", "#94B7AB", "#83DEC0", "#C1D98C", "#2C5146", "#102A25", "#2D5146"),
        new("violet", "暮光紫", "温柔夜色", "#362D51", "#181527", "#2B2542", "#352841", "#62517C", "#D4C7E8", "#B6A1CF", "#C4A4FF", "#EEB1D1", "#4E3D69", "#211C34", "#493A62"),
        new("bayi", "巴依老爷", "你的图片背景", "#19283F", "#0D1728", "#172A42", "#152F37", "#465D79", "#CDDAEC", "#ADC0D9", "#83C6FF", "#70DDC5", "#2B4665", "#101E32", "#314762"),
        new("custom", "自定义图片", "选择本机图片", "#19283F", "#0D1728", "#172A42", "#152F37", "#465D79", "#CDDAEC", "#ADC0D9", "#83C6FF", "#70DDC5", "#2B4665", "#101E32", "#314762")
    };
    internal static SkinPalette Current { get; private set; } = Catalog.First(s => s.Id == "midnight");
    internal static Preferences Settings { get; private set; } = new();
    internal static string? ImageError { get; private set; }
    private static BitmapSource? custom;
    private static string? customPath;
    private static readonly Lazy<BitmapSource> Bayi = new(() => LoadImage(new Uri("pack://application:,,,/CopilotPulse;component/Assets/bayi-background.png")));
    private static readonly Lazy<BitmapSource> MatrixMini = new(() => LoadImage(new Uri("pack://application:,,,/CopilotPulse;component/Assets/matrix-art-mini.png")));
    private static readonly Lazy<BitmapSource> MatrixPortrait = new(() => LoadImage(new Uri("pack://application:,,,/CopilotPulse;component/Assets/matrix-art-portrait.png")));
    private static readonly Lazy<BitmapSource> MatrixWide = new(() => LoadImage(new Uri("pack://application:,,,/CopilotPulse;component/Assets/matrix-art-wide.png")));
    internal static BitmapSource? CurrentImage => Current.Id == "bayi" ? Bayi.Value : Current.Id == "custom" ? custom : null;
    internal static bool HasImage => CurrentImage != null;

    internal static SkinPalette Find(string? id) => Catalog.FirstOrDefault(s => s.Id == id) ?? Catalog.First(s => s.Id == "midnight");
    internal static void Normalize(Preferences p)
    {
        SettingsPolicy.Normalize(p);
        p.SkinId = string.IsNullOrEmpty(p.SkinId) ? p.PhotoBackground ? "bayi" : "midnight" : Find(p.SkinId).Id;
        p.EffectIntensity = double.IsFinite(p.EffectIntensity) ? Math.Clamp(p.EffectIntensity, .35, 1.5) : 1;
        p.EffectSpeed = double.IsFinite(p.EffectSpeed) ? Math.Clamp(p.EffectSpeed, .5, 1.5) : 1;
        p.PhotoDim = double.IsFinite(p.PhotoDim) ? Math.Clamp(p.PhotoDim, 0.45, 0.85) : 0.65;
        p.CustomFocusX = ClampFocus(p.CustomFocusX, 0.5); p.CustomFocusY = ClampFocus(p.CustomFocusY, 0.35);
        p.BayiFocusX = ClampFocus(p.BayiFocusX, 0.755); p.BayiFocusY = ClampFocus(p.BayiFocusY, 0.29);
        p.PhotoBackground = Find(p.SkinId).Photo;
        p.DisplayMode = DisplayModes.Normalize(p.DisplayMode, p.Compact);
    }
    private static double ClampFocus(double value, double fallback) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : fallback;

    internal static void Configure(Preferences p)
    {
        Normalize(p);
        bool paletteChanged = Current.Id != Find(p.SkinId).Id || !Application.Current.Resources.Contains("SkinText");
        Settings = p;
        Current = Find(p.SkinId);
        ImageError = null;
        if (Current.Id == "custom" && (customPath != p.CustomBackgroundPath || custom == null))
        {
            custom = null; customPath = p.CustomBackgroundPath;
            try { custom = SkinImages.LoadLocal(p.CustomBackgroundPath); }
            catch (Exception e) when (SkinImages.IsImageError(e)) { ImageError = "自定义图片暂不可用，请重新选择。当前使用深色底。"; }
        }
        if (!paletteChanged) return;
        var resources = Application.Current.Resources;
        var colors = new Dictionary<string, string> {
            ["SkinText"] = Current.Text, ["SkinMuted"] = Current.Muted, ["SkinFaint"] = Current.Faint,
            ["SkinAccent"] = Current.Accent, ["SkinSecondary"] = Current.Secondary, ["SkinPanel"] = Current.Panel,
            ["SkinPanelAlt"] = Current.PanelAlt, ["SkinSurface"] = Current.Bottom, ["SkinEdge"] = Current.Edge,
            ["SkinHover"] = Current.Hover, ["SkinChart"] = Current.Chart,
            ["MenuSurface"] = Current.Panel, ["MenuEdge"] = Current.Edge, ["MenuText"] = Current.Text,
            ["MenuMuted"] = Current.Faint, ["MenuHover"] = Current.Hover, ["MenuAccent"] = Current.Accent
        };
        foreach (var pair in colors) resources[pair.Key] = Solid(pair.Value);
        resources["SkinFont"] = new FontFamily(DisplayArt.Geek(Current.Id) ? "Cascadia Mono, Consolas, Microsoft YaHei UI" : "Segoe UI, Microsoft YaHei UI");
    }

    internal static SolidColorBrush Solid(string color) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush; }
    internal static SolidColorBrush Resolve(string value)
    {
        var original = (Color)ColorConverter.ConvertFromString(value);
        string hex = $"#{original.R:X2}{original.G:X2}{original.B:X2}";
        string mapped = hex switch {
            "#66D9B8" or "#79D9BF" or "#7EBEAB" => Current.Light ? "#286C38" : hex,
            "#F1B58B" or "#C7A370" or "#C8AC83" or "#E4BA7C" => Current.Light ? "#865321" : hex,
            "#78B6FF" or "#83D6EC" or "#80C9F0" or "#88D8F5" or "#83B7DC" or "#79B8F7" or "#8BD9F0" => Current.Accent,
            "#68DAC8" or "#70DCC7" => Current.Secondary,
            "#152338" or "#152237" or "#172438" => Current.Panel,
            "#132630" => Current.PanelAlt,
            "#101D30" => Current.Chart,
            "#111E31" => Current.Bottom,
            "#24374F" or "#2B3950" or "#263349" => Current.Grid,
            "#2B3E59" or "#264348" or "#344158" or "#354961" or "#4B6687" or "#2D435E" => Current.Edge,
            "#21334C" or "#284560" or "#26344C" => Current.Hover,
            "#E0E9F8" or "#EAF1FD" or "#EFF4FF" or "#EFF8FF" or "#E5F8FF" or "#EDF4FF" => Current.Text,
            "#738BAE" or "#617A9D" or "#8EA6C7" or "#7E94B2" or "#7085A3" or "#7186A6" or "#91AAC9" or "#58728E" => Current.Faint,
            "#A9BDD6" or "#879FBE" or "#708FAD" or "#A6B6D0" => Current.Muted,
            "#223B5B" => Current.Hover,
            "#193F40" => Current.PanelAlt,
            _ => hex
        };
        var c = (Color)ColorConverter.ConvertFromString(mapped);
        c.A = original.A;
        var result = new SolidColorBrush(c); result.Freeze(); return result;
    }

    internal static Brush Preview(SkinPalette skin)
    {
        if (skin.Id == "bayi") return new ImageBrush(Bayi.Value) { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Right };
        if (skin.Id == "custom" && custom != null && customPath == Settings.CustomBackgroundPath) return new ImageBrush(custom) { Stretch = Stretch.UniformToFill };
        return new LinearGradientBrush((Color)ColorConverter.ConvertFromString(skin.Top), (Color)ColorConverter.ConvertFromString(skin.Bottom), 65);
    }

    internal static (Brush Background, Brush Shade) Backdrop(SkinPalette p, double width, double height, bool wide)
    {
        if (p.Id == "matrix")
        {
            var asset = height < 160 ? MatrixMini.Value : width > height ? MatrixWide.Value : MatrixPortrait.Value;
            var art = new ImageBrush(asset) { Stretch = Stretch.Fill }; art.Freeze();
            return (art, DisplayArt.Brush(p.Bottom, height < 160 ? .08 : .16));
        }
        var image=p.Id == "bayi" ? Bayi.Value : p.Id == "custom" && customPath == Settings.CustomBackgroundPath ? custom : null;
        if (image == null) return (Preview(p), Brushes.Transparent);
        double cropWidth=image.PixelWidth, cropHeight=image.PixelHeight, aspect=Math.Max(1,width)/Math.Max(1,height);
        if(aspect<cropWidth/cropHeight)cropWidth=cropHeight*aspect;else cropHeight=cropWidth/aspect;
        bool builtin=p.Id=="bayi";
        double focusX=builtin?Settings.BayiFocusX:Settings.CustomFocusX, focusY=builtin?Settings.BayiFocusY:Settings.CustomFocusY;
        double left=Math.Clamp(image.PixelWidth*focusX-cropWidth*(builtin?wide?.76:.68:.5),0,image.PixelWidth-cropWidth);
        double top=Math.Clamp(image.PixelHeight*focusY-cropHeight*(builtin?.29:.5),0,image.PixelHeight-cropHeight);
        var photo=new ImageBrush(image) {ViewboxUnits=BrushMappingMode.RelativeToBoundingBox,Viewbox=new Rect(left/image.PixelWidth,top/image.PixelHeight,cropWidth/image.PixelWidth,cropHeight/image.PixelHeight),Stretch=Stretch.Fill};
        photo.Freeze();
        Color Shade(double a) { var c=(Color)ColorConverter.ConvertFromString(p.Bottom);c.A=(byte)Math.Round(Math.Clamp(a,0,1)*255);return c; }
        double dim=Settings.PhotoDim;
        return (photo,new LinearGradientBrush(new GradientStopCollection {new(Shade(dim+(wide?.23:.27)),0),new(Shade(dim+.10),.53),new(Shade(dim-(wide?0:.12)),1)},new Point(0,0),new Point(1,0)));
    }
    internal static void ApplySurface(Border surface, Border shade, bool wide)
    {
        surface.BorderBrush=Solid(Current.Edge);
        double radius=DisplayArt.Geek(Current.Id) || Current.Id is "lcd" or "cyber" or "mecha" or "matrix" or "arcade" or "eink" ? DisplayArt.Radius(Current) : wide?21:24;
        surface.CornerRadius=new CornerRadius(radius);shade.CornerRadius=new CornerRadius(Math.Max(0,radius-1));
        var brushes=Backdrop(Current,surface.ActualWidth>0?surface.ActualWidth:wide?944:388,surface.ActualHeight>0?surface.ActualHeight:wide?664:740,wide);
        surface.Background=brushes.Background;shade.Background=brushes.Shade;
    }

    private static BitmapSource LoadImage(Uri uri)
    {
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = uri; image.EndInit(); image.Freeze(); return image;
    }
}
