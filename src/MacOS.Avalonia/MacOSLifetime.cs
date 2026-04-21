using Avalonia.Controls.ApplicationLifetimes;

namespace MacOS.Avalonia;

internal sealed class MacOSActivatableLifetime(NSApplication application) : ActivatableLifetimeBase
{
    private readonly NSApplication _application = application;

    public override bool TryLeaveBackground()
    {
        _application.Unhide(null);
        _application.ActivateIgnoringOtherApps(true);
        return true;
    }

    public override bool TryEnterBackground()
    {
        _application.Hide(null);
        return true;
    }

    public void NotifyActivated(ActivationKind kind)
    {
        OnActivated(kind);
    }

    public void NotifyDeactivated(ActivationKind kind)
    {
        OnDeactivated(kind);
    }
}

internal sealed class MacOSPlatformLifetimeEvents : IPlatformLifetimeEventsImpl
{
    public event EventHandler<ShutdownRequestedEventArgs>? ShutdownRequested;

    public bool TryRequestShutdown()
    {
        if (ShutdownRequested is null)
        {
            return true;
        }

        var args = new ShutdownRequestedEventArgs();
        ShutdownRequested(this, args);
        return !args.Cancel;
    }
}

internal sealed class MacOSApplicationDelegate : NSApplicationDelegate
{
    private readonly MacOSPlatformLifetimeEvents _lifetimeEvents;
    private readonly MacOSActivatableLifetime _activatableLifetime;

    public MacOSApplicationDelegate(MacOSPlatformLifetimeEvents lifetimeEvents, MacOSActivatableLifetime activatableLifetime)
    {
        _lifetimeEvents = lifetimeEvents;
        _activatableLifetime = activatableLifetime;
    }

    public override NSApplicationTerminateReply ApplicationShouldTerminate(NSApplication sender)
    {
        return _lifetimeEvents.TryRequestShutdown()
            ? NSApplicationTerminateReply.Now
            : NSApplicationTerminateReply.Cancel;
    }

    public override bool ApplicationShouldHandleReopen(NSApplication sender, bool hasVisibleWindows)
    {
        _activatableLifetime.NotifyActivated(ActivationKind.Reopen);
        return true;
    }

    public override void DidBecomeActive(NSNotification notification)
    {
        _activatableLifetime.NotifyActivated(ActivationKind.Background);
    }

    public override void DidResignActive(NSNotification notification)
    {
        _activatableLifetime.NotifyDeactivated(ActivationKind.Background);
    }
}
