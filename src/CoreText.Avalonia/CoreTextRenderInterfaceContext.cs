using Avalonia.Metal;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;

namespace CoreText.Avalonia;

internal sealed class CoreTextRenderInterfaceContext : IPlatformRenderInterfaceContext
{
    private readonly CoreTextPlatformOptions _options;
    private readonly IMetalDevice? _metalDevice;

    public CoreTextRenderInterfaceContext(CoreTextPlatformOptions options, IMetalDevice? metalDevice)
    {
        _options = options;
        _metalDevice = metalDevice;
    }

    public IRenderTarget CreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        var available = surfaces.ToArray();

        var wantMetal = _options.SurfaceMode is CoreTextSurfaceMode.Metal or CoreTextSurfaceMode.Auto;

        if (wantMetal && _metalDevice is not null)
        {
            var metalSurface = available.OfType<IMetalPlatformSurface>().FirstOrDefault();
            if (metalSurface is not null)
            {
                return new CoreTextMetalRenderTarget(_options, metalSurface, _metalDevice);
            }
        }

        var framebufferSurface = available.OfType<IFramebufferPlatformSurface>().FirstOrDefault();
        if (framebufferSurface is not null)
        {
            return new CoreTextFramebufferRenderTarget(_options, framebufferSurface);
        }

        throw new NotSupportedException("No supported macOS render surface was provided.");
    }

    public IDrawingContextLayerImpl CreateOffscreenRenderTarget(PixelSize pixelSize, Vector scaling, bool enableTextAntialiasing) =>
        new CoreTextBitmapImpl(
            pixelSize,
            scaling * 96,
            PixelFormats.Bgra8888,
            AlphaFormat.Premul,
            scaleDrawingToDpiOnCreateDrawingContext: false);

    public bool IsLost => false;

    public IReadOnlyDictionary<Type, object> PublicFeatures { get; } = new Dictionary<Type, object>();

    public PixelSize? MaxOffscreenRenderTargetPixelSize => null;

    public bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces) => surfaces.Any(static s => s.IsReady);

    public object? TryGetFeature(Type featureType) => null;

    public void Dispose()
    {
    }
}
