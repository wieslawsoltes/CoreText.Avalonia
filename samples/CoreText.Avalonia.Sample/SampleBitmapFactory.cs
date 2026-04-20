using Avalonia.Media.Imaging;

namespace CoreText.Avalonia.Sample;

internal static class SampleBitmapFactory
{
    public static WriteableBitmap Create()
    {
        var bitmap = new WriteableBitmap(new PixelSize(320, 200), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        unsafe
        {
            using var locked = bitmap.Lock();
            var pixels = (uint*)locked.Address;

            for (var y = 0; y < locked.Size.Height; y++)
            {
                for (var x = 0; x < locked.Size.Width; x++)
                {
                    var r = (byte)(35 + (200 * x / Math.Max(1, locked.Size.Width - 1)));
                    var g = (byte)(80 + (120 * y / Math.Max(1, locked.Size.Height - 1)));
                    var b = (byte)(160 + (70 * ((x + y) % 32) / 31));
                    var a = (byte)255;
                    pixels[(y * locked.RowBytes / 4) + x] = (uint)(a << 24 | r << 16 | g << 8 | b);
                }
            }
        }

        return bitmap;
    }
}
