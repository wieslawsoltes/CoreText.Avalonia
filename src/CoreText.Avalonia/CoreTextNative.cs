using Foundation;
using ObjCRuntime;

namespace CoreText.Avalonia;

internal static unsafe partial class CoreTextNative
{
    private const uint kCGImageAlphaPremultipliedFirst = 2;
    private const uint kCGBitmapByteOrder32Little = 2u << 12;
    private const uint kCGBitmapInfo = kCGImageAlphaPremultipliedFirst | kCGBitmapByteOrder32Little;
    private const int kCFNumberFloat64Type = 13;

    private static readonly Lazy<IntPtr> s_fontAttributeName = new(() => ReadCoreTextSymbol("kCTFontAttributeName"));
    private static readonly Lazy<IntPtr> s_foregroundColorFromContextAttributeName = new(() => ReadCoreTextSymbol("kCTForegroundColorFromContextAttributeName"));
    private static readonly Lazy<IntPtr> s_cfBooleanTrue = new(() => ReadCoreFoundationSymbol("kCFBooleanTrue"));
    private static readonly Lazy<IntPtr> s_cfDictionaryKeyCallbacks = new(() => ReadCoreFoundationExport("kCFTypeDictionaryKeyCallBacks"));
    private static readonly Lazy<IntPtr> s_cfDictionaryValueCallbacks = new(() => ReadCoreFoundationExport("kCFTypeDictionaryValueCallBacks"));
    private static readonly Lazy<IntPtr> s_fontFamilyNameAttribute = new(() => ReadCoreTextSymbol("kCTFontFamilyNameAttribute"));
    private static readonly Lazy<IntPtr> s_fontTraitsAttribute = new(() => ReadCoreTextSymbol("kCTFontTraitsAttribute"));
    private static readonly Lazy<IntPtr> s_fontWeightTrait = new(() => ReadCoreTextSymbol("kCTFontWeightTrait"));
    private static readonly Lazy<IntPtr> s_fontSlantTrait = new(() => ReadCoreTextSymbol("kCTFontSlantTrait"));

    public static IntPtr CreateBitmapContext(IntPtr data, int width, int height, int rowBytes)
    {
        var colorSpace = CGColorSpaceCreateDeviceRGB();
        try
        {
            var context = CGBitmapContextCreate((void*)data, (nuint)width, (nuint)height, 8, (nuint)rowBytes, colorSpace, kCGBitmapInfo);
            if (context == IntPtr.Zero)
            {
                throw new InvalidOperationException("Unable to create CGBitmapContext.");
            }

            return context;
        }
        finally
        {
            CGColorSpaceRelease(colorSpace);
        }
    }

    public static IntPtr CreateGradient(double[] components, double[] locations, int stopCount)
    {
        fixed (double* pComponents = components)
        fixed (double* pLocations = locations)
        {
            var colorSpace = CGColorSpaceCreateDeviceRGB();
            try
            {
                return CGGradientCreateWithColorComponents(colorSpace, pComponents, pLocations, (nuint)stopCount);
            }
            finally
            {
                CGColorSpaceRelease(colorSpace);
            }
        }
    }

