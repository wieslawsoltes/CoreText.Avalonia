using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using System.IO;

namespace CoreText.Avalonia.Tests;

public sealed class CoreTextFontManagerTests
{
    [Fact(Skip = "CoreText font activation from system streams is not reliable in the xUnit macOS bundle host used by dotnet test.")]
    public void CanCreateTypefaceAndShapeSimpleText()
    {
        var manager = new CoreText.Avalonia.CoreTextFontManagerImpl(new CoreText.Avalonia.CoreTextPlatformOptions());
        var fontPath = new[]
        {
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/System/Library/Fonts/Supplemental/Times New Roman.ttf",
            "/System/Library/Fonts/Helvetica.ttc"
        }.FirstOrDefault(File.Exists);

        Assert.False(string.IsNullOrWhiteSpace(fontPath));

        using var fontStream = File.OpenRead(fontPath!);
        Assert.True(manager.TryCreateGlyphTypeface(fontStream, FontSimulations.None, out var platformTypeface));

        using var typefaceHandle = platformTypeface;
        var glyphTypeface = new GlyphTypeface(platformTypeface!);
        using var shaped = new CoreText.Avalonia.CoreTextTextShaper()
            .ShapeText("office".AsMemory(), new TextShaperOptions(glyphTypeface, 18));

        Assert.True(shaped.Length > 0);
        Assert.True(shaped[0].GlyphAdvance > 0);
    }
}
