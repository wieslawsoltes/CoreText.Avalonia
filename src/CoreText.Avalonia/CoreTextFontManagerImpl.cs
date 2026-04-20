using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Platform;
using CoreGraphics;
using CoreText;
using Foundation;
using ObjCRuntime;

namespace CoreText.Avalonia;

internal sealed class CoreTextFontManagerImpl : IFontManagerImpl
{
    private static readonly string[] s_defaultFontCandidates =
    {
        "Helvetica Neue",
        ".AppleSystemUIFont",
        "SF Pro Text",
        "Helvetica",
        "Arial"
    };

    private readonly CoreTextPlatformOptions _options;

    public CoreTextFontManagerImpl(CoreTextPlatformOptions options)
    {
        _options = options;
    }

    public string GetDefaultFontFamilyName()
    {
        var families = GetInstalledFontFamilyNames();

        foreach (var candidate in s_defaultFontCandidates)
        {
            if (families.Contains(candidate, StringComparer.OrdinalIgnoreCase) &&
                TryCreateGlyphTypeface(candidate, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out _))
            {
                return candidate;
            }
        }

        foreach (var family in families)
        {
            if (TryCreateGlyphTypeface(family, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out _))
            {
                return family;
            }
        }

        return "Helvetica";
    }

    public string[] GetInstalledFontFamilyNames(bool checkForUpdates = false) =>
        CoreTextNative.CopyAvailableFontFamilyNames();

    public bool TryMatchCharacter(
        int codepoint,
        FontStyle fontStyle,
        FontWeight fontWeight,
        FontStretch fontStretch,
        string? familyName,
        CultureInfo? culture,
        [NotNullWhen(true)] out IPlatformTypeface? platformTypeface)
    {
        var requestedFamily = string.IsNullOrWhiteSpace(familyName) ? GetDefaultFontFamilyName() : familyName;

        if (!TryCreateGlyphTypeface(requestedFamily, fontStyle, fontWeight, fontStretch, out platformTypeface))
        {
            platformTypeface = null;
            return false;
        }

        if (platformTypeface is not CoreTextPlatformTypeface baseTypeface)
        {
            platformTypeface = null;
            return false;
        }

        if (!_options.PreferPlatformFontFallback)
        {
            platformTypeface = baseTypeface;
            return true;
        }

        var ch = char.ConvertFromUtf32(codepoint);
        var matched = baseTypeface.CreateFallbackForString(ch, fontStyle, fontWeight, fontStretch);
        if (matched is null)
        {
            return false;
        }

        platformTypeface = matched;
        return true;
    }

    public bool TryCreateGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch, [NotNullWhen(true)] out IPlatformTypeface? platformTypeface)
    {
        try
        {
            platformTypeface = CoreTextPlatformTypeface.CreateFromFamily(familyName, style, weight, stretch, FontSimulations.None);
            return true;
        }
        catch
        {
            if ((style != FontStyle.Normal || weight != FontWeight.Normal) &&
                TryCreateGlyphTypeface(familyName, FontStyle.Normal, FontWeight.Normal, stretch, out platformTypeface))
            {
                return true;
            }

            platformTypeface = null;
            return false;
        }
    }

    public bool TryCreateGlyphTypeface(Stream stream, FontSimulations fontSimulations, [NotNullWhen(true)] out IPlatformTypeface? platformTypeface)
    {
        try
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var fontData = memory.ToArray();

            using var data = NSData.FromArray(fontData) ?? throw new InvalidOperationException("Unable to read font stream.");
            using var provider = new CGDataProvider(data);
            using var cgFont = CGFont.CreateFromProvider(provider) ?? throw new InvalidOperationException("Unable to decode font stream.");
            platformTypeface = CoreTextPlatformTypeface.CreateFromGraphicsFont(cgFont, fontSimulations, fontData);
            return true;
        }
        catch
        {
            platformTypeface = null;
            return false;
        }
    }

    public bool TryGetFamilyTypefaces(string familyName, [NotNullWhen(true)] out IReadOnlyList<Typeface>? familyTypefaces)
    {
        var result = new List<Typeface>();
        var candidates = new[]
        {
            new Typeface(familyName, FontStyle.Normal, FontWeight.Normal),
            new Typeface(familyName, FontStyle.Italic, FontWeight.Normal),
            new Typeface(familyName, FontStyle.Normal, FontWeight.Bold),
            new Typeface(familyName, FontStyle.Italic, FontWeight.Bold)
        };

        foreach (var typeface in candidates)
        {
            if (TryCreateGlyphTypeface(familyName, typeface.Style, typeface.Weight, typeface.Stretch, out _))
            {
                result.Add(typeface);
            }
        }

        familyTypefaces = result.Count == 0 ? null : result;
        return familyTypefaces is not null;
    }
}

internal sealed class CoreTextPlatformTypeface : IPlatformTypeface
{
    private readonly byte[]? _fontData;

    private CoreTextPlatformTypeface(string familyName, string postScriptName, FontWeight weight, FontStyle style, FontStretch stretch, FontSimulations fontSimulations, byte[]? fontData)
    {
        FamilyName = familyName;
        PostScriptName = postScriptName;
        Weight = weight;
        Style = style;
        Stretch = stretch;
        FontSimulations = fontSimulations;
        _fontData = fontData;
    }