    public static IntPtr CreateStyledFont(string familyName, double size, FontWeight weight, bool italic)
    {
        using var family = new CoreTextString(familyName);
        IntPtr cfWeight = IntPtr.Zero;
        IntPtr cfSlant = IntPtr.Zero;
        IntPtr traits = IntPtr.Zero;
        IntPtr attributes = IntPtr.Zero;
        IntPtr descriptor = IntPtr.Zero;
        IntPtr matchedDescriptors = IntPtr.Zero;

        try
        {
            if (weight != FontWeight.Normal || italic)
            {
                var weightValue = MapFontWeight(weight);
                cfWeight = CreateNumber(weightValue);

                var traitKeys = stackalloc IntPtr[2];
                var traitValues = stackalloc IntPtr[2];
                var traitCount = 0;

                traitKeys[traitCount] = s_fontWeightTrait.Value;
                traitValues[traitCount] = cfWeight;
                traitCount++;

                if (italic)
                {
                    cfSlant = CreateNumber(1d);
                    traitKeys[traitCount] = s_fontSlantTrait.Value;
                    traitValues[traitCount] = cfSlant;
                    traitCount++;
                }

                traits = CFDictionaryCreate(IntPtr.Zero, traitKeys, traitValues, traitCount, s_cfDictionaryKeyCallbacks.Value, s_cfDictionaryValueCallbacks.Value);
            }

            var attributeKeys = stackalloc IntPtr[2];
            var attributeValues = stackalloc IntPtr[2];
            var attributeCount = 0;

            attributeKeys[attributeCount] = s_fontFamilyNameAttribute.Value;
            attributeValues[attributeCount] = family.Handle;
            attributeCount++;

            if (traits != IntPtr.Zero)
            {
                attributeKeys[attributeCount] = s_fontTraitsAttribute.Value;
                attributeValues[attributeCount] = traits;
                attributeCount++;
            }

            attributes = CFDictionaryCreate(IntPtr.Zero, attributeKeys, attributeValues, attributeCount, s_cfDictionaryKeyCallbacks.Value, s_cfDictionaryValueCallbacks.Value);
            descriptor = CTFontDescriptorCreateWithAttributes(attributes);
            if (descriptor == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            matchedDescriptors = CTFontDescriptorCreateMatchingFontDescriptors(descriptor, IntPtr.Zero);
            if (matchedDescriptors != IntPtr.Zero && CFArrayGetCount(matchedDescriptors) > 0)
            {
                var matchedDescriptor = CFArrayGetValueAtIndex(matchedDescriptors, 0);
                if (matchedDescriptor != IntPtr.Zero)
                {
                    return CTFontCreateWithFontDescriptor(matchedDescriptor, size, IntPtr.Zero);
                }
            }

            return CTFontCreateWithFontDescriptor(descriptor, size, IntPtr.Zero);
        }
        finally
        {
            if (matchedDescriptors != IntPtr.Zero) CFRelease(matchedDescriptors);
            if (descriptor != IntPtr.Zero) CFRelease(descriptor);
            if (attributes != IntPtr.Zero) CFRelease(attributes);
            if (traits != IntPtr.Zero) CFRelease(traits);
            if (cfSlant != IntPtr.Zero) CFRelease(cfSlant);
            if (cfWeight != IntPtr.Zero) CFRelease(cfWeight);
        }
    }

    public static IntPtr ReadCoreTextSymbol(string name)
    {
        using var lib = new NativeLibraryHandle("/System/Library/Frameworks/CoreText.framework/CoreText");
        return lib.ReadPointer(name);
    }

    public static IntPtr ReadCoreFoundationSymbol(string name)
    {
        using var lib = new NativeLibraryHandle("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation");
        return lib.ReadPointer(name);
    }

    public static IntPtr ReadCoreFoundationExport(string name)
    {
        using var lib = new NativeLibraryHandle("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation");
        return lib.ReadExport(name);
    }

    public static IntPtr[] GetGlyphRuns(IntPtr line)
    {
        var array = CTLineGetGlyphRuns(line);
        var count = CFArrayGetCount(array);
        var result = new IntPtr[count];

        for (var i = 0; i < count; i++)
        {
            result[i] = CFArrayGetValueAtIndex(array, i);
        }

        return result;
    }

    public static string[] CopyAvailableFontFamilyNames()
    {
        var array = CTFontManagerCopyAvailableFontFamilyNames();
        if (array == IntPtr.Zero)
        {
            return Array.Empty<string>();
        }

        try
        {
            var count = CFArrayGetCount(array);
            var result = new string[count];

            for (var i = 0; i < count; i++)
            {
                var value = CFArrayGetValueAtIndex(array, i);
                result[i] = CFStringToString(value);
            }

            return result
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            CFRelease(array);
        }
    }

    public static string CFStringToString(IntPtr value)
    {
        if (value == IntPtr.Zero)
        {
            return string.Empty;
        }

        var length = (int)CFStringGetLength(value);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length];
        fixed (char* pBuffer = buffer)
        {
            CFStringGetCharacters(value, new CFRange(0, length), pBuffer);
        }

        return new string(buffer);
    }

    public static CoreTextAttributedString CreateAttributedString(IntPtr text, IntPtr font, bool useContextColor)
    {
        var keys = stackalloc IntPtr[2];
        var values = stackalloc IntPtr[2];
        var count = 0;

        keys[count] = s_fontAttributeName.Value;
        values[count] = font;
        count++;

        if (useContextColor)
        {
            keys[count] = s_foregroundColorFromContextAttributeName.Value;
            values[count] = s_cfBooleanTrue.Value;
            count++;
        }

        var dictionary = CFDictionaryCreate(IntPtr.Zero, keys, values, count, s_cfDictionaryKeyCallbacks.Value, s_cfDictionaryValueCallbacks.Value);
        try
        {
            return new CoreTextAttributedString(CFAttributedStringCreate(IntPtr.Zero, text, dictionary));
        }
        finally
        {
            CFRelease(dictionary);
        }
    }

    private static IntPtr CreateNumber(double value)
    {
        return CFNumberCreate(IntPtr.Zero, kCFNumberFloat64Type, &value);
    }

