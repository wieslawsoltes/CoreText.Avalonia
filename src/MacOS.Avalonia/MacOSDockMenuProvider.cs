namespace MacOS.Avalonia;

internal sealed class MacOSDockMenuProvider(MacOSPlatformOptions options) : IDisposable
{
    private readonly MacOSPlatformOptions _options = options;
    private NSMenu? _nativeMenu;

    public NSMenu? GetDockMenu()
    {
        if (_options.DisableNativeMenus || Application.Current is not { } application)
        {
            ClearNativeMenu();
            return null;
        }

        var dockMenu = NativeDock.GetMenu(application);
        if (dockMenu is not { Items.Count: > 0 })
        {
            ClearNativeMenu();
            return null;
        }

        ClearNativeMenu();
        _nativeMenu = MacOSNativeMenuBuilder.CreateNativeMenu(dockMenu);
        return _nativeMenu;
    }

    public void Dispose()
    {
        ClearNativeMenu();
    }

    private void ClearNativeMenu()
    {
        _nativeMenu?.Dispose();
        _nativeMenu = null;
    }
}