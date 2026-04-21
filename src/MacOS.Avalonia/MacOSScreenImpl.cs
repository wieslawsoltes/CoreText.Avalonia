using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MacOS.Avalonia;

internal sealed class MacOSScreenImpl : IScreenImpl
{
    public int ScreenCount => NSScreen.Screens.Length;

    public IReadOnlyList<Screen> AllScreens => NSScreen.Screens
        .Select(static (screen, index) => (Screen)new MacOSScreen(screen, index))
        .ToArray();

    public Action? Changed { get; set; }

    public Screen? ScreenFromWindow(IWindowBaseImpl window)
    {
        return ScreenFromRect(new PixelRect(window.Position, PixelSize.FromSize(window.ClientSize, window.RenderScaling)));
    }

    public Screen? ScreenFromTopLevel(ITopLevelImpl topLevel)
    {
        if (topLevel is IWindowImpl window)
        {
            return ScreenFromWindow(window);
        }

        return ScreenFromRect(new PixelRect(topLevel.PointToScreen(default), PixelSize.FromSize(topLevel.ClientSize, topLevel.RenderScaling)));
    }

    public Screen? ScreenFromPoint(PixelPoint point)
    {
        return AllScreens.FirstOrDefault(screen => screen.Bounds.Contains(point));
    }

    public Screen? ScreenFromRect(PixelRect rect)
    {
        Screen? bestScreen = null;
        var bestArea = -1;

        foreach (var screen in AllScreens)
        {
            var area = IntersectionArea(screen.Bounds, rect);
            if (area > bestArea)
            {
                bestArea = area;
                bestScreen = screen;
            }
        }

        return bestScreen ?? AllScreens.FirstOrDefault();
    }

    public Task<bool> RequestScreenDetails() => Task.FromResult(true);

    private static int IntersectionArea(PixelRect left, PixelRect right)
    {
        var x1 = Math.Max(left.X, right.X);
        var y1 = Math.Max(left.Y, right.Y);
        var x2 = Math.Min(left.Right, right.Right);
        var y2 = Math.Min(left.Bottom, right.Bottom);

        if (x2 <= x1 || y2 <= y1)
        {
            return 0;
        }

        return (x2 - x1) * (y2 - y1);
    }
}

internal sealed class MacOSScreen : PlatformScreen
{
    public MacOSScreen(NSScreen screen, int index)
        : base(new PlatformHandle(new IntPtr(index + 1), "NSScreen"))
    {
        var scaling = screen.BackingScaleFactor;
        var frame = screen.Frame;
        var visibleFrame = screen.VisibleFrame;

        DisplayName = screen.LocalizedName;
        Scaling = scaling;
        Bounds = new PixelRect(
            (int)Math.Round(frame.X * scaling),
            (int)Math.Round(frame.Y * scaling),
            (int)Math.Round(frame.Width * scaling),
            (int)Math.Round(frame.Height * scaling));
        WorkingArea = new PixelRect(
            (int)Math.Round(visibleFrame.X * scaling),
            (int)Math.Round(visibleFrame.Y * scaling),
            (int)Math.Round(visibleFrame.Width * scaling),
            (int)Math.Round(visibleFrame.Height * scaling));
        CurrentOrientation = frame.Width >= frame.Height ? ScreenOrientation.Landscape : ScreenOrientation.Portrait;
        IsPrimary = index == 0;
    }
}
