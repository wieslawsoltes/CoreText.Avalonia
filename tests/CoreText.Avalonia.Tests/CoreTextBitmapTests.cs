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

    private static int ReadPixel(IntPtr baseAddress, int stride, int x, int y)
    {
        var offset = ((y * stride) + x) * sizeof(int);
        return Marshal.ReadInt32(baseAddress, offset);
    }
}
