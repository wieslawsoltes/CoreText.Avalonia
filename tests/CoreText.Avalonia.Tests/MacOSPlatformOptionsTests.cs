namespace CoreText.Avalonia.Tests;

public sealed class MacOSPlatformOptionsTests
{
    private sealed class TestApplication : global::Avalonia.Application
    {
    }

    [Fact]
    public void DefaultsPreferMetalThenOpenGlThenSoftware()
    {
        var options = new MacOS.Avalonia.MacOSPlatformOptions();

        Assert.Equal(
            [
                MacOS.Avalonia.MacOSRenderingMode.Metal,
                MacOS.Avalonia.MacOSRenderingMode.OpenGl,
                MacOS.Avalonia.MacOSRenderingMode.Software
            ],
            options.RenderingModes);
        Assert.False(options.OverlayPopups);
        Assert.True(options.ShowInDock);
        Assert.False(options.DisableDefaultApplicationMenuItems);
        Assert.False(options.DisableNativeMenus);
        Assert.False(options.DisableSetProcessName);
        Assert.False(options.DisableAvaloniaAppDelegate);
        Assert.True(options.AppSandboxEnabled);
        Assert.Null(options.CustomPlatformGraphics);
    }

    [Fact]
    public void UseMacOSRegistersDeferredWindowingSubsystem()
    {
        var builder = global::Avalonia.AppBuilder.Configure<TestApplication>();

        var result = global::Avalonia.MacOSApplicationExtensions.UseMacOS(builder);

        Assert.Same(builder, result);
        Assert.Equal("MacOS", builder.WindowingSubsystemName);
        Assert.NotNull(builder.WindowingSubsystemInitializer);
        Assert.NotNull(builder.RuntimePlatformServicesInitializer);
    }
}