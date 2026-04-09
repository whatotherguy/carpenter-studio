#if WINDOWS
using System.Windows;
using System.Windows.Media;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering.Layers;

public sealed class CabinetLayer : IRenderLayer
{
    private static readonly Brush NormalFill = CreateBrush(233, 214, 179);
    private static readonly Brush HoverFill = CreateBrush(210, 227, 242);
    private static readonly Brush SelectedFill = CreateBrush(188, 221, 255);
    private static readonly Brush InvalidFill = CreateBrush(244, 204, 204);
    private static readonly Brush GhostFill = CreateGhostBrush();
    private static readonly Typeface LabelTypeface = new("Segoe UI");
    private const double LabelFontSize = 10.0;
    private static readonly Brush LabelBrush = Brushes.Black;
    private static readonly Brush GhostLabelBrush = CreateGhostLabelBrush();
    private static readonly Pen NormalPen = CreatePen(Brushes.SaddleBrown, 1d);
    private static readonly Pen HighlightPen = CreatePen(Brushes.DodgerBlue, 2d);
    private static readonly Pen InvalidPen = CreatePen(Brushes.Firebrick, 2d);

    public void Draw(DrawingContext drawingContext, RenderSceneDto scene, ViewportTransform viewport)
    {
        for (var i = 0; i < scene.Cabinets.Count; i++)
        {
            var cabinet = scene.Cabinets[i];
            var rect = ScreenProjection.ToScreen(cabinet.WorldBounds, viewport);
            var brush = GetFill(cabinet.State);
            var pen = GetPen(cabinet.State);
            drawingContext.DrawRectangle(brush, pen, new Rect(rect.X, rect.Y, rect.Width, rect.Height));

            var labelText = string.IsNullOrWhiteSpace(cabinet.Label)
                ? cabinet.TypeDisplayName
                : cabinet.Label;
            if (string.IsNullOrWhiteSpace(labelText))
            {
                continue;
            }

            var displayLabel = rect.Width < 12d
                ? string.Empty
                : rect.Width < 30d
                    ? labelText[..Math.Min(2, labelText.Length)]
                    : labelText;
            if (string.IsNullOrWhiteSpace(displayLabel))
            {
                continue;
            }

            var scaledFontSize = Math.Clamp(
                LabelFontSize * (ScreenProjection.PixelsPerInch(viewport) / 10.0),
                7.0,
                24.0);
            var textBrush = cabinet.State == CabinetRenderState.Ghost ? GhostLabelBrush : LabelBrush;
            // pixelsPerDip = 1.0 assumes 96 DPI; revisit if high-DPI scaling is needed.
            var formattedText = new FormattedText(
                displayLabel,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                scaledFontSize,
                textBrush,
                1.0);

            if (formattedText.Height <= rect.Height - 4d && formattedText.Width <= rect.Width - 4d)
            {
                var textX = rect.X + (rect.Width - formattedText.Width) / 2.0;
                var textY = rect.Y + (rect.Height - formattedText.Height) / 2.0;
                drawingContext.DrawText(formattedText, new Point(textX, textY));
            }
        }
    }

    private static Brush GetFill(CabinetRenderState state) => state switch
    {
        CabinetRenderState.Hovered => HoverFill,
        CabinetRenderState.Selected => SelectedFill,
        CabinetRenderState.Invalid => InvalidFill,
        CabinetRenderState.Ghost => GhostFill,
        _ => NormalFill
    };

    private static Pen GetPen(CabinetRenderState state) => state switch
    {
        CabinetRenderState.Hovered => HighlightPen,
        CabinetRenderState.Selected => HighlightPen,
        CabinetRenderState.Invalid => InvalidPen,
        _ => NormalPen
    };

    private static Brush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateGhostBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(128, 210, 210, 210));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateGhostLabelBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(180, 80, 80, 80));
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
#endif
