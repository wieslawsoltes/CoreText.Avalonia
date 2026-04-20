using Avalonia.Metal;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Metal;
using ObjCRuntime;

namespace CoreText.Avalonia;

internal sealed class CoreTextFramebufferRenderTarget : IRenderTarget
{
    private readonly CoreTextPlatformOptions _options;
    private IFramebufferRenderTarget? _renderTarget;

    public CoreTextFramebufferRenderTarget(CoreTextPlatformOptions options, IFramebufferPlatformSurface platformSurface)
    {
        _options = options;
        _renderTarget = platformSurface.CreateFramebufferRenderTarget();
    }

    public RenderTargetProperties Properties => new()
    {
        RetainsPreviousFrameContents = _renderTarget?.RetainsFrameContents == true,
        IsSuitableForDirectRendering = true
    };

    public PlatformRenderTargetState PlatformRenderTargetState => _renderTarget?.State ?? PlatformRenderTargetState.Disposed;

    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo sceneInfo, out RenderTargetDrawingContextProperties properties)
    {
        if (_renderTarget is null)
        {
            throw new ObjectDisposedException(nameof(CoreTextFramebufferRenderTarget));
        }

        var framebuffer = _renderTarget.Lock(sceneInfo, out var lockProperties);
        var bitmap = new CoreTextBitmapImpl(framebuffer.Address, framebuffer.Size, framebuffer.Dpi, framebuffer.RowBytes, framebuffer.Format, framebuffer.AlphaFormat, ownsBuffer: false);

        properties = new RenderTargetDrawingContextProperties
        {
            PreviousFrameIsRetained = lockProperties.PreviousFrameIsRetained
        };

        return new CoreTextDrawingContextImpl(bitmap, framebuffer.Dispose, bitmap.Dispose, scaleDrawingToDpi: false);
    }

    public void Dispose()
    {
        _renderTarget?.Dispose();
        _renderTarget = null;
    }
}

internal sealed class CoreTextMetalRenderTarget : IRenderTarget
{
    private readonly CoreTextPlatformOptions _options;
    private readonly IMetalDevice _device;
    private IMetalPlatformSurfaceRenderTarget? _target;

    public CoreTextMetalRenderTarget(CoreTextPlatformOptions options, IMetalPlatformSurface surface, IMetalDevice device)
    {
        _options = options;
        _device = device;
        _target = surface.CreateMetalRenderTarget(device);
    }

    public RenderTargetProperties Properties => new()
    {
        RetainsPreviousFrameContents = false,
        IsSuitableForDirectRendering = true
    };

    public PlatformRenderTargetState PlatformRenderTargetState => _target?.State ?? PlatformRenderTargetState.Disposed;

    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo sceneInfo, out RenderTargetDrawingContextProperties properties)
    {
        if (_target is null)
        {
            throw new ObjectDisposedException(nameof(CoreTextMetalRenderTarget));
        }

        var session = _target.BeginRendering();
        var dpi = new Vector(session.Scaling * 96, session.Scaling * 96);
        var bitmap = new CoreTextBitmapImpl(session.Size, dpi, PixelFormats.Bgra8888, AlphaFormat.Premul);

        properties = default;

        return new CoreTextDrawingContextImpl(
            bitmap,
            () =>
            {
                UploadBitmapToMetalTexture(bitmap, session);
                session.Dispose();
            },
            bitmap.Dispose,
            scaleDrawingToDpi: false);
    }

    public void Dispose()
    {
        _target?.Dispose();
        _target = null;
    }

    private static void UploadBitmapToMetalTexture(CoreTextBitmapImpl bitmap, IMetalPlatformSurfaceRenderingSession session)
    {
        var texture = Runtime.GetINativeObject<IMTLTexture>(session.Texture, false);
        if (texture is null)
        {
            return;
        }

        var region = new MTLRegion(new MTLOrigin(0, 0, 0), new MTLSize(bitmap.PixelSize.Width, bitmap.PixelSize.Height, 1));
        texture.ReplaceRegion(region, 0, bitmap.DataAddress, (nuint)bitmap.RowBytes);
    }
}
