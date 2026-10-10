using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CopilotPulse;

public partial class SkinWindow : Window
{
    private readonly Preferences preferences;
    private readonly Action changed;
    private bool updating = true;
    private readonly List<(string Id, Border Border, TextBlock Check)> tiles = new();

    public SkinWindow(Preferences preferences, Action changed)
    {
        this.preferences = preferences;
        this.changed = changed;
        InitializeComponent();
        Icon = BrandIcon.Window;
        MaxHeight = SystemParameters.WorkArea.Height;
        BuildTiles();
        RefreshSelection();
        UiText.Apply(this);
        updating = false;
    }

    private void BuildTiles()
    {
        SkinTiles.Children.Clear(); tiles.Clear();
        foreach (var skin in SkinTheme.Catalog)
        {
            var preview = new MoneyReadout { Tile = true, Preview = true, PreviewSkin = skin.Id, Height = 90, IsHitTestVisible = false };
            preview.Update(1234.56m,"demo",null);
            var panel = new StackPanel(); panel.Children.Add(preview);
            var caption = new Grid { Margin = new Thickness(4, 8, 4, 0) };
            caption.Children.Add(new TextBlock { Text = skin.Name, FontSize = 12, FontWeight = FontWeights.SemiBold });
            var check = new TextBlock { Text = "✓", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Right };
            check.SetResourceReference(TextBlock.ForegroundProperty, "SkinAccent"); caption.Children.Add(check); panel.Children.Add(caption);
            var description = new TextBlock { Text = skin.Description, FontSize = 10, Margin = new Thickness(4, 4, 4, 0) };
            description.SetResourceReference(TextBlock.ForegroundProperty, "SkinFaint"); panel.Children.Add(description);
            var button = new Button { Content = panel, Padding = new Thickness(7), HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = "使用" + skin.Name };
            var border = new Border { Child = button, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(2), Margin = new Thickness(5), Padding = new Thickness(0, 0, 0, 2) };
            button.Click += (_, _) => SelectSkin(skin.Id);
            tiles.Add((skin.Id, border, check)); SkinTiles.Children.Add(border);
        }
    }

    internal void RefreshSelection()
    {
        updating = true;
        foreach(var button in new[]{MiniModeButton,CompactModeButton,FullModeButton}) button.SetResourceReference(Button.BackgroundProperty,(string)button.Tag==preferences.DisplayMode?"SkinHover":"SkinSurface");
        IntensitySlider.Value = preferences.EffectIntensity * 100;
        SpeedSlider.Value = preferences.EffectSpeed * 100;
        LivePreview.PreviewSkin = preferences.SkinId;
        LivePreview.Update(1234.56m, "demo", null);
        RefreshEffectsPreview();
        MotionButton.Content=preferences.ReduceMotion?"动态效果：关闭":"动态效果：开启";
        MotionNote.Text=!SystemParameters.ClientAreaAnimation?"已遵循 Windows 的减少动画设置":"隐藏到托盘自动停止特效";
        PhotoPanel.Visibility=SkinTheme.Current.Photo?Visibility.Visible:Visibility.Collapsed;
        foreach (var tile in tiles)
        {
            tile.Border.SetResourceReference(Border.BorderBrushProperty, tile.Id == preferences.SkinId ? "SkinAccent" : "SkinEdge");
            if (tile.Border.Child is Button tileButton && tileButton.Content is StackPanel tilePanel && tilePanel.Children[0] is MoneyReadout tilePreview) tilePreview.RefreshAppearance();
            tile.Check.Visibility = tile.Id == preferences.SkinId ? Visibility.Visible : Visibility.Collapsed;
        }
        DimSlider.Value = preferences.PhotoDim * 100;
        bool builtin = preferences.SkinId == "bayi";
        XSlider.Value = (builtin ? preferences.BayiFocusX : preferences.CustomFocusX) * 100;
        YSlider.Value = (builtin ? preferences.BayiFocusY : preferences.CustomFocusY) * 100;
        PhotoControls.IsEnabled = SkinTheme.Current.Photo && SkinTheme.HasImage;
        PhotoControls.Opacity = PhotoControls.IsEnabled ? 1 : 0.5;
        DimValue.Text = $"{preferences.PhotoDim:P0}";
        ImageName.Text = string.IsNullOrEmpty(preferences.CustomBackgroundPath) ? "也可以使用自己的图片" : "自定义图片已保存到本机";
        StatusText.Text = SkinTheme.ImageError ?? "选择会自动记住。关闭窗口即可继续使用。";
        UiText.Apply(this);
        updating = false;
    }

