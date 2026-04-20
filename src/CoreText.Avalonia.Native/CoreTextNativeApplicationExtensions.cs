using CoreText.Avalonia;

namespace Avalonia;

public static class CoreTextNativeApplicationExtensions
{
    public static AppBuilder UseCoreTextNative(
        this AppBuilder builder,
        CoreTextPlatformOptions? rendererOptions = null,
        AvaloniaNativePlatformOptions? nativeOptions = null)
    {
        builder = builder.With(nativeOptions ?? new AvaloniaNativePlatformOptions());
        builder = builder.UseAvaloniaNative();
        builder = builder.UseCoreText(rendererOptions);
        return builder;
    }
}
