using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives.PopupPositioning;

namespace MacOS.Avalonia;

internal class MacOSWindowImpl : MacOSTopLevelImpl, IWindowImpl
{
    private static readonly NSImage s_placeholderWindowImage = new();
    private readonly MacOSPlatformOptions _options;
    private readonly NSWindow _window;
    private readonly WindowDelegate _delegate;
    private readonly MacOSWindowMenuExporter _menuExporter;
    private readonly Timer _invalidateTimer;
    private bool _isVisible;
    private bool _canResize = true;
    private bool _canMinimize = true;
    private bool _canMaximize = true;
    private WindowDecorations _windowDecorations = WindowDecorations.Full;
    private WindowState _windowState = WindowState.Normal;
    private bool _extendClientArea;
    private double _extendClientAreaTitleBarHeight = -1;
    private ResizeDragState? _resizeDrag;
    private NSImage? _windowIconImage;

    public MacOSWindowImpl(MacOSPlatform platform, MacOSPlatformOptions options)
        : this(platform, options, CreateStandardWindow())
    {
    }

    protected MacOSWindowImpl(MacOSPlatform platform, MacOSPlatformOptions options, NSWindow window)
        : base(platform)
    {
        _options = options;
        _window = window;
        var view = new MacOSView(this);
        InitializeNativeView(view);
        _window.ContentView = view;
        _delegate = new WindowDelegate(this);
        _window.Delegate = _delegate;
        _menuExporter = new MacOSWindowMenuExporter(platform.MainMenuManager, _window);
        _window.ReleasedWhenClosed = false;
        _window.AcceptsMouseMovedEvents = true;
        _invalidateTimer = new Timer(_ => Dispatcher.UIThread.Post(Invalidate), null, Timeout.Infinite, Timeout.Infinite);
        Platform.RegisterWindow(this);
        UpdateMetrics(WindowResizeReason.Layout);
    }

    protected override NSWindow? NativeWindow => _window;

    protected override bool TryHandlePlatformPointerEvent(NSEvent eventArgs, RawPointerEventType eventType)
    {
        if (_resizeDrag is not { } resizeDrag)
        {
            return false;
        }

        switch (eventType)
        {
            case RawPointerEventType.Move:
                ApplyResizeDrag(resizeDrag, GetClientPoint(eventArgs));
                return true;
            case RawPointerEventType.LeftButtonUp:
                ApplyResizeDrag(resizeDrag, GetClientPoint(eventArgs));
                _resizeDrag = null;
                return true;
            default:
                return false;
        }
    }

    public Size? FrameSize => new Size(_window.Frame.Width, _window.Frame.Height);

    public PixelPoint Position => PixelPoint.FromPoint(new Point(_window.Frame.X, _window.Frame.Y), RenderScaling);

    public Action<PixelPoint>? PositionChanged { get; set; }

    public Action? Deactivated { get; set; }

    public Action? Activated { get; set; }

    public Size MaxAutoSizeHint => Platform.Screens.AllScreens
        .Select(static screen => screen.Bounds.Size.ToSize(1))
        .OrderByDescending(static size => size.Width + size.Height)
        .FirstOrDefault();

    public void SetTopmost(bool value)
    {
        _window.Level = value ? NSWindowLevel.Floating : NSWindowLevel.Normal;
    }

    public WindowState WindowState
    {
        get => _windowState;
        set
        {
            if (_windowState == value)
            {
                return;
            }

            _windowState = value;
            switch (value)
            {
                case WindowState.Normal:
                    if (_window.IsMiniaturized)
                    {
                        _window.Deminiaturize(null);
                    }

                    if (_window.IsZoomed)
                    {
                        _window.Zoom(null);
                    }
                    break;
                case WindowState.Minimized:
                    _window.Miniaturize(null);
                    break;
                case WindowState.Maximized:
                    if (!_window.IsZoomed)
                    {
                        _window.Zoom(null);
                    }
                    break;
                case WindowState.FullScreen:
                    _window.ToggleFullScreen(null);
                    break;
            }

            WindowStateChanged?.Invoke(_windowState);
        }
    }

    public bool WindowStateGetterIsUsable => false;

    public Action<WindowState>? WindowStateChanged { get; set; }

    public void SetTitle(string? title)
    {
        _window.Title = title ?? string.Empty;
    }

    public void SetParent(IWindowImpl? parent)
    {
        if (parent is MacOSWindowImpl macParent)
        {
            macParent._window.AddChildWindow(_window, NSWindowOrderingMode.Above);
        }
    }

    public void SetEnabled(bool enable)
    {
        _window.IgnoresMouseEvents = !enable;
    }

    public Action? GotInputWhenDisabled { get; set; }

    public void SetWindowDecorations(WindowDecorations enabled)
    {
        _windowDecorations = enabled;
        ApplyStyleMask();
    }

