using Avalonia.Metal;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;

namespace CoreText.Avalonia;

internal sealed class CoreTextRenderInterfaceContext : IPlatformRenderInterfaceContext
{
    private readonly CoreTextPlatformOptions _options;
    private readonly IMetalDevice? _metalDevice;
    private readonly CoreTextCoreImageContext _effectsContext;
    private readonly PixelSize _maxOffscreenRenderTargetPixelSize;
    private readonly IReadOnlyDictionary<Type, object> _publicFeatures;
    private readonly CoreTextExternalObjectsFeature? _externalObjectsFeature;

    public CoreTextRenderInterfaceContext(CoreTextPlatformOptions options, IMetalDevice? metalDevice)
    {
        _options = options;
        _metalDevice = metalDevice;
        _effectsContext = metalDevice is null
            ? CoreTextCoreImageContext.SharedSoftware
            : CoreTextCoreImageContext.ForMetalDevice(metalDevice);
        _maxOffscreenRenderTargetPixelSize = _effectsContext.MaxOutputPixelSize;
        _externalObjectsFeature = metalDevice is null ? null : new CoreTextExternalObjectsFeature(metalDevice);

        var features = new Dictionary<Type, object>();
        if (_externalObjectsFeature is not null)
        {
            features[typeof(IExternalObjectsRenderInterfaceContextFeature)] = _externalObjectsFeature;
            features[typeof(IExternalObjectsHandleWrapRenderInterfaceContextFeature)] = _externalObjectsFeature;
        }

        _publicFeatures = features;
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
            scaleDrawingToDpiOnCreateDrawingContext: false,
            enableFontSmoothing: _options.EnableFontSmoothing && enableTextAntialiasing,
            enableSubpixelPositioning: _options.EnableSubpixelPositioning && enableTextAntialiasing,
            enableEffects: _options.EnableCoreImageEffects,
            coreImageContext: _effectsContext);

    public bool IsLost => false;

    public IReadOnlyDictionary<Type, object> PublicFeatures => _publicFeatures;

    public PixelSize? MaxOffscreenRenderTargetPixelSize => _maxOffscreenRenderTargetPixelSize;

    public bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces) => surfaces.Any(static s => s.IsReady);

    public object? TryGetFeature(Type featureType)
    {
        if (_publicFeatures.TryGetValue(featureType, out var feature))
        {
            return feature;
        }

        return _metalDevice?.TryGetFeature(featureType);
    }

    public void Dispose()
    {
    }
}
