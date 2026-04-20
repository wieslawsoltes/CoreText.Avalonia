using Avalonia.Metal;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using IOSurface;
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
        var bitmap = new CoreTextBitmapImpl(
            framebuffer.Address,
            framebuffer.Size,
            framebuffer.Dpi,
            framebuffer.RowBytes,
            framebuffer.Format,
            framebuffer.AlphaFormat,
            ownsBuffer: false,
            enableFontSmoothing: _options.EnableFontSmoothing,
            enableSubpixelPositioning: _options.EnableSubpixelPositioning,
            enableEffects: _options.EnableCoreImageEffects);

        properties = new RenderTargetDrawingContextProperties
        {
            PreviousFrameIsRetained = lockProperties.PreviousFrameIsRetained
        };

        return new CoreTextDrawingContextImpl(
            bitmap,
            framebuffer.Dispose,
            bitmap.Dispose,
            scaleDrawingToDpi: false,
            enableFontSmoothing: _options.EnableFontSmoothing,
            enableSubpixelPositioning: _options.EnableSubpixelPositioning,
            enableEffects: _options.EnableCoreImageEffects);
    }

    public void Dispose()
    {
        _renderTarget?.Dispose();
        _renderTarget = null;
    }
}

internal sealed class CoreTextMetalRenderTarget : IRenderTarget
{
    private const uint BgraFourCc = ((uint)'B' << 24) | ((uint)'G' << 16) | ((uint)'R' << 8) | 'A';
    private const int BytesPerPixel = 4;
    private readonly CoreTextPlatformOptions _options;
    private readonly IMetalDevice _device;
    private readonly IMTLDevice _metalDevice;
    private readonly IMTLCommandQueue _commandQueue;
    private readonly CoreTextCoreImageContext _effectsContext;
    private IMetalPlatformSurfaceRenderTarget? _target;
    private CoreTextMetalBackBuffer? _backBuffer;

    public CoreTextMetalRenderTarget(CoreTextPlatformOptions options, IMetalPlatformSurface surface, IMetalDevice device)
    {
        _options = options;
        _device = device;
        _metalDevice = Runtime.GetINativeObject<IMTLDevice>(_device.Device, false)
            ?? throw new InvalidOperationException("Unable to resolve the native Metal device.");
        _commandQueue = Runtime.GetINativeObject<IMTLCommandQueue>(_device.CommandQueue, false)
            ?? throw new InvalidOperationException("Unable to resolve the native Metal command queue.");
        _effectsContext = CoreTextCoreImageContext.ForMetalDevice(device);
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
        if (_backBuffer is null || _backBuffer.PixelSize != session.Size || _backBuffer.Dpi != dpi)
        {
            _backBuffer?.Dispose();
            _backBuffer = new CoreTextMetalBackBuffer(
                _metalDevice,
                session.Size,
                dpi,
                _options.EnableFontSmoothing,
                _options.EnableSubpixelPositioning,
                _options.EnableCoreImageEffects,
                _effectsContext);
        }

        var bitmap = _backBuffer.Bitmap;
        bitmap.ClearPixels();

        properties = default;

        return new CoreTextDrawingContextImpl(
            bitmap,
            () =>
            {
                _backBuffer.Present(session.Texture, _commandQueue);
                session.Dispose();
            },
            scaleDrawingToDpi: false,
            enableFontSmoothing: _options.EnableFontSmoothing,
            enableSubpixelPositioning: _options.EnableSubpixelPositioning,
            enableEffects: _options.EnableCoreImageEffects);
    }

    public void Dispose()
    {
        _backBuffer?.Dispose();
        _backBuffer = null;
        _target?.Dispose();
        _target = null;
    }

    internal static int GetAlignedBytesPerRow(IMTLDevice metalDevice, int width)
    {
        var minimumAlignment = (int)metalDevice.GetMinimumLinearTextureAlignment(MTLPixelFormat.BGRA8Unorm);
        return GetAlignedBytesPerRow(width, minimumAlignment);
    }

    internal static int GetAlignedBytesPerRow(int width, int minimumAlignment)
    {
        var rawBytesPerRow = checked(width * BytesPerPixel);
        if (minimumAlignment <= 0)
        {
            return rawBytesPerRow;
        }

        return AlignUp(rawBytesPerRow, minimumAlignment);
    }

    private static int AlignUp(int value, int alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : checked(value + (alignment - remainder));
    }

    private sealed class CoreTextMetalBackBuffer : IDisposable
    {
        private readonly IOSurface.IOSurface _surface;
        private readonly IMTLTexture _texture;

        public CoreTextMetalBackBuffer(
            IMTLDevice metalDevice,
            PixelSize pixelSize,
            Vector dpi,
            bool enableFontSmoothing,
            bool enableSubpixelPositioning,
            bool enableEffects,
            CoreTextCoreImageContext effectsContext)
        {
            PixelSize = pixelSize;
            Dpi = dpi;
            var bytesPerRow = GetAlignedBytesPerRow(metalDevice, pixelSize.Width);

            _surface = new IOSurface.IOSurface(new IOSurfaceOptions
            {
                Width = pixelSize.Width,
                Height = pixelSize.Height,
                BytesPerElement = BytesPerPixel,
                BytesPerRow = bytesPerRow,
                PixelFormat = BgraFourCc
            });

            _surface.Lock((IOSurfaceLockOptions)0);

            var descriptor = MTLTextureDescriptor.CreateTexture2DDescriptor(
                MTLPixelFormat.BGRA8Unorm,
                (nuint)pixelSize.Width,
                (nuint)pixelSize.Height,
                mipmapped: false);
            _texture = metalDevice.CreateTexture(descriptor, _surface, 0)
                ?? throw new InvalidOperationException("Unable to create an IOSurface-backed Metal texture.");

            Bitmap = new CoreTextBitmapImpl(
                _surface.BaseAddress,
                pixelSize,
                dpi,
                (int)_surface.BytesPerRow,
                PixelFormats.Bgra8888,
                AlphaFormat.Premul,
                ownsBuffer: false,
                scaleDrawingToDpiOnCreateDrawingContext: false,
                enableFontSmoothing: enableFontSmoothing,
                enableSubpixelPositioning: enableSubpixelPositioning,
                enableEffects: enableEffects,
                coreImageContext: effectsContext,
                ioSurface: _surface);
        }

        public PixelSize PixelSize { get; }

        public Vector Dpi { get; }

        public CoreTextBitmapImpl Bitmap { get; }

        public void Present(IntPtr destinationTextureHandle, IMTLCommandQueue commandQueue)
        {
            var destinationTexture = Runtime.GetINativeObject<IMTLTexture>(destinationTextureHandle, false);
            if (destinationTexture is null)
            {
                return;
            }

            using var commandBuffer = commandQueue.CommandBuffer()
                ?? throw new InvalidOperationException("Unable to create a Metal command buffer for presentation.");
            using var blitEncoder = commandBuffer.BlitCommandEncoder;
            blitEncoder.CopyFromTexture(
                _texture,
                0,
                0,
                new MTLOrigin(0, 0, 0),
                new MTLSize(PixelSize.Width, PixelSize.Height, 1),
                destinationTexture,
                0,
                0,
                new MTLOrigin(0, 0, 0));
            blitEncoder.EndEncoding();
            commandBuffer.Commit();
            commandBuffer.WaitUntilCompleted();
        }

        public void Dispose()
        {
            Bitmap.Dispose();
            _texture.Dispose();
            _surface.Unlock((IOSurfaceLockOptions)0);
            _surface.Dispose();
        }
    }
}
