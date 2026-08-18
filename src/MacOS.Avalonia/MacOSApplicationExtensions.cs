using MacOS.Avalonia;

namespace Avalonia;

public static class MacOSApplicationExtensions
{
    public static AppBuilder UseMacOS(this AppBuilder builder, MacOSPlatformOptions? options = null)
    {
        var effectiveOptions = options ?? new MacOSPlatformOptions();

        builder = builder.UseStandardRuntimePlatformSubsystem();
        builder = builder.With(effectiveOptions);
        builder = builder.UseWindowingSubsystem(() => MacOSPlatform.Initialize(effectiveOptions), "MacOS");

        return builder;
    }
}