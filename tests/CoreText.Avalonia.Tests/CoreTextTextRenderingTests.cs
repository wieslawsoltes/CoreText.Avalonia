using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Fonts.Inter;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;

namespace CoreText.Avalonia.Tests;

public sealed class CoreTextTextRenderingTests
{
    [Fact]
    public void CTRun_StringIndices_AreReadAs64BitValues()
    {
        using var text = new CoreText.Avalonia.CoreTextString("Probe");
        var fontHandle = IntPtr.Zero;

        foreach (var familyName in new[] { "Helvetica Neue", ".AppleSystemUIFont", "Helvetica", "Arial" })
        {
            fontHandle = CoreText.Avalonia.CoreTextNative.CreateStyledFont(familyName, 16, FontWeight.Normal, italic: false);

            if (fontHandle != IntPtr.Zero)
            {
                break;
            }
        }

        Assert.NotEqual(IntPtr.Zero, fontHandle);

        try
        {
            using var attributedString = CoreText.Avalonia.CoreTextNative.CreateAttributedString(text.Handle, fontHandle, useContextColor: false);
            var line = CoreText.Avalonia.CoreTextNative.CTLineCreateWithAttributedString(attributedString.Handle);
            Assert.NotEqual(IntPtr.Zero, line);

            try
            {
                var clusters = new List<int>();

                foreach (var run in CoreText.Avalonia.CoreTextNative.GetGlyphRuns(line))
                {
                    var glyphCount = (int)CoreText.Avalonia.CoreTextNative.CTRunGetGlyphCount(run);
                    var stringIndices = new nint[glyphCount];
                    CoreText.Avalonia.CoreTextNative.CTRunGetStringIndices(run, CoreText.Avalonia.CoreTextNative.CFRange.All, stringIndices);
                    clusters.AddRange(stringIndices.Select(static x => (int)x));
                }

                Assert.Equal(new[] { 0, 1, 2, 3, 4 }, clusters);
            }
            finally
            {
                CoreText.Avalonia.CoreTextNative.CFRelease(line);
            }
        }
        finally
        {
            CoreText.Avalonia.CoreTextNative.CFRelease(fontHandle);
        }
    }

    [Fact(Skip = "Real TextLayout integration is validated by the sample app; the xUnit macOS bundle host still cannot activate the required CoreText-backed glyph typeface.")]
    public void TextLayout_CanMeasureSimpleAsciiText()
    {
        using var scope = AvaloniaLocator.EnterScope();
        BindTextServices();
        var typeface = CreateInterTypeface();
        using var layout = new TextLayout(
            "FormattedText preview",
            typeface,
            18,
            Brushes.Black,
            maxWidth: 400);

        Assert.NotEmpty(layout.TextLines);
        Assert.True(layout.Width > 0);
        Assert.True(layout.Height > 0);
    }

    [Fact(Skip = "Manual glyph-run rendering through family-based typeface activation is not reliable in the xUnit macOS bundle host used by dotnet test.")]
    public void ManualGlyphRunCanRenderAsciiText()
    {
        using var scope = AvaloniaLocator.EnterScope();
        BindTextServices();
        var glyphTypeface = CreateGlyphTypeface();
        var glyphInfos = CreateGlyphInfos(glyphTypeface, "Core", 28);

        using var bitmap = new CoreText.Avalonia.CoreTextBitmapImpl(
            new PixelSize(180, 64),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using (var context = (CoreText.Avalonia.CoreTextDrawingContextImpl)bitmap.CreateDrawingContext())
        {
            context.Clear(Colors.Transparent);
            using var glyphRun = new CoreText.Avalonia.CoreTextGlyphRunImpl(glyphTypeface, 28, glyphInfos, new Point(12, 36));
            context.DrawGlyphRun(Brushes.Black, glyphRun);
        }

        Assert.True(CountVisiblePixels(bitmap) > 50);
    }

    private static IReadOnlyList<GlyphInfo> CreateGlyphInfos(GlyphTypeface glyphTypeface, string text, double fontRenderingEmSize)
    {
        var result = new List<GlyphInfo>(text.Length);
        var scale = fontRenderingEmSize / glyphTypeface.Metrics.DesignEmHeight;

        for (var i = 0; i < text.Length; i++)
        {
            Assert.True(glyphTypeface.CharacterToGlyphMap.TryGetGlyph(text[i], out var glyphIndex));
            Assert.True(glyphTypeface.TryGetHorizontalGlyphAdvance(glyphIndex, out var advance));
            result.Add(new GlyphInfo(glyphIndex, i, advance * scale));
        }

        return result;
    }

    private static void BindTextServices()
    {
        var fontManager = new CoreText.Avalonia.CoreTextFontManagerImpl(new CoreText.Avalonia.CoreTextPlatformOptions());

        AvaloniaLocator.CurrentMutable
            .Bind<IAssetLoader>().ToConstant(new StandardAssetLoader(typeof(CoreTextTextRenderingTests).Assembly))
            .Bind<IFontManagerImpl>().ToConstant(fontManager)
            .Bind<ITextShaperImpl>().ToConstant(new CoreText.Avalonia.CoreTextTextShaper());
        
        FontManager.Current.AddFontCollection(new InterFontCollection());
    }

    private static Typeface CreateInterTypeface() => new(new FontFamily("fonts:Inter#Inter"));

    private static GlyphTypeface CreateGlyphTypeface()
    {
        Assert.True(FontManager.Current.TryGetGlyphTypeface(CreateInterTypeface(), out var glyphTypeface));
        return glyphTypeface!;
    }

    private static int CountVisiblePixels(CoreText.Avalonia.CoreTextBitmapImpl bitmap)
    {
        using var framebuffer = bitmap.Lock();
        var stride = framebuffer.RowBytes / 4;
        var visiblePixels = 0;

        for (var y = 0; y < framebuffer.Size.Height; y++)
        {
            for (var x = 0; x < framebuffer.Size.Width; x++)
            {
                var pixel = unchecked((uint)Marshal.ReadInt32(framebuffer.Address, ((y * stride) + x) * sizeof(int)));
                var alpha = (byte)(pixel >> 24);

                if (alpha > 10)
                {
                    visiblePixels++;
                }
            }
        }

        return visiblePixels;
    }
}
