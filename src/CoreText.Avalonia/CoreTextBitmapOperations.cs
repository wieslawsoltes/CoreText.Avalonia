using Avalonia.Media;

namespace CoreText.Avalonia;

internal static class CoreTextBitmapOperations
{
    public static CoreTextBitmapImpl Clone(CoreTextBitmapImpl source)
    {
        var clone = new CoreTextBitmapImpl(
            source.PixelSize,
            source.Dpi,
            source.Format ?? PixelFormats.Bgra8888,
            source.AlphaFormat ?? AlphaFormat.Premul,
            scaleDrawingToDpiOnCreateDrawingContext: false,
            enableFontSmoothing: source.EnableFontSmoothing,
            enableSubpixelPositioning: source.EnableSubpixelPositioning,
            enableEffects: source.EnableEffects,
            coreImageContext: source.CoreImageContext);

        CopyPixels(source, clone);
        return clone;
    }

    public static void CopyPixels(CoreTextBitmapImpl source, CoreTextBitmapImpl destination)
    {
        ValidateCompatible(source, destination);

        var bytes = source.RowBytes * source.PixelSize.Height;
        unsafe
        {
            Buffer.MemoryCopy((void*)source.DataAddress, (void*)destination.DataAddress, bytes, bytes);
        }
    }

    public static void MultiplyAlphaInPlace(CoreTextBitmapImpl content, CoreTextBitmapImpl mask)
    {
        ValidateCompatible(content, mask);

        unsafe
        {
            for (var y = 0; y < content.PixelSize.Height; y++)
            {
                var contentRow = (uint*)(content.DataAddress + (y * content.RowBytes));
                var maskRow = (uint*)(mask.DataAddress + (y * mask.RowBytes));

                for (var x = 0; x < content.PixelSize.Width; x++)
                {
                    contentRow[x] = ScalePremultipliedPixel(contentRow[x], GetAlpha(maskRow[x]));
                }
            }
        }
    }

