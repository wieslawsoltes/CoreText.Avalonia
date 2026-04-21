using System.Runtime.InteropServices;
using Avalonia.Metal;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Surfaces;

namespace MacOS.Avalonia;

internal abstract class MacOSTopLevelImpl : ITopLevelImpl, IFramebufferPlatformSurface
{
    private readonly object _softwareFrameSync = new();
    private readonly IPlatformRenderSurface[] _surfaces;
    private readonly MacOSTopLevelHandle _handle;
    private readonly MacOSMetalPlatformSurface _metalSurface;
    private readonly MacOSGlPlatformSurface _glSurface;
    private CGImage? _softwareFrame;
    private Size _clientSize;
    private double _renderScaling = 1;
    private WindowTransparencyLevel _transparencyLevel;

    protected MacOSTopLevelImpl(MacOSPlatform platform)
    {
        Platform = platform;
        _handle = new MacOSTopLevelHandle(this);
        _metalSurface = new MacOSMetalPlatformSurface(this);
        _glSurface = new MacOSGlPlatformSurface(this);
        _surfaces = [_handle, _metalSurface, _glSurface, this];
    }

    protected MacOSPlatform Platform { get; }

    protected NSView NativeView { get; private set; } = null!;

    public virtual double DesktopScaling => RenderScaling;

    public IPlatformHandle? Handle => _handle;

    public Size ClientSize => _clientSize;

    public double RenderScaling => _renderScaling;

    public IPlatformRenderSurface[] Surfaces => _surfaces;

    public Action<RawInputEventArgs>? Input { get; set; }

    public Action<Rect>? Paint { get; set; }

    public Action<Size, WindowResizeReason>? Resized { get; set; }

    public Action<double>? ScalingChanged { get; set; }

