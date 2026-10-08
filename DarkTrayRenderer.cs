using System.Drawing;
using System.Windows.Forms;

namespace CopilotPulse;

internal sealed class DarkTrayRenderer : ToolStripProfessionalRenderer
{
    internal static Color Surface => ColorTranslator.FromHtml(SkinTheme.Current.Panel);
    internal static Color Text => ColorTranslator.FromHtml(SkinTheme.Current.Text);
    private static Color Edge => ColorTranslator.FromHtml(SkinTheme.Current.Edge);
    private static Color Hover => ColorTranslator.FromHtml(SkinTheme.Current.Hover);
    private static Color Muted => ColorTranslator.FromHtml(SkinTheme.Current.Faint);

    public DarkTrayRenderer() : base(new Colors()) { RoundedEdges = false; }
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Surface);
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Edge);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        using var brush = new SolidBrush(e.Item.Enabled && e.Item.Selected ? Hover : Surface);
        e.Graphics.FillRectangle(brush, new Rectangle(4, 0, e.Item.Width - 8, e.Item.Height));
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Text : Muted;
        TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, e.TextColor, e.TextFormat);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == false ? Muted : Text;
        base.OnRenderArrow(e);
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(Edge);
        e.Graphics.DrawLine(pen, 12, e.Item.Height / 2, e.Item.Width - 12, e.Item.Height / 2);
    }
    private sealed class Colors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuBorder => Edge;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color MenuItemBorder => Hover;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
    }
}
