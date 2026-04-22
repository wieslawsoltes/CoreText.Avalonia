using Avalonia.Controls;

namespace MacOS.Avalonia;

internal sealed class MacOSEmbeddableWindowImpl : MacOSTopLevelImpl, IWindowImpl
{
    private static readonly Size s_defaultClientSize = new(640, 480);
    private PixelPoint _position;
    private Size _maxSize = Size.Infinity;
    private Size _minSize = new(1, 1);
    private WindowState _windowState;
    private NSView? _parentView;
    private bool _isVisible = true;

    public MacOSEmbeddableWindowImpl(MacOSPlatform platform)
        : base(platform)
    {
        InitializeNativeView(new MacOSView(this));
        ApplyViewFrame(s_defaultClientSize);
    }

    protected override NSWindow? NativeWindow => null;

    public Size? FrameSize => ClientSize;

    public PixelPoint Position => _position;

    public Action<PixelPoint>? PositionChanged { get; set; }

    public Action? Deactivated { get; set; }

    public Action? Activated { get; set; }

    public Size MaxAutoSizeHint => new(
        double.IsInfinity(_maxSize.Width) ? 4096 : _maxSize.Width,
        double.IsInfinity(_maxSize.Height) ? 4096 : _maxSize.Height);

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
            WindowStateChanged?.Invoke(value);
        }
    }

    public bool WindowStateGetterIsUsable => true;

    public Action<WindowState>? WindowStateChanged { get; set; }

    public Action? GotInputWhenDisabled { get; set; }

    public Func<WindowCloseReason, bool>? Closing { get; set; }

    public bool IsClientAreaExtendedToDecorations => false;

    public Action<bool>? ExtendClientAreaToDecorationsChanged { get; set; }

    public bool NeedsManagedDecorations => false;

    public PlatformRequestedDrawnDecoration RequestedDrawnDecorations => default;

    public Thickness ExtendedMargins => default;

    public Thickness OffScreenMargin => default;

    public void Show(bool activate, bool isDialog)
    {
        _isVisible = true;
        NativeView.Hidden = false;
        if (NativeView.Window is { } window)
        {
            window.AcceptsMouseMovedEvents = true;
        }
        Invalidate();
        if (activate)
        {
            Activate();
        }
    }

    public void Hide()
    {
        _isVisible = false;
        NativeView.Hidden = true;
        Deactivated?.Invoke();
    }

    public void Activate()
    {
        NativeView.Window?.MakeFirstResponder(NativeView);
        Activated?.Invoke();
    }

    public void SetTopmost(bool value)
    {
    }

    public void SetTitle(string? title)
    {
    }

    public void SetParent(IWindowImpl? parent)
    {
        var parentView = (parent as MacOSTopLevelImpl)?.PlatformView;
        if (ReferenceEquals(_parentView, parentView))
        {
            return;
        }

        if (NativeView.Superview is not null)
        {
            NativeView.RemoveFromSuperview();
        }

        _parentView = parentView;
        if (_parentView is not null)
        {
            _parentView.AddSubview(NativeView);
            NativeView.Hidden = !_isVisible;
            if (NativeView.Window is { } window)
            {
                window.AcceptsMouseMovedEvents = true;
            }
            ApplyViewFrame(ClientSize.Width > 0 && ClientSize.Height > 0 ? ClientSize : s_defaultClientSize);
        }
    }

    public void SetEnabled(bool enable)
    {
    }

    public void SetWindowDecorations(WindowDecorations enabled)
    {
    }

    public void SetIcon(IWindowIconImpl? icon)
    {
    }

    public void ShowTaskbarIcon(bool value)
    {
    }

    public void CanResize(bool value)
    {
    }

    public void SetCanMinimize(bool value)
    {
    }

    public void SetCanMaximize(bool value)
    {
    }

    public void BeginMoveDrag(PointerPressedEventArgs e)
    {
    }

    public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs e)
    {
    }

    public void Resize(Size clientSize, WindowResizeReason reason = WindowResizeReason.Application)
    {
        var clampedSize = ClampClientSize(clientSize);
        NativeView.SetFrameSize(new CGSize(clampedSize.Width, clampedSize.Height));
        UpdateMetrics(reason);
        Invalidate();
    }

    public void Move(PixelPoint point)
    {
        _position = point;
        ApplyViewOrigin();
        PositionChanged?.Invoke(point);
    }

    public void SetMinMaxSize(Size minSize, Size maxSize)
    {
        _minSize = new Size(Math.Max(minSize.Width, 1), Math.Max(minSize.Height, 1));
        _maxSize = new Size(
            maxSize.Width <= 0 ? double.PositiveInfinity : maxSize.Width,
            maxSize.Height <= 0 ? double.PositiveInfinity : maxSize.Height);
        Resize(ClientSize, WindowResizeReason.Layout);
    }

    public void SetExtendClientAreaToDecorationsHint(bool extendIntoClientAreaHint)
    {
        ExtendClientAreaToDecorationsChanged?.Invoke(false);
    }

    public void SetExtendClientAreaTitleBarHeightHint(double titleBarHeight)
    {
    }

    public override void Dispose()
    {
        if (Closing?.Invoke(WindowCloseReason.Undefined) == true)
        {
            return;
        }

        NotifyClosed();
        base.Dispose();
    }

    private void ApplyViewFrame(Size clientSize)
    {
        ApplyViewOrigin();
        Resize(clientSize, WindowResizeReason.Layout);
    }

    private void ApplyViewOrigin()
    {
        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;
        NativeView.SetFrameOrigin(new CGPoint(_position.X / scaling, _position.Y / scaling));
    }

    private Size ClampClientSize(Size clientSize)
    {
        return new Size(
            Math.Clamp(clientSize.Width, _minSize.Width, double.IsInfinity(_maxSize.Width) ? clientSize.Width : _maxSize.Width),
            Math.Clamp(clientSize.Height, _minSize.Height, double.IsInfinity(_maxSize.Height) ? clientSize.Height : _maxSize.Height));
    }
}