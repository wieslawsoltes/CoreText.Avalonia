using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace CoreText.Avalonia.Sample;

internal sealed class RenderGalleryControl : Control
{
    private readonly WriteableBitmap _sampleBitmap;

    public RenderGalleryControl(WriteableBitmap sampleBitmap)
    {
        _sampleBitmap = sampleBitmap;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var background = new SolidColorBrush(Color.Parse("#F3F7FA"));
        var panelStroke = new Pen(new SolidColorBrush(Color.Parse("#D8E1E8")), 1);
        var headingBrush = new SolidColorBrush(Color.Parse("#17324D"));
        var bodyBrush = new SolidColorBrush(Color.Parse("#5B7083"));
        var accentBrush = new SolidColorBrush(Color.Parse("#0E7490"));
        var warmBrush = new SolidColorBrush(Color.Parse("#F6C85F"));
        var whiteBrush = Brushes.White;

        var bounds = new Rect(Bounds.Size);
        var canvas = bounds.Deflate(24);
        var inner = canvas.Deflate(20);

        context.FillRectangle(background, bounds);
        context.DrawRectangle(whiteBrush, panelStroke, canvas, 18);

        DrawFormattedText(context, inner.Position, "FormattedText preview", 28, headingBrush, FontWeight.SemiBold);
        DrawFormattedText(context, new Point(inner.X, inner.Y + 44), "This canvas renders text through the normal Avalonia formatter.", 15, bodyBrush);
        DrawFormattedText(context, new Point(inner.X, inner.Y + 66), "It now exercises FormattedText instead of the glyph-run-only sample path.", 15, bodyBrush);

        var swatchTop = inner.Y + 122;
        var lightRect = new Rect(inner.X, swatchTop, Math.Min(210, inner.Width * 0.30), 118);
        var darkRect = new Rect(lightRect.Right + 36, swatchTop, Math.Min(260, inner.Width * 0.38), 138);
        var ellipseSize = Math.Min(180, inner.Width * 0.24);
        var ellipseRect = new Rect(inner.X, swatchTop + 168, ellipseSize, ellipseSize);
        var bitmapRect = new Rect(darkRect.X, swatchTop + 188, Math.Min(320, inner.Width - darkRect.X + inner.X), 200);

        DrawLabel(context, new Point(lightRect.X, lightRect.Y - 24), "Solid fill", accentBrush);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#D7EAF4")), lightRect);

        DrawLabel(context, new Point(darkRect.X, darkRect.Y - 24), "Opaque block", accentBrush);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#1F6D92")), darkRect);

        DrawLabel(context, new Point(ellipseRect.X, ellipseRect.Y - 24), "Stroke and fill", accentBrush);
        context.DrawEllipse(
            warmBrush,
            new Pen(new SolidColorBrush(Color.Parse("#17324D")), 4),
            ellipseRect);

        DrawLabel(context, new Point(bitmapRect.X, bitmapRect.Y - 24), "Bitmap blit", accentBrush);
        context.DrawImage(_sampleBitmap, new Rect(_sampleBitmap.Size), bitmapRect);

        var textPanelTop = Math.Max(ellipseRect.Bottom, bitmapRect.Bottom) + 34;
        var textPanelHeight = Math.Max(220, inner.Bottom - textPanelTop);
        var textPanel = new Rect(inner.X, textPanelTop, inner.Width, textPanelHeight);

        context.DrawRectangle(
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#12344B"), 0.0),
                    new GradientStop(Color.Parse("#0E7490"), 0.62),
                    new GradientStop(Color.Parse("#7AA89A"), 1.0)
                }
            },
            null,
            textPanel,
            26,
            26);

        DrawFormattedText(context, new Point(textPanel.X + 24, textPanel.Y + 22), "Formatted text overlay", 22, Brushes.White, FontWeight.SemiBold);
        DrawFormattedText(context, new Point(textPanel.X + 24, textPanel.Y + 60), "Rich text is demonstrated with real TextBlock inlines on the right.", 17, Brushes.White);
        DrawFormattedText(context, new Point(textPanel.X + 24, textPanel.Y + 104), "Buttons, check boxes, radio buttons, and toggles now use standard control text.", 17, Brushes.White);
        DrawFormattedText(context, new Point(textPanel.X + 24, textPanel.Y + 140), "This panel exists to prove FormattedText also renders through the backend.", 17, Brushes.White);
    }

    private static void DrawLabel(DrawingContext context, Point origin, string text, IBrush brush)
    {
        DrawFormattedText(context, origin, text, 13, brush, FontWeight.Medium);
    }

    private static void DrawFormattedText(
        DrawingContext context,
        Point origin,
        string text,
        double fontSize,
        IBrush foreground,
        FontWeight weight = FontWeight.Normal,
        FontStyle style = FontStyle.Normal)
    {
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Inter"), style, weight),
            fontSize,
            foreground);

        context.DrawText(formattedText, origin);
    }
}
