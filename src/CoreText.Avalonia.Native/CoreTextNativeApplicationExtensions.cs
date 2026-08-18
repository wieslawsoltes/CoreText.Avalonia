using CoreText.Avalonia;
using MacOS.Avalonia;

namespace Avalonia;

public static class CoreTextNativeApplicationExtensions
{
    public static AppBuilder UseCoreTextNative(
        this AppBuilder builder,
        CoreTextPlatformOptions? rendererOptions = null,
        MacOSPlatformOptions? nativeOptions = null)
    {
        builder = builder.UseMacOS(nativeOptions);
        builder = builder.UseCoreText(rendererOptions);
        return builder;
    }
}
