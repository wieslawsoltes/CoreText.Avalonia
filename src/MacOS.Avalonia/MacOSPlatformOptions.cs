using Avalonia.Platform;

namespace MacOS.Avalonia;

public enum MacOSRenderingMode
{
    Metal,
    OpenGl,
    Software
}

public sealed class MacOSPlatformOptions
{
    public IReadOnlyList<MacOSRenderingMode> RenderingModes { get; set; } =
    [
        MacOSRenderingMode.Metal,
        MacOSRenderingMode.OpenGl,
        MacOSRenderingMode.Software
    ];

    public bool OverlayPopups { get; set; }

    public bool ShowInDock { get; set; } = true;

    public bool DisableDefaultApplicationMenuItems { get; set; }

    public bool DisableNativeMenus { get; set; }

    public bool DisableSetProcessName { get; set; }

    public bool DisableAvaloniaAppDelegate { get; set; }

    public bool AppSandboxEnabled { get; set; } = true;

    public IPlatformGraphics? CustomPlatformGraphics { get; set; }
}