using System.Runtime.InteropServices;
using Avalonia.Metal;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Surfaces;
using Avalonia.Input.TextInput;

namespace MacOS.Avalonia;

internal abstract class MacOSTopLevelImpl : ITopLevelImpl, IFramebufferPlatformSurface
{
    private readonly object _softwareFrameSync = new();
    private readonly IPlatformRenderSurface[] _surfaces;
    private readonly MacOSTopLevelHandle _handle;
    private readonly MacOSMetalPlatformSurface _metalSurface;
    private readonly MacOSGlPlatformSurface _glSurface;
    private readonly MouseDevice _mouseDevice = new();
    private readonly MacOSTextInputMethod _textInputMethod = new();
    private CGImage? _softwareFrame;
    private IDataTransfer? _activeDragDataTransfer;
    private IStorageProvider? _storageProvider;
    private Size _clientSize;
    private double _renderScaling = 1;
    private WindowTransparencyLevel _transparencyLevel;
    private NSDragOperation _activeDragOperation;
    private RawInputModifiers _pointerButtonModifiers;
    private RawInputModifiers _keyboardModifiers;

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

    internal NSView PlatformView => NativeView;

    internal MacOSTextInputMethod TextInputMethod => _textInputMethod;

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

    internal NSWindow? DialogOwnerWindow => NativeWindow;

    protected abstract NSWindow? NativeWindow { get; }

