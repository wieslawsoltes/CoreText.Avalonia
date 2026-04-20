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
        var bitmapRect = new Rect(darkRect.X, swatchTop + 182, Math.Min(300, inner.Width - darkRect.X + inner.X), 176);
        var boxShadowRect = new Rect(inner.X, bitmapRect.Bottom + 18, 220, 66);

        DrawLabel(context, new Point(lightRect.X, lightRect.Y - 24), "Solid fill", accentBrush);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#D7EAF4")), lightRect);

        DrawLabel(context, new Point(darkRect.X, darkRect.Y - 24), "Tiled image brush", accentBrush);
        context.DrawRectangle(
            new ImageBrush(_sampleBitmap)
            {
                TileMode = TileMode.FlipXY,
                Stretch = Stretch.UniformToFill,
                DestinationRect = new RelativeRect(new Rect(0, 0, 84, 84), RelativeUnit.Absolute)
            },
            null,
            darkRect,
            16,
            16);

        DrawLabel(context, new Point(ellipseRect.X, ellipseRect.Y - 24), "Stroke and fill", accentBrush);
        context.DrawEllipse(
            warmBrush,
            new Pen(new SolidColorBrush(Color.Parse("#17324D")), 4),
            ellipseRect);

        DrawLabel(context, new Point(bitmapRect.X, bitmapRect.Y - 24), "Bitmap blit", accentBrush);
        context.DrawImage(_sampleBitmap, new Rect(_sampleBitmap.Size), bitmapRect);

        DrawLabel(context, new Point(boxShadowRect.X, boxShadowRect.Y - 24), "Box shadows", accentBrush);
        context.DrawRectangle(
            Brushes.White,
            new Pen(new SolidColorBrush(Color.Parse("#D8E1E8")), 1),
            boxShadowRect,
            18,
            18,
            new BoxShadows(
                new BoxShadow
                {
                    OffsetY = 10,
                    Blur = 12,
                    Spread = 0,
                    Color = Color.FromArgb(70, 17, 50, 77)
                },
                new[]
                {
                    new BoxShadow
                    {
                        OffsetX = 8,
                        OffsetY = 0,
                        Blur = 8,
                        Color = Color.FromArgb(40, 14, 116, 144),
                        IsInset = true
                    }
                }));
        DrawFormattedText(context, new Point(boxShadowRect.X + 18, boxShadowRect.Y + 22), "Outset + inset shadows", 17, headingBrush, FontWeight.SemiBold);
        DrawFormattedText(context, new Point(boxShadowRect.X + 18, boxShadowRect.Y + 46), "This rounded panel is drawn with BoxShadows.", 14, bodyBrush);

        var combinedGeometry = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(darkRect.X + 12, darkRect.Bottom + 32, 120, 56)),
            new EllipseGeometry(new Rect(darkRect.X + 70, darkRect.Bottom + 18, 110, 76)));
        var arcStart = new Point(lightRect.X + 18, ellipseRect.Bottom + 18);
        var arcEnd = new Point(lightRect.X + 104, ellipseRect.Bottom + 18);
        var arcGeometry = CreateArcGeometry(arcStart, arcEnd);
        var maskedRect = new Rect(darkRect.X, bitmapRect.Bottom + 48, Math.Min(300, inner.Width - darkRect.X + inner.X), 72);

        DrawLabel(context, new Point(darkRect.X, darkRect.Bottom - 6), "Combined geometry", accentBrush);
        context.DrawGeometry(new SolidColorBrush(Color.Parse("#0E7490")), null, combinedGeometry);

        DrawLabel(context, new Point(lightRect.X, ellipseRect.Bottom - 8), "Arc geometry", accentBrush);
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#17324D")), 5), arcGeometry);

        DrawLabel(context, new Point(maskedRect.X, maskedRect.Y - 24), "Opacity-masked fill", accentBrush);
        using (context.PushOpacityMask(
                   new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
                   maskedRect))
        {
            context.DrawRectangle(
                new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#12344B"), 0.0),
                        new GradientStop(Color.Parse("#0E7490"), 0.58),
                        new GradientStop(Color.Parse("#7AA89A"), 1.0)
                    }
                },
                null,
                maskedRect,
                18,
                18);
        }

        var textPanelTop = Math.Max(Math.Max(maskedRect.Bottom, ellipseRect.Bottom), boxShadowRect.Bottom) + 34;
        var textPanelHeight = Math.Max(0, inner.Bottom - textPanelTop);
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
        DrawFormattedText(context, new Point(textPanel.X + 24, textPanel.Y + 104), "Combined geometry, arc paths, tiled image brushes, and opacity masks are exercised here.", 17, Brushes.White);
        DrawFormattedText(context, new Point(textPanel.X + 24, textPanel.Y + 140), "The rounded card above uses real BoxShadows while the right panel applies a visual effect.", 17, Brushes.White);
        DrawFormattedText(context, new Point(textPanel.X + 24, textPanel.Y + 176), "This panel exists to prove custom drawing and normal Avalonia controls render through the same backend.", 17, Brushes.White);
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

    private static StreamGeometry CreateArcGeometry(Point startPoint, Point endPoint)
    {
        var geometry = new StreamGeometry();
        using var stream = geometry.Open();
        stream.BeginFigure(startPoint);
        stream.ArcTo(endPoint, new Size((endPoint.X - startPoint.X) / 2, 44), 0, false, SweepDirection.Clockwise);
        stream.EndFigure(false);
        return geometry;
    }
}
