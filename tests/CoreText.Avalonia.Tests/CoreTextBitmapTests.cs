using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;

namespace CoreText.Avalonia.Tests;

public sealed class CoreTextBitmapTests
{
    [Fact]
    public void CanCreateLockAndDrawIntoBitmapSurface()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(8, 6),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using var framebuffer = bitmap.Lock();
        Assert.Equal(8, framebuffer.Size.Width);
        Assert.Equal(6, framebuffer.Size.Height);
        Assert.Equal(32, framebuffer.RowBytes);

        using (var context = (CoreText.Avalonia.CoreTextDrawingContextImpl)bitmap.CreateDrawingContext())
        {
            context.Clear(Colors.CornflowerBlue);
        }

        Assert.True(bitmap.Version > 1);
    }

    [Fact]
    public void DrawRectangleUsesExpectedCoordinatesOnOffscreenBitmap()
    {
        AssertRectangleDraw(new PixelSize(128, 96), new Vector(96, 96), new Point(20, 16), new Point(40, 30), new Point(8, 8));
    }

    [Fact]
    public void DrawRectangleUsesExpectedCoordinatesOnRetinaOffscreenBitmap()
    {
        AssertRectangleDraw(new PixelSize(256, 192), new Vector(192, 192), new Point(40, 32), new Point(80, 60), new Point(8, 8));
    }

    [Fact]
    public void DrawRectangleUsesExpectedCoordinatesWithExplicitScaleTransform()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(256, 192),
            new Vector(192, 192),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.White);
            context.Transform = Matrix.CreateScale(2, 2);
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(16, 12, 32, 24)));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 40, 32)));
        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 80, 60)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 8, 8)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 24, 20)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 110, 80)));
    }

    [Fact]
    public void LargeRectangleUsesExpectedCoordinatesWithExplicitScaleTransform()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(2560, 1720),
            new Vector(192, 192),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.Transform = Matrix.CreateScale(2, 2);
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(32, 32, 1216, 796)));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 80, 80)));
        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 2000, 1400)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 40, 40)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 2500, 1600)));
    }

    [Fact]
    public void SequentialRectanglesUseExpectedCoordinatesWithExplicitScaleTransform()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(2560, 1720),
            new Vector(192, 192),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.Transform = Matrix.CreateScale(2, 2);
            context.DrawRectangle(new SolidColorBrush(Colors.White), null, new RoundedRect(new Rect(0, 0, 1280, 860)));
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(80, 80, 200, 120)));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 180, 180)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 120, 120)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 700, 500)));
    }

    [Fact]
    public void FilledAndStrokedRectangleKeepsExpectedBoundsWithExplicitScaleTransform()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(2560, 1720),
            new Vector(192, 192),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.Transform = Matrix.CreateScale(2, 2);
            context.DrawRectangle(
                new SolidColorBrush(Colors.White),
                new Pen(new SolidColorBrush(Color.Parse("#D8E1E8")), 1),
                new RoundedRect(new Rect(32, 32, 1216, 796)));
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(80, 80, 200, 120)));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.False(IsWhite(ReadPixel(framebuffer.Address, stride, 40, 40)));
        Assert.True(IsWhite(ReadPixel(framebuffer.Address, stride, 100, 100)));
        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 180, 180)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 700, 500)));
    }

    [Fact]
    public void ClippedRectangleKeepsExpectedBoundsWithExplicitScaleTransform()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(2560, 1720),
            new Vector(192, 192),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.Transform = Matrix.CreateScale(2, 2);
            context.PushClip(new Rect(0, 0, 1280, 860));
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(32, 32, 1216, 796)));
            context.PopClip();
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 80, 80)));
        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 2000, 1400)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 40, 40)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 2500, 1600)));
    }

    [Fact]
    public void RectClipInsideEffectLayerUsesLayerLocalCoordinates()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(320, 200),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.PushEffect(new Rect(96, 24, 128, 96), new BlurEffect { Radius = 0 });
            context.PushClip(new Rect(112, 40, 64, 40));
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(96, 24, 128, 96)));
            context.PopClip();
            context.PopEffect();
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 120, 48)));
        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 172, 72)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 104, 48)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 188, 88)));
    }

    [Fact]
    public void OpacityMaskModulatesRenderedAlpha()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(160, 80),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.PushOpacityMask(
                new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
                new Rect(20, 20, 120, 40));
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(20, 20, 120, 40)));
            context.PopOpacityMask();
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;
        var leftAlpha = GetAlpha(ReadPixel(framebuffer.Address, stride, 28, 40));
        var rightAlpha = GetAlpha(ReadPixel(framebuffer.Address, stride, 132, 40));

        Assert.InRange(leftAlpha, 96, 160);
        Assert.InRange(rightAlpha, 96, 160);
    }

    [Fact]
    public void SolidAlphaFillPreservesAlphaChannel()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(80, 60),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(128, 255, 0, 0)), null, new RoundedRect(new Rect(10, 10, 40, 20)));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;
        Assert.InRange(GetAlpha(ReadPixel(framebuffer.Address, stride, 20, 20)), 96, 160);
    }

    [Fact]
    public void CombinedGeometryExcludeRemovesOverlapFromFill()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(160, 120),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        var left = CoreText.Avalonia.CoreTextGeometryImpl.CreateRectangle(new Rect(20, 20, 90, 70));
        var right = CoreText.Avalonia.CoreTextGeometryImpl.CreateEllipse(new Rect(60, 20, 70, 70));
        var combined = CoreText.Avalonia.CoreTextGeometryImpl.CreateCombined(GeometryCombineMode.Exclude, left, right);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawGeometry(new SolidColorBrush(Colors.Red), null, combined);
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 30, 50)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 92, 55)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 140, 55)));
    }

    [Fact]
    public void CombinedGeometryClipRestrictsSubsequentDrawing()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(180, 140),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        var left = CoreText.Avalonia.CoreTextGeometryImpl.CreateRectangle(new Rect(20, 20, 100, 80));
        var right = CoreText.Avalonia.CoreTextGeometryImpl.CreateEllipse(new Rect(60, 20, 90, 80));
        var combined = CoreText.Avalonia.CoreTextGeometryImpl.CreateCombined(GeometryCombineMode.Intersect, left, right);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.PushGeometryClip(combined);
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(0, 0, 180, 140)));
            context.PopGeometryClip();
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 25, 60)));
        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 85, 60)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 155, 60)));
    }

    [Fact]
    public void StreamGeometryArcToProducesCurvedStroke()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(140, 120),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        var geometry = new CoreText.Avalonia.CoreTextStreamGeometryImpl();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(new Point(20, 60));
            stream.ArcTo(new Point(100, 60), new Size(40, 40), 0, isLargeArc: false, SweepDirection.Clockwise);
            stream.EndFigure(false);
        }

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.Red), 3), geometry);
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;
        var curvedPixels = 0;

        for (var y = 0; y < framebuffer.Size.Height; y++)
        {
            if (Math.Abs(y - 60) <= 3)
            {
                continue;
            }

            for (var x = 0; x < framebuffer.Size.Width; x++)
            {
                if (GetAlpha(ReadPixel(framebuffer.Address, stride, x, y)) > 20)
                {
                    curvedPixels++;
                }
            }
        }

        Assert.True(curvedPixels > 20);
    }

    [Fact]
    public void DrawBitmapHonorsSourceRect()
    {
        using var source = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(80, 40),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(source, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(0, 0, 40, 40)));
            context.DrawRectangle(new SolidColorBrush(Colors.Blue), null, new RoundedRect(new Rect(40, 0, 40, 40)));
        }

        using var target = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(60, 40),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(target, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawBitmap(source, 1, new Rect(40, 0, 40, 40), new Rect(10, 0, 40, 40));
        }

        using var framebuffer = target.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsBlue(ReadPixel(framebuffer.Address, stride, 24, 20)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 24, 20)));
        Assert.False(IsBlue(ReadPixel(framebuffer.Address, stride, 4, 20)));
    }

    [Fact]
    public void BlurEffectExpandsContentBeyondOriginalBounds()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(180, 120),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.PushEffect(null, new ImmutableBlurEffect(10));
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(70, 40, 40, 20)));
            context.PopEffect();
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(GetAlpha(ReadPixel(framebuffer.Address, stride, 64, 50)) > 0);
        Assert.True(GetAlpha(ReadPixel(framebuffer.Address, stride, 116, 50)) > 0);
        Assert.True(GetAlpha(ReadPixel(framebuffer.Address, stride, 84, 50)) > 80);
        Assert.Equal(0, GetAlpha(ReadPixel(framebuffer.Address, stride, 20, 20)));
    }

    [Fact]
    public void DropShadowEffectRendersShadowAndSource()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(180, 120),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.PushEffect(null, new ImmutableDropShadowEffect(18, 0, 0, Colors.Black, 1));
            context.DrawRectangle(Brushes.White, null, new RoundedRect(new Rect(24, 30, 36, 24)));
            context.PopEffect();
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsWhite(ReadPixel(framebuffer.Address, stride, 30, 40)));
        Assert.True(IsDark(ReadPixel(framebuffer.Address, stride, 70, 40)));
        Assert.False(IsWhite(ReadPixel(framebuffer.Address, stride, 70, 40)));
    }

    [Fact]
    public void ImageBrushRendersBitmapSource()
    {
        using var source = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(16, 16),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(source, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawRectangle(new SolidColorBrush(Colors.Blue), null, new RoundedRect(new Rect(0, 0, 8, 16)));
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(8, 0, 8, 16)));
        }

        using var sourceFramebuffer = source.Lock();
        AvaloniaLocator.CurrentMutable
            .Bind<IPlatformRenderInterface>()
            .ToConstant(new CoreText.Avalonia.CoreTextPlatformRenderInterface(new CoreTextPlatformOptions()));
        using var sourceBitmap = new global::Avalonia.Media.Imaging.Bitmap(
            sourceFramebuffer.Format,
            sourceFramebuffer.AlphaFormat,
            sourceFramebuffer.Address,
            sourceFramebuffer.Size,
            sourceFramebuffer.Dpi,
            sourceFramebuffer.RowBytes);
        using var target = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(16, 16),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(target, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawRectangle(new ImageBrush(sourceBitmap), null, new RoundedRect(new Rect(0, 0, 16, 16)));
        }

        using var framebuffer = target.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsBlue(ReadPixel(framebuffer.Address, stride, 3, 8)));
        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 12, 8)));
    }

    [Fact]
    public void RenderInterfaceContextReportsMaxOffscreenPixelSize()
    {
        using var context = new CoreText.Avalonia.CoreTextRenderInterfaceContext(new CoreTextPlatformOptions(), null);

        Assert.NotNull(context.MaxOffscreenRenderTargetPixelSize);
        Assert.True(context.MaxOffscreenRenderTargetPixelSize.Value.Width > 0);
        Assert.True(context.MaxOffscreenRenderTargetPixelSize.Value.Height > 0);
    }

    [Fact]
    public void OutsetBoxShadowRendersOutsideRectangle()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(140, 100),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawRectangle(
                Brushes.White,
                null,
                new RoundedRect(new Rect(20, 20, 40, 28)),
                new BoxShadows(new BoxShadow
                {
                    OffsetX = 12,
                    OffsetY = 0,
                    Color = Colors.Black
                }));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsWhite(ReadPixel(framebuffer.Address, stride, 30, 30)));
        Assert.True(GetAlpha(ReadPixel(framebuffer.Address, stride, 66, 30)) > 0);
        Assert.True(IsDark(ReadPixel(framebuffer.Address, stride, 66, 30)));
    }

    [Fact]
    public void BlurredOutsetBoxShadowDoesNotDarkenInterior()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(220, 160),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawRectangle(
                Brushes.White,
                null,
                new RoundedRect(new Rect(40, 30, 100, 70), 14, 14),
                new BoxShadows(new BoxShadow
                {
                    OffsetX = 0,
                    OffsetY = 10,
                    Blur = 16,
                    Color = Color.FromArgb(90, 20, 60, 90)
                }));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsWhite(ReadPixel(framebuffer.Address, stride, 90, 70)));
        Assert.True(GetAlpha(ReadPixel(framebuffer.Address, stride, 90, 120)) > 0);
        Assert.True(IsDark(ReadPixel(framebuffer.Address, stride, 90, 120)));
    }

    [Fact]
    public void InsetBoxShadowDarkensInteriorEdge()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(140, 100),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawRectangle(
                Brushes.White,
                null,
                new RoundedRect(new Rect(20, 20, 60, 40)),
                new BoxShadows(new BoxShadow
                {
                    OffsetX = 8,
                    OffsetY = 0,
                    Color = Colors.Black,
                    IsInset = true
                }));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.True(IsDark(ReadPixel(framebuffer.Address, stride, 24, 40)));
        Assert.True(IsWhite(ReadPixel(framebuffer.Address, stride, 70, 40)));
    }

    [Fact]
    public void PushOpacityIsolatesOverlappingContent()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(120, 80),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.PushOpacity(0.5, new Rect(10, 10, 80, 50));
            context.DrawRectangle(Brushes.Red, null, new RoundedRect(new Rect(10, 10, 50, 40)));
            context.DrawRectangle(Brushes.Blue, null, new RoundedRect(new Rect(40, 10, 50, 40)));
            context.PopOpacity();
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        var overlap = unchecked((uint)ReadPixel(framebuffer.Address, stride, 45, 25));
        var overlapAlpha = (byte)(overlap >> 24);

        Assert.InRange(overlapAlpha, 110, 145);
    }

    [Fact]
    public void ConicGradientBrushRendersDistinctAngularColors()
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(120, 120),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        var brush = new ConicGradientBrush
        {
            Center = RelativePoint.Center,
            Angle = 0
        };
        brush.GradientStops.Add(new GradientStop(Colors.Red, 0));
        brush.GradientStops.Add(new GradientStop(Colors.Lime, 0.25));
        brush.GradientStops.Add(new GradientStop(Colors.Blue, 0.5));
        brush.GradientStops.Add(new GradientStop(Colors.Yellow, 0.75));
        brush.GradientStops.Add(new GradientStop(Colors.Red, 1));

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawRectangle(brush, null, new RoundedRect(new Rect(10, 10, 100, 100)));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        var top = unchecked((uint)ReadPixel(framebuffer.Address, stride, 60, 18));
        var right = unchecked((uint)ReadPixel(framebuffer.Address, stride, 102, 60));
        var bottom = unchecked((uint)ReadPixel(framebuffer.Address, stride, 60, 102));

        Assert.True(((top >> 16) & 0xFF) > 180);
        Assert.True(((right >> 8) & 0xFF) > 150);
        Assert.True((bottom & 0xFF) > 150);
    }

    [Fact]
    public void DrawingBrushSceneContentHonorsStretchAndAlignment()
    {
        CoreText.Avalonia.CoreTextPlatform.Initialize();

        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(140, 120),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing
        {
            Brush = Brushes.Red,
            Geometry = new RectangleGeometry(new Rect(0, 0, 20, 20))
        });

        var brush = new DrawingBrush(drawing);

        using (var context = new CoreText.Avalonia.CoreTextDrawingContextImpl(bitmap, scaleDrawingToDpi: false))
        {
            context.Clear(Colors.Transparent);
            context.DrawRectangle(brush, null, new RoundedRect(new Rect(20, 20, 80, 60)));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 24, 30)));
        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 40, 30)));
        Assert.True(IsRed(ReadPixel(framebuffer.Address, stride, 80, 70)));
        Assert.False(IsRed(ReadPixel(framebuffer.Address, stride, 96, 70)));
    }

    private static void AssertRectangleDraw(PixelSize pixelSize, Vector dpi, Point inside1, Point inside2, Point outside)
    {
        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(pixelSize, dpi, PixelFormats.Bgra8888, AlphaFormat.Premul);

        using (var context = (CoreText.Avalonia.CoreTextDrawingContextImpl)bitmap.CreateDrawingContext())
        {
            context.Clear(Colors.White);
            context.DrawRectangle(new SolidColorBrush(Colors.Red), null, new RoundedRect(new Rect(16, 12, 32, 24)));
        }

        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;

        var topLeftInside = ReadPixel(framebuffer.Address, stride, (int)inside1.X, (int)inside1.Y);
        var bottomRightInside = ReadPixel(framebuffer.Address, stride, (int)inside2.X, (int)inside2.Y);
        var outsidePixel = ReadPixel(framebuffer.Address, stride, (int)outside.X, (int)outside.Y);

        Assert.True(IsRed(topLeftInside));
        Assert.True(IsRed(bottomRightInside));
        Assert.False(IsRed(outsidePixel));
    }

    private static bool IsRed(int pixel)
    {
        var value = unchecked((uint)pixel);
        var a = (byte)(value >> 24);
        var r = (byte)(value >> 16);
        var g = (byte)(value >> 8);
        var b = (byte)value;
        return a > 200 && r > 200 && g < 80 && b < 80;
    }

    private static bool IsWhite(int pixel)
    {
        var value = unchecked((uint)pixel);
        var a = (byte)(value >> 24);
        var r = (byte)(value >> 16);
        var g = (byte)(value >> 8);
        var b = (byte)value;
        return a > 200 && r > 240 && g > 240 && b > 240;
    }

    private static bool IsBlue(int pixel)
    {
        var value = unchecked((uint)pixel);
        var a = (byte)(value >> 24);
        var r = (byte)(value >> 16);
        var g = (byte)(value >> 8);
        var b = (byte)value;
        return a > 200 && r < 80 && g < 80 && b > 200;
    }

    private static bool IsDark(int pixel)
    {
        var value = unchecked((uint)pixel);
        var a = (byte)(value >> 24);
        var r = (byte)(value >> 16);
        var g = (byte)(value >> 8);
        var b = (byte)value;
        return a > 20 && r < 120 && g < 120 && b < 120;
    }

    private static byte GetAlpha(int pixel) => (byte)(unchecked((uint)pixel) >> 24);

    private static int ReadPixel(IntPtr baseAddress, int stride, int x, int y)
    {
        var offset = ((y * stride) + x) * sizeof(int);
        return Marshal.ReadInt32(baseAddress, offset);
    }
}
