using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CopilotPulse;
internal sealed partial class SettingsWindow
{
    private readonly Border skinSurface = new(), skinShade = new();
    private readonly Atmosphere atmosphere = new() { IsHitTestVisible = false };
    private readonly StackPanel navigation = new();
    private readonly ContentControl pageHost = new();
    private readonly TextBlock shellTitle = new(), saveStatus = new();
    private readonly Button shellClose = new(), shellDone = new();
    private readonly Dictionary<string, (Button Button, ScrollViewer View)> pages = new();
    private string selectedPage = "general";
    private void InitializeShell()
    {
        Width = Math.Min(880, SystemParameters.WorkArea.Width - 32);
        Height = Math.Min(720, SystemParameters.WorkArea.Height - 32);
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(ForegroundProperty, "SkinText"); SetResourceReference(FontFamilyProperty, "SkinFont");
        Resources.Add(typeof(Button), new Style(typeof(Button), (Style)FindResource("GlassButtonStyle")));
        Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), new Style(typeof(System.Windows.Controls.Primitives.ScrollBar), (Style)FindResource("GlassScrollBarStyle")));
        var frame = new Border { Margin = new Thickness(8), CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), ClipToBounds = true };
        frame.SetResourceReference(Border.BorderBrushProperty, "SkinEdge");
        var layers = new Grid(); frame.Child = layers; Content = frame;
        skinSurface.Child = skinShade; skinShade.Child = atmosphere; layers.Children.Add(skinSurface);
        layers.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(100, 6, 13, 20)), IsHitTestVisible = false });
        GlassChrome.SetSource(this, skinSurface);
        var root = new Grid { Margin = new Thickness(22, 18, 22, 18) }; layers.Children.Add(root);
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(new Image { Source = BrandIcon.Window, Width = 36, Height = 36, SnapsToDevicePixels = true, Margin = new Thickness(0, 0, 12, 0) });
        RenderOptions.SetBitmapScalingMode(brand.Children[0], BitmapScalingMode.HighQuality);
        var titles = new StackPanel(); titles.Children.Add(new TextBlock { Text = "COPILOT PULSE / SETTINGS", FontSize = 10 });
        shellTitle.FontSize = 24; shellTitle.FontWeight = FontWeights.SemiBold; titles.Children.Add(shellTitle); brand.Children.Add(titles); header.Children.Add(brand);
        shellClose.Content = "✕"; shellClose.Width = 36; shellClose.Height = 34; shellClose.HorizontalAlignment = HorizontalAlignment.Right; shellClose.VerticalAlignment = VerticalAlignment.Top;
        shellClose.Click += (_, _) => Close(); header.Children.Add(shellClose); root.Children.Add(header);
        var middle = new Grid(); middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(162) }); middle.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetRow(middle, 1); root.Children.Add(middle); navigation.Margin = new Thickness(0, 0, 18, 0); middle.Children.Add(navigation);
        var card = new Border { CornerRadius = new CornerRadius(14), BorderBrush = new SolidColorBrush(Color.FromArgb(65, 150, 194, 200)), BorderThickness = new Thickness(1), ClipToBounds = true };
        var cardLayers = new Grid(); cardLayers.Children.Add(new GlassBackdrop()); cardLayers.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(90, 8, 18, 25)) }); cardLayers.Children.Add(pageHost); card.Child = cardLayers;
        Grid.SetColumn(card, 1); middle.Children.Add(card);
        var footer = new Grid { Margin = new Thickness(0, 16, 0, 0), MinHeight = 38 };
        saveStatus.FontSize = 11; saveStatus.VerticalAlignment = VerticalAlignment.Center; saveStatus.TextWrapping = TextWrapping.Wrap; saveStatus.Margin = new Thickness(0, 0, 118, 0);
        saveStatus.SetResourceReference(TextBlock.ForegroundProperty, "SkinMuted"); footer.Children.Add(saveStatus);
        shellDone.MinWidth = 100; shellDone.Height = 36; shellDone.HorizontalAlignment = HorizontalAlignment.Right;
        shellDone.Click += (_, _) => Close(); footer.Children.Add(shellDone); Grid.SetRow(footer, 2); root.Children.Add(footer);
        frame.MouseLeftButtonDown += (_, e) => WindowDrag.TryMove(this, e);
        SizeChanged += (_, _) => ApplySkin(); ApplySkin();
    }
    internal void ApplySkin() { SkinTheme.ApplySurface(skinSurface, skinShade, true); atmosphere.RefreshAppearance(); }
    private void BeginPages()
    {
        pageHost.Content = null; pages.Clear(); navigation.Children.Clear();
        Title = UiText.T("Copilot Pulse · 设置", "Copilot Pulse · Settings");
        shellTitle.Text = UiText.T("偏好设置", "Preferences");
        shellClose.ToolTip = UiText.T("关闭", "Close"); shellDone.Content = UiText.T("完成", "Done");
        saveStatus.Text = UiText.T("设置自动保存 · 数值输入请点击对应的“应用”或“保存”", "Settings save automatically · Use Apply or Save for numeric inputs");
    }
    private void Page(string key, string zh, string en, string descriptionZh, string descriptionEn)
    {
        body = new StackPanel { Margin = new Thickness(22, 16, 22, 18) };
        var title = Label(UiText.T(zh, en), 22); title.FontWeight = FontWeights.SemiBold;
        var description = Label(UiText.T(descriptionZh, descriptionEn), 11); description.SetResourceReference(TextBlock.ForegroundProperty, "SkinMuted");
        var view = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var button = new Button { Content = UiText.T(zh, en), Height = 39, FontSize = 12, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(13, 7, 10, 7), Margin = new Thickness(0, 0, 0, 8) };
        button.Click += (_, _) => SelectPageForReview(key);
        pages.Add(key, (button, view)); navigation.Children.Add(button);
    }
    private void EndPages() => SelectPageForReview(pages.ContainsKey(selectedPage) ? selectedPage : "general");
    internal void SelectPageForReview(string key)
    {
        if (!pages.TryGetValue(key, out var page)) return;
        selectedPage = key; pageHost.Content = page.View;
        foreach (var item in pages)
        {
            item.Value.Button.FontWeight = item.Key == key ? FontWeights.SemiBold : FontWeights.Normal;
            item.Value.Button.SetResourceReference(Control.BackgroundProperty, item.Key == key ? "SkinHover" : "SkinPanel");
            item.Value.Button.SetResourceReference(Control.ForegroundProperty, item.Key == key ? "SkinAccent" : "SkinText");
        }
        page.View.ScrollToTop();
    }
    internal void VerifyFixedChromeForReview()
    {
        UpdateLayout();
        var before = shellClose.TranslatePoint(new Point(), this);
        var done = shellDone.TranslatePoint(new Point(), this);
        if (pageHost.Content is ScrollViewer scroll) scroll.ScrollToEnd();
        UpdateLayout();
        if (shellClose.TranslatePoint(new Point(), this) != before || shellDone.TranslatePoint(new Point(), this) != done || done.Y + shellDone.ActualHeight > ActualHeight || before.X < ActualWidth - 100)
            throw new InvalidOperationException("Settings close/footer are not fixed within window bounds");
        if (pageHost.Content is ScrollViewer top) top.ScrollToTop();
    }
}
