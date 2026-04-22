namespace MacOS.Avalonia;

internal sealed class MacOSTopLevelHandle : INativePlatformHandleSurface, IMacOSTopLevelPlatformHandle
{
    private readonly MacOSTopLevelImpl _topLevel;

    public MacOSTopLevelHandle(MacOSTopLevelImpl topLevel)
    {
        _topLevel = topLevel;
    }

    public IntPtr Handle => _topLevel.NativeViewHandle;

    public string? HandleDescriptor => "NSView";

    public PixelSize Size => _topLevel.PixelSize;

    public double Scaling => _topLevel.RenderScaling;

    public IntPtr NSView => _topLevel.NativeViewHandle;

    public IntPtr GetNSViewRetained() => _topLevel.NativeViewHandle;

    public IntPtr NSWindow => _topLevel.NativeWindowHandle;

    public IntPtr GetNSWindowRetained() => _topLevel.NativeWindowHandle;
}
