using System.Windows.Forms;
using System.Drawing;

namespace Connect4;

public class CustomTabControl : TabControl
{
    public CustomTabControl()
    {
        // Enable user painting so OnPaint is triggered
        SetStyle(ControlStyles.UserPaint, true);
        UpdateStyles();
        DrawMode = TabDrawMode.OwnerDrawFixed;
        BackColor = Color.Black;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Fill the entire control with blue (this includes the area behind tabs)
        using (var brush = new SolidBrush(Color.Black))
        {
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        // Manually draw each tab header by calling your custom draw method.
        for (int i = 0; i < TabCount; i++)
        {
            Rectangle tabRect = GetTabRect(i);
            var drawArgs = new DrawItemEventArgs(
                e.Graphics,
                TabPages[i].Font,
                tabRect,
                i,
                DrawItemState.Default,
                Color.White,
                Color.Black);

            CustomTabControl_DrawItem(this, drawArgs);
        }
    }

    private void CustomTabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        TabPage tabPage = TabPages[e.Index];

        // Draw tab header background
        using (var backBrush = new SolidBrush(Color.Black))
        {
            e.Graphics.FillRectangle(backBrush, e.Bounds);
        }

        // Draw tab header text in a contrasting color
        TextRenderer.DrawText(
            e.Graphics,
            tabPage.Text,
            tabPage.Font,
            e.Bounds,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
        );
    }
}