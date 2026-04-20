namespace CoreText.Avalonia;

public enum CoreTextSurfaceMode
{
    Auto,
    Software,
    Metal
}

public sealed class CoreTextPlatformOptions
{
    public CoreTextSurfaceMode SurfaceMode { get; set; } = CoreTextSurfaceMode.Auto;

    public bool EnableCoreImageEffects { get; set; } = true;

    public bool EnableSubpixelPositioning { get; set; } = true;

    public bool EnableFontSmoothing { get; set; } = true;

    public bool PreferPlatformFontFallback { get; set; } = true;
}
