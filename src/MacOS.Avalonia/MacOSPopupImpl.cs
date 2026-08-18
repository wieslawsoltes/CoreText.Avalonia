using Avalonia.Controls.Primitives.PopupPositioning;

namespace MacOS.Avalonia;

internal sealed class MacOSPopupImpl : MacOSWindowImpl, IPopupImpl
{
    private readonly ITopLevelImpl _parent;

    public MacOSPopupImpl(MacOSPlatform platform, MacOSPlatformOptions options, ITopLevelImpl parent)
        : base(platform, options, new NSPanel(
            new CGRect(0, 0, 400, 300),
            NSWindowStyle.Borderless,
            NSBackingStore.Buffered,
            false))
    {
        _parent = parent;
        PopupPositioner = new ManagedPopupPositioner(new ManagedPopupPositionerPopupImplHelper(parent, MoveResize));

        if (parent is IWindowImpl parentWindow)
        {
            SetParent(parentWindow);
        }

        if (NativeWindow is NSPanel panel)
        {
            panel.FloatingPanel = true;
            panel.BecomesKeyOnlyIfNeeded = true;
            panel.HidesOnDeactivate = false;
        }
    }

    public IPopupPositioner PopupPositioner { get; }

    public override void Show(bool activate, bool isDialog)
    {
        var nativeWindow = NativeWindow;
        if (nativeWindow is null)
        {
            return;
        }

        if (activate)
        {
            nativeWindow.MakeKeyAndOrderFront(null);
            Platform.NativeApplication.ActivateIgnoringOtherApps(true);
        }
        else
        {
            nativeWindow.OrderFront(null);
        }

        Invalidate();
    }

    public void SetWindowManagerAddShadowHint(bool enabled)
    {
        if (NativeWindow is NSPanel panel)
        {
            panel.HasShadow = enabled;
        }
    }

    public void TakeFocus()
    {
        if (NativeWindow is not null)
        {
            NativeWindow.MakeKeyWindow();
            NativeWindow.MakeFirstResponder(NativeView);
            return;
        }

        if (_parent is MacOSWindowImpl parentWindow)
        {
            parentWindow.Activate();
        }
    }

    private void MoveResize(PixelPoint position, Size size, double scaling)
    {
        Move(position);
        Resize(size, WindowResizeReason.Layout);
    }
}