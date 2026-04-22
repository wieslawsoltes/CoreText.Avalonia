using Avalonia;
using MacOS.Avalonia;

namespace CoreText.Avalonia.Sample;

internal static class Program
{
    public static CoreTextSurfaceMode SurfaceMode { get; private set; } = CoreTextSurfaceMode.Auto;

    [STAThread]
    private static int Main(string[] args)
    {
        SurfaceMode = ParseSurfaceMode(args);
        var launchArgs = args
            .Where(static x => !x.StartsWith("--surface=", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var rendererOptions = new CoreTextPlatformOptions
        {
            SurfaceMode = SurfaceMode
        };

        var nativeOptions = new MacOS.Avalonia.MacOSPlatformOptions
        {
            RenderingModes = SurfaceMode switch
            {
                CoreTextSurfaceMode.Software => new[] { MacOSRenderingMode.Software },
                CoreTextSurfaceMode.Metal => new[] { MacOSRenderingMode.Metal, MacOSRenderingMode.Software },
                _ => new[] { MacOSRenderingMode.Metal, MacOSRenderingMode.OpenGl, MacOSRenderingMode.Software }
            }
        };

        return App.BuildAvaloniaApp(rendererOptions, nativeOptions)
            .StartWithClassicDesktopLifetime(launchArgs);
    }

    private static CoreTextSurfaceMode ParseSurfaceMode(IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--surface=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return arg["--surface=".Length..].ToLowerInvariant() switch
            {
                "software" => CoreTextSurfaceMode.Software,
                "metal" => CoreTextSurfaceMode.Metal,
                _ => CoreTextSurfaceMode.Auto
            };
        }

        return CoreTextSurfaceMode.Auto;
    }
}
