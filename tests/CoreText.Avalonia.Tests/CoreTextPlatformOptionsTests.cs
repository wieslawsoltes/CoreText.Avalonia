namespace CoreText.Avalonia.Tests;

public sealed class CoreTextPlatformOptionsTests
{
    [Fact]
    public void DefaultsMatchStandaloneBackendExpectations()
    {
        var options = new CoreText.Avalonia.CoreTextPlatformOptions();

        Assert.Equal(CoreText.Avalonia.CoreTextSurfaceMode.Auto, options.SurfaceMode);
        Assert.True(options.EnableCoreImageEffects);
        Assert.True(options.EnableSubpixelPositioning);
        Assert.True(options.EnableFontSmoothing);
        Assert.True(options.PreferPlatformFontFallback);
    }
}
