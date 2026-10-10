using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CopilotPulse;

public partial class MainWindow
{
    private string appliedMode="";
    private void InitializeDisplayMode()
    {
        preferences.DisplayMode=DisplayModes.Normalize(preferences.DisplayMode,preferences.Compact);
        if(App.MiniAtStartup)preferences.DisplayMode="mini";
        ApplyDisplayMode();
        RefreshNumericState();
    }
    private void ApplyDisplayMode()
    {
        string mode=DisplayModes.Normalize(preferences.DisplayMode,preferences.Compact);
        if(appliedMode==mode)return;
        appliedMode=mode;preferences.DisplayMode=mode;
        if(mode!="mini")preferences.Compact=mode=="compact";
        Width=mode=="mini"?244:408;
        FullView.Visibility=mode=="mini"?Visibility.Collapsed:Visibility.Visible;
        MiniHost.Visibility=mode=="mini"?Visibility.Visible:Visibility.Collapsed;
        DetailsPanel.Visibility=mode=="compact"?Visibility.Collapsed:Visibility.Visible;
        MiniTools.Visibility=Visibility.Collapsed;
        RefreshDisplayEffects();
        if(IsLoaded) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(KeepOnCurrentScreen));
    }
    private void KeepOnCurrentScreen()
    {
        if(closing || !IsLoaded || !IsVisible)return;
        var area=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;
        var origin=PointToScreen(new Point(0,0));var dpi=VisualTreeHelper.GetDpi(this);
        double width=ActualWidth*dpi.DpiScaleX,height=ActualHeight*dpi.DpiScaleY;
        double x=origin.X<area.Left?area.Left-origin.X:origin.X+width>area.Right?area.Right-origin.X-width:0;
        double y=origin.Y<area.Top?area.Top-origin.Y:origin.Y+height>area.Bottom?area.Bottom-origin.Y-height:0;
        if(x!=0)Left+=x/dpi.DpiScaleX;if(y!=0)Top+=y/dpi.DpiScaleY;
        SavePreferences();
    }
    private void SetDisplayMode(string mode)
    {
        preferences.DisplayMode=DisplayModes.Normalize(mode,preferences.Compact);
        ApplyDisplayMode();SavePreferences();skinWindow?.RefreshSelection();
    }
    private void EnterMini(object sender,RoutedEventArgs e)=>SetDisplayMode("mini");
    private void ExpandMini(object sender,RoutedEventArgs e)=>SetDisplayMode("full");
    private MenuItem BuildDisplayModeMenu()
    {
        var menu=new MenuItem {Header=UiText.Translate("显示模式")};
        foreach(string mode in new[]{"mini","compact","full"})
        {
            var item=new MenuItem {Header=UiText.Translate(DisplayModes.Label(mode)),IsCheckable=true,IsChecked=preferences.DisplayMode==mode};
            item.Click+=(_,_)=>SetDisplayMode(mode);menu.Items.Add(item);
        }
        return menu;
    }
    private void RefreshNumericState()
    {
        string effective=paused?"paused":status;
        if(effective=="live" && data!=null && DateTimeOffset.Now-data.FetchedAt>TimeSpan.FromSeconds(Math.Max(180,preferences.RefreshSeconds*3+30)))effective="stale";
        FullAmount.Update(data?.Total,effective,data?.FetchedAt);
        MiniDisplay.Update(data?.Total,effective,data?.FetchedAt);
    }
    private void RefreshDisplayEffects()
    {
        FullAmount.RefreshAppearance();MiniDisplay.RefreshAppearance();FullAtmosphere.RefreshAppearance();
    }
    private void MiniMouseDown(object sender,MouseButtonEventArgs e)
    {
        DependencyObject? node=e.OriginalSource as DependencyObject;
        while(node!=null) {if(node is Button)return;node=VisualTreeHelper.GetParent(node);}
        if(e.ClickCount==2) {SetDisplayMode("full");e.Handled=true;return;}
        if(e.LeftButton==MouseButtonState.Pressed) {DragMove();SavePreferences();}
    }
    private void MiniContextMenu(object sender,MouseButtonEventArgs e)
    {
        var menu=BuildSettingsMenu();menu.PlacementTarget=MiniHost;menu.Placement=PlacementMode.MousePoint;menu.IsOpen=true;e.Handled=true;
    }
    private void MiniEnter(object sender,MouseEventArgs e)
    {
        MiniTools.Visibility=Visibility.Visible;
        MiniTools.BeginAnimation(OpacityProperty,null);
        if(preferences.ReduceMotion||!SystemParameters.ClientAreaAnimation)MiniTools.Opacity=1;
        else { var fade = new DoubleAnimation(1,TimeSpan.FromMilliseconds(150)); Timeline.SetDesiredFrameRate(fade, preferences.AnimationFps); MiniTools.BeginAnimation(OpacityProperty,fade); }
    }
    private void MiniLeave(object sender,MouseEventArgs e)
    {
        double opacity=MiniTools.Opacity;
        MiniTools.BeginAnimation(OpacityProperty,null); MiniTools.Opacity=opacity;
        if(preferences.ReduceMotion||!SystemParameters.ClientAreaAnimation){MiniTools.Visibility=Visibility.Collapsed;return;}
        var fade=new DoubleAnimation(0,TimeSpan.FromMilliseconds(140));
        Timeline.SetDesiredFrameRate(fade, preferences.AnimationFps);
        fade.Completed+=(_,_)=>{if(!MiniHost.IsMouseOver)MiniTools.Visibility=Visibility.Collapsed;};
        MiniTools.BeginAnimation(OpacityProperty,fade);
    }
}
