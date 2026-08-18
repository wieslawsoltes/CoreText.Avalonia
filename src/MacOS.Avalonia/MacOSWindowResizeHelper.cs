using Avalonia.Controls;

namespace MacOS.Avalonia;

internal static class MacOSWindowResizeHelper
{
    public static CGRect CalculateFrame(
        CGRect initialFrame,
        Size initialClientSize,
        Size minClientSize,
        Size maxClientSize,
        WindowEdge edge,
        Vector clientDelta)
    {
        var chromeWidth = Math.Max(0d, initialFrame.Width - initialClientSize.Width);
        var chromeHeight = Math.Max(0d, initialFrame.Height - initialClientSize.Height);

        var minWidth = Math.Max(1d, minClientSize.Width);
        var minHeight = Math.Max(1d, minClientSize.Height);
        var maxWidth = Math.Max(minWidth, NormalizeMax(maxClientSize.Width));
        var maxHeight = Math.Max(minHeight, NormalizeMax(maxClientSize.Height));

        var contentWidth = Clamp(initialClientSize.Width + GetWidthDelta(edge, clientDelta.X), minWidth, maxWidth);
        var contentHeight = Clamp(initialClientSize.Height + GetHeightDelta(edge, clientDelta.Y), minHeight, maxHeight);

        var frameX = (double)initialFrame.X;
        var frameY = (double)initialFrame.Y;

        if (AffectsLeft(edge))
        {
            frameX += initialClientSize.Width - contentWidth;
        }

        if (AffectsBottom(edge))
        {
            frameY -= contentHeight - initialClientSize.Height;
        }

        return new CGRect(frameX, frameY, contentWidth + chromeWidth, contentHeight + chromeHeight);
    }

    private static double GetWidthDelta(WindowEdge edge, double deltaX)
    {
        return edge switch
        {
            WindowEdge.West or WindowEdge.NorthWest or WindowEdge.SouthWest => -deltaX,
            WindowEdge.East or WindowEdge.NorthEast or WindowEdge.SouthEast => deltaX,
            _ => 0d
        };
    }

    private static double GetHeightDelta(WindowEdge edge, double deltaY)
    {
        return edge switch
        {
            WindowEdge.North or WindowEdge.NorthWest or WindowEdge.NorthEast => -deltaY,
            WindowEdge.South or WindowEdge.SouthWest or WindowEdge.SouthEast => deltaY,
            _ => 0d
        };
    }

    private static bool AffectsLeft(WindowEdge edge)
    {
        return edge is WindowEdge.West or WindowEdge.NorthWest or WindowEdge.SouthWest;
    }

    private static bool AffectsBottom(WindowEdge edge)
    {
        return edge is WindowEdge.South or WindowEdge.SouthWest or WindowEdge.SouthEast;
    }

    private static double NormalizeMax(double value)
    {
        return value <= 0 || double.IsInfinity(value) ? double.MaxValue : value;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(value, max));
    }
}