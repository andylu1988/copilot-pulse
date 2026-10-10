using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Threading;

namespace CopilotPulse;

public abstract class AnimatedDisplay : FrameworkElement
{
    private readonly DispatcherTimer animation = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double pulseStarted=-10;
    private bool subscribed;
    private string? previewSkin;
    public bool Preview { get; set; }
    public bool AnimatePreview { get; set; }
    public string? PreviewSkin { get=>previewSkin;set {previewSkin=value; RefreshAppearance();} }
    internal double? ReviewTime { get; set; }
    internal bool AnimationRunning => animation.IsEnabled;
    internal SkinPalette Palette => PreviewSkin == null ? SkinTheme.Current : SkinTheme.Find(PreviewSkin);
    protected virtual bool HasAmbient => DisplayArt.Ambient(Palette.Id);
    protected bool MotionAllowed => (!Preview || AnimatePreview) && !SkinTheme.Settings.ReduceMotion && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    protected double Phase => !MotionAllowed ? (Preview ? 1.5 : 0) : (App.Demo && ReviewTime.HasValue ? ReviewTime.Value : clock.Elapsed.TotalSeconds) * SkinTheme.Settings.EffectSpeed;
    protected double Pulse => MotionAllowed ? Math.Max(0,1-(clock.Elapsed.TotalSeconds-pulseStarted)/1.1) : 0;

    protected AnimatedDisplay()
    {
        animation.Tick += (_,_) => { if(!ShouldAnimate) animation.Stop(); else InvalidateVisual(); };
        Loaded += (_,_) => { if(!subscribed) { SystemParameters.StaticPropertyChanged += SystemChanged; subscribed=true; } RefreshAppearance(); };
        Unloaded += (_,_) => { animation.Stop(); if(subscribed) { SystemParameters.StaticPropertyChanged-=SystemChanged;subscribed=false; } };
        IsVisibleChanged += (_,_) => RefreshAppearance();
    }
    private bool ShouldAnimate => IsLoaded && IsVisible && MotionAllowed && (HasAmbient || Pulse>0);
    private void SystemChanged(object? sender,PropertyChangedEventArgs e) { if(e.PropertyName is nameof(SystemParameters.ClientAreaAnimation) or nameof(SystemParameters.HighContrast)) Dispatcher.Invoke(RefreshAppearance); }
    internal double AnimationIntervalMs => animation.Interval.TotalMilliseconds;
    internal void RefreshAppearance() { animation.Interval = TimeSpan.FromMilliseconds(1000.0 / SkinTheme.Settings.AnimationFps); if(ShouldAnimate)animation.Start();else animation.Stop(); InvalidateVisual(); }
    protected void Flash() { pulseStarted=clock.Elapsed.TotalSeconds;RefreshAppearance(); }
    internal void PaintReviewFrame(double time) { ReviewTime=time;InvalidateVisual(); }
}

public sealed class Atmosphere : AnimatedDisplay
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if(ActualWidth>0 && ActualHeight>0)DisplayArt.Texture(dc,new Rect(0,0,ActualWidth,ActualHeight),Palette,Phase,false);
    }
}

