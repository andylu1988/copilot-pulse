using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CopilotPulse;

internal static class WindowDrag
{
    internal static bool CanDrag(DependencyObject? node)
    {
        while (node != null)
        {
            if (node is ButtonBase or TextBoxBase or PasswordBox or Selector or RangeBase or Thumb or HistoryChart or MenuItem or HyperlinkHost) return false;
            node = node is Visual or Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        return true;
    }
    internal static bool TryMove(Window window, MouseButtonEventArgs e)
    {
        if (e.Handled || e.LeftButton != MouseButtonState.Pressed || !CanDrag(e.OriginalSource as DependencyObject)) return false;
        e.Handled = true;
        window.DragMove();
        return true;
    }
}
internal sealed class HyperlinkHost : TextBlock { }