    public void SetIcon(IWindowIconImpl? icon)
    {
        var image = CreateImage(icon)
            ?? Platform.NativeApplication.ApplicationIconImage
            ?? s_placeholderWindowImage;
        _window.MiniWindowImage = image;
        Platform.NativeApplication.ApplicationIconImage = image;
        Platform.NativeApplication.DockTile.Display();
        _window.DockTile.Display();

        if (!ReferenceEquals(_windowIconImage, image))
        {
            _windowIconImage?.Dispose();
            _windowIconImage = ReferenceEquals(image, s_placeholderWindowImage) ? null : image;
        }
    }

    public void ShowTaskbarIcon(bool value)
    {
        Platform.NativeApplication.ActivationPolicy = value
            ? NSApplicationActivationPolicy.Regular
            : NSApplicationActivationPolicy.Accessory;
    }

    public void CanResize(bool value)
    {
        _canResize = value;
        ApplyStyleMask();
    }

    public void SetCanMinimize(bool value)
    {
        _canMinimize = value;
        ApplyStyleMask();
    }

    public void SetCanMaximize(bool value)
    {
        _canMaximize = value;
        ApplyStyleMask();
    }

    public Func<WindowCloseReason, bool>? Closing { get; set; }

    public bool IsClientAreaExtendedToDecorations => _extendClientArea;

    public Action<bool>? ExtendClientAreaToDecorationsChanged { get; set; }

    public bool NeedsManagedDecorations => false;

    public PlatformRequestedDrawnDecoration RequestedDrawnDecorations => default;

    public Thickness ExtendedMargins => _extendClientArea && _extendClientAreaTitleBarHeight > 0
        ? new Thickness(0, _extendClientAreaTitleBarHeight, 0, 0)
        : new Thickness();

    public Thickness OffScreenMargin => new();

    public void BeginMoveDrag(PointerPressedEventArgs e)
    {
        var currentEvent = NSApplication.SharedApplication.CurrentEvent;
        if (currentEvent is not null)
        {
            _window.PerformWindowDrag(currentEvent);
        }
    }

    public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (!_canResize)
        {
            return;
        }

        e.Pointer.Capture(null);

        var currentEvent = NSApplication.SharedApplication.CurrentEvent;
        if (currentEvent is null)
        {
            return;
        }