    protected void InitializeNativeView(NSView view)
    {
        NativeView = view;
        if (view is IMacOSTextInputHost textInputHost)
        {
            _textInputMethod.AttachHost(textInputHost);
        }

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
        if (featureType == typeof(ITextInputMethodImpl))
        {
            return _textInputMethod;
        }

        if (featureType == typeof(IScreenImpl))
        {
            return Platform.Screens;
        }

        if (featureType == typeof(IClipboard))
        {
            return AvaloniaLocator.Current.GetRequiredService<IClipboard>();
        }

        if (featureType == typeof(IStorageProvider))
        {
            return _storageProvider ??= new MacOSStorageProvider(this);
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

        if (NativeView is IMacOSTextInputHost textInputHost)
        {
            _textInputMethod.DetachHost(textInputHost);
        }

        _textInputMethod.Dispose();

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

    internal void HandlePointerEvent(NSEvent eventArgs, RawPointerEventType eventType)
    {
        if (TryHandlePlatformPointerEvent(eventArgs, eventType))
        {
            return;
        }

        if (InputRoot is null)
        {
            return;
        }

        UpdatePointerButtonModifiers(eventType);
        Input?.Invoke(new RawPointerEventArgs(
            _mouseDevice,
            GetTimestamp(eventArgs),
            InputRoot,
            eventType,
            GetClientPoint(eventArgs),
            GetCurrentModifiers(eventArgs)));
    }

    internal void HandlePointerLeave(NSEvent eventArgs)
    {
        if (InputRoot is null)
        {
            return;
        }

        _pointerButtonModifiers = RawInputModifiers.None;
        Input?.Invoke(new RawPointerEventArgs(
            _mouseDevice,
            GetTimestamp(eventArgs),
            InputRoot,
            RawPointerEventType.LeaveWindow,
            GetClientPoint(eventArgs),
            GetCurrentModifiers(eventArgs)));
    }

    internal void HandleMouseWheel(NSEvent eventArgs)
    {
        if (InputRoot is null)
        {
            return;
        }

        Input?.Invoke(new RawMouseWheelEventArgs(
            _mouseDevice,
            GetTimestamp(eventArgs),
            InputRoot,
            GetClientPoint(eventArgs),
            new Vector(eventArgs.ScrollingDeltaX, eventArgs.ScrollingDeltaY),
            GetCurrentModifiers(eventArgs)));
    }

    internal NSDragOperation HandleDragEnter(INSDraggingInfo draggingInfo)
    {
        _activeDragDataTransfer = MacOSClipboardImpl.TryCreateDataTransfer(draggingInfo.DraggingPasteboard);
        _activeDragOperation = DispatchDragEvent(RawDragEventType.DragEnter, draggingInfo);
        return _activeDragOperation;
    }

    internal NSDragOperation HandleDragOver(INSDraggingInfo draggingInfo)
    {
        _activeDragOperation = DispatchDragEvent(RawDragEventType.DragOver, draggingInfo);
        return _activeDragOperation;
    }

    internal void HandleDragLeave(INSDraggingInfo draggingInfo)
    {
        DispatchDragEvent(RawDragEventType.DragLeave, draggingInfo);
        ClearActiveDragSession();
    }

    internal bool PrepareDragOperation() => _activeDragOperation != NSDragOperation.None;

    internal bool HandleDrop(INSDraggingInfo draggingInfo)
    {
        var dragOperation = DispatchDragEvent(RawDragEventType.Drop, draggingInfo);
        ClearActiveDragSession();
        return dragOperation != NSDragOperation.None;
    }

    internal bool HandleKeyEvent(NSEvent eventArgs, RawKeyEventType eventType, bool suppressTextInput = false)
    {
        if (InputRoot is null)
        {
            return false;
        }

        Dispatcher.UIThread.RunJobs(DispatcherPriority.Input + 1);

        var physicalKey = MacOSInputHelpers.ToPhysicalKey((ushort)eventArgs.KeyCode);
        var modifiers = GetCurrentModifiers(eventArgs);
        var key = MacOSInputHelpers.ToKey((ushort)eventArgs.KeyCode, eventArgs.CharactersIgnoringModifiers);
        var keySymbol = MacOSInputHelpers.GetKeySymbol(eventArgs, physicalKey, modifiers);

        var keyEvent = new RawKeyEventArgs(
            Platform.KeyboardDevice,
            GetTimestamp(eventArgs),
            InputRoot,
            eventType,
            key,
            modifiers,
            physicalKey,
            keySymbol);
        Input?.Invoke(keyEvent);

        if (eventType == RawKeyEventType.KeyDown
            && !suppressTextInput
            && !keyEvent.Handled
            && !modifiers.HasFlag(RawInputModifiers.Control)
            && !modifiers.HasFlag(RawInputModifiers.Meta))
        {
            HandleTextInput(MacOSInputHelpers.GetText(eventArgs), eventArgs);
        }

        return keyEvent.Handled;
    }

    internal void HandleTextInput(string? text, NSEvent? eventArgs = null)
    {
        if (InputRoot is null || string.IsNullOrEmpty(text))
        {
            return;
        }

        Input?.Invoke(new RawTextInputEventArgs(
            Platform.KeyboardDevice,
            GetTimestamp(eventArgs),
            InputRoot,
            text));
    }

    internal void HandleFlagsChanged(NSEvent eventArgs)
    {
        if (InputRoot is null)
        {
            return;
        }

        var newModifiers = MacOSInputHelpers.ToRawInputModifiers(eventArgs.ModifierFlags);
        var changedModifiers = _keyboardModifiers ^ newModifiers;
        if (changedModifiers == RawInputModifiers.None)
        {
            _keyboardModifiers = newModifiers;
            return;
        }

        _keyboardModifiers = newModifiers;
        var physicalKey = MacOSInputHelpers.ToPhysicalKey((ushort)eventArgs.KeyCode);
        if (physicalKey == PhysicalKey.None)
        {
            return;
        }

        Dispatcher.UIThread.RunJobs(DispatcherPriority.Input + 1);

        var key = physicalKey.ToQwertyKey();
        var keyEventType = IsModifierPressed(eventArgs, newModifiers, physicalKey)
            ? RawKeyEventType.KeyDown
            : RawKeyEventType.KeyUp;
        Input?.Invoke(new RawKeyEventArgs(
            Platform.KeyboardDevice,
            GetTimestamp(eventArgs),
            InputRoot,
            keyEventType,
            key,
            GetCurrentModifiers(eventArgs),
            physicalKey,
            MacOSInputHelpers.GetKeySymbol(eventArgs, physicalKey, GetCurrentModifiers(eventArgs))));
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

    internal NSDraggingSession BeginDraggingSession(
        INSPasteboardWriting pasteboardWriting,
        Point position,
        NSEvent currentEvent,
        INSDraggingSource draggingSource,
        NSImage dragImage)
    {
        var dragItem = new NSDraggingItem(pasteboardWriting);
        var dragFrame = new CGRect(
            position.X,
            position.Y,
            Math.Max(dragImage.Size.Width, 1),
            Math.Max(dragImage.Size.Height, 1));
        dragItem.SetDraggingFrame(dragFrame, dragImage);

        var session = NativeView.BeginDraggingSession([dragItem], currentEvent, draggingSource);
        session.DraggingFormation = NSDraggingFormation.None;
        session.AnimatesToStartingPositionsOnCancelOrFail = false;
        return session;
    }

    internal void NotifyLostFocus()
    {
        _pointerButtonModifiers = RawInputModifiers.None;
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

    private ulong GetTimestamp(NSEvent? eventArgs)
    {
        return eventArgs is null ? 0UL : (ulong)Math.Max(0, eventArgs.Timestamp * 1000d);
    }

    protected virtual bool TryHandlePlatformPointerEvent(NSEvent eventArgs, RawPointerEventType eventType) => false;

    protected Point GetClientPoint(NSEvent eventArgs)
    {
        var clientPoint = GetClientPoint(eventArgs.LocationInWindow);
        return new Point(clientPoint.X, clientPoint.Y);
    }

    protected Point GetClientPoint(CGPoint windowPoint)
    {
        var clientPoint = NativeView.ConvertPointFromView(windowPoint, null);
        return new Point(clientPoint.X, clientPoint.Y);
    }

    private RawInputModifiers GetCurrentModifiers(NSEvent eventArgs)
    {
        return GetCurrentModifiers(eventArgs.ModifierFlags);
    }

    private RawInputModifiers GetCurrentModifiers(NSEventModifierMask modifierFlags)
    {
        _keyboardModifiers = MacOSInputHelpers.ToRawInputModifiers(modifierFlags);
        return _keyboardModifiers | _pointerButtonModifiers;
    }

    private RawInputModifiers GetCurrentDragModifiers()
    {
        return GetCurrentModifiers(Platform.NativeApplication.CurrentEvent?.ModifierFlags ?? default);
    }

    private void UpdatePointerButtonModifiers(RawPointerEventType eventType)
    {
        switch (eventType)
        {
            case RawPointerEventType.LeftButtonDown:
                _pointerButtonModifiers |= RawInputModifiers.LeftMouseButton;
                break;
            case RawPointerEventType.LeftButtonUp:
                _pointerButtonModifiers &= ~RawInputModifiers.LeftMouseButton;
                break;
            case RawPointerEventType.RightButtonDown:
                _pointerButtonModifiers |= RawInputModifiers.RightMouseButton;
                break;
            case RawPointerEventType.RightButtonUp:
                _pointerButtonModifiers &= ~RawInputModifiers.RightMouseButton;
                break;
            case RawPointerEventType.MiddleButtonDown:
                _pointerButtonModifiers |= RawInputModifiers.MiddleMouseButton;
                break;
            case RawPointerEventType.MiddleButtonUp:
                _pointerButtonModifiers &= ~RawInputModifiers.MiddleMouseButton;
                break;
        }
    }

    private static bool IsModifierPressed(NSEvent eventArgs, RawInputModifiers modifiers, PhysicalKey physicalKey)
    {
        return physicalKey switch
        {
            PhysicalKey.ShiftLeft or PhysicalKey.ShiftRight => modifiers.HasFlag(RawInputModifiers.Shift),
            PhysicalKey.ControlLeft or PhysicalKey.ControlRight => modifiers.HasFlag(RawInputModifiers.Control),
            PhysicalKey.AltLeft or PhysicalKey.AltRight => modifiers.HasFlag(RawInputModifiers.Alt),
            PhysicalKey.MetaLeft or PhysicalKey.MetaRight => modifiers.HasFlag(RawInputModifiers.Meta),
            PhysicalKey.CapsLock => eventArgs.ModifierFlags.HasFlag(NSEventModifierMask.AlphaShiftKeyMask),
            _ => false
        };
    }

    private NSDragOperation DispatchDragEvent(RawDragEventType eventType, INSDraggingInfo draggingInfo)
    {
        if (InputRoot is null)
        {
            return NSDragOperation.None;
        }

        if (AvaloniaLocator.Current.GetService<IDragDropDevice>() is not { } dragDropDevice)
        {
            return NSDragOperation.None;
        }

        var dataTransfer = _activeDragDataTransfer ??= MacOSClipboardImpl.TryCreateDataTransfer(draggingInfo.DraggingPasteboard);
        if (dataTransfer is null)
        {
            return NSDragOperation.None;
        }

        var allowedEffects = MacOSDragDropHelper.ToDragDropEffects(draggingInfo.DraggingSourceOperationMask);
        var dragEvent = new RawDragEvent(
            dragDropDevice,
            eventType,
            InputRoot,
            GetClientPoint(draggingInfo.DraggingLocation),
            dataTransfer,
            allowedEffects,
            GetCurrentDragModifiers());
        Input?.Invoke(dragEvent);

        return MacOSDragDropHelper.ToNativeDragOperation(dragEvent.Effects) & draggingInfo.DraggingSourceOperationMask;
    }

    private void ClearActiveDragSession()
    {
        _activeDragDataTransfer = null;
        _activeDragOperation = NSDragOperation.None;
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

internal sealed class MacOSView : NSView, INSTextInput, IMacOSTextInputHost
{
    private readonly MacOSTopLevelImpl _topLevel;
    private readonly MacOSTextInputMethod _textInputMethod;
    private NSTrackingArea? _trackingArea;
    private string? _markedText;
    private NSRange _markedSelectionRange;

    public MacOSView(MacOSTopLevelImpl topLevel)
    {
        _topLevel = topLevel;
        _textInputMethod = topLevel.TextInputMethod;
        WantsLayer = true;
        RegisterForDraggedTypes(MacOSDragDropHelper.RegisteredPasteboardTypes);
    }

    public override bool IsFlipped => true;

    public override bool AcceptsFirstResponder() => true;

    public override bool BecomeFirstResponder()
    {
        var result = base.BecomeFirstResponder();
        if (result)
        {
            InputContext?.Activate();
            RefreshTextInputState();
        }

        return result;
    }

    public override bool ResignFirstResponder()
    {
        var result = base.ResignFirstResponder();
        if (result)
        {
            ClearMarkedText();
            InputContext?.Deactivate();
        }

        return result;
    }

    public override void UpdateTrackingAreas()
    {
        if (_trackingArea is not null)
        {
            RemoveTrackingArea(_trackingArea);
            _trackingArea.Dispose();
            _trackingArea = null;
        }

        _trackingArea = new NSTrackingArea(
            Bounds,
            NSTrackingAreaOptions.MouseEnteredAndExited
            | NSTrackingAreaOptions.MouseMoved
            | NSTrackingAreaOptions.ActiveAlways
            | NSTrackingAreaOptions.InVisibleRect
            | NSTrackingAreaOptions.EnabledDuringMouseDrag,
            this,
            null);
        AddTrackingArea(_trackingArea);
        base.UpdateTrackingAreas();
    }

    public override void ViewDidMoveToSuperview()
    {
        base.ViewDidMoveToSuperview();
        UpdateTrackingAreas();
        _topLevel.UpdateMetrics(WindowResizeReason.Layout);
        _topLevel.Invalidate();
    }

    public override void ViewDidMoveToWindow()
    {
        base.ViewDidMoveToWindow();
        if (Window is not null)
        {
            Window.AcceptsMouseMovedEvents = true;
        }

        if (_textInputMethod.HasClient)
        {
            ActivateTextInput();
        }

        UpdateTrackingAreas();
        _topLevel.UpdateMetrics(WindowResizeReason.Layout);
        _topLevel.Invalidate();
    }

    public override void DidChangeBackingProperties()
    {
        base.DidChangeBackingProperties();
        _topLevel.UpdateMetrics(WindowResizeReason.DpiChange);
        _topLevel.Invalidate();
    }

    public override void MouseMoved(NSEvent theEvent)
    {
        base.MouseMoved(theEvent);
        _topLevel.HandlePointerEvent(theEvent, RawPointerEventType.Move);
    }

    public override void MouseDown(NSEvent theEvent)
    {
        base.MouseDown(theEvent);
        _topLevel.HandlePointerEvent(theEvent, RawPointerEventType.LeftButtonDown);
    }

    public override void MouseUp(NSEvent theEvent)
    {
        base.MouseUp(theEvent);
        _topLevel.HandlePointerEvent(theEvent, RawPointerEventType.LeftButtonUp);
    }

    public override void MouseDragged(NSEvent theEvent)
    {
        base.MouseDragged(theEvent);
        _topLevel.HandlePointerEvent(theEvent, RawPointerEventType.Move);
    }

    public override void RightMouseDown(NSEvent theEvent)
    {
        base.RightMouseDown(theEvent);
        _topLevel.HandlePointerEvent(theEvent, RawPointerEventType.RightButtonDown);
    }

    public override void RightMouseUp(NSEvent theEvent)
    {
        base.RightMouseUp(theEvent);
        _topLevel.HandlePointerEvent(theEvent, RawPointerEventType.RightButtonUp);
    }

    public override void RightMouseDragged(NSEvent theEvent)
    {
        base.RightMouseDragged(theEvent);
        _topLevel.HandlePointerEvent(theEvent, RawPointerEventType.Move);
    }

    public override void OtherMouseDown(NSEvent theEvent)
    {
        base.OtherMouseDown(theEvent);
        _topLevel.HandlePointerEvent(theEvent, RawPointerEventType.MiddleButtonDown);
    }

    public override void OtherMouseUp(NSEvent theEvent)
    {
        base.OtherMouseUp(theEvent);
        _topLevel.HandlePointerEvent(theEvent, RawPointerEventType.MiddleButtonUp);
    }

    public override void OtherMouseDragged(NSEvent theEvent)
    {
        base.OtherMouseDragged(theEvent);
        _topLevel.HandlePointerEvent(theEvent, RawPointerEventType.Move);
    }

    public override void MouseExited(NSEvent theEvent)
    {
        base.MouseExited(theEvent);
        _topLevel.HandlePointerLeave(theEvent);
    }

    public override void ScrollWheel(NSEvent theEvent)
    {
        base.ScrollWheel(theEvent);
        _topLevel.HandleMouseWheel(theEvent);
    }

    public override NSDragOperation DraggingEntered(INSDraggingInfo? sender)
    {
        return sender is null ? NSDragOperation.None : _topLevel.HandleDragEnter(sender);
    }

    public override NSDragOperation DraggingUpdated(INSDraggingInfo? sender)
    {
        return sender is null ? NSDragOperation.None : _topLevel.HandleDragOver(sender);
    }

    public override void DraggingExited(INSDraggingInfo? sender)
    {
        if (sender is not null)
        {
            _topLevel.HandleDragLeave(sender);
        }
    }

    public override bool PrepareForDragOperation(INSDraggingInfo? sender)
    {
        return sender is not null && _topLevel.PrepareDragOperation();
    }

    public override bool PerformDragOperation(INSDraggingInfo? sender)
    {
        return sender is not null && _topLevel.HandleDrop(sender);
    }

    public override void KeyDown(NSEvent theEvent)
    {
        var handled = _topLevel.HandleKeyEvent(theEvent, RawKeyEventType.KeyDown, suppressTextInput: _textInputMethod.HasClient);
        if (handled || !_textInputMethod.HasClient)
        {
            return;
        }

        if (theEvent.ModifierFlags.HasFlag(NSEventModifierMask.ControlKeyMask)
            || theEvent.ModifierFlags.HasFlag(NSEventModifierMask.CommandKeyMask))
        {
            return;
        }

        if (InputContext?.HandleEvent(theEvent) != true)
        {
            _topLevel.HandleTextInput(MacOSInputHelpers.GetText(theEvent), theEvent);
        }
    }

    public override void KeyUp(NSEvent theEvent)
    {
        _topLevel.HandleKeyEvent(theEvent, RawKeyEventType.KeyUp);
    }

    public override void FlagsChanged(NSEvent theEvent)
    {
        base.FlagsChanged(theEvent);
        _topLevel.HandleFlagsChanged(theEvent);
    }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing && _trackingArea is not null)
        {
            RemoveTrackingArea(_trackingArea);
            _trackingArea.Dispose();
            _trackingArea = null;
        }

        base.Dispose(disposing);
    }

    public void ActivateTextInput()
    {
        if (Window is not null && !ReferenceEquals(Window.FirstResponder, this))
        {
            Window.MakeFirstResponder(this);
        }

        InputContext?.Activate();
        RefreshTextInputState();
    }

    public void RefreshTextInputState()
    {
        InputContext?.InvalidateCharacterCoordinates();
        if (OperatingSystem.IsMacOSVersionAtLeast(15, 4))
        {
            InputContext?.TextInputClientDidUpdateSelection();
        }
    }

    public void ResetTextInputState()
    {
        ClearMarkedText();
        InputContext?.DiscardMarkedText();
        RefreshTextInputState();
    }

    void INSTextInput.InsertText(NSObject insertString)
    {
        ClearMarkedText();
        _topLevel.HandleTextInput(ExtractText(insertString), NSApplication.SharedApplication.CurrentEvent);
        RefreshTextInputState();
    }

    void INSTextInput.SetMarkedText(NSObject markedTextObject, NSRange selRange)
    {
        var markedText = ExtractText(markedTextObject) ?? string.Empty;
        _markedText = markedText;
        _markedSelectionRange = selRange;
        _textInputMethod.Client?.SetPreeditText(markedText, checked((int)selRange.Location));
        RefreshTextInputState();
    }

    void INSTextInput.UnmarkText()
    {
        ClearMarkedText();
        RefreshTextInputState();
    }

    NSAttributedString INSTextInput.GetAttributedSubstring(NSRange range)
    {
        var text = GetTextInRange(range);
        return new NSAttributedString(text ?? string.Empty);
    }

    CGRect INSTextInput.GetFirstRectForCharacterRange(NSRange range)
    {
        var cursorRect = _textInputMethod.CursorRect;
        var rect = new CGRect(cursorRect.X, cursorRect.Y, Math.Max(cursorRect.Width, 1), Math.Max(cursorRect.Height, 1));
        return Window?.ConvertRectToScreen(rect) ?? rect;
    }

    nuint INSTextInput.GetCharacterIndex(CGPoint point)
    {
        return (nuint)Math.Max(_textInputMethod.Client?.Selection.Start ?? 0, 0);
    }

    bool INSTextInput.HasMarkedText => !string.IsNullOrEmpty(_markedText);

    nint INSTextInput.ConversationIdentifier => Handle;

    NSRange INSTextInput.MarkedRange => string.IsNullOrEmpty(_markedText)
        ? new NSRange((nint)NSRange.NotFound, 0)
        : new NSRange(GetSelectionStart(), _markedText.Length);

    NSRange INSTextInput.SelectedRange
    {
        get
        {
            if (_textInputMethod.Client is not { } client)
            {
                return new NSRange((nint)NSRange.NotFound, 0);
            }

            if (!string.IsNullOrEmpty(_markedText))
            {
                return new NSRange(
                    GetSelectionStart() + (nint)_markedSelectionRange.Location,
                    (nint)_markedSelectionRange.Length);
            }

            return new NSRange(
                Math.Max(client.Selection.Start, 0),
                Math.Max(client.Selection.End - client.Selection.Start, 0));
        }
    }

    NSString[] INSTextInput.ValidAttributesForMarkedText => [];

    private void ClearMarkedText()
    {
        if (string.IsNullOrEmpty(_markedText))
        {
            return;
        }

        _markedText = null;
        _markedSelectionRange = default;
        _textInputMethod.Client?.SetPreeditText(null);
    }

    private string? GetTextInRange(NSRange range)
    {
        var documentText = GetDocumentText();
        var start = range.Location <= 0
            ? 0
            : range.Location >= documentText.Length
                ? documentText.Length
                : (int)range.Location;
        var maxLength = documentText.Length - start;
        var length = range.Length <= 0
            ? 0
            : range.Length >= maxLength
                ? maxLength
                : (int)range.Length;
        return documentText.Substring(start, length);
    }

    private string GetDocumentText()
    {
        var surroundingText = _textInputMethod.Client?.SurroundingText ?? string.Empty;
        if (string.IsNullOrEmpty(_markedText) || _textInputMethod.Client is not { } client)
        {
            return surroundingText;
        }

        var selectionStart = Math.Clamp(client.Selection.Start, 0, surroundingText.Length);
        var selectionEnd = Math.Clamp(client.Selection.End, selectionStart, surroundingText.Length);
        return string.Concat(
            surroundingText.AsSpan(0, selectionStart),
            _markedText,
            surroundingText.AsSpan(selectionEnd));
    }

    private nint GetSelectionStart()
    {
        return Math.Max(_textInputMethod.Client?.Selection.Start ?? 0, 0);
    }

    private static string? ExtractText(NSObject text)
    {
        return text switch
        {
            NSString value => value.ToString(),
            NSAttributedString value => value.Value,
            _ => text.ToString()
        };
    }
}
