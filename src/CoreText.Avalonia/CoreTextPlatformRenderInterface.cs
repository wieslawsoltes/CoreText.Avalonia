using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Metal;
using Avalonia.Platform;

namespace CoreText.Avalonia;

internal sealed class CoreTextPlatformRenderInterface : IPlatformRenderInterface
{
    private readonly CoreTextPlatformOptions _options;

    public CoreTextPlatformRenderInterface(CoreTextPlatformOptions options)
    {
        _options = options;
    }

    public bool SupportsIndividualRoundRects => true;

    public AlphaFormat DefaultAlphaFormat => AlphaFormat.Premul;

    public PixelFormat DefaultPixelFormat => PixelFormats.Bgra8888;

    public bool SupportsRegions => true;

    public bool IsSupportedBitmapPixelFormat(PixelFormat format) =>
        format == PixelFormats.Bgra8888 || format == PixelFormats.Rgba8888;

    public IPlatformRenderInterfaceRegion CreateRegion() => new CoreTextRegionImpl();

    public IGeometryImpl CreateEllipseGeometry(Rect rect) => CoreTextGeometryImpl.CreateEllipse(rect);

    public IGeometryImpl CreateLineGeometry(Point p1, Point p2) => CoreTextGeometryImpl.CreateLine(p1, p2);

    public IGeometryImpl CreateRectangleGeometry(Rect rect) => CoreTextGeometryImpl.CreateRectangle(rect);

    public IStreamGeometryImpl CreateStreamGeometry() => new CoreTextStreamGeometryImpl();

    public IGeometryImpl CreateGeometryGroup(FillRule fillRule, IReadOnlyList<IGeometryImpl> children) =>
        CoreTextGeometryImpl.CreateGroup(fillRule, children);

    public IGeometryImpl CreateCombinedGeometry(GeometryCombineMode combineMode, IGeometryImpl g1, IGeometryImpl g2) =>
        CoreTextGeometryImpl.CreateCombined(combineMode, g1, g2);

    public IGeometryImpl BuildGlyphRunGeometry(GlyphRun glyphRun) => CoreTextGeometryImpl.CreateGlyphRun(glyphRun);

    public IRenderTargetBitmapImpl CreateRenderTargetBitmap(PixelSize size, Vector dpi) =>
        new CoreTextBitmapImpl(
            size,
            dpi,
            PixelFormats.Bgra8888,
            AlphaFormat.Premul,
            enableFontSmoothing: _options.EnableFontSmoothing,
            enableSubpixelPositioning: _options.EnableSubpixelPositioning,
            enableEffects: _options.EnableCoreImageEffects);

    public IWriteableBitmapImpl CreateWriteableBitmap(PixelSize size, Vector dpi, PixelFormat format, AlphaFormat alphaFormat) =>
        new CoreTextBitmapImpl(
            size,
            dpi,
            format,
            alphaFormat,
            enableFontSmoothing: _options.EnableFontSmoothing,
            enableSubpixelPositioning: _options.EnableSubpixelPositioning,
            enableEffects: _options.EnableCoreImageEffects);

    public IBitmapImpl LoadBitmap(string fileName) => CoreTextBitmapImpl.Load(fileName);

    public IBitmapImpl LoadBitmap(Stream stream) => CoreTextBitmapImpl.Load(stream);

    public IWriteableBitmapImpl LoadWriteableBitmapToWidth(Stream stream, int width, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) =>
        CoreTextBitmapImpl.LoadResized(stream, width, resizeToWidth: true, interpolationMode);

    public IWriteableBitmapImpl LoadWriteableBitmapToHeight(Stream stream, int height, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) =>
        CoreTextBitmapImpl.LoadResized(stream, height, resizeToWidth: false, interpolationMode);

    public IWriteableBitmapImpl LoadWriteableBitmap(string fileName) => (IWriteableBitmapImpl)CoreTextBitmapImpl.Load(fileName);

    public IWriteableBitmapImpl LoadWriteableBitmap(Stream stream) => (IWriteableBitmapImpl)CoreTextBitmapImpl.Load(stream);

    public IBitmapImpl LoadBitmapToWidth(Stream stream, int width, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) =>
        CoreTextBitmapImpl.LoadResized(stream, width, resizeToWidth: true, interpolationMode);

    public IBitmapImpl LoadBitmapToHeight(Stream stream, int height, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) =>
        CoreTextBitmapImpl.LoadResized(stream, height, resizeToWidth: false, interpolationMode);

    public IBitmapImpl ResizeBitmap(IBitmapImpl bitmapImpl, PixelSize destinationSize, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) =>
        CoreTextBitmapImpl.Resize(bitmapImpl, destinationSize, interpolationMode);

    public IBitmapImpl LoadBitmap(PixelFormat format, AlphaFormat alphaFormat, IntPtr data, PixelSize size, Vector dpi, int stride) =>
        new CoreTextBitmapImpl(
            data,
            size,
            dpi,
            stride,
            format,
            alphaFormat,
            ownsBuffer: false,
            enableFontSmoothing: _options.EnableFontSmoothing,
            enableSubpixelPositioning: _options.EnableSubpixelPositioning,
            enableEffects: _options.EnableCoreImageEffects);

    public IGlyphRunImpl CreateGlyphRun(GlyphTypeface glyphTypeface, double fontRenderingEmSize, IReadOnlyList<GlyphInfo> glyphInfos, Point baselineOrigin) =>
        new CoreTextGlyphRunImpl(glyphTypeface, fontRenderingEmSize, glyphInfos, baselineOrigin);

    public IPlatformRenderInterfaceContext CreateBackendContext(IPlatformGraphicsContext? graphicsApiContext) =>
        new CoreTextRenderInterfaceContext(_options, graphicsApiContext as IMetalDevice);
}
