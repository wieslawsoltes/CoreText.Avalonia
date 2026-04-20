using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;

namespace CoreText.Avalonia;

internal sealed class CoreTextGlyphRunImpl : IGlyphRunImpl
{
    private readonly CoreTextPlatformTypeface _platformTypeface;
    private readonly ushort[] _glyphIndices;
    private readonly CoreTextNative.CGPoint[] _positions;
    private readonly Rect[] _localGlyphBounds;

    public CoreTextGlyphRunImpl(GlyphTypeface glyphTypeface, double fontRenderingEmSize, IReadOnlyList<GlyphInfo> glyphInfos, Point baselineOrigin)
    {
        if (glyphTypeface.PlatformTypeface is not CoreTextPlatformTypeface platformTypeface)
        {
            throw new NotSupportedException("GlyphTypeface is not backed by CoreText.");
        }

        _platformTypeface = platformTypeface;
        FontRenderingEmSize = fontRenderingEmSize;
        BaselineOrigin = baselineOrigin;

        _glyphIndices = new ushort[glyphInfos.Count];
        _positions = new CoreTextNative.CGPoint[glyphInfos.Count];
        _localGlyphBounds = new Rect[glyphInfos.Count];

        var currentX = baselineOrigin.X;
        var hasBounds = false;
        var bounds = default(Rect);

        using var font = _platformTypeface.CreateFont(fontRenderingEmSize);

        for (var i = 0; i < glyphInfos.Count; i++)
        {
            var info = glyphInfos[i];
            var x = currentX + info.GlyphOffset.X;
            var baselineY = baselineOrigin.Y + info.GlyphOffset.Y;

            _glyphIndices[i] = info.GlyphIndex;
            _positions[i] = new CoreTextNative.CGPoint(x, -baselineY);

            var rect = CoreTextNative.CTFontGetBoundingRectsForGlyphs(font.Handle, 0, ref _glyphIndices[i], IntPtr.Zero, 1);
            var localGlyphBounds = new Rect(
                currentX + info.GlyphOffset.X + rect.origin.x,
                info.GlyphOffset.Y - rect.size.height - rect.origin.y,
                rect.size.width,
                rect.size.height);
            _localGlyphBounds[i] = localGlyphBounds;

            var glyphBounds = localGlyphBounds.Translate(BaselineOrigin);
            bounds = hasBounds ? bounds.Union(glyphBounds) : glyphBounds;
            hasBounds = true;
            currentX += info.GlyphAdvance;
        }

        Bounds = hasBounds ? bounds : default;
    }

    public double FontRenderingEmSize { get; }

    public Point BaselineOrigin { get; }

    public Rect Bounds { get; }

    public ReadOnlySpan<ushort> GlyphIndices => _glyphIndices;

    public ReadOnlySpan<CoreTextNative.CGPoint> Positions => _positions;

    public CoreTextPlatformTypeface Typeface => _platformTypeface;

    public IReadOnlyList<float> GetIntersections(float lowerLimit, float upperLimit)
    {
        if (_localGlyphBounds.Length == 0)
        {
            return Array.Empty<float>();
        }

        List<float>? intersections = null;
        var activeStart = 0f;
        var activeEnd = 0f;
        var hasActiveRange = false;

        foreach (var glyphBounds in _localGlyphBounds)
        {
            if (glyphBounds.Width <= 0 || glyphBounds.Height <= 0)
            {
                continue;
            }

            if (upperLimit <= glyphBounds.Top || lowerLimit >= glyphBounds.Bottom)
            {
                continue;
            }

            var left = (float)glyphBounds.Left;
            var right = (float)glyphBounds.Right;

            if (!hasActiveRange)
            {
                activeStart = left;
                activeEnd = right;
                hasActiveRange = true;
                continue;
            }

            if (left <= activeEnd)
            {
                activeEnd = Math.Max(activeEnd, right);
                continue;
            }

            intersections ??= new List<float>();
            intersections.Add(activeStart);
            intersections.Add(activeEnd);
            activeStart = left;
            activeEnd = right;
        }

        if (!hasActiveRange)
        {
            return Array.Empty<float>();
        }

        intersections ??= new List<float>();
        intersections.Add(activeStart);
        intersections.Add(activeEnd);
        return intersections;
    }

    public void Dispose()
    {
    }
}