    public static void MultiplyAlphaInPlace(CoreTextBitmapImpl bitmap, double opacity)
    {
        var alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255), 0, 255);
        if (alpha == byte.MaxValue)
        {
            return;
        }

        unsafe
        {
            for (var y = 0; y < bitmap.PixelSize.Height; y++)
            {
                var row = (uint*)(bitmap.DataAddress + (y * bitmap.RowBytes));
                for (var x = 0; x < bitmap.PixelSize.Width; x++)
                {
                    row[x] = ScalePremultipliedPixel(row[x], alpha);
                }
            }
        }
    }

    public static void CompositeSourceOver(CoreTextBitmapImpl destination, CoreTextBitmapImpl source)
    {
        ValidateCompatible(destination, source);

        unsafe
        {
            for (var y = 0; y < destination.PixelSize.Height; y++)
            {
                var destinationRow = (uint*)(destination.DataAddress + (y * destination.RowBytes));
                var sourceRow = (uint*)(source.DataAddress + (y * source.RowBytes));

                for (var x = 0; x < destination.PixelSize.Width; x++)
                {
                    destinationRow[x] = CompositeSourceOver(destinationRow[x], sourceRow[x]);
                }
            }
        }
    }

    public static void CompositeSourceOver(CoreTextBitmapImpl destination, CoreTextBitmapImpl source, PixelPoint offset)
    {
        unsafe
        {
            for (var y = 0; y < source.PixelSize.Height; y++)
            {
                var destinationY = y + offset.Y;
                if ((uint)destinationY >= (uint)destination.PixelSize.Height)
                {
                    continue;
                }

                var sourceRow = (uint*)(source.DataAddress + (y * source.RowBytes));
                var destinationRow = (uint*)(destination.DataAddress + (destinationY * destination.RowBytes));

                for (var x = 0; x < source.PixelSize.Width; x++)
                {
                    var destinationX = x + offset.X;
                    if ((uint)destinationX >= (uint)destination.PixelSize.Width)
                    {
                        continue;
                    }

                    destinationRow[destinationX] = CompositeSourceOver(destinationRow[destinationX], sourceRow[x]);
                }
            }
        }
    }

    public static void BoxBlurInPlace(CoreTextBitmapImpl bitmap, int radius)
    {
        if (radius <= 0)
        {
            return;
        }

        using var scratch = new CoreTextBitmapImpl(
            bitmap.PixelSize,
            bitmap.Dpi,
            bitmap.Format ?? PixelFormats.Bgra8888,
            bitmap.AlphaFormat ?? AlphaFormat.Premul,
            scaleDrawingToDpiOnCreateDrawingContext: false,
            enableFontSmoothing: bitmap.EnableFontSmoothing,
            enableSubpixelPositioning: bitmap.EnableSubpixelPositioning,
            enableEffects: bitmap.EnableEffects,
            coreImageContext: bitmap.CoreImageContext);

        BoxBlurHorizontal(bitmap, scratch, radius);
        BoxBlurVertical(scratch, bitmap, radius);
    }

    public static CoreTextBitmapImpl CreateTintedFromAlphaMask(CoreTextBitmapImpl alphaSource, Color color, double opacity)
    {
        var tinted = new CoreTextBitmapImpl(
            alphaSource.PixelSize,
            alphaSource.Dpi,
            alphaSource.Format ?? PixelFormats.Bgra8888,
            alphaSource.AlphaFormat ?? AlphaFormat.Premul,
            scaleDrawingToDpiOnCreateDrawingContext: false,
            enableFontSmoothing: alphaSource.EnableFontSmoothing,
            enableSubpixelPositioning: alphaSource.EnableSubpixelPositioning,
            enableEffects: alphaSource.EnableEffects,
            coreImageContext: alphaSource.CoreImageContext);

        var alphaMultiplier = Math.Clamp(opacity, 0, 1) * (color.A / 255d);

        unsafe
        {
            for (var y = 0; y < alphaSource.PixelSize.Height; y++)
            {
                var sourceRow = (uint*)(alphaSource.DataAddress + (y * alphaSource.RowBytes));
                var tintedRow = (uint*)(tinted.DataAddress + (y * tinted.RowBytes));

                for (var x = 0; x < alphaSource.PixelSize.Width; x++)
                {
                    var alpha = (byte)Math.Clamp((int)Math.Round(GetAlpha(sourceRow[x]) * alphaMultiplier), 0, 255);
                    if (alpha == 0)
                    {
                        tintedRow[x] = 0;
                        continue;
                    }

                    var red = ScaleAlpha(color.R, alpha);
                    var green = ScaleAlpha(color.G, alpha);
                    var blue = ScaleAlpha(color.B, alpha);
                    tintedRow[x] = ((uint)alpha << 24) | ((uint)red << 16) | ((uint)green << 8) | blue;
                }
            }
        }

        return tinted;
    }

    public static void CombineMasksInPlace(CoreTextBitmapImpl leftMask, CoreTextBitmapImpl rightMask, GeometryCombineMode mode)
    {
        ValidateCompatible(leftMask, rightMask);

        unsafe
        {
            for (var y = 0; y < leftMask.PixelSize.Height; y++)
            {
                var leftRow = (uint*)(leftMask.DataAddress + (y * leftMask.RowBytes));
                var rightRow = (uint*)(rightMask.DataAddress + (y * rightMask.RowBytes));

                for (var x = 0; x < leftMask.PixelSize.Width; x++)
                {
                    var leftAlpha = GetAlpha(leftRow[x]);
                    var rightAlpha = GetAlpha(rightRow[x]);
                    var combinedAlpha = mode switch
                    {
                        GeometryCombineMode.Union => Math.Max(leftAlpha, rightAlpha),
                        GeometryCombineMode.Intersect => ScaleAlpha(leftAlpha, rightAlpha),
                        GeometryCombineMode.Exclude => ScaleAlpha(leftAlpha, (byte)(255 - rightAlpha)),
                        GeometryCombineMode.Xor => ClampAlpha(
                            ScaleAlpha(leftAlpha, (byte)(255 - rightAlpha)) +
                            ScaleAlpha(rightAlpha, (byte)(255 - leftAlpha))),
                        _ => leftAlpha
                    };

                    leftRow[x] = PackMaskPixel(combinedAlpha);
                }
            }
        }
    }

    private static void ValidateCompatible(CoreTextBitmapImpl left, CoreTextBitmapImpl right)
    {
        if (left.PixelSize != right.PixelSize || left.RowBytes != right.RowBytes)
        {
            throw new InvalidOperationException("Bitmaps must use the same layout for in-place composition.");
        }
    }

    private static uint ScalePremultipliedPixel(uint pixel, byte maskAlpha)
    {
        if (maskAlpha == byte.MaxValue)
        {
            return pixel;
        }

        if (maskAlpha == 0)
        {
            return 0;
        }

        var a = ScaleAlpha((byte)(pixel >> 24), maskAlpha);
        var r = ScaleAlpha((byte)(pixel >> 16), maskAlpha);
        var g = ScaleAlpha((byte)(pixel >> 8), maskAlpha);
        var b = ScaleAlpha((byte)pixel, maskAlpha);

        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static uint CompositeSourceOver(uint destinationPixel, uint sourcePixel)
    {
        var srcAlpha = (byte)(sourcePixel >> 24);
        if (srcAlpha == 0)
        {
            return destinationPixel;
        }

        if (srcAlpha == byte.MaxValue)
        {
            return sourcePixel;
        }

        var dstAlpha = (byte)(destinationPixel >> 24);
        var inverseSrcAlpha = (byte)(255 - srcAlpha);

        var outAlpha = ClampAlpha(srcAlpha + ScaleAlpha(dstAlpha, inverseSrcAlpha));
        var outRed = ClampAlpha((byte)(sourcePixel >> 16) + ScaleAlpha((byte)(destinationPixel >> 16), inverseSrcAlpha));
        var outGreen = ClampAlpha((byte)(sourcePixel >> 8) + ScaleAlpha((byte)(destinationPixel >> 8), inverseSrcAlpha));
        var outBlue = ClampAlpha((byte)sourcePixel + ScaleAlpha((byte)destinationPixel, inverseSrcAlpha));

        return ((uint)outAlpha << 24) | ((uint)outRed << 16) | ((uint)outGreen << 8) | outBlue;
    }

    private static uint PackMaskPixel(byte alpha) => ((uint)alpha << 24) | ((uint)alpha << 16) | ((uint)alpha << 8) | alpha;

    private static byte GetAlpha(uint pixel) => (byte)(pixel >> 24);

    private static byte ScaleAlpha(byte value, byte alpha) => (byte)((value * alpha + 127) / 255);

    private static byte ClampAlpha(int value) => (byte)Math.Clamp(value, 0, 255);

    private static void BoxBlurHorizontal(CoreTextBitmapImpl source, CoreTextBitmapImpl destination, int radius)
    {
        var width = source.PixelSize.Width;
        var height = source.PixelSize.Height;
        var windowSize = radius * 2 + 1;

        unsafe
        {
            for (var y = 0; y < height; y++)
            {
                var sourceRow = (uint*)(source.DataAddress + (y * source.RowBytes));
                var destinationRow = (uint*)(destination.DataAddress + (y * destination.RowBytes));

                var sumA = 0;
                var sumR = 0;
                var sumG = 0;
                var sumB = 0;

                for (var i = -radius; i <= radius; i++)
                {
                    var pixel = sourceRow[Math.Clamp(i, 0, width - 1)];
                    sumA += GetAlpha(pixel);
                    sumR += (byte)(pixel >> 16);
                    sumG += (byte)(pixel >> 8);
                    sumB += (byte)pixel;
                }

                for (var x = 0; x < width; x++)
                {
                    destinationRow[x] = PackPixel(sumA / windowSize, sumR / windowSize, sumG / windowSize, sumB / windowSize);

                    var removePixel = sourceRow[Math.Clamp(x - radius, 0, width - 1)];
                    var addPixel = sourceRow[Math.Clamp(x + radius + 1, 0, width - 1)];

                    sumA += GetAlpha(addPixel) - GetAlpha(removePixel);
                    sumR += (byte)(addPixel >> 16) - (byte)(removePixel >> 16);
                    sumG += (byte)(addPixel >> 8) - (byte)(removePixel >> 8);
                    sumB += (byte)addPixel - (byte)removePixel;
                }
            }
        }
    }

    private static void BoxBlurVertical(CoreTextBitmapImpl source, CoreTextBitmapImpl destination, int radius)
    {
        var width = source.PixelSize.Width;
        var height = source.PixelSize.Height;
        var windowSize = radius * 2 + 1;

        unsafe
        {
            for (var x = 0; x < width; x++)
            {
                var sumA = 0;
                var sumR = 0;
                var sumG = 0;
                var sumB = 0;

                for (var i = -radius; i <= radius; i++)
                {
                    var row = Math.Clamp(i, 0, height - 1);
                    var pixel = ((uint*)(source.DataAddress + (row * source.RowBytes)))[x];
                    sumA += GetAlpha(pixel);
                    sumR += (byte)(pixel >> 16);
                    sumG += (byte)(pixel >> 8);
                    sumB += (byte)pixel;
                }

                for (var y = 0; y < height; y++)
                {
                    var destinationRow = (uint*)(destination.DataAddress + (y * destination.RowBytes));
                    destinationRow[x] = PackPixel(sumA / windowSize, sumR / windowSize, sumG / windowSize, sumB / windowSize);

                    var removeRow = Math.Clamp(y - radius, 0, height - 1);
                    var addRow = Math.Clamp(y + radius + 1, 0, height - 1);
                    var removePixel = ((uint*)(source.DataAddress + (removeRow * source.RowBytes)))[x];
                    var addPixel = ((uint*)(source.DataAddress + (addRow * source.RowBytes)))[x];

                    sumA += GetAlpha(addPixel) - GetAlpha(removePixel);
                    sumR += (byte)(addPixel >> 16) - (byte)(removePixel >> 16);
                    sumG += (byte)(addPixel >> 8) - (byte)(removePixel >> 8);
                    sumB += (byte)addPixel - (byte)removePixel;
                }
            }
        }
    }

    private static uint PackPixel(int alpha, int red, int green, int blue) =>
        ((uint)ClampAlpha(alpha) << 24) |
        ((uint)ClampAlpha(red) << 16) |
        ((uint)ClampAlpha(green) << 8) |
        ClampAlpha(blue);
}
