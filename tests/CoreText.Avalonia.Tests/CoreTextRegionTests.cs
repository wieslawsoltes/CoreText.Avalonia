using Avalonia;
using Avalonia.Platform;
using System.Reflection;

namespace CoreText.Avalonia.Tests;

public sealed class CoreTextRegionTests
{
    [Fact]
    public void RegionTracksBoundsAndPointContainment()
    {
        using var region = new CoreText.Avalonia.CoreTextRegionImpl();

        region.AddRect(CreateRect(10, 20, 50, 80));
        region.AddRect(CreateRect(40, 10, 90, 30));

        Assert.False(region.IsEmpty);
        Assert.True(region.Contains(new Point(25, 25)));
        Assert.False(region.Contains(new Point(4, 4)));

        var bounds = region.Bounds;
        Assert.Equal(10, Read(bounds, "Left"));
        Assert.Equal(10, Read(bounds, "Top"));
        Assert.Equal(90, Read(bounds, "Right"));
        Assert.Equal(80, Read(bounds, "Bottom"));
    }

    private static LtrbPixelRect CreateRect(int left, int top, int right, int bottom)
    {
        var rect = default(LtrbPixelRect);
        Write(ref rect, "Left", left);
        Write(ref rect, "Top", top);
        Write(ref rect, "Right", right);
        Write(ref rect, "Bottom", bottom);
        return rect;
    }

    private static void Write(ref LtrbPixelRect rect, string name, int value)
    {
        var field = typeof(LtrbPixelRect).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(LtrbPixelRect).FullName, name);
        field.SetValueDirect(__makeref(rect), value);
    }

    private static int Read(LtrbPixelRect rect, string name)
    {
        var field = typeof(LtrbPixelRect).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(LtrbPixelRect).FullName, name);
        return (int)(field.GetValue(rect) ?? 0);
    }
}
