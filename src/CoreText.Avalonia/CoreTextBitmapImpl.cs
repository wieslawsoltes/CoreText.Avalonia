using Avalonia.Media;
using Avalonia.Platform;
using Foundation;
using ImageIO;
using ObjCRuntime;

namespace CoreText.Avalonia;

internal sealed class CoreTextBitmapImpl : IRenderTargetBitmapImpl, IWriteableBitmapImpl, IDrawingContextLayerImpl
{
    private readonly bool _ownsBuffer;
    private readonly bool _ownsContext;
    private readonly bool _scaleDrawingToDpiOnCreateDrawingContext;
    private readonly bool _enableFontSmoothing;
    private readonly bool _enableSubpixelPositioning;
    private readonly bool _enableEffects;
    private IntPtr _data;
    private IntPtr _context;

    public CoreTextBitmapImpl(
        PixelSize pixelSize,
        Vector dpi,
        PixelFormat format,
        AlphaFormat alphaFormat,
        bool scaleDrawingToDpiOnCreateDrawingContext = true,
        bool enableFontSmoothing = true,
        bool enableSubpixelPositioning = true,
        bool enableEffects = true,
        CoreTextCoreImageContext? coreImageContext = null,
        global::IOSurface.IOSurface? ioSurface = null)
    {
        if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelSize));
        }

        PixelSize = pixelSize;
        Dpi = dpi;
        Format = format;
        AlphaFormat = alphaFormat;
        _scaleDrawingToDpiOnCreateDrawingContext = scaleDrawingToDpiOnCreateDrawingContext;
        _enableFontSmoothing = enableFontSmoothing;
        _enableSubpixelPositioning = enableSubpixelPositioning;
        _enableEffects = enableEffects;
        CoreImageContext = coreImageContext;
        Surface = ioSurface;
        RowBytes = pixelSize.Width * 4;
        _data = Marshal.AllocHGlobal(RowBytes * pixelSize.Height);
        unsafe
        {
            new Span<byte>((void*)_data, RowBytes * pixelSize.Height).Clear();
        }
        _ownsBuffer = true;
        _context = CoreTextNative.CreateBitmapContext(_data, pixelSize.Width, pixelSize.Height, RowBytes);
        _ownsContext = true;
    }

    public CoreTextBitmapImpl(
        IntPtr data,
        PixelSize pixelSize,
        Vector dpi,
        int rowBytes,
        PixelFormat format,
        AlphaFormat alphaFormat,
        bool ownsBuffer,
        bool scaleDrawingToDpiOnCreateDrawingContext = true,
        bool enableFontSmoothing = true,
        bool enableSubpixelPositioning = true,
        bool enableEffects = true,
        CoreTextCoreImageContext? coreImageContext = null,
        global::IOSurface.IOSurface? ioSurface = null)
    {
        PixelSize = pixelSize;
        Dpi = dpi;
        RowBytes = rowBytes;
        Format = format;
        AlphaFormat = alphaFormat;
        _scaleDrawingToDpiOnCreateDrawingContext = scaleDrawingToDpiOnCreateDrawingContext;
        _enableFontSmoothing = enableFontSmoothing;
        _enableSubpixelPositioning = enableSubpixelPositioning;
        _enableEffects = enableEffects;
        CoreImageContext = coreImageContext;
        Surface = ioSurface;
        _data = data;
        _ownsBuffer = ownsBuffer;
        _context = CoreTextNative.CreateBitmapContext(_data, pixelSize.Width, pixelSize.Height, rowBytes);
        _ownsContext = true;
    }

    public Vector Dpi { get; }

    public PixelSize PixelSize { get; }

    public int Version { get; private set; } = 1;

    public int RowBytes { get; }

    public PixelFormat? Format { get; }

    public AlphaFormat? AlphaFormat { get; }

    public bool CanBlit => true;

    public bool IsCorrupted => false;

    public IntPtr DataAddress => _data;

    public IntPtr ContextHandle => _context;

    internal bool EnableFontSmoothing => _enableFontSmoothing;

    internal bool EnableSubpixelPositioning => _enableSubpixelPositioning;

    internal bool EnableEffects => _enableEffects;

    internal CoreTextCoreImageContext? CoreImageContext { get; }

    internal global::IOSurface.IOSurface? Surface { get; }

    public ILockedFramebuffer Lock() => new CoreTextLockedFramebuffer(
        _data,
        PixelSize,
        RowBytes,
        Dpi,
        Format ?? PixelFormats.Bgra8888,
        AlphaFormat ?? global::Avalonia.Platform.AlphaFormat.Premul);

    public IDrawingContextImpl CreateDrawingContext() =>
        new CoreTextDrawingContextImpl(
            this,
            disposeAction: IncrementVersion,
            scaleDrawingToDpi: _scaleDrawingToDpiOnCreateDrawingContext,
            enableFontSmoothing: _enableFontSmoothing,
            enableSubpixelPositioning: _enableSubpixelPositioning,
            enableEffects: _enableEffects);

    public void Save(string fileName, int? quality = null)
    {
        using var destination = CGImageDestination.Create(NSUrl.FromFilename(fileName), GuessUti(fileName), 1);
        if (destination is null)
        {
            throw new InvalidOperationException("Unable to create image destination.");
        }

        using var image = CreateCGImage();
        var options = new CGImageDestinationOptions();
        if (quality.HasValue)
        {
            options.LossyCompressionQuality = quality.Value / 100f;
        }

        destination.AddImage(image, options);
        destination.Close();
    }

    public void Save(Stream stream, int? quality = null)
    {
        using var data = NSMutableData.FromCapacity(0);
        using var destination = CGImageDestination.Create(data, "public.png", 1, new CGImageDestinationOptions());
        if (destination is null)
        {
            throw new InvalidOperationException("Unable to create image destination.");
        }

        using var image = CreateCGImage();
        var options = new CGImageDestinationOptions();
        if (quality.HasValue)
        {
            options.LossyCompressionQuality = quality.Value / 100f;
        }

        destination.AddImage(image, options);
        destination.Close();
        data.AsStream().CopyTo(stream);
    }

    public void Blit(IDrawingContextImpl context)
    {
        if (context is CoreTextDrawingContextImpl drawingContext)
        {
            var target = drawingContext.Bitmap;
            if (target.PixelSize == PixelSize &&
                target.RowBytes == RowBytes &&
                target.Format == Format &&
                target.AlphaFormat == AlphaFormat)
            {
                var bytes = RowBytes * PixelSize.Height;
                unsafe
                {
                    Buffer.MemoryCopy((void*)_data, (void*)target.DataAddress, bytes, bytes);
                }

                return;
            }

            drawingContext.DrawBitmap(this, 1, new Rect(PixelSize.ToSize(1)), new Rect(PixelSize.ToSize(1)));
        }
    }

    public void Dispose()
    {
        if (_ownsContext && _context != IntPtr.Zero)
        {
            CoreTextNative.CGContextRelease(_context);
            _context = IntPtr.Zero;
        }

        if (_ownsBuffer && _data != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_data);
            _data = IntPtr.Zero;
        }
    }

    public void IncrementVersion() => Version++;

    internal void ClearPixels()
    {
        unsafe
        {
            new Span<byte>((void*)_data, RowBytes * PixelSize.Height).Clear();
        }
    }

    public CGImage CreateCGImage()
    {
        var handle = CreateCGImageHandle();
        return Runtime.GetINativeObject<CGImage>(handle, true)!;
    }

    public CGImage CreateCGImage(Rect sourceRect)
    {
        var handle = CreateCGImageHandle(sourceRect);
        return Runtime.GetINativeObject<CGImage>(handle, true)!;
    }

    public IntPtr CreateCGImageHandle()
    {
        return CoreTextNative.CGBitmapContextCreateImage(_context);
    }

    public IntPtr CreateCGImageHandle(Rect sourceRect)
    {
        var logicalWidth = PixelSize.Width / Math.Max(Dpi.X / 96d, 0.0001);
        var logicalHeight = PixelSize.Height / Math.Max(Dpi.Y / 96d, 0.0001);
        var clamped = sourceRect.Intersect(new Rect(0, 0, logicalWidth, logicalHeight));
        if (clamped.Width <= 0 || clamped.Height <= 0)
        {
            return CreateCGImageHandle();
        }

        var imageHandle = CreateCGImageHandle();
        var scaleX = PixelSize.Width / logicalWidth;
        var scaleY = PixelSize.Height / logicalHeight;
        var cropRect = new CoreTextNative.CGRect(
            clamped.X * scaleX,
            clamped.Y * scaleY,
            clamped.Width * scaleX,
            clamped.Height * scaleY);
        var croppedHandle = CoreTextNative.CGImageCreateWithImageInRect(imageHandle, cropRect);
        if (croppedHandle == IntPtr.Zero)
        {
            return imageHandle;
        }

        CoreTextNative.CFRelease(imageHandle);
        return croppedHandle;
    }

    public static CoreTextBitmapImpl Load(string fileName)
    {
        using var data = NSData.FromFile(fileName) ?? throw new FileNotFoundException("Unable to read image file.", fileName);
        return Load(data);
    }

    public static CoreTextBitmapImpl Load(Stream stream)
    {
        using var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("Unable to read image stream.");
        return Load(data);
    }

    public static CoreTextBitmapImpl LoadResized(Stream stream, int dimension, bool resizeToWidth, BitmapInterpolationMode interpolationMode)
    {
        using var source = Load(stream);
        var aspect = source.PixelSize.Width / (double)source.PixelSize.Height;
        var size = resizeToWidth
            ? new PixelSize(dimension, Math.Max(1, (int)Math.Round(dimension / aspect)))
            : new PixelSize(Math.Max(1, (int)Math.Round(dimension * aspect)), dimension);
        return (CoreTextBitmapImpl)Resize(source, size, interpolationMode);
    }

    public static IBitmapImpl Resize(IBitmapImpl bitmapImpl, PixelSize destinationSize, BitmapInterpolationMode interpolationMode)
    {
        if (bitmapImpl is not CoreTextBitmapImpl source)
        {
            throw new NotSupportedException("Bitmap resize currently supports CoreText bitmaps only.");
        }

        var resized = new CoreTextBitmapImpl(
            destinationSize,
            source.Dpi,
            source.Format ?? PixelFormats.Bgra8888,
            source.AlphaFormat ?? global::Avalonia.Platform.AlphaFormat.Premul,
            enableFontSmoothing: source.EnableFontSmoothing,
            enableSubpixelPositioning: source.EnableSubpixelPositioning,
            enableEffects: source.EnableEffects,
            coreImageContext: source.CoreImageContext);

        using var sourceImage = source.CreateCGImage();
        CoreTextNative.CGContextSaveGState(resized.ContextHandle);
        CoreTextNative.CGContextTranslateCTM(resized.ContextHandle, 0, destinationSize.Height);
        CoreTextNative.CGContextScaleCTM(resized.ContextHandle, 1, -1);
        CoreTextNative.CGContextDrawImage(resized.ContextHandle, new CoreTextNative.CGRect(0, 0, destinationSize.Width, destinationSize.Height), sourceImage.Handle);
        CoreTextNative.CGContextRestoreGState(resized.ContextHandle);
        return resized;
    }

    private static CoreTextBitmapImpl Load(NSData data)
    {
        using var source = CGImageSource.FromData(data) ?? throw new InvalidOperationException("Unable to decode image.");
        using var image = source.CreateImage(0, new CGImageOptions()) ?? throw new InvalidOperationException("Unable to decode image frame.");
        var bitmap = new CoreTextBitmapImpl(
            new PixelSize((int)image.Width, (int)image.Height),
            CoreTextPlatform.DefaultDpi,
            PixelFormats.Bgra8888,
            global::Avalonia.Platform.AlphaFormat.Premul);

        CoreTextNative.CGContextSaveGState(bitmap.ContextHandle);
        CoreTextNative.CGContextTranslateCTM(bitmap.ContextHandle, 0, bitmap.PixelSize.Height);
        CoreTextNative.CGContextScaleCTM(bitmap.ContextHandle, 1, -1);
        CoreTextNative.CGContextDrawImage(bitmap.ContextHandle, new CoreTextNative.CGRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), image.Handle);
        CoreTextNative.CGContextRestoreGState(bitmap.ContextHandle);

        return bitmap;
    }

    private static string GuessUti(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "public.jpeg",
            ".tif" or ".tiff" => "public.tiff",
            _ => "public.png"
        };
    }

    private sealed class CoreTextLockedFramebuffer : ILockedFramebuffer
    {
        public CoreTextLockedFramebuffer(IntPtr address, PixelSize size, int rowBytes, Vector dpi, PixelFormat format, AlphaFormat alphaFormat)
        {
            Address = address;
            Size = size;
            RowBytes = rowBytes;
            Dpi = dpi;
            Format = format;
            AlphaFormat = alphaFormat;
        }

        public IntPtr Address { get; }

        public PixelSize Size { get; }

        public int RowBytes { get; }

        public Vector Dpi { get; }

        public PixelFormat Format { get; }

        public AlphaFormat AlphaFormat { get; }

        public void Dispose()
        {
        }
    }
}