    public string FamilyName { get; }

    public string PostScriptName { get; }

    public FontWeight Weight { get; }

    public FontStyle Style { get; }

    public FontStretch Stretch { get; }

    public FontSimulations FontSimulations { get; }

    public CTFont CreateFont(double size)
    {
        if (_fontData is { Length: > 0 })
        {
            using var data = NSData.FromArray(_fontData) ?? throw new InvalidOperationException("Unable to recreate font data.");
            using var provider = new CGDataProvider(data);
            using var graphicsFont = CGFont.CreateFromProvider(provider) ?? throw new InvalidOperationException("Unable to recreate CoreGraphics font.");
            var handle = CoreTextNative.CTFontCreateWithGraphicsFont(graphicsFont.Handle, Math.Max(1, size), IntPtr.Zero, IntPtr.Zero);
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Unable to create CoreText font '{PostScriptName}' from font data.");
            }

            return Runtime.GetINativeObject<CTFont>(handle, true)!;
        }

        return new CTFont(PostScriptName, (nfloat)Math.Max(1, size), CTFontOptions.Default);
    }

    public CoreTextPlatformTypeface? CreateFallbackForString(string text, FontStyle style, FontWeight weight, FontStretch stretch)
    {
        using var current = CreateFont(12);
        using var cfString = new CoreTextString(text);
        var range = new CoreTextNative.CFRange(0, text.Length);
        var handle = CoreTextNative.CTFontCreateForString(current.Handle, cfString.Handle, range);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        using var font = Runtime.GetINativeObject<CTFont>(handle, true)!;
        return new CoreTextPlatformTypeface(
            font.FamilyName ?? FamilyName,
            font.PostScriptName ?? PostScriptName,
            weight,
            style,
            stretch,
            FontSimulations.None,
            null);
    }

    public static CoreTextPlatformTypeface CreateFromFamily(string familyName, FontStyle style, FontWeight weight, FontStretch stretch, FontSimulations fontSimulations)
    {
        var handle = CoreTextNative.CreateStyledFont(familyName, 12, weight, style == FontStyle.Italic);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Unable to create CoreText font '{familyName}'.");
        }

        using var font = Runtime.GetINativeObject<CTFont>(handle, true)!;
        return new CoreTextPlatformTypeface(
            font.FamilyName ?? familyName,
            font.PostScriptName ?? familyName,
            weight,
            style,
            stretch,
            fontSimulations,
            null);
    }

    public static CoreTextPlatformTypeface CreateFromPostScriptName(string postScriptName, FontSimulations fontSimulations, byte[]? fontData)
    {
        using var font = new CTFont(postScriptName, 12, CTFontOptions.Default);
        var weight = font.SymbolicTraits.HasFlag(CTFontSymbolicTraits.Bold)
            ? FontWeight.Bold
            : FontWeight.Normal;
        var style = font.SymbolicTraits.HasFlag(CTFontSymbolicTraits.Italic)
            ? FontStyle.Italic
            : FontStyle.Normal;
        return new CoreTextPlatformTypeface(
            font.FamilyName ?? postScriptName,
            font.PostScriptName ?? postScriptName,
            weight,
            style,
            FontStretch.Normal,
            fontSimulations,
            fontData);
    }

    public static CoreTextPlatformTypeface CreateFromGraphicsFont(CGFont graphicsFont, FontSimulations fontSimulations, byte[] fontData)
    {
        var handle = CoreTextNative.CTFontCreateWithGraphicsFont(graphicsFont.Handle, 12, IntPtr.Zero, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            var fallbackName = graphicsFont.PostScriptName ?? "Unknown";
            return new CoreTextPlatformTypeface(
                fallbackName,
                fallbackName,
                FontWeight.Normal,
                FontStyle.Normal,
                FontStretch.Normal,
                fontSimulations,
                fontData);
        }

        using var font = Runtime.GetINativeObject<CTFont>(handle, true)!;
        var postScriptName = font.PostScriptName ?? graphicsFont.PostScriptName ?? "Unknown";
        var familyName = font.FamilyName ?? postScriptName;
        var weight = font.SymbolicTraits.HasFlag(CTFontSymbolicTraits.Bold)
            ? FontWeight.Bold
            : FontWeight.Normal;
        var style = font.SymbolicTraits.HasFlag(CTFontSymbolicTraits.Italic)
            ? FontStyle.Italic
            : FontStyle.Normal;

        return new CoreTextPlatformTypeface(
            familyName,
            postScriptName,
            weight,
            style,
            FontStretch.Normal,
            fontSimulations,
            fontData);
    }

    public bool TryGetStream([NotNullWhen(true)] out Stream? stream)
    {
        if (_fontData is not null)
        {
            stream = new MemoryStream(_fontData, writable: false);
            return true;
        }

        stream = null;
        return false;
    }

    public bool TryGetTable(OpenTypeTag tag, out ReadOnlyMemory<byte> table)
    {
        using var font = CreateFont(12);
        using var data = font.GetFontTableData((CTFontTable)(uint)tag, CTFontTableOptions.None);
        if (data is null)
        {
            table = default;
            return false;
        }

        table = data.ToArray();
        return true;
    }

    public void Dispose()
    {
    }
}