    public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }

    public Compositor Compositor => MacOSPlatform.Compositor;

    public Action? Closed { get; set; }

    public Action? LostFocus { get; set; }

    public WindowTransparencyLevel TransparencyLevel
    {
        get => _transparencyLevel;
        protected set
        {
            if (_transparencyLevel == value)
            {
                return;
            }

            _transparencyLevel = value;
            TransparencyLevelChanged?.Invoke(value);
        }
    }

    public AcrylicPlatformCompensationLevels AcrylicCompensationLevels { get; } = new(1, 0, 0);

    public PixelSize PixelSize => PixelSize.FromSize(ClientSize, RenderScaling);

    public IntPtr NativeViewHandle => NativeView.Handle;

    public IntPtr NativeWindowHandle => NativeWindow?.Handle ?? IntPtr.Zero;

    protected abstract NSWindow? NativeWindow { get; }

    protected void InitializeNativeView(NSView view)
    {
        NativeView = view;
        UpdateMetrics(WindowResizeReason.Layout);
    }

    public void SetInputRoot(IInputRoot inputRoot)
    {
        InputRoot = inputRoot;
    }

    protected IInputRoot? InputRoot { get; private set; }

    public Point PointToClient(PixelPoint point)
    {
        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;
        if (NativeWindow is null)
        {
            return point.ToPoint(scaling);
        }

        var frame = NativeWindow.Frame;
        return new Point((point.X / scaling) - frame.X, (point.Y / scaling) - frame.Y);
    }

    public PixelPoint PointToScreen(Point point)
    {
        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;
        if (NativeWindow is null)
        {
            return PixelPoint.FromPoint(point, scaling);
        }

        var frame = NativeWindow.Frame;
        return PixelPoint.FromPoint(new Point(frame.X + point.X, frame.Y + point.Y), scaling);
    }

    public void SetCursor(ICursorImpl? cursor)
    {
        var effectiveCursor = (cursor as MacOSCursorImpl)?.Cursor ?? NSCursor.ArrowCursor;
        NativeView.DiscardCursorRects();
        effectiveCursor.Set();
    }

    public virtual IPopupImpl? CreatePopup() => null;

    public virtual void SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels)
    {
        foreach (var level in transparencyLevels)
        {
            if (level == WindowTransparencyLevel.None || level == WindowTransparencyLevel.Transparent)
            {
                TransparencyLevel = level;
                return;
            }
        }

        TransparencyLevel = WindowTransparencyLevel.None;
    }

    public virtual void SetFrameThemeVariant(PlatformThemeVariant themeVariant)
    {
    }

    public virtual object? TryGetFeature(Type featureType)
    {
        if (featureType == typeof(IScreenImpl))
        {
            return Platform.Screens;
        }

        if (featureType == typeof(IClipboard))
        {
            return AvaloniaLocator.Current.GetRequiredService<IClipboard>();
        }

        return null;
    }

    public virtual void Dispose()
    {
        lock (_softwareFrameSync)
        {
            _softwareFrame?.Dispose();
            _softwareFrame = null;
        }

        NativeView.Dispose();
    }

    IFramebufferRenderTarget IFramebufferPlatformSurface.CreateFramebufferRenderTarget()
    {
        Dispatcher.UIThread.VerifyAccess();
        return new MacOSFramebufferRenderTarget(this);
    }

    internal virtual void UpdateMetrics(WindowResizeReason reason)
    {
        var newClientSize = new Size(NativeView.Bounds.Width, NativeView.Bounds.Height);
        if (_clientSize != newClientSize)
        {
            _clientSize = newClientSize;
            Resized?.Invoke(newClientSize, reason);
        }

        var newScaling = NativeWindow?.Screen?.BackingScaleFactor ?? NativeView.Window?.Screen?.BackingScaleFactor ?? 1d;
        if (Math.Abs(_renderScaling - newScaling) > double.Epsilon)
        {
            _renderScaling = newScaling;
            ScalingChanged?.Invoke(newScaling);
        }

        UpdateMetalLayerMetrics();
    }

    internal void OnPaint(CGContext? context)
    {
        Dispatcher.UIThread.RunJobs(DispatcherPriority.UiThreadRender);
        Paint?.Invoke(new Rect(0, 0, ClientSize.Width, ClientSize.Height));

        if (context is not null)
        {
            DrawSoftwareFrame(context);
        }
    }

    protected internal void Invalidate()
    {
        NativeView.NeedsDisplay = true;
    }

    internal void SetSoftwareFrame(CGImage image)
    {
        lock (_softwareFrameSync)
        {
            _softwareFrame?.Dispose();
            _softwareFrame = image;
        }
    }

    internal CAMetalLayer EnsureMetalLayer(IMTLDevice device)
    {
        NativeView.WantsLayer = true;
        if (NativeView.Layer is not CAMetalLayer layer)
        {
            layer = new CAMetalLayer
            {
                Device = device,
                PixelFormat = MTLPixelFormat.BGRA8Unorm,
                FramebufferOnly = false
            };
            NativeView.Layer = layer;
        }

        UpdateMetalLayerMetrics();
        return layer;
    }

    private void UpdateMetalLayerMetrics()
    {
        if (NativeView.Layer is CAMetalLayer layer)
        {
            layer.Frame = NativeView.Bounds;
            layer.DrawableSize = new CGSize(PixelSize.Width, PixelSize.Height);
            layer.ContentsScale = (nfloat)(RenderScaling <= 0 ? 1 : RenderScaling);
        }
    }

    internal void ConfigureOpenGlContext(MacOSGlContext context)
    {
        NativeView.WantsLayer = false;
        context.NativeContext.View = NativeView;
        context.NativeContext.Update();
    }

    internal void NotifyLostFocus()
    {
        LostFocus?.Invoke();
    }

    internal void NotifyClosed()
    {
        Closed?.Invoke();
    }

    private void DrawSoftwareFrame(CGContext context)
    {
        lock (_softwareFrameSync)
        {
            if (_softwareFrame is null)
            {
                return;
            }

            context.DrawImage(new CGRect(0, 0, ClientSize.Width, ClientSize.Height), _softwareFrame);
        }
    }

    private sealed class MacOSFramebufferRenderTarget(MacOSTopLevelImpl topLevel) : IFramebufferRenderTarget
    {
        private readonly MacOSTopLevelImpl _topLevel = topLevel;

        public ILockedFramebuffer Lock(IRenderTarget.RenderTargetSceneInfo sceneInfo, out FramebufferLockProperties properties)
        {
            properties = default;
            return new MacOSLockedFramebuffer(_topLevel);
        }

        public void Dispose()
        {
        }
    }

    private sealed class MacOSLockedFramebuffer : ILockedFramebuffer
    {
        private readonly MacOSTopLevelImpl _topLevel;
        private readonly int _bufferSize;
        private bool _disposed;

        public MacOSLockedFramebuffer(MacOSTopLevelImpl topLevel)
        {
            _topLevel = topLevel;
            Size = topLevel.PixelSize;
            Dpi = new Vector(topLevel.RenderScaling * 96, topLevel.RenderScaling * 96);
            RowBytes = Math.Max(Size.Width, 1) * 4;
            _bufferSize = Math.Max(Size.Height, 1) * RowBytes;
            Address = Marshal.AllocHGlobal(_bufferSize);
        }

        public IntPtr Address { get; }

        public PixelSize Size { get; }

        public int RowBytes { get; }

        public Vector Dpi { get; }

        public PixelFormat Format => PixelFormats.Bgra8888;

        public AlphaFormat AlphaFormat => AlphaFormat.Premul;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            using var colorSpace = CGColorSpace.CreateDeviceRGB();
            using var bitmapContext = new CGBitmapContext(
                Address,
                Math.Max(Size.Width, 1),
                Math.Max(Size.Height, 1),
                8,
                RowBytes,
                colorSpace,
                CGBitmapFlags.ByteOrder32Little | CGBitmapFlags.PremultipliedFirst);
            var image = bitmapContext.ToImage();
            _topLevel.SetSoftwareFrame(image);
            Marshal.FreeHGlobal(Address);
        }
    }

    private sealed class MacOSMetalPlatformSurface(MacOSTopLevelImpl topLevel) : IMetalPlatformSurface
    {
        private readonly MacOSTopLevelImpl _topLevel = topLevel;

        public IMetalPlatformSurfaceRenderTarget CreateMetalRenderTarget(IMetalDevice device)
        {
            Dispatcher.UIThread.VerifyAccess();
            return new MacOSMetalRenderTarget(_topLevel, (MacOSMetalDevice)device);
        }
    }

    private sealed class MacOSMetalRenderTarget(MacOSTopLevelImpl topLevel, MacOSMetalDevice device) : IMetalPlatformSurfaceRenderTarget
    {
        private readonly MacOSTopLevelImpl _topLevel = topLevel;
        private readonly MacOSMetalDevice _device = device;

        public PlatformRenderTargetState State => PlatformRenderTargetState.Ready;

        public IMetalPlatformSurfaceRenderingSession BeginRendering()
        {
            var layer = _topLevel.EnsureMetalLayer(_device.NativeDevice);
            var drawable = layer.NextDrawable();
            if (drawable is null)
            {
                throw new RenderTargetNotReadyException();
            }

            return new MacOSMetalRenderingSession(_topLevel, _device.NativeCommandQueue, drawable);
        }

        public void Dispose()
        {
        }
    }

    private sealed class MacOSMetalRenderingSession(
        MacOSTopLevelImpl topLevel,
        IMTLCommandQueue commandQueue,
        ICAMetalDrawable drawable) : IMetalPlatformSurfaceRenderingSession
    {
        private readonly IMTLCommandQueue _commandQueue = commandQueue;
        private readonly ICAMetalDrawable _drawable = drawable;

        public IntPtr Texture => _drawable.Texture.Handle;

        public PixelSize Size => topLevel.PixelSize;

        public double Scaling => topLevel.RenderScaling;

        public bool IsYFlipped => false;

        public void Dispose()
        {
            using var commandBuffer = _commandQueue.CommandBuffer();
            commandBuffer?.PresentDrawable(_drawable);
            commandBuffer?.Commit();
        }
    }

    private sealed class MacOSGlPlatformSurface(MacOSTopLevelImpl topLevel) : IGlPlatformSurface
    {
        private readonly MacOSTopLevelImpl _topLevel = topLevel;

        public IGlPlatformSurfaceRenderTarget CreateGlRenderTarget(IGlContext context)
        {
            Dispatcher.UIThread.VerifyAccess();
            return new MacOSGlPlatformSurfaceRenderTarget(_topLevel, (MacOSGlContext)context);
        }
    }

    private sealed class MacOSGlPlatformSurfaceRenderTarget(MacOSTopLevelImpl topLevel, MacOSGlContext context) : IGlPlatformSurfaceRenderTarget
    {
        private readonly MacOSTopLevelImpl _topLevel = topLevel;
        private readonly MacOSGlContext _context = context;

        public PlatformRenderTargetState State => PlatformRenderTargetState.Ready;

        public IGlPlatformSurfaceRenderingSession BeginDraw(IRenderTarget.RenderTargetSceneInfo sceneInfo)
        {
            _topLevel.ConfigureOpenGlContext(_context);
            _context.NativeContext.Update();
            return new MacOSGlPlatformRenderingSession(_topLevel, _context);
        }

        public void Dispose()
        {
        }
    }

    private sealed class MacOSGlPlatformRenderingSession(MacOSTopLevelImpl topLevel, MacOSGlContext context) : IGlPlatformSurfaceRenderingSession
    {
        private readonly MacOSTopLevelImpl _topLevel = topLevel;
        private readonly IDisposable _currentContext = context.MakeCurrent();

        public IGlContext Context => context;

        public PixelSize Size => _topLevel.PixelSize;

        public double Scaling => _topLevel.RenderScaling;

        public bool IsYFlipped => true;

        public void Dispose()
        {
            context.NativeContext.FlushBuffer();
            _currentContext.Dispose();
        }
    }
}

internal sealed class MacOSView : NSView
{
    private readonly MacOSTopLevelImpl _topLevel;

    public MacOSView(MacOSTopLevelImpl topLevel)
    {
        _topLevel = topLevel;
        WantsLayer = true;
    }

    public override bool IsFlipped => true;

    public override bool AcceptsFirstResponder() => true;

    public override void DrawRect(CGRect dirtyRect)
    {
        base.DrawRect(dirtyRect);
        _topLevel.OnPaint(NSGraphicsContext.CurrentContext?.CGContext);
    }

    public override void SetFrameSize(CGSize newSize)
    {
        base.SetFrameSize(newSize);
        _topLevel.UpdateMetrics(WindowResizeReason.Unspecified);
    }
}
