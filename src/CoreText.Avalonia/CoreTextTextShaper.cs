using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;

namespace CoreText.Avalonia;

internal sealed class CoreTextTextShaper : ITextShaperImpl
{
    public ShapedBuffer ShapeText(ReadOnlyMemory<char> text, TextShaperOptions options)
    {
        if (text.Length == 0)
        {
            return new ShapedBuffer(text, 0, options.GlyphTypeface, options.FontRenderingEmSize, options.BidiLevel);
        }

        if (options.GlyphTypeface.PlatformTypeface is not CoreTextPlatformTypeface platformTypeface)
        {
            throw new NotSupportedException("GlyphTypeface is not backed by CoreText.");
        }

        using var font = platformTypeface.CreateFont(options.FontRenderingEmSize);
        using var textBuffer = new CoreTextString(text.Span);
        using var attrString = CoreTextNative.CreateAttributedString(textBuffer.Handle, font.Handle, useContextColor: false);
        var line = CoreTextNative.CTLineCreateWithAttributedString(attrString.Handle);
        if (line == IntPtr.Zero)
        {
            return new ShapedBuffer(text, 0, options.GlyphTypeface, options.FontRenderingEmSize, options.BidiLevel);
        }

        try
        {
            var runs = CoreTextNative.GetGlyphRuns(line);
            var totalGlyphCount = 0;

            foreach (var run in runs)
            {
                totalGlyphCount += (int)CoreTextNative.CTRunGetGlyphCount(run);
            }

            var shapedBuffer = new ShapedBuffer(text, totalGlyphCount, options.GlyphTypeface, options.FontRenderingEmSize, options.BidiLevel);
            var glyphIndex = 0;

            foreach (var run in runs)
            {
                var glyphCount = (int)CoreTextNative.CTRunGetGlyphCount(run);
                if (glyphCount == 0)
                {
                    continue;
                }

                var glyphs = new ushort[glyphCount];
                var advances = new CoreTextNative.CGSize[glyphCount];
                var positions = new CoreTextNative.CGPoint[glyphCount];
                var stringIndices = new nint[glyphCount];

                CoreTextNative.CTRunGetGlyphs(run, CoreTextNative.CFRange.All, glyphs);
                CoreTextNative.CTRunGetAdvances(run, CoreTextNative.CFRange.All, advances);
                CoreTextNative.CTRunGetPositions(run, CoreTextNative.CFRange.All, positions);
                CoreTextNative.CTRunGetStringIndices(run, CoreTextNative.CFRange.All, stringIndices);

                var nominalPenX = 0d;

                for (var i = 0; i < glyphCount; i++)
                {
                    var cluster = Math.Clamp((int)stringIndices[i], 0, text.Length);
                    var offset = new Vector(positions[i].x - nominalPenX, -positions[i].y);
                    shapedBuffer[glyphIndex++] = new GlyphInfo(glyphs[i], cluster, advances[i].width + options.LetterSpacing, offset);
                    nominalPenX += advances[i].width;
                }
            }

            return shapedBuffer;
        }
        finally
        {
            CoreTextNative.CFRelease(line);
        }
    }

    public ITextShaperTypeface CreateTypeface(GlyphTypeface glyphTypeface) => new CoreTextTextShaperTypeface(glyphTypeface);
}

internal sealed class CoreTextTextShaperTypeface : ITextShaperTypeface
{
    public CoreTextTextShaperTypeface(GlyphTypeface glyphTypeface)
    {
        GlyphTypeface = glyphTypeface;
    }

    public GlyphTypeface GlyphTypeface { get; }

    public void Dispose()
    {
    }
}
