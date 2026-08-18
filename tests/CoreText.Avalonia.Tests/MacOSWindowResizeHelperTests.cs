using Avalonia;
using Avalonia.Controls;

namespace CoreText.Avalonia.Tests;

public sealed class MacOSWindowResizeHelperTests
{
    [Fact]
    public void EastEdge_GrowsFrameWidth_AndPreservesOrigin()
    {
        var frame = MacOS.Avalonia.MacOSWindowResizeHelper.CalculateFrame(
            new CGRect(100, 200, 420, 330),
            new Size(400, 300),
            new Size(100, 100),
            new Size(1000, 1000),
            WindowEdge.East,
            new Vector(25, 0));

        AssertFrame(frame, 100, 200, 445, 330);
    }

    [Fact]
    public void WestEdge_ClampsToMinimum_AndMovesOriginByAppliedDelta()
    {
        var frame = MacOS.Avalonia.MacOSWindowResizeHelper.CalculateFrame(
            new CGRect(100, 200, 420, 330),
            new Size(400, 300),
            new Size(380, 100),
            new Size(1000, 1000),
            WindowEdge.West,
            new Vector(50, 0));

        AssertFrame(frame, 120, 200, 400, 330);
    }

    [Fact]
    public void SouthEdge_GrowsFrameHeight_AndMovesOriginDownward()
    {
        var frame = MacOS.Avalonia.MacOSWindowResizeHelper.CalculateFrame(
            new CGRect(100, 200, 420, 330),
            new Size(400, 300),
            new Size(100, 100),
            new Size(1000, 1000),
            WindowEdge.South,
            new Vector(0, 30));

        AssertFrame(frame, 100, 170, 420, 360);
    }

    [Fact]
    public void NorthEastCorner_UsesCombinedWidthAndHeightDeltas()
    {
        var frame = MacOS.Avalonia.MacOSWindowResizeHelper.CalculateFrame(
            new CGRect(100, 200, 420, 330),
            new Size(400, 300),
            new Size(100, 100),
            new Size(1000, 1000),
            WindowEdge.NorthEast,
            new Vector(40, 25));

        AssertFrame(frame, 100, 200, 460, 305);
    }

    private static void AssertFrame(CGRect frame, double x, double y, double width, double height)
    {
        Assert.Equal(x, (double)frame.X, 6);
        Assert.Equal(y, (double)frame.Y, 6);
        Assert.Equal(width, (double)frame.Width, 6);
        Assert.Equal(height, (double)frame.Height, 6);
    }
}