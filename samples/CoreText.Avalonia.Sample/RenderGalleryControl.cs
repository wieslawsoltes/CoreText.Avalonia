using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace CoreText.Avalonia.Sample;

internal enum RenderGalleryScene
{
    BrushesAndImages,
    GeometryAndMasks,
    EffectsAndComposition,
    TextFormatting
}

internal sealed class RenderGalleryControl : Control
{
    private readonly WriteableBitmap _sampleBitmap;
    private readonly RenderGalleryScene _scene;

    public RenderGalleryControl(WriteableBitmap sampleBitmap, RenderGalleryScene scene)
    {
        _sampleBitmap = sampleBitmap;
        _scene = scene;
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var surfaceFill = new SolidColorBrush(Color.Parse("#EEF5F9"));
        var cardFill = Brushes.White;
        var panelStroke = new Pen(new SolidColorBrush(Color.Parse("#D9E4EE")), 1);
        var headingBrush = new SolidColorBrush(Color.Parse("#17324D"));
        var bodyBrush = new SolidColorBrush(Color.Parse("#5B7083"));
        var accentBrush = new SolidColorBrush(Color.Parse("#0E7490"));
        var warmBrush = new SolidColorBrush(Color.Parse("#F6C85F"));

        var bounds = new Rect(Bounds.Size);
        var surface = bounds.Deflate(1);
        context.FillRectangle(surfaceFill, bounds);
        context.DrawRectangle(cardFill, panelStroke, surface, 18);

        var inner = surface.Deflate(16);
        switch (_scene)
        {
            case RenderGalleryScene.BrushesAndImages:
                DrawBrushesAndImagesScene(context, inner, headingBrush, bodyBrush, accentBrush, panelStroke);
                break;
            case RenderGalleryScene.GeometryAndMasks:
                DrawGeometryAndMasksScene(context, inner, headingBrush, bodyBrush, accentBrush, panelStroke, warmBrush);
                break;
            case RenderGalleryScene.EffectsAndComposition:
                DrawEffectsAndCompositionScene(context, inner, headingBrush, bodyBrush, accentBrush, panelStroke);
                break;
            case RenderGalleryScene.TextFormatting:
                DrawTextFormattingScene(context, inner, headingBrush, bodyBrush, accentBrush, panelStroke);
                break;
        }
    }