    private static double MapFontWeight(FontWeight weight) => (int)weight switch
    {
        <= 100 => -0.8,
        <= 200 => -0.6,
        <= 300 => -0.4,
        <= 450 => 0.0,
        <= 550 => 0.23,
        <= 650 => 0.3,
        <= 750 => 0.4,
        <= 850 => 0.56,
        _ => 0.62
    };

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static partial void CFRelease(IntPtr cfTypeRef);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static partial IntPtr CFStringCreateWithCharacters(IntPtr allocator, char* chars, nint numChars);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static partial IntPtr CFAttributedStringCreate(IntPtr allocator, IntPtr str, IntPtr attributes);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static partial IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr* keys, IntPtr* values, nint numValues, IntPtr keyCallBacks, IntPtr valueCallBacks);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static partial IntPtr CFNumberCreate(IntPtr allocator, int theType, void* valuePtr);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static partial nint CFArrayGetCount(IntPtr theArray);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static partial IntPtr CFArrayGetValueAtIndex(IntPtr theArray, nint idx);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static partial nint CFStringGetLength(IntPtr theString);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static partial void CFStringGetCharacters(IntPtr theString, CFRange range, char* buffer);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTFontCreateWithName(IntPtr name, double size, IntPtr matrix);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTFontCreateWithGraphicsFont(IntPtr graphicsFont, double size, IntPtr matrix, IntPtr attributes);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTFontCreateWithFontDescriptor(IntPtr descriptor, double size, IntPtr matrix);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTFontCreateCopyWithAttributes(IntPtr font, double size, IntPtr matrix, IntPtr attributes);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTFontDescriptorCreateWithAttributes(IntPtr attributes);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTFontDescriptorCreateMatchingFontDescriptors(IntPtr descriptor, IntPtr mandatoryAttributes);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTFontManagerCopyAvailableFontFamilyNames();

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTFontCreateForString(IntPtr currentFont, IntPtr @string, CFRange range);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTFontCreatePathForGlyph(IntPtr font, ushort glyph, ref CGAffineTransform transform);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial CGRect CTFontGetBoundingRectsForGlyphs(IntPtr font, int orientation, ref ushort glyphs, IntPtr boundingRects, nuint count);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial void CTFontDrawGlyphs(IntPtr font, ReadOnlySpan<ushort> glyphs, ReadOnlySpan<CGPoint> positions, nint count, IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTLineCreateWithAttributedString(IntPtr attrString);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial IntPtr CTLineGetGlyphRuns(IntPtr line);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial nint CTRunGetGlyphCount(IntPtr run);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial void CTRunGetGlyphs(IntPtr run, CFRange range, ushort[] buffer);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial void CTRunGetAdvances(IntPtr run, CFRange range, CGSize[] buffer);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial void CTRunGetPositions(IntPtr run, CFRange range, CGPoint[] buffer);

    [LibraryImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    internal static partial void CTRunGetStringIndices(IntPtr run, CFRange range, nint[] buffer);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial IntPtr CGColorSpaceCreateDeviceRGB();

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGColorSpaceRelease(IntPtr colorSpace);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial IntPtr CGBitmapContextCreate(void* data, nuint width, nuint height, nuint bitsPerComponent, nuint bytesPerRow, IntPtr colorSpace, uint bitmapInfo);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial IntPtr CGBitmapContextCreateImage(IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial IntPtr CGImageCreateWithImageInRect(IntPtr image, CGRect rect);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGImageRelease(IntPtr image);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial IntPtr CGGradientCreateWithColorComponents(IntPtr colorSpace, double* components, double* locations, nuint count);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextRelease(IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSaveGState(IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextRestoreGState(IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextClearRect(IntPtr context, CGRect rect);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextFillRect(IntPtr context, CGRect rect);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetRGBFillColor(IntPtr context, double red, double green, double blue, double alpha);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetRGBStrokeColor(IntPtr context, double red, double green, double blue, double alpha);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetAlpha(IntPtr context, double alpha);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetBlendMode(IntPtr context, int mode);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetInterpolationQuality(IntPtr context, int quality);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetShouldAntialias(IntPtr context, [MarshalAs(UnmanagedType.I1)] bool value);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetAllowsAntialiasing(IntPtr context, [MarshalAs(UnmanagedType.I1)] bool value);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetAllowsFontSmoothing(IntPtr context, [MarshalAs(UnmanagedType.I1)] bool value);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetShouldSmoothFonts(IntPtr context, [MarshalAs(UnmanagedType.I1)] bool value);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetShouldSubpixelPositionFonts(IntPtr context, [MarshalAs(UnmanagedType.I1)] bool value);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetShouldSubpixelQuantizeFonts(IntPtr context, [MarshalAs(UnmanagedType.I1)] bool value);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetLineWidth(IntPtr context, double width);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetLineJoin(IntPtr context, int lineJoin);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetLineCap(IntPtr context, int lineCap);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetMiterLimit(IntPtr context, double limit);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetLineDash(IntPtr context, double phase, double[] lengths, nint count);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextBeginPath(IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextMoveToPoint(IntPtr context, double x, double y);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextAddLineToPoint(IntPtr context, double x, double y);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextAddCurveToPoint(IntPtr context, double cp1x, double cp1y, double cp2x, double cp2y, double x, double y);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextAddQuadCurveToPoint(IntPtr context, double cpx, double cpy, double x, double y);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextAddArcToPoint(IntPtr context, double x1, double y1, double x2, double y2, double radius);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextAddRect(IntPtr context, CGRect rect);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextAddEllipseInRect(IntPtr context, CGRect rect);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextClosePath(IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextDrawPath(IntPtr context, int mode);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextStrokePath(IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextClip(IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextEOClip(IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextClipToRect(IntPtr context, CGRect rect);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextTranslateCTM(IntPtr context, double tx, double ty);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextScaleCTM(IntPtr context, double sx, double sy);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextConcatCTM(IntPtr context, CGAffineTransform transform);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextSetTextMatrix(IntPtr context, CGAffineTransform transform);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextAddPath(IntPtr context, IntPtr path);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial IntPtr CGContextCopyPath(IntPtr context);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextDrawImage(IntPtr context, CGRect rect, IntPtr image);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextDrawLinearGradient(IntPtr context, IntPtr gradient, CGPoint startPoint, CGPoint endPoint, uint options);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGContextDrawRadialGradient(IntPtr context, IntPtr gradient, CGPoint startCenter, double startRadius, CGPoint endCenter, double endRadius, uint options);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    internal static partial void CGPathRelease(IntPtr path);

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CFRange
    {
        public CFRange(nint location, nint length)
        {
            this.location = location;
            this.length = length;
        }

        public nint location { get; }
        public nint length { get; }

        public static CFRange All => new(0, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CGPoint
    {
        public CGPoint(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public double x { get; }
        public double y { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CGSize
    {
        public CGSize(double width, double height)
        {
            this.width = width;
            this.height = height;
        }

        public double width { get; }
        public double height { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CGRect
    {
        public CGRect(double x, double y, double width, double height)
        {
            origin = new CGPoint(x, y);
            size = new CGSize(width, height);
        }

        public CGPoint origin { get; }
        public CGSize size { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CGAffineTransform
    {
        public CGAffineTransform(double a, double b, double c, double d, double tx, double ty)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            this.tx = tx;
            this.ty = ty;
        }

        public double a { get; }
        public double b { get; }
        public double c { get; }
        public double d { get; }
        public double tx { get; }
        public double ty { get; }

        public static CGAffineTransform FromMatrix(Matrix matrix) => new(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);

        public static CGAffineTransform MakeTranslation(double x, double y) => new(1, 0, 0, 1, x, y);

        public static CGAffineTransform MakeScale(double x, double y) => new(x, 0, 0, y, 0, 0);
    }

    private sealed class NativeLibraryHandle : IDisposable
    {
        private readonly nint _handle;

        public NativeLibraryHandle(string path)
        {
            _handle = NativeLibrary.Load(path);
        }

        public IntPtr ReadPointer(string symbol)
        {
            var export = ReadExport(symbol);
            return export == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(export);
        }

        public IntPtr ReadExport(string symbol) =>
            NativeLibrary.TryGetExport(_handle, symbol, out var export) ? export : IntPtr.Zero;

        public void Dispose() => NativeLibrary.Free(_handle);
    }
}

internal sealed unsafe class CoreTextString : IDisposable
{
    public CoreTextString(ReadOnlySpan<char> text)
    {
        fixed (char* pChars = text)
        {
            Handle = CoreTextNative.CFStringCreateWithCharacters(IntPtr.Zero, pChars, text.Length);
        }
    }

    public CoreTextString(string text)
    {
        fixed (char* pChars = text)
        {
            Handle = CoreTextNative.CFStringCreateWithCharacters(IntPtr.Zero, pChars, text.Length);
        }
    }

    public IntPtr Handle { get; }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            CoreTextNative.CFRelease(Handle);
        }
    }
}

internal sealed unsafe class CoreTextAttributedString : IDisposable
{
    public CoreTextAttributedString(IntPtr handle)
    {
        Handle = handle;
    }

    public IntPtr Handle { get; }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            CoreTextNative.CFRelease(Handle);
        }
    }
}
