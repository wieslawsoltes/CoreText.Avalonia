using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace CoreText.Avalonia.Sample;

internal sealed class App : Application
{
    public override void Initialize()
    {
        Name = "CoreText.Avalonia.Sample";
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(Program.SurfaceMode);
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static AppBuilder BuildAvaloniaApp(
        CoreTextPlatformOptions rendererOptions,
        AvaloniaNativePlatformOptions nativeOptions)
    {
        return AppBuilder.Configure<App>()
            .UseCoreTextNative(rendererOptions, nativeOptions)
            .WithInterFont()
            .LogToTrace();
    }
}
