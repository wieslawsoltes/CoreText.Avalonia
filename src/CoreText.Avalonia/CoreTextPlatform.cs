using Avalonia.Platform;

namespace CoreText.Avalonia;

public static class CoreTextPlatform
{
    public static void Initialize(CoreTextPlatformOptions? options = null)
    {
        var effectiveOptions = options ?? AvaloniaLocator.Current.GetService<CoreTextPlatformOptions>() ?? new CoreTextPlatformOptions();
        var renderInterface = new CoreTextPlatformRenderInterface(effectiveOptions);

        AvaloniaLocator.CurrentMutable
            .Bind<IPlatformRenderInterface>().ToConstant(renderInterface)
            .Bind<IFontManagerImpl>().ToConstant(new CoreTextFontManagerImpl(effectiveOptions));
    }

    public static Vector DefaultDpi => new(96, 96);
}
