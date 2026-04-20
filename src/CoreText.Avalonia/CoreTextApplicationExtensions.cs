using Avalonia.Controls;
using Avalonia.Platform;

namespace Avalonia;

public static class CoreTextApplicationExtensions
{
    public static AppBuilder UseCoreText(this AppBuilder builder, CoreText.Avalonia.CoreTextPlatformOptions? options = null)
    {
        var effectiveOptions = options ?? new CoreText.Avalonia.CoreTextPlatformOptions();

        builder = builder.With(effectiveOptions);

        builder = builder.UseRenderingSubsystem(
            () => CoreText.Avalonia.CoreTextPlatform.Initialize(effectiveOptions),
            "CoreText");

        builder = builder.UseTextShapingSubsystem(
            () => AvaloniaLocator.CurrentMutable.Bind<ITextShaperImpl>().ToConstant(new CoreText.Avalonia.CoreTextTextShaper()),
            "CoreText");

        return builder;
    }
}