    private void SelectSkin(string id)
    {
        if (id == "custom" && !File.Exists(preferences.CustomBackgroundPath)) { ChooseImage(this, new RoutedEventArgs()); return; }
        preferences.SkinId = id;
        changed();
        BuildTiles(); RefreshSelection();
    }

    private void ChooseImage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = UiText.Translate("选择自定义皮肤图片"), Filter = UiText.Translate("背景图片") + "|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            string local = SkinImages.Import(dialog.FileName);
            preferences.CustomBackgroundPath = local;
            preferences.CustomFocusX = 0.5; preferences.CustomFocusY = 0.35;
            preferences.SkinId = "custom";
            changed(); BuildTiles(); RefreshSelection();
            StatusText.Text = UiText.Translate("图片已保存。可用下方滑块调整遮罩和取景。");
        }
        catch (Exception ex) when (SkinImages.IsImageError(ex)) { StatusText.Text = UiText.Translate("未能加载图片。请选择有效的 PNG、JPG 或 BMP，大小不超过 25 MB。"); }
    }

    private void AdjustPhoto(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (updating || DimSlider == null || XSlider == null || YSlider == null) return;
        preferences.PhotoDim = DimSlider.Value / 100;
        if (preferences.SkinId == "bayi") { preferences.BayiFocusX = XSlider.Value / 100; preferences.BayiFocusY = YSlider.Value / 100; }
        else { preferences.CustomFocusX = XSlider.Value / 100; preferences.CustomFocusY = YSlider.Value / 100; }
        DimValue.Text = $"{preferences.PhotoDim:P0}";
        changed();
    }

    private void AdjustEffects(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (updating || IntensitySlider == null || SpeedSlider == null) return;
        preferences.EffectIntensity = IntensitySlider.Value / 100;
        preferences.EffectSpeed = SpeedSlider.Value / 100;
        changed();
        RefreshEffectsPreview();
        foreach (var tile in tiles)
            if (tile.Border.Child is Button button && button.Content is StackPanel panel && panel.Children[0] is MoneyReadout preview) preview.RefreshAppearance();
    }

    private void RefreshEffectsPreview()
    {
        IntensityValue.Text = $"{preferences.EffectIntensity:P0}";
        SpeedValue.Text = $"{preferences.EffectSpeed:0.00}×";
        EffectsNote.Text = preferences.ReduceMotion || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast ? "动态效果已暂停，保留静态外观" : DisplayArt.Ambient(preferences.SkinId ?? "") ? "柔和 ← 强度 → 炫酷 · 自动保存" : "当前为静态皮肤，可选择上方动态皮肤";
        LivePreview.RefreshAppearance();
        UiText.Apply(this);
    }

    internal double PreviewAnimationIntervalMs => LivePreview.AnimationIntervalMs;
    internal bool PreviewAnimationRunning => LivePreview.AnimationRunning;
    internal void SetReviewEffects(double intensity, double speed) { IntensitySlider.Value = intensity * 100; SpeedSlider.Value = speed * 100; }
    internal void PaintPreviewFrame(double time) => LivePreview.PaintReviewFrame(time);

    private void ChangeDisplay(object sender,RoutedEventArgs e) { preferences.DisplayMode=(string)((Button)sender).Tag;changed();RefreshSelection(); }
    private void ToggleMotion(object sender,RoutedEventArgs e) { preferences.ReduceMotion=!preferences.ReduceMotion;changed();RefreshSelection(); }

    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        if (WindowDrag.TryMove(this, e)) {}
    }
    private void ClosePicker(object sender, RoutedEventArgs e) => Close();
}