        _resizeDrag = new ResizeDragState(
            edge,
            _window.Frame,
            ClientSize,
            GetClientPoint(currentEvent),
            new Size(_window.ContentMinSize.Width, _window.ContentMinSize.Height),
            new Size(_window.ContentMaxSize.Width, _window.ContentMaxSize.Height));
    }

    public void Resize(Size clientSize, WindowResizeReason reason = WindowResizeReason.Application)
    {
        _window.SetContentSize(new CGSize(clientSize.Width, clientSize.Height));
        UpdateMetrics(reason);
        Invalidate();
    }

    public void Move(PixelPoint point)
    {
        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;
        _window.SetFrameOrigin(new CGPoint(point.X / scaling, point.Y / scaling));
        PositionChanged?.Invoke(point);
    }

    public void SetMinMaxSize(Size minSize, Size maxSize)
    {
        _window.ContentMinSize = new CGSize(minSize.Width, minSize.Height);
        _window.ContentMaxSize = new CGSize(
            maxSize.Width <= 0 ? nfloat.MaxValue : maxSize.Width,
            maxSize.Height <= 0 ? nfloat.MaxValue : maxSize.Height);
    }

    public void SetExtendClientAreaToDecorationsHint(bool extendIntoClientAreaHint)
    {
        _extendClientArea = extendIntoClientAreaHint;
        ExtendClientAreaToDecorationsChanged?.Invoke(extendIntoClientAreaHint);
    }

    public void SetExtendClientAreaTitleBarHeightHint(double titleBarHeight)
    {
        _extendClientAreaTitleBarHeight = titleBarHeight;
        ExtendClientAreaToDecorationsChanged?.Invoke(_extendClientArea);
    }

    public override IPopupImpl? CreatePopup()
    {
        return _options.OverlayPopups ? null : new MacOSPopupImpl(Platform, _options, this);
    }

    public virtual void Show(bool activate, bool isDialog)
    {
        _isVisible = true;
        _window.MakeKeyAndOrderFront(null);
        if (activate)
        {
            Platform.NativeApplication.ActivateIgnoringOtherApps(true);
            _window.MakeMainWindow();
        }

        _invalidateTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(16));
        Invalidate();
    }

    public void Hide()
    {
        _isVisible = false;
        _invalidateTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _window.OrderOut(null);
    }

    public void Activate()
    {
        Platform.NativeApplication.ActivateIgnoringOtherApps(true);
        _window.MakeKeyAndOrderFront(null);
    }

    public override void Dispose()
    {
        _resizeDrag = null;
        _invalidateTimer.Dispose();
        _menuExporter.Dispose();
        _window.MiniWindowImage = s_placeholderWindowImage;
        if (ReferenceEquals(Platform.NativeApplication.ApplicationIconImage, _windowIconImage))
        {
            Platform.NativeApplication.ApplicationIconImage = s_placeholderWindowImage;
            Platform.NativeApplication.DockTile.Display();
        }

        _windowIconImage?.Dispose();
        _windowIconImage = null;
        Platform.UnregisterWindow(this);
        _window.Close();
        base.Dispose();
    }

    public override object? TryGetFeature(Type featureType)
    {
        if (featureType == typeof(ITopLevelNativeMenuExporter))
        {
            return _menuExporter;
        }

        return base.TryGetFeature(featureType);
    }

    internal void HandleActivated()
    {
        _menuExporter.HandleActivated();
        Activated?.Invoke();
    }

    internal void HandleDeactivated()
    {
        _menuExporter.HandleDeactivated();
        Deactivated?.Invoke();
        NotifyLostFocus();
    }

    internal void HandleMoved()
    {
        PositionChanged?.Invoke(Position);
    }

    internal bool HandleShouldClose()
    {
        return !(Closing?.Invoke(WindowCloseReason.WindowClosing) ?? false);
    }

    internal void HandleClosed()
    {
        _resizeDrag = null;
        _isVisible = false;
        _invalidateTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        NotifyClosed();
    }

    private void ApplyResizeDrag(ResizeDragState resizeDrag, Point currentPoint)
    {
        var frame = MacOSWindowResizeHelper.CalculateFrame(
            resizeDrag.InitialFrame,
            resizeDrag.InitialClientSize,
            resizeDrag.MinClientSize,
            resizeDrag.MaxClientSize,
            resizeDrag.Edge,
            currentPoint - resizeDrag.InitialPointer);
        _window.SetFrame(frame, true);
    }

    internal void HandleWindowStateChanged(WindowState state)
    {
        _windowState = state;
        WindowStateChanged?.Invoke(state);
    }

    private void ApplyStyleMask()
    {
        var style = _windowDecorations == WindowDecorations.None
            ? NSWindowStyle.Borderless
            : NSWindowStyle.Titled | NSWindowStyle.Closable;

        if (_windowDecorations != WindowDecorations.None && _canMinimize)
        {
            style |= NSWindowStyle.Miniaturizable;
        }

        if (_windowDecorations != WindowDecorations.None && _canResize)
        {
            style |= NSWindowStyle.Resizable;
        }

        _window.StyleMask = style;
    }

    private static NSImage? CreateImage(IWindowIconImpl? icon)
    {
        if (icon is null)
        {
            return null;
        }

        using var memoryStream = new MemoryStream();
        icon.Save(memoryStream);
        memoryStream.Position = 0;
        using var data = NSData.FromStream(memoryStream);
        if (data is null)
        {
            return null;
        }

        return new NSImage(data);
    }

    private static NSWindow CreateStandardWindow()
    {
        return new NSWindow(
            new CGRect(200, 200, 1024, 768),
            NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable,
            NSBackingStore.Buffered,
            false);
    }

    private readonly record struct ResizeDragState(
        WindowEdge Edge,
        CGRect InitialFrame,
        Size InitialClientSize,
        Point InitialPointer,
        Size MinClientSize,
        Size MaxClientSize);

    private sealed class WindowDelegate(MacOSWindowImpl owner) : NSWindowDelegate
    {
        private readonly MacOSWindowImpl _owner = owner;

        public override bool WindowShouldClose(NSObject sender)
        {
            return _owner.HandleShouldClose();
        }

        public override void WillClose(NSNotification notification)
        {
            _owner.HandleClosed();
        }

        public override void DidResize(NSNotification notification)
        {
            _owner.UpdateMetrics(WindowResizeReason.Unspecified);
            _owner.Invalidate();
        }

        public override void DidChangeScreen(NSNotification notification)
        {
            _owner.UpdateMetrics(WindowResizeReason.DpiChange);
        }

        public override void DidMove(NSNotification notification)
        {
            _owner.HandleMoved();
        }

        public override void DidBecomeKey(NSNotification notification)
        {
            _owner.HandleActivated();
        }

        public override void DidResignKey(NSNotification notification)
        {
            _owner.HandleDeactivated();
        }

        public override void DidMiniaturize(NSNotification notification)
        {
            _owner.HandleWindowStateChanged(WindowState.Minimized);
        }

        public override void DidDeminiaturize(NSNotification notification)
        {
            _owner.HandleWindowStateChanged(WindowState.Normal);
        }
    }
}