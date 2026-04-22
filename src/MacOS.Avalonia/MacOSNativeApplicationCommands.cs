namespace MacOS.Avalonia;

internal sealed class MacOSNativeApplicationCommands(NSApplication application)
{
    private readonly NSApplication _application = application;

    public void ShowApp()
    {
        _application.Unhide(null!);
        _application.ActivateIgnoringOtherApps(true);
    }

    public void HideApp()
    {
        _application.Hide(null!);
    }

    public void ShowAll()
    {
        _application.UnhideAllApplications(null!);
    }

    public void HideOthers()
    {
        _application.HideOtherApplications(null!);
    }
}