    private void DrawBrushesAndImagesScene(
        DrawingContext context,
        Rect rect,
        IBrush headingBrush,
        IBrush bodyBrush,
        IBrush accentBrush,
        Pen panelStroke)
    {
        var panels = CreatePanelGrid(rect, 2, 2, 16);

        var solidContent = DrawPanelChrome(context, panels[0], "Solid fill", accentBrush, panelStroke);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#D7EAF4")), null, solidContent, 14, 14);

        var gradientContent = DrawPanelChrome(context, panels[1], "Gradient fill", accentBrush, panelStroke);
        context.DrawRectangle(
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#12344B"), 0.0),
                    new GradientStop(Color.Parse("#0E7490"), 0.55),
                    new GradientStop(Color.Parse("#7AA89A"), 1.0)
                }
            },
            null,
            gradientContent,
            14,
            14);

        var brushContent = DrawPanelChrome(context, panels[2], "Tiled image brush", accentBrush, panelStroke);
        context.DrawRectangle(
            new ImageBrush(_sampleBitmap)
            {
                TileMode = TileMode.FlipXY,
                Stretch = Stretch.UniformToFill,
                DestinationRect = new RelativeRect(new Rect(0, 0, 84, 84), RelativeUnit.Absolute)
            },
            null,
            brushContent,
            14,
            14);

        var bitmapContent = DrawPanelChrome(context, panels[3], "Bitmap blit", accentBrush, panelStroke);
        context.DrawRectangle(Brushes.White, panelStroke, bitmapContent, 14, 14);
        var imageRect = Inset(bitmapContent, 12);
        context.DrawImage(_sampleBitmap, new Rect(_sampleBitmap.Size), imageRect);
    }

    private void DrawGeometryAndMasksScene(
        DrawingContext context,
        Rect rect,
        IBrush headingBrush,
        IBrush bodyBrush,
        IBrush accentBrush,
        Pen panelStroke,
        IBrush warmBrush)
    {
        var panels = CreatePanelGrid(rect, 2, 2, 16);

        var ellipseContent = DrawPanelChrome(context, panels[0], "Stroke and fill", accentBrush, panelStroke);
        context.DrawEllipse(
            warmBrush,
            new Pen(new SolidColorBrush(Color.Parse("#17324D")), 4),
            ellipseContent);

        var geometryContent = DrawPanelChrome(context, panels[1], "Combined geometry", accentBrush, panelStroke);
        var combinedGeometry = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(Inset(geometryContent, 10)),
            new EllipseGeometry(new Rect(
                geometryContent.X + geometryContent.Width * 0.42,
                geometryContent.Y - 6,
                geometryContent.Width * 0.54,
                geometryContent.Height + 18)));
        context.DrawGeometry(new SolidColorBrush(Color.Parse("#0E7490")), null, combinedGeometry);

        var arcContent = DrawPanelChrome(context, panels[2], "Arc path", accentBrush, panelStroke);
        var arcStart = new Point(arcContent.X + 16, arcContent.Bottom - 18);
        var arcEnd = new Point(arcContent.Right - 16, arcContent.Bottom - 18);
        context.DrawGeometry(
            null,
            new Pen(new SolidColorBrush(Color.Parse("#17324D")), 5),
            CreateArcGeometry(arcStart, arcEnd));

        var maskContent = DrawPanelChrome(context, panels[3], "Opacity mask", accentBrush, panelStroke);
        using (context.PushOpacityMask(
                   new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
                   maskContent))
        {
            context.DrawRectangle(
                new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#17324D"), 0.0),
                        new GradientStop(Color.Parse("#0E7490"), 0.58),
                        new GradientStop(Color.Parse("#A7D4C8"), 1.0)
                    }
                },
                null,
                maskContent,
                14,
                14);
        }
    }

    private void DrawEffectsAndCompositionScene(
        DrawingContext context,
        Rect rect,
        IBrush headingBrush,
        IBrush bodyBrush,
        IBrush accentBrush,
        Pen panelStroke)
    {
        var panels = CreatePanelGrid(rect, 3, 1, 16);

        var shadowContent = DrawPanelChrome(context, panels[0], "Shadow card", accentBrush, panelStroke);
        var shadowCard = new Rect(shadowContent.X + 10, shadowContent.Y + 12, shadowContent.Width - 20, shadowContent.Height - 24);
        context.DrawRectangle(
            Brushes.White,
            null,
            shadowCard,
            18,
            18,
            new BoxShadows(
                new BoxShadow
                {
                    OffsetY = 14,
                    Blur = 18,
                    Color = Color.FromArgb(76, 17, 50, 77)
                },
                new[]
                {
                    new BoxShadow
                    {
                        OffsetX = 10,
                        OffsetY = 0,
                        Blur = 10,
                        Color = Color.FromArgb(42, 14, 116, 144),
                        IsInset = true
                    }
                }));
        DrawFormattedText(context, new Point(shadowCard.X + 18, shadowCard.Y + 18), "Outset + inset", 18, headingBrush, FontWeight.SemiBold);
        DrawFormattedText(context, new Point(shadowCard.X + 18, shadowCard.Y + 48), "One card, two shadow modes.", 14, bodyBrush);

        var blurContent = DrawPanelChrome(context, panels[1], "Soft light", accentBrush, panelStroke);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#17324D")), null, blurContent, 16, 16);
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(92, 55, 176, 255)),
            null,
            new Rect(blurContent.X + 18, blurContent.Y + 10, 92, 92));
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(88, 50, 224, 176)),
            null,
            new Rect(blurContent.Right - 118, blurContent.Bottom - 110, 96, 96));
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            null,
            new Rect(blurContent.X + 52, blurContent.Y + 38, 42, 42));
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(118, 255, 255, 255)),
            null,
            new Rect(blurContent.Right - 82, blurContent.Bottom - 74, 38, 38));
        DrawFormattedText(context, new Point(blurContent.X + 18, blurContent.Bottom - 42), "Layered light stays contained to the panel.", 14, Brushes.White);

        var opacityContent = DrawPanelChrome(context, panels[2], "Opacity stack", accentBrush, panelStroke);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#F3F7FA")), null, opacityContent, 16, 16);
        using (context.PushOpacity(0.72))
        {
            context.DrawRectangle(new SolidColorBrush(Color.Parse("#17324D")), null, new Rect(opacityContent.X + 16, opacityContent.Y + 18, 96, 96), 18, 18);
            context.DrawRectangle(new SolidColorBrush(Color.Parse("#0E7490")), null, new Rect(opacityContent.X + 68, opacityContent.Y + 44, 96, 96), 18, 18);
            context.DrawRectangle(new SolidColorBrush(Color.Parse("#8FD0C8")), null, new Rect(opacityContent.X + 38, opacityContent.Y + 96, 96, 64), 18, 18);
        }
        DrawFormattedText(context, new Point(opacityContent.X + 16, opacityContent.Bottom - 40), "Grouped alpha stays contained.", 14, bodyBrush);
    }

    private void DrawTextFormattingScene(
        DrawingContext context,
        Rect rect,
        IBrush headingBrush,
        IBrush bodyBrush,
        IBrush accentBrush,
        Pen panelStroke)
    {
        var bannerRect = new Rect(rect.X, rect.Y, rect.Width, 92);
        context.DrawRectangle(
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#12344B"), 0.0),
                    new GradientStop(Color.Parse("#0E7490"), 0.6),
                    new GradientStop(Color.Parse("#7AA89A"), 1.0)
                }
            },
            null,
            bannerRect,
            18,
            18);
        DrawFormattedText(context, new Point(bannerRect.X + 18, bannerRect.Y + 18), "FormattedText canvas", 24, Brushes.White, FontWeight.SemiBold);
        DrawFormattedText(context, new Point(bannerRect.X + 18, bannerRect.Y + 54), "Hierarchy, contrast, and utility labels are rendered through DrawText.", 14, Brushes.White);

        var lowerRect = new Rect(rect.X, bannerRect.Bottom + 16, rect.Width, rect.Bottom - bannerRect.Bottom - 16);
        var panels = CreatePanelGrid(lowerRect, 2, 1, 16);

        var hierarchyContent = DrawPanelChrome(context, panels[0], "Hierarchy", accentBrush, panelStroke);
        DrawFormattedText(context, new Point(hierarchyContent.X, hierarchyContent.Y), "Display heading", 26, headingBrush, FontWeight.SemiBold);
        DrawFormattedText(context, new Point(hierarchyContent.X, hierarchyContent.Y + 38), "Section label", 14, accentBrush, FontWeight.Medium);
        DrawFormattedText(context, new Point(hierarchyContent.X, hierarchyContent.Y + 64), "Body copy is kept dense but readable in the sample cards.", 15, bodyBrush);
        DrawFormattedText(context, new Point(hierarchyContent.X, hierarchyContent.Y + 92), "Italic emphasis", 17, accentBrush, FontWeight.Medium, FontStyle.Italic);
        DrawFormattedText(context, new Point(hierarchyContent.X, hierarchyContent.Y + 124), "12 pt utility text", 12, bodyBrush);

        var contrastContent = DrawPanelChrome(context, panels[1], "Alignment and accents", accentBrush, panelStroke);
        var noteRect = new Rect(contrastContent.X, contrastContent.Y, contrastContent.Width, 72);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#E7F3F5")), null, noteRect, 14, 14);
        DrawFormattedText(context, new Point(noteRect.X + 14, noteRect.Y + 16), "Accent note", 15, accentBrush, FontWeight.Medium);
        DrawFormattedText(context, new Point(noteRect.X + 14, noteRect.Y + 40), "FormattedText stays isolated from inline styling samples.", 13, bodyBrush);
        DrawFormattedText(context, new Point(contrastContent.Right - 118, contrastContent.Y + 104), "22 pt", 22, headingBrush, FontWeight.SemiBold);
        DrawFormattedText(context, new Point(contrastContent.Right - 112, contrastContent.Y + 140), "Semibold", 13, bodyBrush);
        DrawFormattedText(context, new Point(contrastContent.X, contrastContent.Y + 116), "15 pt body line", 15, bodyBrush);
        DrawFormattedText(context, new Point(contrastContent.X, contrastContent.Y + 144), "13 pt metadata", 13, accentBrush, FontWeight.Medium);
    }

    private static Rect[] CreatePanelGrid(Rect rect, int columns, int rows, double gap)
    {
        var panels = new Rect[columns * rows];
        var panelWidth = (rect.Width - ((columns - 1) * gap)) / columns;
        var panelHeight = (rect.Height - ((rows - 1) * gap)) / rows;

        var index = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                panels[index++] = new Rect(
                    rect.X + (column * (panelWidth + gap)),
                    rect.Y + (row * (panelHeight + gap)),
                    panelWidth,
                    panelHeight);
            }
        }

        return panels;
    }

    private static Rect DrawPanelChrome(
        DrawingContext context,
        Rect panelRect,
        string title,
        IBrush accentBrush,
        Pen panelStroke)
    {
        context.DrawRectangle(Brushes.White, panelStroke, panelRect, 16, 16);
        DrawFormattedText(context, new Point(panelRect.X + 14, panelRect.Y + 12), title, 13, accentBrush, FontWeight.Medium);
        return new Rect(panelRect.X + 14, panelRect.Y + 40, panelRect.Width - 28, panelRect.Height - 54);
    }

    private static Rect Inset(Rect rect, double amount) =>
        new(rect.X + amount, rect.Y + amount, Math.Max(1, rect.Width - (amount * 2)), Math.Max(1, rect.Height - (amount * 2)));

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