public sealed partial class MoneyReadout : AnimatedDisplay
{
    private decimal? amount;
    public bool Tile { get; set; }
    private string state="connecting";
    private DateTimeOffset? updated;
    protected override bool HasAmbient => Palette.Id == "nixie" || Tile && DisplayArt.Ambient(Palette.Id);
    internal decimal? Value => amount;
    internal void Update(decimal? value,string status,DateTimeOffset? time)
    {
        bool changed=amount!=value, different=changed||state!=status||updated!=time;
        if(changed && amount.HasValue && value.HasValue && Palette.Id!="eink")Flash();
        amount=value;state=status;updated=time;
        if(different || UiText.English)
        {
            string display=amount?.ToString("C2",DisplayArt.Us)??"尚未同步";
            AutomationProperties.SetName(this,UiText.Translate((Preview || App.Demo ? "示例金额 " : "本月内部用量 ")+display));
            if(Tile)ToolTip=UiText.Translate($"本月内部用量：{display}\n最近更新：{(time.HasValue?time.Value.LocalDateTime.ToString("MM/dd HH:mm:ss"):"尚无记录")}\n{StateLabel()}\n拖动移动 · 双击展开 · 右键设置");
            InvalidateVisual();
        }
    }
    private string StateLabel()=>UiText.Translate(state switch { "live"=>"已同步", "syncing"=>"正在同步", "demo"=>"示例", "paused"=>"已暂停", "login"=>"需要登录", "network" or "layout" or "cached" or "stale" or "runtime"=>"缓存 · 待更新", _=>"等待连接" });
    protected override AutomationPeer OnCreateAutomationPeer()=>new MoneyPeer(this);
    private sealed class MoneyPeer : FrameworkElementAutomationPeer { internal MoneyPeer(MoneyReadout owner):base(owner){} protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Text; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if(ActualWidth<5 || ActualHeight<5)return;
        var p=Palette;double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if(!Tile) { DisplayArt.Money(dc,new Rect(0,0,ActualWidth,ActualHeight),amount,p,Phase,Pulse,dpi); return; }
        dc.PushTransform(new ScaleTransform(ActualWidth/228,ActualHeight/96));
        var r=new Rect(1,1,226,94);var frame=DisplayArt.Frame(r,p);
        var background=SkinTheme.Backdrop(p,228,96,false);
        dc.DrawGeometry(background.Background,new Pen(DisplayArt.Brush(p.Edge),1),frame);
        dc.PushClip(frame);dc.DrawGeometry(background.Shade,null,frame);
        DisplayArt.Texture(dc,r,p,Phase,true);
        if (DisplayArt.Geek(p.Id)) { DrawGeekTile(dc, p, dpi); dc.Pop(); dc.Pop(); return; }
        if (DisplayArt.Advanced(p.Id))
        {
            var shade = new RadialGradientBrush(DisplayArt.Brush(p.Bottom, .50).Color, Colors.Transparent);
            dc.DrawEllipse(shade, null, new Point(114, 51), 121, 33);
        }
        if(p.Id=="matrix") { DrawMatrixInstrument(dc,p,dpi); dc.Pop();dc.Pop();return; }
        if(p.Id=="lcd")
        {
            dc.DrawRoundedRectangle(DisplayArt.Brush(p.Chart),new Pen(DisplayArt.Brush(p.Edge,.9),1),new Rect(10,24,208,53),4,4);
            foreach(var pt in new[]{new Point(6,6),new Point(222,6),new Point(6,90),new Point(222,90)})dc.DrawEllipse(DisplayArt.Brush(p.Text,.3),null,pt,1.6,1.6);
        }
        if(p.Id=="cyber")
        {
            dc.PushOpacity(DisplayArt.EffectOpacity);
            dc.DrawLine(new Pen(DisplayArt.Brush(p.Accent,.45),2),new Point(12,2),new Point(79,2));
            dc.DrawLine(new Pen(DisplayArt.Brush(p.Secondary,.8),2),new Point(153,94),new Point(214,94));
            double travel=14+(Phase%6)/6*180;
            dc.DrawLine(new Pen(DisplayArt.Brush(p.Accent,.7),2),new Point(travel,2),new Point(travel+19,2));
            dc.DrawLine(new Pen(DisplayArt.Brush(p.Secondary,.55),1),new Point(9,69),new Point(17,77));
            dc.Pop();
        }
        if(p.Id=="arcade")
        {
            dc.DrawRectangle(DisplayArt.Brush(p.Secondary),null,new Rect(5,5,22,2));
            dc.DrawRectangle(DisplayArt.Brush(p.Accent),null,new Rect(29,5,10,2));
        }
        if(Pulse>0)dc.DrawGeometry(null,new Pen(DisplayArt.Brush(p.Accent,Pulse*.35*DisplayArt.EffectOpacity),2),frame);
        string title=Preview?"PREVIEW / USD":p.Id=="terminal"?"COPILOT // USD":p.Id=="lcd"?"MONTH  ·  USD":"COPILOT  /  MTD USD";
        dc.DrawText(DisplayArt.Text(title,7.5,p.Faint,dpi,"Consolas"),new Point(14,10));
        string statusColor=state=="live"?p.Light?"#286C38":"#6FD8B2":state=="syncing"?p.Accent:p.Light?"#865321":"#D4B174";
        dc.DrawEllipse(DisplayArt.Brush(statusColor),null,new Point(212,14),2.1,2.1);
        DisplayArt.Money(dc,new Rect(15,29,198,43),amount,p,Phase,Pulse,dpi);
        string footer=Preview?"STYLE PREVIEW":state is "live" or "syncing"?"MONTH TO DATE":StateLabel();
        dc.DrawText(DisplayArt.Text(footer,7,p.Faint,dpi,"Segoe UI"),new Point(14,81));
        if(p.Id=="terminal" && MotionAllowed && Phase%1.8<.9)dc.DrawRectangle(DisplayArt.Brush(p.Accent,.7),null,new Rect(192,82,10,2));
        dc.Pop();dc.Pop();
    }
}
