namespace MacOS.Avalonia;

internal sealed class MacOSPlatformSettings : DefaultPlatformSettings
{
    public override PlatformColorValues GetColorValues()
    {
        var appearance = NSUserDefaults.StandardUserDefaults.StringForKey("AppleInterfaceStyle");
        return new PlatformColorValues
        {
            ThemeVariant = string.Equals(appearance, "Dark", StringComparison.OrdinalIgnoreCase)
                ? PlatformThemeVariant.Dark
                : PlatformThemeVariant.Light
        };
    }
}
